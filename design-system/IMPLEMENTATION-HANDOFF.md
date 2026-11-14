# 任务指派：星尘 Stardust 控制台前端实现

> 本提示词用于将已定稿的设计产物交给实现 Agent（如 Claude / Codex / Cursor 等）完成 Vue 前端工程。
> 复制本文件全文作为任务指令即可。

---

## 一、任务目标

使用 **Vue 3 + Element Plus + Tailwind** 将已定稿的设计规范实现为一个可运行的前端工程。
要求：**严格还原设计稿的布局 / 配色 / 圆角 / 间距 / 交互**，且**全程使用设计令牌，禁止在业务代码中硬编码颜色**。

---

## 二、必读文件（绝对路径，按序阅读）

设计依据（规范与令牌，唯一事实来源）：
1. `D:\src\os-proj\X\Stardust\design-system\design-spec.md` — 设计规范（IA 收敛、色彩、排版、交互、anti-slop）
2. `D:\src\os-proj\X\Stardust\design-system\tokens.css` — 设计令牌（`--sd-*`），改色只改这里
3. `D:\src\os-proj\X\Stardust\design-system\element-theme-override.css` — 用 `--sd-*` 覆盖 Element Plus 的 `--el-*`
4. `D:\src\os-proj\X\Stardust\design-system\element-token-mapping.md` — 令牌→Element 映射表 + 覆盖清单 + Vue 接入步骤
5. `D:\src\os-proj\X\Stardust\design-system\tailwind.config.js` — 把 `--sd-*` 注入 Tailwind 主题（布局用）

视觉还原基准（静态预览稿，4 页，像素级参考）：
6. `D:\src\os-proj\X\Stardust\design-preview\pages\home.html` （概览·星图）
7. `D:\src\os-proj\X\Stardust\design-preview\pages\node.html` （节点）
8. `D:\src\os-proj\X\Stardust\design-preview\pages\app.html` （应用·6 Tab）
9. `D:\src\os-proj\X\Stardust\design-preview\pages\settings.html` （设置）

> ⚠️ 这些 html 是**设计稿的视觉参考**，其内部是硬编码色值，**仅供对照**；新工程必须改用令牌，不得照抄十六进制。

---

## 三、技术栈与工程约定

- **Vue 3（Vite）** + **Element Plus**（交互组件） + **Tailwind**（布局）。
- 主题接入顺序（`src/main.ts`，后者覆盖前者）：
  ```ts
  import 'element-plus/dist/index.css'                       // ① Element 默认 + 暗色变量
  import './design-system/tokens.css'                        // ② 设计令牌
  import './design-system/element-theme-override.css'        // ③ 主题覆盖（必须最后）
  // 根节点加暗色类：document.documentElement.classList.add('dark')
  ```
- 布局用 Tailwind 工具类（颜色/间距来自 `tailwind.config.js` 映射的 `--sd-*`）；交互控件（按钮/表格/Tab/抽屉/开关/表单/弹层）用 `<el-*>` 组件，自动套用主题。
- ❌ **红线**：任何 `.vue` / `.tsx` 不得出现 `#6673FF`、`#16202A` 之类字面色值。要改色只改 `tokens.css`，全站生效。

---

## 四、信息架构（4 入口，已收敛）

侧栏仅 4 项：**概览 / 节点 / 应用 / 设置**。原独立的「服务 / 配置 / 监控 / 发布」已收敛到其上级，由上级详情抽屉披露。

- **概览**：星图（Hub + 模块节点 + 连线）+ KPI + 健康/事件 + 功能导航。
- **节点**：列表 + KPI + 拓扑卡（状态色）；详情抽屉新增「**服务**」Tab（本节点提供/消费的服务）。
- **应用**：列表 + 主从抽屉，详情 **6 Tab**：概览 / 实例 / **配置** / **发布** / **监控** / **服务**。
  - 配置：表格 ↔ 文本双模式 + 版本导出 / 对比上一版 / 回滚。
  - 发布：流水线步骤（构建→上传→分发→部署）箭头推进 + 图例（✓完成 / ●进行中 / ○等待）。
  - 监控：时间范围选择 + 自动刷新脉冲点（如「自动刷新 · 15s」）+ 面积图。
  - 服务：本应用提供 / 消费的服务。
- **设置**：基础信息 / 通知 / 安全 / 外观 表单 + 开关 + 保存按钮。

所有页面统一带**星图背景**（参考 home.html 的 `design-preview/assets/images/2_2.png` 或内联 SVG 星点），强化「分布式」心智。

---

## 五、关键设计约束（来自 design-spec）

- 品牌星尘蓝 `#6673FF`、渐变到 `#8C5CF5`；强调青 `#6FE3EC`；状态三态：在线绿 `#43E8A3` / 告警黄 `#FFC208` / 离线红 `#FF4545`。
- 暗色表面 3 级：`page #16202A` < `surface #1F2A33` < `elevated #26333F`（输入框内底 `#18222C`）。
- 字体栈 `Inter` + 中文回退（PingFang SC / Microsoft YaHei）；字号阶梯 12/13/14/16/20/24/32/40；间距 4px 基准（4/8/12/16/24/32/48）；圆角默认 6。
- 图标**一律内联 SVG**（`stroke="currentColor"`、`fill="none"`、1.5px），**禁止 emoji**。
- 画布基准 **1440×900**，等比缩放居中（`transform: scale(min(w/1440,h/900))`）。

---

## 六、验收标准

1. 4 个页面全部实现，导航互跳，视觉与预览稿一致（最终由用户目视核对）。
2. **全局无硬编码色值**：`grep -rnE '#[0-9A-Fa-f]{6}' src/` 结果应仅为 `tailwind.config.js` / 令牌相关文件内的引用，业务代码为空。
3. **单一来源验证**：改 `tokens.css` 中 `--sd-color-brand` 一处，全站主色随之变化。
4. 抽屉 / Tab / 配置双模式 / 发布流水线 / 监控自动刷新等交互可用。

---

## 七、注意事项（避免踩坑）

- ❌ 旧文件 `D:\src\os-proj\X\Stardust\stardust-design-tokens.md`（生命绿 `#00875A` 主题 + 8 页 IA）**已作废**，以 `design-system/` 为准，**不要参考它**。
- Ardot 设计源文件 `https://ardot.tencent.com/file/712657487101595` 尚未同步本次 IA 收敛，请以 `design-preview/pages/*.html` 四个预览稿为视觉准绳。
- 不要修改 `design-system/` 下的规范文件，除非用户明确授权；如需调整先反馈。
- 数据用 mock（真实结构、真实字段名），不接后端；组件按设计稿静态呈现即可。
