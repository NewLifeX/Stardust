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

// 在线快照（CARD-NODE-11）：id=NodeOnline 会话主键（批量 keys 用它，非 Node.Id），
// nodeId=节点主键；nodeId=1 两条历史（id=9 旧、id=11 新）覆盖「取 updateTime 最新」映射。
const ONLINE = [
  { id: 9, nodeID: 1, name: 'node-a', updateTime: '2026-11-08 10:00:00', cpuRate: 40, memory: 8000, memoryUsed: 4000, pingCount: 120 },
  { id: 11, nodeID: 1, name: 'node-a', updateTime: '2026-11-09 03:00:00', cpuRate: 42, memory: 8000, memoryUsed: 4100, pingCount: 120 },
  { id: 12, nodeID: 2, name: 'node-b', updateTime: '2026-11-09 02:30:00', cpuRate: 92, memory: 16000, memoryUsed: 12500, pingCount: 88 },
  { id: 14, nodeID: 4, name: 'node-d', updateTime: '2026-11-09 03:10:00', cpuRate: 30, memory: 8000, memoryUsed: 3000, pingCount: 200 },
  { id: 15, nodeID: 5, name: 'node-e', updateTime: '2026-11-09 03:12:00', cpuRate: 88, memory: 16000, memoryUsed: 9000, pingCount: 76 },
];

const APPS = [
  { id: 101, nodeId: 1, name: 'order-api', version: '1.2.0', processName: 'order-api', processId: 3210, iP: '10.0.0.1' },
  { id: 102, nodeId: 1, name: 'pay-worker', version: '0.9.3', processName: 'pay-worker', processId: 3350, iP: '10.0.0.1' },
  { id: 103, nodeId: 2, name: 'gateway', version: '2.0.1', processName: 'gateway', processId: 4100, iP: '10.0.0.2' },
  { id: 104, nodeId: 4, name: 'report', version: '1.0.0', processName: 'report', processId: 5200, iP: '10.0.0.4' },
];

// 节点命令（NODE-06 B1）：载荷形态取自 6681 实测（camelCase 扁平数组，data 即数组）。
// node-a 2 条（一条有 traceId 一条无，覆盖追踪列「—」）、node-b 1 条、node-d 0 条（空态）。
const CMDS = [
  { id: 2, nodeID: 1, command: 'deploy/start', argument: '{"name":"order-api"}', startTime: '2026-11-09 01:00:00', expire: 3600, status: 0, times: 0, result: null, traceId: 'trc-abc-123', createUser: '星尘管理平台 API', createUserID: 1, createTime: '2026-11-09 01:00:05', createIP: '127.0.0.1', updateUserID: 1, updateTime: '2026-11-09 01:00:05', updateIP: '127.0.0.1' },
  { id: 1, nodeID: 1, command: 'check/upgrade', argument: '', startTime: '2026-11-08 22:00:00', expire: 3600, status: 1, times: 1, result: '已是最新版本', traceId: '', createUser: '星尘管理平台 API', createUserID: 1, createTime: '2026-11-08 22:00:05', createIP: '127.0.0.1', updateUserID: 1, updateTime: '2026-11-08 22:01:00', updateIP: '127.0.0.1' },
  { id: 3, nodeID: 2, command: 'deploy/stop', argument: '{"name":"gateway"}', startTime: '2026-11-09 00:30:00', expire: 3600, status: 2, times: 0, result: null, traceId: 'trc-def-456', createUser: 'Admin', createUserID: 2, createTime: '2026-11-09 00:30:05', createIP: '127.0.0.1', updateUserID: 2, updateTime: '2026-11-09 00:30:05', updateIP: '127.0.0.1' },
];

// 24h 性能采样（CARD-NODE-07）：NodeDataController 序列化形态（camelCase，百分比字段已除以 100）。
// 乱序两行：温度全空（显隐断言=不出现在图内）；电量仅一行有值（null 断点）。
const NODE_DATA = [
  { id: 1, nodeID: 1, localTime: '2026-11-09 12:00:00', cpuRate: 0.42, systemLoad: 1.2, battery: null, uplinkSpeed: 1024, temperature: null },
  { id: 2, nodeID: 1, localTime: '2026-11-09 11:00:00', cpuRate: 0.5, systemLoad: 1.0, battery: 0.85, uplinkSpeed: 2048, temperature: null },
];

