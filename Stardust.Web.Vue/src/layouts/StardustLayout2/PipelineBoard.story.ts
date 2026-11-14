import PipelineBoard from './PipelineBoard.vue';
import type { PipelineCard } from './pipelineForm.logic.ts';

const cards: PipelineCard[] = [
  {
    id: 5, name: '测试-MindHub', deployId: 25, projectId: 2, branch: 'main', buildNodeId: 2,
    deployNodeIds: null, autoDeploy: true, enable: true, remark: '',
  },
  {
    id: 6, name: '停用流水线', deployId: 25, projectId: 2, branch: 'dev', buildNodeId: 0,
    deployNodeIds: null, autoDeploy: false, enable: false, remark: '',
  },
];

export const stories = [
  {
    id: 'PipelineBoard/Default',
    component: PipelineBoard,
    props: { cards, failed: false, busyKey: '', buildLabels: { 2: 'DGCWIAX10-Win' }, copyingId: 0 },
  },
  {
    id: 'PipelineBoard/Empty',
    component: PipelineBoard,
    props: { cards: [], failed: false, busyKey: '', buildLabels: {}, copyingId: 0 },
  },
  {
    id: 'PipelineBoard/Failed',
    component: PipelineBoard,
    props: { cards: [], failed: true, busyKey: '', buildLabels: {}, copyingId: 0 },
  },
];
