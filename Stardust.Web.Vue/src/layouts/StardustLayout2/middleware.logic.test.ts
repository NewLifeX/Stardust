// middleware.logic 单元测试：MW-01 MySQL 数据参数拼装/行归一/格式化/取数错误归因/节点候选 + MW-02 节点列表/筛选/编辑/刷新 + MW-03 Redis 数据（C04 硬项：请求参数绝不携带 pageSize）。
// stub 范式同 gatewayNode.logic.test.ts；分页响应额外带 page.totalCount（fetchMcpAuditPage 同源形态）。
import { describe, expect, it } from 'vitest';
import {
  buildRedisDataChartOption,
  emptyMySqlDataFilter,
  emptyMySqlNodeFilter,
  emptyRedisDataFilter,
  emptyRedisNodeFilter,
  emptyRedisQueueFilter,
  fetchMySqlData,
  fetchMySqlNodeDetail,
  fetchMySqlNodeList,
  fetchMySqlNodes,
  fetchRedisData,
  fetchRedisNodeDetail,
  fetchRedisNodeList,
  fetchRedisNodes,
  fetchRedisQueues,
  createRedisNode,
  deleteRedisNode,
  formatBufferPool,
  formatBytes,
  formatUptime,
  mysqlDataFilterParams,
  mysqlDataTotalPages,
  mysqlNodeFilterParams,
  nodeDetailToRedisForm,
  nodeDetailToForm,
  nodeUpdatePayload,
  redisDataFilterParams,
  redisDataTotalPages,
  redisNodeFilterParams,
  redisNodeTotalPages,
  redisNodeUpdatePayload,
  redisQueueFilterParams,
  redisQueueTotalPages,
  refreshMySqlNode,
  refreshRedisNode,
  submitMySqlNode,
  submitRedisNode,
  toMySqlDataRows,
  toMySqlNodeOptions,
  toMySqlNodeRows,
  toRedisDataRows,
  toRedisNodeRows,
  toRedisQueueRows,
  validateMySqlNodeForm,
  validateRedisNodeForm,
} from './middleware.logic';

type Rec = Record<string, unknown>;
type Call = { method?: string; url?: string; params?: Rec; data?: unknown };

/** 简单请求桩：按 url 分发表；表项 = { data, message?, code?, page? }，返回 { code:0, message:'', ...表项 }（表项可覆盖 code/message 以模拟失败信封） */
function stubReq(
  map: Record<string, { data?: unknown; message?: string; code?: number; page?: { totalCount?: number; total?: number } }>,
  opts?: { throwOn?: string; calls?: Call[] },
) {
  return async (opt: { url?: string; method?: string; params?: Rec; data?: unknown }) => {
    opts?.calls?.push(opt as Call);
    const url = String(opt.url || '');
    if (opts?.throwOn && url.includes(opts.throwOn)) throw new Error('数据库拒绝');
    const hit = map[url] ?? { data: [] };
    return { code: 0, message: '', ...hit };
  };
}

describe('mysqlDataFilterParams', () => {
  it('默认条件下发分页、省略全部筛选（mysqlId 0 不下发=服务端不过滤语义）', () => {
    const p = mysqlDataFilterParams(emptyMySqlDataFilter());
    expect(p).toEqual({ pageIndex: 0, pageSize: 20 });
    expect(p).not.toHaveProperty('mysqlId');
    expect(p).not.toHaveProperty('Q');
    expect(p).not.toHaveProperty('dtStart');
    expect(p).not.toHaveProperty('dtEnd');
  });

  it('节点>0 下发 mysqlId；dtEnd 纯日期补 23:59:59；dtStart/Q trim 下发', () => {
    const p = mysqlDataFilterParams({ mysqlId: 3, dtStart: ' 2026-11-08 ', dtEnd: '2026-11-09', q: ' 主库 ', pageIndex: 1, pageSize: 20 });
    expect(p).toEqual({ pageIndex: 1, pageSize: 20, mysqlId: 3, dtStart: '2026-11-08', dtEnd: '2026-11-09 23:59:59', Q: '主库' });
  });

  it('dtEnd 带时间原样下发，不重复补时间', () => {
    const p = mysqlDataFilterParams({ ...emptyMySqlDataFilter(), dtEnd: '2026-11-09 12:00:00' });
    expect(p.dtEnd).toBe('2026-11-09 12:00:00');
  });

  it('mysqlId 0 绝不下发（0 是服务端等值过滤值，非「全部」）', () => {
    const p = mysqlDataFilterParams({ mysqlId: 0, dtStart: '', dtEnd: '', q: '', pageIndex: 0, pageSize: 20 });
    expect(p).not.toHaveProperty('mysqlId');
  });

  it('pageIndex 负数钳 0；pageSize 非法回退 20', () => {
    const p = mysqlDataFilterParams({ mysqlId: 0, dtStart: '', dtEnd: '', q: '', pageIndex: -5, pageSize: 0 });
    expect(p.pageIndex).toBe(0);
    expect(p.pageSize).toBe(20);
  });
});

describe('formatUptime', () => {
  it('0 与负数 → —', () => {
    expect(formatUptime(0)).toBe('—');
    expect(formatUptime(-3)).toBe('—');
  });
  it('秒/分/小时/天四档', () => {
    expect(formatUptime(45)).toBe('45 秒');
    expect(formatUptime(600)).toBe('10 分');
    expect(formatUptime(3 * 3600 + 5 * 60)).toBe('3 小时 5 分');
    expect(formatUptime(2 * 86400 + 3 * 3600)).toBe('2 天 3 小时');
  });
});

describe('formatBytes', () => {
  it('0/负数 → 0 B', () => {
    expect(formatBytes(0)).toBe('0 B');
    expect(formatBytes(-5)).toBe('0 B');
  });
  it('B/KB/MB/GB 阶梯且去尾零', () => {
    expect(formatBytes(356)).toBe('356 B');
    expect(formatBytes(1024)).toBe('1 KB');
    expect(formatBytes(1536)).toBe('1.5 KB');
    expect(formatBytes(1572864)).toBe('1.5 MB');
    expect(formatBytes(2147483648)).toBe('2 GB');
  });
});

describe('formatBufferPool', () => {
  it('双 0 → —；单边有值正常拼', () => {
    expect(formatBufferPool(0, 0)).toBe('—');
    expect(formatBufferPool(128, 65536)).toBe('128 MB · 65536 页');
    expect(formatBufferPool(0, 65536)).toBe('0 MB · 65536 页');
  });
});

