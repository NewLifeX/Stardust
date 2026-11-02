// 「此刻」页纯逻辑单测（Vitest）。2026-10-30：先红后绿。
// 覆盖：告警字段映射/空数组/缺字段防御、空态判定、projectId 透传、
// 分段请求失败进 errors（不静默）、应用前 6 / 变更前 5 截断、舰队三态、格式化。
import { describe, it, expect } from 'vitest';
import {
  buildFleet,
  fetchNow,
  fmtDuration,
  hasIncidents,
  pickFeaturedApps,
  relTimeText,
  severityOf,
  toIncidents,
} from './now.logic';
import type { NowResult } from './now.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { AppAggRow } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 桩请求：按 url 返回预设 data；记录收到的 options（用于断言 projectId 透传）；throwUrls 命中即抛 */
function stubReq(map: Record<string, unknown[]>, throwUrls: string[] = []): { req: Req; calls: ReqOptions[] } {
  const calls: ReqOptions[] = [];
  const req: Req = async (opt) => {
    calls.push({ ...opt, params: { ...(opt.params ?? {}) } });
    const url = String(opt.url ?? '');
    if (throwUrls.includes(url)) throw new Error(`stub ${url} 500`);
    if (url in map) return { data: map[url] };
    return { data: [] };
  };
  return { req, calls };
}

const APP_ROWS = (count: number): AppAggRow[] =>
  Array.from({ length: count }, (_, i) => ({
    id: i + 1,
    name: `app-${i + 1}`,
    displayName: `应用 ${i + 1}`,
    category: 'Web',
    version: 'v1.0',
    enable: true,
    updateTime: '',
    instances: 2,
    services: 0,
    metric: null,
    status: 'running' as const,
  }));

describe('toIncidents · 告警归一', () => {
  it('字段映射：Name→title、Category/Action、Duration→持续文案、StartTime→相对时间、Content、Times', () => {
    const nowMs = Date.parse('2026-10-30T10:00:00');
    const list = toIncidents(
      [
        {
          Id: 7,
          Name: 'OrderService 错误数过高',
          Category: '错误数过高',
          Action: 'OrderService',
          Status: 1,
          StartTime: '2026-10-30T09:50:00',
          Duration: 600,
          Times: 3,
          Content: '最近 5 分钟错误率 12%，阈值 1%',
        },
      ],
      nowMs,
    );
    expect(list).toHaveLength(1);
    const it = list[0];
    expect(it.id).toBe(7);
    expect(it.title).toBe('OrderService 错误数过高');
    expect(it.category).toBe('错误数过高');
    expect(it.action).toBe('OrderService');
    expect(it.sinceText).toBe('10 分钟');
    expect(it.relText).toBe('10 分钟前');
    expect(it.content).toBe('最近 5 分钟错误率 12%，阈值 1%');
    expect(it.times).toBe(3);
  });

  it('空数组 → 空列表，不抛错', () => {
    expect(toIncidents([], Date.now())).toEqual([]);
  });

  it('缺字段防御：空行给兜底文案，数字缺失为 0', () => {
    const list = toIncidents([{}], Date.now());
    expect(list).toHaveLength(1);
    const it = list[0];
    expect(it.title).toBe('未命名告警');
    expect(it.category).toBe('');
    expect(it.action).toBe('');
    expect(it.content).toBe('');
    expect(it.sinceText).toBe('刚刚');
    expect(it.relText).toBe('');
  });

  it('按 Id 倒序（新的在前）', () => {
    const list = toIncidents([{ Id: 1 }, { Id: 9, Name: 'b' }, { Id: 5, Name: 'a' }], Date.now());
    expect(list.map((i) => i.id)).toEqual([9, 5, 1]);
  });
});

describe('severityOf · 严重度派生', () => {
  it('类别含「下线」→ danger；其余 → warn', () => {
    expect(severityOf('应用下线')).toBe('danger');
    expect(severityOf('节点下线')).toBe('danger');
    expect(severityOf('错误数过高')).toBe('warn');
    expect(severityOf('CPU 过高')).toBe('warn');
    expect(severityOf('')).toBe('warn');
  });
});

