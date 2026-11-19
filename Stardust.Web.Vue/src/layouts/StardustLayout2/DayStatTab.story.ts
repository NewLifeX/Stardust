import DayStatTab from './DayStatTab.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 请求桩：AppDayStat 返回 3 天、AppMinuteStat 返回 2 个点；Trace 统计端点按 type 分支（二次钻取回归桩），其它路径返回空 */
function ok(): Req {
  return async (cfg) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('AppMinuteStat')) {
      return {
        data: [
          { ID: 2, StatTime: '2026-11-03 09:55:00', Total: 300, Errors: 0, Cost: 9, MaxCost: 30, MinCost: 2 },
          { ID: 1, StatTime: '2026-11-03 10:05:00', Total: 500, Errors: 6, Cost: 12, MaxCost: 40, MinCost: 3 },
        ],
      };
    }
    if (url.includes('TraceItem')) {
      return {
        data: [
          { ID: 11, Name: 'StarServer/Home', Kind: '接口', Enable: true },
          { ID: 12, Name: 'sql/select-user', Kind: '数据库', Enable: true },
        ],
      };
    }
    if (/Trace(Day|Hour|Minute)Stat/.test(url)) {
      const type = String((cfg?.params as { type?: string } | undefined)?.type ?? '');
      // 二次钻取回归桩：按 type 返回可区分行；无 type（未过滤）保持 3 行混合（旧用例口径）
      if (type === 'Api') return { data: [{ ID: 21, StatDate: '2026-11-03', Name: '/api/Galaxy', Type: 'Api', Total: 106, RingRate: 1.2, Errors: 0, ErrorRate: 0, Cost: 15, MaxCost: 120, MinCost: 3, TotalValue: 0 }] };
      if (type === 'Http') return { data: [{ ID: 22, StatDate: '2026-11-03', Name: 'http/webhook', Type: 'Http', Total: 88, RingRate: 0.75, Errors: 1, ErrorRate: 0.011, Cost: 22, MaxCost: 90, MinCost: 5, TotalValue: 0 }] };
      if (type === 'Db') return { data: [{ ID: 23, StatDate: '2026-11-03', Name: 'sql/select-user', Type: 'Db', Total: 42, RingRate: 1, Errors: 0, ErrorRate: 0, Cost: 9, MaxCost: 45, MinCost: 1, TotalValue: 18 }] };
      if (type === 'Mq') return { data: [{ ID: 24, StatDate: '2026-11-03', Name: 'mq/order', Type: 'Mq', Total: 7, RingRate: 1, Errors: 0, ErrorRate: 0, Cost: 5, MaxCost: 9, MinCost: 2, TotalValue: 0 }] };
      if (type === 'Redis') return { data: [{ ID: 25, StatDate: '2026-11-03', Name: 'redis/cache', Type: 'Redis', Total: 3, RingRate: 1, Errors: 0, ErrorRate: 0, Cost: 2, MaxCost: 4, MinCost: 1, TotalValue: 0 }] };
      return {
        data: [
          { ID: 21, StatDate: '2026-11-03', Name: 'StarServer/Home', Type: 'Api', Total: 1200, RingRate: 1.2, Errors: 24, ErrorRate: 0.02, Cost: 15, MaxCost: 120, MinCost: 3, TotalValue: 0 },
          { ID: 20, StatDate: '2026-11-02', Name: 'sql/select-user', Type: 'Db', Total: 300, RingRate: 0.75, Errors: 2, ErrorRate: 0.0045, Cost: 9, MaxCost: 45, MinCost: 1, TotalValue: 18 },
          { ID: 19, StatDate: '2026-11-01', Name: 'http/webhook', Type: 'Http', Total: 88, RingRate: 0, Errors: 1, ErrorRate: 0.0011, Cost: 22, MaxCost: 90, MinCost: 5, TotalValue: 0 },
        ],
      };
    }
    return {
      data: [
        { ID: 3, StatDate: '2026-11-03', Total: 500, Errors: 6, Cost: 12, MaxCost: 40, MinCost: 3, Names: 12, Apis: 2, Https: 3, Dbs: 4, Mqs: 1, Redis: 2, ErrorRate: 1.2 },
        { ID: 2, StatDate: '2026-11-02', Total: 420, Errors: 2, Cost: 11, MaxCost: 35, MinCost: 2, Names: 10, Apis: 2, Https: 2, Dbs: 3, Mqs: 0, Redis: 1, ErrorRate: 0.48 },
        { ID: 1, StatDate: '2026-11-01', Total: 380, Errors: 0, Cost: 10, MaxCost: 30, MinCost: 2, Names: 9, Apis: 2, Https: 2, Dbs: 3, Mqs: 0, Redis: 1, ErrorRate: 0 },
      ],
    };
  };
}
function empty(): Req {
  return async () => ({ data: [] });
}
function fail(): Req {
  return async () => {
    throw new Error('stub 500');
  };
}

export const stories = [
  { id: 'DayStatTab/Chart', component: DayStatTab, props: { appId: 32, appName: 'IoC容器', deps: { request: ok() } } },
  { id: 'DayStatTab/Empty', component: DayStatTab, props: { appId: 5, appName: '新应用', deps: { request: empty() } } },
  { id: 'DayStatTab/Failed', component: DayStatTab, props: { appId: 32, appName: 'IoC容器', deps: { request: fail() } } },
];