describe('mysqlDataTotalPages', () => {
  it('向上取整；0/非法回 1', () => {
    expect(mysqlDataTotalPages(42, 20)).toBe(3);
    expect(mysqlDataTotalPages(40, 20)).toBe(2);
    expect(mysqlDataTotalPages(0, 20)).toBe(1);
    expect(mysqlDataTotalPages(42, 0)).toBe(1);
  });
});

describe('toMySqlDataRows', () => {
  const nodeNames = new Map([[3, '主库节点']]);

  it('PascalCase 归一 + 节点名映射 + 格式化字段', () => {
    const rows = toMySqlDataRows(
      [
        {
          Id: '1900000000000012000',
          MySqlId: 3,
          Name: '主库@10.0.0.8',
          Uptime: 2 * 86400 + 3 * 3600,
          Connections: 12,
          MaxConnections: 100,
          Qps: 321,
          Tps: 45,
          BytesReceived: 1572864,
          BytesSent: 15728640,
          InnodbBufferPoolSize: 128,
          InnodbBufferPoolPages: 65536,
          InnodbBufferPoolHitRate: 98.53,
          SlowQueries: 7,
          Threads: 15,
          ThreadsRunning: 2,
          CreateTime: '2026-11-09 10:30:00',
        },
      ],
      nodeNames,
    );
    expect(rows).toHaveLength(1);
    const r = rows[0];
    expect(r.id).toBe('1900000000000012000');
    expect(r.mysqlId).toBe(3);
    expect(r.nodeName).toBe('主库节点');
    expect(r.name).toBe('主库@10.0.0.8');
    expect(r.uptime).toBe('2 天 3 小时');
    expect(r.connections).toBe(12);
    expect(r.maxConnections).toBe(100);
    expect(r.qps).toBe(321);
    expect(r.tps).toBe(45);
    expect(r.bytesIn).toBe('1.5 MB');
    expect(r.bytesOut).toBe('15 MB');
    expect(r.bufferPool).toBe('128 MB · 65536 页');
    expect(r.hitRate).toBe('98.53%');
    expect(r.slowQueries).toBe(7);
    expect(r.threads).toBe(15);
    expect(r.threadsRunning).toBe(2);
    expect(r.sampledAt).toBe('2026-11-09 10:30');
  });

  it('camelCase 同样兼容；缺节点映射留空；命中率 0 → —；无时间 → —', () => {
    const rows = toMySqlDataRows([{ id: 77, mySqlId: 9, name: '从库', innodbBufferPoolHitRate: 0, createTime: '' }]);
    const r = rows[0];
    expect(r.id).toBe('77');
    expect(r.nodeName).toBe('');
    expect(r.hitRate).toBe('—');
    expect(r.bufferPool).toBe('—');
    expect(r.sampledAt).toBe('—');
    expect(r.uptime).toBe('—');
  });
});

describe('fetchMySqlData', () => {
  it('成功：GET /MySql/MySqlData 携带拼装参数；total 取 page.totalCount', async () => {
    const calls: Call[] = [];
    const req = stubReq(
      {
        '/MySql/MySqlData': {
          data: [{ Id: '1900000000000012000', MySqlId: 3, Name: '主库@10.0.0.8', Uptime: 600, Qps: 100, CreateTime: '2026-11-09 10:30:00' }],
          page: { totalCount: 42 },
        },
      },
      { calls },
    );
    const res = await fetchMySqlData(req, { ...emptyMySqlDataFilter(), mysqlId: 3, pageIndex: 0 });
    expect(res.error).toBe('');
    expect(res.total).toBe(42);
    expect(res.rows).toHaveLength(1);
    expect(res.rows[0].uptime).toBe('10 分');
    expect(calls).toHaveLength(1);
    expect(calls[0].method).toBe('GET');
    expect(calls[0].url).toBe('/MySql/MySqlData');
    expect(calls[0].params).toMatchObject({ mysqlId: 3, pageIndex: 0, pageSize: 20 });
  });

  it('失败：Error 消息透传归因；rows 空且 total 0', async () => {
    const req = stubReq({}, { throwOn: '/MySql/MySqlData' });
    const res = await fetchMySqlData(req, emptyMySqlDataFilter());
    expect(res.rows).toEqual([]);
    expect(res.total).toBe(0);
    expect(res.error).toBe('数据库拒绝');
  });

  it('失败：非 Error 抛出 → 兜底中文文案', async () => {
    const req = async () => {
      throw 'boom';
    };
    const res = await fetchMySqlData(req, emptyMySqlDataFilter());
    expect(res.error).toBe('MySQL 数据查询失败');
  });
});

describe('fetchMySqlNodes / toMySqlNodeOptions', () => {
  it('成功：enable=true&pageSize=0 全量候选，脏行过滤', async () => {
    const calls: Call[] = [];
    const req = stubReq(
      { '/MySql/MySqlNode': { data: [{ Id: 3, Name: '主库节点' }, { Id: 0, Name: '脏行' }, { Id: 5, Name: '' }, { Name: '无Id' }] } },
      { calls },
    );
    const res = await fetchMySqlNodes(req);
    expect(res.error).toBe('');
    expect(res.options).toEqual([{ id: 3, name: '主库节点' }]);
    expect(calls[0].params).toEqual({ enable: true, pageSize: 0 });
  });

  it('toMySqlNodeOptions 纯函数：无名字/无 Id 行剔除', () => {
    expect(toMySqlNodeOptions([{ Id: 3, Name: '主库节点' }, { Id: 0, Name: '脏行' }, { Name: '无Id' }, { Id: 5, Name: '' }])).toEqual([
      { id: 3, name: '主库节点' },
    ]);
  });

  it('失败：错误归因', async () => {
    const req = stubReq({}, { throwOn: '/MySql/MySqlNode' });
    const res = await fetchMySqlNodes(req);
    expect(res.options).toEqual([]);
    expect(res.error).toBe('数据库拒绝');
  });
});

// ==================== MW-02 · MySQL 节点 ====================

