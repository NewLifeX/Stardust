import NavSegment from './NavSegment.vue';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
// 新增变体只需往数组里加一项；gallery 自动收集。
//
// 每个 story 上方用「测试状态：」注释标明该变体要验证的运行时状态，
// 使 story 与 CT 截图测试一一对应。

export const stories = [
  // 测试状态：未选中任何项（active=null），五项均为次要色、无高亮底
  { id: 'NavSegment/None', component: NavSegment, props: { active: null } },

  // 测试状态：选中「此刻」（now），该项有高亮底 + 左侧圆点，其余为次要色
  { id: 'NavSegment/ActiveNow', component: NavSegment, props: { active: 'now' } },

  // 测试状态：选中「应用」（apps），验证高亮随 active 移动
  { id: 'NavSegment/ActiveApps', component: NavSegment, props: { active: 'apps' } },

  // 测试状态：选中「治理」（mesh），验证末项高亮
  { id: 'NavSegment/ActiveMesh', component: NavSegment, props: { active: 'mesh' } },

  // 测试状态：选中「此刻」且 nowCount=3，「此刻」项右侧显示红色计数徽标
  { id: 'NavSegment/ActiveNowWithCount', component: NavSegment, props: { active: 'now', nowCount: 3 } },

  // 测试状态：选中「此刻」但 nowCount=0，计数徽标不显示（0 不显示）
  { id: 'NavSegment/ActiveNowZeroCount', component: NavSegment, props: { active: 'now', nowCount: 0 } },
];
