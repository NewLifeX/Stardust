import TraceItemsPanel from './TraceItemsPanel.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/**
 * 请求桩：GET /Monitors/TraceItem 返回 3 行（Id Int32 → string；kind 3 种；enable 三态各一），
 * GET /Monitors/TraceItem/GetKinds 返回词典（与后端 GetKinds() IDictionary<String,String> 信封一致）。
 * 种类值与 Stardust.Data GetKinds 实际展示名同源（Http/Sql/Redis）。
 */
function ok(): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/Monitors/TraceItem/GetKinds')) {
      return { data: { Http: 'Http远程请求', Sql: 'Sql执行', Redis: 'Redis缓存' }, total: 0 };
    }
    if (url.includes('/Monitors/TraceItem')) {
      return {
        data: [
          { Id: 11, AppId: 32, Kind: 'Http', Name: 'GET /api/user', DisplayName: '用户接口', Enable: true, Rules: 'name=*/api/user*', Cloned: false, Timeout: 3, Days: 41, Total: 1064, Errors: 2, UpdateUser: 'admin', UpdateTime: '2026-11-09 08:12:00' },
          { Id: 12, AppId: 32, Kind: 'Sql', Name: 'User.FindAll', DisplayName: '', Enable: false, Rules: '', Cloned: true, Timeout: 0, Days: 40, Total: 385364, Errors: 1497, UpdateUser: 'admin', UpdateTime: '2026-11-08 21:40:00' },
          { Id: 13, AppId: 32, Kind: 'Redis', Name: 'redis://cache/get', DisplayName: '缓存读取', Enable: null, Rules: 'clientId=10.10.*', Cloned: false, Timeout: 5, Days: 12, Total: 9932, Errors: 0, UpdateUser: '', UpdateTime: '' },
        ],
        total: 3,
      };
    }
    return { data: [], total: 0 };
  };
}

function empty(): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/Monitors/TraceItem/GetKinds')) return { data: {}, total: 0 };
    return { data: [], total: 0 };
  };
}

/** 加载桩：永不 resolve，锁定 loading 态 */
function pending(): Req {
  return async () => new Promise(() => {});
}

/** 失败桩：throw 语义（错误透传 message）——「跟踪项加载失败：{原因}」分支 */
function fail(): Req {
  return async (cfg: { url?: string }) => {
    if (String(cfg?.url ?? '').includes('/Monitors/TraceItem/GetKinds')) return { data: {}, total: 0 };
    throw new Error('示例数据源未启用');
  };
}

export const stories = [
  { id: 'TraceItemsPanel/WithData', component: TraceItemsPanel, props: { appId: 32, appName: 'IoC容器', deps: { request: ok() } } },
  { id: 'TraceItemsPanel/Empty', component: TraceItemsPanel, props: { appId: 5, appName: '新应用', deps: { request: empty() } } },
  { id: 'TraceItemsPanel/Loading', component: TraceItemsPanel, props: { appId: 32, appName: 'IoC容器', deps: { request: pending() } } },
  { id: 'TraceItemsPanel/Failed', component: TraceItemsPanel, props: { appId: 32, appName: 'IoC容器', deps: { request: fail() } } },
];
