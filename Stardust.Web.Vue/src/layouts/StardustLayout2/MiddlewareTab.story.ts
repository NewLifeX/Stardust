import MiddlewareTab from './MiddlewareTab.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** MySQL 数据桩：两条采样（雪花 Id 走 string），total=46 触发分页（46/20 → 3 页） */
const DATA_ROWS = [
  {
    Id: '1900000000000012001', MySqlId: 3, Name: '主库@10.0.0.8', Uptime: 2 * 86400 + 3 * 3600,
    Connections: 12, MaxConnections: 100, Qps: 321, Tps: 45,
    BytesReceived: 1572864, BytesSent: 15728640, InnodbBufferPoolSize: 128, InnodbBufferPoolPages: 65536,
    InnodbBufferPoolHitRate: 98.53, SlowQueries: 7, Threads: 15, ThreadsRunning: 2,
    CreateTime: '2026-11-09 10:30:00',
  },
  {
    Id: '1900000000000012000', MySqlId: 5, Name: '从库@10.0.0.9', Uptime: 45 * 60,
    Connections: 4, MaxConnections: 80, Qps: 96, Tps: 12,
    BytesReceived: 356, BytesSent: 1024, InnodbBufferPoolSize: 0, InnodbBufferPoolPages: 0,
    InnodbBufferPoolHitRate: 0, SlowQueries: 0, Threads: 6, ThreadsRunning: 1,
    CreateTime: '2026-11-09 10:29:40',
  },
];
const NODES = [{ Id: 3, Name: '主库节点' }, { Id: 5, Name: '从库节点' }];

/** MW-02 节点列表桩：两条节点（含 Pascal 混排），total=2 不分页 */
const NODE_ROWS = [
  {
    Id: 3, ProjectId: 1, ProjectName: '银河项目', Name: '主库', Category: 'MySQL',
    Server: '10.0.0.8', Port: 3306, UserName: 'root', DatabaseName: 'stardust',
    Version: '8.0.36', Enable: true, UpdateTime: '2026-11-09 10:30:00',
  },
  {
    Id: 5, ProjectId: 0, ProjectName: '', Name: '备库', Category: 'MySQL',
    Server: '10.0.0.9', Port: 3307, UserName: 'root', DatabaseName: '',
    Version: '', Enable: false, UpdateTime: '',
  },
];
/** Detail 桩（Id=3 主库）：含全部可编辑字段，供抽屉回填 */
const NODE_DETAIL = {
  Id: 3, ProjectId: 1, Name: '主库', Category: 'MySQL', Server: '10.0.0.8', Port: 3306,
  UserName: 'root', Password: 'seed-pwd', DatabaseName: 'stardust', Version: '8.0.36',
  Enable: true, WebHook: 'https://hooks.example.com/x', AlarmConnections: 200, AlarmQPS: 300,
  AlarmSlowQuery: 10, Remark: '主库节点',
};

