// 命令面板纯逻辑单测（Vitest）
import { describe, it, expect } from 'vitest';
import {
  filterGroups,
  flattenGroups,
  moveSelection,
  resolvePaletteKey,
  type PaletteGroup,
} from './palette.logic';

const noop = () => {};
const groups: PaletteGroup[] = [
  { title: '页面', list: [{ text: '跳转 · 此刻', hint: '首页', run: noop }, { text: '跳转 · 发布', hint: '远程发布', run: noop }] },
  { title: '应用', list: [{ text: '应用 · OrderService', hint: '交易', run: noop }, { text: '应用 · IotWeb', run: noop }] },
  { title: '节点', list: [{ text: '节点 · bj-app-01', hint: '10.1.10.1', run: noop }] },
];

describe('filterGroups · 按查询过滤分组', () => {
  it('空查询保留全部分组', () => {
    expect(filterGroups(groups, '')).toHaveLength(3);
  });

  it('纯空白等同空查询', () => {
    expect(filterGroups(groups, '   ')).toHaveLength(3);
  });

  it('不区分大小写命中标题', () => {
    expect(filterGroups(groups, 'order')).toHaveLength(1);
  });

  it('命中分组为应用', () => {
    expect(filterGroups(groups, 'order')[0].title).toBe('应用');
  });

  it('命中 hint', () => {
    expect(filterGroups(groups, '交易')[0]?.list).toHaveLength(1);
  });

  it('hint 里的 IP 可搜', () => {
    expect(filterGroups(groups, '10.1.10')[0]?.title).toBe('节点');
  });

  it('无结果返回空数组', () => {
    expect(filterGroups(groups, '不存在')).toHaveLength(0);
  });

  it('同组多项都保留', () => {
    const hit = filterGroups(groups, '跳转');
    expect(hit).toHaveLength(1);
    expect(hit[0].list).toHaveLength(2);
  });

  it('无 hint 的项也能按标题命中', () => {
    expect(filterGroups(groups, 'iotweb')[0].list).toHaveLength(1);
  });

  it('过滤不修改入参', () => {
    expect(groups[0].list).toHaveLength(2);
  });
});

describe('flattenGroups · 分组展平', () => {
  it('展平后共 5 项', () => {
    expect(flattenGroups(groups)).toHaveLength(5);
  });

  it('顺序保持', () => {
    expect(flattenGroups(groups)[2].text).toBe('应用 · OrderService');
  });

  it('空输入', () => {
    expect(flattenGroups([])).toHaveLength(0);
  });
});

describe('moveSelection · 选中项移动', () => {
  it('下移', () => {
    expect(moveSelection(0, 1, 5)).toBe(1);
  });

  it('末尾钳制', () => {
    expect(moveSelection(4, 1, 5)).toBe(4);
  });

  it('顶部钳制', () => {
    expect(moveSelection(0, -1, 5)).toBe(0);
  });

  it('上移', () => {
    expect(moveSelection(3, -1, 5)).toBe(2);
  });

  it('空列表恒为 0', () => {
    expect(moveSelection(0, 1, 0)).toBe(0);
  });

  it('越界选中值被拉回', () => {
    expect(moveSelection(7, 0, 3)).toBe(2);
  });
});

describe('resolvePaletteKey · 面板按键', () => {
  it('Esc 关闭', () => {
    expect(resolvePaletteKey('Escape', 0, 5)?.type).toBe('close');
  });

  it('↓ 选中下一项', () => {
    expect(resolvePaletteKey('ArrowDown', 0, 5)).toEqual({ type: 'select', index: 1 });
  });

  it('↑ 顶部钳制', () => {
    expect(resolvePaletteKey('ArrowUp', 0, 5)).toEqual({ type: 'select', index: 0 });
  });

  it('Enter 执行当前项', () => {
    expect(resolvePaletteKey('Enter', 2, 5)).toEqual({ type: 'run', index: 2 });
  });

  it('空结果按 Enter 返回 none（需 preventDefault，不执行）', () => {
    expect(resolvePaletteKey('Enter', 0, 0)?.type).toBe('none');
  });

  it('选中越界按 Enter 不执行', () => {
    expect(resolvePaletteKey('Enter', 9, 3)?.type).toBe('none');
  });

  it('普通字符不处理', () => {
    expect(resolvePaletteKey('a', 0, 5)).toBeNull();
  });

  it('Tab 不处理', () => {
    expect(resolvePaletteKey('Tab', 0, 5)).toBeNull();
  });
});
