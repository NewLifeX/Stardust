import AppTracerTab from './AppTracerTab.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 请求桩：按 url 返回固定行（PascalCase 行同时验证双取映射） */
function ok(rows: unknown[]): Req {
  return async () => ({ data: rows });
}
function fail(): Req {
  return async () => {
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
