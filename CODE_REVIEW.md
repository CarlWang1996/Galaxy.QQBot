# 项目代码审查报告

- 项目：Galaxy.QQBot
- 审查范围：`Galaxy.QQBot`、`Galaxy.QQBot.DependencyInjection`、`Galaxy.QQBot.Test`
- 构建结果：`dotnet build` 通过（0 警告 0 错误）
- 说明：本文档仅记录发现的问题与修复建议，审查过程中未修改任何业务代码。

---

## 一、严重问题

### 1. AppSecret 明文硬编码并提交到仓库

- 位置：`Galaxy.QQBot.Test/Program.cs:21`、`Galaxy.QQBot.Test/Program.cs:28`
- 问题：真实的 `AppId` / `AppSecret` 以字符串字面量写在测试入口中，会随 Git 提交泄露。
- 建议：
  - 立即到 QQ 开放平台重置该密钥。
  - 改为从环境变量、User Secrets 或配置文件读取，且配置文件不提交。
  - 清理 Git 历史中的密钥（仅删除当前文件不足以消除泄露）。

### 2. access_token 被明文写入日志

- 位置：`Galaxy.QQBot/Manager/AccessTokenManager.cs:83`
- 问题：`_logger.LogInformation("获取accessToken成功:{0}", AccessToken)` 会打印完整凭证。
- 建议：只记录过期时间等非敏感信息，或对 token 做脱敏处理。

### 3. 断线重连存在无限递归与双重重连

- 位置：
  - `Galaxy.QQBot/Tool/WebSocketClient.cs:60`（连接失败触发 `OnDisconnected`）
  - `Galaxy.QQBot/Bot/SocketQQBot.cs:95`（处理器中再次调用 `ConnectAsync()`）
  - `Galaxy.QQBot/Tool/WebSocketClient.cs:151`、`:159`、`:171`、`:208`（内部自带 `AttemptReconnectAsync`）
- 问题：
  - `ConnectInternalAsync` 连接失败 -> 触发 `OnDisconnected` -> `HandleDisconnectedAsync` 再次 `ConnectAsync()` -> 再次失败 -> 再触发……形成无限递归，持续断网时可能栈溢出。
  - `WebSocketClient` 内部有一整套重连逻辑，`SocketQQBot` 又有一套，两套同时工作会互相争抢连接状态。
- 建议：只保留一套重连状态机，用锁/标志位保证同一时刻只有一个重连流程；`OnDisconnected` 只做通知，不直接递归重连。

### 4. GroupEventData 的字段永远不会被反序列化

- 位置：
  - `Galaxy.QQBot/EventData/GroupEventData.cs:15-18`
  - `Galaxy.QQBot/Models/Event/BotMsgElement.cs:15`
- 问题：这些成员声明为**字段**（如 `public BotMessageAttachment[] attachments = [];`），而 System.Text.Json 默认只填充公共**属性**，因此 `attachments`、`mentions`、`ark_data`、`msg_elements` 永远是初始空值。
- 建议：改为属性（`public BotMessageAttachment[] Attachments { get; set; } = [];`），或在序列化选项中开启 `IncludeFields = true`（不推荐，影响面大）。

### 5. WebSocketClient 发送错误的“心跳帧”

- 位置：`Galaxy.QQBot/Tool/WebSocketClient.cs:174-193`
- 问题：每 30 秒发送一次空的 `WebSocketMessageType.Binary` 帧。QQ 网关心跳协议要求发送 `{"op":1,"d":<最新s>}` 的 JSON 文本帧；空二进制帧可能被服务端视为非法数据并断开。
- 建议：删除这层心跳，统一由 `HeartbeatManager` 负责；或改为发送正确的 op1 JSON。

### 6. 每次 READY 都会新开一个心跳循环

- 位置：`Galaxy.QQBot/Bot/SocketQQBot.cs:122`
- 问题：`_ = _heartbeatManager.RunAsync(_source.Token)` 在每次 READY 时都 fire-and-forget 启动新循环，重连后会话会并发多个心跳循环，无法停止旧循环。
- 建议：用标志位或保存 `Task` 判重，重连/重新登录前先取消旧循环。

### 7. 每次 ConnectAsync 都会新开一个 token 刷新循环

- 位置：`Galaxy.QQBot/Bot/SocketQQBot.cs:165`
- 问题：`StartGetAccessToken` 是 `do...while` 无限循环，`ConnectAsync` 每次都 `_ = ...` 启动一份且从不取消旧任务，重连几次就有多个循环并发刷新、并发写 token。
- 建议：token 刷新只启动一次；重连复用已有刷新任务。

