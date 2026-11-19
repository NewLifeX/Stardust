import TraceDataPanel from './TraceDataPanel.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/**
 * 请求桩：GET /Monitors/TraceData 返回 4 行（与 traceData.logic.test.ts 的 row(over) 手算基准一致）。
 * Id 用雪花超界字符串；StartTime=0 → fmtUnixMs 显示「—」（时区无关，视觉基线稳定）。
 * StatHour 乱序穿插（10 点在前），验证前端按 StatHour 升序重排。
 */
function ok(): Req {
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
    return { data: [], total: 0 };
  };
}

function empty(): Req {
  return async () => ({ data: [], total: 0 });
}

/** 加载桩：永不 resolve，锁定 loading 态 */
function pending(): Req {
  return async () => new Promise(() => {});
}

/** 失败桩：throw 语义（错误透传 message）——与 TraceStatCard.story.ts 的 failMixed 先例同形态 */
function fail(): Req {
  return async () => {
    throw new Error('示例数据源未启用');
  };
}

/** 单行桩：itemId 条件外的「双图隐藏」第二分支（行数不足） */
function single(): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/Monitors/TraceData')) {
      return {
        data: [
          { Id: '9007199254740993', AppId: 32, ItemId: 11, Name: 'GET /api/user', ClientId: 'web-01', StatMinute: '2026-11-09 08:05', StatHour: '2026-11-09 08:00', StartTime: 0, EndTime: 0, Total: 15, Errors: 0, TotalCost: 3000, Cost: 200, MaxCost: 450, MinCost: 80 },
        ],
        total: 1,
      };
    }
    return { data: [], total: 0 };
  };
}

export const stories = [
  { id: 'TraceDataPanel/WithData', component: TraceDataPanel, props: { appId: 32, appName: 'IoC容器', deps: { request: ok() } } },
  { id: 'TraceDataPanel/Empty', component: TraceDataPanel, props: { appId: 5, appName: '新应用', deps: { request: empty() } } },
  { id: 'TraceDataPanel/Loading', component: TraceDataPanel, props: { appId: 32, appName: 'IoC容器', deps: { request: pending() } } },
  { id: 'TraceDataPanel/Failed', component: TraceDataPanel, props: { appId: 32, appName: 'IoC容器', deps: { request: fail() } } },
  { id: 'TraceDataPanel/SingleRow', component: TraceDataPanel, props: { appId: 32, appName: 'IoC容器', deps: { request: single() } } },
];