/** MW-03 Redis 节点候选桩（NodeSearch Lov 形态：{Id,Name,Server,Category}） */
const REDIS_NODES = [{ Id: 7, Name: '主缓存', Server: '10.0.0.9:6379', Category: 'Cache' }];
/** MW-03 Redis 数据桩（无节点=服务端分页形态）：3 行 */
const REDIS_DATA_ROWS = [
  { Id: '1900000000000030001', RedisId: 7, Name: 'cache@10.0.0.9', Speed: 1200, InputKbps: 2048, OutputKbps: 4096, ConnectedClients: 8, UsedMemory: 512, Keys: 9000, Commands: 123456, CreateTime: '2026-11-09 10:30:00' },
  { Id: '1900000000000030000', RedisId: 7, Name: 'cache@10.0.0.9', Speed: 1150, InputKbps: 1980, OutputKbps: 3950, ConnectedClients: 7, UsedMemory: 510, Keys: 8980, Commands: 123000, CreateTime: '2026-11-09 10:29:00' },
  { Id: '1900000000000029999', RedisId: 9, Name: 'queue@10.0.0.10', Speed: 300, InputKbps: 120, OutputKbps: 260, ConnectedClients: 3, UsedMemory: 96, Keys: 2100, Commands: 45000, CreateTime: '2026-11-09 10:28:00' },
];
/** MW-04 Redis 队列桩：3 行——行1 有 TraceId（追踪按钮可用），行2 无 TraceId（禁用态），行3 零值兜底；total=46 触发分页 */
const REDIS_QUEUE_ROWS = [
  {
    Id: 101, RedisId: 7, Name: 'order-queue', Category: '订单', Db: 0, Topic: 'order.topic', Type: 'PubSub',
    Groups: 'order-group', Consumers: 4, Total: 123456, Messages: 320, MaxMessages: 5000,
    Enable: true, FirstConsumer: '2026-11-09 09:00:00', LastActive: '2026-11-09 10:29:00',
    TraceId: '1900000000000050001', UpdateTime: '2026-11-09 10:29:00',
  },
  {
    Id: 102, RedisId: 7, Name: 'alarm-queue', Category: '告警', Db: 1, Topic: '', Type: 'Stream',
    Groups: '', Consumers: 0, Total: 0, Messages: 0, MaxMessages: 0,
    Enable: false, FirstConsumer: '', LastActive: '',
    TraceId: '', UpdateTime: '2026-11-09 08:12:00',
  },
  {
    Id: 103, RedisId: 9, Name: 'log-queue', Category: '日志', Db: 2, Topic: 'log.topic', Type: 'PubSub',
    Groups: 'log-group', Consumers: 2, Total: 88, Messages: 6, MaxMessages: 100,
    Enable: true, FirstConsumer: '2026-11-09 07:40:00', LastActive: '2026-11-09 10:20:00',
    TraceId: '', UpdateTime: '2026-11-09 10:20:00',
  },
];
/** MW-05 Redis 节点列表桩：3 行（Pascal 混排；行2 空值兜底）；total=3 不分页 */
const REDIS_NODE_ROWS = [
  {
    Id: 7, ProjectId: 1, ProjectName: '银河项目', Name: '主缓存', Category: 'Cache',
    Server: '10.0.0.9:6379', UserName: 'default', Version: '7.2.4', Mode: 'standalone',
    Role: 'master', Enable: true, ScanQueue: true, UpdateTime: '2026-11-09 10:30:00',
  },
  {
    Id: 9, ProjectId: 0, ProjectName: '', Name: '队列库', Category: 'Queue',
    Server: '10.0.0.10:6379', UserName: '', Version: '', Mode: '', Role: '',
    Enable: false, ScanQueue: false, UpdateTime: '2026-11-09 08:12:00',
  },
  {
    Id: 11, ProjectId: 0, ProjectName: '', Name: '兜底节点', Category: '',
    Server: '127.0.0.1:6379', UserName: '', Version: '', Mode: '', Role: '',
    Enable: true, UpdateTime: '',
  },
];
/** MW-05 节点 Detail 桩（Id=7）：全可编辑字段（含 Password 回显），供抽屉回填 */
const REDIS_NODE_DETAIL = {
  Id: 7, ProjectId: 1, Name: '主缓存', Category: 'Cache', Server: '10.0.0.9:6379',
  UserName: 'default', Password: 'seed-redis', Version: '7.2.4', Mode: 'standalone',
  Role: 'master', Enable: true, ScanQueue: true, MaxMemory: 512, MemoryPolicy: 'noeviction',
  MemoryAllocator: 'jemalloc', AlarmMemoryRate: 90, AlarmConnections: 200, AlarmSpeed: 3000,
  AlarmInputKbps: 2048, AlarmOutputKbps: 4096, WebHook: 'https://hooks.example.com/redis',
  Remark: '主缓存节点',
};