### 8. token 获取失败后 TaskCompletionSource 永久 faulted

- 位置：`Galaxy.QQBot/Manager/AccessTokenManager.cs:66-69`、`:82`
- 问题：首次失败时 `TrySetException` 后 TCS 永久处于 faulted 状态，后续即使成功也无法复位（`TrySetResult` 失败）；正在等待的 `WaitForTokenAsync` 会立即抛异常，导致无法连接。
- 建议：失败后重建 TCS，或改为基于重试的轮询/信号量实现。

### 9. 文件分块上传可能损坏文件

- 位置：`Galaxy.QQBot/Manager/BotMessageManager.cs:146-166`
- 问题：
  - 每个 block 只调用一次 `ReadAsync`，流读取不保证一次读满请求长度，可能只读到部分数据。
  - 读到 0 字节时直接 `break`，会静默跳过后续分块。
  - `block_size` 上报的是本次实际读取的 `n`，与文件总大小不一致时合并会失败。
- 建议：循环读取直到填满 block 缓冲区（或文件结束），并校验已上传总大小等于 `FileInfo.Length`。

### 10. 单条消息解析异常会拖垮整条连接

- 位置：
  - `Galaxy.QQBot/Tool/WebSocketClient.cs:131-133`、`:148-152`
  - `Galaxy.QQBot/Bot/SocketQQBot.cs:109`、`:141`、`:147`、`:152`、`:157`
- 问题：`OnMessageReceived` 被直接 await，事件处理器中的反序列化异常会冒泡到接收循环的 `catch`，被当成“接收异常”处理并触发断线重连。一条格式异常的事件即可导致连接反复重连。
- 建议：在 `HandleMessageAsync` 内部按事件做 try/catch，单条消息失败只记录日志，不影响连接；`ReceiveLoop` 只捕获真正的 socket 异常。

### 11. 重连后旧接收循环会读取新的 socket

- 位置：`Galaxy.QQBot/Tool/WebSocketClient.cs:51-52`、`:109`、`:117`
- 问题：`ConnectInternalAsync` 直接替换 `_webSocket` 字段，而旧 `ReceiveLoopAsync` 每轮都读取该字段，重连后旧循环会读到新 socket，导致同一 socket 上并发 `ReceiveAsync`，抛出 “There is already one outstanding ReceiveAsync” 之类的异常。
- 建议：接收循环捕获当时的 socket 局部引用；重连前确保旧循环已退出。

### 12. 未处理 op7 / op9 / 服务端 op1

- 位置：`Galaxy.QQBot/Bot/SocketQQBot.cs:111-160`
- 问题：
  - 未处理 `OpCode.Reconnect(7)`：服务端要求重连时无响应。
  - 未处理 `OpCode.Invalid_Session(9)`：session 失效后无法清理 session 并重新 identify，只能等超时。
  - 未处理服务端主动下发的 `OpCode.Heartbeat(1)`：无法即时响应心跳请求。
- 建议：补齐各 OpCode 分支；收到 9 时清空 `SessionId/LastS` 并重新登录。

---

## 二、中等问题

### 13. Dispose 未释放 WebSocket 与 CTS

- 位置：`Galaxy.QQBot/Bot/SocketQQBot.cs:190-193`
- 问题：`Dispose()` 只调用了 `_source.Cancel()`，没有释放 `_webSocket`（实现了 `IAsyncDisposable`），也没有释放 `_source`，且没有取消事件订阅。
- 建议：实现 `IAsyncDisposable`，依次取消 CTS、`await _webSocket.DisposeAsync()`、释放 CTS。

### 14. HTTP 请求/响应对象未释放

- 位置：`Galaxy.QQBot/Tool/HttpHelper.cs:52-91`
- 问题：除 `PutAsyncWithoutToken` 外，`HttpRequestMessage` 都未 `using`；`HttpResponseMessage` 读取后也未 Dispose。高频发送消息时会累积资源。
- 建议：统一 `using var request`，并在读取响应体后 `using`/Dispose 响应。

### 15. DI 生命周期错配（captive dependency）

- 位置：`Galaxy.QQBot.DependencyInjection/ServiceCollectionExtensions.cs:16`、`:24`、`:25`
- 问题：`IHttpHelper` 通过 `AddHttpClient` 注册为 transient，却被注入到 singleton 的 `AccessTokenManager` / `BotMessageManager`，导致 typed client 实际上被单例捕获，连接池与 handler 轮换失效。
- 建议：将这两个服务改为 `Scoped`/`Transient`，或把 `HttpClient` 改由 `IHttpClientFactory` 按需创建。

