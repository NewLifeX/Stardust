import DeployNodes from './DeployNodes.vue';
import type { ShipNode } from './ship.logic.ts';

const NODES: ShipNode[] = [
  // task-92 列扩展：node-a 五列全有值；node-b 部署了但进程/心跳未上报；node-c 停用全空（时间列由 toShipNodes 用 fmtTime 格式化，未上报为「—」）
  // 2026-11-12 新列 appVersion（应用程序版本）：node-a 在线实例已上报；node-b/c 无在线实例为空串
  { id: 11, nodeId: 1, name: 'node-a', ip: '10.0.0.1', enable: true, version: 'v1.4.2', appVersion: '1.4.2.0', mode: 10, modeLabel: 'Standard', delay: 30, processId: 61000, startTime: '2026-11-01 19:09', compile: '2026-10-30 11:20', lastActive: '2026-11-01 19:09' },
  { id: 12, nodeId: 2, name: 'node-b', ip: '10.0.0.2', enable: true, version: 'v1.4.1', appVersion: '', mode: 11, modeLabel: 'Shadow', delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '—' },
  { id: 13, nodeId: 3, name: 'node-c', ip: '', enable: false, version: '', appVersion: '', mode: 12, modeLabel: 'Hosted', delay: 0, processId: 0, startTime: '—', compile: '—', lastActive: '—' },
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
  // 测试状态：三个节点（一致、不一致、已停用），每行五个操作按钮 + 编辑/删除；批量栏隐藏（未勾选）
  { id: 'DeployNodes/Default', component: DeployNodes, props: { ...BASE } },

  // 测试状态：多集应用非空态（CARD-DEP-08 D2）：底部副行显示其余集数与启用节点总数
  { id: 'DeployNodes/Others', component: DeployNodes, props: { ...BASE, deployName: '主集', others: { count: 2, nodes: 6 } } },

  // 测试状态：正在重启 node-b（行 id 12）：该按钮写「下发中…」，所有操作按钮禁用
  { id: 'DeployNodes/Busy', component: DeployNodes, props: { ...BASE, busyKey: '12:restart' } },

  // 测试状态：批量删除进行中（F07）：批量栏按钮写「下发中…」，行内按钮全禁用
  { id: 'DeployNodes/BatchBusy', component: DeployNodes, props: { ...BASE, busyKey: 'batch:uninstall' } },

  // 测试状态：没有节点：空态主句 + 引导按钮（CARD-DEP-08 D4）
  { id: 'DeployNodes/Empty', component: DeployNodes, props: { ...BASE, nodes: [], versionTags: {}, deployName: 'DEP08-Set4' } },

  // 测试状态：多集应用空态（CARD-DEP-08 D2/D4）：空态主句 + 引导 + 其余集副行
  { id: 'DeployNodes/EmptyOthers', component: DeployNodes, props: { ...BASE, nodes: [], versionTags: {}, deployName: '主集', others: { count: 2, nodes: 6 } } },

  // 测试状态：节点加载失败（不能显示成「暂无节点」）
  { id: 'DeployNodes/Failed', component: DeployNodes, props: { ...BASE, nodes: [], versionTags: {}, nodesFailed: true } },
];
