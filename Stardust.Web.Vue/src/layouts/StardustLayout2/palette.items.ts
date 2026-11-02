// 命令面板的条目生成（纯逻辑，无 Vue 依赖，依赖全部由调用方注入）。
// 对照原型 kit-app.jsx 的 openPalette / openVerbPalette。
// 原型的条目来自写死的演示数据 KD；这里不带演示数据：没有数据或没有处理函数的条目一律不生成。
import { NAV_ITEMS, type NavKey } from './nav.logic.ts';
import type { PaletteGroup, PaletteItem } from './palette.logic.ts';
import type { Verb } from './hotkeys.logic.ts';
import type { PaletteConfig } from './ui.store.ts';

/** 应用工作台页签（原型 openApp 的第二个参数） */
export type AppTab =
  | 'overview'
  | 'trace'
  | 'logs'
  | 'config'
  | 'instances'
  | 'appHistory'
  | 'commands'
  | 'meter'
  | 'services'
  | 'consume'
  | 'tracer'
  | 'daystat'
  | 'versions'
  | 'nodes'
  | 'history'
  | 'pipeline';

/** 面板只关心的应用字段；真实数据在接线层映射成它 */
export interface PaletteApp {
  id: number;
  name: string;
  category?: string;
  version?: string;
  instances?: number;
  online: boolean;
}

/** 面板只关心的节点字段 */
export interface PaletteNode {
  id: string | number;
  name: string;
  ip?: string;
  online: boolean;
}

export interface PaletteDeps {
  /** 路由跳转 */
  go: (path: string) => void;
  apps?: PaletteApp[];
  nodes?: PaletteNode[];
  /** 打开节点抽屉；不传则不生成「节点」分组 */
  openNode?: (id: PaletteNode['id']) => void;
  /** 打开某节点的终端；不传则 N 键无面板可开 */
  openTerm?: (id: PaletteNode['id']) => void;
  /** 打开底部控制台；不传则不生成对应快捷操作 */
  openDock?: () => void;
  /** 启动接入向导；不传则不生成对应快捷操作，A 键无动作 */
  startWizard?: () => void;
}

/** 动词键的结果：打开一个新面板，或交给调用方启动向导 */
export type VerbAction = { type: 'palette'; config: PaletteConfig } | { type: 'wizard' };

/** 原型：'搜索应用、节点、TraceId,或执行操作…' */
export const BASE_PLACEHOLDER = '搜索应用、节点、TraceId,或执行操作…';

// 原型 openPalette 的「页面」组：文案与 hint 照抄（第三项原型写作「星图」，导航条上叫「节点」）
const PAGE_ENTRIES: ReadonlyArray<{ key: NavKey; text: string; hint: string }> = [
  { key: 'now', text: '此刻', hint: '首页' },
  { key: 'apps', text: '应用', hint: '列表' },
  { key: 'nodes', text: '星图', hint: '节点' },
  { key: 'changes', text: '变更', hint: '跨应用时间线' },
  { key: 'mesh', text: '治理', hint: '低频配置' },
];

export function pagePath(key: NavKey): string {
  return NAV_ITEMS.find((n) => n.key === key)?.path ?? '/now';
}

/** 应用工作台路径：/app/:tab/:id；末尾是应用 Id（数字），仍做编码以防传入异常值 */
export function appPath(id: number, tab: AppTab): string {
  return `/app/${tab}/${encodeURIComponent(String(id))}`;
}

/** 解析路由里的应用 Id：只接受正整数，其余（缺失、非数字、小数、负数、前导 0）返回 null；数组取首项 */
export function parseAppId(raw: unknown): number | null {
  const v = Array.isArray(raw) ? raw[0] : raw;
  if (typeof v !== 'string' || !/^[1-9]\d*$/.test(v)) return null;
  const n = Number(v);
  return Number.isSafeInteger(n) ? n : null;
}

/** 按 Id 找应用名；Id 为空或找不到返回 null */
export function appNameById(apps: readonly PaletteApp[], id: number | null): string | null {
  if (id === null) return null;
  return apps.find((a) => a.id === id)?.name ?? null;
}

function group(title: string, list: PaletteItem[]): PaletteGroup | null {
  return list.length > 0 ? { title, list } : null;
}

function compact(groups: Array<PaletteGroup | null>): PaletteGroup[] {
  return groups.filter((g): g is PaletteGroup => g !== null);
}

/** ⌘K 打开的基础面板 */
export function buildBasePalette(deps: PaletteDeps): PaletteConfig {
  const { go, apps = [], nodes = [], openNode, openDock, startWizard } = deps;

  const pages = PAGE_ENTRIES.map<PaletteItem>((p) => ({
    text: `跳转 · ${p.text}`,
    hint: p.hint,
    run: () => go(pagePath(p.key)),
  }));

  const appItems = apps.map<PaletteItem>((a) => ({
    text: `应用 · ${a.name}`,
    hint: a.category,
    run: () => go(appPath(a.id, 'overview')),
  }));

  const nodeItems = openNode
    ? nodes.map<PaletteItem>((n) => ({ text: `节点 · ${n.name}`, hint: n.ip, run: () => openNode(n.id) }))
    : [];

  const quick: PaletteItem[] = [];
  if (startWizard) quick.push({ text: '操作 · 接入应用或节点', hint: '向导', run: startWizard });
  if (openDock) quick.push({ text: '操作 · 打开底部控制台', hint: '`', run: openDock });

  return {
    placeholder: BASE_PLACEHOLDER,
    groups: compact([group('页面', pages), group('应用', appItems), group('节点', nodeItems), group('快捷操作', quick)]),
  };
}

// 原型 appsPick：P/C/L 各自的目标页签与提示语
const APP_VERBS: Partial<Record<Verb, { tab: AppTab; placeholder: string }>> = {
  P: { tab: 'versions', placeholder: '发布哪个应用？' },
  C: { tab: 'config', placeholder: '改哪个应用的配置？' },
  L: { tab: 'logs', placeholder: '看哪个应用的日志？' },
};

function appHint(a: PaletteApp): string | undefined {
  const parts: string[] = [];
  if (a.version) parts.push(a.version);
  if (a.instances !== undefined) parts.push(`${a.instances} 实例`);
  return parts.length > 0 ? parts.join(' · ') : undefined;
}

/**
 * P/C/L/N/A 动词键。返回 null 表示无事可做（没有可选项，或缺少处理函数），调用方不应打开空面板。
 */
export function buildVerbAction(verb: Verb, deps: PaletteDeps): VerbAction | null {
  const { go, apps = [], nodes = [], openTerm, startWizard } = deps;

  const appVerb = APP_VERBS[verb];
  if (appVerb) {
    const list = apps
      .filter((a) => a.online)
      .map<PaletteItem>((a) => ({ text: a.name, hint: appHint(a), run: () => go(appPath(a.id, appVerb.tab)) }));
    const g = group('应用', list);
    return g ? { type: 'palette', config: { placeholder: appVerb.placeholder, groups: [g] } } : null;
  }

  if (verb === 'N') {
    if (!openTerm) return null;
    const list = nodes
      .filter((n) => n.online)
      .map<PaletteItem>((n) => ({ text: n.name, hint: n.ip, run: () => openTerm(n.id) }));
    const g = group('在线节点', list);
    return g ? { type: 'palette', config: { placeholder: '连接哪台节点？', groups: [g] } } : null;
  }

  if (verb === 'A') return startWizard ? { type: 'wizard' } : null;

  return null;
}
