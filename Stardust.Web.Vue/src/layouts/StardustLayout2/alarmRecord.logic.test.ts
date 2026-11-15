// 报警记录纯逻辑单测（CARD-MON-03）。
// 覆盖：toLocalInput/normDateTime 时间形态换算、alarmRecordFormFromRow 行→表单（status 映射/
//   datetime-local 初值）、validateAlarmRecordForm Name 必填、alarmRecordBody 形状（主键不入 body、
//   trim、时间归一）、saveAlarmRecord PUT /Monitors/AlarmRecord/{id}、removeAlarmRecord DELETE、
//   alarmStatusLabel 三态文案、buildAlarmCsv（表头逐字、组名解析/兜底、BOM、引号转义）。
import { describe, it, expect } from 'vitest';
import {
  alarmRecordBody,
  alarmRecordFormFromRow,
  alarmStatusLabel,
  ALARM_CSV_HEADERS,
  buildAlarmCsv,
  normDateTime,
  removeAlarmRecord,
  saveAlarmRecord,
  toLocalInput,
  validateAlarmRecordForm,
  type AlarmRecordForm,
} from './alarmRecord.logic';
import type { AlarmRow } from './alarms.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 构造一条完整告警行（AlarmRow 全字段，可选字段按需覆盖） */
function makeRow(extra: Partial<AlarmRow> = {}): AlarmRow {
  return {
    id: '11',
    title: '应用下线告警',
    category: '应用下线',
    action: 'OrderService',
    appName: 'OrderService',
    level: 'critical',
    status: 'active',
    groupId: '2',
    startTime: '2026-11-07 10:00',
    startMs: new Date(2026, 10, 7, 10, 0, 0).getTime(),
    durationText: '5 分钟',
    content: '节点心跳超时',
    error: '',
    times: 3,
    ...extra,
  };
}

function captureReq(): { req: Req; calls: Array<{ url: string; method: string; data?: unknown }> } {
  const calls: Array<{ url: string; method: string; data?: unknown }> = [];
  const req: Req = async (opt) => {
    calls.push({
      url: String(opt.url ?? ''),
      method: String(opt.method ?? ''),
      data: (opt as ReqOptions & { data?: unknown }).data,
    });
    return { data: [] };
  };
  return { req, calls };
}

describe('toLocalInput / normDateTime · 时间形态换算', () => {
  it('ms → datetime-local（本地时区 YYYY-MM-DDTHH:mm）', () => {
    const ms = new Date(2026, 10, 7, 10, 30, 0).getTime();
    expect(toLocalInput(ms)).toBe('2026-11-07T10:30');
  });
  it('0/负数/非法 → 空串（未设时间）', () => {
    expect(toLocalInput(0)).toBe('');
    expect(toLocalInput(-5)).toBe('');
    expect(toLocalInput(Number.NaN)).toBe('');
  });
  it('datetime-local 无秒 → 补 :00；带秒原样；空串原样', () => {
    expect(normDateTime('2026-11-07T10:30')).toBe('2026-11-07 10:30:00');
    expect(normDateTime('2026-11-07T10:30:45')).toBe('2026-11-07 10:30:45');
    expect(normDateTime('')).toBe('');
    expect(normDateTime('  ')).toBe('');
  });
});

describe('alarmRecordFormFromRow · 行 → 编辑表单初值', () => {
  it('记录行（active）：status=1、时间转 datetime-local、durationSec/times 透传', () => {
    const form = alarmRecordFormFromRow(
      makeRow({ endTimeMs: new Date(2026, 10, 7, 10, 5, 0).getTime(), durationSec: 300, times: 3 }),
    );
    expect(form).toEqual({
      id: '11',
      name: '应用下线告警',
      groupId: '2',
      category: '应用下线',
      action: 'OrderService',
      status: 1,
      startTime: '2026-11-07T10:00',
      endTime: '2026-11-07T10:05',
      durationSec: 300,
      times: 3,
      content: '节点心跳超时',
    } satisfies AlarmRecordForm);
  });
  it('recovered 行：status=2；endTime/durationSec 缺省 → 空串/0', () => {
    const form = alarmRecordFormFromRow(makeRow({ status: 'recovered' }));
    expect(form.status).toBe(2);
    expect(form.endTime).toBe('');
    expect(form.durationSec).toBe(0);
  });
  it('历史行（resolved）同映射 status=1（入口已挡，纯逻辑不特判）', () => {
    expect(alarmRecordFormFromRow(makeRow({ status: 'resolved' })).status).toBe(1);
  });
});

describe('validateAlarmRecordForm · Name 必填（后端 ArgumentNullException 同语义）', () => {
  const base: AlarmRecordForm = {
    id: '1', name: 'g', groupId: '', category: '', action: '', status: 1,
    startTime: '', endTime: '', durationSec: 0, times: 0, content: '',
  };
  it('空/纯空格 → 「名称不能为空」；有值 → 空串', () => {
    expect(validateAlarmRecordForm({ ...base, name: '' })).toBe('名称不能为空');
    expect(validateAlarmRecordForm({ ...base, name: '   ' })).toBe('名称不能为空');
    expect(validateAlarmRecordForm({ ...base, name: '应用下线告警' })).toBe('');
  });
});