/** MW-03 选中节点桩：45 条分钟采样（一天最多 1440 的缩样；45=3 页×20+尾页 5，供本地切片断言） */
function node7Rows(): Record<string, unknown>[] {
  return Array.from({ length: 45 }, (_, i) => ({
    Id: String(1900000000000020000 + i), RedisId: 7, Name: `cache@10.0.0.9 #${i}`,
    Speed: 1000 + i, InputKbps: 100 + i, OutputKbps: 200 + i * 2,
    ConnectedClients: 8 + (i % 5), UsedMemory: 512 + (i % 7), Keys: 9000 + i,
    Commands: 100000 + i * 3, CreateTime: `2026-11-09 10:${String(i % 60).padStart(2, '0')}:00`,
  }));
}

/** 正常桩：数据 + 候选都有。/MySql/MySqlNode 双消费者：候选（pageSize=0）回 NODES，节点列表回 NODE_ROWS；
 *  节点写请求（PUT 保存 / POST 刷新）录制到 window.__mwNodeCalls 供 CT 断言 */
function ok(): Req {
  return async (cfg: { url?: string; params?: Record<string, unknown>; method?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/MySqlNode') && String(cfg?.method ?? 'GET').toUpperCase() !== 'GET') {
      const w = window as unknown as { __mwNodeCalls?: string[] };
      w.__mwNodeCalls = w.__mwNodeCalls ?? [];
      w.__mwNodeCalls.push(`${String(cfg?.method).toUpperCase()} ${url}`);
    }
    if (url.includes('MySqlData')) return { data: DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/MySqlNode')) {
      if (url.endsWith('/Refresh')) return { code: 0, message: '刷新[主库]成功！', data: null };
      if (String(cfg?.method ?? 'GET').toUpperCase() === 'PUT') return { code: 0, message: '保存成功！', data: null };
      if (url.endsWith('/MySqlNode/3')) return { data: NODE_DETAIL };
      if (cfg?.params?.id !== undefined) return { data: [NODE_ROWS[1]] };
      if (cfg?.params?.pageSize === 0) return { data: NODES };
      return { data: NODE_ROWS, page: { totalCount: NODE_ROWS.length } };
    }
    // MW-03：NodeSearch 候选；RedisData 选中节点（redisId>0）→ 全量 45 行本地切片，无节点 → 服务端分页 3 行
    if (url.includes('/RedisNode/NodeSearch')) return { data: REDIS_NODES };
    // MW-05：RedisNode 列表/Detail/写请求（写请求录制到 __mwRedisNodeCalls 供 CT 断言）
    if (url.includes('/Redis/RedisNode')) {
      const w = window as unknown as { __mwRedisNodeCalls?: string[] };
      const m = String(cfg?.method ?? 'GET').toUpperCase();
      if (m !== 'GET' || url.endsWith('/Refresh')) {
        w.__mwRedisNodeCalls = w.__mwRedisNodeCalls ?? [];
        w.__mwRedisNodeCalls.push(`${m} ${url}`);
      }
      if (url.endsWith('/Refresh')) return { code: 0, message: '刷新[主缓存]成功！', data: null };
      if (m === 'PUT') return { code: 0, message: '保存成功！', data: null };
      if (m === 'POST') return { code: 0, message: '新增成功！', data: null };
      if (m === 'DELETE') return { code: 0, message: '删除成功！', data: null };
      if (url.endsWith('/RedisNode/7')) return { data: REDIS_NODE_DETAIL };
      return { data: REDIS_NODE_ROWS, page: { totalCount: REDIS_NODE_ROWS.length } };
    }
    if (url.includes('/Redis/RedisData')) {
      if (Number(cfg?.params?.redisId) > 0) return { data: node7Rows(), page: { totalCount: 45 } };
      return { data: REDIS_DATA_ROWS, page: { totalCount: 46 } };
    }
    // MW-04：RedisMessageQueue 标准服务端分页（pageIndex+pageSize 恒发），3 行 + total=46 触发分页
    if (url.includes('/Redis/RedisMessageQueue')) return { data: REDIS_QUEUE_ROWS, page: { totalCount: 46 } };
    return { data: [] };
  };
}
/** 空态桩：本地库常态（无采样、无候选/节点也不炸） */
function empty(): Req {
  return async (cfg: { url?: string }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('MySqlData')) return { data: [], page: { totalCount: 0 } };
    if (url.includes('/Redis/RedisData')) return { data: [], page: { totalCount: 0 } };
    return { data: [], page: { totalCount: 0 } };
  };
}
/** 加载态桩：永不 resolve，组件停留在 loading */
function pending(): Req {
  return async () => new Promise(() => {});
}
/** 失败桩：数据接口 throw——候选照常（候选/节点列表按 pageSize=0 分流） */
function failData(): Req {
  return async (cfg: { url?: string; params?: Record<string, unknown> }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('MySqlData')) throw new Error('stub 500');
    if (url.includes('/Redis/RedisData')) throw new Error('Redis stub 500');
    if (url.includes('/Redis/RedisMessageQueue')) throw new Error('Redis 队列 stub 500');
    if (url.includes('/RedisNode/NodeSearch')) return { data: REDIS_NODES };
    if (url.includes('/MySqlNode')) {
      if (cfg?.params?.pageSize === 0) return { data: NODES };
      return { data: NODE_ROWS, page: { totalCount: NODE_ROWS.length } };
    }
    return { data: [] };
  };
}
/** 候选失败桩（Mixed）：MySQL/Redis 候选接口 throw，节点列表/数据正常——独立区块降级不拖垮 */
function failNodes(): Req {
  return async (cfg: { url?: string; params?: Record<string, unknown> }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('MySqlData')) return { data: DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisData')) return { data: REDIS_DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisMessageQueue')) return { data: REDIS_QUEUE_ROWS, page: { totalCount: 46 } };
    if (url.includes('/RedisNode/NodeSearch')) throw new Error('Redis 候选服务离线');
    if (url.includes('/MySqlNode')) {
      if (cfg?.params?.pageSize === 0) throw new Error('节点服务离线');
      return { data: NODE_ROWS, page: { totalCount: NODE_ROWS.length } };
    }
    return { data: [] };
  };
}
/** 节点列表失败桩（MW-02）：候选正常（pageSize=0 回 NODES）、节点卡 throw——独立区块互不拖垮 */
function failNodeList(): Req {
  return async (cfg: { url?: string; params?: Record<string, unknown> }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('MySqlData')) return { data: DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisData')) return { data: REDIS_DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisMessageQueue')) return { data: REDIS_QUEUE_ROWS, page: { totalCount: 46 } };
    if (url.includes('/RedisNode/NodeSearch')) return { data: REDIS_NODES };
    if (url.includes('/MySqlNode')) {
      if (cfg?.params?.pageSize === 0) return { data: NODES };
      throw new Error('节点查询失败');
    }
    return { data: [] };
  };
}

