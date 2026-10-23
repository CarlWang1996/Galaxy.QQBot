# 项目代码审查报告（第二轮·更新）

- 项目：Galaxy.QQBot
- 审查范围：`Galaxy.QQBot`、`Galaxy.QQBot.Test`（`Galaxy.QQBot.DependencyInjection` 已无源码且不在解决方案中）
- 审查日期：2026-10-23（心跳、密钥、5 秒保护及轻微问题 1-10 修复后复核）
- 构建结果：解决方案编译通过，0 警告 0 错误（使用临时 `OutputPath` 避免运行中 `Galaxy.QQBot.dll` 被占用）
- 说明：本轮仍以只读审查为主；本文档汇总第一轮修复情况与当前仍存在的问题。

---

## 结论概览

已确认修复：

- **断线重连自锁死**：已于 `ShardConnection.cs:151`、`WebSocketClient.cs:27` 修复，接收循环不再等待自己。
- **Resume 后心跳不重启**：重连 Resume 分支已调用 `EnsureHeartbeatLoopStarted()`（`ShardConnection.cs:178-179`），并新增心跳间隔校验。
- **AppSecret 泄露**：用户已到平台重置该密钥，历史中的旧密钥已失效；当前代码树仍从环境变量读取。
- **5 秒保护未真正限时**：已改为 `await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5))`（`WebSocketClient.cs:180`）。
- **轻微问题 1-5**：`LastS` 原子化、`expires_in` 兼容解析、启动 token 失败重试、等待 `Hello` 后 `Identify`、`DisposeAsync` 等待刷新任务并释放 `_readySubject`。
- **轻微问题 6-10**：`StartAsync` 幂等并等待 `READY`、WebSocket 连接使用取消令牌、补全关闭握手、接线 `OnHandlerError`、交互事件在 `finally` 中 ACK。

当前无“严重”和“中等”问题，剩余均为轻微 / 代码质量问题（约 8 项），可按迭代清理。

| 级别 | 数量 |
| --- | --- |
| 严重 | 0 |
| 中等 | 0 |
| 轻微 / 代码质量 | 约 8 |

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

### 3. 初始连接失败没有重试，且启动失败会被静默吞掉 —— ✅ 已修复

- 原问题：首次连接失败不会进入重连流程；`SocketQQBot.StartAsync` 吞掉每个 shard 的异常，全部失败也返回成功。
- 修复：
  - `ShardConnection.StartAsync` 改为调用 `TryReconnectAsync(InitialConnectMaxAttempts, immediateFirstAttempt: true)`（`ShardConnection.cs:71-74`）：首次立即尝试，失败后按 1/2/4/8/16 秒退避，最多 5 次；耗尽后抛 `InvalidOperationException`（内含最后一次异常）。
  - `TryReconnectAsync` 新增 `maxAttempts` / `immediateFirstAttempt` 参数；仅在“耗尽”时抛出，成功返回后的窗口断线仍由 `finally` 补偿（`ShardConnection.cs:157-230`）。
  - `SocketQQBot.StartAsync` 收集每个 shard 的失败（`SocketQQBot.cs:69-105`）：全部失败抛 `AggregateException`，部分失败记录警告后继续，不再静默。
- 说明：运行期断线仍走无限重连；只有初始启动有次数上限。

### 4. 终态断开（4914/4915/4013/4014）没有停止心跳循环 —— ✅ 已修复

- 修复：终态分支设置 `_terminal = true` 并调用 `StopHeartbeatLoop()`（`ShardConnection.cs:129-135`）；`EnsureHeartbeatLoopStarted` 增加 `_terminal` 守卫（`ShardConnection.cs:462`），避免与重连线程竞态时又把心跳拉起来。

### 5. 请求体不会忽略 null 字段 —— ✅ 已修复

