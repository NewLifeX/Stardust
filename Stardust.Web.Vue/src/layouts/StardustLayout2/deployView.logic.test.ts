// 发布页展示用的纯计算单测（Vitest）：从 ShipPanel.vue 里移出，行为与原组件保持一致。
import { describe, expect, it } from 'vitest';
import { nodeStats, nodeVersionTag } from './deployView.logic.ts';

describe('nodeVersionTag', () => {
  it('没有部署集时不显示标签', () => {
    expect(nodeVersionTag(null, { enable: true, version: '1.0' })).toBeNull();
  });

  it('节点已停用时不显示标签', () => {
    expect(nodeVersionTag({ version: '1.0', multiVersion: false }, { enable: false, version: '1.0' })).toBeNull();
  });

  it('节点版本与部署集一致：绿色「与部署集一致」', () => {
    expect(nodeVersionTag({ version: '1.0', multiVersion: false }, { enable: true, version: '1.0' })).toEqual({
      tone: 'ok',
      label: '与部署集一致',
    });
  });

  it('节点没有上报版本：黄色「未上报版本」，多版本时也一样', () => {
    const want = { tone: 'warn', label: '未上报版本' };
    expect(nodeVersionTag({ version: '1.0', multiVersion: false }, { enable: true, version: '' })).toEqual(want);
    expect(nodeVersionTag({ version: '1.0', multiVersion: true }, { enable: true, version: '' })).toEqual(want);
  });

  it('版本不一致且没开多版本：黄色「与部署集不一致」', () => {
    expect(nodeVersionTag({ version: '1.0', multiVersion: false }, { enable: true, version: '0.9' })).toEqual({
      tone: 'warn',
      label: '与部署集不一致',
    });
  });

  it('版本不一致但开了多版本：灰色「多版本，允许不同」', () => {
    expect(nodeVersionTag({ version: '1.0', multiVersion: true }, { enable: true, version: '0.9' })).toEqual({
      tone: 'default',
      label: '多版本，允许不同',
    });
  });

  it('部署集版本为空、节点版本也为空：按「未上报版本」处理，不会判成一致', () => {
    expect(nodeVersionTag({ version: '', multiVersion: false }, { enable: true, version: '' })).toEqual({
      tone: 'warn',
      label: '未上报版本',
    });
  });
});

describe('nodeStats', () => {
  it('只统计启用的节点，停用节点不算', () => {
    const nodes = [
      { enable: true, version: '1.0' },
      { enable: true, version: '0.9' },
      { enable: false, version: '1.0' },
    ];
    expect(nodeStats('1.0', nodes)).toEqual({ total: 2, same: 1, diff: 1 });
  });

  it('全部一致时 diff 为 0', () => {
    expect(nodeStats('1.0', [{ enable: true, version: '1.0' }, { enable: true, version: '1.0' }])).toEqual({
      total: 2,
      same: 2,
      diff: 0,
    });
  });

  it('部署集版本为空：没有节点算一致', () => {
    expect(nodeStats('', [{ enable: true, version: '' }, { enable: true, version: '1.0' }])).toEqual({
      total: 2,
      same: 0,
      diff: 2,
    });
  });

  it('没有节点：三项都是 0', () => {
    expect(nodeStats('1.0', [])).toEqual({ total: 0, same: 0, diff: 0 });
  });
});