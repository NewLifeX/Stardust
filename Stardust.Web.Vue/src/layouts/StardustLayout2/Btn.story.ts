import { defineComponent, h, type PropType } from 'vue';
import Btn from './Btn.vue';
import KIcon from './KIcon.vue';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
//
// Btn 的文字与图标在插槽里，ct/stories.ts 的条目只有 { id, component, props }，
// 所以用一个最小外壳把 props 翻译成插槽：label 进默认插槽，withIcon 进 icon 插槽。
const BtnStory = defineComponent({
  name: 'BtnStory',
  props: {
    variant: { type: String as PropType<'default' | 'primary' | 'danger' | 'ghost'>, default: 'default' },
    size: { type: String as PropType<'md' | 'sm'>, default: 'md' },
    disabled: { type: Boolean, default: false },
    withIcon: { type: Boolean, default: false },
    label: { type: String, default: '发布' },
  },
  setup(props) {
    return () =>
      h(
        Btn,
        { variant: props.variant, size: props.size, disabled: props.disabled },
        {
          default: () => props.label,
          ...(props.withIcon ? { icon: () => h(KIcon, { name: 'Rocket', size: 14 }) } : {}),
        },
      );
  },
});

export const stories = [
  // 测试状态：默认变体，surface-1 底 + border-2 描边
  { id: 'Btn/Default', component: BtnStory, props: { label: '取消' } },

  // 测试状态：primary，品牌色底 + 白字
  { id: 'Btn/Primary', component: BtnStory, props: { variant: 'primary', label: '发布' } },

  // 测试状态：danger，透明底 + 红字 + 40% 红描边
  { id: 'Btn/Danger', component: BtnStory, props: { variant: 'danger', label: '回滚' } },

  // 测试状态：ghost，透明底 + 透明描边 + 次级文字色
  { id: 'Btn/Ghost', component: BtnStory, props: { variant: 'ghost', label: '查看详情' } },

  // 测试状态：sm 尺寸，高 26、字号 12、圆角 6
  { id: 'Btn/Small', component: BtnStory, props: { variant: 'primary', size: 'sm', label: '执行' } },

  // 测试状态：禁用，透明度 0.4 且不响应指针
  { id: 'Btn/Disabled', component: BtnStory, props: { variant: 'primary', disabled: true, label: '发布' } },

  // 测试状态：带图标，图标在文字前，间距 6
  { id: 'Btn/WithIcon', component: BtnStory, props: { variant: 'primary', withIcon: true, label: '发布' } },
];