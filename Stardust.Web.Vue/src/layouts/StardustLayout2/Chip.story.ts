import { defineComponent, h } from 'vue';
import Chip from './Chip.vue';

// Chip 的文字在默认插槽里，ct/stories.ts 的条目只有 { id, component, props }，
// 所以用一个最小外壳把 label 翻译成插槽。
const ChipStory = defineComponent({
  name: 'ChipStory',
  props: {
    on: { type: Boolean, default: false },
    label: { type: String, default: '全部' },
  },
  setup(props) {
    return () => h(Chip, { on: props.on }, { default: () => props.label });
  },
});

export const stories = [
  // 测试状态：未选中，描边灰、底为 bg-color、文字次级色，高 26
  { id: 'Chip/Off', component: ChipStory, props: { label: 'IoT 平台' } },

  // 测试状态：选中，品牌色文字 + 45% 描边 + 14% 底，aria-pressed=true
  { id: 'Chip/On', component: ChipStory, props: { on: true, label: '全部' } },
];
