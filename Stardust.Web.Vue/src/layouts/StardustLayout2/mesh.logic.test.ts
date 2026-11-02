// 治理页纯逻辑单测（Vitest）。覆盖：四个 tab 归一、ClusterId→集群名映射、令牌掩码、
// ProjectUser/GalaxyProject 计数、projectId 透传、单 tab 请求失败隔离。
import { describe, it, expect } from 'vitest';
import {
  toRegistryRows,
  fetchRegistry,
  buildClusterMap,
  certDomainMatches,
  matchCert,
  toGatewayRoutes,
  fetchGateway,
  maskToken,
  buildScopeMap,
  toMcpTokens,
  toMcpAudits,
  fetchMcp,
  fetchMcpServers,
  toMembers,
  toProjectInfo,
  countCsv,
  pickProject,
  buildTiles,
  fetchPlatform,
} from './mesh.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 桩请求：按 url 返回预设 data；throwOn 时指定 url 抛错。记录最后一次 params 用于断言透传。
 *  map 值可以是数组（列表接口）或单个对象（如 /Platform/StarServer 返回单设置对象）；
 *  stubReq 统一再包一层 { data: map[url] }，所以这里的值就是响应里的 data 本体。 */
function stubReq(map: Record<string, unknown>, throwOn?: string[]): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (throwOn?.includes(url)) throw new Error(`stub 500:${url}`);
    if (url in map) return { data: map[url] };
    return { data: [] };
  };
}

describe('注册中心 · toRegistryRows 字段归一与防御', () => {
  it('实例数=Providers，消费者数=Consumers，健康反映 Enable；DisplayName 优先', () => {
    const rows = toRegistryRows([
      { Id: 1, Name: 'order', DisplayName: '订单服务', Category: 'biz', Providers: 8, Consumers: 3, Enable: true, HealthCheck: true },
      { Id: 2, Name: 'offline', Providers: 0, Consumers: 0, Enable: false, HealthCheck: false },
    ]);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toMatchObject({ id: 1, name: '订单服务', serviceName: 'order', category: 'biz', instances: 8, consumers: 3, enable: true, healthCheck: true });
    expect(rows[1].name).toBe('offline'); // 无 DisplayName 兜底 Name
    expect(rows[1].enable).toBe(false);
  });

  it('空数组与缺字段不抛错（数字缺省 0，布尔缺省 false）', () => {
    expect(toRegistryRows([])).toEqual([]);
    const rows = toRegistryRows([{ Id: 9 }]);
    expect(rows[0]).toMatchObject({ instances: 0, consumers: 0, enable: false, healthCheck: false, name: '' });
  });
});

describe('网关 · ClusterId→集群名映射与 TLS 判定', () => {
  it('buildClusterMap：Id→Name，跳过 id<=0', () => {
    const m = buildClusterMap([{ Id: 11, Name: 'DeviceService' }, { Id: 22, Name: 'OrderService' }, { Id: 0, Name: 'Bad' }]);
    expect(m.get(11)).toBe('DeviceService');
    expect(m.get(22)).toBe('OrderService');
    expect(m.has(0)).toBe(false);
  });

  it('certDomainMatches：精确与通配', () => {
    expect(certDomainMatches('api.example.com', 'api.example.com')).toBe(true);
    expect(certDomainMatches('a.example.com', '*.example.com')).toBe(true);
    expect(certDomainMatches('example.com', '*.example.com')).toBe(true);
    expect(certDomainMatches('api.other.com', '*.example.com')).toBe(false);
    expect(certDomainMatches('', '*.example.com')).toBe(false);
  });

  it('matchCert：精确优先于通配；无启用证书 present=false', () => {
    const certs = [
      { Domain: 'api.example.com', Enable: true, AutoRenew: true },
      { Domain: '*.example.com', Enable: true, AutoRenew: false },
      { Domain: 'old.com', Enable: false, AutoRenew: true },
    ];
    const hit = matchCert('api.example.com', certs);
    expect(hit.present).toBe(true);
    expect(hit.domain).toBe('api.example.com');
    expect(hit.autoRenew).toBe(true);
    const wild = matchCert('dev.example.com', certs);
    expect(wild.present).toBe(true);
    expect(wild.domain).toBe('*.example.com');
    expect(matchCert('nope.com', certs).present).toBe(false);
  });

  it('toGatewayRoutes：Methods 空/有值、ClusterId 查不到集群名留空、TLS 按域名证书', () => {
    const clusterMap = buildClusterMap([{ Id: 11, Name: 'DeviceService' }]);
    const certs = [{ Domain: 'iot.example.com', Enable: true, AutoRenew: true }];
    const rows = toGatewayRoutes(
      [
        { Id: 1, Name: 'iot-api', Domain: 'iot.example.com', Path: '/api/device/**', Methods: 'GET,POST', ClusterId: 11, Enable: true, IsStaticRoute: false },
        { Id: 2, Name: 'web', Domain: 'spa.example.com', Path: '/', Methods: '', ClusterId: 999, Enable: true, IsStaticRoute: true },
      ],
      clusterMap,
      certs,
    );
    expect(rows[0].clusterName).toBe('DeviceService');
    expect(rows[0].methods).toBe('GET,POST');
    expect(rows[0].tls.present).toBe(true);
    expect(rows[1].clusterName).toBe(''); // 999 查不到 → 组件显示待接入
    expect(rows[1].methods).toBe('');
    expect(rows[1].isStatic).toBe(true);
    expect(rows[1].tls.present).toBe(false);
  });
});

