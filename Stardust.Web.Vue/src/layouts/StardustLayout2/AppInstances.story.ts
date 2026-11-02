import AppInstances from './AppInstances.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { InstanceRow } from './appInstances.logic.ts';

// F04：实例列表 + Snapshot 快照两枪。快照按 URL 路由：
//  - /Registry/AppOnline → 实例列表（PascalCase）
//  - /Registry/AppOnline/Snapshot → 快照（camelCase；id 13 无性能数据 → CPU/内存「待接入」，id 12 offline）
const LIST = {
  data: [
    { Id: 11, NodeId: 7, NodeName: 'node-a', IP: '10.0.0.1', Client: '10.0.0.1@1234', Version: '1.2.0', LongLink: true },
    { Id: 12, NodeId: 8, NodeName: 'node-b', IP: '10.0.0.2', Client: '10.0.0.2@5678', Version: '1.2.0', LongLink: true },
    { Id: 13, NodeId: 9, NodeName: 'node-c', IP: '10.0.0.3', Client: '10.0.0.3@9012', Version: '1.1.0', LongLink: false },
  ],
};

// F04-1：Snapshot 的 memory/availableMemory 单位是 MB（189→189MB、5119≈5GB），merge 层转字节
const SNAPSHOT = {
  code: 0,
  data: [
    { id: 11, clientId: '10.0.0.1@1234', nodeId: 7, name: 'Star.Web', ip: '10.0.0.1', online: true, cpu: 12.34, memory: 150, availableMemory: 8192, threads: 32, connections: 5, updateTime: '2026-11-03T10:00:00' },
    { id: 12, clientId: '10.0.0.2@5678', nodeId: 8, name: 'Star.Web', ip: '10.0.0.2', online: false, cpu: 88.5, memory: 500, availableMemory: 1024, threads: 64, connections: 2, updateTime: '2026-11-03T09:59:00' },
    { id: 13, clientId: '10.0.0.3@9012', nodeId: 9, name: 'Star.Web', ip: '10.0.0.3', online: true, cpu: null, memory: null, availableMemory: null, threads: null, connections: null, updateTime: null },
  ],
};

function stubReq(opts: { throwOn?: string[]; snapshot?: unknown } = {}): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (opts.throwOn?.includes(url)) throw new Error('stub 500');
    if (url.includes('/Snapshot')) return (opts.snapshot ?? SNAPSHOT) as Awaited<ReturnType<Req>>;
    if (url.includes('/Execute') || url.includes('/FreeMemory')) {
      // 汇总文案按 keys 数量生成（后端 JsonRefresh：下发N个，响应N-1个，超时1个 + 各实例返回）
      const params = (opt.params ?? {}) as Record<string, unknown>;
      const keys = String(params.keys ?? '').split(',').filter(Boolean);
      const n = keys.length || 1;
      const lines = [`操作成功！下发${n}个，响应${Math.max(n - 1, 0)}个，超时${n > 0 ? 1 : 0}个`, 'node-a: pong'];
      if (n > 1) lines.push('node-b: (超时)');
      return { code: 0, message: lines.join('\n') } as Awaited<ReturnType<Req>>;
    }
    return LIST as Awaited<ReturnType<Req>>;
  };
}

function stubReqEmpty(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (url.includes('/Snapshot')) return { code: 0, data: [] } as Awaited<ReturnType<Req>>;
    return { data: [] } as Awaited<ReturnType<Req>>;
  };
}

function recorderReq(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (url.includes('/Snapshot')) return SNAPSHOT as Awaited<ReturnType<Req>>;
    return LIST as Awaited<ReturnType<Req>>;
  };
}

export const stories = [
  // F04：三个实例带快照——node-a 正常、node-b 高负载离线、node-c 无性能数据（CPU/内存待接入）
  { id: 'AppInstances/ThreeRows', component: AppInstances, props: { appId: 32, deps: { request: stubReq() } } },

  // 空态：整行「暂无在线实例」
  { id: 'AppInstances/Empty', component: AppInstances, props: { appId: 32, deps: { request: stubReqEmpty() } } },

  // 失败态：列表请求抛错，显示加载失败而不是空表
  { id: 'AppInstances/Failed', component: AppInstances, props: { appId: 32, deps: { request: stubReq({ throwOn: ['/Registry/AppOnline'] }) } } },

  // 事件录制：行点击发 select、重启/终端按钮发对应事件，进 window.__instEvents（CT 断言用）
  // gallery 只消费 props，事件监听器走 Vue 的 onXxx 约定（onSelect/onRestart/onTerminal）
  {
    id: 'AppInstances/EmitRecorder',
    component: AppInstances,
    props: {
      appId: 32,
      deps: { request: recorderReq() },
      onSelect: (row: InstanceRow) => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents?.push(['select', row.id]),
      onRestart: (row: InstanceRow) => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents?.push(['restart', row.id]),
      onTerminal: (row: InstanceRow) => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents?.push(['terminal', row.id]),
    },
  },
];
