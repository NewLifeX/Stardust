// 命令面板会话状态单测（Vitest）
// 对照原型 kit-shell.jsx 的 Palette：选中项随查询归零、回车与点击先关闭再执行。
import { describe, it, expect } from 'vitest';
import { shallowRef } from 'vue';
import { usePaletteState, DEFAULT_PLACEHOLDER } from './usePaletteState';
import type { PaletteConfig } from '../ui.store';

/**
 * 造一个会话：配置用 shallowRef（与 ui.store 一致，保证 computed 能感知变化）。
 * log 必须由调用方传入并同时交给 sample(log)：close 与 run 要写进同一个数组，
 * 否则无法断言“先关闭再执行”的顺序。
 */
function setup(initial: PaletteConfig | null, log: string[] = []) {
  const config = shallowRef<PaletteConfig | null>(initial);
  const state = usePaletteState({
    config: () => config.value,
    close: () => {
      log.push('close');
      config.value = null;
    },
  });
  return { config, log, state };
}

function sample(log: string[]): PaletteConfig {
  return {
    placeholder: '发布哪个应用？',
    groups: [
      {
        title: '应用',
        list: [
          { text: 'IotWeb', hint: 'v2.1 · 3 实例', run: () => log.push('run:IotWeb') },
          { text: 'OrderService', hint: 'v1.8 · 4 实例', run: () => log.push('run:OrderService') },
        ],
      },
      {
        title: '节点',
        list: [{ text: 'bj-01', hint: '10.1.10.1', run: () => log.push('run:bj-01') }],
      },
    ],
  };
}

describe('usePaletteState · 占位文案', () => {
  it('配置为空时用默认文案', () => {
    const { config, state } = setup(null);
    expect(state.placeholder.value).toBe(DEFAULT_PLACEHOLDER);
    config.value = { placeholder: '', groups: [] };
  });

  it('文案为空串时用默认文案（原型 placeholder || 默认）', () => {
    const { config, state } = setup(null);
    config.value = { placeholder: '', groups: [] };
    expect(state.placeholder.value).toBe(DEFAULT_PLACEHOLDER);
  });

  it('有文案时用配置文案', () => {
    const { config, state } = setup(null);
    config.value = { placeholder: '连接哪台节点？', groups: [] };
    expect(state.placeholder.value).toBe('连接哪台节点？');
  });
});

describe('usePaletteState · 分组视图与全局下标', () => {
  it('两个分组', () => {
    const { state } = setup(sample([]), []);
    expect(state.view.value).toHaveLength(2);
  });

  it('第一组下标 0,1', () => {
    const { state } = setup(sample([]), []);
    expect(state.view.value[0].rows.map((r) => r.index).join()).toBe('0,1');
  });

  it('第二组下标接着编号为 2', () => {
    const { state } = setup(sample([]), []);
    expect(state.view.value[1].rows.map((r) => r.index).join()).toBe('2');
  });

  it('展平后 3 项', () => {
    const { state } = setup(sample([]), []);
    expect(state.flat.value).toHaveLength(3);
  });

  it('初始选中第 0 项', () => {
    const { state } = setup(sample([]), []);
    expect(state.sel.value).toBe(0);
  });
});

describe('usePaletteState · 查询与选中项归零', () => {
  it('hover 选中该行', () => {
    const { state } = setup(sample([]), []);
    state.hover(2);
    expect(state.sel.value).toBe(2);
  });

  it('输入后选中项归零', () => {
    const { state } = setup(sample([]), []);
    state.hover(2);
    state.setQuery('order');
    expect(state.sel.value).toBe(0);
  });

  it('按标题过滤', () => {
    const { state } = setup(sample([]), []);
    state.setQuery('order');
    expect(state.flat.value).toHaveLength(1);
    expect(state.flat.value[0].text).toBe('OrderService');
  });

  it('按 hint 过滤', () => {
    const { state } = setup(sample([]), []);
    state.setQuery('10.1.10');
    expect(state.flat.value).toHaveLength(1);
    expect(state.flat.value[0].text).toBe('bj-01');
  });

  it('无命中的分组被丢弃', () => {
    const { state } = setup(sample([]), []);
    state.setQuery('10.1.10');
    expect(state.view.value).toHaveLength(1);
    expect(state.view.value[0].title).toBe('节点');
  });

  it('无结果时视图为空', () => {
    const { state } = setup(sample([]), []);
    state.setQuery('zzz');
    expect(state.flat.value).toHaveLength(0);
    expect(state.view.value).toHaveLength(0);
  });

  it('清空查询恢复全部', () => {
    const { state } = setup(sample([]), []);
    state.setQuery('zzz');
    state.setQuery('');
    expect(state.flat.value).toHaveLength(3);
  });
});

