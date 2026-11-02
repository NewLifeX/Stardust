// 命令面板条目生成的单测（Vitest）
import { describe, it, expect } from 'vitest';
import {
  BASE_PLACEHOLDER,
  appNameById,
  appPath,
  buildBasePalette,
  buildVerbAction,
  pagePath,
  parseAppId,
  type PaletteApp,
  type PaletteDeps,
  type PaletteNode,
  type VerbAction,
} from './palette.items';
import type { PaletteConfig } from './ui.store';

const BACKTICK = '\x60';

function setup(extra: Partial<PaletteDeps> = {}) {
  const log: string[] = [];
  const deps: PaletteDeps = { go: (p) => log.push(`go:${p}`), ...extra };
  return { log, deps };
}

function paletteOf(a: VerbAction | null): PaletteConfig | null {
  return a && a.type === 'palette' ? a.config : null;
}

const apps: PaletteApp[] = [
  { id: 1, name: 'iotweb', category: '业务', version: 'v1.2', instances: 2, online: true },
  { id: 2, name: 'oldjob', category: '任务', version: 'v0.9', instances: 0, online: false },
  { id: 3, name: '订单 服务', online: true },
];
const nodes: PaletteNode[] = [
  { id: 1, name: 'node-a', ip: '10.0.0.1', online: true },
  { id: 2, name: 'node-b', ip: '10.0.0.2', online: false },
];

describe('pagePath / appPath · 路由路径生成', () => {
  it('now → /now', () => {
    expect(pagePath('now')).toBe('/now');
  });

  it('mesh → /mesh', () => {
    expect(pagePath('mesh')).toBe('/mesh');
  });

  it('页签在前、应用 Id 在末尾', () => {
    expect(appPath(12, 'versions')).toBe('/app/versions/12');
  });

  it('各页签都按 /app/<页签>/<Id> 生成', () => {
    expect(appPath(7, 'overview')).toBe('/app/overview/7');
    expect(appPath(7, 'logs')).toBe('/app/logs/7');
    expect(appPath(7, 'config')).toBe('/app/config/7');
  });
});

describe('parseAppId · 路由参数解析', () => {
  it('正整数字符串', () => {
    expect(parseAppId('12')).toBe(12);
  });

  it('数组取首项（vue-router 重复参数时是数组）', () => {
    expect(parseAppId(['5', '6'])).toBe(5);
  });

  it.each<unknown>([undefined, null, '', '0', '-1', '1.5', 'abc', '012', '1e3', ' 1', 12, [], ['x']])(
    '%s 无法解析，返回 null',
    (raw) => {
      expect(parseAppId(raw)).toBeNull();
    },
  );

  it('超出安全整数范围返回 null', () => {
    expect(parseAppId('9007199254740993')).toBeNull();
  });
});

describe('appNameById · 按 Id 查应用名', () => {
  it('找到：返回应用名', () => {
    expect(appNameById(apps, 3)).toBe('订单 服务');
  });

  it('找不到：返回 null', () => {
    expect(appNameById(apps, 99)).toBeNull();
  });

  it('Id 为 null（非工作台页面）：返回 null', () => {
    expect(appNameById(apps, null)).toBeNull();
  });

  it('应用列表为空：返回 null', () => {
    expect(appNameById([], 1)).toBeNull();
  });
});

describe('buildBasePalette · 页面分组', () => {
  it('占位符使用原型文案', () => {
    const { deps } = setup();
    expect(buildBasePalette(deps).placeholder).toBe(BASE_PLACEHOLDER);
  });

  it('只有数据与处理函数齐备的分组才出现：仅「页面」', () => {
    const { deps } = setup();
    const cfg = buildBasePalette(deps);
    expect(cfg.groups).toHaveLength(1);
    expect(cfg.groups[0].title).toBe('页面');
  });

  it('页面 5 项', () => {
    const { deps } = setup();
    expect(buildBasePalette(deps).groups[0].list).toHaveLength(5);
  });

  it('文案照抄原型，第三项是「星图」', () => {
    const { deps } = setup();
    const list = buildBasePalette(deps).groups[0].list;
    expect(list.map((i) => i.text).join('|')).toBe('跳转 · 此刻|跳转 · 应用|跳转 · 星图|跳转 · 变更|跳转 · 治理');
  });

  it('hint 照抄原型', () => {
    const { deps } = setup();
    const list = buildBasePalette(deps).groups[0].list;
    expect(list.map((i) => i.hint).join('|')).toBe('首页|列表|节点|跨应用时间线|低频配置');
  });

  it('点击「星图」跳 /nodes，「变更」跳 /changes，「治理」跳 /mesh', () => {
    const { log, deps } = setup();
    const list = buildBasePalette(deps).groups[0].list;
    list[2].run();
    list[3].run();
    list[4].run();
    expect(log.join(',')).toBe('go:/nodes,go:/changes,go:/mesh');
  });
});

