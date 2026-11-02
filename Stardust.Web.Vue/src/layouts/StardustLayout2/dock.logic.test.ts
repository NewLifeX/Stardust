// 底部控制台纯逻辑单测（Vitest）
import { describe, it, expect } from 'vitest';
import {
  LOG_LIMIT,
  bufferOf,
  clearBuffer,
  pickTermNode,
  scopeLabel,
  scopedLogs,
  showClear,
  submitCommand,
  type DockLog,
  type DockNode,
  type TermBuffers,
} from './dock.logic';
import { BLOCKED_MESSAGE, PENDING_MESSAGE } from './terminal.logic';

const mkLog = (i: number, app = 'A'): DockLog => ({ t: '10:00:00', l: 'Info', app, m: 'm' + i });
const node = (id: number | string, online = true): DockNode => ({ id, name: 'n' + id, ip: '10.0.0.' + id, online });

describe('LOG_LIMIT · 日志条数上限', () => {
  it('默认上限 120（原型 slice(-120)）', () => {
    expect(LOG_LIMIT).toBe(120);
  });
});

describe('scopedLogs · 按应用范围过滤日志', () => {
  const logs = [mkLog(1, 'A'), mkLog(2, 'B'), mkLog(3, 'A')];

  it('scopeApp 为 null：看全部应用', () => {
    expect(scopedLogs(logs, null)).toHaveLength(3);
  });

  it('scopeApp 为空串：按无范围处理', () => {
    expect(scopedLogs(logs, '')).toHaveLength(3);
  });

  it('指定应用：只留该应用', () => {
    const a = scopedLogs(logs, 'A');
    expect(a).toHaveLength(2);
    expect(a.every((x) => x.app === 'A')).toBe(true);
  });

  it('无匹配返回空数组', () => {
    expect(scopedLogs(logs, 'Z')).toHaveLength(0);
  });

  it('空输入返回空数组', () => {
    expect(scopedLogs([], null)).toHaveLength(0);
  });

  it('超过 120 条只取 120 条', () => {
    const many = Array.from({ length: 130 }, (_, i) => mkLog(i));
    expect(scopedLogs(many, null)).toHaveLength(120);
  });

  it('取的是最后 120 条，顺序不变', () => {
    const many = Array.from({ length: 130 }, (_, i) => mkLog(i));
    const cut = scopedLogs(many, null);
    expect(cut[0].m).toBe('m10');
    expect(cut[119].m).toBe('m129');
  });

  it('可自定义上限', () => {
    const many = Array.from({ length: 130 }, (_, i) => mkLog(i));
    expect(scopedLogs(many, null, 5)).toHaveLength(5);
  });

  it('不修改入参', () => {
    const before = logs.length;
    scopedLogs(logs, 'A');
    expect(logs).toHaveLength(before);
  });
});

describe('scopeLabel · 范围标签文案', () => {
  it('无应用：只显示项目', () => {
    expect(scopeLabel('prod', null)).toBe('项目：prod');
  });

  it('有应用：用「 · 」连接', () => {
    expect(scopeLabel('prod', 'OrderService')).toBe('项目：prod · OrderService');
  });

  it('空串应用按无应用处理', () => {
    expect(scopeLabel('prod', '')).toBe('项目：prod');
  });
});

describe('pickTermNode · 终端目标节点选择', () => {
  const nodes = [node(1, false), node(2), node(3)];

  it('目标在线：用目标', () => {
    expect(pickTermNode(nodes, 3)).toBe(3);
  });

  it('目标离线：退到第一台在线节点', () => {
    expect(pickTermNode(nodes, 1)).toBe(2);
  });

  it('未指定：第一台在线节点', () => {
    expect(pickTermNode(nodes, null)).toBe(2);
  });

  it('目标不存在：第一台在线节点', () => {
    expect(pickTermNode(nodes, 99)).toBe(2);
  });

  it('没有在线节点：null', () => {
    expect(pickTermNode([node(1, false), node(2, false)], 1)).toBeNull();
  });

  it('空列表：null', () => {
    expect(pickTermNode([], null)).toBeNull();
  });

  it('严格相等：字符串 2 找不到数字 2，退回第一台在线节点', () => {
    expect(pickTermNode([node(1), node(2)], '2')).toBe(1);
  });

  it('字符串 id 同样可用', () => {
    expect(pickTermNode([node('a'), node('b')], 'b')).toBe('b');
  });
});

describe('bufferOf · 取节点终端缓冲区', () => {
  const bufs: TermBuffers = { '1': [{ kind: 'cmd', text: 'ls' }] };

  it('未选节点：空', () => {
    expect(bufferOf(bufs, null)).toHaveLength(0);
  });

  it('没有缓冲区的节点：空', () => {
    expect(bufferOf(bufs, 2)).toHaveLength(0);
  });

  it('有缓冲区的节点：原样返回', () => {
    expect(bufferOf(bufs, 1)).toHaveLength(1);
  });

  it('数字 1 与字符串 1 共用一份缓冲区（键是 String(id)）', () => {
    expect(bufferOf(bufs, '1')).toHaveLength(1);
  });
});

