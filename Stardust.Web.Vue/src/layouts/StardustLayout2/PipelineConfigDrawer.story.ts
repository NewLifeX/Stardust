import PipelineConfigDrawer from './PipelineConfigDrawer.vue';
import type { BuildNodeSearchItem, PipelineForm } from './pipelineConfig.logic.ts';

const initial: PipelineForm = {
  name: 'build-main',
  branch: 'release/*',
  buildNodeId: 3,
  deployNodeIds: [1, 2],
  autoDeploy: true,
  enable: true,
};

const NODE_OPTIONS = [
  { id: 1, label: 'node-a (10.0.0.1)' },
  { id: 2, label: 'node-b (10.0.0.2)' },
];

async function searchOk(key: string): Promise<BuildNodeSearchItem[]> {
  return [{ id: 3, name: 'build-a', ip: '10.0.0.11', projectName: 'StarDeploy' }].filter((s) => s.name.includes(key));
}

async function searchFail(): Promise<BuildNodeSearchItem[] | null> {
  return null;
}

export const stories = [
  // 测试状态：编辑回填（分支通配、编译节点 #3、部署节点 1/2 勾选、自动部署/启用开、项目级联 #88）
  {
    id: 'PipelineConfigDrawer/Edit',
    component: PipelineConfigDrawer,
    props: {
      open: true,
      initial,
      title: '编辑流水线 · build-main',
      sub: '部署集 Stardust',
      projectId: 88,
      nodeOptions: NODE_OPTIONS,
      nodesLoading: false,
      searchNodes: searchOk,
      busy: false,
    },
  },

  // 测试状态：新增空表单；部署节点加载中
  {
    id: 'PipelineConfigDrawer/Create',
    component: PipelineConfigDrawer,
    props: {
      open: true,
      initial: null,
      title: '新增流水线',
      sub: '部署集 Stardust',
      projectId: 0,
      nodeOptions: [],
      nodesLoading: true,
      searchNodes: searchOk,
      busy: false,
    },
  },

  // 测试状态：保存中 → 提交按钮禁用「保存中…」（AGENTS.md §11）
  {
    id: 'PipelineConfigDrawer/Busy',
    component: PipelineConfigDrawer,
    props: {
      open: true,
      initial,
      title: '编辑流水线 · build-main',
      sub: '部署集 Stardust',
      projectId: 88,
      nodeOptions: NODE_OPTIONS,
      nodesLoading: false,
      searchNodes: searchOk,
      busy: true,
    },
  },

  // 测试状态：部署节点为空（无启用节点）+ suggest 查询失败降级文案
  {
    id: 'PipelineConfigDrawer/NodesEmpty',
    component: PipelineConfigDrawer,
    props: {
      open: true,
      initial: null,
      title: '新增流水线',
      sub: '部署集 Stardust',
      projectId: 0,
      nodeOptions: [],
      nodesLoading: false,
      searchNodes: searchFail,
      busy: false,
    },
  },
];