describe('usePaletteState · activate 先关闭再执行', () => {
  it('有效下标返回 true', () => {
    const { state } = setup(sample([]), []);
    expect(state.activate(1)).toBe(true);
  });

  it('顺序为 close → run', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.activate(1);
    expect(log.join()).toBe('close,run:OrderService');
  });

  it('越界返回 false', () => {
    const { state } = setup(sample([]), []);
    expect(state.activate(9)).toBe(false);
  });

  it('负数下标返回 false', () => {
    const { state } = setup(sample([]), []);
    expect(state.activate(-1)).toBe(false);
  });

  it('越界时不关闭也不执行', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.activate(9);
    state.activate(-1);
    expect(log).toHaveLength(0);
  });

  it('run 中打开的新面板保留，没有被关闭覆盖', () => {
    // run 里再打开新面板（如动词键链路），不能被前面的关闭覆盖
    const log: string[] = [];
    const next: PaletteConfig = { placeholder: '下一个', groups: [] };
    const { config, state } = setup(null, log);
    config.value = {
      placeholder: '当前',
      groups: [{ title: '操作', list: [{ text: '再开一个', run: () => { log.push('run'); config.value = next; } }] }],
    };
    state.activate(0);
    expect(config.value).toBe(next);
  });

  it('先 close 再 run，各一次', () => {
    const log: string[] = [];
    const next: PaletteConfig = { placeholder: '下一个', groups: [] };
    const { config, state } = setup(null, log);
    config.value = {
      placeholder: '当前',
      groups: [{ title: '操作', list: [{ text: '再开一个', run: () => { log.push('run'); config.value = next; } }] }],
    };
    state.activate(0);
    expect(log.join()).toBe('close,run');
  });
});

describe('usePaletteState · onKey 方向键', () => {
  it('↓ 返回 select 1', () => {
    const { state } = setup(sample([]), []);
    expect(state.onKey('ArrowDown')).toEqual({ type: 'select', index: 1 });
  });

  it('↓ 后选中 1', () => {
    const { state } = setup(sample([]), []);
    state.onKey('ArrowDown');
    expect(state.sel.value).toBe(1);
  });

  it('↓ 钳制在最后一项', () => {
    const { state } = setup(sample([]), []);
    state.onKey('ArrowDown');
    state.onKey('ArrowDown');
    state.onKey('ArrowDown');
    expect(state.sel.value).toBe(2);
  });

  it('↑ 钳制在第一项', () => {
    const { state } = setup(sample([]), []);
    state.onKey('ArrowDown');
    state.onKey('ArrowUp');
    state.onKey('ArrowUp');
    state.onKey('ArrowUp');
    expect(state.sel.value).toBe(0);
  });

  it('方向键不触发关闭或执行', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.onKey('ArrowDown');
    state.onKey('ArrowUp');
    expect(log).toHaveLength(0);
  });

  it('回车执行当前项，先关闭再执行', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.onKey('ArrowDown');
    const r = state.onKey('Enter');
    expect(r?.type).toBe('run');
    expect(log.join()).toBe('close,run:OrderService');
  });
});

