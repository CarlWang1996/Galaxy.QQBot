# 项目代码审查报告（第二轮·更新）

- 项目：Galaxy.QQBot
- 审查范围：`Galaxy.QQBot`、`Galaxy.QQBot.Test`（`Galaxy.QQBot.DependencyInjection` 已无源码且不在解决方案中）
- 审查日期：2026-10-23（心跳、密钥、5 秒保护修复后复核）
- 构建结果：`Galaxy.QQBot` 编译通过，0 警告 0 错误；整个解决方案因程序正在运行、`bin\...\Galaxy.QQBot.dll` 被占用而无法完成构建（环境问题，非代码错误）
- 说明：本轮仍以只读审查为主；本文档汇总第一轮修复情况与当前仍存在的问题。

---

## 结论概览

已确认修复：

- **断线重连自锁死**：已于 `ShardConnection.cs:151`、`WebSocketClient.cs:27` 修复，接收循环不再等待自己。
- **Resume 后心跳不重启**：重连 Resume 分支已调用 `EnsureHeartbeatLoopStarted()`（`ShardConnection.cs:178-179`），并新增心跳间隔校验。
- **AppSecret 泄露**：用户已到平台重置该密钥，历史中的旧密钥已失效；当前代码树仍从环境变量读取。
- **5 秒保护未真正限时**：已改为 `await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5))`（`WebSocketClient.cs:180`）。

当前已无“严重”问题。剩余重点是：

1. **首次连接失败没有重试**，且启动失败会被静默吞掉（见问题 3）。
2. 终态断开未停心跳、请求体不忽略 null、`Subject` 并发 / async void、C2C openid 等（见问题 4-7）。
3. 重连 Task 已改为可观察；可选保存句柄以便 `DisposeAsync` 等待（见轻微问题 19）。

| 级别 | 数量 |
| --- | --- |
| 严重 | 0 |
| 中等 | 6 |
| 轻微 / 代码质量 | 约 19 |

---

## 一、重连修复复核（已按建议修改）

### 1. `_ = TryReconnectAsync()` 未观察异常 —— ✅ 已修复

- `ShardConnection.cs:151` 已改为 `_ = ReconnectSafeAsync()`；
- `ShardConnection.cs:211` 新增包装方法，统一 `catch` 并记录日志，异常不会再无人观察；
- `Galaxy.QQBot` 编译通过，0 警告 0 错误；
- 可选后续：保存 `_reconnectTask`，便于 `DisposeAsync` 等待（轻微问题 19）。

### 2. 重连窗口期丢断开通知 —— ✅ 已修复（但引入新问题，见问题 8）

- `ShardConnection.cs:200-208` 的 `finally` 已先复位 `_reconnecting`，再在“未连接且未取消”时补一次 `ReconnectSafeAsync()`；
- 正确性：复位后新任务能重新拿到 guard，被忽略的断开通知不会导致 shard 永久离线；正常成功路径 `IsConnected == true` 不会重复连接，关闭阶段（`_linkedSource` 已取消）会跳过；
- 终态码（4914/4915/4013/4014）现已通过 `_terminal` 标记排除，避免兜底逻辑反复重连，详见问题 8。

---

## 二、中等问题

### 3. 初始连接失败没有重试，且启动失败会被静默吞掉

- 位置：
  - `Galaxy.QQBot/Bot/SocketQQBot.cs:69-92`（逐 shard `StartAsync`，异常只记日志）
  - `Galaxy.QQBot/Bot/ShardConnection.cs:67-71`（`StartAsync` = `ConnectAsync` + `IdentifyAsync`，无重试）
- 问题：
  - `TryReconnectAsync` 只在“已建立的连接断开”时才会被触发；如果**首次连接**就失败（网络、网关、token 等），不会进入重连流程，该 shard 就此失效。
  - `SocketQQBot.StartAsync` 捕获异常后继续，即使所有 shard 都失败也返回成功，调用方无法感知“其实没连上”。
- 建议：首次连接失败也走统一的重试/退避逻辑；全部 shard 失败时向上抛出或提供明确的状态回调。

### 4. 终态断开（4914/4915/4013/4014）没有停止心跳循环