/** RedisNode 节点卡失败桩（MW-05）：其余四块正常——RedisNode 卡独立降级不拖垮 */
function failRedisNodeList(): Req {
  return async (cfg: { url?: string; params?: Record<string, unknown> }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('MySqlData')) return { data: DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisData')) return { data: REDIS_DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisMessageQueue')) return { data: REDIS_QUEUE_ROWS, page: { totalCount: 46 } };
    if (url.includes('/RedisNode/NodeSearch')) return { data: REDIS_NODES };
    if (url.includes('/Redis/RedisNode')) throw new Error('Redis 节点查询失败');
    if (url.includes('/MySqlNode')) {
      if (cfg?.params?.pageSize === 0) return { data: NODES };
      return { data: NODE_ROWS, page: { totalCount: NODE_ROWS.length } };
    }
    return { data: [] };
  };
}

/** Redis 队列卡空态隔离桩（MW-04 定稿裁定 4 · C05 空态三态变体）：仅队列列表空（total=0），
 *  其余区块照常——隔离验证队列区块空态独立渲染（全页 Empty/Loading/Failed 三态文案用例见 ct.spec） */
function redisQueueEmpty(): Req {
  return async (cfg: { url?: string; params?: Record<string, unknown> }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/Redis/RedisMessageQueue')) return { data: [], page: { totalCount: 0 } };
    if (url.includes('MySqlData')) return { data: DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisData')) return { data: REDIS_DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/RedisNode/NodeSearch')) return { data: REDIS_NODES };
    if (url.includes('/Redis/RedisNode')) return { data: REDIS_NODE_ROWS, page: { totalCount: REDIS_NODE_ROWS.length } };
    if (url.includes('/MySqlNode')) {
      if (cfg?.params?.pageSize === 0) return { data: NODES };
      return { data: NODE_ROWS, page: { totalCount: NODE_ROWS.length } };
    }
    return { data: [] };
  };
}

