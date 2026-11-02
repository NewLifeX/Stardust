// 手动刷新状态机单测（Vitest）：时钟与 sleep 注入，不依赖真实计时器。
import { describe, it, expect } from 'vitest';
import { useManualRefresh } from './useManualRefresh.ts';
import type { RefreshOutcome } from './ship.logic.ts';

const OK: RefreshOutcome = { loadError: '', versionsFailed: false, nodesFailed: false, historyFailed: false };

/** 每个 it 自己调用，拿到独立的时钟与记录 */
function setup(opts: { loadMs?: number; outcome?: RefreshOutcome; loadThrows?: string } = {}) {
  let t = 1000;
  const slept: number[] = [];
  const notices: Array<{ ok: boolean; text: string }> = [];
  let loadCalls = 0;
  let release: () => void = () => {};
  const gate = new Promise<void>((resolve) => {
    release = resolve;
  });
  const r = useManualRefresh({
    load: async () => {
      loadCalls++;
      if (opts.loadThrows) throw new Error(opts.loadThrows);
      t += opts.loadMs ?? 0;
      await gate;
    },
    outcome: () => opts.outcome ?? OK,
    notify: (ok, text) => notices.push({ ok, text }),
    now: () => t,
    sleep: async (ms) => {
      slept.push(ms);
      t += ms;
    },
  });
  return { r, slept, notices, release, calls: () => loadCalls };
}

describe('useManualRefresh', () => {
  it('进行中 refreshing 为 true，结束后恢复为 false', async () => {
    const s = setup();
    expect(s.r.refreshing.value).toBe(false);
    const p = s.r.refresh();
    expect(s.r.refreshing.value).toBe(true);
    s.release();
    await p;
    expect(s.r.refreshing.value).toBe(false);
  });

  it('进行中再次点击被忽略，只取一次数', async () => {
    const s = setup();
    const p1 = s.r.refresh();
    const p2 = s.r.refresh();
    expect(s.calls()).toBe(1);
    s.release();
    await Promise.all([p1, p2]);
    expect(s.calls()).toBe(1);
  });

  it('请求很快（0ms）时仍补足 300ms 旋转', async () => {
    const s = setup({ loadMs: 0 });
    const p = s.r.refresh();
    s.release();
    await p;
    expect(s.slept).toEqual([300]);
  });

  it('请求用了 120ms，只再补 180ms', async () => {
    const s = setup({ loadMs: 120 });
    const p = s.r.refresh();
    s.release();
    await p;
    expect(s.slept).toEqual([180]);
  });

  it('请求已超过 300ms，不再额外等待', async () => {
    const s = setup({ loadMs: 450 });
    const p = s.r.refresh();
    s.release();
    await p;
    expect(s.slept).toEqual([]);
  });

  it('全部成功：不弹提示', async () => {
    const s = setup();
    const p = s.r.refresh();
    s.release();
    await p;
    expect(s.notices).toEqual([]);
  });

  it('部分区块失败：提示「未完全成功」，且在按钮恢复之后给出', async () => {
    const s = setup({ outcome: { ...OK, nodesFailed: true } });
    const p = s.r.refresh();
    s.release();
    await p;
    expect(s.notices).toHaveLength(1);
    expect(s.notices[0].ok).toBe(false);
    expect(s.notices[0].text).toContain('未完全成功');
    expect(s.r.refreshing.value).toBe(false);
  });

  it('整体加载失败：提示带上失败原因', async () => {
    const s = setup({ outcome: { ...OK, loadError: '部署集加载失败' } });
    const p = s.r.refresh();
    s.release();
    await p;
    expect(s.notices).toEqual([{ ok: false, text: '刷新失败：部署集加载失败' }]);
  });

  it('load 抛错：兜住并提示，按钮恢复，之后可以再次刷新', async () => {
    const s = setup({ loadThrows: '网络中断' });
    await s.r.refresh();
    expect(s.notices).toEqual([{ ok: false, text: '刷新失败：网络中断' }]);
    expect(s.r.refreshing.value).toBe(false);
    await s.r.refresh();
    expect(s.calls()).toBe(2);
  });
});
