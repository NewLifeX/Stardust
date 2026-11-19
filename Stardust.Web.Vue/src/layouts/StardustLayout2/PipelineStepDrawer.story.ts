import PipelineStepDrawer from './PipelineStepDrawer.vue';
import { toRunDetail } from './ship.logic.ts';
import type { ShipNode } from './ship.logic.ts';

// 手算步骤：Build(0) + 两节点 Deploy(1/2)，含 Running 未回包与 Skipped
const detail = toRunDetail({
  Id: 42,
  PipelineId: 7,
  Status: 'Success',
  steps: [
    { Id: 901, RunId: 42, StepType: 'Build', StepIndex: 0, NodeId: 0, CommandId: 5501, Status: 'Success', Message: 'Build completed', StartedTime: '2026-11-07T09:13:00', FinishedTime: '2026-11-07T09:19:00' },
    { Id: 902, RunId: 42, StepType: 'Deploy', StepIndex: 1, NodeId: 1, CommandId: 5502, Status: 'Success', Message: '安装成功 v20261107-090101', StartedTime: '2026-11-07T09:20:00', FinishedTime: '2026-11-07T09:21:00' },
    { Id: 903, RunId: 42, StepType: 'Deploy', StepIndex: 2, NodeId: 2, CommandId: 5503, Status: 'Failed', Message: '安装失败：404 包不存在', StartedTime: '2026-11-07T09:20:00', FinishedTime: '2026-11-07T09:20:40' },
    { Id: 904, RunId: 42, StepType: 'Deploy', StepIndex: 3, NodeId: 9, CommandId: 0, Status: 'Skipped', Message: '', StartedTime: '', FinishedTime: '' },
  ],
});

const NODES: ShipNode[] = [
  { id: 1, nodeId: 1, name: 'node-a', ip: '10.0.0.1', enable: true, version: '', appVersion: '', mode: 10, modeLabel: 'Standard', delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '—' },
  { id: 2, nodeId: 2, name: 'node-b', ip: '10.0.0.2', enable: true, version: '', appVersion: '', mode: 10, modeLabel: 'Standard', delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '—' },
];

export const stories = [
  // 测试状态：四步（Build+3 Deploy），节点 9 不在列表 → 兜底「节点 9」；Skipped 消息空显 —
  { id: 'PipelineStepDrawer/Default', component: PipelineStepDrawer, props: { open: true, detail, nodes: NODES } },

  // 测试状态：无步骤
  { id: 'PipelineStepDrawer/Empty', component: PipelineStepDrawer, props: { open: true, detail: { ...detail, steps: [] }, nodes: NODES } },
];
