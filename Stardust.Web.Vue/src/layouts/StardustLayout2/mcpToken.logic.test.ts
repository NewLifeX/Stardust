// MCP 令牌管理纯逻辑单测（Vitest）：request 用桩函数注入，不发真实请求。
import { describe, it, expect } from 'vitest';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  addResource,
  buildAuditQueryParams,
  createMcpServer,
  createToken,
  deleteMcpServer,
  deleteResource,
  deleteToken,
  fetchMcpAuditPage,
  fetchMcpManage,
  fetchMcpRuntime,
  filterAudits,
  filterResourcesByToken,
  formatBoundUser,
  maskTokenFull,
  parseScopes,
  previewDefaultProjectScopes,
  resetToken,
  resourceLabel,
  scopeLabel,
  setMcpEnabled,
  setResourceEnable,
  setServerEnable,
  setTokenEnable,
  toAuditRows,
  toResourceRows,
  toTokenRows,
  updateMcpServer,
  updateToken,
} from './mcpToken.logic';

// 桩：按 method+url 匹配路由，记录每次调用的 params
interface Route {
  method: string;
  url: string;
  reply: () => unknown;
}
function recording(routes: Route[]): { req: Req; calls: ReqOptions[] } {
  const calls: ReqOptions[] = [];
  const req = (async (o: ReqOptions) => {
    calls.push(o);
    const hit = routes.find((r) => r.method === o.method && r.url === o.url);
    return hit ? hit.reply() : { code: 0, data: [] };
  }) as Req;
  return { req, calls };
}

describe('maskTokenFull · 脱敏', () => {
  it('正常令牌：前 8 + … + 后 4', () => {
    expect(maskTokenFull('sk-mcp-ab12cd34ef56gh78ij90kl12mn34op56qr78st90')).toBe('sk-mcp-a…st90');
    expect(maskTokenFull('sdmcp_ab12cd34ef56gh78ij90kl12mn34op56')).toBe('sdmcp_ab…op56');
  });
  it('空值显示 —', () => {
    expect(maskTokenFull('')).toBe('—');
    expect(maskTokenFull(null)).toBe('—');
  });
  it('过短令牌不暴露中间', () => {
    expect(maskTokenFull('abcde')).toBe('abcd****');
  });
});

describe('formatBoundUser / previewDefaultProjectScopes', () => {
  it('formatBoundUser：未绑定与具名用户', () => {
    expect(formatBoundUser(0, '')).toBe('—');
    expect(formatBoundUser(2, '70298304')).toBe('70298304 (#2)');
    expect(formatBoundUser(9, '')).toBe('#9');
  });
  it('previewDefaultProjectScopes：绑项目且 Scope 空时返回中文预览', () => {
    const p = previewDefaultProjectScopes(3, '');
    expect(p).toContain('应用·读');
    expect(p).toContain('平台·读');
    expect(previewDefaultProjectScopes(0, '')).toBe('');
    expect(previewDefaultProjectScopes(3, 'apps:read')).toBe('');
  });
});

describe('parseScopes / scopeLabel / resourceLabel', () => {
  it('parseScopes：csv 拆分去空去空白', () => {
    expect(parseScopes('apps:read, nodes:write,,')).toEqual(['apps:read', 'nodes:write']);
    expect(parseScopes('')).toEqual([]);
  });
  it('scopeLabel：域与级别中文名', () => {
    expect(scopeLabel('apps:read')).toBe('应用·读');
    expect(scopeLabel('deploy:admin')).toBe('部署·高危');
    expect(scopeLabel('unknown:x')).toBe('unknown:x');
  });
  it('resourceLabel：Project/Node/App', () => {
    expect(resourceLabel('Project')).toBe('项目');
    expect(resourceLabel('Node')).toBe('节点');
    expect(resourceLabel('App')).toBe('应用');
  });
});

