import McpTokenResourcePanel from './McpTokenResourcePanel.vue';
import type { McpResourceRow, McpTokenFullRow } from './mcpToken.logic.ts';

const TOKEN: McpTokenFullRow = {
  id: 1, name: 'claude-ops', masked: 'sdmcp_ab1…p56', enable: true,
  userId: 9, userName: 'admin', projectId: 7, projectName: '默认项目', scopes: 'apps:read',
  expireMs: 0, expireText: '永不过期', callCount: 1280, lastText: '2026-10-29 14:02', lastIP: '10.0.0.5', remark: '',
};

const RESOURCES: McpResourceRow[] = [
  { id: 11, tokenId: 1, resourceType: 'Project', resourceTypeLabel: '项目', resourceId: 7, resourceName: '默认项目', isAll: false, enable: true },
  { id: 12, tokenId: 1, resourceType: 'Node', resourceTypeLabel: '节点', resourceId: 11, resourceName: 'node-a', isAll: true, enable: true },
  { id: 13, tokenId: 1, resourceType: 'App', resourceTypeLabel: '应用', resourceId: 21, resourceName: 'Orders.Api', isAll: false, enable: false },
];

const PROJECTS = [{ id: 7, name: '默认项目' }, { id: 12, name: '支付网关' }];
const NODES = [{ id: 11, name: 'node-a' }, { id: 12, name: 'node-b' }];
const APPS = [{ id: 21, name: 'Orders.Api' }];

export const stories = [
  // 选中令牌 + 已有授权
  { id: 'McpTokenResourcePanel/WithToken', component: McpTokenResourcePanel, props: { token: TOKEN, resources: RESOURCES, projects: PROJECTS, nodes: NODES, apps: APPS, busy: false } },
  // 未选令牌：提示在上方选择
  { id: 'McpTokenResourcePanel/NoToken', component: McpTokenResourcePanel, props: { token: null, resources: RESOURCES, projects: PROJECTS, nodes: NODES, apps: APPS, busy: false } },
  // 选中令牌但无授权：空态说明
  { id: 'McpTokenResourcePanel/EmptyResources', component: McpTokenResourcePanel, props: { token: TOKEN, resources: [], projects: PROJECTS, nodes: NODES, apps: APPS, busy: false } },
];