describe('mysqlNodeFilterParams', () => {
  it('默认条件下发分页、省略全部筛选（id/server/port/projectId/enable 均不下发）', () => {
    const p = mysqlNodeFilterParams(emptyMySqlNodeFilter());
    expect(p).toEqual({ pageIndex: 0, pageSize: 20 });
    expect(p).not.toHaveProperty('id');
    expect(p).not.toHaveProperty('server');
    expect(p).not.toHaveProperty('port');
    expect(p).not.toHaveProperty('projectId');
    expect(p).not.toHaveProperty('enable');
    expect(p).not.toHaveProperty('Q');
  });

  it('全量筛选下发：server 精确值、port/projectId 数字化、enable 布尔、dtEnd 补 23:59:59、Q、深链 id', () => {
    const p = mysqlNodeFilterParams({
      id: 5, server: ' 127.0.0.1 ', port: '3306', projectId: '2', enable: 'false',
      dtStart: ' 2026-11-01 ', dtEnd: '2026-11-09', q: ' 主库 ', pageIndex: 1, pageSize: 20,
    });
    expect(p).toEqual({
      pageIndex: 1, pageSize: 20, id: 5, server: '127.0.0.1', port: 3306, projectId: 2,
      enable: false, dtStart: '2026-11-01', dtEnd: '2026-11-09 23:59:59', Q: '主库',
    });
  });

  it('port/projectId = 0 合法等值过滤照发（后端 >=0 即过滤）；非数字不下发', () => {
    const p0 = mysqlNodeFilterParams({ ...emptyMySqlNodeFilter(), port: '0', projectId: '0' });
    expect(p0.port).toBe(0);
    expect(p0.projectId).toBe(0);
    const bad = mysqlNodeFilterParams({ ...emptyMySqlNodeFilter(), port: 'abc', projectId: 'x1' });
    expect(bad).not.toHaveProperty('port');
    expect(bad).not.toHaveProperty('projectId');
  });

  it('enable 空串=全部不下发；enable true 下发布尔 true', () => {
    const off = mysqlNodeFilterParams({ ...emptyMySqlNodeFilter(), enable: '' });
    expect(off).not.toHaveProperty('enable');
    const on = mysqlNodeFilterParams({ ...emptyMySqlNodeFilter(), enable: 'true' });
    expect(on.enable).toBe(true);
  });

  it('dtEnd 带时间原样下发，不重复补时间', () => {
    const p = mysqlNodeFilterParams({ ...emptyMySqlNodeFilter(), dtEnd: '2026-11-09 12:00:00' });
    expect(p.dtEnd).toBe('2026-11-09 12:00:00');
  });
});

describe('toMySqlNodeRows', () => {
  it('Pascal/camel 双兼容；Int32 Id 用 number；ProjectName 扩展列映射', () => {
    const rows = toMySqlNodeRows([
      { Id: 3, ProjectId: 1, ProjectName: '银河项目', Name: '主库', Category: 'MySQL', Server: '127.0.0.1', Port: 3306, UserName: 'root', DatabaseName: 'stardust', Version: '8.0.36', Enable: true, UpdateTime: '2026-11-09 10:30:00' },
      { id: 4, projectId: 0, projectName: '', name: '备库', category: '', server: '10.0.0.2', port: 3307, userName: '', databaseName: '', version: '', enable: false, updateTime: '' },
    ]);
    expect(rows[0]).toEqual({
      id: 3, projectId: 1, projectName: '银河项目', name: '主库', category: 'MySQL', server: '127.0.0.1',
      port: 3306, userName: 'root', databaseName: 'stardust', version: '8.0.36', enable: true, updateTime: '2026-11-09 10:30',
    });
    expect(rows[1].enable).toBe(false);
    expect(rows[1].updateTime).toBe('—');
  });

  it('enable 字符串/数字容错；缺省行归零不抛', () => {
    const rows = toMySqlNodeRows([{ Enable: 'true' }, { Enable: 0 }, {}]);
    expect(rows.map((r) => r.enable)).toEqual([true, false, false]);
    expect(rows[2].id).toBe(0);
    expect(rows[2].name).toBe('');
  });
});

describe('fetchMySqlNodeList', () => {
  it('成功：行归一 + page.totalCount 优先；params 原样下发', async () => {
    const calls: Call[] = [];
    const req = stubReq(
      { '/MySql/MySqlNode': { data: [{ Id: 3, Name: '主库', Enable: true }], page: { totalCount: 12 } } },
      { calls },
    );
    const res = await fetchMySqlNodeList(req, { ...emptyMySqlNodeFilter(), q: '主库', pageIndex: 0 });
    expect(res.error).toBe('');
    expect(res.total).toBe(12);
    expect(res.rows).toHaveLength(1);
    expect(res.rows[0].name).toBe('主库');
    expect(calls[0].params).toEqual({ pageIndex: 0, pageSize: 20, Q: '主库' });
  });

  it('失败：错误归因', async () => {
    const req = stubReq({}, { throwOn: '/MySql/MySqlNode' });
    const res = await fetchMySqlNodeList(req, emptyMySqlNodeFilter());
    expect(res.rows).toEqual([]);
    expect(res.total).toBe(0);
    expect(res.error).toBe('数据库拒绝');
  });
});

describe('fetchMySqlNodeDetail', () => {
  it('返回对象；data 数组/请求异常 → null', async () => {
    const ok = await fetchMySqlNodeDetail(stubReq({ '/MySql/MySqlNode/3': { data: { Id: 3, Name: '主库' } } }), 3);
    expect(ok).toEqual({ Id: 3, Name: '主库' });
    const arr = await fetchMySqlNodeDetail(stubReq({ '/MySql/MySqlNode/3': { data: [] } }), 3);
    expect(arr).toBeNull();
    const boom = await fetchMySqlNodeDetail(stubReq({}, { throwOn: '/MySql/MySqlNode/3' }), 3);
    expect(boom).toBeNull();
  });
});

describe('nodeDetailToForm / validateMySqlNodeForm / nodeUpdatePayload', () => {
  it('Detail → 表单：AlarmQPS camel/Pascal 双兼容、密码回显、缺省归零', () => {
    const f = nodeDetailToForm({ Id: 3, ProjectId: 1, Name: '主库', Category: 'MySQL', Server: '127.0.0.1', Port: 3306, UserName: 'root', Password: 'seed-pwd', DatabaseName: 'stardust', Version: '8.0.36', Enable: true, WebHook: 'http://h', AlarmQPS: 300, AlarmConnections: 200, AlarmSlowQuery: 10, Remark: 'r' });
    expect(f).toEqual({
      projectId: 1, name: '主库', category: 'MySQL', server: '127.0.0.1', port: 3306, userName: 'root',
      password: 'seed-pwd', databaseName: 'stardust', version: '8.0.36', enable: true, webHook: 'http://h',
      alarmConnections: 200, alarmQps: 300, alarmSlowQuery: 10, remark: 'r',
    });
    const empty = nodeDetailToForm({});
    expect(empty.port).toBe(0);
    expect(empty.password).toBe('');
    expect(empty.enable).toBe(false);
  });

  it('校验：Name 空 → 「名称不能为空」；有值通过', () => {
    expect(validateMySqlNodeForm({ ...nodeDetailToForm({}), name: ' ' }).name).toBe('名称不能为空');
    expect(validateMySqlNodeForm({ ...nodeDetailToForm({}), name: '主库' })).toEqual({});
  });

  it('提交体 = Detail 全量 + 表单覆盖；Id 与未入表单的审计字段原样保留', () => {
    const d = { Id: 3, CreateTime: '2026-01-01 00:00:00', Name: '旧名', Enable: false };
    const body = nodeUpdatePayload(d, { ...nodeDetailToForm(d), name: '新名', enable: true, port: 3307 });
    expect(body.Id).toBe(3);
    expect(body.CreateTime).toBe('2026-01-01 00:00:00');
    expect(body.Name).toBe('新名');
    expect(body.Enable).toBe(true);
    expect(body.Port).toBe(3307);
  });
});

