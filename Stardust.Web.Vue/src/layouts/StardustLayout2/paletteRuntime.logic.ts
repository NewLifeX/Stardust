// 命令面板数据的缓存运行时（纯逻辑，无 Vue 依赖，request 由调用方注入）。
// 从 index.vue 抽离：缓存命中直接返回、并发复用同一个请求、部分失败不缓存下次重取、
// 上下文变化时使缓存失效（在途旧请求结果作废）。
// 参照 contextReload.logic.ts 的注入风格：不持有响应式状态，只管理 Promise 与缓存。
import { fetchPaletteData, type PaletteData } from './paletteData.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

export interface PaletteRuntime {
  /** 当前缓存的数据（可能为 null：尚未拉取或已失效） */
  peek: () => PaletteData | null;
  /** 确保数据已加载：缓存命中直接返回；并发复用同一个请求；部分失败不算就绪 */
  ensure: () => Promise<PaletteData>;
  /** 使缓存失效：清空缓存并让在途请求的结果作废（项目/环境变化时调用） */
  invalidate: () => void;
}

/**
 * 创建一个面板数据运行时实例。
 * 每个实例持有自己的缓存与在途请求；通常整个外壳只创建一个。
 */
export function createPaletteRuntime(request: Req): PaletteRuntime {
  let cached: PaletteData | null = null;
  let pending: Promise<PaletteData> | null = null;
  let seq = 0;

  function ensure(): Promise<PaletteData> {
    // 缓存命中且全部成功：直接返回
    if (cached && cached.failed.length === 0) return Promise.resolve(cached);

    // 已有在途请求：复用同一个 Promise（并发去重）
    if (pending) return pending;

    const token = ++seq;
    pending = fetchPaletteData(request)
      .then((d) => {
        // 只有最新一次请求的结果才写入缓存（防上下文切换竞态）
        if (token === seq) cached = d;
        return d;
      })
      .finally(() => {
        // 只有最新一次请求才清除在途标记（旧请求完成不影响新请求）
        if (token === seq) pending = null;
      });

    return pending;
  }

  function invalidate(): void {
    ++seq; // 作废所有在途请求的结果
    cached = null;
    pending = null;
  }

  return { peek: () => cached, ensure, invalidate };
}
