import AppTabs from './AppTabs.vue';
import { WORKBENCH_TABS, tabLabel } from './workbench.logic';

// story = 一个渲染变体，只描述「传什么 props」。AppTabs 没有插槽，可直接作为 component。
const items = WORKBENCH_TABS.map((t) => ({ key: t.key, label: tabLabel(t.key, 3) }));

export const stories = [
  // 测试状态：概览激活，「实例」页签带实例数 3
  { id: 'AppTabs/Overview', component: AppTabs, props: { items, active: 'overview' } },

  // 测试状态：版本激活（部署四个页签之一，停点 19 起没有「发布」页签）
  { id: 'AppTabs/Versions', component: AppTabs, props: { items, active: 'versions' } },

  // 测试状态：实例数为 0 也要显示「实例 0」
  {
    id: 'AppTabs/NoInstance',
    component: AppTabs,
    props: { items: WORKBENCH_TABS.map((t) => ({ key: t.key, label: tabLabel(t.key, 0) })), active: 'instances' },
  },
];
