# AI-AI 对话接口

> 版本：v2.0 | 日期：2026-11-12
> 需求对应：[需求文档](需求文档.md) §3.14 | 功能清单：[功能清单](功能清单.md)

## 1. 模块职责

| 职责 | 说明 |
|------|------|
| 统一上游 | 平台只维护一份大模型地址与密钥，下游应用不需要各自申请、保管和轮换密钥 |
| 令牌复用 | 下游应用使用星尘应用登录令牌作为 OpenAI `api_key` 调用，不新增一套 AI 鉴权体系 |
| 协议兼容 | 对外遵循 OpenAI Chat Completions 协议，现成 OpenAI SDK 改一个 `base_url` 即可接入 |
| 两种输出 | 请求体 `stream` 开关切换：非流式一次返回完整回复；流式以 SSE 逐段返回增量 |
| 模型准入 | 默认模型之外，请求指定模型必须落在白名单内，防止下游误用高成本模型 |
| 错误收敛 | 上游异常统一转换为 OpenAI 错误体，不向上游暴露密钥与内部地址 |

## 2. 核心组件

| 组件 | 所在工程 | 说明 |
|------|----------|------|
| `OpenAIController` | `Stardust.Server` | 接口入口 `POST /v1/chat/completions`，解析 Bearer 令牌并校验应用身份 |
| `AIService` | `Stardust.Server` | 校验开关与配置、注入系统提示词、调用 OpenAI 兼容上游，解析完整回复与 SSE 增量 |
| `OpenAIStreamResult` | `Stardust.Server` | 把上游增量写成 OpenAI 流式分片，处理首包角色、中途错误与客户端断开 |
| `OpenAIErrors` | `Stardust.Server` | 内部异常映射为 OpenAI 错误体与 HTTP 状态码 |
| `OpenAIChat*` / `OpenAIUsage` / `OpenAIError` | `Stardust` | OpenAI 线协议模型，客户端与服务端共用 |
| `AIChatRequest` / `AIChatResponse` / `AIChatDelta` | `Stardust` | SDK 友好的请求、响应与增量模型，`AppClient` 负责与线协议互转 |
| `AppClient.ChatAsync` / `ChatStreamAsync` | `Stardust` | 客户端封装，与服务端共用登录令牌，独立长超时 |

## 3. 请求与响应

### 3.1 接口

```http
POST /v1/chat/completions
Authorization: Bearer {appToken}
Content-Type: application/json
```

单个端点，非流式与流式由请求体 `stream` 字段切换：

- `stream` 缺失或 `false`：返回 `application/json` 的 `chat.completion`
- `stream: true`：返回 `text/event-stream` 的 `chat.completion.chunk` 序列

`appToken` 即应用登录 `App/Login` 下发的令牌。只有令牌主体对应的应用存在且启用时才允许调用；令牌有效期 2 小时，过期后需重新登录获取（`AppClient` 自动处理）。

响应不使用星尘统一的 `{code,data}` 包装，直接返回 OpenAI 原始结构。

### 3.2 请求体

```json
{
  "model": "deepseek-chat",
  "messages": [
    { "role": "system", "content": "你是星尘助手" },
    { "role": "user", "content": "帮我解释一下这段 SQL" }
  ],
  "temperature": 0.7,
  "max_tokens": 1024,
  "stream": false
}
```

| 字段 | 必填 | 说明 |
|------|:----:|------|
| `messages` | 是 | 消息列表，按时间顺序排列，至少一条且内容非空 |
| `model` | 否 | 模型编码，为空时使用模型清单首项（默认模型）；填写时必须命中模型白名单 |
| `temperature` | 否 | 采样温度，原样透传；为空时不发送，由上游模型决定 |
| `max_tokens` | 否 | 最大输出令牌数，原样透传；为空时不发送，不替调用方截断回答 |
| `stream` | 否 | 是否流式，默认 `false` |
| `user` | 否 | 调用方标识，仅透传给上游 |

未列出的 OpenAI 字段（如 `tools`、`response_format`、`n`）会被忽略，平台只提供纯对话能力。

### 3.3 非流式响应

