import DeployHistory from './DeployHistory.vue';
import type { HistoryRow, ShipNode } from './ship.logic.ts';

const NODES: ShipNode[] = [
  { id: 11, nodeId: 1, name: 'node-a', ip: '10.0.0.1', enable: true, version: 'v1.4.2', mode: 10, modeLabel: 'Standard' },
  { id: 12, nodeId: 2, name: 'node-b', ip: '10.0.0.2', enable: true, version: 'v1.4.1', mode: 11, modeLabel: 'Shadow' },
];

function row(id: number, patch: Partial<HistoryRow>): HistoryRow {
  return {
    id,
    deployId: 1,
    nodeId: 1,
    node: 'node-a',
    action: 'install',
    actionLabel: '安装',
    success: true,
    remark: '',
    traceId: `trace-${id}`,
    at: '2026-10-28T10:00:00',
    time: '10-28 10:00:00',
    ip: '192.168.1.8',
    ...patch,
  };
}

// 三条：node-a 安装成功；node-b 重启下发失败；流水线写的记录没有节点（nodeId 为 0）
const THREE: HistoryRow[] = [
  row(101, { remark: '安装 v1.4.2' }),
  row(102, { nodeId: 2, node: 'node-b', action: 'restart', actionLabel: '重启', success: false, remark: '连接超时' }),
  row(103, { nodeId: 0, node: '', action: 'deploy', actionLabel: 'deploy', remark: '流水线发布 #7', ip: '' }),
];

const BASE = { history: THREE, nodes: NODES };

export const stories = [
  // 测试状态：三条记录（成功、失败、无节点），点行展开详情，按节点可筛选
  { id: 'DeployHistory/Default', component: DeployHistory, props: { ...BASE } },

  // 测试状态：没有任何记录
  { id: 'DeployHistory/Empty', component: DeployHistory, props: { ...BASE, history: [] } },

  // 测试状态：加载失败（history 为 null，不能显示成「暂无部署历史」）
  { id: 'DeployHistory/Failed', component: DeployHistory, props: { ...BASE, history: null } },

  // 测试状态：已取回的记录里只有 node-a，筛选 node-b 时显示「最近这一页里没有该节点的记录」
  { id: 'DeployHistory/OnlyNodeA', component: DeployHistory, props: { ...BASE, history: [THREE[0]] } },
];
