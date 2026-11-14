# 星尘 Stardust · Element Plus Token 映射与主题覆盖清单

> 配套：`tokens.css`（唯一事实来源）、`element-theme-override.css`（覆盖实现）、`tailwind.config.js`、`design-spec.md`。
> 范围：本文件定义 **设计令牌 → Element Plus (`--el-*`) 变量** 的映射，并给出一份**覆盖清单**（哪些 `--el-*` 必须覆盖、覆盖成什么、为什么）。
> 目标：Vue 工程只引入主题 CSS，不硬编码颜色。

---

## 0. 技术选型说明（重要）

- **饿了么 UI = Element**。本规范以 **Element Plus（Vue 3，CSS 变量主题）** 为默认目标 —— 因为它的 `--el-*` 是运行时 CSS 变量，可在「主题 CSS 文件」里直接覆盖，零编译、可热更。
- 若项目仍用 **Element UI（Vue 2，SCSS 变量 `$--color-primary` 等）**，变量在编译期固定，需改用 SCSS 覆盖（见文末附录 A），无法纯 CSS 覆盖。新工程一律建议 Element Plus。

---

## 1. 映射总表（设计令牌 → Element 变量 → 用途）

| 设计令牌 (`--sd-*`) | Element 变量 (`--el-*`) | 主要作用组件 | 备注 |
|---|---|---|---|
| `--sd-color-brand` | `--el-color-primary` (+ light-3/5/7/8/9, dark-2) | 按钮、链接、激活态、进度条、焦点 | 星尘蓝 `#6673FF`；浅阶按 Element 混合比预计算 |
| `--sd-color-online` | `--el-color-success` (+ 浅阶) | 在线状态药丸、成功提示、Tag | `#43E8A3` |
| `--sd-color-alert` | `--el-color-warning` (+ 浅阶) | 告警药丸、警告提示、Tag | `#FFC208` |
| `--sd-color-offline` | `--el-color-danger` / `--el-color-error` (+ 浅阶) | 离线药丸、错误、删除确认 | `#FF4545` |
| `--sd-color-accent` | `--el-color-info` (+ 浅阶) | 强调标签、提示、焦点环同源色 | `#6FE3EC` 星尘青 |
| `--sd-text-primary` | `--el-text-color-primary` | 所有主文本 | `#E6EDF3` |
| `--sd-text-regular` | `--el-text-color-regular` | 常规文本、表格 | `#C7D0DA` |
| `--sd-text-secondary` | `--el-text-color-secondary` | 次要文本、说明 | `#8A97A6` |
| `--sd-text-placeholder` | `--el-text-color-placeholder` | 输入占位 | `#6B7785` |
| `--sd-text-disabled` | `--el-text-color-disabled` | 禁用文本 | `#5A6675` |
| `--sd-border` | `--el-border-color` | 默认边框 | `#3A4450` |
| `--sd-border-light` | `--el-border-color-light` | 卡片内分隔 | `#33414E` |
| `--sd-border-lighter` | `--el-border-color-lighter` | 弱分隔线 | `#2D3943` |
| `--sd-border-extra-light` | `--el-border-color-extra-light` | 极弱分隔 | `#27323B` |
| `--sd-border-dark` | `--el-border-color-dark` | 强边框 | `#47535F` |
| `--sd-bg-surface` | `--el-bg-color` | 卡片、控件表面 | `#1F2A33` |
| `--sd-bg-page` | `--el-bg-color-page` | 页面底 | `#16202A` |
| `--sd-bg-elevated` | `--el-bg-color-overlay` | 抽屉、Dialog、Popover | `#26333F` |
| `--sd-bg-elevated` | `--el-fill-color` | 按钮 hover、滑块轨道 | `#26333F` |
| `--sd-bg-input` | `--el-fill-color-blank` | 输入框内底 | `#18222C` |
| `--sd-radius-base` | `--el-border-radius-base` | 默认圆角 | 6px |
| `--sd-radius-sm` | `--el-border-radius-small` | 小圆角 | 4px |
| `--sd-radius-round` | `--el-border-radius-round` | 胶囊 / 圆角按钮 | 20px |
| `--sd-font-family` | `--el-font-family` | 全局字体栈 | Inter + 中文回退 |
| `--sd-font-xs~xl` | `--el-font-size-*` | 字号阶梯 | 12/13/14/16/20/24 |
| `--sd-shadow-1/2` | `--el-box-shadow*` | 浮层阴影 | 蓝调 |

