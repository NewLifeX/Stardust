// 页面随项目 / 环境上下文取数的组合函数（纯逻辑在 contextReload.logic.ts）。
// 上下文就绪后首轮取数，之后项目或环境变化再取；快速切换时旧响应由 isStale 判定丢弃。
// 上下文由调用方传入（页面传 stardustContext），本文件不依赖 store，便于单测。
import { watch } from 'vue';
import { canLoad, contextKey, createRequestGuard, type ContextLike } from './contextReload.logic';

/** load 收到 isStale：await 之后、写入页面状态之前先判断，返回 true 就丢弃本次结果 */
export type ContextLoader = (isStale: () => boolean) => void | Promise<void>;

export function useContextReload(load: ContextLoader, ctx: ContextLike): { reload: () => void } {
  const guard = createRequestGuard();
  const reload = (): void => {
    if (!canLoad(ctx)) return;
    const token = guard.begin();
    void load(() => !guard.isLatest(token));
  };
  watch(() => contextKey(ctx), reload, { immediate: true });
  return { reload };
}