describe('submitMySqlNode', () => {
  it('detail 缺 Id → 明确失败不盲发', async () => {
    const calls: Call[] = [];
    const req = stubReq({}, { calls });
    const res = await submitMySqlNode(req, {}, nodeDetailToForm({}));
    expect(res).toEqual({ ok: false, message: '节点不存在或已被删除' });
    expect(calls).toHaveLength(0);
  });

  it('成功：PUT /MySql/MySqlNode/{id} data=合并提交体；code!=0 → ok=false 透传后端 message', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/MySql/MySqlNode/3': { data: null } }, { calls });
    const d = { Id: 3, Name: '旧名' };
    const res = await submitMySqlNode(req, d, { ...nodeDetailToForm(d), name: '新名' });
    expect(res.ok).toBe(true);
    expect(calls[0].method).toBe('PUT');
    expect(calls[0].url).toBe('/MySql/MySqlNode/3');
    expect(calls[0].data).toMatchObject({ Id: 3, Name: '新名' });
    const bad = await submitMySqlNode(
      stubReq({ '/MySql/MySqlNode/3': { data: null, message: '端口被占用', code: 500 } }),
      d,
      nodeDetailToForm(d),
    );
    expect(bad).toEqual({ ok: false, message: '端口被占用' });
  });

  it('异常：错误归因', async () => {
    const req = stubReq({}, { throwOn: '/MySql/MySqlNode/3' });
    const res = await submitMySqlNode(req, { Id: 3 }, nodeDetailToForm({}));
    expect(res.ok).toBe(false);
    expect(res.message).toBe('数据库拒绝');
  });
});

describe('refreshMySqlNode', () => {
  it('成功：POST /MySql/MySqlNode/Refresh params={id}；后端 message 透传（C05 toast）', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/MySql/MySqlNode/Refresh': { data: null, message: '刷新[主库]成功！' } }, { calls });
    const res = await refreshMySqlNode(req, 3);
    expect(res).toEqual({ ok: true, message: '刷新[主库]成功！' });
    expect(calls[0].method).toBe('POST');
    expect(calls[0].params).toEqual({ id: 3 });
  });

  it('message 空回退兜底；异常错误归因', async () => {
    const res = await refreshMySqlNode(stubReq({ '/MySql/MySqlNode/Refresh': { data: null } }), 3);
    expect(res).toEqual({ ok: true, message: '刷新失败' });
    const boom = await refreshMySqlNode(stubReq({}, { throwOn: '/MySql/MySqlNode/Refresh' }), 3);
    expect(boom).toEqual({ ok: false, message: '数据库拒绝' });
  });
});

// ==================== MW-03 · Redis 数据 ====================

describe('redisDataFilterParams', () => {
  it('硬性负向（C04 定稿裁定①）：任何条件下 params 都不含 pageSize 键——服务端魔法（redisId>0 && PageSize==20 → 1440）靠缺省触发', () => {
    const p = redisDataFilterParams({ redisId: 7, dtStart: '2026-11-08', dtEnd: '2026-11-09', q: 'cache', pageIndex: 2, pageSize: 50 });
    expect(p).not.toHaveProperty('pageSize');
    expect(p).not.toHaveProperty('PageSize');
  });

  it('无节点（redisId<=0）：下发 pageIndex（服务端分页），redisId 不下发（0 非「全部」语义，RedisDataController.cs:30-32）', () => {
    const p = redisDataFilterParams({ redisId: 0, dtStart: '', dtEnd: '', q: '', pageIndex: 3, pageSize: 20 });
    expect(p).toEqual({ pageIndex: 3 });
    expect(p).not.toHaveProperty('redisId');
  });

  it('节点选中（redisId>0）：下发 redisId、不下发 pageIndex（一次拉全 1440，本地分页切片）', () => {
    const p = redisDataFilterParams({ redisId: 7, dtStart: '', dtEnd: '', q: '', pageIndex: 2, pageSize: 20 });
    expect(p).toEqual({ redisId: 7 });
    expect(p).not.toHaveProperty('pageIndex');
  });

  it('dtStart/dtEnd/Q trim 下发；dtEnd 纯日期补 23:59:59（含端点，同 MW-01 先例）', () => {
    const p = redisDataFilterParams({ redisId: 0, dtStart: ' 2026-11-08 ', dtEnd: '2026-11-09', q: ' 主库 ', pageIndex: 0, pageSize: 20 });
    expect(p).toEqual({ pageIndex: 0, dtStart: '2026-11-08', dtEnd: '2026-11-09 23:59:59', Q: '主库' });
  });

  it('dtEnd 带时间原样下发；pageIndex 负数钳 0', () => {
    const p = redisDataFilterParams({ redisId: 0, dtStart: '', dtEnd: '2026-11-09 12:00:00', q: '', pageIndex: -5, pageSize: 20 });
    expect(p.dtEnd).toBe('2026-11-09 12:00:00');
    expect(p.pageIndex).toBe(0);
  });
});

describe('toRedisDataRows', () => {
  it('PascalCase 归一；nodeName 由候选映射；雪花 Id 以 string 承载（曲线字段 inputKbps/outputKbps 一并归一）', () => {
    const rows = toRedisDataRows(
      [
        {
          Id: '1900000000000012001',
          RedisId: 7,
          Name: 'cache@10.0.0.9',
          Speed: 1200,
          InputKbps: 2048.5,
          OutputKbps: 4096.25,
          ConnectedClients: 8,
          UsedMemory: 512,
          Keys: 9000,
          Commands: 123456,
          CreateTime: '2026-11-09 10:30:00',
        },
      ],
      new Map([[7, '主缓存']]),
    );
    expect(rows).toEqual([
      {
        id: '1900000000000012001',
        redisId: 7,
        nodeName: '主缓存',
        name: 'cache@10.0.0.9',
        speed: 1200,
        inputKbps: 2048.5,
        outputKbps: 4096.25,
        connections: 8,
        usedMemory: 512,
        keys: 9000,
        commands: 123456,
        sampledAt: '2026-11-09 10:30',
      },
    ]);
  });

  it('camelCase 响应兼容；无候选映射 nodeName 留空；缺字段归零、空时间 → —', () => {
    const rows = toRedisDataRows([{ id: 5, redisId: 9, name: 'x', speed: 1, connectedClients: 2, usedMemory: 3, keys: 4, commands: 5, createTime: '' }]);
    expect(rows[0]).toEqual({
      id: '5',
      redisId: 9,
      nodeName: '',
      name: 'x',
      speed: 1,
      inputKbps: 0,
      outputKbps: 0,
      connections: 2,
      usedMemory: 3,
      keys: 4,
      commands: 5,
      sampledAt: '—',
    });
  });
});

