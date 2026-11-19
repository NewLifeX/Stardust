// pipelineCell.logic 单元测试（2026-11-12 线上走查 bug2：AppPipeline 列表数据列全空白）
// 红线根因：cellText/row.Id 直取 PascalCase，212 真机序列化为 camelCase → 全空白。

import { describe, expect, it } from 'vitest';
import { cellText, pickField } from './pipelineCell.logic.ts';

describe('pickField · 大小写都认', () => {
  it('212 线上方言（camelCase 行）取 PascalCase 列名', () => {
    const row = { id: 5, name: '测试-MindHub', deployName: 'MindHub.Api', branch: 'main' };
    expect(pickField(row, 'Id')).toBe(5);
    expect(pickField(row, 'Name')).toBe('测试-MindHub');
    expect(pickField(row, 'DeployName')).toBe('MindHub.Api');
    expect(pickField(row, 'Branch')).toBe('main');
  });

  it('MVC/本地方言（PascalCase 行）直取', () => {
    const row = { Id: 9, Name: '主集', DeployName: 'MindHub.Api' };
    expect(pickField(row, 'Id')).toBe(9);
    expect(pickField(row, 'Name')).toBe('主集');
  });

  it('缺字段/空行返回 undefined', () => {
    expect(pickField({ name: 'x' }, 'UpdateTime')).toBeUndefined();
    expect(pickField(undefined, 'Id')).toBeUndefined();
    expect(pickField(null, 'Id')).toBeUndefined();
  });
});

describe('cellText · 单元格文本', () => {
  it('camelCase 行 Name 出值（bug2 回归主用例）', () => {
    const row = { id: 5, name: '测试-MindHub', deployName: 'MindHub.Api', branch: 'main', enable: true, updateTime: '2026-11-11T14:55:54.123' };
    expect(cellText(row, 'Name')).toBe('测试-MindHub');
    expect(cellText(row, 'DeployName')).toBe('MindHub.Api');
    expect(cellText(row, 'Branch')).toBe('main');
    expect(cellText(row, 'Enable')).toBe('是');
    expect(cellText(row, 'UpdateTime')).toBe('2026-11-11 14:55:54');
  });

  it('PascalCase 行同语义', () => {
    const row = { Name: 'a', Enable: false, UpdateTime: '2026-11-11T14:55:54' };
    expect(cellText(row, 'Name')).toBe('a');
    expect(cellText(row, 'Enable')).toBe('否');
    expect(cellText(row, 'UpdateTime')).toBe('2026-11-11 14:55:54');
  });

  it('缺值出空串不显示 undefined/null', () => {
    expect(cellText({}, 'Name')).toBe('');
    expect(cellText({ name: null }, 'Name')).toBe('');
  });

  it('AutoDeploy 数字方言 0/1', () => {
    expect(cellText({ autoDeploy: 1 }, 'AutoDeploy')).toBe('是');
    expect(cellText({ autoDeploy: 0 }, 'AutoDeploy')).toBe('否');
  });
});
