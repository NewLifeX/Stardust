// 底部控制台（纯逻辑，无 Vue 依赖，可直接用 node --experimental-strip-types 跑单测）。
// 对照原型 kit-shell.jsx 的 ConsoleDock 与 kit-app.jsx 的 runTerm / openTerm。
//
// 偏离原型（均为有意）：
//  1. 终端缓冲区存行数组，模板用文本插值渲染，不用 v-html（原型靠 dangerouslySetInnerHTML + 手工转义）。
//  2. 节点用真实 id，不用数组下标。
//  3. 未被拦截的命令不会发送到节点，也不带 df / uptime / free 的演示输出，只回显 PENDING_MESSAGE。
//  4. 日志页签暂无真实数据源（现有 SampleData 看起来是链路采样，未发现级别与正文字段），不生成演示日志；
//     因此日志页签不显示“清空”，也不照搬原型那条“日志已清空(演示)”。

import { BLOCKED_MESSAGE, PENDING_MESSAGE, isDangerousCommand, normalizeCommand } from './terminal.logic.ts';
import type { DockTab } from './ui.store.ts';

export type NodeId = number | string;

export interface DockNode {
  id: NodeId;
  name: string;
  ip: string;
  online: boolean;
}

/** 与原型 KD.logs 的字段一致：t 时间、l 级别、app 应用名、m 正文 */
export interface DockLog {
  t: string;
  l: 'Info' | 'Warn' | 'Error' | 'Debug';
  app: string;
  m: string;
}

/** cmd：用户输入的命令（原型里是品牌浅色）；out：输出（原型里 pre-wrap、不透明度 .85） */
export interface TermLine {
  kind: 'cmd' | 'out';
  text: string;
}

/** 以 String(节点 id) 为键，每台节点一份缓冲区 */
export type TermBuffers = Readonly<Record<string, readonly TermLine[]>>;

/** 原型：KD.logs.filter(...).slice(-120) */
export const LOG_LIMIT = 120;

/** 日志范围：只有在应用工作台页才有 scopeApp，其余页面看全部应用 */
export function scopedLogs(logs: readonly DockLog[], scopeApp: string | null, limit: number = LOG_LIMIT): DockLog[] {
  return logs.filter((x) => !scopeApp || x.app === scopeApp).slice(-limit);
}

/** 原型：项目：{KD.project}{scopeApp ? ` · ${scopeApp}` : ''} */
export function scopeLabel(project: string, scopeApp: string | null): string {
  return `项目：${project}${scopeApp ? ` · ${scopeApp}` : ''}`;
}

/** 对应原型 openTerm：目标节点在线就用它，否则退到第一台在线节点；没有在线节点返回 null */
export function pickTermNode(nodes: readonly DockNode[], wanted: NodeId | null): NodeId | null {
  const online = nodes.filter((n) => n.online);
  const hit = online.find((n) => n.id === wanted);
  const target = hit ?? online[0];
  return target ? target.id : null;
}

/** 当前节点的缓冲区；没有选中节点时为空 */
export function bufferOf(buffers: TermBuffers, nodeId: NodeId | null): readonly TermLine[] {
  if (nodeId === null) return [];
  return buffers[String(nodeId)] ?? [];
}

/**
 * 提交一条命令。空白不提交（accepted=false，缓冲区原样返回）。
 * 高危命令回显拦截文案；其余命令因为没有终端通道，只回显“待接入”。
 */
export function submitCommand(
  buffers: TermBuffers,
  nodeId: NodeId,
  raw: string,
): { buffers: TermBuffers; accepted: boolean } {
  const cmd = normalizeCommand(raw);
  if (cmd === null) return { buffers, accepted: false };
  const key = String(nodeId);
  const reply = isDangerousCommand(cmd) ? BLOCKED_MESSAGE : PENDING_MESSAGE;
  const next: TermLine[] = [...(buffers[key] ?? []), { kind: 'cmd', text: cmd }, { kind: 'out', text: reply }];
  return { buffers: { ...buffers, [key]: next }, accepted: true };
}

/** 清空某台节点的缓冲区，不影响其它节点 */
export function clearBuffer(buffers: TermBuffers, nodeId: NodeId): TermBuffers {
  return { ...buffers, [String(nodeId)]: [] };
}

/** 日志页签没有真实数据可清，只有终端页签显示“清空” */
export function showClear(tab: DockTab): boolean {
  return tab === 'term';
}
