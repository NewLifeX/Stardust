// 告警中心纯逻辑单测（Vitest）。
// 覆盖：级别派生、Duration 格式化、toAlarmRow 归一、toAlarmRows 合并排序（当前在前历史在后）、
// filterAlarms 三维过滤、fetchAlarms 端到端成功与失败。
import { describe, it, expect } from 'vitest';
import {
  alarmFilterParams,
  fetchAlarmGroupOptions,
  fetchAlarms,
  filterAlarms,
  fmtDuration,
  parseAlarmsQuery,
  severityOf,
  toAlarmRow,
  toAlarmRows,
  type AlarmRow,
} from './alarms.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 桩请求：按 url 返回预设 data；throwOn 时全部抛错 */
function stubReq(map: Record<string, unknown[]>, throwOn = false): Req {
  return async (opt) => {
    if (throwOn) throw new Error('stub 500');
    const url = String(opt.url ?? '');
    if (url in map) return { data: map[url] };
    return { data: [] };
  };
}

describe('severityOf · 级别派生（实体无 severity 字段，从 Category/Name 语义归一）', () => {
  it('含「下线/停止/失败」→ critical', () => {
    expect(severityOf('节点下线')).toBe('critical');
    expect(severityOf('应用停止', 'OrderService 已停止')).toBe('critical');
    expect(severityOf('发布失败', 'deploy failed')).toBe('critical');
  });
  it('含「高/慢/超」→ warning（且不含下线/停止/失败）', () => {
    expect(severityOf('错误数过高')).toBe('warning');
    expect(severityOf('', 'OrderService 响应慢')).toBe('warning');
    expect(severityOf('CPU 超限')).toBe('warning');
  });
  it('其余 → info', () => {
    expect(severityOf('自定义告警', 'Disk warning')).toBe('info');
    expect(severityOf('', '')).toBe('info');
  });
});

describe('fmtDuration · Duration 秒 → 时长文案', () => {
  it('<=0 为 —', () => {
    expect(fmtDuration(0)).toBe('—');
    expect(fmtDuration(-5)).toBe('—');
  });
  it('秒/分钟/小时/天分级', () => {
    expect(fmtDuration(30)).toBe('30 秒');
    expect(fmtDuration(300)).toBe('5 分钟');
    expect(fmtDuration(3660)).toBe('1 小时 1 分');
    expect(fmtDuration(90060)).toBe('1 天 1 小时');
  });
});

describe('toAlarmRow · 单条归一（防御性兜底）', () => {
  it('字段齐全：title/category/action/appName/level/status/content/times', () => {
    const row = toAlarmRow(
      {
        Id: 11, Name: 'OrderService 错误率过高', Category: '错误数过高', Action: 'OrderService',
        Duration: 300, StartTime: '2026-10-30 09:00:00', Times: 3,
        Content: '错误率 12%',
      },
      'active',
    );
    expect(row.id).toBe('11');
    expect(row.title).toBe('OrderService 错误率过高');
    expect(row.category).toBe('错误数过高');
    expect(row.action).toBe('OrderService');
    expect(row.appName).toBe('OrderService');
    expect(row.level).toBe('warning');
    expect(row.status).toBe('active');
    expect(row.durationText).toBe('5 分钟');
    expect(row.startTime).toContain('2026-10-30 09:00');
    expect(row.content).toBe('错误率 12%');
    expect(row.times).toBe(3);
  });
  it('缺 Name → 兜底「未命名告警」；缺 Action → appName 为 —；StartTime 缺失兜底 UpdateTime', () => {
    const row = toAlarmRow(
      { Id: 9, Category: '节点下线', UpdateTime: '2026-10-30 10:00:00', Duration: 0 },
      'resolved',
    );
    expect(row.title).toBe('未命名告警');
    expect(row.appName).toBe('—');
    expect(row.level).toBe('critical');
    expect(row.status).toBe('resolved');
    expect(row.durationText).toBe('—');
    expect(row.startTime).toContain('2026-10-30 10:00');
  });
  it('groupId/error 映射（CARD-MON-02）：GroupId/Error 入行；缺省 groupId=\'\'（空串哨兵）、error=空串', () => {
    const withAll = toAlarmRow(
      { Id: 7, Name: '投递失败', Category: '投递失败', GroupId: '5', Error: 'webhook 500', Content: 'x' },
      'resolved',
    );
    expect(withAll.groupId).toBe('5');
    expect(withAll.error).toBe('webhook 500');
    const without = toAlarmRow({ Id: 8, Name: '无组告警', Category: '自定义' }, 'active');
    expect(without.groupId).toBe('');
    expect(without.error).toBe('');
  });
  it('Status 自描述（CARD-MON-03）：Status=2 → recovered；Status=1/缺省落调用方默认；endTimeMs/durationSec 入行', () => {
    const rec = toAlarmRow(
      { Id: 21, Name: 'Svc 已恢复', Category: '自定义', Status: 2, EndTime: '2026-10-30 12:00:00', Duration: 600 },
      'active',
    );
    expect(rec.status).toBe('recovered');
    expect(rec.endTimeMs).toBe(Date.parse('2026-10-30T12:00:00'));
    expect(rec.durationSec).toBe(600);
    const alarming = toAlarmRow({ Id: 22, Name: 'b', Category: 'x', Status: 1 }, 'active');
    expect(alarming.status).toBe('active');
    // 历史行无 Status 字段 → 调用方默认 resolved（AlarmHistory 实体没有 Status）
    const hist = toAlarmRow({ Id: 23, Name: 'c', Category: 'x' }, 'resolved');
    expect(hist.status).toBe('resolved');
  });
  it('雪花 Id 字符串透传（走查缺陷②）：>2^53 不经 Number 不丢精度；数字/缺失形态归一', () => {
    const snow = toAlarmRow(
      { Id: '7524741207769665536', Name: 'svc 下线', Category: '节点下线', Status: 2 },
      'active',
    );
    expect(snow.id).toBe('7524741207769665536');
    // 后端已把雪花 Id 字符串化（"id":"7524…"）；数字形态小 id 也归一为字符串，路径拼接同形
    expect(toAlarmRow({ Id: 11, Name: 'x', Category: 'y' }, 'active').id).toBe('11');
    expect(toAlarmRow({ Name: 'x', Category: 'y' }, 'active').id).toBe('');
  });
});