- 修复：
  - `JsonHelper` 新增共享 `JsonSerializerOptions`，设置 `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`，`Serialize` 改为传入该 options（`JsonHelper.cs:8-20`）。
  - **关键**：HTTP 请求原来走 `JsonContent.Create(data)`，不会使用 `JsonHelper` 的 options；已在 `HttpHelper` 的 POST/PUT 中显式传入 `JsonHelper.Options`（`HttpHelper.cs:63`、`:70`、`:78`），否则该修复对实际发消息不生效。
- 验证：`SendBotMessageRequest` 的 `MsgSeq = null` 等字段不再输出；编译 0 警告 0 错误。

### 6. 多分片下 `Subject<T>` 并发不安全，且示例订阅是 async void —— ✅ 已修复

- 修复：
  - `SocketQQBot` 新增内部 `SynchronizedSubject<T>`，用 `Subject.Synchronize` 包装 `OnNext`；5 个事件 Subject 全部替换（`SocketQQBot.cs:29-33`、`:174-189`），多分片并发推送不再直接调用裸 `Subject.OnNext`。
  - 测试示例改为 `Task.Run` + `try/catch`，不再使用 `Subscribe(async s => ...)` 的 async void 形式（`Galaxy.QQBot.Test/Program.cs:19-30`）。
- 说明：订阅方仍应避免在回调里长时间同步阻塞；需要异步处理时按示例显式包裹并捕获异常。

### 7. C2C 回复使用了 `Author.Id` 而不是 `Author.UserOpenid` —— ✅ 已修复

- 修复：`C2cEventData.ReplyAsync` 改用 `author.user_openid`，并先取局部变量做空值校验（`C2cEventData.cs:50-59`）。空值改为抛 `InvalidOperationException`（原 `NullReferenceException` 语义不合适），同时消除可空性告警。
- 验证：编译 0 警告 0 错误。

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
- 终态分支现已在问题 4 的修复中调用 `StopHeartbeatLoop()`。

---

## 三、轻微 / 代码质量

1. **`LastS` 跨线程读写非原子 —— ✅ 已修复**：`BotSessionContext` 改用 `int` 后备字段（`-1` 表示 null），读写分别通过 `Volatile.Read` / `Volatile.Write`，接收线程写入与心跳线程读取不再有撕裂风险。
2. **`expires_in` 解析不健壮 —— ✅ 已修复**：新增 `TryGetExpiresIn`，同时兼容 `string` 与 `number`（含整数值的浮点数），非法值记录警告后进入 5 秒重试，不再抛异常。
3. **首次 token 获取失败会让启动直接失败 —— ✅ 已修复**：`WaitForTokenAsync` 在单次获取失败后继续等待后台刷新循环的下一轮（直到取消）；`StartAsync` 不再因瞬时失败直接退出。
4. **Identify 发送早于 Hello —— ✅ 已修复**：每次新建连接时创建 `TaskCompletionSource`，`ConnectAsync` 等待 `Hello`（30 秒超时）后才发送 `Identify`/`Resume`；超时或 Hello 解析失败会断开当前连接并走重连。
5. **`DisposeAsync` 细节 —— ✅ 已修复**：`StopTokenRefreshLoopAsync` 取消后等待 `_tokenRefreshTask` 退出，再释放 `_httpHelper`；token 请求也传入取消令牌；`_readySubject` 已补充 Dispose。
6. **`StartAsync` 非幂等 —— ✅ 已修复**：新增 `_started` 原子守卫，重复调用抛 `InvalidOperationException`；`ShardConnection.StartAsync` 等待首个 `READY`/`RESUMED`（30 秒超时）后才返回，失败的 shard 会被释放并从列表移除。
7. **WebSocket 连接未使用 shard 的取消令牌 —— ✅ 已修复（代码中已生效）**：`ShardConnection.ConnectAsync` 调用 `_webSocket.ConnectAsync(response.url, _linkedSource.Token)`，gateway 请求也传入同一令牌。
8. **关闭握手未完成 —— ✅ 已修复**：接收循环收到 Close 且状态为 `CloseReceived` 时调用 `CloseOutputAsync`（5 秒超时）；`DisconnectAsync` 同样处理 `CloseReceived`，并用局部 socket 避免并发置空新连接。
9. **`OnHandlerError` 未接线 —— ✅ 已修复**：`ShardConnection` 构造时将其接到 logger，消息处理器、断开回调、关闭握手中的异常都会被记录。
10. **交互事件先发消息后 ACK —— ✅ 已修复**：`InteractionEventData.ReplyAsync` 将发送逻辑包在 `try/finally`，无论发送成功与否都会调用 `ResponedAsync(Id)`。
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
18. **`EnsureHeartbeatLoopStarted` 抛异常的副作用**：`ShardConnection.cs:458-465` 在 `HeartBeatInterval <= 0` 时抛 `InvalidOperationException`。这消除了 `Task.Delay(0)` 紧循环，但如果 `Hello` 解析失败后收到 `READY`，异常会让整个 READY 处理中断（SessionId 不会被设置）。建议改为“记录警告 + 使用默认间隔（如 30s）”而不是抛异常。
19. **重连任务无法在 Dispose 时等待 —— ✅ 已修复**：`ShardConnection` 保存 `_reconnectTask`，`DisposeAsync` 先取消再等待其退出，并再次停止心跳；同时 WebSocket 连接与 gateway 请求使用取消令牌，避免释放资源时仍有重连流程在跑。