describe('buildBasePalette · 应用与节点', () => {
  it('应用组包含全部应用（含离线），共 3 项', () => {
    const { deps } = setup({ apps });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '应用');
    expect(g).toBeTruthy();
    expect(g?.list).toHaveLength(3);
  });

  it('文案与 hint（分类）', () => {
    const { deps } = setup({ apps });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '应用');
    expect(g?.list[0].text).toBe('应用 · iotweb');
    expect(g?.list[0].hint).toBe('业务');
  });

  it('无分类时 hint 为空', () => {
    const { deps } = setup({ apps });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '应用');
    expect(g?.list[2].hint).toBeUndefined();
  });

  it('点击应用跳到概览页签', () => {
    const { log, deps } = setup({ apps });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '应用');
    g?.list[0].run();
    expect(log[0]).toBe('go:/app/overview/1');
  });

  it('路径只用 Id，名称含空格或中文也不影响', () => {
    const { log, deps } = setup({ apps });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '应用');
    g?.list[2].run();
    expect(log[0]).toBe('go:/app/overview/3');
  });

  it('没传 openNode 时不生成节点组', () => {
    const { deps } = setup({ apps });
    expect(buildBasePalette(deps).groups.some((x) => x.title === '节点')).toBe(false);
  });

  it('传了 openNode 后生成节点组，含离线节点', () => {
    const { deps } = setup({ nodes, openNode: () => {} });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '节点');
    expect(g).toBeTruthy();
    expect(g?.list).toHaveLength(2);
  });

  it('节点文案与 hint（IP）', () => {
    const { deps } = setup({ nodes, openNode: () => {} });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '节点');
    expect(g?.list[0].text).toBe('节点 · node-a');
    expect(g?.list[0].hint).toBe('10.0.0.1');
  });

  it('点击节点以 id 调用 openNode', () => {
    const opened: Array<string | number> = [];
    const { deps } = setup({ nodes, openNode: (id) => opened.push(id) });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '节点');
    g?.list[1].run();
    expect(opened).toHaveLength(1);
    expect(opened[0]).toBe(2);
  });

  it('应用、节点列表为空时丢掉对应分组', () => {
    const { deps } = setup({ apps: [], nodes: [], openNode: () => {} });
    expect(buildBasePalette(deps).groups).toHaveLength(1);
  });
});

describe('buildBasePalette · 快捷操作', () => {
  it('没有任何处理函数时不生成快捷操作组', () => {
    const { deps } = setup();
    expect(buildBasePalette(deps).groups.some((x) => x.title === '快捷操作')).toBe(false);
  });

  it('只传 openDock：只有控制台一项', () => {
    const { deps } = setup({ openDock: () => {} });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '快捷操作');
    expect(g).toBeTruthy();
    expect(g?.list).toHaveLength(1);
    expect(g?.list[0].text).toBe('操作 · 打开底部控制台');
  });

  it('hint 是反引号', () => {
    const { deps } = setup({ openDock: () => {} });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '快捷操作');
    expect(g?.list[0].hint).toBe(BACKTICK);
  });

  it('两个处理函数都传：两项', () => {
    const { deps } = setup({ openDock: () => {}, startWizard: () => {} });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '快捷操作');
    expect(g).toBeTruthy();
    expect(g?.list).toHaveLength(2);
  });

  it('第一项：接入向导', () => {
    const { deps } = setup({ openDock: () => {}, startWizard: () => {} });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '快捷操作');
    expect(g?.list[0].text).toBe('操作 · 接入应用或节点');
    expect(g?.list[0].hint).toBe('向导');
  });

  it('点击分别调用对应处理函数', () => {
    const log: string[] = [];
    const { deps } = setup({ openDock: () => log.push('dock'), startWizard: () => log.push('wizard') });
    const g = buildBasePalette(deps).groups.find((x) => x.title === '快捷操作');
    g?.list[0].run();
    g?.list[1].run();
    expect(log.join(',')).toBe('wizard,dock');
  });

  it('不带演示数据条目', () => {
    const { deps } = setup({ openDock: () => {}, startWizard: () => {} });
    const cfg = buildBasePalette(deps);
    expect(cfg.groups.some((x) => x.list.some((i) => i.text.includes('TraceId') || i.text.includes('OrderService')))).toBe(false);
  });
});