describe('toAlarmRows · 当前 + 历史合并排序', () => {
  it('当前告警在前（active），历史在后（resolved）；组内按开始时间倒序', () => {
    const active = [
      { Id: 2, Name: 'b', Category: '错误数过高', Action: 'app-b', StartTime: '2026-10-30 09:00:00' },
      { Id: 1, Name: 'a', Category: '节点下线', Action: 'node-a', StartTime: '2026-10-30 11:00:00' },
    ];
    const history = [
      { Id: 100, Name: 'old', Category: '错误数过高', Action: 'app-old', StartTime: '2026-10-29 08:00:00' },
    ];
    const rows = toAlarmRows(active, history);
    expect(rows.map((r) => r.status)).toEqual(['active', 'active', 'resolved']);
    // active 组内：11:00 的 a(Id=1) 在 09:00 的 b(Id=2) 之前
    expect(rows[0].id).toBe('1');
    expect(rows[1].id).toBe('2');
    expect(rows[2].id).toBe('100');
  });
  it('两路都为空 → 空数组', () => {
    expect(toAlarmRows([], [])).toEqual([]);
  });
  it('同刻并列按 Id 数字语义倒序（走查缺陷②）：雪花等长串按字典序、不同长度按位数，不按 Number 损毁', () => {
    const active = [
      { Id: '99', Name: 'a', Category: 'x', Action: 'a', StartTime: '2026-10-30 09:00:00' },
      { Id: '100', Name: 'b', Category: 'x', Action: 'b', StartTime: '2026-10-30 09:00:00' },
      { Id: '7524741207769665536', Name: 'c', Category: 'x', Action: 'c', StartTime: '2026-10-30 09:00:00' },
      { Id: '7524741207769666000', Name: 'd', Category: 'x', Action: 'd', StartTime: '2026-10-30 09:00:00' },
    ];
    const rows = toAlarmRows(active, []);
    expect(rows.map((r) => r.id)).toEqual([
      '7524741207769666000',
      '7524741207769665536',
      '100',
      '99',
    ]);
  });
});

