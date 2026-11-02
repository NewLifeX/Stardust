// 应用工作台的纯逻辑（无 Vue 依赖）：页签清单与路由 :tab 的解析。
// 对照原型 page-apps.jsx 的 PageApp：前五个页签照抄；2026-10-29 停点 19 起「发布」取消，部署内容拆成版本、节点、部署历史、流水线四个页签。
import type { AppTab } from './palette.items.ts';

/** 页签清单：概览到实例沿用原型；其后是注册侧五页签（对齐 MVC _App_Nav）；再后是监控两个页签（F01/F02）与部署四页签。history 仍是部署历史。 */
export const WORKBENCH_TABS: ReadonlyArray<{ key: AppTab; label: string }> = [
  { key: 'overview', label: '概览' },
  { key: 'trace', label: '调用链' },
  { key: 'logs', label: '日志' },
  { key: 'config', label: '配置' },
  { key: 'instances', label: '实例' },
  { key: 'appHistory', label: '应用历史' },
  { key: 'commands', label: '命令' },
  { key: 'meter', label: '性能' },
  { key: 'services', label: '注册' },
  { key: 'consume', label: '消费' },
  { key: 'tracer', label: '监控中心' },
  { key: 'daystat', label: '监控大屏' },
  { key: 'versions', label: '版本' },
  { key: 'nodes', label: '节点' },
  { key: 'history', label: '部署历史' },
  { key: 'pipeline', label: '流水线' },
];

export const DEFAULT_TAB: AppTab = 'overview';

/** 路由 :tab → 合法页签；缺失、非法值（含旧书签的 ship）回落到概览（vue-router 重复参数时是数组，取首项） */
export function resolveTab(raw: unknown): AppTab {
  const v = Array.isArray(raw) ? raw[0] : raw;
  return WORKBENCH_TABS.find((t) => t.key === v)?.key ?? DEFAULT_TAB;
}

/** 页签文案：「实例」带实例数（原型里是「实例 N」），实例数未知时只显示「实例」 */
export function tabLabel(key: AppTab, instances?: number): string {
  const label = WORKBENCH_TABS.find((t) => t.key === key)?.label ?? '';
  return key === 'instances' && instances !== undefined ? `${label} ${instances}` : label;
}

export type DeploySection = 'versions' | 'nodes' | 'history' | 'pipeline'; // 与页签 key 同名

/** 页签 → 该页签渲染的部署内容块；非部署页签返回 null（仍显示「待接入」或各自内容） */
export function tabSection(key: AppTab): DeploySection | null {
  return key === 'versions' || key === 'nodes' || key === 'history' || key === 'pipeline' ? key : null;
}