describe('redisDataTotalPages', () => {
  it('向上取整；total<=0 或 pageSize<=0 → 1（语义同 mysqlDataTotalPages）', () => {
    expect(redisDataTotalPages(1440, 50)).toBe(29);
    expect(redisDataTotalPages(45, 20)).toBe(3);
    expect(redisDataTotalPages(0, 50)).toBe(1);
    expect(redisDataTotalPages(100, 0)).toBe(1);
  });
});

describe('fetchRedisData', () => {
  it('无节点：GET /Redis/RedisData 携带 pageIndex（无 pageSize），rows=服务端分页结果；total 取 page.totalCount', async () => {
    const calls: Call[] = [];
    const req = stubReq(
      {
        '/Redis/RedisData': {
          data: [{ Id: '1900000000000012001', RedisId: 7, Name: 'c', Speed: 5, CreateTime: '2026-11-09 10:30:00' }],
          page: { totalCount: 42 },
        },
      },
      { calls },
    );
    const res = await fetchRedisData(req, { ...emptyRedisDataFilter(), pageIndex: 1 });
    expect(res.error).toBe('');
    expect(res.total).toBe(42);
    expect(res.rows).toHaveLength(1);
    expect(calls[0].method).toBe('GET');
    expect(calls[0].url).toBe('/Redis/RedisData');
    expect(calls[0].params).toEqual({ pageIndex: 1 });
    expect(calls[0].params).not.toHaveProperty('pageSize');
  });

  it('节点选中：不下发 pageIndex/pageSize；rows=本地分页切片（当前页），chartRows=全量（曲线数据源）', async () => {
    const calls: Call[] = [];
    const data = Array.from({ length: 45 }, (_, i) => ({
      Id: String(1000 + i),
      RedisId: 7,
      Name: `c${i}`,
      Speed: i,
      ConnectedClients: i,
      UsedMemory: i,
      Keys: i,
      Commands: i,
      CreateTime: `2026-11-09 10:${String(i % 60).padStart(2, '0')}:00`,
    }));
    const req = stubReq({ '/Redis/RedisData': { data, page: { totalCount: 45 } } }, { calls });
    const res = await fetchRedisData(req, { redisId: 7, dtStart: '', dtEnd: '', q: '', pageIndex: 1, pageSize: 20 });
    expect(calls[0].params).toEqual({ redisId: 7 });
    expect(res.chartRows).toHaveLength(45);
    expect(res.rows).toHaveLength(20);
    expect(res.rows[0].name).toBe('c20');
    expect(res.total).toBe(45);
  });

  it('节点选中尾页切片不足一页：rows=余量行', async () => {
    const data = Array.from({ length: 25 }, (_, i) => ({ Id: String(2000 + i), RedisId: 7, Name: `d${i}`, Speed: i, CreateTime: '2026-11-09 10:30:00' }));
    const req = stubReq({ '/Redis/RedisData': { data, page: { totalCount: 25 } } });
    const res = await fetchRedisData(req, { redisId: 7, dtStart: '', dtEnd: '', q: '', pageIndex: 1, pageSize: 20 });
    expect(res.rows).toHaveLength(5);
    expect(res.rows[0].name).toBe('d20');
  });

  it('失败：Error 消息透传归因；rows/chartRows 空、total 0', async () => {
    const req = stubReq({}, { throwOn: '/Redis/RedisData' });
    const res = await fetchRedisData(req, emptyRedisDataFilter());
    expect(res.rows).toEqual([]);
    expect(res.chartRows).toEqual([]);
    expect(res.total).toBe(0);
    expect(res.error).toBe('数据库拒绝');
  });

  it('失败：非 Error 抛出 → 兜底中文文案', async () => {
    const req = async () => {
      throw 'boom';
    };
    const res = await fetchRedisData(req, emptyRedisDataFilter());
    expect(res.error).toBe('Redis 数据查询失败');
  });
});

describe('fetchRedisNodes / toRedisNodeOptions（NodeSearch Lov 数据源，MW-03 筛选共用）', () => {
  it('成功：GET /Redis/RedisNode/NodeSearch 无参全类型；脏行剔除；server 保留', async () => {
    const calls: Call[] = [];
    const req = stubReq(
      {
        '/Redis/RedisNode/NodeSearch': {
          data: [
            { Id: 7, Name: '主缓存', Server: '10.0.0.9:6379', Category: 'Cache' },
            { Id: 0, Name: '脏行' },
            { Name: '无Id' },
            { Id: 8, Name: '' },
          ],
        },
      },
      { calls },
    );
    const res = await fetchRedisNodes(req);
    expect(res.error).toBe('');
    expect(res.options).toEqual([{ id: 7, name: '主缓存', server: '10.0.0.9:6379' }]);
    expect(calls[0].method).toBe('GET');
    expect(calls[0].url).toBe('/Redis/RedisNode/NodeSearch');
    expect(calls[0].params).toBeUndefined();
  });

  it('失败：错误归因', async () => {
    const req = stubReq({}, { throwOn: 'NodeSearch' });
    const res = await fetchRedisNodes(req);
    expect(res.options).toEqual([]);
    expect(res.error).toBe('数据库拒绝');
  });
});