describe('AI/MCP · 令牌掩码与授权范围', () => {
  it('maskToken：保留下划线前缀后打码；无前缀打码前 4 位；空→—', () => {
    expect(maskToken('sdmcp_abcdef123456')).toBe('sdmcp_****');
    expect(maskToken('sk-abcdef')).toBe('sk-a****');
    expect(maskToken('abcd')).toBe('****');
    expect(maskToken('')).toBe('—');
    expect(maskToken(null)).toBe('—');
  });

  it('buildScopeMap：IsAll→全部资源，否则去重资源类型', () => {
    const m = buildScopeMap([
      { TokenId: 1, ResourceType: 'Project', IsAll: true },
      { TokenId: 2, ResourceType: 'Node', IsAll: false },
      { TokenId: 2, ResourceType: 'App', IsAll: false },
      { TokenId: 2, ResourceType: 'App', IsAll: false }, // 重复去重
    ]);
    expect(m.get(1)).toBe('全部资源');
    expect(m.get(2)).toBe('节点 + 应用');
  });

  it('toMcpTokens：掩码、过期时间、调用次数；无过期→永不过期；无 scope→—', () => {
    const scopeMap = buildScopeMap([{ TokenId: 1, ResourceType: 'App', IsAll: false }]);
    const rows = toMcpTokens(
      [
        { Id: 1, Name: 'claude-ops', Token: 'sdmcp_zzzz', Enable: true, CallCount: 42, LastTime: '2026-10-29 10:00:00' },
        { Id: 2, Name: 'ci-bot', Token: 'sdmcp_yyyy', Enable: false, ExpireTime: '2026-12-31 23:59:59', CallCount: 0 },
      ],
      scopeMap,
    );
    expect(rows[0]).toMatchObject({ name: 'claude-ops', masked: 'sdmcp_****', enable: true, callCount: 42, scopeText: '应用', expireText: '永不过期' });
    expect(rows[0].lastText).toContain('2026-10-29');
    expect(rows[1].expireText).toContain('2026-12-31');
    expect(rows[1].scopeText).toBe('—');
  });

  it('toMcpAudits：工具名+动作名合并、成功/失败点、倒序截断', () => {
    const rows = toMcpAudits(
      [
        { Id: 1, ToolName: 'list_actions', Success: true, CallerIp: '10.0.0.5', CreateTime: '2026-10-29 09:00:00' },
        { Id: 2, ToolName: 'invoke_action', ActionName: 'node_send_command', Success: false, ErrorMessage: '被安全策略拦截', CreateTime: '2026-10-29 11:00:00' },
      ],
    );
    expect(rows[0].tool).toBe('invoke_action · node_send_command');
    expect(rows[0].ok).toBe(false);
    expect(rows[0].detail).toBe('被安全策略拦截');
    expect(rows[1].detail).toBe('10.0.0.5');
    // 最新的 11:00 排第一
    expect(rows[0].time).toContain('11:00');
  });
});

