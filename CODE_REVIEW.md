





## 二、中等问题

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
