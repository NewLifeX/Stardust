import { describe, expect, it, vi } from 'vitest';
import { nextTick, reactive } from 'vue';
import { useContextReload } from './useContextReload';

function makeCtx(over: Partial<{ ready: boolean; projectId: number | null; environment: string | null }> = {}) {
  return reactive({ ready: false, projectId: null as number | null, environment: null as string | null, ...over });
}

describe('useContextReload', () => {
  it('未就绪时不取数，就绪后取一次', async () => {
    const ctx = makeCtx();
    const load = vi.fn();
    useContextReload(load, ctx);
    expect(load).not.toHaveBeenCalled();
    ctx.ready = true;
    await nextTick();
    expect(load).toHaveBeenCalledTimes(1);
  });

  it('已就绪时立即取一次', () => {
    const load = vi.fn();
    useContextReload(load, makeCtx({ ready: true, projectId: 3 }));
    expect(load).toHaveBeenCalledTimes(1);
  });

  it('切换项目后重取，切到全部项目（null）也重取', async () => {
    const ctx = makeCtx({ ready: true, projectId: 3 });
    const load = vi.fn();
    useContextReload(load, ctx);
    ctx.projectId = 4;
    await nextTick();
    ctx.projectId = null;
    await nextTick();
    expect(load).toHaveBeenCalledTimes(3);
  });

  it('切换环境后重取', async () => {
    const ctx = makeCtx({ ready: true });
    const load = vi.fn();
    useContextReload(load, ctx);
    ctx.environment = 'prod';
    await nextTick();
    expect(load).toHaveBeenCalledTimes(2);
  });

  it('上下文没变时不重复取数', async () => {
    const ctx = makeCtx({ ready: true, projectId: 3 });
    const load = vi.fn();
    useContextReload(load, ctx);
    ctx.projectId = 3;
    await nextTick();
    expect(load).toHaveBeenCalledTimes(1);
  });

  it('快速切换时，旧请求的 isStale 为 true，最新请求为 false', async () => {
    const ctx = makeCtx({ ready: true, projectId: 3 });
    const stale: Array<() => boolean> = [];
    useContextReload((isStale) => {
      stale.push(isStale);
    }, ctx);
    ctx.projectId = 4;
    await nextTick();
    expect(stale).toHaveLength(2);
    expect(stale[0]()).toBe(true);
    expect(stale[1]()).toBe(false);
  });

  it('reload 手动重取，未就绪时不取', () => {
    const ctx = makeCtx();
    const load = vi.fn();
    const { reload } = useContextReload(load, ctx);
    reload();
    expect(load).not.toHaveBeenCalled();
    ctx.ready = true;
    reload();
    expect(load).toHaveBeenCalledTimes(1);
  });
});