- 位置：`Galaxy.QQBot/Bot/ShardConnection.cs:126-130`
- 问题：该分支直接 `return`，但心跳循环仍在后台运行，会持续向已断开的 socket 发送心跳并反复报错（日志刷屏）。
- 建议：该分支中调用 `StopHeartbeatLoop()`，并标记 shard 为终止状态。

### 5. 请求体不会忽略 null 字段

- 位置：
  - `Galaxy.QQBot/Tool/JsonHelper.cs:14`（默认 `JsonSerializer.Serialize`）
  - `Galaxy.QQBot/Models/Message/SendBotMessageRequest.cs:7-38`（大量可空字段）
  - `Galaxy.QQBot/Manager/BotMessageManager.cs:63`（未传 sequence 时 `MsgSeq = null`）
- 问题：System.Text.Json 默认不忽略 null，`SendBotMessageRequest` 会序列化出 `"content": null`、`"msg_id": null`、`"event_id": null`、`"msg_seq": null`、`"markdown": null` 等字段。QQ 接口对部分字段的显式 null 可能返回 invalid request（如 11255）。
- 建议：配置 `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`，或对相关属性加 `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`。

### 6. 多分片下 `Subject<T>` 并发不安全，且示例订阅是 async void

- 位置：
  - `Galaxy.QQBot/Bot/SocketQQBot.cs:29-38`（Subject 定义）
  - `Galaxy.QQBot/Bot/SocketQQBot.cs:77-81`（每个 ShardConnection 的接收线程都会调用 `OnNext`）
  - `Galaxy.QQBot.Test/Program.cs:19`（`Subscribe(async s => ...)`）
- 问题：
  - 当 `shards > 1` 时，多个接收线程并发调用同一个 `Subject<T>.OnNext`；Rx 的 `Subject<T>` 不保证并发安全，可能丢事件或抛异常。
  - `IObservable<T>.Subscribe(Action<T>)` 接收的是 `Action<T>`，`async` 表达式会变成 **async void**；`await` 之后的异常无法被捕获，可能直接终结进程。
- 建议：使用 `Subject.Synchronize(...)` 或加锁后再 `OnNext`；文档/示例中改用 `SelectMany` 或显式 `Task.Run` 并处理异常，避免 async void。

### 7. C2C 回复使用了 `Author.Id` 而不是 `Author.UserOpenid`

- 位置：`Galaxy.QQBot/EventData/C2cEventData.cs:52`
- 问题：官方单聊事件说明中发送目标应使用 `author.user_openid`。示例里 `id` 与 `user_openid` 通常相同，但字段语义不同，存在风险。
- 建议：改为 `userId: Author.UserOpenid!`（并做空值校验）。

### 8. 重连兜底会绕过终态码（4914/4915/4013/4014）的停止逻辑 —— ✅ 已修复

- 原问题：`finally` 的窗口补偿没有排除终态码；重连期间收到 4914/4915/4013/4014 时，本应停止重连，却会被兜底逻辑反复重连。
- 修复：
  - `HandleDisconnectedAsync` 终态分支设置 `_terminal = true`（`ShardConnection.cs:128-132`）；
  - `finally` 的补偿条件增加 `!_terminal`（`ShardConnection.cs:206`）：

    ```csharp
    if (!_terminal && !_linkedSource.IsCancellationRequested && !_webSocket.IsConnected)
    {
        _ = ReconnectSafeAsync();
    }
    ```

- 验证：`Galaxy.QQBot` 编译 0 警告 0 错误。
- 注：本次只修兜底重连；终态分支仍未调用 `StopHeartbeatLoop()`，见问题 4。

---

## 三、轻微 / 代码质量

