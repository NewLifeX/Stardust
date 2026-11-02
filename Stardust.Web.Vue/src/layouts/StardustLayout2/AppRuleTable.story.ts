import AppRuleTable from './AppRuleTable.vue';
import type { AppRuleRow } from './configCenter.logic.ts';

const rows: AppRuleRow[] = [
  { id: 2, rule: 'IP=10.*,172.*;LocalIP=192.*', result: 'Scope=dev', priority: 9, enable: true, remark: '', updateTimeText: '2026-11-03 09:30' },
  { id: 1, rule: 'IP=192.168.*', result: 'Scope=prod', priority: 1, enable: false, remark: '生产段', updateTimeText: '2026-11-02 18:00' },
];

export const stories = [
  // F11 列表：两行，启用/停用标签
  {
    id: 'AppRuleTable/Default',
    component: AppRuleTable,
    props: { rows, loading: false, error: '', removingId: 0, busy: false },
  },

  // F11 删除中：行内删除按钮写「删除中…」
  {
    id: 'AppRuleTable/Removing',
    component: AppRuleTable,
    props: { rows, loading: false, error: '', removingId: 1, busy: false },
  },

  // F11 空态：引导新增
  {
    id: 'AppRuleTable/Empty',
    component: AppRuleTable,
    props: { rows: [], loading: false, error: '', removingId: 0, busy: false },
  },

  // F11 加载失败：错误 + 重试
  {
    id: 'AppRuleTable/Failed',
    component: AppRuleTable,
    props: { rows: [], loading: false, error: '加载失败', removingId: 0, busy: false },
  },

  // F11 加载中
  {
    id: 'AppRuleTable/Loading',
    component: AppRuleTable,
    props: { rows: [], loading: true, error: '', removingId: 0, busy: false },
  },
];
