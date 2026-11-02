import KIcon from './KIcon.vue';
import { KICONS, type KIconName } from './icons';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
//
// KIcon 是纯展示组件（name → icons.ts 里的 SVG 常量），
// 故 story 覆盖「图标清单抽样 + 尺寸」两类状态。

/** 抽样若干图标名，保证 gallery 侧栏不至于过长 */
const SAMPLE_ICONS: KIconName[] = ['Sparkles', 'Boxes', 'Server', 'Rocket', 'Shield'];

export const stories = [
  // 测试状态：默认尺寸 16，渲染 Sparkles 图标
  { id: 'KIcon/Default', component: KIcon, props: { name: 'Sparkles' } },

  // 测试状态：显式指定 size=20，验证 svg 宽高随 props 变化
  { id: 'KIcon/Size20', component: KIcon, props: { name: 'Sparkles', size: 20 } },

  // 测试状态：显式指定 size=12，小尺寸渲染
  { id: 'KIcon/Size12', component: KIcon, props: { name: 'Boxes', size: 12 } },
];

/** 图标清单快照用：导出全部图标名，便于测试断言 icons.ts 与渲染一致 */
export const ALL_ICON_NAMES = Object.keys(KICONS) as KIconName[];

export { SAMPLE_ICONS };
