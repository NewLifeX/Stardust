// 应用工作台页纯逻辑单测（Vitest）
import { describe, it, expect } from 'vitest';
import { findAppById, resolveWorkbenchView, statusTone } from './appWorkbench.logic';
import type { AppStatus } from '../../../apps/stardust/src/views/Stardust/appsModel';

describe('findAppById · 按 Id 找应用', () => {
  const rows = [
    { id: 1, name: 'A' },
    { id: 2, name: 'B' },
  ];

  it('找到返回该行', () => {
    expect(findAppById(rows, 2)?.name).toBe('B');
  });

  it('找不到返回 null', () => {
    expect(findAppById(rows, 99)).toBeNull();
  });

  it('id 为 null 返回 null', () => {
    expect(findAppById(rows, null)).toBeNull();
  });

  it('行没有 id 时不会误匹配', () => {
    const noId: { id?: number; name: string }[] = [{ name: 'X' }];
    expect(findAppById(noId, 1)).toBeNull();
  });

  it('后端把 id 当字符串返回也能匹配', () => {
    expect(findAppById([{ id: '7' as unknown as number, name: 'S' }], 7)?.name).toBe('S');
  });

  it('空列表返回 null', () => {
    expect(findAppById([], 1)).toBeNull();
  });
});

describe('resolveWorkbenchView · 页面视图判定', () => {
  it('id 非法时不管加载状态都是 invalid', () => {
    expect(resolveWorkbenchView({ id: null, load: 'loading', found: false })).toBe('invalid');
    expect(resolveWorkbenchView({ id: null, load: 'ok', found: false })).toBe('invalid');
    expect(resolveWorkbenchView({ id: null, load: 'failed', found: false })).toBe('invalid');
  });

  it('加载中', () => {
    expect(resolveWorkbenchView({ id: 1, load: 'loading', found: false })).toBe('loading');
  });

  it('加载失败，与「找不到」区分开', () => {
    expect(resolveWorkbenchView({ id: 1, load: 'failed', found: false })).toBe('failed');
  });

  it('加载成功但没有该应用是 notfound', () => {
    expect(resolveWorkbenchView({ id: 1, load: 'ok', found: false })).toBe('notfound');
  });

  it('加载成功且找到是 ok', () => {
    expect(resolveWorkbenchView({ id: 1, load: 'ok', found: true })).toBe('ok');
  });
});

describe('statusTone · 状态 → Tag 语气', () => {
  it.each<[AppStatus, string]>([
    ['running', 'ok'],
    ['error', 'err'],
    ['publishFailed', 'err'],
    ['deploying', 'warn'],
    ['maintenance', 'warn'],
    ['offline', 'default'],
    ['unknown', 'default'],
  ])('%s → %s', (status, tone) => {
    expect(statusTone(status)).toBe(tone);
  });
});