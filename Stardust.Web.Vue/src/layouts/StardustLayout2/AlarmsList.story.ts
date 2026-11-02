import AlarmsList from './AlarmsList.vue';
import type { AlarmRow, AlarmSeverity } from './alarms.logic.ts';

function row(
  id: number,
  title: string,
  level: AlarmSeverity,
  status: 'active' | 'resolved',
  appName: string,
  extra: Partial<AlarmRow> = {},
): AlarmRow {
  return {
    id,
    title,
    category: level === 'critical' ? '节点下线' : level === 'warning' ? '错误数过高' : '自定义',
    action: appName,
    appName,
    level,
    status,
    startTime: '2026-10-30 09:00',
    startMs: Date.parse('2026-10-30T09:00:00'),
    durationText: '5 分钟',
    content: '最近 5 分钟错误率 12%（阈值 1%）',
    times: 2,
    ...extra,
  };
}

// 当前告警在前（active），历史在后（resolved）：1 critical + 1 warning + 1 info active，1 resolved
const MIXED: AlarmRow[] = [
  row(11, 'node-b 已下线', 'critical', 'active', 'node-b', { times: 1, content: '连续 6 次心跳丢失。' }),
  row(10, 'OrderService 错误率过高', 'warning', 'active', 'OrderService'),
  row(9, 'node-a 磁盘提示', 'info', 'active', 'node-a', { times: 0, content: '' }),
  row(8, 'PayService 已恢复', 'warning', 'resolved', 'PayService', { durationText: '2 小时 10 分', startTime: '2026-10-29 18:00', startMs: Date.parse('2026-10-29T18:00:00') }),
];

export const stories = [
  // 有数据：3 条未处理（critical/warning/info）+ 1 条已处理
  { id: 'AlarmsList/Mixed', component: AlarmsList, props: { rows: MIXED } },

  // 空态：没有告警
  { id: 'AlarmsList/Empty', component: AlarmsList, props: { rows: [] } },

  // 加载态：spinner 旋转
  { id: 'AlarmsList/Loading', component: AlarmsList, props: { rows: [], loading: true } },

  // 失败态：可见错误 + 重试按钮
  { id: 'AlarmsList/Failed', component: AlarmsList, props: { rows: [], error: 'Request failed with status 500' } },

  // 点击录制：失败态下点重试按钮把 1 追加进 window.__alarmRetry
  {
    id: 'AlarmsList/Recorder',
    component: AlarmsList,
    props: {
      rows: [],
      error: 'Request failed with status 500',
      onRetry: () => {
        const w = window as unknown as { __alarmRetry?: number[] };
        (w.__alarmRetry ??= []).push(1);
      },
    },
  },
];