describe('buildVerbAction · P/C/L', () => {
  const cases: Array<['P' | 'C' | 'L', string, string]> = [
    ['P', '发布哪个应用？', 'versions'],
    ['C', '改哪个应用的配置？', 'config'],
    ['L', '看哪个应用的日志？', 'logs'],
  ];

  it.each(cases)('%s：提示语', (verb, ph) => {
    const { deps } = setup({ apps });
    const cfg = paletteOf(buildVerbAction(verb, deps));
    expect(cfg).toBeTruthy();
    expect(cfg?.placeholder).toBe(ph);
  });

  it.each(cases)('%s：只有「应用」一组', (verb) => {
    const { deps } = setup({ apps });
    const cfg = paletteOf(buildVerbAction(verb, deps));
    expect(cfg?.groups).toHaveLength(1);
    expect(cfg?.groups[0].title).toBe('应用');
  });

  it.each(cases)('%s：只列在线应用', (verb) => {
    const { deps } = setup({ apps });
    const cfg = paletteOf(buildVerbAction(verb, deps));
    expect(cfg?.groups[0].list.map((i) => i.text).join('|')).toBe('iotweb|订单 服务');
  });

  it.each(cases)('%s：选中后跳到对应页签', (verb, _ph, tab) => {
    const { log, deps } = setup({ apps });
    const cfg = paletteOf(buildVerbAction(verb, deps));
    cfg?.groups[0].list[0].run();
    expect(log[0]).toBe(`go:/app/${tab}/1`);
  });

  it('hint 版本与实例数用「 · 」连接', () => {
    const { deps } = setup({ apps });
    const cfg = paletteOf(buildVerbAction('P', deps));
    expect(cfg?.groups[0].list[0].hint).toBe('v1.2 · 2 实例');
  });

  it('版本、实例数都没有：hint 为空', () => {
    const { deps } = setup({ apps });
    const cfg = paletteOf(buildVerbAction('P', deps));
    expect(cfg?.groups[0].list[1].hint).toBeUndefined();
  });

  it('只有版本：没有多余分隔符', () => {
    const { deps } = setup({ apps: [{ id: 1, name: 'x', version: 'v1', online: true }] });
    expect(paletteOf(buildVerbAction('P', deps))?.groups[0].list[0].hint).toBe('v1');
  });

  it('只有实例数：没有多余分隔符', () => {
    const { deps } = setup({ apps: [{ id: 1, name: 'x', instances: 3, online: true }] });
    expect(paletteOf(buildVerbAction('P', deps))?.groups[0].list[0].hint).toBe('3 实例');
  });

  it('实例数为 0 也要显示', () => {
    const { deps } = setup({ apps: [{ id: 1, name: 'x', instances: 0, online: true }] });
    expect(paletteOf(buildVerbAction('P', deps))?.groups[0].list[0].hint).toBe('0 实例');
  });

  it('没有应用：返回 null', () => {
    const { deps } = setup();
    expect(buildVerbAction('P', deps)).toBeNull();
  });

  it('只有离线应用：返回 null', () => {
    const { deps } = setup({ apps: [apps[1]] });
    expect(buildVerbAction('L', deps)).toBeNull();
  });
});

describe('buildVerbAction · N', () => {
  it('缺 openTerm：返回 null', () => {
    const { deps } = setup({ nodes });
    expect(buildVerbAction('N', deps)).toBeNull();
  });

  it('没有在线节点：返回 null', () => {
    const { deps } = setup({ nodes: [nodes[1]], openTerm: () => {} });
    expect(buildVerbAction('N', deps)).toBeNull();
  });

  it('提示语', () => {
    const { deps } = setup({ nodes, openTerm: () => {} });
    expect(paletteOf(buildVerbAction('N', deps))?.placeholder).toBe('连接哪台节点？');
  });

  it('只列在线节点', () => {
    const { deps } = setup({ nodes, openTerm: () => {} });
    const cfg = paletteOf(buildVerbAction('N', deps));
    expect(cfg?.groups[0].title).toBe('在线节点');
    expect(cfg?.groups[0].list).toHaveLength(1);
  });

  it('文案与 hint（IP）', () => {
    const { deps } = setup({ nodes, openTerm: () => {} });
    const cfg = paletteOf(buildVerbAction('N', deps));
    expect(cfg?.groups[0].list[0].text).toBe('node-a');
    expect(cfg?.groups[0].list[0].hint).toBe('10.0.0.1');
  });

  it('选中后以 id 调用 openTerm', () => {
    const opened: Array<string | number> = [];
    const { deps } = setup({ nodes, openTerm: (id) => opened.push(id) });
    const cfg = paletteOf(buildVerbAction('N', deps));
    cfg?.groups[0].list[0].run();
    expect(opened).toHaveLength(1);
    expect(opened[0]).toBe(1);
  });
});

describe('buildVerbAction · A 与其它', () => {
  it('A 有 startWizard：返回 wizard 指令', () => {
    const { deps } = setup({ startWizard: () => {} });
    const a = buildVerbAction('A', deps);
    expect(a).toBeTruthy();
    expect(a?.type).toBe('wizard');
  });

  it('A 缺 startWizard：返回 null', () => {
    const { deps } = setup();
    expect(buildVerbAction('A', deps)).toBeNull();
  });

  it('未知动词：返回 null', () => {
    const { deps } = setup({ apps, nodes });
    expect(buildVerbAction('X' as never, deps)).toBeNull();
  });
});
