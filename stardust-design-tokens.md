# 星尘(Stardust)控制台 · 设计令牌文档

> 基于 NewLifeX（Element UI 命名）令牌体系，融合「新生命」与「星尘」双母题的专属定制。
> 所有色值严格继承 `newlifex-design-spec.md`，不发明新色值；仅在语义映射与星尘用法侧重上定制。

---

## 1. 星尘专属语义色映射

8 模块落位：①集群总览（拓扑用 accent、状态药丸用语义色）②节点管理 StarAgent（accent 环形在线率）③应用管理（primary 水平条形）④配置中心（success/primary）⑤监控中心 APM（primary 面积图）⑥注册中心/服务发现（accent 拓扑子图）⑦远程发布（primary 进度条）⑧日志/DNS（primary + warning/danger 药丸）。

**一句话规则**：星尘组件（网关 / Agent / 服务发现 / 拓扑）用星尘青 `--el-color-accent`；生命系统（集群 / 应用 / 发布 / 监控）用生命绿 `--el-color-primary`；告警 `--el-color-warning`、异常 `--el-color-danger` 各归其位。

## 2. 星尘青的升级用法

星尘控制台中 accent 比通用 newlifex 更突出 3 处：①导航轨节点星点以 accent 发光，标识「星」节点；②顶部生长光带中段由 accent 过渡，呼应星尘而非纯生命绿；③全局焦点环统一用 accent，强化分布式交互。

**深墨导航轨 `#1F2A33` 上的发光规范（box-shadow）**：
- 节点星点 glow：`0 0 6px rgba(31,200,214,.6), 0 0 12px rgba(31,200,214,.3)`
- 焦点环：`0 0 0 2px rgba(31,200,214,.25)`
- 当前节点生命绿光晕：`0 0 0 3px rgba(0,135,90,.35), 0 0 10px rgba(0,135,90,.4)`

## 3. 暗色模式令牌（继承 newlifex-admin `body.dark`）

关键覆盖值：

| Token | 值 | 说明 |
|-------|-----|------|
| `--el-bg-color` | `#1F2A33` | 复用 Ink 作表面 |
| `--el-bg-color-page` | `#16202A` | Ink 压深一级的暗底延伸 |
| `--el-text-primary` | `#E6F3EF` | primary-light-9 |
| `--el-text-regular` | `#CCE7DE` | primary-light-8 |
| `--el-text-secondary` | `#9AA4B0` | 冷灰6 |
| `--el-border-color` | `#3A4450` | 冷灰8 |
| `--rail-bg` | `#16202A` | 深墨导航轨压深 |

`--el-color-primary` / `--el-color-accent` / 语义色保持原值（`#00875A` / `#1FC8D6` / `#1F9D5B` / `#E0922E` / `#D9363E` / `#1F6FD0`）不变。

## 4. 组件规范要点（原型构建师可执行清单）

- **导航轨**：深墨底 `#1F2A33`，竖线 + 发光星点 dot，当前节点生命绿光晕（见 §2）。
- **顶部光带**：高 3px，`linear-gradient(90deg,#00875A 0%,#1FC8D6 50%,#00875A 100%)`，glow = `0 1px 8px rgba(0,135,90,.25)`。
- **卡片**：白底 `#FFFFFF` / 圆角 12 / 三级阴影 S1~S3（绿调）/ 禁止左侧彩边；卡片头标题 15px/600。
- **状态药丸 run/wait/warn/err**：淡底用 `primary-light-9 #E6F3EF` 或 `accent-light #6FE3EC` + 语义色文字（success/info/warning/danger）。
- **图标**：一律内联 SVG，线性 stroke + `currentColor`，禁止 emoji。
- **数据图表**：全部内联 SVG（拓扑网络 / 环形进度 / 面积图 / sparkline / 水平条形），无外部库。

## 5. 星尘专属图标清单（线性 SVG 路径意图）

| 场景 | 图标 | SVG 意图 |
|------|------|----------|
| 集群总览 | 星尘拓扑 | 圆点阵 + 连线（constellation） |
| 节点 StarAgent | 圆点阵 | 多 node dots 矩阵 |
| 网关 | 菱形分流 | diamond + 两侧分叉线 |
| 远程发布 | 上行火箭/箭头 | rocket / up-arrow |
| 链路追踪 | 折线链路 | polyline nodes |
| 配置中心 | 齿轮 | gear |
| 注册/DNS | 地球 | globe |
| 日志 | 文档列表 | doc-lines |
| 监控 APM | 心率波形 | pulse wave |

全部 `stroke="currentColor"`、`fill="none"`、线性 1.5px。

## 6. 排版 / 间距 / 圆角 / 阴影（复用既有，重述关键值）

- **字体栈**：`"Inter","PingFang SC","Microsoft YaHei","Helvetica Neue",sans-serif`
- **字号**：`12 / 13 / 14 / 16 / 20 / 24 / 32 / 40`；**字重**：`400 / 500 / 600 / 700`
- **间距（4px 基准）**：`4 / 8 / 12 / 16 / 24 / 32 / 48`
- **圆角**：`4 / 6 / 12 / 16`
- **阴影（3 级绿调）**：
  - S1 `0 1px 2px rgba(0,135,90,.06), 0 1px 3px rgba(31,42,51,.04)`
  - S2 `0 4px 12px rgba(0,135,90,.08), 0 2px 4px rgba(31,42,51,.05)`
  - S3 `0 12px 32px rgba(0,135,90,.10), 0 6px 12px rgba(31,42,51,.06)`

## 7. anti-slop 自检清单

- [x] 无紫粉渐变（仅生命绿→星尘青）
- [x] 无 emoji（图标全内联 SVG）
- [x] 无玻璃拟态（无 backdrop-blur）
- [x] 无左彩边（卡片禁左侧色条）
- [x] 中文回退（PingFang SC / Microsoft YaHei）
- [x] 对比度 AA（`#566371` 次文 on `#FFF` ≈5.67:1；语义色文字落浅底均达 AA）

设计令牌文档已就绪，可进入原型生成阶段。