```json
{
  "id": "chatcmpl-9xQ2m1Vb7Kp0sT4nR6yLd3Fz",
  "object": "chat.completion",
  "created": 1762900000,
  "model": "deepseek-chat",
  "choices": [
    {
      "index": 0,
      "message": { "role": "assistant", "content": "这段 SQL 的作用是……" },
      "finish_reason": "stop"
    }
  ],
  "usage": { "prompt_tokens": 128, "completion_tokens": 256, "total_tokens": 384 }
}
```

| 字段 | 说明 |
|------|------|
| `model` | 实际使用的模型编码 |
| `choices[0].message.content` | 回复文本 |
| `choices[0].finish_reason` | 结束原因，如 `stop`/`length`/`content_filter` |
| `usage` | 令牌用量，上游未返回时为 0 |

### 3.4 流式响应

`stream: true` 时返回 `text/event-stream`，逐条写出 `chat.completion.chunk`：

```text
data: {"id":"chatcmpl-9xQ2m1Vb7Kp0sT4nR6yLd3Fz","object":"chat.completion.chunk","created":1762900000,"model":"deepseek-chat","choices":[{"index":0,"delta":{"role":"assistant"}}]}

data: {"id":"chatcmpl-9xQ2m1Vb7Kp0sT4nR6yLd3Fz","object":"chat.completion.chunk","created":1762900000,"model":"deepseek-chat","choices":[{"index":0,"delta":{"content":"这"}}]}

data: {"id":"chatcmpl-9xQ2m1Vb7Kp0sT4nR6yLd3Fz","object":"chat.completion.chunk","created":1762900000,"model":"deepseek-chat","choices":[{"index":0,"delta":{"content":"段 SQL"}}]}

data: {"id":"chatcmpl-9xQ2m1Vb7Kp0sT4nR6yLd3Fz","object":"chat.completion.chunk","created":1762900000,"model":"deepseek-chat","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

data: [DONE]
```

- 首包下发 `delta.role = assistant`，随后每包只带新增文本，调用方按顺序拼接
- 以 `data: [DONE]` 标记正常结束，中途以 `finish_reason` 给出结束原因
- 上游返回用量时，另发一包只含 `usage`、`choices` 为空的分片
- 首次写出前发生错误（令牌无效、未启用、未配置等）时，回退为标准 OpenAI 错误 JSON，HTTP 状态码同步为错误码
- 已开始输出后上游中断时，写一包 `data: {"error":{...}}` 后以 `data: [DONE]` 结束
- 客户端断开时服务端立即中止上游请求

### 3.5 错误响应

错误体与 OpenAI 一致，HTTP 状态码同时表达语义：

```json
{
  "error": {
    "message": "Invalid application token",
    "type": "authentication_error",
    "code": "invalid_api_key"
  }
}
```

| 状态码 | `type` | 场景 |
|:------:|--------|------|
| 400 | `invalid_request_error` | 没有有效消息，或请求模型不在白名单 |
| 401 | `authentication_error` | 未携带令牌、令牌无效或令牌主体应用不存在 |
| 403 | `permission_error` | 应用已禁用，或平台未启用 AI 对话 |
| 429 | `rate_limit_error` | 平台对 AI 接口限流（需把该路径加入 `RateLimitPaths`） |
| 500 | `server_error` | 平台未配置上游地址/默认模型，或上游请求失败、返回无法解析的响应 |

错误体只包含 `message`/`type`/`code`，不包含上游密钥、内部地址与堆栈。

## 4. 平台配置

配置项位于 `StarServerSetting`（数据库 `StarServer` 分类），在管理后台「系统设置」的 **AI对话** 页签中修改：

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| `EnableAI` | false | 启用后 `/v1/chat/completions` 才对外提供服务 |
| `AIUrl` | — | OpenAI 兼容地址，支持基础地址、`/v1` 地址或完整 `chat/completions` 地址 |
| `AIKey` | — | 上游密钥，作为 `Authorization: Bearer` 发送；留空表示上游免鉴权 |
| `AIModels` | — | 允许的模型清单，逗号分隔；**首个即默认模型**，请求未指定模型时使用；留空视为未配置 |
| `AISystemPrompt` | — | 默认系统提示词，消息中没有 `system` 时追加到平台上下文之后 |

