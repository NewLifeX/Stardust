// 日志页签纯逻辑单测：级别归一、按应用本地过滤（后端不支持 appId/Level 过滤）、级别筛选、取数兜底。
import { describe, expect, it } from 'vitest';
import {
  fetchAppLogs,
  filterLogsByLevel,
  normalizeLevel,
  todayRange,
  toAppLogRows,
} from './appLog.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

function fakeRequest(handler: (url: string, params: Record<string, unknown>) => unknown): Req {
  return async ({ url, params }) => {
    const data = handler(url, params ?? {});
    return { data };
  };
}

describe('normalizeLevel：日志级别归一（Kind 是 NewLife 日志头里的类型串）', () => {
  it.each([
    ['Info', 'info'],
    ['info', 'info'],
    ['Warn', 'warn'],
    ['Warning', 'warn'],
    ['Error', 'error'],
    ['Fatal', 'error'],
    ['Debug', 'debug'],
    ['', ''],
    ['UnknownX', ''],
  ])('%s → %s', (input, want) => {
    expect(normalizeLevel(input)).toBe(want);
  });
});

describe('toAppLogRows：后端按时间窗返回全部应用日志，前端按 appId 本地过滤', () => {
  it('只保留本应用行，按时间升序（新日志在底部，便于实时滚动）', () => {
    const rows = toAppLogRows(
      [
        { Id: 2, AppId: 32, Time: '2026-10-29 10:00:02.000', Kind: 'Info', Name: 'n2', Message: 'second' },
        { Id: 1, AppId: 32, Time: '2026-10-29 10:00:01.000', Kind: 'Error', Name: 'n1', Message: 'first' },
        { Id: 9, AppId: 99, Time: '2026-10-29 10:00:09.000', Kind: 'Debug', Name: 'n9', Message: 'other app' },
      ],
      32,
    );
    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({ id: 1, level: 'error', message: 'first' });
    expect(rows[1]).toMatchObject({ id: 2, level: 'info', message: 'second' });
  });

  it('camelCase 方言也认', () => {
    const rows = toAppLogRows([{ id: 5, appId: 7, time: 't', kind: 'warn', name: 'n', message: 'm' }], 7);
    expect(rows[0]).toMatchObject({ level: 'warn', message: 'm' });
  });
});

describe('filterLogsByLevel：级别 chip 本地过滤', () => {
  const rows = toAppLogRows(
    [
      { Id: 1, AppId: 1, Kind: 'Error', Message: 'e' },
      { Id: 2, AppId: 1, Kind: 'Warn', Message: 'w' },
      { Id: 3, AppId: 1, Kind: 'Info', Message: 'i' },
      { Id: 4, AppId: 1, Kind: 'Debug', Message: 'd' },
      { Id: 5, AppId: 1, Kind: 'UnknownKind', Message: 'u' },
    ],
    1,
  );

  it('全部返回所有行（含级别识别不出的行）', () => {
    expect(filterLogsByLevel(rows, 'all')).toHaveLength(5);
  });

  it('Error chip 只留 Error；识别不出的行在具体级别 chip 下隐藏', () => {
    expect(filterLogsByLevel(rows, 'error')).toHaveLength(1);
    expect(filterLogsByLevel(rows, 'warn')).toHaveLength(1);
    expect(filterLogsByLevel(rows, 'debug')).toHaveLength(1);
    expect(filterLogsByLevel(rows, 'info')).toHaveLength(1);
  });
});

describe('fetchAppLogs：取数（request 注入）', () => {
  it('带 dtStart/dtEnd 时间窗请求 /Registry/AppClientLog，并按 appId 本地过滤', async () => {
    const req = fakeRequest((url, params) => {
      expect(url).toBe('/Registry/AppClientLog');
      expect(params.dtStart).toBe('2026-10-29');
      expect(params.dtEnd).toBe('2026-10-29 23:59:59');
      return [
        { Id: 1, AppId: 32, Kind: 'Info', Message: 'mine' },
        { Id: 2, AppId: 40, Kind: 'Error', Message: 'not mine' },
      ];
    });
    const rows = await fetchAppLogs(req, 32, { dtStart: '2026-10-29', dtEnd: '2026-10-29 23:59:59' });
    expect(rows).toHaveLength(1);
    expect(rows?.[0].message).toBe('mine');
  });

  it('请求失败返回 null，由组件显示加载失败', async () => {
    const bad: Req = async () => {
      throw new Error('network');
    };
    expect(await fetchAppLogs(bad, 32, todayRange(new Date(2026, 9, 29)))).toBeNull();
  });

  it('appId 无效时不取数，返回空列表', async () => {
    let called = 0;
    const req = fakeRequest(() => {
      called++;
      return [];
    });
    expect(await fetchAppLogs(req, 0, todayRange(new Date(2026, 9, 29)))).toEqual([]);
    expect(called).toBe(0);
  });
});

describe('todayRange：默认查今天', () => {
  it('dtStart 是当天日期，dtEnd 是当天末尾', () => {
    const range = todayRange(new Date(2026, 0, 5));
    expect(range).toEqual({ dtStart: '2026-01-05', dtEnd: '2026-01-05 23:59:59' });
  });
});
