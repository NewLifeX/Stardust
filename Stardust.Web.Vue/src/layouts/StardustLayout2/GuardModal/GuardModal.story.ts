import { defineComponent, h, ref } from 'vue';
import GuardModal from './GuardModal.vue';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
//
// GuardModal 只发 cancel / confirm，不自己关闭。
// 所以这里用一个最小外壳：把收到的事件写进 data-spec="story-result"，供 CT 断言「点了哪个」。
// 结果 span 用 sr-only，不占画面，也不进截图。
const GuardModalStory = defineComponent({
  name: 'GuardModalStory',
  props: {
    label: { type: String, default: '' },
  },
  setup(props) {
    const result = ref('');
    return () => [
      h(GuardModal, {
        label: props.label,
        onCancel: () => {
          result.value = 'cancel';
        },
        onConfirm: () => {
          result.value = 'confirm';
        },
      }),
      h('span', { 'data-spec': 'story-result', class: 'sr-only' }, result.value),
    ];
  },
});

export const stories = [
  // 测试状态：典型文案，渲染遮罩 + 警示条 + 标题 + 副文字 + 两个按钮
  {
    id: 'GuardModal/Basic',
    component: GuardModalStory,
    props: { label: '回滚 IotWeb 配置到 v12' },
  },

  // 测试状态：长标题，验证在 660px 宽的弹窗里自然换行，不撑破布局
  {
    id: 'GuardModal/LongLabel',
    component: GuardModalStory,
    props: {
      label: '发布 IotWeb 的配置（12 项变更）到全部 6 个在线实例，并在灰度 25% 后等待人工确认再继续推送剩余实例',
    },
  },
];