describe('submitCommand · 终端命令提交', () => {
  it('空串：不提交，缓冲区引用不变', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, '');
    expect(r.accepted).toBe(false);
    expect(r.buffers).toBe(empty);
  });

  it('纯空白：不提交，缓冲区引用不变', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, '   ');
    expect(r.accepted).toBe(false);
    expect(r.buffers).toBe(empty);
  });

  it('普通命令：已受理', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, '  ls -la  ');
    expect(r.accepted).toBe(true);
  });

  it('追加两行：命令和输出', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, '  ls -la  ');
    expect(bufferOf(r.buffers, 1)).toHaveLength(2);
  });

  it('命令行去首尾空白', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, '  ls -la  ');
    const lines = bufferOf(r.buffers, 1);
    expect(lines[0].kind).toBe('cmd');
    expect(lines[0].text).toBe('ls -la');
  });

  it('未拦截的命令回显待接入文案', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, '  ls -la  ');
    const lines = bufferOf(r.buffers, 1);
    expect(lines[1].kind).toBe('out');
    expect(lines[1].text).toBe(PENDING_MESSAGE);
  });

  it('高危命令：同样受理（回显而不是丢弃）', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, 'rm -rf /');
    expect(r.accepted).toBe(true);
  });

  it('高危命令本身也回显', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, 'rm -rf /');
    expect(bufferOf(r.buffers, 1)[0].text).toBe('rm -rf /');
  });

  it('高危命令输出拦截文案', () => {
    const empty: TermBuffers = {};
    const r = submitCommand(empty, 1, 'rm -rf /');
    expect(bufferOf(r.buffers, 1)[1].text).toBe(BLOCKED_MESSAGE);
  });

  it('继续追加，不覆盖已有内容', () => {
    const empty: TermBuffers = {};
    const r3 = submitCommand(empty, 1, '  ls -la  ');
    const r5 = submitCommand(r3.buffers, 1, 'uptime');
    expect(bufferOf(r5.buffers, 1)).toHaveLength(4);
  });

  it('另一台节点提交，不影响第一台', () => {
    const empty: TermBuffers = {};
    const r3 = submitCommand(empty, 1, '  ls -la  ');
    const r6 = submitCommand(r3.buffers, 2, 'uptime');
    expect(bufferOf(r6.buffers, 1)).toHaveLength(2);
  });

  it('另一台节点有自己的缓冲区', () => {
    const empty: TermBuffers = {};
    const r3 = submitCommand(empty, 1, '  ls -la  ');
    const r6 = submitCommand(r3.buffers, 2, 'uptime');
    expect(bufferOf(r6.buffers, 2)).toHaveLength(2);
  });

  it('不修改入参对象', () => {
    const empty: TermBuffers = {};
    submitCommand(empty, 1, 'ls');
    expect(Object.keys(empty)).toHaveLength(0);
  });

  it('旧缓冲区不被后续提交改动', () => {
    const empty: TermBuffers = {};
    const r3 = submitCommand(empty, 1, '  ls -la  ');
    const r5 = submitCommand(r3.buffers, 1, 'uptime');
    expect(bufferOf(r3.buffers, 1)).toHaveLength(2);
    expect(bufferOf(r5.buffers, 1)).not.toBe(bufferOf(r3.buffers, 1));
  });
});

describe('clearBuffer · 清空节点终端缓冲区', () => {
  it('清空目标节点', () => {
    let bufs: TermBuffers = {};
    bufs = submitCommand(bufs, 1, 'a').buffers;
    bufs = submitCommand(bufs, 2, 'b').buffers;
    const cleared = clearBuffer(bufs, 1);
    expect(bufferOf(cleared, 1)).toHaveLength(0);
  });

  it('其它节点不受影响', () => {
    let bufs: TermBuffers = {};
    bufs = submitCommand(bufs, 1, 'a').buffers;
    bufs = submitCommand(bufs, 2, 'b').buffers;
    const cleared = clearBuffer(bufs, 1);
    expect(bufferOf(cleared, 2)).toHaveLength(2);
  });

  it('不修改入参', () => {
    let bufs: TermBuffers = {};
    bufs = submitCommand(bufs, 1, 'a').buffers;
    bufs = submitCommand(bufs, 2, 'b').buffers;
    clearBuffer(bufs, 1);
    expect(bufferOf(bufs, 1)).toHaveLength(2);
  });

  it('清空没有缓冲区的节点不报错', () => {
    expect(bufferOf(clearBuffer({}, 5), 5)).toHaveLength(0);
  });
});

describe('showClear · 是否显示清空按钮', () => {
  it('终端页签显示清空', () => {
    expect(showClear('term')).toBe(true);
  });

  it('日志页签不显示清空（没有真实日志可清）', () => {
    expect(showClear('log')).toBe(false);
  });
});
