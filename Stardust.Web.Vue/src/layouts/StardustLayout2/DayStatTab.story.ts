import DayStatTab from './DayStatTab.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 请求桩：AppDayStat 返回 3 天、AppMinuteStat 返回 2 个点，其它路径返回空 */
function ok(): Req {
  return async (cfg: { url?: string }) => {
    if (String(cfg?.url ?? '').includes('AppMinuteStat')) {
      return {
        data: [
          { ID: 2, StatTime: '2026-11-03 09:55:00', Total: 300, Errors: 0, Cost: 9, MaxCost: 30, MinCost: 2 },
          { ID: 1, StatTime: '2026-11-03 10:05:00', Total: 500, Errors: 6, Cost: 12, MaxCost: 40, MinCost: 3 },
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
