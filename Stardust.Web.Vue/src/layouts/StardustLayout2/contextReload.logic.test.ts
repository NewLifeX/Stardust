import { describe, expect, it } from 'vitest';
import { canLoad, contextKey, createRequestGuard, type ContextLike } from './contextReload.logic';

describe('contextKey', () => {
  it('未就绪且无项目、无环境时 key 稳定', () => {
    const ctx: ContextLike = { ready: false, projectId: null, environment: null };
    expect(contextKey(ctx)).toBe('0||');
  });

  it('就绪、有项目和环境时拼出三段', () => {
    const ctx: ContextLike = { ready: true, projectId: 3, environment: 'prod' };
    expect(contextKey(ctx)).toBe('1|3|prod');
  });

  it('就绪状态变化会改变 key', () => {
    const before = contextKey({ ready: false, projectId: 3, environment: null });
    const after = contextKey({ ready: true, projectId: 3, environment: null });
    expect(after).not.toBe(before);
  });

  it('切换项目会改变 key，切到「全部项目」（null）也会', () => {
    const a = contextKey({ ready: true, projectId: 3, environment: null });
    const b = contextKey({ ready: true, projectId: 4, environment: null });
    const all = contextKey({ ready: true, projectId: null, environment: null });
    expect(b).not.toBe(a);
    expect(all).not.toBe(a);
  });

  it('切换环境会改变 key', () => {
    const a = contextKey({ ready: true, projectId: 3, environment: 'prod' });
    const b = contextKey({ ready: true, projectId: 3, environment: 'test' });
    expect(b).not.toBe(a);
  });

  it('上下文没变时 key 相同', () => {
    const a = contextKey({ ready: true, projectId: 3, environment: 'prod' });
    const b = contextKey({ ready: true, projectId: 3, environment: 'prod' });
    expect(b).toBe(a);
  });
});

describe('canLoad', () => {
  it('未就绪不取数', () => {
    expect(canLoad({ ready: false, projectId: 3, environment: null })).toBe(false);
  });

  it('就绪后取数，即使没选项目（全部项目）', () => {
    expect(canLoad({ ready: true, projectId: null, environment: null })).toBe(true);
  });
});

describe('createRequestGuard', () => {
  it('只有最新一次请求的令牌有效', () => {
    const guard = createRequestGuard();
    const first = guard.begin();
    const second = guard.begin();
    expect(guard.isLatest(first)).toBe(false);
    expect(guard.isLatest(second)).toBe(true);
  });

  it('没有新请求时，当前令牌一直有效', () => {
    const guard = createRequestGuard();
    const token = guard.begin();
    expect(guard.isLatest(token)).toBe(true);
    expect(guard.isLatest(token)).toBe(true);
  });

  it('两个 guard 互不影响', () => {
    const a = createRequestGuard();
    const b = createRequestGuard();
    const ta = a.begin();
    b.begin();
    b.begin();
    expect(a.isLatest(ta)).toBe(true);
  });
});