// 节点历史（CARD-NODE-09）：字段 camelCase（NodeHistory 实体：id/nodeId/nodeName/action/success(bool)/
// remark/traceId/createTime/createIP）。node-a 2 条（成功+失败各一，覆盖「是/否」双色与追踪列「—」）、
// node-b 1 条、node-d 0 条（空态）。
const HIST = [
  { id: 21, nodeId: 1, nodeName: 'node-a', action: '登录', success: true, remark: '节点上线', traceId: 'trc-his-001', createTime: '2026-11-10T21:00:05', createIP: '10.0.0.1' },
  { id: 20, nodeId: 1, nodeName: 'node-a', action: '升级', success: false, remark: '升级失败：下载超时', traceId: '', createTime: '2026-11-10T20:30:00', createIP: '10.0.0.1' },
  { id: 19, nodeId: 2, nodeName: 'node-b', action: '登出', success: true, remark: '节点下线', traceId: 'trc-his-002', createTime: '2026-11-10T19:00:00', createIP: '10.0.0.2' },
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
    if (opts.url === '/Nodes/NodeCommand') {
      const nid = opts.params?.nodeId;
      return { data: CMDS.filter((c) => c.nodeID === nid) };
    }
    if (opts.url === '/Nodes/NodeHistory') {
      // CARD-NODE-09：捕获 success 过滤参数（'true'/'false' 字符串，CT 断言用）并按值过滤
      captureBatch(String(opts.url), (opts.params ?? {}) as Record<string, unknown>);
      const nid = opts.params?.nodeId;
      let list = HIST.filter((h) => h.nodeId === nid);
      const sc = opts.params?.success;
      if (sc === 'true') list = list.filter((h) => h.success === true);
      if (sc === 'false') list = list.filter((h) => h.success === false);
      return { code: 0, data: list };
    }
    if (opts.url === '/Nodes/NodeData') {
      captureBatch(String(opts.url), (opts.params ?? {}) as Record<string, unknown>);
      return { data: NODE_DATA };
    }
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

// 列表正常但命令接口失败：节点行可点开抽屉，命令页签走错误+重试态（NODE-06 B1）
function stubReqCmdFail(): Req {
  return async (opts) => {
    if (opts.url === '/Nodes/NodeCommand') throw new Error('cmd stub 500');
    if (opts.url === '/Nodes/Node') return { data: NODES };
    if (opts.url === '/Nodes/NodeOnline') return { data: ONLINE };
    if (opts.url === '/Registry/AppOnline') {
      const nid = opts.params?.nodeId;
      if (nid !== undefined && nid !== null) return { data: APPS.filter((a) => a.nodeId === nid) };
      return { data: APPS };
    }
    return { data: [] };
  };
}

// ---------- 批量运维（CARD-NODE-11） ----------

// 批量动作请求捕获（浏览器侧）：CT 不能 Node 侧 import story（会连带转译 .vue 炸 esbuild），
// 所以 stub 里把请求挂 window.__batchCalls，spec 用 page.evaluate 读取/清空。
// keys=NodeOnline 会话 Id（勾选 nodeId=1 → 在线快照 updateTime 最新 id=11）。
// 后端契约样例（NodeOnlineController.cs :110/:289/:294 逐字形态）
const BATCH_SUMMARY =
  '操作成功！下发1个，响应1个，超时0个\nnode-a: 已是最新版本';

function captureBatch(url: string, params: Record<string, unknown>): void {
  const w = window as unknown as { __batchCalls?: Array<{ url: string; params: Record<string, unknown> }> };
  w.__batchCalls = w.__batchCalls ?? [];
  w.__batchCalls.push({ url, params });
}

function stubReqBatch(): Req {
  return async (opts) => {
    // 框架管理（CARD-NODE-08）：列表=IDictionary 键值对对象（Biz :138/:271，auto=自动首项）；安装/卸载捕获并契约汇总
    if (opts.url === '/Nodes/NodeFramework/GetVersionList') {
      return { code: 0, message: null, data: { '8.0.11': '8.0.11', '9.0.0': '9.0.0' } };
    }
    if (opts.url === '/Nodes/NodeFramework/GetKindList') {
      return { code: 0, message: null, data: { auto: '自动', runtime: '运行时' } };
    }
    if (opts.url.startsWith('/Nodes/NodeFramework/InstallFramework') || opts.url.startsWith('/Nodes/NodeFramework/UninstallFramework')) {
      captureBatch(String(opts.url), (opts.params ?? {}) as Record<string, unknown>);
      return { code: 0, message: BATCH_SUMMARY, data: null };
    }
    if (opts.url.startsWith('/Nodes/NodeOnline/')) {
      captureBatch(String(opts.url), (opts.params ?? {}) as Record<string, unknown>);
      return { code: 0, message: BATCH_SUMMARY, data: null };
    }
    if (opts.url === '/Nodes/Node') return { data: NODES };
    if (opts.url === '/Nodes/NodeOnline') return { data: ONLINE };
    if (opts.url === '/Registry/AppOnline') return { data: [] };
    if (opts.url === '/Nodes/NodeStat') return { data: [] };
    if (opts.url === '/Nodes/NodeCommand') return { data: [] };
    return { data: [] };
  };
}

function stubReqBatchFail(): Req {
  return async (opts) => {
    // 框架失败态：全跳过清单（NodeFrameworkController.cs :100 「没有可推送的节点！」逐字形态）
    if (opts.url.startsWith('/Nodes/NodeFramework/InstallFramework') || opts.url.startsWith('/Nodes/NodeFramework/UninstallFramework')) {
      return { code: 400, message: '没有可推送的节点！\nnode-c：没有匹配的安装包', data: null };
    }
    if (opts.url === '/Nodes/NodeFramework/GetVersionList') {
      return { code: 0, message: null, data: { '8.0.11': '8.0.11', '9.0.0': '9.0.0' } };
    }
    if (opts.url === '/Nodes/NodeFramework/GetKindList') {
      return { code: 0, message: null, data: { auto: '自动', runtime: '运行时' } };
    }
    if (opts.url.startsWith('/Nodes/NodeOnline/')) {
      return { code: 400, message: '权限不足', data: null };
    }
    if (opts.url === '/Nodes/Node') return { data: NODES };
    if (opts.url === '/Nodes/NodeOnline') return { data: ONLINE };
    if (opts.url === '/Registry/AppOnline') return { data: [] };
    if (opts.url === '/Nodes/NodeStat') return { data: [] };
    if (opts.url === '/Nodes/NodeCommand') return { data: [] };
    return { data: [] };
  };
}

// 性能失败态（CARD-NODE-07）：仅 NodeData 抛错，列表/抽屉/命令页签正常
function stubReqPerfFail(): Req {
  return async (opts) => {
    if (opts.url === '/Nodes/NodeData') throw new Error('stub 500');
    if (opts.url === '/Nodes/Node') return { data: NODES };
    if (opts.url === '/Nodes/NodeOnline') return { data: ONLINE };
    if (opts.url === '/Registry/AppOnline') return { data: [] };
    if (opts.url === '/Nodes/NodeStat') return { data: [] };
    if (opts.url === '/Nodes/NodeCommand') return { data: [] };
    return { data: [] };
  };
}

export const stories = [
  // 五节点三态混合：星图光点 + 过滤条计数(全部5/正常2/高负载2/离线1) + 8列表格
  { id: 'NodesPage/Mixed', component: NodesPage, props: { deps: { request: stubReq() } } },

  // 空态：星图「还没有节点」、表格「没有匹配的节点」、chip 计数全 0
  { id: 'NodesPage/Empty', component: NodesPage, props: { deps: { request: stubReqEmpty() } } },

  // 失败态：可见错误条 + 重试按钮（不当空态）
  { id: 'NodesPage/Failed', component: NodesPage, props: { deps: { request: stubReqFail() } } },

  // 命令失败态：列表正常可开抽屉，命令页签显示错误 + 重试
  { id: 'NodesPage/CmdFailed', component: NodesPage, props: { deps: { request: stubReqCmdFail() } } },

  // 批量运维（CARD-NODE-11）：勾选后五动作可点，动作请求被 batchCalls 捕获，成功返回契约汇总
  { id: 'NodesPage/Batch', component: NodesPage, props: { deps: { request: stubReqBatch() } } },

  // 批量失败态：后端 code 400（权限不足）→ 失败条可见
  { id: 'NodesPage/BatchFail', component: NodesPage, props: { deps: { request: stubReqBatchFail() } } },

  // 性能失败态（CARD-NODE-07）：仅 NodeData 抛错 → 性能页签错误条 + 重试
  { id: 'NodesPage/PerfFail', component: NodesPage, props: { deps: { request: stubReqPerfFail() } } },
];
