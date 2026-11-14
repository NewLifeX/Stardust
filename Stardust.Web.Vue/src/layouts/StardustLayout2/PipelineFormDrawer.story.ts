import PipelineFormDrawer from './PipelineFormDrawer.vue';
import type { PipelineForm } from './pipelineForm.logic.ts';

const initial: PipelineForm = {
  name: '测试-MindHub',
  branch: 'main',
  buildNodeId: 2,
  deployNodeIds: [73],
  autoDeploy: true,
  enable: true,
  remark: '',
};

const compileChoices = [{ nodeId: 2, label: 'DGCWIAX10-Win' }];
const deployChoices = [{ id: 73, label: 'node-a · 10.0.0.8' }];

export const stories = [
  {
    id: 'PipelineFormDrawer/Create',
    component: PipelineFormDrawer,
    props: { open: true, pipelineId: null, initial: null, busy: false, loading: false, compileChoices, deployChoices },
  },
  {
    id: 'PipelineFormDrawer/Edit',
    component: PipelineFormDrawer,
    props: { open: true, pipelineId: 5, initial, busy: false, loading: false, compileChoices, deployChoices },
  },
  {
    id: 'PipelineFormDrawer/Busy',
    component: PipelineFormDrawer,
    props: { open: true, pipelineId: 5, initial, busy: true, loading: false, compileChoices, deployChoices },
  },
];