1. **`LastS` 跨线程读写非原子**：`int?` 在接收线程写（`ShardConnection.cs:302`）、心跳线程读（`HeartbeatManager.cs:32`），`BotSessionContext.cs:5`。建议用 `Volatile`/`Interlocked` 或原子包装类型。
2. **`expires_in` 用 `GetString()` 解析**：`AccessTokenManager.cs:72`。官方字段表标注为 number、示例是 string；若某环境返回数字会抛异常并进入 5 秒重试。建议兼容 `number` 和 `string`。
3. **首次 token 获取失败会让启动直接失败**：`AccessTokenManager.WaitForTokenAsync` 会把单次失败暴露给调用方，`SocketQQBot.StartAsync`（`:59`）随即抛出，而后台刷新循环其实还会继续重试。建议启动阶段对瞬时失败做重试/等待。
4. **Identify 发送早于 Hello**：`ShardConnection.cs:69-70` 连接后立刻 `IdentifyAsync`（`:87-103`），严格按协议应等待 `Hello`，风险较低但建议按协议处理。
5. **`DisposeAsync` 细节**：`SocketQQBot.cs:143-159` 未等待 `_tokenRefreshTask` 就 `_httpHelper.Dispose()`（`:146`、`:153`），可能在途请求抛 `ObjectDisposedException`；`_readySubject` 漏了 Dispose（`:155-158`）。
6. **`StartAsync` 非幂等**：`SocketQQBot.cs:56-95` 未清空 `_shards`，重复调用会产生重复连接；也不等待 READY 就返回。
7. **WebSocket 连接未使用 shard 的取消令牌**：`ShardConnection.cs:83` 调用 `_webSocket.ConnectAsync(response.url)` 未传 `_linkedSource.Token`，关闭/取消时可能等待网络超时。
8. **关闭握手未完成**：`WebSocketClient.cs:96-100` 收到 Close 后直接返回，未 `CloseOutputAsync`；`Dispose` 时状态已是 `CloseReceived`，不会再发关闭帧（`WebSocketClient.cs:194`）。
9. **`OnHandlerError` 未接线**：`WebSocketClient.cs:15` 定义了错误回调，但 `ShardConnection.cs:57-58` 没有设置，消息处理器异常会被静默吞掉。
10. **交互事件先发消息后 ACK**：`InteractionEventData.cs:79` 在发送成功后才 `ResponedAsync`；若发送抛异常则不会 ACK，可能导致平台侧交互失败。建议在 `finally` 中 ACK。
11. **上传/哈希细节**：
    - `BotMessageManager.cs:139` 的 `isLastBlock` 声明后未使用；
    - `ReadFullyAsync`（`BotMessageManager.cs:180-195`）未接收取消令牌；
    - `mergeResp?.FileInfo!`（`:176-177`）在响应异常时可能返回 null 却标记为非空；
    - 计算 md5 / sha1 / md5_10m 需要完整读取文件约 2.5 遍（`BotMessageManager.cs:107-109`），可合并为单次读取。
12. **`message_scene.ext` 仍按 `string[]` 建模**：`Models/Common/BotMessageScene.cs:10-11`。目前官方示例是数组，风险较低，但建议确认元素类型。
13. **遗留空项目**：`Galaxy.QQBot.DependencyInjection/` 只剩 csproj、无源码且不在 `Galaxy.QQBot.slnx` 中，建议删除或恢复实现。
14. **测试项目清理**：`Galaxy.QQBot.Test/Galaxy.QQBot.Test.csproj` 仍引用 `Microsoft.Extensions.Hosting`、`Serilog.AspNetCore` 等未使用的包；`Program.cs:18-25` 未 `await using` bot，`Console.Read()` 后没有优雅释放。
15. **命名/结构**：`ResponedAsync` 应为 `RespondedAsync`（`BotMessageManager.cs:91`）；`PostAsyncWithOutToken` 应为 `WithoutToken`（`HttpHelper.cs:67`）；`FileMd5Helper`/`FileHashHelper` 重复且位于全局命名空间，建议合并到 `Galaxy.QQBot.Tool`。
16. **`BotMessageManager` / `AccessTokenManager` 直接读取 `AccessToken`**：在未启动或 token 过期瞬间可能为空字符串，建议发送前确保 token 可用。
17. **`StopReceiveLoopAsync` 超时后静默继续**：`WebSocketClient.cs:178-186` 的 `catch { }` 会在 5 秒超时后照样把 `_receiveTask` 置 null，旧接收循环可能仍在运行，随后访问已释放的 socket 再抛异常退出。不会再死锁，但建议至少对 `TimeoutException` 记录一条警告。
18. **`EnsureHeartbeatLoopStarted` 抛异常的副作用**：`ShardConnection.cs:414-421` 在 `HeartBeatInterval <= 0` 时抛 `InvalidOperationException`。这消除了 `Task.Delay(0)` 紧循环，但如果 `Hello` 解析失败后收到 `READY`，异常会让整个 READY 处理中断（SessionId 不会被设置）。建议改为“记录警告 + 使用默认间隔（如 30s）”而不是抛异常。
19. **重连任务无法在 Dispose 时等待**：与问题 1 相关，建议保存 `_reconnectTask`，在 `ShardConnection.DisposeAsync` 中等待/取消后退出，避免释放资源时重连流程仍在跑。

