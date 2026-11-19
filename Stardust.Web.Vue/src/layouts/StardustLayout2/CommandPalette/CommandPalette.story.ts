import CommandPalette from './CommandPalette.vue';
import { buildBasePalette } from '../palette.items';

// story = 一个渲染变体。只描述「传什么 props」，不手写任何 demo 页。
//
// CommandPalette 是纯 UI 组件：分组内容由 config 传入，内部状态与按键逻辑在 usePaletteState.ts。

const run = () => {};

// TraceId 变体：用生产同一来源 buildBasePalette 构造（go/goTrace 经 window 捕获，CT 断言跳转）。
// 参照反模式 #236：函数 props 由 story 捕获 + window 取值断言（2026-11-16 输入2）。
const captured = { go: '', trace: '' };
const traceConfig = buildBasePalette({
  go: (p) => {
    captured.go = p;
    (window as unknown as { __paletteGo?: string }).__paletteGo = p;
  },
  goTrace: (ids) => {
    captured.trace = ids;
    (window as unknown as { __paletteTrace?: string }).__paletteTrace = ids;
  },
});

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

  // 测试状态：带 goTrace 的基础面板（与顶栏搜索/⌘K 生产同源构造），
  // 输入像 TraceId 时追加「链路追踪」组，回车跳 /trace（go/goTrace 经 window 捕获）
  {
    id: 'CommandPalette/Trace',
    component: CommandPalette,
    props: {
      config: traceConfig,
    },
  },
];
