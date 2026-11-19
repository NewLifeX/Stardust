import AlarmsRules from './AlarmsRules.vue';
import type { Req } from '../../../src/views/Stardust/appsModel.ts';

/**
 * AlarmsRules story（CARD-MON-13）。请求桩按 url 分流：
 *  - GET    /Monitors/TraceRule        → 规则列表（PascalCase 行，同时验证双取映射）
 *  - DELETE /Monitors/TraceRule/{id}   → 从内存数组删除（有状态：CT 验证「确认后行消失」）
 * 数据形状取自真源：C05 默认三条白名单（跟踪规则.Biz.cs InitData）+ 一条黑名单停用行（展示 Tag 三态）。
 * 每次测试 page.goto 重新加载 gallery 页，模块级数组随之重置，用例间无共享状态。
 */
const RULES: Array<Record<string, unknown>> = [
  {
    Id: 1, Rule: '^/Admin/[A-Z]\\w+', Enable: true, IsWhite: true, IsRegex: true, Priority: 100,
    Remark: '后台管理默认放行',
    CreateUser: 'admin', CreateTime: '2026-11-09 10:00:00', CreateIP: '127.0.0.1',
    UpdateUser: 'admin', UpdateTime: '2026-11-09 10:30:00', UpdateIP: '127.0.0.1',
  },
  {
    Id: 2, Rule: '^/Cube/[A-Z]\\w+', Enable: true, IsWhite: true, IsRegex: true, Priority: 90,
    Remark: 'Cube 默认放行',
    CreateUser: 'admin', CreateTime: '2026-11-09 10:00:00', CreateIP: '127.0.0.1',
    UpdateUser: 'admin', UpdateTime: '2026-11-09 10:30:00', UpdateIP: '127.0.0.1',
  },
  {
    Id: 3, Rule: '/Sso/*', Enable: true, IsWhite: true, IsRegex: false, Priority: 0,
    Remark: '单点登录模糊放行',
    CreateUser: 'admin', CreateTime: '2026-11-09 10:00:00', CreateIP: '127.0.0.1',
    UpdateUser: 'admin', UpdateTime: '2026-11-09 10:30:00', UpdateIP: '127.0.0.1',
  },
  {
    Id: 4, Rule: '/api/Debug/*', Enable: false, IsWhite: false, IsRegex: false, Priority: 0,
    Remark: '黑名单示例（停用）',
    CreateUser: 'admin', CreateTime: '2026-11-09 10:00:00', CreateIP: '127.0.0.1',
    UpdateUser: 'admin', UpdateTime: '2026-11-09 10:30:00', UpdateIP: '127.0.0.1',
  },
];

function liveReq(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (url.includes('/Monitors/TraceRule')) {
      if (String(opt.method ?? '').toUpperCase() === 'DELETE') {
        const id = Number(url.split('/').pop());
        const i = RULES.findIndex((r) => r.Id === id);
        if (i >= 0) RULES.splice(i, 1);
        return { data: true };
      }
      return { data: RULES };
    }
    return { data: [] };
  };
}

function emptyReq(): Req {
  return async () => ({ data: [] });
}

function failReq(): Req {
  return async () => {
    throw new Error('stub 500');
  };
}

export const stories = [
  { id: 'AlarmsRules/Data', component: AlarmsRules, props: { deps: { request: liveReq() } } },
  { id: 'AlarmsRules/Empty', component: AlarmsRules, props: { deps: { request: emptyReq() } } },
  { id: 'AlarmsRules/Failed', component: AlarmsRules, props: { deps: { request: failReq() } } },
];