describe('usePaletteState · onKey Esc 与普通键', () => {
  it('Esc 返回 close', () => {
    const { state } = setup(sample([]), []);
    expect(state.onKey('Escape')?.type).toBe('close');
  });

  it('Esc 调用一次 close', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.onKey('Escape');
    expect(log.join()).toBe('close');
  });

  it('重复 Esc 再关一次，无副作用', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.onKey('Escape');
    expect(state.onKey('Escape')?.type).toBe('close');
    expect(log.join()).toBe('close,close');
  });

  it('空结果回车返回 none（需 preventDefault）', () => {
    const { state } = setup(sample([]), []);
    state.setQuery('zzz');
    expect(state.onKey('Enter')?.type).toBe('none');
  });

  it('空结果回车不关闭、不执行', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.setQuery('zzz');
    state.onKey('Enter');
    expect(log).toHaveLength(0);
  });

  it('普通字符返回 null，不拦截输入', () => {
    const { state } = setup(sample([]), []);
    expect(state.onKey('a')).toBeNull();
  });

  it('Tab 返回 null', () => {
    const { state } = setup(sample([]), []);
    expect(state.onKey('Tab')).toBeNull();
  });

  it('未识别的键不产生任何副作用', () => {
    const log: string[] = [];
    const { state } = setup(sample(log), log);
    state.onKey('a');
    state.onKey('Tab');
    expect(log).toHaveLength(0);
  });
});

// ---------- 追踪组（traceJump，2026-11-16 输入2） ----------

/** 带追踪跳转的配置：traceJump 产自 buildBasePalette 同款形状 */
function sampleWithTrace(log: string[]): PaletteConfig {
  return {
    placeholder: '搜索应用、节点、TraceId,或执行操作…',
    groups: [{ title: '页面', list: [{ text: '跳转 · 此刻', hint: '首页', run: () => log.push('run:now') }] }],
    traceJump: (q) => (/^[A-Za-z0-9_-]{2,}(,[A-Za-z0-9_-]{2,})*$/.test(q.trim()) ? {
      title: '链路追踪',
      list: [{ text: `追踪 · ${q.trim()}`, hint: '打开链路详情', run: () => log.push(`run:trace:${q.trim()}`) }],
    } : null),
  };
}

describe('usePaletteState · traceJump 追踪组', () => {
  it('空查询不追加追踪组', () => {
    const { state } = setup(sampleWithTrace([]), []);
    expect(state.view.value.some((g) => g.title === '链路追踪')).toBe(false);
  });

  it('输入像 id：追加「链路追踪」组（页面组被过滤空时视图只剩追踪组）', () => {
    const { state } = setup(sampleWithTrace([]), []);
    state.setQuery('0abc20d417947901');
    expect(state.view.value.map((g) => g.title).join()).toBe('链路追踪');
  });

  it('输入中文：不追加追踪组', () => {
    const { state } = setup(sampleWithTrace([]), []);
    state.setQuery('星尘');
    expect(state.view.value.some((g) => g.title === '链路追踪')).toBe(false);
  });

  it('纯字母短串也属 id 形态（追加式，页面组命中时共存）', () => {
    const { state } = setup(sampleWithTrace([]), []);
    state.setQuery('zzz');
    // 「zzz」不命中「跳转 · 此刻」→ 页面组空，视图只有追踪组
    expect(state.view.value.map((g) => g.title).join()).toBe('链路追踪');
  });

  it('traceJump 返回 null：视图与无追踪组一致', () => {
    const { state } = setup(sampleWithTrace([]), []);
    state.setQuery('星尘!'); // 不像 id → traceJump null；也不命中页面组 → 空视图
    expect(state.view.value).toHaveLength(0);
  });

  it('追踪组参与全局下标编号，回车按序执行并先关闭', () => {
    const log: string[] = [];
    const { state } = setup(sampleWithTrace(log), log);
    state.setQuery('aaa111');
    // 查询不命中页面组 → 视图只有追踪组，下标 0
    expect(state.view.value[0].rows[0].index).toBe(0);
    state.onKey('Enter');
    expect(log.join()).toBe('close,run:trace:aaa111');
  });

  it('两组共存时追踪组下标接着编号（追加式，不挤掉过滤结果）', () => {
    const log: string[] = [];
    const cfg: PaletteConfig = {
      placeholder: 'x',
      groups: [{ title: '页面', list: [{ text: 'aaa111', run: () => log.push('run:page') }] }],
      traceJump: () => ({ title: '链路追踪', list: [{ text: '追踪 · aaa111', run: () => log.push('run:trace') }] }),
    };
    const s = usePaletteState({ config: () => cfg, close: () => log.push('close') });
    s.setQuery('aaa111');
    expect(s.view.value.map((g) => g.title).join()).toBe('页面,链路追踪');
    expect(s.view.value[1].rows[0].index).toBe(1);
    s.onKey('Enter');
    expect(log.join()).toBe('close,run:page');
  });
});
