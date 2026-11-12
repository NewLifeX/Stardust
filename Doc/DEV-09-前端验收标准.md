# 前端验收标准

审查 `Stardust.Web.Vue` 和按原型还原的控制台时用这一份。同一类问题用同一条标准，不按页面临时放宽。

依据：`Doc/DEV-03-编码规范.md`、`Agents.md` §11、`.agents/skills/stardust-prototype-to-vue/SKILL.md`，以及 Emil Kowalski 的设计工程做法（`emil-design-eng`）。Emil 文里的自定义缓动变量不新增到本仓库：曲线直接写在使用处，不造 `--ease-*` 或 `--app-*`。

## 1. 什么才算可以提交

同时满足才进提交：

- 用户旅程文档里该功能已核对完成，或改动就是补齐文档已勾选的行为。
- 纯逻辑有 Vitest，界面行为有 CT 或旅程走查记录。配置只用 `vitest.config.unit.ts`。
- 对照下面第 2–5 节没有未解释的违规。
- 一个提交只含一个已完成功能（一个旅程页或一个独立交互），不把半成品页面夹进去。

做了一半、只有界面没有数据、或测试是红的，留在工作区。

## 2. 颜色、字体、间距

- 颜色只用 `var(--el-*)`。Tailwind 只用于布局（flex、grid、gap、padding、宽度）。
- 禁止硬编码色值：`#rgb`、`rgb(`、`hsl(`、`oklch(`、`bg-red-500` 这类调色板类。需要透明时用 `color-mix(in oklab, var(--el-...) N%, transparent)`。
- 禁止新的 `--app-*`、`--sd-*` 或组件私有颜色变量。已有 `--el-*` 不够用时，只改 `design-system/element-theme-override.css`。
- 不设置 `font-family`。字号和字重走 Element Plus 或已有工具类，不按页面另选一套字体。
- 间距走 Tailwind 刻度或组件已有间距。不为一处对齐写魔法数，除非原型或现有组件已经用这个数，并在注释里写出来源。

## 3. 交互必须一致

- 会发请求的按钮：点击后立刻进入 loading（`animate-spin`）、按钮禁用、结束后恢复。刷新类反馈至少能看见约 300ms。失败要写出对象和步骤，不能静默，也不能把失败显示成空数据。
- 禁用原因要出现在用户看得见的地方，并随选择变化。
- 同一种动作用同一个组件。确认用现有 `GuardModal`，抽屉用现有 `Drawer`，不另起一套对话框。
- 空、加载中、失败三种状态都要有文案。没有接口就显示「待接入」，不造演示数据。
- 带分支的判断放在 `*.logic.ts` 或 `useXxx.ts`。`.vue` 只绑定状态和事件。
- `index.vue` 的脚本保持短小。页面定制用 CubeTable 插槽，不新增 `useSections`。

## 4. 动效

控制台是整天使用的工具。动效只保留反馈，不为了好看加动画。

- 键盘打开的界面（命令面板、快捷键）不做开闭动画。
- 按钮按下可以有轻微缩放（约 `scale(0.97)`），时长 100–160ms，缓动用 ease-out。不要用 `ease-in`。
- 界面过渡指定具体属性，禁止 `transition: all`。时长不超过 300ms。抽屉可以到 500ms。
- 只动画 `transform` 和 `opacity`。不要动画 `width`、`height`、`margin`、`padding`。
- 悬停效果包在 `@media (hover: hover) and (pointer: fine)` 里。
- 尊重 `prefers-reduced-motion: reduce`：去掉位移动画，保留颜色或透明度变化。
- 弹出层从触发点缩放。对话框保持居中，不从角落飞入。

## 5. 审查记录

发现问题用一张表，每行是改前、改后、原因。不另起「之前 / 之后」的长清单。

| 改前 | 改后 | 原因 |
| --- | --- | --- |
| 示例：`transition: all 300ms` | `transition: transform 160ms ease-out` | 只动画合成属性，按下立刻有反馈 |

没有问题就写「未发现第 2–4 节违规」，并列出看过的文件。
