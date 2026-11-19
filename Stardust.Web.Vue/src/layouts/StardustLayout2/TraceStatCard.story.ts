import TraceStatCard from './TraceStatCard.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 请求桩：按 URL 分发 TraceItem / Trace{Day|Hour|Minute}Stat / Trace?id= */
function ok(): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('TraceItem')) {
      return {
        data: [
          { ID: 11, Name: 'StarServer/Home', Kind: '接口', Enable: true },
          { ID: 12, Name: 'sql/select-user', Kind: '数据库', Enable: true },
        ],
      };
    }
    if (url.includes('Trace')) {
      return {
        data: [
          { ID: 21, StatDate: '2026-11-03', Name: 'StarServer/Home', Type: 'Api', Total: 1200, RingRate: 1.2, Errors: 24, ErrorRate: 0.02, Cost: 15, MaxCost: 120, MinCost: 3, TotalValue: 0 },
          { ID: 20, StatDate: '2026-11-02', Name: 'sql/select-user', Type: 'Db', Total: 300, RingRate: 0.75, Errors: 2, ErrorRate: 0.0045, Cost: 9, MaxCost: 45, MinCost: 1, TotalValue: 18 },
          { ID: 19, StatDate: '2026-11-01', Name: 'http/webhook', Type: 'Http', Total: 88, RingRate: 0, Errors: 1, ErrorRate: 0.0011, Cost: 22, MaxCost: 90, MinCost: 5, TotalValue: 0 },
        ],
      };
    }
    return { data: [] };
  };
}
function empty(): Req {
  return async () => ({ data: [] });
}
/** 失败桩（Mixed）：埋点下拉成功、统计接口 throw——语义=列表可用但统计失败（真实流主场景） */
function failMixed(): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('TraceItem')) {
      return { data: [{ ID: 11, Name: 'StarServer/Home', Kind: '接口', Enable: true }] };
    }
    throw new Error('stub 500');
  };
}

export const stories = [
  { id: 'TraceStatCard/WithData', component: TraceStatCard, props: { appId: 32, appName: 'IoC容器', deps: { request: ok() } } },
  { id: 'TraceStatCard/Drill', component: TraceStatCard, props: { appId: 32, appName: 'IoC容器', deps: { request: ok() }, drill: { date: '2026-11-03', type: 'Api', seq: 1 } } },
  { id: 'TraceStatCard/Empty', component: TraceStatCard, props: { appId: 5, appName: '新应用', deps: { request: empty() } } },
  { id: 'TraceStatCard/Failed', component: TraceStatCard, props: { appId: 32, appName: 'IoC容器', deps: { request: failMixed() } } },
];
