// 应用工作台纯逻辑单测（Vitest）
// 2026-10-29 停点 19：页签 6 变 9，「发布」页签取消，部署相关内容拆成版本、节点、部署历史、流水线四个页签。
import { describe, it, expect } from 'vitest';
import { DEFAULT_TAB, WORKBENCH_TABS, resolveTab, tabLabel, tabSection } from './workbench.logic';

describe('WORKBENCH_TABS · 页签清单', () => {
  it('九个页签：原型的五个在前，部署的四个在后', () => {
    expect(WORKBENCH_TABS.map((t) => `${t.key}:${t.label}`).join('|')).toBe(
      'overview:概览|trace:调用链|logs:日志|config:配置|instances:实例|versions:版本|nodes:节点|history:部署历史|pipeline:流水线',
    );
  });

  it('「发布」页签已取消', () => {
    const keys: string[] = WORKBENCH_TABS.map((t) => t.key);
    expect(keys).not.toContain('ship');
    expect(WORKBENCH_TABS.map((t) => t.label)).not.toContain('发布');
  });

  it('key 与文案都不重复', () => {
    expect(new Set(WORKBENCH_TABS.map((t) => t.key)).size).toBe(WORKBENCH_TABS.length);
    expect(new Set(WORKBENCH_TABS.map((t) => t.label)).size).toBe(WORKBENCH_TABS.length);
  });

  it('默认页签是概览', () => {
    expect(DEFAULT_TAB).toBe('overview');
  });
});

describe('resolveTab · 路由 :tab 解析', () => {
  it.each(WORKBENCH_TABS.map((t) => t.key))('合法页签 %s 原样返回', (key) => {
    expect(resolveTab(key)).toBe(key);
  });

  it('数组取首项', () => {
    expect(resolveTab(['logs', 'versions'])).toBe('logs');
  });

  it.each<unknown>([undefined, null, '', 'unknown', 'ship', 'Ship', 'Versions', 'iotweb', 12, [], ['x']])(
    '%s 回落到概览（旧书签 /app/ship/<id> 也在此列）',
    (raw) => {
      expect(resolveTab(raw)).toBe('overview');
    },
  );
});

describe('tabLabel · 页签文案', () => {
  it('实例页签带实例数', () => {
    expect(tabLabel('instances', 3)).toBe('实例 3');
  });

  it('实例数为 0 也要显示', () => {
    expect(tabLabel('instances', 0)).toBe('实例 0');
  });

  it('实例数未知时只显示「实例」', () => {
    expect(tabLabel('instances')).toBe('实例');
  });

  it('其它页签不带数字，即使传了实例数', () => {
    expect(tabLabel('versions', 5)).toBe('版本');
    expect(tabLabel('nodes', 5)).toBe('节点');
    expect(tabLabel('overview', 5)).toBe('概览');
  });
});

// 一个页签渲染一块部署内容：版本、节点、部署历史、流水线各占一个页签；概览与其余页签不渲染部署面板
describe('tabSection · 页签对应哪一块部署内容', () => {
  it.each(['versions', 'nodes', 'history', 'pipeline'] as const)('%s 页签对应同名的部署内容', (key) => {
    expect(tabSection(key)).toBe(key);
  });

  it.each(['overview', 'trace', 'logs', 'config', 'instances'] as const)('%s 不是部署页签：返回 null', (key) => {
    expect(tabSection(key)).toBeNull();
  });

  it('九个页签都有明确结果（部署内容名或 null），不会返回 undefined', () => {
    for (const t of WORKBENCH_TABS) {
      expect(tabSection(t.key)).not.toBeUndefined();
    }
  });

  it('恰好四个页签对应部署内容', () => {
    expect(WORKBENCH_TABS.filter((t) => tabSection(t.key) !== null)).toHaveLength(4);
  });
});
