// 命令面板的会话状态（无生命周期、无 DOM，依赖由调用方注入，便于单测）。
// 对照原型 kit-shell.jsx 的 Palette：q / sel、分组过滤、选中项随查询归零、
// 回车与点击都是“先关闭，再执行”。
// 每次调用都会新建状态：组件用 v-if 挂载，每次打开自然重置（原型也是在 open 变化时清空）。
import { computed, ref } from 'vue';
import type { PaletteConfig } from '../ui.store.ts';
import {
  filterGroups,
  flattenGroups,
  resolvePaletteKey,
  type PaletteItem,
  type PaletteKeyAction,
} from '../palette.logic.ts';

/** 原型：placeholder || '搜索应用、节点、TraceId,或执行操作…' */
export const DEFAULT_PLACEHOLDER = '搜索应用、节点、TraceId,或执行操作…';

export interface PaletteStateDeps {
  /** 当前面板配置；null 表示未打开 */
  config: () => PaletteConfig | null;
  /** 关闭面板（通常是 ui.store 的 closePalette） */
  close: () => void;
}

/** 渲染用的行：index 是展平后的全局下标，与选中项对应 */
export interface PaletteRow {
  item: PaletteItem;
  index: number;
}

export interface PaletteView {
  title: string;
  rows: PaletteRow[];
}

export function usePaletteState(deps: PaletteStateDeps) {
  const query = ref('');
  const sel = ref(0);

  const groups = computed(() => filterGroups(deps.config()?.groups ?? [], query.value));
  const flat = computed(() => flattenGroups(groups.value));
  const placeholder = computed(() => deps.config()?.placeholder || DEFAULT_PLACEHOLDER);

  /** 分组视图：给每一行标上全局下标，模板里不必再维护计数器 */
  const view = computed<PaletteView[]>(() => {
    let i = 0;
    return groups.value.map((g) => ({
      title: g.title,
      rows: g.list.map((item) => ({ item, index: i++ })),
    }));
  });

  /** 查询变化时选中项归零（原型：useEffect(() => setSel(0), [q])） */
  function setQuery(value: string): void {
    query.value = value;
    sel.value = 0;
  }

  /** 鼠标移入某一行即选中它 */
  function hover(index: number): void {
    sel.value = index;
  }

  /** 先关闭再执行（原型：onClose(); it.f()）；下标越界返回 false，什么都不做 */
  function activate(index: number): boolean {
    const item = flat.value[index];
    if (!item) return false;
    deps.close();
    item.run();
    return true;
  }

  /**
   * 处理面板打开时的按键。返回 null 表示不处理；
   * 返回动作表示已识别，调用方应 preventDefault（原型四个按键都会）。
   */
  function onKey(key: string): PaletteKeyAction | null {
    const action = resolvePaletteKey(key, sel.value, flat.value.length);
    if (!action) return null;
    if (action.type === 'close') deps.close();
    else if (action.type === 'select') sel.value = action.index;
    else if (action.type === 'run') activate(action.index);
    return action;
  }

  return { query, sel, placeholder, view, flat, setQuery, hover, activate, onKey };
}
