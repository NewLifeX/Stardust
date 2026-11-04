import NodeFormDrawer from './NodeFormDrawer.vue';
import type { BuildNodeForm, DeployNodeForm } from './deploy.logic.ts';

const deployInitial: DeployNodeForm = {
  nodeId: 2,
  enable: true,
  port: 8080,
  fileName: '',
  arguments: '--urls http://*:8080',
  workingDirectory: '/opt/star',
  userName: '',
  environments: 'ENV=prod',
  maxMemory: 1024,
  delay: 5,
  healthCheck: 'http://*/health',
  remark: '内网节点',
  logDirectory: 'logs',
  logFilePattern: 'app-*.log',
};

const buildInitial: BuildNodeForm = {
  nodeId: 3,
  enable: true,
  sourcePath: '/src/MindHub.Api',
  pullCode: true,
  buildProject: true,
  packageOutput: true,
  uploadPackage: true,
  outputPath: './publish',
  remark: '',
};

export const stories = [
  // F07 新增部署节点：空表单 + 节点选择器（加载完成）
  {
    id: 'NodeFormDrawer/DeployCreate',
    component: NodeFormDrawer,
    props: {
      open: true,
      kind: 'deploy',
      initial: null,
      title: '新增部署节点',
      sub: '部署集 IoC容器',
      nodeOptions: [
        { id: 1, name: 'node-a', ip: '10.0.0.1' },
        { id: 2, name: 'node-b', ip: '10.0.0.2' },
      ],
      nodesLoading: false,
      busy: false,
    },
  },

  // F07 编辑部署节点：初值回填
  {
    id: 'NodeFormDrawer/DeployEdit',
    component: NodeFormDrawer,
    props: {
      open: true,
      kind: 'deploy',
      initial: deployInitial,
      title: '编辑部署节点 · node-b',
      sub: 'IP 10.0.0.2 · 编号 12',
      nodeOptions: [
        { id: 1, name: 'node-a', ip: '10.0.0.1' },
        { id: 2, name: 'node-b', ip: '10.0.0.2' },
      ],
      nodesLoading: false,
      busy: false,
    },
  },

  // F07 节点选择器加载中：选择器位置显示「节点列表加载中…」
  {
    id: 'NodeFormDrawer/NodesLoading',
    component: NodeFormDrawer,
    props: {
      open: true,
      kind: 'deploy',
      initial: null,
      title: '新增部署节点',
      sub: '部署集 IoC容器',
      nodeOptions: [],
      nodesLoading: true,
      busy: false,
    },
  },

  // F08 编辑编译节点：源代码目录、四个阶段开关
  {
    id: 'NodeFormDrawer/BuildEdit',
    component: NodeFormDrawer,
    props: {
      open: true,
      kind: 'build',
      initial: buildInitial,
      title: '编辑编译节点 · node-build-a',
      sub: 'IP 10.0.0.11 · 编号 21',
      nodeOptions: [{ id: 3, name: 'node-build-a', ip: '10.0.0.11' }],
      nodesLoading: false,
      busy: false,
    },
  },

  // F08 保存中：提交按钮禁用写「保存中…」（AGENTS.md §11）
  {
    id: 'NodeFormDrawer/Busy',
    component: NodeFormDrawer,
    props: {
      open: true,
      kind: 'build',
      initial: buildInitial,
      title: '编辑编译节点 · node-build-a',
      sub: 'IP 10.0.0.11 · 编号 21',
      nodeOptions: [{ id: 3, name: 'node-build-a', ip: '10.0.0.11' }],
      nodesLoading: false,
      busy: true,
    },
  },
];
