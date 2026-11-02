// 告警中心纯逻辑单测（Vitest）。
// 覆盖：级别派生、Duration 格式化、toAlarmRow 归一、toAlarmRows 合并排序（当前在前历史在后）、
// filterAlarms 三维过滤、fetchAlarms 端到端成功与失败。
import { describe, it, expect } from 'vitest';
import {
  fetchAlarms,
  filterAlarms,
  fmtDuration,
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
    expect(row.id).toBe(11);
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
    expect(rows[0].id).toBe(1);
    expect(rows[1].id).toBe(2);
    expect(rows[2].id).toBe(100);
  });
  it('两路都为空 → 空数组', () => {
    expect(toAlarmRows([], [])).toEqual([]);
  });
});

describe('filterAlarms · level / status / appQ 三维过滤', () => {
  const rows: AlarmRow[] = [
    { id: 1, title: '节点下线', category: '节点下线', action: 'node-a', appName: 'node-a', level: 'critical', status: 'active', startTime: '', startMs: 0, durationText: '—', content: '', times: 1 },
    { id: 2, title: '错误率过高', category: '错误数过高', action: 'OrderService', appName: 'OrderService', level: 'warning', status: 'active', startTime: '', startMs: 0, durationText: '—', content: '', times: 2 },
    { id: 3, title: '慢查询', category: '慢查询', action: 'PayService', appName: 'PayService', level: 'warning', status: 'resolved', startTime: '', startMs: 0, durationText: '—', content: '', times: 1 },
    { id: 4, title: '磁盘提示', category: '自定义', action: 'node-b', appName: 'node-b', level: 'info', status: 'resolved', startTime: '', startMs: 0, durationText: '—', content: '', times: 0 },
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
    expect(out[0].id).toBe(3);
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

  it('任一请求失败：返回 error、rows 为空（不静默空态）', async () => {
    const r = await fetchAlarms(stubReq({}, true));
    expect(r.rows).toEqual([]);
    expect(r.error).not.toBe('');
  });
});
