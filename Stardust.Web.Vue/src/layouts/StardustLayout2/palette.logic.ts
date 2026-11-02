// 命令面板（纯逻辑）：分组过滤、选中项移动、按键解析。对照原型 kit-shell.jsx 的 Palette。

export interface PaletteItem {
  text: string;
  hint?: string;
  run: () => void;
}

export interface PaletteGroup {
  title: string;
  list: PaletteItem[];
}

export type PaletteKeyAction =
  | { type: 'close' }
  | { type: 'select'; index: number }
  | { type: 'run'; index: number }
  /** 已识别但无可执行项（如空结果时按 Enter）：需要 preventDefault，不做其它事 */
  | { type: 'none' };

/** 对「标题 + 提示」做不区分大小写的包含匹配；空查询返回全部；丢弃空分组 */
export function filterGroups(groups: PaletteGroup[], query: string): PaletteGroup[] {
  const q = query.trim().toLowerCase();
  return groups
    .map((g) => ({
      ...g,
      list: g.list.filter((it) => !q || `${it.text} ${it.hint ?? ''}`.toLowerCase().includes(q)),
    }))
    .filter((g) => g.list.length > 0);
}

/** 展平成一维列表，下标即选中索引 */
export function flattenGroups(groups: PaletteGroup[]): PaletteItem[] {
  return groups.flatMap((g) => g.list);
}

/** 选中项移动并钳制在 [0, length-1]；列表为空时恒为 0 */
export function moveSelection(sel: number, delta: number, length: number): number {
  if (length <= 0) return 0;
  return Math.min(length - 1, Math.max(0, sel + delta));
}

/** 面板打开时的按键；返回 null 表示不处理 */
export function resolvePaletteKey(key: string, sel: number, length: number): PaletteKeyAction | null {
  switch (key) {
    case 'Escape':
      return { type: 'close' };
    case 'ArrowDown':
      return { type: 'select', index: moveSelection(sel, 1, length) };
    case 'ArrowUp':
      return { type: 'select', index: moveSelection(sel, -1, length) };
    case 'Enter':
      return length > 0 && sel >= 0 && sel < length ? { type: 'run', index: sel } : { type: 'none' };
    default:
      return null;
  }
}
