import AppTracerTab from './AppTracerTab.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/**
 * 请求桩：按 url 分支（CARD-MON-11 接线后宿主含 TraceDataPanel，CARD-MON-12 后再含 TraceItemsPanel，须分别供数）。
 * /Monitors/TraceData 回 4 行与 TraceDataPanel.story.ts 基准一致（总计行手算同源）；
 * /Monitors/TraceItem（含 /GetKinds）回 3 行与词典（与 TraceItemsPanel.story.ts 基准一致）；
 * 其余（AppTracer CRUD）回固定行（PascalCase 行同时验证双取映射）。
 */
function ok(rows: unknown[]): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/Monitors/TraceData')) {
      return {
        data: [
          { Id: '9007199254740993', AppId: 32, ItemId: 11, Name: 'GET /api/user', ClientId: 'web-01', StatMinute: '2026-11-09 08:05', StatHour: '2026-11-09 08:00', StartTime: 0, EndTime: 0, Total: 15, Errors: 0, TotalCost: 3000, Cost: 200, MaxCost: 450, MinCost: 80 },
          { Id: '9007199254740994', AppId: 32, ItemId: 11, Name: 'GET /api/user', ClientId: 'web-02', StatMinute: '2026-11-09 09:05', StatHour: '2026-11-09 09:00', StartTime: 0, EndTime: 0, Total: 11, Errors: 1, TotalCost: 378, Cost: 34, MaxCost: 88, MinCost: 12 },
          { Id: '9007199254740995', AppId: 32, ItemId: 12, Name: 'POST /api/order', ClientId: 'web-01', StatMinute: '2026-11-09 10:05', StatHour: '2026-11-09 10:00', StartTime: 0, EndTime: 0, Total: 3, Errors: 0, TotalCost: 900, Cost: 300, MaxCost: 350, MinCost: 260 },
          { Id: '9007199254740996', AppId: 32, ItemId: 12, Name: 'POST /api/order', ClientId: 'web-02', StatMinute: '2026-11-09 08:15', StatHour: '2026-11-09 08:00', StartTime: 0, EndTime: 0, Total: 4, Errors: 2, TotalCost: 4200, Cost: 1050, MaxCost: 2600, MinCost: 40 },
        ],
        total: 4,
      };
    }
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
    return { data: rows };
  };
}
function fail(): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/Monitors/TraceData')) throw new Error('追踪数据源不可用');
    throw new Error('stub 500');
  };
}
function empty(): Req {
  return async () => ({ data: [] });
}

const tracers = [
  {
    ID: 1, AppId: 32, Name: 'ioc', DisplayName: 'IoC容器', Category: '中间件', ItemCount: 12, Days: 30, Total: 900,
    Enable: true, Mode: 0, Period: 60, MaxSamples: 10, MaxErrors: 10, EnableMeter: true, Timeout: 5000, UpdateTime: '2026-11-02 20:15',
  },
  {
    ID: 2, AppId: 32, Name: 'antjob', DisplayName: '', Category: '任务', ItemCount: 5, Days: 7, Total: 120,
    Enable: false, Mode: 1, Period: 300, MaxSamples: 5, MaxErrors: 5, EnableMeter: false, Timeout: 5000, UpdateTime: '2026-11-01 09:00',
  },
];

export const stories = [
  { id: 'AppTracerTab/List', component: AppTracerTab, props: { appId: 32, appName: 'IoC容器', deps: { request: ok(tracers) } } },
  { id: 'AppTracerTab/Empty', component: AppTracerTab, props: { appId: 5, appName: '新应用', deps: { request: empty() } } },
  { id: 'AppTracerTab/Failed', component: AppTracerTab, props: { appId: 32, appName: 'IoC容器', deps: { request: fail() } } },
];
