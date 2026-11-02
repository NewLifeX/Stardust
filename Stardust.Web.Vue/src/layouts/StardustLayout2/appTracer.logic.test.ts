// appTracer.logic 单测（Vitest）——字段映射与行为语义，数据手算
import { describe, it, expect, vi } from 'vitest';
import {
  TRACE_MODES,
  deleteTracer,
  fetchTracers,
  fixTracers,
  saveTracer,
  toTracerEditModel,
  toTracerRow,
  traceModeLabel,
  tracerEmptyState,
  tracerPutBody,
  validateTracerEdit,
} from './appTracer.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

describe('toTracerRow · 行映射（大小写双取）', () => {
  it('camelCase 行', () => {
    const r = toTracerRow({ id: 3, appId: 32, name: 'ioc', displayName: 'IoC容器', category: '中间件', itemCount: 12, days: 5, total: 900, enable: true, mode: 1, period: 60, maxSamples: 10, maxErrors: 10, enableMeter: false, timeout: 5000, updateTime: '2026-11-03 00:00' });
    expect(r.id).toBe(3);
    expect(r.appId).toBe(32);
    expect(r.displayName).toBe('IoC容器');
    expect(r.enable).toBe(true);
    expect(r.enableMeter).toBe(false);
    expect(r.mode).toBe(1);
    expect(r.timeout).toBe(5000);
  });

  it('PascalCase 行（ID 键优先命中）', () => {
    const r = toTracerRow({ ID: 7, AppId: 5, Name: 'antjob', WhiteList: '/Cube/*', VipClients: 'a,b', AlarmThreshold: 3, AlarmErrorRate: 0.1 });
    expect(r.id).toBe(7);
    expect(r.appId).toBe(5);
    expect(r.name).toBe('antjob');
    expect(r.whiteList).toBe('/Cube/*');
    expect(r.vipClients).toBe('a,b');
    expect(r.alarmThreshold).toBe(3);
    expect(r.alarmErrorRate).toBeCloseTo(0.1);
  });

  it('缺字段取 0/空串/null，不抛错', () => {
    const r = toTracerRow({});
    expect(r.id).toBe(0);
    expect(r.name).toBe('');
    expect(r.enable).toBeNull();
    expect(r.enableMeter).toBeNull();
  });
});

describe('fetchTracers', () => {
  it('带 appId+pageSize 请求并映射 data 数组', async () => {
    const calls: Array<Record<string, unknown>> = [];
    const request: Req = (async (cfg: unknown) => {
      calls.push(cfg as Record<string, unknown>);
      return { data: [{ id: 1, name: 'a' }], page: { totalCount: 1 } };
    }) as Req;
    const rows = await fetchTracers(request, 32);
    expect(rows).toHaveLength(1);
    expect(rows[0].name).toBe('a');
    const c = calls[0] as { url: string; method: string; params: Record<string, unknown> };
    expect(c.url).toBe('/Monitors/AppTracer');
    expect(c.method).toBe('GET');
    expect(c.params.appId).toBe(32);
    expect(c.params.pageSize).toBe(50);
  });

  it('响应是裸数组也能映射', async () => {
    const request = (async () => [{ id: 2 }]) as Req;
    const rows = await fetchTracers(request, 1);
    expect(rows[0].id).toBe(2);
  });
});

describe('fixTracers · 批量修正', () => {
  it('Fix 用 GET + keys 逗号串，返回 message', async () => {
    let seen: { url?: string; params?: Record<string, unknown> } = {};
    const request = (async (cfg: { url: string; params: Record<string, unknown> }) => {
      seen = cfg;
      return { code: 0, message: '成功！' };
    }) as Req;
    const msg = await fixTracers(request, [1, 2, 3]);
    expect(msg).toBe('成功！');
    expect(seen.url).toBe('/Monitors/AppTracer/Fix');
    expect(seen.params?.keys).toBe('1,2,3');
  });

  it('FixOldData 走另一个 Action', async () => {
    let url = '';
    const request = (async (cfg: { url: string }) => {
      url = cfg.url;
      return {};
    }) as Req;
    await fixTracers(request, [5], true);
    expect(url).toBe('/Monitors/AppTracer/FixOldData');
  });

  it('空选择不发请求', async () => {
    const request = vi.fn() as unknown as Req;
    expect(await fixTracers(request, [])).toBe('');
    expect(request).not.toHaveBeenCalled();
  });
});