describe('hasIncidents · 空态判定', () => {
  it('有告警 true，空数组 false', () => {
    expect(hasIncidents(toIncidents([{ Id: 1 }], Date.now()))).toBe(true);
    expect(hasIncidents([])).toBe(false);
  });
});

describe('fmtDuration / relTimeText · 格式化', () => {
  it('fmtDuration：秒/分钟/小时/天', () => {
    expect(fmtDuration(45)).toBe('45 秒');
    expect(fmtDuration(300)).toBe('5 分钟');
    expect(fmtDuration(3700)).toBe('1 小时 1 分');
    expect(fmtDuration(90061)).toBe('1 天 1 小时');
    expect(fmtDuration(0)).toBe('刚刚');
    expect(fmtDuration(NaN)).toBe('刚刚');
  });

  it('relTimeText：刚刚/分钟前/小时前/天前，未来时间为空串', () => {
    const now = 1_000_000_000_000;
    expect(relTimeText(now - 30_000, now)).toBe('刚刚');
    expect(relTimeText(now - 900_000, now)).toBe('15 分钟前');
    expect(relTimeText(now - 7_200_000, now)).toBe('2 小时前');
    expect(relTimeText(now - 200_000_000, now)).toBe('2 天前');
    expect(relTimeText(now + 5000, now)).toBe('');
    expect(relTimeText(0, now)).toBe('');
  });
});

describe('buildFleet · 节点舰队三态', () => {
  const nodes = [
    { ID: 1, Name: 'node-a', IP: '10.0.0.1' },
    { ID: 2, Name: 'node-b', IP: '10.0.0.2' },
    { ID: 3, Name: 'node-c', IP: '10.0.0.3' },
  ];
  const online = [
    { NodeID: 1, CpuRate: 23 },
    { NodeID: 2, CpuRate: 91 },
    // node-c 无在线会话
  ];

  it('在线 ok / CPU≥85 high / 无在线会话 off，并带上 name 与 ip', () => {
    const fleet = buildFleet(nodes, online);
    expect(fleet.map((n) => n.status)).toEqual(['ok', 'high', 'off']);
    expect(fleet[0]).toMatchObject({ id: 1, name: 'node-a', ip: '10.0.0.1', cpu: 23 });
    expect(fleet[2].cpu).toBe(0);
  });

  it('缺字段防御：空节点给「未命名节点」，不抛错', () => {
    const fleet = buildFleet([{}], []);
    expect(fleet[0].name).toBe('未命名节点');
    expect(fleet[0].status).toBe('off');
  });
});

describe('pickFeaturedApps · 应用截断与异常优先', () => {
  it('8 行截断为前 6', () => {
    const apps = pickFeaturedApps(APP_ROWS(8), 6);
    expect(apps).toHaveLength(6);
  });

  it('异常/发布失败优先排到前面，其余保持原顺序', () => {
    const rows = APP_ROWS(3);
    rows[0].status = 'running';
    rows[1].status = 'error';
    rows[2].status = 'running';
    const apps = pickFeaturedApps(rows, 6);
    expect(apps.map((a) => a.id)).toEqual([2, 1, 3]);
    expect(apps[0].health).toBe('err');
    expect(apps[1].health).toBe('ok');
  });
});

