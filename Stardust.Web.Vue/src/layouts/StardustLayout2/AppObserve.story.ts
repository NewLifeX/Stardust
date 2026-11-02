import AppObserve from './AppObserve.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

function ok(rows: unknown[]): Req {
  return async () => ({ data: rows });
}
function fail(): Req {
  return async () => {
    throw new Error('stub 500');
  };
}

const history = [
  { Id: 1, Client: '10.0.0.1@11', NodeName: 'node-a', Action: '登录', Success: true, Remark: '上线', CreateTime: '2026-11-02 09:00', TraceId: 'abc' },
  { Id: 2, Client: '10.0.0.2@22', NodeName: 'node-b', Action: '心跳', Success: false, Remark: '超时', CreateTime: '2026-11-02 09:01', TraceId: '' },
];

const meter = [
  { Id: 1, ClientId: '10.0.0.1@11', Source: 'agent', Memory: 1048576, CpuUsage: 12, Threads: 20, Time: '2026-11-02 09:00' },
  { Id: 2, ClientId: '10.0.0.1@11', Source: 'agent', Memory: 2097152, CpuUsage: 40, Threads: 24, Time: '2026-11-02 09:05' },
];

export const stories = [
  { id: 'AppObserve/History', component: AppObserve, props: { appId: 32, kind: 'appHistory', deps: { request: ok(history) } } },
  { id: 'AppObserve/Meter', component: AppObserve, props: { appId: 32, kind: 'meter', deps: { request: ok(meter) } } },
  { id: 'AppObserve/Empty', component: AppObserve, props: { appId: 32, kind: 'commands', deps: { request: ok([]) } } },
  { id: 'AppObserve/Failed', component: AppObserve, props: { appId: 32, kind: 'services', deps: { request: fail() } } },
];
