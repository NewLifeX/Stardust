import { defineComponent, h, ref, watch } from 'vue';
import Drawer from './Drawer.vue';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
//
// Drawer 的 open 由父组件持有，关闭（X、遮罩、Esc）只发 close。
// 所以这里用一个最小外壳：自己持有 open，收到 close 就置 false；
// actions 与正文插槽只放占位文字，不放业务数据。
const DrawerStory = defineComponent({
  name: 'DrawerStory',
  props: {
    open: { type: Boolean, default: true },
    title: { type: String, default: '' },
    sub: { type: String, default: '' },
    withActions: { type: Boolean, default: false },
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
      h(
        Drawer,
        {
          open: visible.value,
          title: props.title,
          sub: props.sub,
          onClose: () => {
            visible.value = false;
          },
        },
        {
          default: () => h('p', { 'data-spec': 'story-body', class: 'text-[13px]' }, '正文区'),
          ...(props.withActions
            ? {
                actions: () =>
                  h(
                    'button',
                    {
                      type: 'button',
                      'data-spec': 'story-action',
                      class:
                        'rounded-[8px] border border-[var(--el-border-color)] px-3 py-1 text-[13px] text-[var(--el-text-color-primary)]',
                    },
                    '操作',
                  ),
              }
            : {}),
        },
      );
  },
});

export const stories = [
  // 测试状态：标题 + 副标题，渲染遮罩 + 抽屉头部
  {
    id: 'Drawer/Basic',
    component: DrawerStory,
    props: { open: true, title: 'bj-app-01', sub: '10.1.10.1' },
  },

  // 测试状态：没有副标题，头部只剩标题，副标题行不占位
  {
    id: 'Drawer/NoSub',
    component: DrawerStory,
    props: { open: true, title: 'bj-app-01' },
  },

  // 测试状态：带操作按钮行（actions 插槽）
  {
    id: 'Drawer/WithActions',
    component: DrawerStory,
    props: { open: true, title: 'bj-app-01', sub: '10.1.10.1', withActions: true },
  },

  // 测试状态：关闭态，验证不渲染可见内容
  {
    id: 'Drawer/Closed',
    component: DrawerStory,
    props: { open: false, title: 'bj-app-01' },
  },
];
