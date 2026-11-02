/** 把顶栏上下文合并进请求 query。调用方已经写上的同名参数优先，避免顶栏项目盖住表单里选中的项目。 */
export function mergeContextParams(
  explicit: Record<string, unknown> | undefined,
  ctx: { projectId?: number; environment?: string },
): Record<string, unknown> {
  const base: Record<string, unknown> = {};
  if (ctx.projectId != null) base.projectId = ctx.projectId;
  if (ctx.environment != null && ctx.environment !== '') base.environment = ctx.environment;
  return { ...base, ...(explicit ?? {}) };
}