平台不提供温度与最大输出令牌的默认值：两者由调用方按需透传，未传时不发送，避免替调用方决定回答风格与长度。上游请求超时固定 120 秒（流式模式为整条流的总时限）。

地址填写示例（三者等价）：

```text
https://ai.newlifex.com
https://ai.newlifex.com/v1
https://ai.newlifex.com/v1/chat/completions
```

> 需要按 IP 限流时，可把 `/v1/chat/completions` 加入 `RateLimitPaths`（默认只对登录与令牌接口限流）。

### 4.1 平台上下文注入

令牌已经确定调用方身份，服务端在转发前于消息列表最前面注入一条 `system` 消息，让模型知道自己在为哪个应用服务：

```text
You are the built-in AI assistant of the Stardust platform.
Context:
- Platform: 星尘 2.0.2026.1113, server time 2026-11-13 12:00:00 +08:00
- Application: 码神 (CrazyCoder), category Web, version 1.2.3
- Team: 新生命
Answer in the same language as the user's latest message.
```

- 应用编码、显示名、类别、版本来自应用实体，团队名称来自应用所属星系项目，均来自数据库，客户端无法伪造
- 注入内容固定为英文模板，并要求「跟随用户最后一条消息的语言回答」，不做多语言翻译；如需强制中文回答，用 `AISystemPrompt` 约束
- 消息列表自带 `system` 时仍注入平台上下文，但不再追加 `AISystemPrompt`，避免覆盖调用方意图
- 应用名与团队名会随请求发送到上游大模型；对第三方云敏感时，可将其显示名改为不含业务信息的代号

## 5. 客户端用法

### 5.1 AppClient（推荐，自动登录与令牌续期）

`AppClient` 与服务端共用登录令牌，首次调用自动登录，令牌过期自动重新登录：

```csharp
using var app = new AppClient("http://star.example.com:6600")
{
    AppId = "CrazyCoder",
    Secret = "...",
};

var rs = await app.ChatAsync("帮我把这段 SQL 解释一下");
Console.WriteLine(rs?.Content);
Console.WriteLine($"tokens: {rs?.Usage?.TotalTokens}");
```

多轮对话由调用方维护上下文：

```csharp
var request = new AIChatRequest
{
    SystemPrompt = "你是一个 .NET 专家",
    Messages =
    [
        new AIChatMessage { Role = "user", Content = "什么是依赖注入？" },
        new AIChatMessage { Role = "assistant", Content = "依赖注入是一种……" },
        new AIChatMessage { Role = "user", Content = "给个 C# 例子" },
    ],
};

var rs = await app.ChatAsync(request);
```

`AppClient.ChatTimeout` 默认 180 秒，使用独立的 Http 客户端，不放大普通业务接口的 15 秒超时；服务端仍以 120 秒作为上游总时限。

流式对话按段输出，适合控制台/界面边生成边展示：

```csharp
await foreach (var delta in app.ChatStreamAsync("写一个快速排序"))
{
    Console.Write(delta.Content);
    if (delta.FinishReason != null) Console.WriteLine($"\n[{delta.FinishReason}] {delta.Elapsed}ms");
}
```

流式请求同样使用独立 HTTP 客户端与无限超时，生命周期由取消令牌控制；服务端固定 120 秒作为整条流的总时限。

### 5.2 OpenAI SDK（任何语言）

把 `base_url` 指向星尘的 `/v1`，`api_key` 填应用登录令牌即可：

```python
from openai import OpenAI

client = OpenAI(base_url="http://star.example.com:6600/v1", api_key=app_token)

rs = client.chat.completions.create(
    model="deepseek-chat",
    messages=[{"role": "user", "content": "你好"}],
)
print(rs.choices[0].message.content)

# 流式
for chunk in client.chat.completions.create(
    model="deepseek-chat",
    messages=[{"role": "user", "content": "写一个快速排序"}],
    stream=True,
):
    if chunk.choices and chunk.choices[0].delta.content:
        print(chunk.choices[0].delta.content, end="")
```

命令行验证：

```bash
curl -X POST http://star.example.com:6600/v1/chat/completions \
  -H "Authorization: Bearer $APP_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"model":"deepseek-chat","messages":[{"role":"user","content":"你好"}]}'
```