---

## 四、第一轮 / 第二轮问题修复情况

以下问题在当前代码中已确认修复：

- ✅ **断线重连自锁死**：已通过 `_ = ReconnectSafeAsync()`、`_cts?.Cancel()` 前置、`StopReceiveLoopAsync` 等待保护解决（残留见轻微问题 17、19）。
- ✅ **重连 Task 未观察**：已改为 `ReconnectSafeAsync()` 包装并在外层记录异常（`ShardConnection.cs:151`、`:211`）。
- ✅ **重连窗口期丢断开通知**：`finally` 中复位 guard 后已补一次 `IsConnected` 检查（`ShardConnection.cs:200-208`）。
- ✅ **重连兜底绕过终态码**：已增加 `_terminal` 标记并在 `finally` 中排除（`ShardConnection.cs:128-132`、`:206`）。
- ✅ **Resume 成功后心跳不重启**：已在 `ResumeAsync()` 后调用 `EnsureHeartbeatLoopStarted()`（`ShardConnection.cs:178-179`）。
- ✅ **AppSecret 明文**：当前代码树已改为环境变量；用户已在平台重置密钥，历史中的旧密钥已失效。若仓库仍需对外分享，建议仍用 `git filter-repo` / BFG 清理历史。
- ✅ **5 秒保护并未真正限时**：已改为 `await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5))`。
- ✅ access_token 明文日志：已移除，仅记录“获取成功”。
- ✅ 双套重连 / 无限递归：已移除 WebSocketClient 内部自动重连，改为 ShardConnection 单一入口。
- ✅ `GroupEventData` / `BotMsgElement` 字段不反序列化：已全部改为带 `[JsonPropertyName]` 的属性。
- ✅ WebSocketClient 空二进制心跳：已删除，统一由 HeartbeatManager 发送 op1。
- ✅ 每次 READY 重复启动心跳：已通过 `_heartbeatTask` 判重。
- ✅ 心跳间隔为 0 的紧循环风险：已通过启动前校验消除（副作用见轻微问题 18）。
- ✅ 每次 Connect 重复启动 token 刷新：已通过 `_tokenRefreshTask` 判重。
- ✅ TCS 永久 faulted：失败后会重建 TCS。
- ✅ 分块上传单次 `ReadAsync` 读不满：已改为 `ReadFullyAsync` 并校验分块/总大小。
- ✅ 单条事件异常拖垮连接：WS 层与事件处理层均已加 try/catch。
- ✅ 旧接收循环读取新 socket：接收循环改为捕获局部 socket，并校验引用。
- ✅ op1 / op7 / op9 未处理：均已实现。
- ✅ 分片只连 shard 0：已按 `shards` 循环建立连接。
- ✅ HTTP 请求/响应未释放：已改为 `using`。
- ✅ DI 生命周期错配：DI 项目已移除（遗留空目录待清理）。
- ✅ 4914/4915 被内部重连：WebSocketClient 不再自动重连（但需补停止心跳，见中等问题 4）。

---

## 五、建议修复优先级

1. 首次连接失败重试 + 启动失败上报（中等问题 3）。
2. 终态断开停止心跳（中等问题 4）。
3. 保存重连任务句柄（轻微问题 19）。
4. 请求体忽略 null（中等问题 5）与 Subject 并发 / async void（中等问题 6）。
5. C2C openid 字段修正（中等问题 7）。
6. 其余轻微问题按迭代逐步清理。