### 16. 分片数 >1 时只连接 shard 0

- 位置：`Galaxy.QQBot/Bot/SocketQQBot.cs:182`
- 问题：`shard = new int[] { 0, _options.Shards }` 固定使用分片索引 0；当 `Shards > 1` 时其余分片从未建立连接，会漏收事件。
- 建议：按 shard 数量循环建立多个连接，每个连接使用对应索引。

### 17. 4914/4915 仍会被 WebSocketClient 重连

- 位置：`Galaxy.QQBot/Bot/SocketQQBot.cs:81-85`、`Galaxy.QQBot/Tool/WebSocketClient.cs:151`、`:171`
- 问题：`HandleDisconnectedAsync` 对“下架/封禁”只是 `return`，但 `WebSocketClient` 自身仍会执行 `AttemptReconnectAsync`，封禁机器人会被反复连接。
- 建议：让“不可重连”的结果传递给 WebSocketClient 并停止其重连循环。

### 18. LastS 跨线程读写非原子

- 位置：`Galaxy.QQBot/Bot/BotSessionContext.cs:5`、`Galaxy.QQBot/Bot/SocketQQBot.cs:118`、`Galaxy.QQBot/Manager/HeartbeatManager.cs:44`
- 问题：`int? LastS` 在接收线程写、心跳线程读，`Nullable<int>` 由值 + 标志位组成，非原子操作，可能读到撕裂值。
- 建议：改用 `Volatile`/`Interlocked`，或只在单一线程维护并用队列/原子包装类传递。

### 19. HttpClient 超时对文件上传偏短

- 位置：`Galaxy.QQBot.DependencyInjection/ServiceCollectionExtensions.cs:18`
- 问题：统一 30 秒超时，大文件分块 PUT 到预签名地址可能超时。
- 建议：为上传请求单独设置更长的超时或使用独立 `HttpClient`。

### 20. message_scene.ext 类型假设可能错误

- 位置：`Galaxy.QQBot/Models/Common/BotMessageScene.cs:6`
- 问题：`ext` 声明为 `string[]`。若平台某些事件返回字符串（如 `"[\"qq.com\"]"`），整条事件反序列化失败，并触发问题 10 的断线。
- 建议：确认实际返回类型；必要时用自定义转换器兼容 `string` / `string[]`。

---

## 三、轻微 / 代码质量

- `AccessTokenManager` 使用 `static readonly object tokenLock`，多个实例之间共享锁，语义不清（`AccessTokenManager.cs:20`）。建议改为实例字段。
- `Payload.S` / `Payload.Id` 等字段未处理缺省情况；`Payload.D` 为 `object`（实际是 `JsonElement`），依赖 `ToString()` 取 JSON，建议显式使用 `JsonElement` 或泛型。
- `JsonHelper` 未配置 `PropertyNameCaseInsensitive`，一旦字段大小写与平台不一致会静默失败。
- `SocketQQBot` 的第二个构造函数内部 `new HttpClient()`，绕过连接池且不释放。
- `Galaxy.QQBot.Test/Program.cs` 中保留了注释掉的 DI 启动代码，建议清理。
- `IHeartbeatManager` 启动后没有心跳 ACK 超时检测，无法发现“连接假死”。
- `ResponedAsync` 拼写应为 `RespondedAsync`（`BotMessageManager.cs:30`）。
- `HttpHelper.PostAsyncWithOutToken` 命名应为 `WithoutToken`（`HttpHelper.cs:9`）。
- `FileMd5Helper` / `FileHashHelper` 位于全局命名空间且逻辑重复，建议合并到 `Galaxy.QQBot.Tool` 并去重。

---

## 四、建议修复优先级

1. 重置并移除硬编码 AppSecret，清理 token 日志（安全）。
2. 重构重连状态机，消除无限递归与双重重连（稳定性核心）。
3. 修复 `GroupEventData`/`BotMsgElement` 字段为属性（功能正确性）。
4. 修复文件上传分块读取逻辑（功能正确性）。
5. 补齐 op1/op7/op9 与异常隔离（稳定性）。
6. 处理心跳重复、token 刷新重复、TCS 不可恢复（稳定性）。
7. 其余中等与轻微问题按迭代逐步清理。