describe('平台设置 · 计数与磁贴归一', () => {
  it('countCsv：逗号分隔计数，空串为 0', () => {
    expect(countCsv('1.1.1.1, 2.2.2.2')).toBe(2);
    expect(countCsv('')).toBe(0);
    expect(countCsv(null)).toBe(0);
  });

  it('pickProject：按 projectId 精确匹配，否则首行', () => {
    const rows = [{ Id: 1, Name: 'A' }, { Id: 2, Name: 'B' }];
    expect(pickProject(rows, 2)?.Name).toBe('B');
    expect(pickProject(rows, 99)?.Name).toBe('A'); // 找不到回退首行
    expect(pickProject(rows, null)?.Name).toBe('A');
  });

  it('buildTiles：成员数=ProjectUser 行数，项目数=GalaxyProject 行数，节点/应用/防火墙取 GalaxyProject；数据保留/运行时/级联取 StarServer；缺的磁贴 pending 且写明原因', () => {
    const tiles = buildTiles(
      [{ Id: 7, Name: 'prod', Nodes: 5, Apps: 12, WhiteIPs: '10.0.0.1,10.0.0.2', BlackIPs: '' }],
      [{ UserId: 1 }, { UserId: 2 }, { UserId: 3 }],
      { DataRetention: 3, DataRetention2: 30, DataRetention3: 300, DotNetSyncPeriod: 43200, UplinkServer: '' },
      7,
    );
    const byKey = Object.fromEntries(tiles.map((t) => [t.key, t]));
    expect(byKey.members.detail).toBe('3 位成员 · 1 个项目 · 5 节点 / 12 应用');
    expect(byKey.firewall.detail).toBe('白名单 2 条 · 黑名单 0 条');
    expect(byKey.retention.detail).toBe('明细 3 天 · 小时 30 天 · 历史 300 天');
    expect(byKey.dotnet.detail).toBe('每 12h 同步官方版本');
    expect(byKey.cascade.detail).toBe('未配置上级服务器');
    // 取不到真实值的磁贴标 pending，detail 不再只写「待接入」，而是诚实说明
    expect(byKey.sso.status).toBe('pending');
    expect(byKey.ddns.status).toBe('pending');
    expect(byKey.audit.status).toBe('pending');
    expect(byKey.sso.detail).toBe('控制台暂未开放配置入口，请到后端管理后台配置');
    expect(byKey.ddns.detail).toBe('控制台暂未开放配置入口，请到后端管理后台配置');
    expect(byKey.audit.detail).toBe('MCP 调用审计见「AI / MCP」页签');
  });

  it('buildTiles：StarServerSetting 为空时真实磁贴退化为 0，pending 磁贴带诚实说明', () => {
    const tiles = buildTiles([], [], undefined, null);
    const byKey = Object.fromEntries(tiles.map((t) => [t.key, t]));
    expect(byKey.members.detail).toBe('0 位成员 · 0 个项目 · 0 节点 / 0 应用');
    expect(byKey.dotnet.detail).toBe('自动同步已禁用');
    expect(byKey.retention.detail).toBe('明细 0 天 · 小时 0 天 · 历史 0 天');
    expect(byKey.sso.detail).not.toBe('待接入');
  });
});

describe('projectId 透传（请求注入断言）', () => {
  it('四个 fetch 函数在 projectId 非空时把它放进 params', async () => {
    const seen: Record<string, Record<string, unknown>> = {};
    const req: Req = async (opt) => {
      seen[String(opt.url)] = { ...(opt.params as Record<string, unknown>) };
      return { data: [] };
    };
    await fetchRegistry(req, 42);
    await fetchGateway(req, 42);
    await fetchMcp(req, 42);
    await fetchPlatform(req, 42);
    expect(seen['/Registry/Service'].projectId).toBe(42);
    expect(seen['/Gateway/GatewayRoute'].projectId).toBe(42);
    expect(seen['/Platform/McpToken'].projectId).toBe(42);
    expect(seen['/Platform/GalaxyProject'].projectId).toBe(42);
  });

  it('projectId 为空（全部项目）时 params 不带 projectId', async () => {
    const seen: Record<string, Record<string, unknown>> = {};
    const req: Req = async (opt) => {
      seen[String(opt.url)] = { ...(opt.params as Record<string, unknown>) };
      return { data: [] };
    };
    await fetchRegistry(req, null);
    expect(seen['/Registry/Service'].projectId).toBeUndefined();
  });
});

describe('单 tab 请求失败隔离', () => {
  it('网关 tab 失败只返回自身 error，不影响其它 tab 的成功取数', async () => {
    // 各 tab 独立 fetch：让 /Gateway/* 抛错，注册中心/MCP/平台正常
    const gw = await fetchGateway(stubReq({}, ['/Gateway/GatewayRoute', '/Gateway/GatewayCluster', '/Gateway/GatewayCert']), 1);
    expect(gw.rows).toEqual([]);
    expect(gw.error).not.toBe(''); // 失败透传真实错误信息，不静默空态

    const reg = await fetchRegistry(stubReq({ '/Registry/Service': [{ Id: 1, Name: 'ok', Providers: 1 }] }), 1);
    expect(reg.error).toBe('');
    expect(reg.rows).toHaveLength(1);

    const mcp = await fetchMcp(stubReq({ '/Platform/McpToken': [], '/Platform/McpTokenResource': [], '/Platform/McpAudit': [] }), 1);
    expect(mcp.error).toBe('');
    expect(mcp.tokens).toEqual([]);

    const plat = await fetchPlatform(
      stubReq({
        '/Platform/GalaxyProject': [],
        '/Platform/ProjectUser': [],
        '/Platform/StarServer': { DotNetSyncPeriod: 0 },
      }),
      1,
    );
    expect(plat.error).toBe('');
    expect(plat.tiles.length).toBe(8);
  });
});

