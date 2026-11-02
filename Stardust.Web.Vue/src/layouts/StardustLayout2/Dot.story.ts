import Dot from './Dot.vue';

// story = 一个渲染变体，只描述「传什么 props」。Dot 没有插槽，可直接作为 component。
export const stories = [
  // 测试状态：默认实心灰点，7px 圆
  { id: 'Dot/Default', component: Dot, props: {} },

  // 测试状态：ok，绿点 + 60% 绿光晕
  { id: 'Dot/Ok', component: Dot, props: { tone: 'ok' } },

  // 测试状态：err，红点 + 70% 红光晕
  { id: 'Dot/Err', component: Dot, props: { tone: 'err' } },

  // 测试状态：warn，黄点，无光晕
  { id: 'Dot/Warn', component: Dot, props: { tone: 'warn' } },

  // 测试状态：off，空心圈，1.5px 灰描边 + 透明底
  { id: 'Dot/Off', component: Dot, props: { tone: 'off' } },
];
