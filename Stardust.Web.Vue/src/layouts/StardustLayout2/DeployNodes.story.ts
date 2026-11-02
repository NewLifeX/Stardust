import DeployNodes from './DeployNodes.vue';
import type { ShipNode } from './ship.logic.ts';

const NODES: ShipNode[] = [
  { id: 11, nodeId: 1, name: 'node-a', ip: '10.0.0.1', enable: true, version: 'v1.4.2', mode: 10, modeLabel: 'Standard' },
  { id: 12, nodeId: 2, name: 'node-b', ip: '10.0.0.2', enable: true, version: 'v1.4.1', mode: 11, modeLabel: 'Shadow' },
  { id: 13, nodeId: 3, name: 'node-c', ip: '', enable: false, version: '', mode: 12, modeLabel: 'Hosted' },
];

// 版本标签由容器用 nodeVersionTag 算好，按节点行 id 传入；停用的节点没有标签
const TAGS = {
  11: { tone: 'ok', label: '与部署集一致' },
  12: { tone: 'warn', label: '与部署集不一致' },
  13: null,
};

const BASE = {
  nodes: NODES,
  nodesFailed: false,
  busyKey: '',
  versionTags: TAGS,
};

export const stories = [
  // 测试状态：三个节点（一致、不一致、已停用），每行五个操作按钮
  { id: 'DeployNodes/Default', component: DeployNodes, props: { ...BASE } },

  // 测试状态：正在重启 node-b（行 id 12）：该按钮写「下发中…」，所有操作按钮禁用
  { id: 'DeployNodes/Busy', component: DeployNodes, props: { ...BASE, busyKey: '12:restart' } },

  // 测试状态：没有节点
  { id: 'DeployNodes/Empty', component: DeployNodes, props: { ...BASE, nodes: [], versionTags: {} } },

  // 测试状态：节点加载失败（不能显示成「暂无节点」）
  { id: 'DeployNodes/Failed', component: DeployNodes, props: { ...BASE, nodes: [], versionTags: {}, nodesFailed: true } },
];