describe('toTokenRows · 令牌行映射', () => {
  const projects = new Map<number, string>([[7, '默认项目']]);
  const users = new Map<number, string>([[9, 'admin']]);
  it('PascalCase 字段正确映射，项目名解析', () => {
    const rows = toTokenRows([{
      Id: 3, Name: 'claude-ops', Token: 'sdmcp_ab12cd34ef56gh78ij90kl12mn34op56',
      Enable: true, UserId: 9, ProjectId: 7, Scopes: 'apps:read',
      ExpireTime: '2027-12-31T23:59:00', CallCount: 1280, LastTime: '2026-10-29T14:02:00',
      LastIP: '10.0.0.5', Remark: '运维助手',
    }], projects, users);
    expect(rows[0]).toMatchObject({
      id: 3, name: 'claude-ops', enable: true, userId: 9, userName: 'admin', projectId: 7,
      projectName: '默认项目', scopes: 'apps:read', callCount: 1280, lastIP: '10.0.0.5',
      remark: '运维助手', masked: 'sdmcp_ab…op56',
    });
    expect(rows[0].expireMs).toBeGreaterThan(0);
    expect(rows[0].expireText).toContain('2027-12-31');
  });
  it('ExpireTime 为空 → 永不过期；ProjectId=0 → 项目 —', () => {
    const rows = toTokenRows([{ Id: 1, Name: 'ci-bot', ProjectId: 0 }], projects);
    expect(rows[0].expireText).toBe('永不过期');
    expect(rows[0].projectName).toBe('—');
  });
  it('项目查不到时兜底 #projectId', () => {
    const rows = toTokenRows([{ Id: 1, Name: 'x', ProjectId: 42 }], projects);
    expect(rows[0].projectName).toBe('#42');
  });
});

describe('toResourceRows · 授权行映射与资源名解析', () => {
  const nodes = [{ id: 11, name: 'node-a' }];
  const apps = [{ id: 21, name: 'Orders.Api' }];
  const projects = [{ id: 7, name: '默认项目' }];
  it('按类型解析资源名', () => {
    const rows = toResourceRows([
      { Id: 1, TokenId: 3, ResourceType: 'Project', ResourceId: 7, IsAll: false, Enable: true },
      { Id: 2, TokenId: 3, ResourceType: 'Node', ResourceId: 11, IsAll: true, Enable: false },
      { Id: 3, TokenId: 3, ResourceType: 'App', ResourceId: 21 },
    ], nodes, apps, projects);
    expect(rows[0].resourceName).toBe('默认项目');
    expect(rows[0].resourceTypeLabel).toBe('项目');
    expect(rows[1].resourceName).toBe('node-a');
    expect(rows[1].isAll).toBe(true);
    expect(rows[1].enable).toBe(false);
    expect(rows[2].resourceName).toBe('Orders.Api');
  });
});

