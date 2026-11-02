import { defineComponent, h, ref, watch } from 'vue';
import InstallWizard from './InstallWizard.vue';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
// InstallWizard 的 open 由父组件持有，关闭只发 close。
// 用最小外壳持有 open，收到 close 就置 false。
const InstallWizardStory = defineComponent({
  name: 'InstallWizardStory',
  props: {
    open: { type: Boolean, default: true },
  },
  setup(props) {
    const visible = ref(props.open);
    watch(
      () => props.open,
      (v) => {
        visible.value = v;
      },
    );
    return () =>
      h(InstallWizard, {
        open: visible.value,
        onClose: () => {
          visible.value = false;
        },
      });
  },
});

export const stories = [
  // 测试状态：打开态，显示安装命令 + 等待上线空态
  {
    id: 'InstallWizard/Basic',
    component: InstallWizardStory,
    props: { open: true },
  },

  // 测试状态：关闭态，验证不渲染
  {
    id: 'InstallWizard/Closed',
    component: InstallWizardStory,
    props: { open: false },
  },
];
