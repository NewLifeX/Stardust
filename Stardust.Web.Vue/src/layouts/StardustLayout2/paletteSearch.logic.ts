// 全局搜索面板的统一打开器：顶栏搜索 / ⌘K / 此刻页搜索框共用同一入口（2026-11-16 输入2）。
// 行为：
//  1) 立即打开基础面板：页面 + 快捷操作 + 追踪跳转（不依赖网络，先能搜能跳）；
//  2) 应用/节点数据到达后，若面板还开着且仍停留在本搜索面板，用带数据的配置替换
//     （查询与选中状态在组件内部，替换 config 不打断输入）；
//  3) 取数失败且仍停留时给可见提示（页面与追踪跳转不受影响，应用/节点搜不了要说明原因）。
// 面板已关或已切到别的面板（如动词键面板）时，迟到的数据绝不把面板重新弹出或顶掉现面板：
// openPalette 的语义就是「替换/打开」，必须用对象同一性守卫。
import { buildBasePalette } from './palette.items.ts';
import { openPalette, palette, paletteOpen } from './ui.store.ts';
import { createPaletteRuntime, type PaletteRuntime } from './paletteRuntime.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

export interface SearchNavDeps {
  /** 路径跳转（页面/应用工作台） */
  go: (path: string) => void;
  /** 追踪跳转：命名路由 ConsoleTrace（禁路径跳转）；不传则面板不带「链路追踪」组 */
  goTrace?: (ids: string) => void;
  /** 节点搜索结果的动作（与动词键 N 同语义：开终端）；不传则不生成「节点」组 */
  openNode?: (id: number | string) => void;
  /** 取数失败提示（生产传 toast）；不传则静默降级为基础面板 */
  warn?: (message: string) => void;
}

export const SEARCH_DATA_FAILED_MESSAGE = '应用与节点搜索暂不可用，页面与追踪跳转不受影响';

// 模块级共享运行时：外壳与页面打开的是同一个面板，应用/节点缓存只该有一份
// （index.vue 的上下文失效 watch 也调这份实例的 invalidate）。
let runtime: PaletteRuntime | null = null;

/** 外壳启动时初始化共享运行时；重复调用返回同一实例 */
export function initSearchRuntime(request: Req): PaletteRuntime {
  runtime ??= createPaletteRuntime(request);
  return runtime;
}

/** 读共享运行时；外壳未初始化时为 null（此刻页等调用方降级为基础面板） */
export function searchRuntime(): PaletteRuntime | null {
  return runtime;
}

/** 打开全局搜索面板。rt 不传则用共享运行时（测试可注入替身） */
export function openSearchPalette(deps: SearchNavDeps, rt: PaletteRuntime | null = searchRuntime()): void {
  // 先开基础面板（对象留引用，数据到达时核对是否仍停留在本面板）
  const base = buildBasePalette({ go: deps.go, goTrace: deps.goTrace, openNode: deps.openNode });
  openPalette(base);
  if (!rt) return;

  void rt
    .ensure()
    .then((data) => {
      // 面板已关，或用户已切到别的面板（动词键 P/C/L/N）：不能顶掉
      if (!paletteOpen.value || palette.value !== base) return;
      openPalette(
        buildBasePalette({
          go: deps.go,
          goTrace: deps.goTrace,
          openNode: deps.openNode,
          apps: data.apps,
          nodes: data.nodes,
        }),
      );
    })
    .catch(() => {
      if (paletteOpen.value && palette.value === base) deps.warn?.(SEARCH_DATA_FAILED_MESSAGE);
    });
}