describe('toAuditRows · 审计行映射与倒序', () => {
  it('tool·action 合并、字段映射', () => {
    const rows = toAuditRows([
      { Id: 1, TokenId: 3, TokenName: 'claude-ops', ToolName: 'invoke_action', ActionName: 'node_send_command', CallerIp: '10.0.0.5', Success: false, ErrorMessage: '拒绝', Duration: 12, TraceId: 'tr-1', CreateTime: '2026-10-29T14:02:00', Arguments: '{"x":"y"}', RiskLevel: 2, ProjectId: 9, ScopesUsed: 'apps:read,nodes:read', BeforeValue: '{"state":"old"}', AfterValue: '{"state":"new"}' },
      { Id: 2, TokenId: 3, ToolName: 'tools/list', Success: true, CreateTime: '2026-10-29T14:05:00' },
    ]);
    expect(rows).toHaveLength(2);
    expect(rows[0].toolLabel).toBe('tools/list');
    expect(rows[0].success).toBe(true);
    expect(rows[1].toolLabel).toBe('invoke_action · node_send_command');
    expect(rows[1].errorMessage).toBe('拒绝');
    expect(rows[1].duration).toBe(12);
    expect(rows[1].traceId).toBe('tr-1');
    expect(rows[1].args).toBe('{"x":"y"}');
    // 倒序：14:05 在前
    expect(rows[0].time).not.toBe('—');
  });

  it('G2: 映射 RiskLevel/ProjectId/ScopesUsed/BeforeValue/AfterValue（后端 MCP审计日志 新字段）', () => {
    const rows = toAuditRows([
      { Id: 1, TokenId: 3, ToolName: 'set_config', ActionName: 'config_set', Success: true, CreateTime: '2026-10-29T14:02:00', RiskLevel: 1, ProjectId: 42, ScopesUsed: 'config:write', BeforeValue: '{"k":"old"}', AfterValue: '{"k":"new"}' },
    ]);
    expect(rows[0].riskLevel).toBe(1);
    expect(rows[0].projectId).toBe(42);
    expect(rows[0].scopesUsed).toBe('config:write');
    expect(rows[0].beforeValue).toBe('{"k":"old"}');
    expect(rows[0].afterValue).toBe('{"k":"new"}');
  });

  it('G2: 新字段缺失时回落默认值', () => {
    const rows = toAuditRows([{ Id: 1, TokenId: 3, ToolName: 'tools/list', Success: true, CreateTime: '2026-10-29T14:02:00' }]);
    expect(rows[0].riskLevel).toBe(0);
    expect(rows[0].projectId).toBe(0);
    expect(rows[0].scopesUsed).toBe('');
    expect(rows[0].beforeValue).toBe('');
    expect(rows[0].afterValue).toBe('');
  });
});

describe('filterAudits · 客户端过滤', () => {
  const rows = toAuditRows([
    { Id: 1, TokenId: 3, ToolName: 'a', CallerIp: '10.0.0.5', Success: true, CreateTime: '2026-10-29T14:02:00' },
    { Id: 2, TokenId: 4, ToolName: 'b', CallerIp: '10.0.0.6', Success: false, CreateTime: '2026-10-29T14:03:00' },
    { Id: 3, TokenId: 3, ToolName: 'c', CallerIp: '192.168.1.1', Success: false, CreateTime: '2026-10-29T14:04:00' },
  ]);
  it('按令牌过滤', () => {
    expect(filterAudits(rows, { tokenId: 3, ipQ: '', level: 'all' })).toHaveLength(2);
  });
  it('按成败过滤', () => {
    expect(filterAudits(rows, { tokenId: 0, ipQ: '', level: 'failed' })).toHaveLength(2);
    expect(filterAudits(rows, { tokenId: 0, ipQ: '', level: 'ok' })).toHaveLength(1);
  });
  it('按 IP 包含过滤（大小写无关）', () => {
    expect(filterAudits(rows, { tokenId: 0, ipQ: '10.0.0', level: 'all' })).toHaveLength(2);
  });
});

describe('filterResourcesByToken', () => {
  it('tokenId<=0 返回空；否则只留该令牌的行', () => {
    const rows = [
      { id: 1, tokenId: 3, resourceType: 'Project', resourceTypeLabel: '项目', resourceId: 7, resourceName: 'p', isAll: false, enable: true },
      { id: 2, tokenId: 4, resourceType: 'Node', resourceTypeLabel: '节点', resourceId: 1, resourceName: 'n', isAll: false, enable: true },
    ];
    expect(filterResourcesByToken(rows as never, 0)).toHaveLength(0);
    expect(filterResourcesByToken(rows as never, 3)).toHaveLength(1);
  });
});

