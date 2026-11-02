import McpTab from './McpTab.vue';
import type { McpServerRow } from './mesh.logic.ts';
import type { McpAuditFullRow, McpResourceRow, McpTokenFullRow } from './mcpToken.logic.ts';

const TOKENS: McpTokenFullRow[] = [
  {
    id: 1, name: 'claude-ops', masked: 'sdmcp_ab1…p56', enable: true,
    userId: 9, userName: 'admin', projectId: 7, projectName: '默认项目', scopes: 'apps:read,nodes:read,deploy:write',
    expireMs: 0, expireText: '永不过期', callCount: 1280, lastText: '2026-10-29 14:02', lastIP: '10.0.0.5', remark: '运维助手',
  },
  {
    id: 2, name: 'ci-bot', masked: 'sdmmp_cd2…q67', enable: true,
    userId: 0, userName: '', projectId: 7, projectName: '默认项目', scopes: '',
    expireMs: 0, expireText: '2026-12-31 23:59', callCount: 96, lastText: '2026-10-29 09:30', lastIP: '10.0.0.6', remark: '',
  },
  {
    id: 3, name: 'expired', masked: 'sdmmp_ef3…r78', enable: false,
    userId: 0, userName: '', projectId: 0, projectName: '—', scopes: 'apps:read',
    expireMs: 0, expireText: '2026-01-01 00:00', callCount: 0, lastText: '—', lastIP: '', remark: '已停用示例',
  },
];

const RESOURCES: McpResourceRow[] = [
  { id: 11, tokenId: 1, resourceType: 'Project', resourceTypeLabel: '项目', resourceId: 7, resourceName: '默认项目', isAll: false, enable: true },
  { id: 12, tokenId: 1, resourceType: 'App', resourceTypeLabel: '应用', resourceId: 21, resourceName: 'Orders.Api', isAll: false, enable: true },
];

const AUDITS: McpAuditFullRow[] = [
  { id: '1', tokenId: 1, tokenName: 'claude-ops', tool: 'invoke_action', action: 'node_send_command', toolLabel: 'invoke_action · node_send_command', ip: '10.0.0.5', ua: 'MCP-Client/1.0', args: '{"node":"node-a","script":"uptime"}', success: false, errorMessage: '高危动作需人工确认', duration: 12, traceId: 'tr-001', time: '2026-10-29 14:02', riskLevel: 2, projectId: 7, scopesUsed: 'apps:read,nodes:read,deploy:write', beforeValue: '{"node":"node-a","status":"running"}', afterValue: '{"node":"node-a","status":"restarting"}', timeMs: 0 },
  { id: '2', tokenId: 1, tokenName: 'claude-ops', tool: 'list_actions', action: '', toolLabel: 'list_actions', ip: '10.0.0.5', ua: 'MCP-Client/1.0', args: '', success: true, errorMessage: '', duration: 3, traceId: 'tr-002', time: '2026-10-29 13:58', riskLevel: 0, projectId: 7, scopesUsed: 'apps:read', beforeValue: '', afterValue: '', timeMs: 0 },
  { id: '3', tokenId: 2, tokenName: 'ci-bot', tool: 'get_server', action: '', toolLabel: 'get_server', ip: '10.0.0.6', ua: 'MCP-Client/1.0', args: '', success: true, errorMessage: '', duration: 5, traceId: '', time: '2026-10-29 13:55', riskLevel: 0, projectId: 0, scopesUsed: '', beforeValue: '', afterValue: '', timeMs: 0 },
];

const PROJECTS = [{ id: 7, name: '默认项目' }];
const USERS = [{ id: 9, name: 'admin' }];
const NODES = [{ id: 11, name: 'node-a' }];
const APPS = [{ id: 21, name: 'Orders.Api' }];

const SERVERS: McpServerRow[] = [
  { id: 1, name: 'stardust-core', url: 'http://localhost:6680/api/mcp', enable: true, tools: 18, projectId: 7, remark: '平台内置端点' },
  { id: 2, name: 'filesystem', url: 'http://node-a:6680/api/mcp', enable: false, tools: 0, projectId: 0, remark: '' },
];

const SERVERS_EMPTY = { servers: [] as McpServerRow[], serversError: '' };

const BASE = { resources: RESOURCES, audits: AUDITS, projects: PROJECTS, users: USERS, nodes: NODES, apps: APPS };

export const stories = [
  { id: 'McpTab/Data', component: McpTab, props: { tokens: TOKENS, ...BASE, servers: SERVERS, serversError: '' } },
  { id: 'McpTab/Empty', component: McpTab, props: { tokens: [], ...BASE, ...SERVERS_EMPTY } },
  { id: 'McpTab/Loading', component: McpTab, props: { tokens: [], loading: true, ...BASE, ...SERVERS_EMPTY } },
  { id: 'McpTab/Failed', component: McpTab, props: { tokens: [], error: 'Request failed with status 500', ...BASE, ...SERVERS_EMPTY } },
];
