import DeployFeedbackStrip from './DeployFeedbackStrip.vue';
import type { DeployFeedbackItem } from './DeployFeedbackStrip.vue';

function item(key: number, ok: boolean, text: string, at: string, action?: DeployFeedbackItem['action']): DeployFeedbackItem {
  return { key, ok, text, at, action };
}

const BASE = {
  busyText: '',
  items: [] as DeployFeedbackItem[],
};

export const stories = [
  // 测试状态：触发流水线成功（带「查看运行」直达）+ 失败条目；对照 AGENTS.md §11 的可见反馈
  {
    id: 'DeployFeedbackStrip/Default',
    component: DeployFeedbackStrip,
    props: {
      ...BASE,
      items: [
        item(3, true, '流水线已触发，运行编号[42]', '10:21:08', { kind: 'openRun', label: '查看运行', runId: 42 }),
        item(2, false, '触发流水线 build-main 失败：流水线未启用', '10:19:44'),
        item(1, true, '部署集已保存', '10:15:02'),
      ],
    },
  },

  // 测试状态：操作进行中（busy 条），无反馈条目
  {
    id: 'DeployFeedbackStrip/Busy',
    component: DeployFeedbackStrip,
    props: { ...BASE, busyText: '正在触发流水线 build-main…' },
  },

  // 测试状态：空（busyText 空且无条目）→ 整条不渲染
  {
    id: 'DeployFeedbackStrip/Empty',
    component: DeployFeedbackStrip,
    props: { ...BASE },
  },
];