describe('fetchMcpManage · 四路+三软依赖', () => {
  it('正常返回令牌/授权/审计/下拉；软依赖 404 降级空列表', async () => {
    const { req } = recording([
      { method: 'GET', url: '/Platform/McpToken', reply: () => ({ code: 0, data: [{ Id: 1, Name: 't1', ProjectId: 7, Enable: true }] }) },
      { method: 'GET', url: '/Platform/McpTokenResource', reply: () => ({ code: 0, data: [{ Id: 9, TokenId: 1, ResourceType: 'Project', ResourceId: 7 }] }) },
      { method: 'GET', url: '/Platform/McpAudit', reply: () => ({ code: 0, data: [{ Id: 5, TokenId: 1, ToolName: 'a', Success: true, CreateTime: '2026-10-29T14:00:00' }] }) },
      { method: 'GET', url: '/Platform/GalaxyProject', reply: () => ({ code: 0, data: [{ Id: 7, Name: '默认项目' }] }) },
      { method: 'GET', url: '/Admin/User', reply: () => { throw new Error('404'); } },
      { method: 'GET', url: '/Nodes/Node', reply: () => ({ code: 0, data: [] }) },
      { method: 'GET', url: '/Registry/App', reply: () => ({ code: 0, data: [{ Id: 21, Name: 'Orders.Api' }] }) },
    ]);
    const r = await fetchMcpManage(req, null);
    expect(r.error).toBe('');
    expect(r.tokens).toHaveLength(1);
    expect(r.tokens[0].projectName).toBe('默认项目');
    expect(r.resources).toHaveLength(1);
    expect(r.resources[0].resourceName).toBe('默认项目');
    expect(r.audits).toHaveLength(1);
    expect(r.projects).toHaveLength(1);
    expect(r.users).toHaveLength(0); // 404 降级
    expect(r.apps).toHaveLength(1);
  });
});

describe('createToken · 创建', () => {
  it('空名称直接拒，不发请求', async () => {
    const { req, calls } = recording([]);
    const r = await createToken(req, { name: '  ', enable: true, userId: 0, projectId: 7, scopes: 'apps:read', expireIso: '', remark: '' });
    expect(r.ok).toBe(false);
    expect(calls).toHaveLength(0);
  });
  it('优先读 POST 响应 Data.Token，不再重拉列表', async () => {
    const { req, calls } = recording([
      { method: 'POST', url: '/Platform/McpToken', reply: () => ({ code: 0, message: 'Added!', data: { Id: 2, Token: 'sdmcp_resptokenresptokenresp12' } }) },
    ]);
    const r = await createToken(req, { name: 'new', enable: true, userId: 0, projectId: 7, scopes: 'apps:read', expireIso: '', remark: '' });
    expect(r.ok).toBe(true);
    expect(r.tokenValue).toBe('sdmcp_resptokenresptokenresp12');
    expect(r.tokenId).toBe(2);
    expect(calls).toHaveLength(1); // 只有 POST，无列表重拉
    const body = (calls[0] as unknown as { data: Record<string, unknown> }).data;
    expect(body.name).toBe('new');
    expect(body.projectId).toBe(7);
    expect(body.expireTime).toBeUndefined(); // 永不过期时不发 expireTime
  });
  it('响应缺 Token 时兜底重拉列表取 max(Id) 行', async () => {
    const { req, calls } = recording([
      { method: 'POST', url: '/Platform/McpToken', reply: () => ({ code: 0, message: 'Added!' }) },
      { method: 'GET', url: '/Platform/McpToken', reply: () => ({ code: 0, data: [
        { Id: 1, Name: 'old', Token: 'sdmcp_oldoldoldoldoldoldoldoldoldold11' },
        { Id: 2, Name: 'new', Token: 'sdmcp_newnewnewnewnewnewnewnewnewnew22' },
      ] }) },
    ]);
    const r = await createToken(req, { name: 'new', enable: true, userId: 0, projectId: 7, scopes: 'apps:read', expireIso: '', remark: '' });
    expect(r.ok).toBe(true);
    expect(r.tokenValue).toBe('sdmcp_newnewnewnewnewnewnewnewnewnew22');
    // 创建请求携带表单字段（JSON body）
    const add = calls.find((c) => c.url === '/Platform/McpToken' && c.method === 'POST');
    expect(add?.method).toBe('POST');
    const body = (add as unknown as { data: Record<string, unknown> }).data;
    expect(body.name).toBe('new');
    expect(body.projectId).toBe(7);
    // 永不过期时不发 expireTime
    expect(body.expireTime).toBeUndefined();
  });
  it('后端业务失败透传 message', async () => {
    const { req } = recording([
      { method: 'POST', url: '/Platform/McpToken', reply: () => ({ code: 500, message: '名称已存在' }) },
    ]);
    const r = await createToken(req, { name: 'x', enable: true, userId: 0, projectId: 0, scopes: '', expireIso: '', remark: '' });
    expect(r.ok).toBe(false);
    expect(r.message).toBe('名称已存在');
  });
});