---

## 2. 主题覆盖清单（Checklist）— 必须覆盖的 `--el-*`

> 在 `element-theme-override.css` 中逐项落实。**原则：覆盖值一律 `var(--sd-*)`，禁止写死 hex。**

- [x] 主色族：`--el-color-primary` 及 `light-3/5/7/8/9`、`dark-2` → 全部指向 `--sd-color-brand*` 系列
- [x] 成功族：`--el-color-success*` → `--sd-color-online*`
- [x] 警告族：`--el-color-warning*` → `--sd-color-alert*`
- [x] 危险/错误族：`--el-color-danger*`、`--el-color-error` → `--sd-color-offline*`
- [x] 信息族：`--el-color-info*` → `--sd-color-accent*`（星尘青）
- [x] 文本 5 级：`--el-text-color-{primary,regular,secondary,placeholder,disabled}`
- [x] 边框 5 级：`--el-border-color{,-light,-lighter,-extra-light,-dark}`
- [x] 背景 3 级：`--el-bg-color`、`--el-bg-color-page`、`--el-bg-color-overlay`
- [x] 填充：`--el-fill-color`、`--el-fill-color-light`、`--el-fill-color-lighter`、`--el-fill-color-blank`
- [x] 圆角：`--el-border-radius-base/small/round/circle`
- [x] 字体：`--el-font-family` + 6 档 `--el-font-size-*`
- [x] 阴影：`--el-box-shadow`、`--el-box-shadow-light/lighter`

### 组件级微调（引用令牌，不写死）
- [x] `.el-drawer` / `.el-dialog` 背景 = `--sd-bg-elevated`、阴影 = `--sd-shadow-3`
- [x] 输入框聚焦边框 = `--sd-color-brand`
- [x] 主按钮 hover = `--sd-color-brand-light-3`
- [x] 表格行 hover = `--sd-bg-elevated`

---

## 3. 在 Vue 工程里接入（防止硬编码的落地步骤）

```ts
// main.ts —— 顺序决定优先级（后者覆盖前者）
import 'element-plus/dist/index.css'                 // ① Element 默认 + 暗色变量
import './design-system/tokens.css'                  // ② 设计令牌（--sd-*）
import './design-system/element-theme-override.css'  // ③ 主题覆盖（必须最后）

import { createApp } from 'vue'
import ElementPlus from 'element-plus'
import App from './App.vue'

const app = createApp(App)
app.use(ElementPlus)
app.classList?.add('dark')        // 根节点加暗色类，激活 --el-* 暗色调
document.documentElement.classList.add('dark')
app.mount('#app')
```

**布局用 Tailwind，组件用 Element：**
- 外层栅格 / 间距 / 定位 → Tailwind 工具类（映射见 `tailwind.config.js`）。
- 交互控件（按钮、表格、Tab、抽屉、开关、表单）→ `<el-*>` 组件，自动套用本主题。
- 业务代码里 **禁止** 出现 `#6673FF`、`#16202A` 之类的字面色值；必须改色时只改 `tokens.css`。

---

## 附录 A（可选）：Element UI (Vue 2, SCSS) 覆盖

Element UI 的 `$--color-primary` 等是 SCSS 变量，编译期固定，纯 CSS 无法覆盖。改用 SCSS `@use`：

```scss
// styles/element-variables.scss
$--color-primary: #6673FF;
$--color-success: #43E8A3;
$--color-warning: #FFC208;
$--color-danger:  #FF4545;
$--color-error:   #FF4545;
$--color-info:    #6FE3EC;
$--border-color-base: #3A4450;
$--background-color-base: #1F2A33;
$--font-size-base: 14px;
$--border-radius-base: 6px;
// 然后: import 'element-ui/packages/theme-chalk/src/index.scss'（需在 vue.config 配 sass additionalData）
```

> 注意：Element UI 暗色需自行叠加暗色变量或外层 `.dark` 覆写，工作量大于 Element Plus。新工程优先 Element Plus + 本 CSS 覆盖方案。
