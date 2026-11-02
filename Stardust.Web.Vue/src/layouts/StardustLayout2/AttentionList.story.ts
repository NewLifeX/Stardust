import AttentionList from './AttentionList.vue';
import type { AttentionItem } from './overview.logic.ts';

// 四项的固定骨架（标题、跳转页签、按钮文案与 buildAttention 一致），各 story 只换语气与详情
const versions = { key: 'versions', title: '节点版本', tab: 'nodes', actionLabel: '去节点' } as const;
const pipeline = { key: 'pipeline', title: '流水线', tab: 'pipeline', actionLabel: '看流水线' } as const;
const history = { key: 'history', title: '部署下发', tab: 'history', actionLabel: '看部署历史' } as const;
const instances = { key: 'instances', title: '在线实例', tab: 'instances', actionLabel: '看实例' } as const;

const MIXED: AttentionItem[] = [
  { ...versions, tone: 'warn', detail: '2 个启用节点版本与部署集 1.2.0 不一致' },
  { ...pipeline, tone: 'ok', detail: '最近一次运行成功（main）' },
  { ...history, tone: 'info', detail: '还没有部署下发记录' },
  { ...instances, tone: 'info', detail: '3 个在线实例' },
];

const ALL_PENDING: AttentionItem[] = [
  { ...versions, tone: 'pending', detail: '没有部署集' },
  { ...pipeline, tone: 'pending', detail: '没有部署集' },
  { ...history, tone: 'pending', detail: '没有部署集' },
  { ...instances, tone: 'pending', detail: '在线实例数待接入' },
];

const ALL_OK: AttentionItem[] = [
  { ...versions, tone: 'ok', detail: '4 个启用节点版本都与部署集 1.2.0 一致' },
  { ...pipeline, tone: 'ok', detail: '最近一次运行成功（main）' },
  { ...history, tone: 'ok', detail: '最近一次下发（node-a 安装）：命令已发出，是否生效请看节点版本' },
  { ...instances, tone: 'info', detail: '3 个在线实例' },
];

export const stories = [
  // 测试状态：warn、ok、info 混合；warn 项详情用警告色、黄点；没有「待接入」标签；每行右侧一个跳转按钮
  { id: 'AttentionList/Mixed', component: AttentionList, props: { items: MIXED } },

  // 测试状态：四项都取不到，空心圈加「待接入」标签，不当作正常
  { id: 'AttentionList/AllPending', component: AttentionList, props: { items: ALL_PENDING } },

  // 测试状态：版本一致、流水线与下发成功，绿点；最后一行没有下边线
  { id: 'AttentionList/AllOk', component: AttentionList, props: { items: ALL_OK } },
  // 测试状态：点每个跳转按钮，onGo 把收到的页签依次追加进 window.__attentionGo（CT 点击用例读它），用来确认按钮发出的是 nodes、pipeline、history、instances
  {
    id: 'AttentionList/GoRecorder',
    component: AttentionList,
    props: {
      items: MIXED,
      onGo: (tab: string) => {
        const w = window as unknown as { __attentionGo?: string[] };
        (w.__attentionGo ??= []).push(tab);
      },
    },
  },
];