describe('updateToken / deleteToken / setTokenEnable', () => {
  it('update：PUT /{id} 带 body；成功消息', async () => {
    const { req, calls } = recording([
      { method: 'PUT', url: '/Platform/McpToken/3', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    const r = await updateToken(req, 3, { name: 'n', enable: false, userId: 1, projectId: 7, scopes: 'apps:read', expireIso: '2027-12-31T23:59', remark: '' });
    expect(r.ok).toBe(true);
    expect(calls[0].url).toBe('/Platform/McpToken/3');
    const body = (calls[0] as unknown as { data: Record<string, unknown> }).data;
    expect(body.id).toBe(3);
    expect(body.expireTime).toBe('2027-12-31T23:59');
  });
  it('delete：DELETE /{id}；setEnable：PUT /{id} 带 enable', async () => {
    const { req, calls } = recording([
      { method: 'DELETE', url: '/Platform/McpToken/3', reply: () => ({ code: 0, message: 'ok' }) },
      { method: 'PUT', url: '/Platform/McpToken/3', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    await deleteToken(req, 3);
    await setTokenEnable(req, 3, false, { name: 'n', enable: true, userId: 0, projectId: 0, scopes: '', expireIso: '', remark: '' });
    expect(calls[0].url).toBe('/Platform/McpToken/3');
    expect(calls[0].method).toBe('DELETE');
    expect(calls[1].url).toBe('/Platform/McpToken/3');
    const body = (calls[1] as unknown as { data: Record<string, unknown> }).data;
    expect(body.enable).toBe(false);
  });
});

describe('resetToken · 重置', () => {
  it('优先读 Reset 响应 data.token，否则重拉列表', async () => {
    const { req } = recording([
      { method: 'GET', url: '/Platform/McpToken/Reset', reply: () => ({ code: 0, data: { id: 3, token: 'sdmcp_fromResetResponse123456789012345678' } }) },
    ]);
    const r = await resetToken(req, 3);
    expect(r.ok).toBe(true);
    expect(r.tokenValue).toBe('sdmcp_fromResetResponse123456789012345678');
  });

  it('响应无 token 时重拉列表', async () => {
    const { req } = recording([
      { method: 'GET', url: '/Platform/McpToken/Reset', reply: () => ({ code: 0, message: 'Token已重置！' }) },
      { method: 'GET', url: '/Platform/McpToken', reply: () => ({ code: 0, data: [{ Id: 3, Name: 'old', Token: 'sdmcp_brandnewtokenvaluebrandnew12345678' }] }) },
    ]);
    const r = await resetToken(req, 3);
    expect(r.ok).toBe(true);
    expect(r.tokenValue).toBe('sdmcp_brandnewtokenvaluebrandnew12345678');
  });
});

describe('addResource / deleteResource', () => {
  it('add：POST 集合 URL 带 tokenId/resourceType/resourceId，isAll=false', async () => {
    const { req, calls } = recording([
      { method: 'POST', url: '/Platform/McpTokenResource', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    const r = await addResource(req, 3, 'Node', 11);
    expect(r.ok).toBe(true);
    const body = (calls[0] as unknown as { data: Record<string, unknown> }).data;
    expect(body.tokenId).toBe(3);
    expect(body.resourceType).toBe('Node');
    expect(body.resourceId).toBe(11);
    expect(body.isAll).toBe(false);
  });
  it('add：缺资源拒绝不发请求', async () => {
    const { req, calls } = recording([]);
    const r = await addResource(req, 3, 'Node', 0);
    expect(r.ok).toBe(false);
    expect(calls).toHaveLength(0);
  });
  it('delete：DELETE /{id}', async () => {
    const { req, calls } = recording([
      { method: 'DELETE', url: '/Platform/McpTokenResource/9', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    await deleteResource(req, 9);
    expect(calls[0].url).toBe('/Platform/McpTokenResource/9');
  });
});

describe('setResourceEnable · 授权启停', () => {
  it('PUT /{id} 仅提交 id+enable', async () => {
    const { req, calls } = recording([
      { method: 'PUT', url: '/Platform/McpTokenResource/9', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    const r = await setResourceEnable(req, 9, false);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('授权已停用');
    const body = (calls[0] as unknown as { data: Record<string, unknown> }).data;
    expect(body.id).toBe(9);
    expect(body.enable).toBe(false);
  });
  it('缺 id 拒绝不发请求', async () => {
    const { req, calls } = recording([]);
    const r = await setResourceEnable(req, 0, true);
    expect(r.ok).toBe(false);
    expect(calls).toHaveLength(0);
  });
});

describe('McpServer CRUD · 服务器管理', () => {
  it('create：POST 集合 URL 带全字段（trim 后提交）', async () => {
    const { req, calls } = recording([
      { method: 'POST', url: '/Platform/McpServer', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    const r = await createMcpServer(req, { name: ' fs ', url: ' http://n:6680/api/mcp ', enable: true, projectId: 7, remark: 'r' });
    expect(r.ok).toBe(true);
    expect(r.message).toBe('服务器已登记');
    const body = (calls[0] as unknown as { data: Record<string, unknown> }).data;
    expect(body.name).toBe('fs');
    expect(body.url).toBe('http://n:6680/api/mcp');
    expect(body.projectId).toBe(7);
  });
  it('create：缺名称/地址直接拒，不发请求', async () => {
    const { req, calls } = recording([]);
    expect((await createMcpServer(req, { name: '', url: 'u', enable: true, projectId: 0, remark: '' })).ok).toBe(false);
    expect((await createMcpServer(req, { name: 'n', url: '', enable: true, projectId: 0, remark: '' })).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });
  it('update：PUT /{id} 带 id 与全字段', async () => {
    const { req, calls } = recording([
      { method: 'PUT', url: '/Platform/McpServer/5', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    const r = await updateMcpServer(req, 5, { name: 'n2', url: 'u2', enable: false, projectId: 0, remark: '' });
    expect(r.ok).toBe(true);
    const body = (calls[0] as unknown as { data: Record<string, unknown> }).data;
    expect(body.id).toBe(5);
    expect(body.enable).toBe(false);
  });
  it('delete 与 setEnable：DELETE / PUT /{id} enable', async () => {
    const { req, calls } = recording([
      { method: 'DELETE', url: '/Platform/McpServer/5', reply: () => ({ code: 0, message: 'ok' }) },
      { method: 'PUT', url: '/Platform/McpServer/5', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    await deleteMcpServer(req, 5);
    const r = await setServerEnable(req, 5, true);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('服务器已启用');
    expect(calls[0].method).toBe('DELETE');
    const body = (calls[1] as unknown as { data: Record<string, unknown> }).data;
    expect(body.enable).toBe(true);
  });
});

describe('buildAuditQueryParams · 审计查询参数', () => {
  it('dtEnd 自动补 23:59:59；空值不下发', () => {
    const p = buildAuditQueryParams({ tokenId: 3, toolName: ' stardust_node_list ', success: false, dtStart: '2026-10-01', dtEnd: '2026-10-31', pageIndex: 2, pageSize: 50 });
    expect(p).toEqual({ pageIndex: 2, pageSize: 50, tokenId: 3, toolName: 'stardust_node_list', success: false, dtStart: '2026-10-01', dtEnd: '2026-10-31 23:59:59' });
  });
  it('全空条件只带分页', () => {
    const p = buildAuditQueryParams({ tokenId: 0, toolName: '', dtStart: '', dtEnd: '', pageIndex: 0, pageSize: 50 });
    expect(p).toEqual({ pageIndex: 0, pageSize: 50 });
  });
});

describe('fetchMcpAuditPage · 审计历史服务端查询', () => {
  it('GET 集合 URL；行按时间倒序；取 page.totalCount', async () => {
    const { req, calls } = recording([
      { method: 'GET', url: '/Platform/McpAudit', reply: () => ({ code: 0, data: [
        { Id: 1, TokenId: 1, TokenName: 't', ToolName: 'a', ActionName: 'x', CreateTime: '2026-10-29 10:00', Success: true },
        { Id: 2, TokenId: 1, TokenName: 't', ToolName: 'b', ActionName: '', CreateTime: '2026-10-29 11:00', Success: false, ErrorMessage: 'boom' },
      ], page: { totalCount: 120 } }) },
    ]);
    const r = await fetchMcpAuditPage(req, { tokenId: 0, toolName: '', dtStart: '', dtEnd: '', pageIndex: 0, pageSize: 50 });
    expect(r.error).toBe('');
    expect(r.total).toBe(120);
    expect(r.rows[0].id).toBe('2'); // 11:00 的在前
    expect(r.rows[0].success).toBe(false);
    expect(r.rows[0].timeMs).toBeGreaterThan(0);
    const params = (calls[0] as unknown as { params: Record<string, unknown> }).params;
    expect(params.pageSize).toBe(50);
  });
  it('异常降级为 error 字段', async () => {
    const { req } = recording([
      { method: 'GET', url: '/Platform/McpAudit', reply: () => { throw new Error('net down'); } },
    ]);
    const r = await fetchMcpAuditPage(req, { tokenId: 0, toolName: '', dtStart: '', dtEnd: '', pageIndex: 0, pageSize: 50 });
    expect(r.rows).toHaveLength(0);
    expect(r.total).toBe(0);
    expect(r.error).toBe('net down');
  });
});

describe('fetchMcpRuntime / setMcpEnabled · 运行时开关', () => {
  it('GET StarServer 解析 EnableMcp/McpActionSet/SensitiveKeywords，raw 原样保留', async () => {
    const raw = { EnableMcp: true, McpActionSet: '*', SensitiveKeywords: 'password,secret' };
    const { req, calls } = recording([
      { method: 'GET', url: '/Platform/StarServer', reply: () => ({ code: 0, data: raw }) },
    ]);
    const r = await fetchMcpRuntime(req);
    expect(r.error).toBe('');
    expect(r.enableMcp).toBe(true);
    expect(r.actionSet).toBe('*');
    expect(r.sensitiveKeywords).toBe('password,secret');
    expect(r.raw).toBe(raw);
    expect(calls[0].url).toBe('/Platform/StarServer');
  });
  it('data 为空降级「未知」（不报错）', async () => {
    const { req } = recording([
      { method: 'GET', url: '/Platform/StarServer', reply: () => ({ code: 0, data: null }) },
    ]);
    const r = await fetchMcpRuntime(req);
    expect(r.raw).toBeNull();
    expect(r.enableMcp).toBeUndefined();
    expect(r.error).toBe('');
  });
  it('setMcpEnabled：PUT 整对象回传，仅翻转开关字段（全量覆盖语义）', async () => {
    const raw = { Enable: false, Port: 6680, EnableMcp: false };
    const { req, calls } = recording([
      { method: 'PUT', url: '/Platform/StarServer', reply: () => ({ code: 0, message: 'ok' }) },
    ]);
    const r = await setMcpEnabled(req, raw, true);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('MCP 服务已开启');
    const body = (calls[0] as unknown as { data: Record<string, unknown> }).data;
    expect(body.EnableMcp).toBe(true);
    expect(body.enableMcp).toBe(true);
    expect(body.Port).toBe(6680);
    expect(body.Enable).toBe(false);
  });
  it('setMcpEnabled：raw 为空拒绝不发请求', async () => {
    const { req, calls } = recording([]);
    const r = await setMcpEnabled(req, null, true);
    expect(r.ok).toBe(false);
    expect(calls).toHaveLength(0);
  });
});
