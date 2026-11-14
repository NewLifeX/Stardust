# APP-1 应用工作台走查缺陷

- 状态：待修。本文只记缺陷和改法，不改代码。
- 日期：2026-11-06
- 环境：本机 Vite `http://localhost:5188`，接口 `http://10.188.32.212:6681`（`Stardust.Web.Vue/.env.development.local` 的 `VITE_APP_BASE_API`）。本机 6681 是另一个工作区，不在本次范围。
- 样本：应用列表 29 条；工作台用智能工单系统 `App.Id = 30`（显示名「智能工单系统」，配置名 MindHub.Api）。Playwright 只读，没有点发布、保存、触发、删除、回滚、重启、上传。
- 结论：下面两处都是页面把接口读错了。接口里的记录和数值是对的。

## 1. 不修

这些在走查里见过，不是缺陷，不要顺手改：

| 现象 | 原因 |
|---|---|
| 调用链、日志、注册、监控大屏是空态 | 当天没有采样、没有日志、没有注册实例、没有日统计。页面写的是「没有记录」，不是加载失败 |
| 部署节点写「未上报版本」，概览写 3 个节点与 `v20261105-121311` 不一致 | 部署节点的版本字段为空。实例上的 `1.0.2026.1105` 是进程版本，不是部署包版本 |
| 命令里有一条 `config/publish`「找不到该服务」 | 历史命令的结果原文，不是这次页面请求失败 |

## 2. 缺陷

### 2.1 配置页把已有配置显示成加载失败

**现象。** `/app/config/30` 显示「配置加载失败，请点刷新重试」。页面错误数为 0，这次请求也不是 4xx。

**接口。** `GET /api/Configs/AppConfig?appId=30&PageSize=20` 返回 200、`code: 0`，`data` 有一条：`id` 22，`name` MindHub.Api，`version` 2，`nextVersion` 2，`enable` true。这条 JSON 里没有 `appId`。

**原因。** 列表接口故意去掉了 `AppId`。`Stardust.WebApi/Areas/Configs/Controllers/AppConfigController.cs` 构造函数里有 `ListFields.RemoveField("AppId", ...)`，查询参数 `appId=30` 已经过滤过。前端 `fetchConfigApp`（`appConfig.logic.ts`）却在每一行上找 `AppId` / `appId`，找不到就 `return null`。`AppConfigTable.vue` 的 `load()` 把 `null` 一律写成 `loadFailed`，于是请求失败和「行上没有 appId」变成同一句「加载失败」。

**改法。** 只改前端，不把 `AppId` 加回列表字段（MVC 列表也删了这个字段）。

1. `fetchConfigApp` 在 `listOf(res)` 之后：
   - 行上有 `appId` 时，仍只收与入参相同的那一行，避免把别的应用收进来。
   - 每一行都没有 `appId`（列表字段被去掉）且列表非空时，用第一条。查询已经按 `appId` 过滤。返回对象里的 `appId` 用来参，不要写成 0。
   - 列表为空，或请求抛错，仍返回 `null`。
2. 不改 `AppConfigTable.vue` 的失败文案。这次的误报会在 `fetchConfigApp` 能取到 id 22 之后消失。
3. 单测补在 `appConfig.logic.test.ts`：
   - 行只有 `id` / `name` / `version`，没有 `appId`，入参 30，得到 id 22（或夹具里的 id），且 `appId` 为 30。
   - 行上的 `appId` 与入参不同，仍然返回 `null`。
   - 现有「带 AppId 命中」「非法 id 不请求」「请求抛错返回 null」保持通过。

**做完怎么认。** 再打开 `/app/config/30`，应看到 MindHub.Api 的配置项，而不是「配置加载失败」。不要点发布、同步或保存。

### 2.2 性能页把 MB 显示成 B

**现象。** 同一时刻，`/app/instances/30` 三台实例内存约 537 MB、543 MB、552 MB；`/app/meter/30` 对应行显示 537 B，堆内存约 58 B。CPU 0.0% 两边一致。

**接口。** `Stardust.Data/Entity/应用性能.cs` 写明 `Memory`、`HeapSize` 单位是 M。`应用性能.Biz.cs` 上报时 `Memory = WorkingSet / 1024 / 1024`。所以 537 就是 537 MB，不是 537 字节。

**原因。** 实例页在 `mergeSnapshots` 里用 `mbToBytes` 把 MB 乘成字节，再交给 `formatBytes`。性能页 `toMeterRows` 原样留下 `memory`、`heap`，`AppObserve.vue` 对这两列直接 `formatBytes`。`formatBytes` 把小于 1024 的数显示成 B，于是 537 MB 变成 537 B，58 MB 变成 58 B。

**改法。** `formatBytes` 继续只接受字节，实例页不要改。

1. 在 `toMeterRows` 里把 `Memory` 和 `HeapSize` 当成 MB，转成字节后再放进 `memory`、`heap`。直接调用 `appInstances.logic.ts` 的 `mbToBytes`（`null` 或非数字仍按现在的 `num()` 落到 0，再乘）。CPU、线程、句柄、连接、GC 次数不要乘。
2. 曲线用的是同一份 `memory`。转换放在 `toMeterRows`，表格和曲线一起对，不必改 `AppObserve.vue` 的模板。
3. 夹具现在把 `Memory: 1048576` 当成字节（`AppObserve.story.ts`，CT 期望 `1.0 MB`）。改完后故事和 CT 的原始字段用 MB：`Memory: 1` 仍应显示 `1.0 MB`。`appObserve.logic.test.ts` 里 `Memory: 1024` 的期望从 `memory: 1024` 改为 `1024 * 1024 * 1024`。有 `HeapSize` 的夹具同样改成 MB。

**做完怎么认。** 再打开 `/app/meter/30`，内存应接近实例页的 537 / 543 / 552 MB，堆内存不再是几十 B。只看列表，不发命令。

## 3. 验证

两处都只动 `StardustLayout2` 的纯逻辑和已有单测、故事夹具。

```bash
pnpm exec vitest run --config vitest.config.unit.ts src/layouts/StardustLayout2/appConfig.logic.test.ts src/layouts/StardustLayout2/appObserve.logic.test.ts --maxWorkers=2
```

工作目录是 `Stardust.Web.Vue`。故事夹具改了 `Memory` 的话，再跑 `AppObserve` 的 CT。浏览器复看仍用 Playwright，应用 id 30，不要对线上点写操作。