describe('fetchNow · 端到端取数编排', () => {
  /** 全段成功的桩：应用 8 行、变更 12 条（由 fetchChanges 内部合并）、告警 2 条、节点 3 台 */
  function okStub() {
    const changesRows = Array.from({ length: 12 }, (_, i) => ({
      Id: i + 1,
      DeployId: 11,
      Action: 'install',
      Success: true,
      CreateUserId: 7,
      CreateTime: `2026-10-30T${String(8 + i).padStart(2, '0')}:00:00`,
    }));
    return stubReq({
      '/Monitors/AlarmRecord': [{ Id: 1, Name: '告警甲', Category: '应用下线', Action: 'app-1', Duration: 60, StartTime: '2026-10-30T09:00:00' }],
      '/Nodes/Node': [{ ID: 1, Name: 'node-a' }],
      '/Nodes/NodeOnline': [{ NodeID: 1, CpuRate: 10 }],
      '/Registry/App': APP_ROWS(8).map((a) => ({ id: a.id, name: a.name, displayName: a.displayName, category: a.category, version: a.version, enable: a.enable })),
      // 每个 app-N 两个在线实例行 → 8 应用 × 2 = instanceTotal 16（fetchAppList 按 appName 计数）
      '/Registry/AppOnline': Array.from({ length: 16 }, (_, i) => ({ appName: `app-${(i % 8) + 1}` })),
      '/Registry/AppService': [],
      '/Monitors/AppMinuteStat': [],
      // fetchChanges 的六路
      '/Deployment/AppDeploy': [{ Id: 11, AppId: 101, AppName: 'OrderService' }],
      '/Configs/AppConfig': [{ Id: 21, AppId: 101, Name: 'OrderService' }],
      '/Deployment/AppPipeline': [{ Id: 301, DeployId: 11 }],
      '/Deployment/AppDeployHistory': changesRows,
      '/Deployment/AppPipelineRun': [],
      '/Configs/ConfigHistory': [],
    });
  }

  it('全段成功：告警/舰队/应用/变更加载，errors 全空', async () => {
    const { req } = okStub();
    const r: NowResult = await fetchNow(req, { nowMs: Date.parse('2026-10-30T10:00:00') });
    expect(r.errors).toEqual({});
    expect(r.data.incidents).toHaveLength(1);
    expect(r.data.incidents[0].severity).toBe('danger');
    expect(r.data.totalNodes).toBe(1);
    expect(r.data.onlineNodes).toBe(1);
    expect(r.data.apps).toHaveLength(6); // 8 行截断为 6
    expect(r.data.changes).toHaveLength(5); // 12 条截断为 5
    expect(r.data.instanceTotal).toBe(16); // 8 应用 × 每应用 2 实例
  });

  it('projectId 非空：告警与节点请求显式带 params.projectId；变更不重复带（fetchChanges 内部处理）', async () => {
    const { req, calls } = okStub();
    await fetchNow(req, { projectId: 7 });
    const alarmCall = calls.find((c) => String(c.url).includes('/Monitors/AlarmRecord'));
    const nodeCall = calls.find((c) => String(c.url).includes('/Nodes/Node'));
    expect(alarmCall?.params).toMatchObject({ status: 1, projectId: 7 });
    expect(nodeCall?.params).toMatchObject({ projectId: 7 });
  });

  it('projectId 为空：请求不带 projectId 参数', async () => {
    const { req, calls } = okStub();
    await fetchNow(req);
    const alarmCall = calls.find((c) => String(c.url).includes('/Monitors/AlarmRecord'));
    expect(alarmCall?.params ?? {}).not.toHaveProperty('projectId');
  });

  it('告警请求失败：errors.alarms 可见，其余段落照常渲染（不整页打空、不静默）', async () => {
    const { req } = okStub();
    // 重包一层：只让告警失败
    const throwing: Req = async (opt) => {
      if (String(opt.url).includes('/Monitors/AlarmRecord')) throw new Error('boom');
      return req(opt);
    };
    const r = await fetchNow(throwing);
    expect(r.errors.alarms).toBe('boom');
    expect(r.data.incidents).toEqual([]);
    expect(r.data.apps.length).toBeGreaterThan(0);
    expect(r.data.changes.length).toBeGreaterThan(0);
    expect(r.data.totalNodes).toBeGreaterThan(0);
  });

  it('应用请求失败：errors.apps 可见，应用为空但告警正常', async () => {
    const { req } = okStub();
    const throwing: Req = async (opt) => {
      if (String(opt.url).includes('/Registry/App')) throw new Error('app down');
      return req(opt);
    };
    const r = await fetchNow(throwing);
    expect(r.errors.apps).toBe('app down');
    expect(r.data.apps).toEqual([]);
    expect(r.data.incidents.length).toBeGreaterThan(0);
  });
});