describe('alarmRecordBody / saveAlarmRecord / removeAlarmRecord · 写路由与 body', () => {
  const form: AlarmRecordForm = {
    id: '7', name: ' 应用下线告警 ', groupId: '2', category: ' 应用下线 ', action: ' OrderService ',
    status: 2, startTime: '2026-11-07T10:00', endTime: '2026-11-07T10:05',
    durationSec: 300, times: 3, content: '节点心跳超时',
  };

  it('body：camelCase、name/category/action trim、时间归一 HH:mm:ss、不含 id', () => {
    const body = alarmRecordBody(form);
    expect(body).toEqual({
      name: '应用下线告警',
      groupId: '2',
      category: '应用下线',
      action: 'OrderService',
      status: 2,
      startTime: '2026-11-07 10:00:00',
      endTime: '2026-11-07 10:05:00',
      duration: 300,
      times: 3,
      content: '节点心跳超时',
    });
    expect('id' in body).toBe(false);
  });
  it('body：无组（groupId=\'\'）→ 落 0 对齐后端 GroupId 列 int（0=无组，同改前 wire 行为）', () => {
    expect(alarmRecordBody({ ...form, groupId: '' }).groupId).toBe(0);
  });
  it('保存 → PUT /Monitors/AlarmRecord/{id}，body 同形（主键在路径）', async () => {
    const { req, calls } = captureReq();
    await saveAlarmRecord(req, form);
    expect(calls[0].method).toBe('PUT');
    expect(calls[0].url).toBe('/Monitors/AlarmRecord/7');
    expect(calls[0].data).toEqual(alarmRecordBody(form));
  });
  it('删除 → DELETE /Monitors/AlarmRecord/{id}', async () => {
    const { req, calls } = captureReq();
    await removeAlarmRecord(req, '9');
    expect(calls[0]).toMatchObject({ method: 'DELETE', url: '/Monitors/AlarmRecord/9' });
  });
  it('雪花 id 原串透传（走查缺陷②）：PUT/DELETE 路径不丢精度（>2^53 不经 Number）', async () => {
    const { req, calls } = captureReq();
    const big = '7524741207769665536';
    await saveAlarmRecord(req, { ...form, id: big });
    expect(calls[0].method).toBe('PUT');
    expect(calls[0].url).toBe(`/Monitors/AlarmRecord/${big}`);
    await removeAlarmRecord(req, big);
    expect(calls[1].method).toBe('DELETE');
    expect(calls[1].url).toBe(`/Monitors/AlarmRecord/${big}`);
  });
});

describe('alarmStatusLabel · 状态三态文案（与 Tag 逐字一致）', () => {
  it('active=未处理 / recovered=已恢复 / resolved=已处理', () => {
    expect(alarmStatusLabel('active')).toBe('未处理');
    expect(alarmStatusLabel('recovered')).toBe('已恢复');
    expect(alarmStatusLabel('resolved')).toBe('已处理');
  });
});

describe('buildAlarmCsv · 导出（C17）', () => {
  const resolve = (groupId: string): string =>
    groupId === '2' ? '节点巡检' : groupId === '1' ? '值班告警' : `#${groupId}`;

  it('表头逐字（7 列 + 告警组），首字符 \\uFEFF BOM', () => {
    const csv = buildAlarmCsv([], resolve);
    expect(csv.charCodeAt(0)).toBe(0xfeff);
    expect(csv.slice(1)).toBe(ALARM_CSV_HEADERS.map((h) => `"${h}"`).join(','));
  });
  it('行单元格：级别/标题/对象/状态/时长/次数/开始时间/组名（解析到组名）', () => {
    const csv = buildAlarmCsv([makeRow()], resolve);
    const lines = csv.split('\r\n');
    expect(lines[1]).toBe(
      '"Critical","应用下线告警","OrderService","未处理","5 分钟","3","2026-11-07 10:00","节点巡检"',
    );
  });
  it('组名取不到 → 兜底 GroupId；无组（groupId=\'\'）→ 空', () => {
    const csv = buildAlarmCsv(
      [makeRow({ groupId: '9' }), makeRow({ groupId: '', status: 'recovered' })],
      resolve,
    );
    const lines = csv.split('\r\n');
    expect(lines[1]).toContain(',"#9"');
    expect(lines[2]).toContain(',"已恢复"');
    expect(lines[2].endsWith(',""')).toBe(true);
  });
  it('雪花组 Id 原串入导出兜底（走查缺陷② C18 导出同源锚点）：不经 Number 不丢精度', () => {
    const big = '7524741207769665536';
    const csv = buildAlarmCsv([makeRow({ groupId: big })], resolve);
    expect(csv).toContain(`,"#${big}"`);
  });
  it('单元格含逗号/引号 → 全量引号包裹 + 引号翻倍（csvCell）', () => {
    const csv = buildAlarmCsv([makeRow({ title: '报"警严重', appName: 'a,b' })], resolve);
    expect(csv).toContain('"报""警严重"');
    expect(csv).toContain('"a,b"');
  });
  it('多行以 \\r\\n 分隔', () => {
    const csv = buildAlarmCsv([makeRow({ id: '1' }), makeRow({ id: '2' })], resolve);
    expect(csv.charCodeAt(0)).toBe(0xfeff);
    expect(csv.split('\r\n')).toHaveLength(3);
  });
});
