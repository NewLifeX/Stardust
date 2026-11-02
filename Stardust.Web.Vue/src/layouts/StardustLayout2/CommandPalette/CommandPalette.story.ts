import CommandPalette from './CommandPalette.vue';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
//
// CommandPalette 是纯 UI 组件：分组内容由 config 传入，内部状态与按键逻辑在 usePaletteState.ts。

const run = () => {};

export const stories = [
  // 测试状态：三个分组（页面 / 应用 / 节点）共 5 项，首项选中，渲染遮罩 + 面板
  {
    id: 'CommandPalette/Groups',
    component: CommandPalette,
    props: {
      config: {
        placeholder: '搜索应用、节点、TraceId,或执行操作…',
        groups: [
          {
            title: '页面',
            list: [
              { text: '跳转 · 此刻', hint: '首页', run },
              { text: '跳转 · 发布', hint: '远程发布', run },
            ],
          },
          {
            title: '应用',
            list: [
              { text: '应用 · OrderService', hint: '交易', run },
              { text: '应用 · IotWeb', run },
            ],
          },
          {
            title: '节点',
            list: [{ text: '节点 · bj-app-01', hint: '10.1.10.1', run }],
          },
        ],
      },
    },
  },

  // 测试状态：只有一个分组且仅一项，验证极简形态（面板高度收缩）
  {
    id: 'CommandPalette/Single',
    component: CommandPalette,
    props: {
      config: {
        placeholder: '连接哪台节点？',
        groups: [{ title: '在线节点', list: [{ text: 'bj-app-01', hint: '10.1.10.1', run }] }],
      },
    },
  },

  // 测试状态：空分组（无任何结果），验证空态不塌陷
  {
    id: 'CommandPalette/Empty',
    component: CommandPalette,
    props: {
      config: { placeholder: '搜索…', groups: [] },
    },
  },
];