describe('MCP 服务器 · fetchMcpServers', () => {
  it('后端请求失败时返回空 rows + 通用失败文案，不抛错', async () => {
    const req: Req = async () => {
      throw new Error('Request failed with status 404');
    };
    const r = await fetchMcpServers(req, 1);
    expect(r.rows).toEqual([]);
    expect(r.error).toBe('MCP 服务器列表加载失败');
  });

  it('接口返回列表时正常归一（名称/URL/状态/工具数）', async () => {
    const req: Req = async () => ({
      data: [
        { Id: 1, Name: 'filesystem', Url: 'http://localhost:8100', Enable: true, ToolCount: 7 },
        { Id: 2, Name: 'git', Address: 'http://localhost:8200', Enable: false },
      ],
    });
    const r = await fetchMcpServers(req, 1);
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(2);
    expect(r.rows[0]).toMatchObject({ name: 'filesystem', url: 'http://localhost:8100', enable: true, tools: 7 });
    expect(r.rows[1].url).toBe('http://localhost:8200'); // Address 别名兜底
    expect(r.rows[1].tools).toBe(0);
  });
});

describe('成员管理 · toMembers 归一与项目信息', () => {
  it('toMembers：有 Name 用用户名，无名字段退化为 #UserId；Role/Kind 归一；缺省角色为「成员」', () => {
    const rows = toMembers([
      { Id: 11, UserId: 101, Name: '何炳宏', Role: '管理员', CreateTime: '2026-08-01 09:30:00' },
      { Id: 12, UserId: 202, Kind: '开发', CreateTime: '2026-09-15 14:00:00' },
      { Id: 13, UserId: 303 },
    ]);
    expect(rows[0]).toMatchObject({ id: 11, userId: 101, name: '何炳宏', role: '管理员' });
    expect(rows[0].joinedText).toContain('2026-08-01');
    expect(rows[1].name).toBe('#202'); // ProjectUser 不 join User 表 → 诚实降级
    expect(rows[1].role).toBe('开发');
    expect(rows[2].role).toBe('成员');
    expect(rows[2].joinedText).toBe('—');
    expect(rows[2].enable).toBe(true);
  });

  it('toMembers：UserName 优先，Enable=false 记为停用', () => {
    const rows = toMembers([
      { Id: 1, UserId: 2, UserName: '何炳宏', Name: '忽略', Enable: false },
    ]);
    expect(rows[0].name).toBe('何炳宏');
    expect(rows[0].enable).toBe(false);
  });

  it('toMembers：空数组返回空，不抛错', () => {
    expect(toMembers([])).toEqual([]);
  });

  it('toProjectInfo：按 projectId 精确选项目，环境/创建时间归一', () => {
    const info = toProjectInfo(
      [
        { Id: 1, Name: 'dev', Environment: '测试', CreateTime: '2026-01-01 10:00:00' },
        { Id: 2, Name: 'prod' },
      ],
      2,
    );
    expect(info).not.toBeNull();
    expect(info?.name).toBe('prod');
    expect(info?.environment).toBe('—'); // 缺字段诚实显示 —
    expect(info?.createdText).toBe('—');
  });

  it('toProjectInfo：projectId 不匹配回退首行；空数组返回 null', () => {
    const fallback = toProjectInfo([{ Id: 9, Name: 'only' }], 999);
    expect(fallback?.name).toBe('only');
    expect(toProjectInfo([], null)).toBeNull();
  });

  it('fetchPlatform：成员列表与项目信息随 tiles 一起返回', async () => {
    const plat = await fetchPlatform(
      stubReq({
        '/Platform/GalaxyProject': [{ Id: 7, Name: 'prod', Environment: '生产', CreateTime: '2026-01-01 00:00:00' }],
        '/Platform/ProjectUser': [{ Id: 1, UserId: 101, Name: '何炳宏', Role: '管理员' }, { Id: 2, UserId: 202, Kind: '开发' }],
        '/Platform/StarServer': {},
      }),
      7,
    );
    expect(plat.error).toBe('');
    expect(plat.members).toHaveLength(2);
    expect(plat.members[0].name).toBe('何炳宏');
    expect(plat.members[1].name).toBe('#202');
    expect(plat.projectInfo?.name).toBe('prod');
    expect(plat.projectInfo?.environment).toBe('生产');
  });
});