> 令牌有效期 2 小时。SDK 直连时需自行用 `App/Login` 续期，或改用 `AppClient`。
> 仅支持 Bearer 令牌，不支持 OpenAI 官方的 `api_key = "appId:secret"` 形式。

## 6. 上游协议

服务端按 OpenAI Chat Completions 协议转发：

```json
{
  "model": "deepseek-chat",
  "messages": [{ "role": "user", "content": "..." }],
  "temperature": 0.7,
  "max_tokens": 1024,
  "stream": false,
  "user": "CrazyCoder"
}
```

- 星语网关：基地址填网关地址，密钥填 `sk-` 开头的 AppKey，模型填网关模型 Code
- DeepSeek / OpenAI 等：基地址填官方地址，密钥填对应 API Key
- 上游返回非 2xx，或返回体中含 `error` 字段时，统一按失败处理并提取 `error.message`
- 流式请求把 `stream` 置为 `true`，服务端解析上游 SSE 的 `data:` 段并把增量转写为 `chat.completion.chunk`

## 7. 设计决策

| 决策 | 选项 | 选择 | 理由 |
|------|------|------|------|
| 密钥归属 | 每个应用各自申请 vs 平台统一维护 | 平台统一维护 | 下游多为内部工具，单独申请与轮换密钥成本高；统一出口也便于集中控制模型与成本 |
| 协议形态 | OpenAI 兼容入站 vs 简化 RPC | OpenAI 兼容（`/v1/chat/completions`） | 下游既有 OpenAI SDK 与现成工具可直接接入，不用为星尘单独写客户端；也便于日后挂接到支持 OpenAI 协议的第三方平台 |
| 端点数量 | 流式/非流式两个端点 vs 单端点 | 单端点 + `stream` 开关 | 与 OpenAI 协议一致，SDK 无需切换地址；服务端只维护一条鉴权与校验路径 |
| 鉴权方式 | 仅 Bearer 令牌 vs 兼容 `appId:secret` | 仅 Bearer 令牌 | 令牌由 `App/Login` 下发、可撤销、可审计，比每次携带密钥更安全；不做第二套凭据解析 |
| 流式实现形态 | 入站直接透传上游 SSE vs 服务端转写 | 服务端解析后转写 | 上游协议差异（角色段、usage 段、错误段）由服务端吸收，保证分片结构稳定 |
| 会话状态 | 服务端保存 vs 调用方维护 | 调用方维护 | 平台不保存对话内容，避免隐私与存储负担；多轮上下文由调用方随请求携带 |
| 客户端超时 | 复用 15 秒 vs 独立长超时 | 独立 180 秒 Http 客户端 | 大模型生成常超过 15 秒，直接复用会导致稳定超时；独立客户端不改变既有接口的失败检测速度；比服务端 120 秒上限留出余量，避免客户端先于服务端超时 |
| 模型准入 | 任意模型 vs 白名单 | 白名单，首项为默认模型 | 防止下游误用高成本模型；默认模型与白名单合成一份清单，避免两处配置漂移 |
| 采样与长度 | 平台设默认温度/最大令牌 vs 调用方控制 | 调用方控制，未传不发送 | 不同模型最佳温度不同，平台默认会覆盖客户端偏好；固定最大令牌会截断长回答 |
| 上下文注入 | 不注入 vs 注入平台/应用/团队信息 | 注入一条 `system` 消息 | 模型知道自己在为哪个应用与团队服务，回答更贴合场景；信息来自数据库，客户端无法伪造 |
| 提示词语言 | 多语言模板 vs 英文固定模板 | 英文固定模板 + 跟随用户语言回答 | 提示词不面向终端用户，模型对英文指令稳定；省去 32 份翻译与后续维护 |

## 8. 边界

- 不保存会话历史，不记录对话正文；埋点只记录应用、模型与消息条数
- 只实现对话补全（Chat Completions）子集，不提供工具调用、图片/音频/嵌入等扩展能力
- 不做令牌计费，费用由上游账号结算；`usage` 仅回传给调用方
- 上游地址与密钥只存在于平台设置中，不通过任何响应下发

---

（完）
