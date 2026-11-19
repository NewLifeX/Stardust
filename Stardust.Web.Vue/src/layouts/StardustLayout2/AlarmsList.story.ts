import { defineComponent, h, ref } from 'vue';
import AlarmsList from './AlarmsList.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import type { AlarmRow, AlarmSeverity } from './alarms.logic.ts';

function row(
  id: string,
  title: string,
  level: AlarmSeverity,
  status: 'active' | 'resolved' | 'recovered',
  appName: string,
  extra: Partial<AlarmRow> = {},
): AlarmRow {
  // Object.assign：extra 覆盖默认值，且 Partial 展开不会把必填字段放宽为 undefined
  return Object.assign(
    {
      id,
      title,
      category: level === 'critical' ? '节点下线' : level === 'warning' ? '错误数过高' : '自定义',
      action: appName,
      appName,
      level,
      status,
      groupId: '',
      startTime: '2026-10-30 09:00',
      startMs: Date.parse('2026-10-30T09:00:00'),
      endTimeMs: 0,
      durationSec: 300,
      durationText: '5 分钟',
      content: '最近 5 分钟错误率 12%（阈值 1%）',
      error: '',
      times: 2,
    },
    extra,
  );
}

// 当前告警在前（active），历史在后（resolved）：1 critical + 1 warning + 1 info active，1 resolved
// groupId：active 行带组（行内「告警历史」链接可见），node-a 无组（链接隐藏）；resolved 行带 Error 展示原文
const MIXED: AlarmRow[] = [
  row('11', 'node-b 已下线', 'critical', 'active', 'node-b', { times: 1, content: '连续 6 次心跳丢失。', groupId: '2' }),
  row('10', 'OrderService 错误率过高', 'warning', 'active', 'OrderService', { groupId: '1' }),
  row('9', 'node-a 磁盘提示', 'info', 'active', 'node-a', { times: 0, content: '' }),
  row('8', 'PayService 已恢复', 'warning', 'resolved', 'PayService', { durationText: '2 小时 10 分', startTime: '2026-10-29 18:00', startMs: Date.parse('2026-10-29T18:00:00'), groupId: '1', error: '投递超时' }),
];

// CARD-MON-03：追加一条 recovered 记录行（编辑/删除/告警历史均可见；Tag「已恢复」ok 语气）
const RECORD_ACTIONS: AlarmRow[] = [
  ...MIXED,
  row('7', 'OrderService 已恢复', 'warning', 'recovered', 'OrderService', {
    groupId: '1',
    startTime: '2026-10-30 09:10',
    startMs: Date.parse('2026-10-30T09:10:00'),
    endTimeMs: Date.parse('2026-10-30T11:10:00'),
    durationSec: 7200,
    durationText: '2 小时',
  }),
];

/** 告警组下拉选项（alarms-filter-group 数据源样例；组 Id 字符串透传，走查缺陷②） */
const GROUPS = [
  { id: '1', name: '值班告警' },
  { id: '2', name: '节点巡检' },
];

function recordAction(key: string, value: unknown): void {
  const w = window as unknown as { __alarmAction?: [string, unknown][] };
  (w.__alarmAction ??= []).push([key, value]);
}

/**
 * CARD-MON-03 行操作 + 已恢复视图 + 删除 Guard（defineComponent 包装，镜像 Alarms.vue 页面接线）：
 * 状态下拉受控（变更上抛 statusFilter）；行操作「编辑」上抛 edit；「删除」先弹 GuardModal
 * （标题含记录名 + 正文逐字），确认上抛 remove、取消关闭（与页面 confirmRemove 同构，不真发请求）。
 */
const RecordActionsStory = defineComponent({
  name: 'AlarmsListRecordActionsStory',
  setup() {
    const statusFilter = ref('');
    const removeTarget = ref<AlarmRow | null>(null);
    return () => [
      h(AlarmsList, {
        rows: RECORD_ACTIONS,
        groups: GROUPS,
        statusFilter: statusFilter.value,
        'onUpdate:statusFilter': (v: string) => {
          statusFilter.value = v;
          recordAction('statusFilter', v);
        },
        onEdit: (r: AlarmRow) => recordAction('edit', r.id),
        onRemove: (r: AlarmRow) => {
          removeTarget.value = r;
        },
        onHistory: (gid: string) => recordAction('history', gid),
      }),
      removeTarget.value
        ? h(GuardModal, {
            label: `删除告警记录「${removeTarget.value.title}」？`,
            detail: '删除后该条告警生命周期记录不可恢复',
            onCancel: () => {
              removeTarget.value = null;
              recordAction('guardCancel', true);
            },
            onConfirm: () => {
              const id = removeTarget.value?.id ?? '';
              removeTarget.value = null;
              recordAction('remove', id);
            },
          })
        : null,
    ];
  },
});

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

  // CARD-MON-02 组过滤 + 时间范围（受控）：选中组 1 + 时间窗，变更上抛录制进 window.__alarmFilter
  {
    id: 'AlarmsList/GroupFilter',
    component: AlarmsList,
    props: {
      rows: MIXED,
      groups: GROUPS,
      groupId: '1',
      dtStart: '2026-11-01',
      dtEnd: '2026-11-07',
      'onUpdate:groupId': (v: string | null) => {
        const w = window as unknown as { __alarmFilter?: [string, unknown][] };
        (w.__alarmFilter ??= []).push(['groupId', v]);
      },
      'onUpdate:dtStart': (v: string) => {
        const w = window as unknown as { __alarmFilter?: [string, unknown][] };
        (w.__alarmFilter ??= []).push(['dtStart', v]);
      },
      'onUpdate:dtEnd': (v: string) => {
        const w = window as unknown as { __alarmFilter?: [string, unknown][] };
        (w.__alarmFilter ??= []).push(['dtEnd', v]);
      },
      onHistory: (gid: string) => {
        const w = window as unknown as { __alarmFilter?: [string, unknown][] };
        (w.__alarmFilter ??= []).push(['history', gid]);
      },
    },
  },

  // CARD-MON-02 告警组列表加载失败：下拉旁可见错误提示（§11 失败不静默）
  {
    id: 'AlarmsList/GroupError',
    component: AlarmsList,
    props: { rows: MIXED, groups: [], groupError: '告警组列表加载失败' },
  },

  // CARD-MON-03 行操作 + 已恢复 + 删除 Guard：见 RecordActionsStory（录制进 window.__alarmAction）
  { id: 'AlarmsList/RecordActions', component: RecordActionsStory },
];
