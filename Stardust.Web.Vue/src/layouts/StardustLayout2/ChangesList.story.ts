import ChangesList from './ChangesList.vue';
import type { ChangeItem } from './changes.logic.ts';

// 混合 D/C 时间线（已按时间倒序）：D 部署历史、D 流水线、C 配置
const MIXED: ChangeItem[] = [
  {
    id: 'p-9', kind: 'D', appName: 'OrderService', appId: 101,
    time: '2026-10-28 10:00', sortAt: Date.parse('2026-10-28T10:00:00'),
    summary: 'feat: 接入支付回调重试，超时改为 3s', operator: '#7', path: '/app/pipeline/101', ok: true,
  },
  {
    id: 'd-1', kind: 'D', appName: 'OrderService', appId: 101,
    time: '2026-10-28 09:30', sortAt: Date.parse('2026-10-28T09:30:00'),
    summary: '安装 · node-a', operator: '#7', path: '/app/history/101', ok: true,
  },
  {
    id: 'c-5', kind: 'C', appName: 'IotWeb', appId: 202,
    time: '2026-10-28 08:00', sortAt: Date.parse('2026-10-28T08:00:00'),
    summary: '发布配置', operator: '#3', path: '/app/config/202', ok: true,
  },
  {
    id: 'd-2', kind: 'D', appName: 'IotWeb', appId: 202,
    time: '2026-10-27 19:12', sortAt: Date.parse('2026-10-27T19:12:00'),
    summary: '重启 · node-b', operator: '', path: '/app/history/202', ok: false,
  },
];

export const stories = [
  // 有数据：D/C 混合时间线，D 品牌色方块、C 灰方块，成功绿 / 失败红 Tag
  { id: 'ChangesList/Mixed', component: ChangesList, props: { items: MIXED } },

  // 空态：没有变更记录
  { id: 'ChangesList/Empty', component: ChangesList, props: { items: [] } },

  // 加载态：spinner 旋转
  { id: 'ChangesList/Loading', component: ChangesList, props: { items: [], loading: true } },

  // 失败态：可见错误文案，不当作空态
  { id: 'ChangesList/Failed', component: ChangesList, props: { items: [], error: 'Request failed with status 500' } },

  // 点击录制：点行把 item.path 追加进 window.__changesSelect（CT 点击用例读它）
  {
    id: 'ChangesList/SelectRecorder',
    component: ChangesList,
    props: {
      items: MIXED,
      onSelect: (item: ChangeItem) => {
        const w = window as unknown as { __changesSelect?: string[] };
        (w.__changesSelect ??= []).push(item.path);
      },
    },
  },
];
