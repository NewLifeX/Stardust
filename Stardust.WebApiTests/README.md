# Stardust.WebApiTests

Stardust.WebApi（解耦纯 API 版）的**集成测试工程**（xUnit，net10.0）。本文档记录测试基座约定，**新增测试前请先阅读**，避免重新踩进全局状态和并发的坑。

## 测试分层定位

本工程是** HTTP 集成测试**：用 `WebApplicationFactory<Program>` 启动真实的 Stardust.WebApi 主机，通过真实 HTTP 请求（带 Bearer 登录态）验证各区域的 `GetFields` 等端点，不 mock 控制器与数据层。

- 验证对象：统一响应信封 `{code, data, message}`、各区域字段端点可用性、ViewKinds 契约。
- 单元测试（服务层、实体行为）放在 `Stardust.ServerTests`，不在本工程。
- 前端 e2e 在 `Stardust.Web.Vue`（`pnpm exec playwright test`），与后端测试分层互不重叠。

## 接入被测工程

`Stardust.WebApi.csproj` 用顶级语句，隐式 `Program` 类是全局命名空间下的 `internal`。为了让本工程能用 `WebApplicationFactory<Program>`，在 WebApi 工程的 csproj 里声明了：

```xml
<InternalsVisibleTo Include="Stardust.WebApiTests" />
```

本工程**未**重复引用 Stardust.Data / Stardust.Extensions / Stardust，它们由 Stardust.WebApi 传递引入。

## 并发与运行配置

- `xunit.runner.json` 关闭集合级并行（`parallelizeTestCollections: false`），全工程串行执行，复用同一个主机实例避免 OOM。
- 所有测试类用 `[Collection(WebApiCollection.Name)]` 声明复用 `WebApiTestFactory` 共享实例。

## 进程级状态契约

`WebApiTestFactory` 在构造函数里写入 5 个进程级环境变量（4 个 SQLite 连接串 + `StarServer` 重定向）来隔离数据库与配置中心，并在 `Dispose` 中**原样还原**，不污染同一进程内的其它测试工程。

| 状态 | 处理 |
|---|---|
| `ConnectionStrings__Stardust` 等 4 个 | 构造时写入临时 SQLite 路径，Dispose 还原旧值 |
| `StarServer` | 构造时指向无效地址，Dispose 还原旧值 |
| `%TEMP%/stardust_webapi_test_*` 临时库目录 | Dispose 中先停主机释放文件锁，再删除 |

## 新增测试指引

- 区域字段端点：在 `Areas/` 下对应区域文件里给 `Endpoints` 静态属性加一行 `"{Area}/{Controller}/GetFields?kind=List"`，复用 `[Theory] GetFields_ReturnsFieldList`。
- 需要自定义断言的端点：注入 `ITestOutputHelper` 构造 `ApiClient`，用 `GetApiAsync<T>` / `GetJsonAsync`，业务断言写在测试方法内（断言留在测试里，不藏进基类抛异常）。
- 新的特性路由控制器（非 `api/{area}/{controller}/{action}` 模板）：在 `ApiClient.NormalizeUrl` 的 `_routeRules` 里加一行前缀映射。
- 统一信封契约：见 `Contract/ApiResponseContractTests.cs`（覆盖 GetFields 的 5 种 ViewKinds）。

### 新增测试检查清单

- [ ] 断言业务结果而非仅「无异常」；失败路径也要断言
- [ ] 触碰了进程级状态？确认 `WebApiTestFactory` 已负责还原
- [ ] 加了特性路由控制器？确认 `ApiClient.NormalizeUrl` 映射已更新
