import DeployPipelineRuns from './DeployPipelineRuns.vue';
import type { PipelineView, StageItem } from './deployPipeline.styles.ts';
import type { NodeResult, ShipRunRow } from './ship.logic.ts';

function run(id: number, patch: Partial<ShipRunRow>): ShipRunRow {
  return {
    key: `run-${id}`,
    id,
    version: `v1.4.${id}`,
    pipeline: 'build-main',
    trigger: 'Webhook',
    author: 'zhang',
    message: '',
    tone: 'ok',
    statusLabel: '成功',
    time: '10-28 10:00',
    active: false,
    status: 'Success',
    ...patch,
  };
}

const RUNS: ShipRunRow[] = [
  run(7, {}),
  run(6, { tone: 'err', statusLabel: '失败', author: 'li', trigger: '手动', time: '10-27 18:30', status: 'Failed' }),
  run(5, { tone: 'default', statusLabel: '已取消', time: '10-26 09:12', status: 'Cancelled' }),
];

// 四段：构建、上传已完成，分发进行中，部署未开始
const STAGES: StageItem[] = [
  { key: 'build', label: '构建', state: 'done' },
  { key: 'upload', label: '上传', state: 'done' },
  { key: 'dispatch', label: '分发', state: 'run' },
  { key: 'deploy', label: '部署', state: 'wait' },
];

function cell(i: number, cell: NodeResult['cell'], label: string, message = ''): NodeResult {
  return { key: `n${i}`, nodeId: i, name: `node-${String.fromCharCode(96 + i)}`, ip: `10.0.0.${i}`, cell, label, message };
}

// 八个格子，七种状态（ok 出现两次，图例里只算一种）
const ITEMS: NodeResult[] = [
  cell(1, 'ok', '已部署'),
  cell(2, 'ok', '已部署'),
  cell(3, 'fail', '部署失败', '连接超时'),
  cell(4, 'skip', '已跳过'),
  cell(5, 'off', '节点已停用'),
  cell(6, 'wait', '等待下发'),
  cell(7, 'ing', '部署中'),
  cell(8, 'none', '无部署记录'),
];

const VIEW: PipelineView = { items: ITEMS, allSkipped: false, note: '运行 #7 按节点的部署步骤' };

const BASE = { runRows: RUNS, selectedRunId: 7, stages: STAGES, view: VIEW };

export const stories = [
  // 测试状态：选中运行 #7，四段进度、八个节点格子、图例、三条运行记录
  { id: 'DeployPipelineRuns/Default', component: DeployPipelineRuns, props: { ...BASE } },

  // 测试状态：没有选中任何运行：不显示四段进度和状态标签，所有行都不高亮
  {
    id: 'DeployPipelineRuns/NoSelection',
    component: DeployPipelineRuns,
    props: {
      ...BASE,
      selectedRunId: null,
      stages: [],
      view: { items: ITEMS, allSkipped: false, note: '点一条运行记录查看按节点的部署步骤' },
    },
  },

  // 测试状态：所有部署步骤被跳过：单独的警告提示
  {
    id: 'DeployPipelineRuns/AllSkipped',
    component: DeployPipelineRuns,
    props: { ...BASE, view: { items: [cell(1, 'skip', '已跳过'), cell(2, 'skip', '已跳过')], allSkipped: true, note: '运行 #7 按节点的部署步骤' } },
  },

  // 测试状态：没有运行记录，也没有格子
  {
    id: 'DeployPipelineRuns/Empty',
    component: DeployPipelineRuns,
    props: { runRows: [], selectedRunId: null, stages: [], view: { items: [], allSkipped: false, note: '' } },
  },
];
