// 应用工作台页（Console/App.vue）的纯逻辑（无 Vue 依赖，便于单测）。
// 对照原型 page-apps.jsx 的 PageApp：按路由 :id 找应用、判定页面状态、状态 → Tag 语气。
import type { AppAggRow, AppStatus } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 页面视图：id 非法、加载中、加载失败、找不到应用、正常 */
export type WorkbenchView = 'invalid' | 'loading' | 'failed' | 'notfound' | 'ok';

export type StatusTagTone = 'default' | 'ok' | 'warn' | 'err';

/** 按应用 Id 找行；id 为空或找不到返回 null（后端偶尔把 id 当字符串返回，统一转数字比较） */
export function findAppById<T extends Pick<AppAggRow, 'id'>>(rows: readonly T[], id: number | null): T | null {
  if (id === null) return null;
  return rows.find((r) => Number(r.id) === id) ?? null;
}

/**
 * 页面视图判定。优先级：id 非法（不用等请求）> 加载中 > 加载失败 > 找不到 > 正常。
 * 「id 非法」与「找不到应用」要分开提示，不能留空白。
 */
export function resolveWorkbenchView(input: {
  id: number | null;
  load: 'loading' | 'ok' | 'failed';
  found: boolean;
}): WorkbenchView {
  if (input.id === null) return 'invalid';
  if (input.load === 'loading') return 'loading';
  if (input.load === 'failed') return 'failed';
  return input.found ? 'ok' : 'notfound';
}

/** 应用状态 → Tag 语气：运行 ok，异常与发布失败 err，部署中与维护 warn，其余灰 */
export function statusTone(status: AppStatus): StatusTagTone {
  switch (status) {
    case 'running':
      return 'ok';
    case 'error':
    case 'publishFailed':
      return 'err';
    case 'deploying':
    case 'maintenance':
      return 'warn';
    default:
      return 'default';
  }
}