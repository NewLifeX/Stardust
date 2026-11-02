import NodesPage from './NodesPage.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

// 五节点三态混合：2 正常(node-a/d) · 2 高负载(node-b/e，cpuRate 92/88 超阈) · 1 离线(node-c，lastActive 3 天前)。
// node-a 上 2 个在线应用、node-b 1 个、node-d 1 个（抽屉「应用进程」tab 可点开验证）。
const NODES = [
  { id: 1, name: 'node-a', code: 'NA', iP: '10.0.0.1', projectName: '北京机房', oS: 'CentOS', oSVersion: '7.9', version: 'v3.9.2', framework: 'NewLife', runtime: '.NET 8', machineName: 'host-a', lastActive: new Date(Date.now() - 5000).toISOString(), alarmCpuRate: 85 },
  { id: 2, name: 'node-b', code: 'NB', iP: '10.0.0.2', projectName: '上海机房', oS: 'Ubuntu', oSVersion: '22.04', version: 'v3.9.0', framework: 'NewLife', runtime: '.NET 6', machineName: 'host-b', lastActive: new Date(Date.now() - 3000).toISOString(), alarmCpuRate: 80 },
  { id: 3, name: 'node-c', code: 'NC', iP: '10.0.0.3', projectName: '默认集群', oS: 'Debian', oSVersion: '11', version: 'v3.8.1', framework: 'NewLife', runtime: '.NET 6', machineName: 'host-c', lastActive: new Date(Date.now() - 86400_000 * 3).toISOString() },
  { id: 4, name: 'node-d', code: 'ND', iP: '10.0.0.4', projectName: '北京机房', oS: 'CentOS', oSVersion: '7.9', version: 'v3.9.2', framework: 'NewLife', runtime: '.NET 8', machineName: 'host-d', lastActive: new Date(Date.now() - 8000).toISOString(), alarmCpuRate: 85 },
  { id: 5, name: 'node-e', code: 'NE', iP: '10.0.0.5', projectName: '上海机房', oS: 'Rocky', oSVersion: '9', version: 'v3.9.0', framework: 'NewLife', runtime: '.NET 8', machineName: 'host-e', lastActive: new Date(Date.now() - 2000).toISOString(), alarmCpuRate: 85 },
];

const ONLINE = [
  { nodeId: 1, name: 'node-a', cpuRate: 42, memory: 8000, memoryUsed: 4100, pingCount: 120 },
  { nodeId: 2, name: 'node-b', cpuRate: 92, memory: 16000, memoryUsed: 12500, pingCount: 88 },
  { nodeId: 4, name: 'node-d', cpuRate: 30, memory: 8000, memoryUsed: 3000, pingCount: 200 },
  { nodeId: 5, name: 'node-e', cpuRate: 88, memory: 16000, memoryUsed: 9000, pingCount: 76 },
];

const APPS = [
  { id: 101, nodeId: 1, name: 'order-api', version: '1.2.0', processName: 'order-api', processId: 3210, iP: '10.0.0.1' },
  { id: 102, nodeId: 1, name: 'pay-worker', version: '0.9.3', processName: 'pay-worker', processId: 3350, iP: '10.0.0.1' },
  { id: 103, nodeId: 2, name: 'gateway', version: '2.0.1', processName: 'gateway', processId: 4100, iP: '10.0.0.2' },
  { id: 104, nodeId: 4, name: 'report', version: '1.0.0', processName: 'report', processId: 5200, iP: '10.0.0.4' },
];

function stubReq(): Req {
  return async (opts) => {
    if (opts.url === '/Nodes/Node') return { data: NODES };
    if (opts.url === '/Nodes/NodeOnline') return { data: ONLINE };
    if (opts.url === '/Registry/AppOnline') {
      const nid = opts.params?.nodeId;
      if (nid !== undefined && nid !== null) return { data: APPS.filter((a) => a.nodeId === nid) };
      return { data: APPS };
    }
    if (opts.url === '/Nodes/NodeStat') return { data: [] };
    return { data: [] };
  };
}

function stubReqEmpty(): Req {
  return async (opts) => {
    if (opts.url === '/Registry/AppOnline') return { data: [] };
    return { data: [] };
  };
}

function stubReqFail(): Req {
  return async () => {
    throw new Error('stub 500');
  };
}

export const stories = [
  // 五节点三态混合：星图光点 + 过滤条计数(全部5/正常2/高负载2/离线1) + 8列表格
  { id: 'NodesPage/Mixed', component: NodesPage, props: { deps: { request: stubReq() } } },

  // 空态：星图「还没有节点」、表格「没有匹配的节点」、chip 计数全 0
  { id: 'NodesPage/Empty', component: NodesPage, props: { deps: { request: stubReqEmpty() } } },

  // 失败态：可见错误条 + 重试按钮（不当空态）
  { id: 'NodesPage/Failed', component: NodesPage, props: { deps: { request: stubReqFail() } } },
];
