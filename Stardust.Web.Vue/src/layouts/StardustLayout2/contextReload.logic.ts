// 项目 / 环境上下文变化时，页面重新取数的纯逻辑（无 Vue 依赖，便于单测）。
// 沿用旧 AdminCenter.vue 的做法：把 ready、projectId、environment 拼成一个 key，
// 就绪与后续切换共用同一条触发路径；ready 之前不取数，避免先按空项目打一轮请求、再整轮重打。
// 请求本身不手拼 projectId：stores/stardustContext.ts 的全局拦截器会注入。

/** 上下文里只关心的三个字段（StardustContext 结构兼容） */
export interface ContextLike {
  ready: boolean;
  projectId: number | null;
  environment: string | null;
}

/** 上下文变化的触发键：就绪、项目、环境任一变化，key 就变 */
export function contextKey(ctx: ContextLike): string {
  return `${ctx.ready ? 1 : 0}|${ctx.projectId ?? ''}|${ctx.environment ?? ''}`;
}

/** 上下文就绪后才允许取数 */
export function canLoad(ctx: ContextLike): boolean {
  return ctx.ready;
}

export interface RequestGuard {
  /** 发起一次请求前调用，返回该请求的令牌 */
  begin: () => number;
  /** 响应回来时检查：不是最新一次请求的令牌就丢弃结果 */
  isLatest: (token: number) => boolean;
}

/** 防止「快速切项目时，旧项目的慢响应覆盖新项目的数据」 */
export function createRequestGuard(): RequestGuard {
  let seq = 0;
  return {
    begin: () => ++seq,
    isLatest: (token) => token === seq,
  };
}