/** Redis 队列卡追踪列禁用态隔离桩（MW-04 定稿裁定 4 · 禁用态变体）：队列 3 行全无 TraceId，
 *  其余区块照常——隔离验证「无 TraceId 按钮 disabled+原因在视线内（§11）」整列禁用渲染 */
function redisQueueNoTrace(): Req {
  return async (cfg: { url?: string; params?: Record<string, unknown> }) => {
    const url = String(cfg?.url ?? '');
    if (url.includes('/Redis/RedisMessageQueue')) {
      return {
        data: REDIS_QUEUE_ROWS.map((r) => ({ ...r, TraceId: '' })),
        page: { totalCount: REDIS_QUEUE_ROWS.length },
      };
    }
    if (url.includes('MySqlData')) return { data: DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/Redis/RedisData')) return { data: REDIS_DATA_ROWS, page: { totalCount: 46 } };
    if (url.includes('/RedisNode/NodeSearch')) return { data: REDIS_NODES };
    if (url.includes('/Redis/RedisNode')) return { data: REDIS_NODE_ROWS, page: { totalCount: REDIS_NODE_ROWS.length } };
    if (url.includes('/MySqlNode')) {
      if (cfg?.params?.pageSize === 0) return { data: NODES };
      return { data: NODE_ROWS, page: { totalCount: NODE_ROWS.length } };
    }
    return { data: [] };
  };
}

export const stories = [
  { id: 'MiddlewareTab/WithData', component: MiddlewareTab, props: { deps: { request: ok() } } },
  { id: 'MiddlewareTab/Empty', component: MiddlewareTab, props: { deps: { request: empty() } } },
  { id: 'MiddlewareTab/Loading', component: MiddlewareTab, props: { deps: { request: pending() } } },
  { id: 'MiddlewareTab/Failed', component: MiddlewareTab, props: { deps: { request: failData() } } },
  { id: 'MiddlewareTab/NodeFailed', component: MiddlewareTab, props: { deps: { request: failNodes() } } },
  { id: 'MiddlewareTab/NodeListFailed', component: MiddlewareTab, props: { deps: { request: failNodeList() } } },
  { id: 'MiddlewareTab/RedisNodeListFailed', component: MiddlewareTab, props: { deps: { request: failRedisNodeList() } } },
  { id: 'MiddlewareTab/RedisQueueEmpty', component: MiddlewareTab, props: { deps: { request: redisQueueEmpty() } } },
  { id: 'MiddlewareTab/RedisQueueTraceDisabled', component: MiddlewareTab, props: { deps: { request: redisQueueNoTrace() } } },
  // J-MW-02 深链：/mesh?tab=middleware&id=5 → 节点卡 Id 单条直达（桩按 params.id 回备库一行）
  { id: 'MiddlewareTab/NodeDeepLink', component: MiddlewareTab, props: { deps: { request: ok() }, initialNodeFilter: { id: 5 } } },
];
