// 应用概览「需关注事项」的纯逻辑（无 Vue 依赖，便于单测）。2026-10-29 停点 19 第 7 步。
// 只用已有接口的数据：部署集、节点、流水线运行、部署历史、在线实例数；取不到的一律返回 pending，由页面显示「待接入」，不当作正常。
// 部署历史只是下发记录（Success 只表示命令已发出，不是节点执行结果），文案里写明。
import type { AppTab } from './palette.items.ts';
import { nodeStats } from './deployView.logic.ts';

/** 流水线停在「构建中」超过这个分钟数就提醒（我暂定 30，可改） */
export const LONG_BUILD_MINUTES = 30;

export type AttentionTone = 'ok' | 'warn' | 'info' | 'pending';
export type AttentionKey = 'versions' | 'pipeline' | 'history' | 'instances';

export interface AttentionItem {
  key: AttentionKey;
  title: string;
  tone: AttentionTone;
  detail: string;
  /** 点按钮跳到哪个页签 */
  tab: AppTab;
  actionLabel: string;
}

export interface AttentionInput {
  /** 部署集；没有部署集为 null */
  deploy: { version: string; multiVersion: boolean } | null;
  /** 部署集加载失败（此时 deploy 也是 null）；缺省按 false，即「没有部署集」 */
  deployFailed?: boolean;
  nodes: ReadonlyArray<{ enable: boolean; version: string }>;
  nodesFailed: boolean;
  /** 流水线运行，最近的在最前（mergeRunHistory 已按时间倒序） */
  runs: ReadonlyArray<{ id?: number; status?: string; pipelineName?: string; buildStartedTime?: string }>;
  /** 部署历史，最近的在最前；null 表示加载失败 */
  history: ReadonlyArray<{ success: boolean; actionLabel: string; node: string; time: string }> | null;
  /** 在线实例数；未知为 undefined */
  instances: number | undefined;
  /** 当前时间戳，由调用方注入 */
  now: number;
}

const RUN_LABEL: Record<string, string> = {
  Pending: '排队中',
  Building: '构建中',
  UploadSucceeded: '构建完成，待部署',
  Deploying: '部署中',
};

/** deploy 为 null 时的说明：加载失败与没有部署集要分开写，取不到不等于没有记录 */
function noDeployText(i: AttentionInput): string {
  return i.deployFailed ? '部署集加载失败，无法判断' : '没有部署集';
}

function versionsItem(i: AttentionInput): AttentionItem {
  const base = { key: 'versions' as const, title: '节点版本', tab: 'nodes' as const, actionLabel: '去节点' };
  if (!i.deploy) return { ...base, tone: 'pending', detail: noDeployText(i) };
  if (i.nodesFailed) return { ...base, tone: 'pending', detail: '节点加载失败，无法判断版本是否一致' };
  const s = nodeStats(i.deploy.version, i.nodes);
  if (s.total === 0) return { ...base, tone: 'info', detail: '没有启用的节点' };
  const v = i.deploy.version || '—';
  if (s.diff === 0) return { ...base, tone: 'ok', detail: `${s.total} 个启用节点版本都与部署集 ${v} 一致` };
  if (i.deploy.multiVersion) {
    return { ...base, tone: 'info', detail: `已开启多版本：${s.diff} 个启用节点版本与部署集 ${v} 不同，属于允许` };
  }
  return { ...base, tone: 'warn', detail: `${s.diff} 个启用节点版本与部署集 ${v} 不一致` };
}

function pipelineItem(i: AttentionInput): AttentionItem {
  const base = { key: 'pipeline' as const, title: '流水线', tab: 'pipeline' as const, actionLabel: '看流水线' };
  if (!i.deploy) return { ...base, tone: 'pending', detail: noDeployText(i) };
  const run = i.runs[0];
  if (!run) return { ...base, tone: 'info', detail: '还没有流水线运行记录' };
  const status = run.status ?? '';
  const name = run.pipelineName ? `（${run.pipelineName}）` : '';
  if (status === 'Failed') return { ...base, tone: 'warn', detail: `最近一次运行失败${name}` };
  if (status === 'Success') return { ...base, tone: 'ok', detail: `最近一次运行成功${name}` };
  if (status === 'Cancelled') return { ...base, tone: 'info', detail: `最近一次运行已取消${name}` };
  if (status === 'Building') {
    const started = Date.parse(run.buildStartedTime ?? '');
    if (Number.isNaN(started)) return { ...base, tone: 'info', detail: `最近一次运行构建中${name}` };
    const minutes = Math.max(0, Math.floor((i.now - started) / 60000));
    if (minutes >= LONG_BUILD_MINUTES) {
      return { ...base, tone: 'warn', detail: `构建中已 ${minutes} 分钟，超过 ${LONG_BUILD_MINUTES} 分钟，可能卡住${name}` };
    }
    return { ...base, tone: 'info', detail: `构建中，已 ${minutes} 分钟${name}` };
  }
  return { ...base, tone: 'info', detail: `最近一次运行${RUN_LABEL[status] ?? `状态：${status || '未知'}`}${name}` };
}

function historyItem(i: AttentionInput): AttentionItem {
  const base = { key: 'history' as const, title: '部署下发', tab: 'history' as const, actionLabel: '看部署历史' };
  if (!i.deploy) return { ...base, tone: 'pending', detail: noDeployText(i) };
  if (i.history === null) return { ...base, tone: 'pending', detail: '部署历史加载失败，无法判断最近一次下发' };
  const h = i.history[0];
  if (!h) return { ...base, tone: 'info', detail: '还没有部署下发记录' };
  if (!h.success) return { ...base, tone: 'warn', detail: `最近一次下发失败：${h.node} ${h.actionLabel}（${h.time}）` };
  return { ...base, tone: 'ok', detail: `最近一次下发（${h.node} ${h.actionLabel}）：命令已发出，是否生效请看节点版本` };
}

function instancesItem(i: AttentionInput): AttentionItem {
  const base = { key: 'instances' as const, title: '在线实例', tab: 'instances' as const, actionLabel: '看实例' };
  if (i.instances === undefined) return { ...base, tone: 'pending', detail: '在线实例数待接入' };
  if (i.instances === 0) return { ...base, tone: 'warn', detail: '没有在线实例' };
  return { ...base, tone: 'info', detail: `${i.instances} 个在线实例` };
}

/** 概览的四项需关注事项，次序固定：版本、流水线、部署历史、实例 */
export function buildAttention(i: AttentionInput): AttentionItem[] {
  return [versionsItem(i), pipelineItem(i), historyItem(i), instancesItem(i)];
}