describe('filterAlarms · level / status / appQ 三维过滤', () => {
  const rows: AlarmRow[] = [
    { id: '1', title: '节点下线', category: '节点下线', action: 'node-a', appName: 'node-a', level: 'critical', status: 'active', groupId: '', startTime: '', startMs: 0, durationText: '—', content: '', error: '', times: 1 },
    { id: '2', title: '错误率过高', category: '错误数过高', action: 'OrderService', appName: 'OrderService', level: 'warning', status: 'active', groupId: '', startTime: '', startMs: 0, durationText: '—', content: '', error: '', times: 2 },
    { id: '3', title: '慢查询', category: '慢查询', action: 'PayService', appName: 'PayService', level: 'warning', status: 'resolved', groupId: '', startTime: '', startMs: 0, durationText: '—', content: '', error: '', times: 1 },
    { id: '4', title: '磁盘提示', category: '自定义', action: 'node-b', appName: 'node-b', level: 'info', status: 'resolved', groupId: '', startTime: '', startMs: 0, durationText: '—', content: '', error: '', times: 0 },
  ];

  it('level=critical 只留 critical', () => {
    expect(filterAlarms(rows, 'critical', '', '')).toHaveLength(1);
  });
  it('status=active 只留报警中；status=resolved 只留已恢复；all/空不过滤', () => {
    expect(filterAlarms(rows, '', 'active', '')).toHaveLength(2);
    expect(filterAlarms(rows, '', 'resolved', '')).toHaveLength(2);
    expect(filterAlarms(rows, '', 'all', '')).toHaveLength(4);
  });
  it('appQ 不区分大小写匹配 appName/title/category', () => {
    expect(filterAlarms(rows, '', '', 'orderservice')).toHaveLength(1);
    expect(filterAlarms(rows, '', '', 'node')).toHaveLength(2); // node-a + node-b
  });
  it('三维组合', () => {
    const out = filterAlarms(rows, 'warning', 'resolved', 'pay');
    expect(out).toHaveLength(1);
    expect(out[0].id).toBe('3');
  });
  it('status=recovered 不做客户端状态过滤（服务端视图标记，CARD-MON-03）；级别/关键字仍叠加', () => {
    expect(filterAlarms(rows, '', 'recovered', '')).toHaveLength(4);
    expect(filterAlarms(rows, 'warning', 'recovered', '')).toHaveLength(2);
  });
  it('空条件不过滤', () => {
    expect(filterAlarms(rows, '', '', '')).toHaveLength(4);
  });
});

describe('fetchAlarms · 端到端取数（request 注入）', () => {
  it('两路都成功：当前告警 status=active、历史 status=resolved，error 为空', async () => {
    const req = stubReq({
      '/Monitors/AlarmRecord': [
        { Id: 11, Name: '节点下线', Category: '节点下线', Action: 'node-a', Duration: 600, StartTime: '2026-10-30 08:50:00', Times: 1 },
      ],
      '/Monitors/AlarmHistory': [
        { Id: 55, Name: '旧告警', Category: '错误数过高', Action: 'OldApp', Duration: 120, StartTime: '2026-10-29 08:00:00', Times: 2 },
      ],
    });
    const r = await fetchAlarms(req);
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(2);
    expect(r.rows[0].status).toBe('active');
    expect(r.rows[0].level).toBe('critical');
    expect(r.rows[1].status).toBe('resolved');
    expect(r.rows[1].level).toBe('warning');
  });

  it('请求带 projectId（对 AlarmRecord 无过滤效果，但规范上透传）', async () => {
    let captured: Record<string, unknown> | undefined;
    const req: Req = async (opt) => {
      captured = opt.params ?? {};
      return { data: [] };
    };
    await fetchAlarms(req, 42);
    expect(captured).toBeDefined();
    expect(captured?.projectId).toBe(42);
  });

  it('选中组/时间时两路请求都携带 groupId/dtStart/dtEnd（C11/C12，CARD-MON-02）', async () => {
    const seen: Record<string, unknown>[] = [];
    const req: Req = async (opt) => {
      seen.push({ ...(opt.params ?? {}) });
      return { data: [] };
    };
    const r = await fetchAlarms(req, 1, { groupId: '5', dtStart: '2026-11-01', dtEnd: '2026-11-07' });
    expect(r.error).toBe('');
    expect(seen).toHaveLength(2);
    for (const params of seen) {
      expect(params.groupId).toBe('5');
      expect(params.dtStart).toBe('2026-11-01');
      expect(params.dtEnd).toBe('2026-11-07');
    }
    // 记录路仍保留 status=1（报警中）与 PageSize 200，历史路仍 PageSize 200
    expect(seen[0].status).toBe(1);
    expect(seen[0].PageSize).toBe(200);
    expect(seen[1].PageSize).toBe(200);
  });

  it('未选组/时间时请求不带 groupId/dtStart/dtEnd（全量语义，C12）', async () => {
    const seen: Record<string, unknown>[] = [];
    const req: Req = async (opt) => {
      seen.push({ ...(opt.params ?? {}) });
      return { data: [] };
    };
    await fetchAlarms(req);
    expect(seen).toHaveLength(2);
    for (const params of seen) {
      expect(params.groupId).toBeUndefined();
      expect(params.dtStart).toBeUndefined();
      expect(params.dtEnd).toBeUndefined();
    }
  });

  it('recordStatus=2（已恢复视图，CARD-MON-03）：只发记录路且 status=2，不发历史路；行落 recovered', async () => {
    const urls: string[] = [];
    const req: Req = async (opt) => {
      urls.push(String(opt.url ?? ''));
      if (String(opt.url).includes('AlarmRecord')) {
        return { data: [{ Id: 31, Name: '已恢复记录', Category: '自定义', Status: 2, GroupId: '3' }] };
      }
      return { data: [] };
    };
    const r = await fetchAlarms(req, 1, { groupId: '3' }, { recordStatus: 2 });
    expect(r.error).toBe('');
    expect(urls).toEqual(['/Monitors/AlarmRecord']);
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0].status).toBe('recovered');
    expect(r.rows[0].groupId).toBe('3');
  });

  it('任一请求失败：返回 error、rows 为空（不静默空态）', async () => {
    const r = await fetchAlarms(stubReq({}, true));
    expect(r.rows).toEqual([]);
    expect(r.error).not.toBe('');
  });
});