describe('saveTracer / deleteTracer', () => {
  it('PUT 到 /{id} 路径，体含全部编辑字段且不带 id（主键在路径）', async () => {
    let url = '';
    let body: Record<string, unknown> | undefined;
    const request = (async (cfg: { url: string; data?: Record<string, unknown> }) => {
      url = cfg.url;
      body = cfg.data;
      return {};
    }) as Req;
    await saveTracer(request, 9, {
      displayName: '显示名', enable: false, mode: 2, period: 30, maxSamples: 5, maxErrors: 8, enableMeter: true, timeout: 3000, whiteList: '/w/*', excludes: '/e/*', vipClients: 'vip1', webHook: 'http://h', alarmThreshold: 1, alarmErrorRate: 0.5,
    });
    expect(url).toBe('/Monitors/AppTracer/9');
    expect(body?.id).toBeUndefined();
    expect(body?.displayName).toBe('显示名');
    expect(body?.enable).toBe(false);
    expect(body?.mode).toBe(2);
    expect(body?.enableMeter).toBe(true);
  });

  it('DELETE 到 /{id} 路径', async () => {
    let url = '';
    const request = (async (cfg: { url: string }) => {
      url = cfg.url;
      return {};
    }) as Req;
    await deleteTracer(request, 12);
    expect(url).toBe('/Monitors/AppTracer/12');
  });
});

describe('tracerPutBody / toTracerEditModel / validateTracerEdit', () => {
  it('往返：行 → 表单 → 体，可编辑字段一致', () => {
    const row = toTracerRow({ id: 4, displayName: 'x', enable: true, mode: 0, period: 60, maxSamples: 10, maxErrors: 10, enableMeter: true, timeout: 5000, whiteList: '', excludes: '', vipClients: '', webHook: '', alarmThreshold: 0, alarmErrorRate: 0 });
    const m = toTracerEditModel(row);
    expect(validateTracerEdit(m)).toBe('');
    const body = tracerPutBody(m) as Record<string, unknown>;
    expect(body.displayName).toBe('x');
    expect(body.mode).toBe(0);
  });

  it.each([-1, -60])('period=%i 非法被拦', (p) => {
    const m = toTracerEditModel(toTracerRow({ period: p }));
    expect(validateTracerEdit(m)).not.toBe('');
  });

  it('alarmErrorRate=-0.5 非法被拦', () => {
    const m = toTracerEditModel(toTracerRow({ alarmErrorRate: -0.5 }));
    expect(validateTracerEdit(m)).toContain('告警错误率');
  });
});

describe('traceModeLabel / TRACE_MODES', () => {
  it('三种模式文案对齐枚举注释', () => {
    expect(TRACE_MODES).toHaveLength(3);
    expect(traceModeLabel(0)).toBe('跟踪所有项');
    expect(traceModeLabel(1)).toBe('仅新增跟踪项');
    expect(traceModeLabel(2)).toBe('仅跟踪已有项');
  });

  it('未知模式给出兜底文案', () => {
    expect(traceModeLabel(9)).toBe('模式9');
  });
});

describe('tracerEmptyState · 空态引导', () => {
  it('有数据不显示空态', () => {
    const rows = [toTracerRow({ id: 1, name: 'a' })];
    expect(tracerEmptyState(rows, 32, 'ioc')).toBe('');
  });

  it('无数据且 appId 有效：引导文案含应用名', () => {
    const text = tracerEmptyState([], 32, 'IoC容器');
    expect(text).toContain('IoC容器');
    expect(text).toContain('自动创建');
  });

  it('appId 无效：提示参数问题', () => {
    expect(tracerEmptyState([], 0, '')).toContain('参数无效');
  });

  it('应用名缺省时用 #id 兜底', () => {
    expect(tracerEmptyState([], 5, '')).toContain('#5');
  });
});
