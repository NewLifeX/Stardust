import McpTokenForm from './McpTokenForm.vue';
import type { McpTokenFullRow } from './mcpToken.logic.ts';

const PROJECTS = [
  { id: 0, name: '未绑定' },
  { id: 7, name: '默认项目' },
  { id: 12, name: '支付网关' },
];
const USERS = [
  { id: 9, name: 'admin' },
  { id: 15, name: 'dev-zhang' },
];

const EDIT_TOKEN: McpTokenFullRow = {
  id: 1, name: 'claude-ops', masked: 'sdmcp_ab1…p56', enable: false,
  userId: 9, userName: 'admin', projectId: 7, projectName: '默认项目', scopes: 'apps:read,nodes:read,deploy:write',
  expireMs: Date.parse('2027-12-31T23:59:00'), expireText: '2027-12-31 23:59',
  callCount: 1280, lastText: '2026-10-29 14:02', lastIP: '10.0.0.5', remark: '运维助手小萌',
};

const MEMBERS = [
  { id: 1, projectId: 7, userId: 9, enable: true, userName: 'admin' },
  { id: 2, projectId: 7, userId: 15, enable: true, userName: 'dev-zhang' },
];

export const stories = [
  { id: 'McpTokenForm/Create', component: McpTokenForm, props: { open: true, mode: 'create', token: null, projects: PROJECTS, users: USERS, members: MEMBERS, membersReady: true, submitting: false } },
  { id: 'McpTokenForm/Edit', component: McpTokenForm, props: { open: true, mode: 'edit', token: EDIT_TOKEN, projects: PROJECTS, users: USERS, members: MEMBERS, membersReady: true, submitting: false } },
  { id: 'McpTokenForm/Submitting', component: McpTokenForm, props: { open: true, mode: 'create', token: null, projects: PROJECTS, users: USERS, members: MEMBERS, membersReady: true, submitting: true } },
];