describe('parseAlarmsQuery · /alarms?groupId 深链解析（CARD-MON-02）', () => {
  it('合法 groupId（纯数字串）→ 原样字符串返回；数组形态取首元素', () => {
    expect(parseAlarmsQuery({ groupId: '3' })).toBe('3');
    expect(parseAlarmsQuery({ groupId: 0 })).toBe('0');
    expect(parseAlarmsQuery({ groupId: ['12', '99'] })).toBe('12');
  });
  it('雪花组 Id 原串透传（走查缺陷② C18 深链锚点）：不经 Number 不丢精度', () => {
    expect(parseAlarmsQuery({ groupId: '7524741207769665536' })).toBe('7524741207769665536');
    expect(parseAlarmsQuery({ groupId: '7524741207769666000' })).toBe('7524741207769666000');
  });
  it('无参 / 空 / 非数字 → null=不过滤（与后端 groupId<0 同语义）', () => {
    expect(parseAlarmsQuery({})).toBeNull();
    expect(parseAlarmsQuery({ groupId: null })).toBeNull();
    expect(parseAlarmsQuery({ groupId: '' })).toBeNull();
    expect(parseAlarmsQuery({ groupId: 'abc' })).toBeNull();
    expect(parseAlarmsQuery({ groupId: '-1' })).toBeNull();
    expect(parseAlarmsQuery({ groupId: '3.5' })).toBeNull();
  });
});

describe('fetchAlarmGroupOptions · 组下拉数据源（走查缺陷② C18：组 Id 字符串透传）', () => {
  it('Id 字符串原样（雪花/数字归一字符串）；缺失 Id 的脏行被过滤，name 透传', async () => {
    const req = stubReq({
      '/Monitors/AlarmGroup': [
        { Id: '7524741207769665536', Name: '雪花组' },
        { Id: 11, Name: '值班告警' },
        { Name: '无 Id 脏行' },
      ],
    });
    const r = await fetchAlarmGroupOptions(req, 1);
    expect(r.error).toBe('');
    expect(r.options).toEqual([
      { id: '7524741207769665536', name: '雪花组' },
      { id: '11', name: '值班告警' },
    ]);
  });
  it('请求失败 → options 空、error 可见（不静默）', async () => {
    const r = await fetchAlarmGroupOptions(stubReq({}, true));
    expect(r.options).toEqual([]);
    expect(r.error).not.toBe('');
  });
});

describe('alarmFilterParams · 组/时间过滤参数组装（CARD-MON-02）', () => {
  it('不传 filter 或全空 → 空参数（全量语义，请求不带 dtStart/dtEnd）', () => {
    expect(alarmFilterParams(undefined)).toEqual({});
    expect(alarmFilterParams({})).toEqual({});
    expect(alarmFilterParams({ groupId: null, dtStart: '', dtEnd: '' })).toEqual({});
  });
  it('groupId 纯数字串才携带；空/非数字不携带（后端 <0 = 不过滤）', () => {
    expect(alarmFilterParams({ groupId: '5' })).toEqual({ groupId: '5' });
    expect(alarmFilterParams({ groupId: '-1' })).toEqual({});
    expect(alarmFilterParams({ groupId: '2.5' })).toEqual({});
    expect(alarmFilterParams({ groupId: '' })).toEqual({});
  });
  it('dtStart/dtEnd 非空才携带（各自独立，可只选一端）', () => {
    expect(alarmFilterParams({ dtStart: '2026-11-01', dtEnd: '2026-11-07' })).toEqual({
      dtStart: '2026-11-01',
      dtEnd: '2026-11-07',
    });
    expect(alarmFilterParams({ dtStart: '2026-11-01' })).toEqual({ dtStart: '2026-11-01' });
  });
  it('组+时间叠加', () => {
    expect(alarmFilterParams({ groupId: '3', dtStart: '2026-11-01', dtEnd: '2026-11-07' })).toEqual({
      groupId: '3',
      dtStart: '2026-11-01',
      dtEnd: '2026-11-07',
    });
  });
});