---

## 四、第一轮 / 第二轮问题修复情况

以下问题在当前代码中已确认修复：

- ✅ **断线重连自锁死**：已通过 `_ = ReconnectSafeAsync()`、`_cts?.Cancel()` 前置、`StopReceiveLoopAsync` 等待保护解决（残留见轻微问题 17、19）。
- ✅ **重连 Task 未观察**：已改为 `ReconnectSafeAsync()` 包装并在外层记录异常（`ShardConnection.cs:154`、`:234`）。
- ✅ **重连窗口期丢断开通知**：`finally` 中复位 guard 后已补一次 `IsConnected` 检查（`ShardConnection.cs:223-230`）。
- ✅ **重连兜底绕过终态码**：已增加 `_terminal` 标记并在 `finally` 中排除（`ShardConnection.cs:129-135`、`:227`）。
- ✅ **初始连接失败无重试 / 启动失败被吞**：`ShardConnection.StartAsync` 改为最多 5 次、首次立即尝试的退避重试，耗尽后抛出；`SocketQQBot.StartAsync` 汇总失败，全部失败抛 `AggregateException`，部分失败记录警告（`ShardConnection.cs:71-74`、`SocketQQBot.cs:69-105`）。
- ✅ **终态断开未停心跳**：终态分支调用 `StopHeartbeatLoop()`，且 `EnsureHeartbeatLoopStarted` 增加 `_terminal` 守卫（`ShardConnection.cs:129-135`、`:462`）。
- ✅ **Resume 成功后心跳不重启**：已在 `ResumeAsync()` 后调用 `EnsureHeartbeatLoopStarted()`（`ShardConnection.cs:194-195`）。
- ✅ **请求体未忽略 null**：`JsonHelper` 增加共享 options，并在 `HttpHelper` 的 `JsonContent.Create` 中显式传入（否则实际 HTTP 请求不会生效）。
- ✅ **Subject 并发 / async void 示例**：`SocketQQBot` 用 `Subject.Synchronize` 包装 `OnNext`；测试示例改为显式 `Task.Run` + `try/catch`。
- ✅ **C2C 使用 openid**：`C2cEventData.ReplyAsync` 改为 `Author.UserOpenid` 并做空值校验。
- ✅ **重连任务未在 Dispose 时等待**：保存 `_reconnectTask`，`DisposeAsync` 取消后等待其退出；`ConnectAsync` 的 HTTP/WebSocket 调用传入 `_linkedSource.Token`，确保取消能及时中断。
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
- ✅ 4914/4915 被内部重连：WebSocketClient 不再自动重连；终态心跳已在问题 4 修复。

---

## 五、建议修复优先级

1. 其余轻微问题按迭代逐步清理（见第三节）。
