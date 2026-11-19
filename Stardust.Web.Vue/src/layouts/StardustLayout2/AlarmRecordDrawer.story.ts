import AlarmRecordDrawer from './AlarmRecordDrawer.vue';
import type { AlarmRecordForm } from './alarmRecord.logic.ts';
import type { AlarmGroupOption } from './alarms.logic.ts';

/** 告警组下拉选项（容器注入样例，与 AlarmsList.story 同源；组 Id 字符串透传，走查缺陷②） */
const GROUPS: AlarmGroupOption[] = [
  { id: '1', name: '值班告警' },
  { id: '2', name: '节点巡检' },
];

/** 编辑态初值：按 alarmRecordFormFromRow 产出的形态（时间=datetime-local） */
function prefilled(): AlarmRecordForm {
  return {
    id: '7',
    name: 'node-b 已下线',
    groupId: '2',
    category: '节点下线',
    action: 'node-b',
    status: 1,
    startTime: '2026-11-07T10:00',
    endTime: '',
    durationSec: 300,
    times: 1,
    content: '连续 6 次心跳丢失。',
  };
}

export const stories = [
  // 编辑态：回填记录行，提交上抛录制进 window.__recordSubmit（CT 断言表单对象）
  {
    id: 'AlarmRecordDrawer/Edit',
    component: AlarmRecordDrawer,
    props: {
      open: true,
      initial: prefilled(),
      title: '编辑告警记录 · node-b 已下线',
      sub: '/alarms',
      groups: GROUPS,
      busy: false,
      onSubmit: (form: AlarmRecordForm) => {
        const w = window as unknown as { __recordSubmit?: AlarmRecordForm[] };
        (w.__recordSubmit ??= []).push(form);
      },
    },
  },
  // 保存中态：按钮禁用 + 文案「保存中…」（§11）
  {
    id: 'AlarmRecordDrawer/Busy',
    component: AlarmRecordDrawer,
    props: { open: true, initial: prefilled(), title: '编辑告警记录 · node-b 已下线', sub: '/alarms', groups: GROUPS, busy: true },
  },
  // 关闭态（不渲染内容，占位回归）
  { id: 'AlarmRecordDrawer/Closed', component: AlarmRecordDrawer, props: { open: false, initial: null, title: '', sub: '', groups: GROUPS, busy: false } },
];