describe('buildRedisDataChartOption（C03 定稿裁定②：六序列不含 Commands）', () => {
  it('六序列=速度/入流量/出流量/连接数/已用内存/Keys 数；X 轴=采样时间；每序列 line 型且行数一致', () => {
    const rows = toRedisDataRows([
      { Id: '1', RedisId: 7, Name: 'c', Speed: 1, InputKbps: 2.5, OutputKbps: 3.5, ConnectedClients: 4, UsedMemory: 5, Keys: 6, CreateTime: '2026-11-09 10:30:00' },
      { Id: '2', RedisId: 7, Name: 'c', Speed: 7, InputKbps: 8.5, OutputKbps: 9.5, ConnectedClients: 10, UsedMemory: 11, Keys: 12, CreateTime: '2026-11-09 10:31:00' },
    ]);
    const opt = buildRedisDataChartOption(rows) as {
      legend: { data: string[] };
      xAxis: { data: string[] };
      series: { name: string; type: string; data: number[] }[];
    };
    expect(opt.legend.data).toEqual(['速度', '入流量', '出流量', '连接数', '已用内存', 'Keys 数']);
    expect(opt.legend.data).not.toContain('命令数');
    expect(opt.xAxis.data).toEqual(['2026-11-09 10:30', '2026-11-09 10:31']);
    expect(opt.series).toHaveLength(6);
    expect(opt.series.map((s) => s.name)).toEqual(['速度', '入流量', '出流量', '连接数', '已用内存', 'Keys 数']);
    expect(opt.series.every((s) => s.type === 'line')).toBe(true);
    expect(opt.series[0].data).toEqual([1, 7]);
    expect(opt.series[5].data).toEqual([6, 12]);
  });

  it('空行：X 轴与序列数据为空数组（B1 空态由卡片渲染文案）', () => {
    const opt = buildRedisDataChartOption([]) as { xAxis: { data: string[] }; series: { data: number[] }[] };
    expect(opt.xAxis.data).toEqual([]);
    expect(opt.series.every((s) => s.data.length === 0)).toBe(true);
  });
});

// ==================== MW-04 Redis 消息队列（CARD-MW-04 定稿 4 裁定；Biz Search Redis消息队列.Biz.cs:95-106） ====================

describe('redisQueueFilterParams（MW-04：标准服务端分页，无 RedisData 1440 魔法）', () => {
  it('pageIndex/pageSize 恒发（PageParameter 标准分页，与 RedisData「选中不发」相反）；redisId=0 全部不下发（Biz redisId>=0 即等值过滤，0 非「全部」——对齐 MW-01 mysqlId 语义）', () => {
    const p = redisQueueFilterParams({ redisId: 0, category: '', dtStart: '', dtEnd: '', q: '', pageIndex: 2, pageSize: 20 });
    expect(p).toEqual({ pageIndex: 2, pageSize: 20 });
    expect(p).not.toHaveProperty('redisId');
  });

  it('redisId=7 下发；category trim 非空下发', () => {
    const p = redisQueueFilterParams({ redisId: 7, category: ' 订单队列 ', dtStart: '', dtEnd: '', q: '', pageIndex: 0, pageSize: 20 });
    expect(p).toEqual({ pageIndex: 0, pageSize: 20, redisId: 7, category: '订单队列' });
  });

  it('dtStart/dtEnd 同先例：trim、纯日期补 23:59:59、带时间原样；Q trim 下发', () => {
    const p = redisQueueFilterParams({ redisId: 0, category: '', dtStart: ' 2026-11-08 ', dtEnd: '2026-11-09', q: ' pay ', pageIndex: 0, pageSize: 20 });
    expect(p).toEqual({ pageIndex: 0, pageSize: 20, dtStart: '2026-11-08', dtEnd: '2026-11-09 23:59:59', Q: 'pay' });
  });

  it('pageIndex 负数钳 0；pageSize<=0 → 20（对齐 mysqlDataFilterParams 先例）', () => {
    const p = redisQueueFilterParams({ redisId: 0, category: '', dtStart: '', dtEnd: '', q: '', pageIndex: -5, pageSize: 0 });
    expect(p.pageIndex).toBe(0);
    expect(p.pageSize).toBe(20);
  });
});

describe('toRedisQueueRows', () => {
  it('PascalCase 全字段归一；Id=Int32 以 number 承载（非雪花，同 MySqlNode 先例）；nodeName 由候选映射', () => {
    const rows = toRedisQueueRows(
      [
        {
          Id: 3,
          RedisId: 7,
          Name: '订单流',
          Category: '订单',
          Db: 0,
          Topic: 'order-paid',
          Type: 'Stream',
          Groups: 'g1,g2',
          Consumers: 2,
          Total: 45678,
          Messages: 120,
          MaxMessages: 1000,
          Enable: true,
          FirstConsumer: '2026-11-09 08:00:00',
          LastActive: '2026-11-09 10:30:00',
          TraceId: 't-abc',
          UpdateTime: '2026-11-09 10:35:00',
        },
      ],
      new Map([[7, '主缓存']]),
    );
    expect(rows).toEqual([
      {
        id: 3,
        redisId: 7,
        nodeName: '主缓存',
        name: '订单流',
        category: '订单',
        db: 0,
        topic: 'order-paid',
        type: 'Stream',
        groups: 'g1,g2',
        consumers: 2,
        total: 45678,
        messages: 120,
        maxMessages: 1000,
        enable: true,
        firstConsume: '2026-11-09 08:00',
        lastActive: '2026-11-09 10:30',
        traceId: 't-abc',
        updateTime: '2026-11-09 10:35',
      },
    ]);
  });

  it('camelCase 兼容；无候选映射 nodeName 留空；缺字段归零、Enable 缺省 false、空时间 —', () => {
    const rows = toRedisQueueRows([{ id: 5, name: 'q', redisId: 9 }]);
    expect(rows).toEqual([
      {
        id: 5,
        redisId: 9,
        nodeName: '',
        name: 'q',
        category: '',
        db: 0,
        topic: '',
        type: '',
        groups: '',
        consumers: 0,
        total: 0,
        messages: 0,
        maxMessages: 0,
        enable: false,
        firstConsume: '—',
        lastActive: '—',
        traceId: '',
        updateTime: '—',
      },
    ]);
  });
});

describe('redisQueueTotalPages', () => {
  it('向上取整；total<=0 或 pageSize<=0 → 1（语义同 mysqlDataTotalPages）', () => {
    expect(redisQueueTotalPages(45, 20)).toBe(3);
    expect(redisQueueTotalPages(40, 20)).toBe(2);
    expect(redisQueueTotalPages(0, 20)).toBe(1);
    expect(redisQueueTotalPages(45, 0)).toBe(1);
  });
});

describe('fetchRedisQueues', () => {
  it('GET /Redis/RedisMessageQueue + params 透传；rows 归一 + total=page.totalCount', async () => {
    const calls: Call[] = [];
    const req = stubReq(
      {
        '/Redis/RedisMessageQueue': {
          data: [{ Id: 3, RedisId: 7, Name: '订单流', Topic: 'order-paid', Messages: 120, UpdateTime: '2026-11-09 10:35:00' }],
          page: { totalCount: 46 },
        },
      },
      { calls },
    );
    const res = await fetchRedisQueues(req, { redisId: 7, category: '订单', dtStart: '', dtEnd: '', q: '', pageIndex: 1, pageSize: 20 }, new Map([[7, '主缓存']]));
    expect(calls).toHaveLength(1);
    expect(calls[0].url).toBe('/Redis/RedisMessageQueue');
    expect(calls[0].params).toEqual({ pageIndex: 1, pageSize: 20, redisId: 7, category: '订单' });
    expect(res.error).toBe('');
    expect(res.total).toBe(46);
    expect(res.rows).toHaveLength(1);
    expect(res.rows[0]).toMatchObject({ id: 3, nodeName: '主缓存', topic: 'order-paid', messages: 120 });
  });

  it('page 缺失 → total 回落 rows.length；请求失败 → error 归因、rows=[]、total=0', async () => {
    const ok = await fetchRedisQueues(stubReq({ '/Redis/RedisMessageQueue': { data: [{ Id: 1, Name: 'a' }] } }), emptyRedisQueueFilter());
    expect(ok.total).toBe(1);
    const bad = await fetchRedisQueues(stubReq({}, { throwOn: 'RedisMessageQueue' }), emptyRedisQueueFilter());
    expect(bad).toEqual({ rows: [], total: 0, error: '数据库拒绝' });
  });
});

// ==================== MW-05 Redis 节点 ====================

describe('redisNodeFilterParams（MW-05：Id/category/server/enable/dt/Q，Biz Search 语义对齐）', () => {
  it('redisNodeTotalPages：向上取整与零值兜底', () => {
    expect(redisNodeTotalPages(0, 20)).toBe(1);
    expect(redisNodeTotalPages(41, 20)).toBe(3);
    expect(redisNodeTotalPages(40, 20)).toBe(2);
    expect(redisNodeTotalPages(10, 0)).toBe(1);
  });

  it('全空 → 恒发 pageIndex/pageSize，其余不下发（enable 全部省略）', () => {
    expect(redisNodeFilterParams(emptyRedisNodeFilter())).toEqual({ pageIndex: 0, pageSize: 20 });
  });

  it('部分填 → 仅非空下发；id>0 深链随查；enable 三态原样', () => {
    expect(
      redisNodeFilterParams({ ...emptyRedisNodeFilter(), id: 5, category: '缓存', server: '127.0.0.1:6379', enable: 'false', q: '主' }),
    ).toEqual({ pageIndex: 0, pageSize: 20, id: 5, category: '缓存', server: '127.0.0.1:6379', enable: false, Q: '主' });
  });

  it('dtStart/dtEnd → dtEnd 补 23:59:59（UpdateTime.Between 含端点）', () => {
    expect(redisNodeFilterParams({ ...emptyRedisNodeFilter(), dtStart: '2026-11-01', dtEnd: '2026-11-09' })).toEqual({
      pageIndex: 0,
      pageSize: 20,
      dtStart: '2026-11-01',
      dtEnd: '2026-11-09 23:59:59',
    });
  });
});

describe('toRedisNodeRows（MW-05：Pascal/camel 双兼容；密码不映射——任何只读路径不渲染）', () => {
  it('Pascal 真源 13 字段：Enable/ScanQueue boolOf、updateTime 缺省 —', () => {
    const rows = toRedisNodeRows([
      { Id: 3, ProjectId: 1, ProjectName: '星尘', Name: '主缓存', Category: '缓存', Server: '127.0.0.1:6379', UserName: 'default', Password: 'secret', Version: '7.2.4', Mode: 'standalone', Role: 'master', Enable: true, ScanQueue: 1, UpdateTime: '2026-11-09 10:00:00' },
      { Id: 4, Name: '从缓存', Enable: false },
    ]);
    expect(rows[0]).toEqual({
      id: 3, projectId: 1, projectName: '星尘', name: '主缓存', category: '缓存', server: '127.0.0.1:6379',
      userName: 'default', version: '7.2.4', mode: 'standalone', role: 'master', enable: true, scanQueue: true,
      updateTime: '2026-11-09 10:00',
    });
    expect(rows[1].enable).toBe(false);
    expect(rows[1].scanQueue).toBe(false);
    expect(rows[1].updateTime).toBe('—');
    expect(rows[0]).not.toHaveProperty('password');
  });

  it('camel 兜底', () => {
    const rows = toRedisNodeRows([{ id: 9, name: 'n', server: 's', enable: 'true' }]);
    expect(rows[0]).toMatchObject({ id: 9, name: 'n', server: 's', enable: true });
  });
});

describe('nodeDetailToRedisForm / validateRedisNodeForm / redisNodeUpdatePayload（MW-05）', () => {
  it('Detail → 表单：密码回显、Enable/ScanQueue boolOf、缺省归零', () => {
    const f = nodeDetailToRedisForm({ Id: 3, ProjectId: 1, Name: '主缓存', Category: '缓存', Server: '127.0.0.1:6379', UserName: 'default', Password: 'seed-pwd', Version: '7.2.4', Mode: 'standalone', Role: 'master', MaxMemory: 1024, Enable: true, ScanQueue: 1, WebHook: 'http://h', AlarmMemoryRate: 90, Remark: 'r' });
    expect(f).toMatchObject({ name: '主缓存', password: 'seed-pwd', enable: true, scanQueue: true, maxMemory: 1024, alarmMemoryRate: 90, remark: 'r' });
    const empty = nodeDetailToRedisForm({});
    expect(empty.password).toBe('');
    expect(empty.enable).toBe(false);
    expect(empty.scanQueue).toBe(true);
    expect(empty.maxMemory).toBe(0);
  });

  it('校验：Name 空 → 「名称不能为空」；有值通过', () => {
    expect(validateRedisNodeForm({ ...nodeDetailToRedisForm({}), name: ' ' }).name).toBe('名称不能为空');
    expect(validateRedisNodeForm({ ...nodeDetailToRedisForm({}), name: '主缓存' })).toEqual({});
  });

  it('提交体 = Detail 全量 + 表单覆盖；Id 与审计字段原样保留', () => {
    const d = { Id: 3, CreateTime: '2026-01-01 00:00:00', Name: '旧名', Enable: false };
    const body = redisNodeUpdatePayload(d, { ...nodeDetailToRedisForm(d), name: '新名', enable: true, scanQueue: false });
    expect(body.Id).toBe(3);
    expect(body.CreateTime).toBe('2026-01-01 00:00:00');
    expect(body.Name).toBe('新名');
    expect(body.Enable).toBe(true);
    expect(body.ScanQueue).toBe(false);
  });
});

describe('fetchRedisNodeList（MW-05：GET /Redis/RedisNode，标准服务端分页）', () => {
  it('GET + params 透传；rows 归一 + total=page.totalCount', async () => {
    const calls: Call[] = [];
    const req = stubReq(
      {
        '/Redis/RedisNode': {
          data: [{ Id: 3, Name: '主缓存', Server: '127.0.0.1:6379', Enable: true, UpdateTime: '2026-11-09 10:00:00' }],
          page: { totalCount: 23 },
        },
      },
      { calls },
    );
    const res = await fetchRedisNodeList(req, { ...emptyRedisNodeFilter(), category: '缓存', pageIndex: 1 });
    expect(calls).toHaveLength(1);
    expect(calls[0].url).toBe('/Redis/RedisNode');
    expect(calls[0].params).toEqual({ pageIndex: 1, pageSize: 20, category: '缓存' });
    expect(res.error).toBe('');
    expect(res.total).toBe(23);
    expect(res.rows[0]).toMatchObject({ id: 3, name: '主缓存' });
  });

  it('page 缺失 → total 回落 rows.length；请求失败 → error 归因、rows=[]、total=0', async () => {
    const ok = await fetchRedisNodeList(stubReq({ '/Redis/RedisNode': { data: [{ Id: 1, Name: 'a' }] } }), emptyRedisNodeFilter());
    expect(ok.total).toBe(1);
    const bad = await fetchRedisNodeList(stubReq({}, { throwOn: '/Redis/RedisNode' }), emptyRedisNodeFilter());
    expect(bad).toEqual({ rows: [], total: 0, error: '数据库拒绝' });
  });
});

describe('fetchRedisNodeDetail / submitRedisNode（MW-05：GET+PUT /Redis/RedisNode/{id}）', () => {
  it('Detail：data Rec → 原样；data null → null；异常 → null', async () => {
    expect(await fetchRedisNodeDetail(stubReq({ '/Redis/RedisNode/3': { data: { Id: 3, Name: '主缓存' } } }), 3)).toEqual({ Id: 3, Name: '主缓存' });
    expect(await fetchRedisNodeDetail(stubReq({ '/Redis/RedisNode/3': { data: null } }), 3)).toBeNull();
    expect(await fetchRedisNodeDetail(stubReq({}, { throwOn: '/Redis/RedisNode/3' }), 3)).toBeNull();
  });

  it('submit：detail 缺 Id → 明确失败不盲发；PUT 合并提交体；code!=0 透传', async () => {
    const calls: Call[] = [];
    const res = await submitRedisNode(stubReq({}, { calls }), {}, nodeDetailToRedisForm({}));
    expect(res).toEqual({ ok: false, message: '节点不存在或已被删除' });
    expect(calls).toHaveLength(0);

    const d = { Id: 3, Name: '旧名' };
    const ok = await submitRedisNode(stubReq({ '/Redis/RedisNode/3': { data: null } }, { calls }), d, { ...nodeDetailToRedisForm(d), name: '新名' });
    expect(ok.ok).toBe(true);
    expect(calls[0].method).toBe('PUT');
    expect(calls[0].url).toBe('/Redis/RedisNode/3');
    expect(calls[0].data).toMatchObject({ Id: 3, Name: '新名' });

    const bad = await submitRedisNode(
      stubReq({ '/Redis/RedisNode/3': { data: null, message: '地址冲突', code: 500 } }),
      d,
      nodeDetailToRedisForm(d),
    );
    expect(bad).toEqual({ ok: false, message: '地址冲突' });
  });

  it('submit 异常：错误归因', async () => {
    const res = await submitRedisNode(stubReq({}, { throwOn: '/Redis/RedisNode/3' }), { Id: 3 }, nodeDetailToRedisForm({}));
    expect(res.ok).toBe(false);
    expect(res.message).toBe('数据库拒绝');
  });
});

describe('refreshRedisNode（MW-05：卡 §3 契约 GET /Redis/RedisNode/Refresh?id=，C04 toast 透传）', () => {
  it('成功：GET + params={id}；后端 message 透传「刷新[主缓存]成功！」', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/Redis/RedisNode/Refresh': { data: null, message: '刷新[主缓存]成功！' } }, { calls });
    const res = await refreshRedisNode(req, 3);
    expect(res).toEqual({ ok: true, message: '刷新[主缓存]成功！' });
    expect(calls[0].method).toBe('GET');
    expect(calls[0].params).toEqual({ id: 3 });
  });

  it('message 空回退兜底；异常错误归因', async () => {
    const res = await refreshRedisNode(stubReq({ '/Redis/RedisNode/Refresh': { data: null } }), 3);
    expect(res).toEqual({ ok: true, message: '刷新失败' });
    const boom = await refreshRedisNode(stubReq({}, { throwOn: '/Redis/RedisNode/Refresh' }), 3);
    expect(boom).toEqual({ ok: false, message: '数据库拒绝' });
  });
});

describe('createRedisNode / deleteRedisNode（MW-05：标准实体接口 POST+DELETE，GuardModal 确认后删）', () => {
  it('新增：POST /Redis/RedisNode data=Pascal 提交体；code!=0 透传；异常归因', async () => {
    const calls: Call[] = [];
    const req = stubReq({ '/Redis/RedisNode': { data: null } }, { calls });
    const res = await createRedisNode(req, { ...nodeDetailToRedisForm({}), name: '新节点', server: '127.0.0.1:6380' });
    expect(res.ok).toBe(true);
    expect(calls[0].method).toBe('POST');
    expect(calls[0].url).toBe('/Redis/RedisNode');
    expect(calls[0].data).toMatchObject({ Name: '新节点', Server: '127.0.0.1:6380' });

    const bad = await createRedisNode(stubReq({ '/Redis/RedisNode': { data: null, message: '名称重复', code: 500 } }), nodeDetailToRedisForm({}));
    expect(bad).toEqual({ ok: false, message: '名称重复' });
    const boom = await createRedisNode(stubReq({}, { throwOn: '/Redis/RedisNode' }), nodeDetailToRedisForm({}));
    expect(boom).toEqual({ ok: false, message: '数据库拒绝' });
  });

  it('删除：DELETE /Redis/RedisNode/{id}；失败透传；异常归因', async () => {
    const calls: Call[] = [];
    const res = await deleteRedisNode(stubReq({ '/Redis/RedisNode/3': { data: null, message: '删除成功' } }, { calls }), 3);
    expect(res).toEqual({ ok: true, message: '删除成功' });
    expect(calls[0]).toMatchObject({ method: 'DELETE', url: '/Redis/RedisNode/3' });
    const bad = await deleteRedisNode(stubReq({ '/Redis/RedisNode/3': { data: null, message: '拒绝删除', code: 500 } }), 3);
    expect(bad).toEqual({ ok: false, message: '拒绝删除' });
    const boom = await deleteRedisNode(stubReq({}, { throwOn: '/Redis/RedisNode/3' }), 3);
    expect(boom).toEqual({ ok: false, message: '数据库拒绝' });
  });
});
