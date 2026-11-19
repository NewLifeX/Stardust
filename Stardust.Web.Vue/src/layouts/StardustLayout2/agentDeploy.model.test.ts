// Agent 远程部署面板纯逻辑单测（接口契约：WebApi AgentDeployController.cs 2026-11-10 核对）
import { describe, expect, it } from 'vitest';
import {
  DOTNET_VERSIONS,
  defaultForm,
  deploy,
  deploySummary,
  fetchDeployDefaults,
  firstHost,
  hostCount,
  mapDeployRows,
  outputText,
  parseDeployResponse,
  parseTestResponse,
  resetState,
  testConnection,
  toDeployBody,
  validateDeployForm,
  type AgentReqOptions,
  type DeployForm,
} from './agentDeploy.model';

function filled(over: Partial<DeployForm> = {}): DeployForm {
  return { ...defaultForm(), hosts: '192.168.1.10', userName: 'root', password: 'p@ss', serverUrl: 'http://10.0.0.2:6600', ...over };
}

describe('agentDeploy.model 基础形态', () => {
  it('defaultForm：九字段默认 端口22/Linux/dotnet9/其余空', () => {
    expect(defaultForm()).toEqual({
      hosts: '', port: 22, userName: '', password: '', osType: 'Linux',
      serverUrl: '', downloadUrl: '', dotnetVersion: 9,
    });
  });

  it('DOTNET_VERSIONS：8/9/10 三档，默认 9.0 在列', () => {
    expect(DOTNET_VERSIONS.map((v) => v.value).sort((a, b) => a - b)).toEqual([8, 9, 10]);
    expect(DOTNET_VERSIONS.find((v) => v.value === 9)?.label).toBe('9.0');
  });

  it('resetState：表单默认 + 执行位置 server', () => {
    expect(resetState()).toEqual({ form: defaultForm(), executeOn: 'server' });
  });
});

describe('firstHost（C06：仅测第一个地址，修复旧壳传整串差异）', () => {
  it('逗号分隔取第一段', () => {
    expect(firstHost('192.168.1.10,192.168.1.11')).toBe('192.168.1.10');
  });
  it('前导空段跳过（空格/逗号开头）', () => {
    expect(firstHost(' , 10.0.0.3;10.0.0.4')).toBe('10.0.0.3');
  });
  it('换行/回车拆分并去首尾空白', () => {
    expect(firstHost('\n  172.16.0.5 \n172.16.0.6')).toBe('172.16.0.5');
    expect(firstHost('10.1.1.1\r\n10.1.1.2')).toBe('10.1.1.1');
  });
  it('单值/CIDR 网段原样返回（CIDR 含斜杠不受拆分影响）', () => {
    expect(firstHost('192.168.1.0/24')).toBe('192.168.1.0/24');
    expect(firstHost('  server.corp.local  ')).toBe('server.corp.local');
  });
  it('空/全分隔符返回空串', () => {
    expect(firstHost('')).toBe('');
    expect(firstHost(' ,, ;  \n ')).toBe('');
  });
});

describe('hostCount（C07 确认框「N 台目标主机」台数）', () => {
  it('逗号/分号/混合分隔计数', () => {
    expect(hostCount('10.0.0.1,10.0.0.2')).toBe(2);
    expect(hostCount('10.0.0.1;10.0.0.2;10.0.0.3')).toBe(3);
    expect(hostCount('a, ; b\n c')).toBe(3);
  });
  it('重复地址按段数如实计数（部署循环逐台执行，文案须与实际一致，不去重）', () => {
    expect(hostCount('10.0.0.1, 10.0.0.1 ,10.0.0.2')).toBe(3);
  });
  it('空段不计入，空串为 0', () => {
    expect(hostCount(' ,, ;')).toBe(0);
    expect(hostCount('')).toBe(0);
  });
  it('单地址为 1', () => {
    expect(hostCount('  server.corp.local  ')).toBe(1);
  });
});

describe('validateDeployForm（C05：四项必填拦截，旧壳无此校验）', () => {
  it('全空：四条中文提示，顺序 hosts/userName/password/serverUrl', () => {
    expect(validateDeployForm(defaultForm())).toEqual([
      '目标主机不能为空',
      '用户名不能为空',
      '密码不能为空',
      '服务器地址不能为空',
    ]);
  });
  it('仅缺 serverUrl：单条提示（默认值未预填时常见）', () => {
    expect(validateDeployForm(filled({ serverUrl: '  ' }))).toEqual(['服务器地址不能为空']);
  });
  it('四项齐全：通过（空数组）', () => {
    expect(validateDeployForm(filled())).toEqual([]);
  });
});

describe('toDeployBody：PascalCase 对齐 AgentDeployModel 绑定', () => {
  it('九字段键名与值一一对应', () => {
    const f = filled({ port: 2222, osType: 'Windows', downloadUrl: 'http://x/star/', dotnetVersion: 8 });
    expect(toDeployBody(f)).toEqual({
      Hosts: '192.168.1.10', Port: 2222, UserName: 'root', Password: 'p@ss',
      OSType: 'Windows', ServerUrl: 'http://10.0.0.2:6600', DownloadUrl: 'http://x/star/', DotnetVersion: 8,
    });
  });
});

describe('fetchDeployDefaults（C02：消费 WebApi Index 预填）', () => {
  it('code=0 camelCase：serverUrl/downloadUrl 透出', async () => {
    const seen: AgentReqOptions[] = [];
    const req = (o: AgentReqOptions) => { seen.push(o); return Promise.resolve({ code: 0, data: { serverUrl: 'http://10.0.0.2:6600', downloadUrl: 'http://x.newlifex.com/star/' } }); };
    const r = await fetchDeployDefaults(req);
    expect(r).toEqual({ serverUrl: 'http://10.0.0.2:6600', downloadUrl: 'http://x.newlifex.com/star/' });
    expect(seen[0]).toMatchObject({ url: '/Nodes/AgentDeploy/Index', method: 'GET' });
  });
  it('PascalCase 兜底 + 网络失败回空串', async () => {
    const r1 = await fetchDeployDefaults(() => Promise.resolve({ code: 0, data: { ServerUrl: 'http://a', DownloadUrl: 'http://b' } }));
    expect(r1).toEqual({ serverUrl: 'http://a', downloadUrl: 'http://b' });
    const r2 = await fetchDeployDefaults(() => Promise.reject(new Error('boom')));
    expect(r2).toEqual({ serverUrl: '', downloadUrl: '' });
  });
});

describe('parseTestResponse / testConnection（C06/C09）', () => {
  it('code=0 success=true → 成功文案', () => {
    expect(parseTestResponse({ code: 0, data: { success: true } })).toEqual({ ok: true, message: '连接测试成功' });
  });
  it('code=0 success=false（executeOn=web 未实现）→ 后端 message 原文透出', () => {
    const r = parseTestResponse({ code: 0, data: { success: false, message: 'StarWeb本地执行功能暂未实现，请选择StarServer执行' } });
    expect(r.ok).toBe(false);
    expect(r.message).toBe('StarWeb本地执行功能暂未实现，请选择StarServer执行');
  });
  it('code=500 → message 透出；空信封兜底「连接失败」', () => {
    expect(parseTestResponse({ code: 500, message: '连接超时' })).toEqual({ ok: false, message: '连接超时' });
    expect(parseTestResponse(null)).toEqual({ ok: false, message: '连接失败' });
  });
  it('testConnection：host 仅取第一段 + 凭据走 body 不入 query（安全 N3）', async () => {
    const seen: AgentReqOptions[] = [];
    const req = (o: AgentReqOptions) => { seen.push(o); return Promise.resolve({ code: 0, data: { success: true } }); };
    const r = await testConnection(req, filled({ hosts: ' 1.2.3.4 , 5.6.7.8' }), 'web');
    expect(r.ok).toBe(true);
    expect(seen[0]).toMatchObject({
      url: '/Nodes/AgentDeploy/TestConnection', method: 'POST',
      data: { host: '1.2.3.4', port: 22, userName: 'root', password: 'p@ss', executeOn: 'web' },
    });
    expect(seen[0].params).toBeUndefined();
  });
  it('testConnection：网络拒绝 → 异常文案可见不静默', async () => {
    const r = await testConnection(() => Promise.reject(new Error('net down')), filled(), 'server');
    expect(r.ok).toBe(false);
    expect(r.message).toContain('net down');
  });
});

describe('mapDeployRows / parseDeployResponse / deploy（C08/C09）', () => {
  it('mapDeployRows：camelCase 归一 + PascalCase 兼容 + 非数组回空', () => {
    expect(mapDeployRows([{ host: 'a', success: true, message: 'ok', output: 'log' }, { Host: 'b', Success: false, Message: 'fail' }]))
      .toEqual([
        { host: 'a', success: true, message: 'ok', output: 'log' },
        { host: 'b', success: false, message: 'fail', output: '' },
      ]);
    expect(mapDeployRows(null)).toEqual([]);
    expect(mapDeployRows({})).toEqual([]);
  });
  it('parseDeployResponse：code=0 取 data 列表；code=500（web 未实现）message 透出', () => {
    const ok = parseDeployResponse({ code: 0, data: [{ host: 'a', success: true }] });
    expect(ok.ok).toBe(true);
    expect(ok.rows).toEqual([{ host: 'a', success: true, message: '', output: '' }]);
    const bad = parseDeployResponse({ code: 500, message: 'Local execution in StarWeb is not implemented; use StarServer instead.' });
    expect(bad.ok).toBe(false);
    expect(bad.message).toContain('not implemented');
    expect(bad.rows).toEqual([]);
  });
  it('deploy：body=PascalCase 九字段 + query executeOn=server', async () => {
    const seen: AgentReqOptions[] = [];
    const req = (o: AgentReqOptions) => { seen.push(o); return Promise.resolve({ code: 0, data: [{ host: '192.168.1.10', success: true, message: 'done', output: 'installed' }] }); };
    const r = await deploy(req, filled(), 'server');
    expect(r.ok).toBe(true);
    expect(seen[0]).toMatchObject({ url: '/Nodes/AgentDeploy/Deploy', method: 'POST', params: { executeOn: 'server' } });
    expect((seen[0].data as Record<string, unknown>).Hosts).toBe('192.168.1.10');
    expect(r.rows[0]).toEqual({ host: '192.168.1.10', success: true, message: 'done', output: 'installed' });
  });
  it('deploy：code=500 → message 透出 + rows 空；网络拒绝 → 异常文案', async () => {
    const r1 = await deploy(() => Promise.resolve({ code: 500, message: 'boom-deploy' }), filled(), 'server');
    expect(r1).toEqual({ ok: false, message: 'boom-deploy', rows: [] });
    const r2 = await deploy(() => Promise.reject(new Error('down')), filled(), 'server');
    expect(r2.ok).toBe(false);
    expect(r2.message).toContain('down');
  });
});

describe('deploySummary / outputText（C08）', () => {
  it('汇总文案：成功 N 个，失败 M 个', () => {
    const rows = mapDeployRows([{ success: true }, { success: false }, { success: true }]);
    expect(deploySummary(rows)).toEqual({ ok: 2, fail: 1, text: '部署完成：成功 2 个，失败 1 个' });
  });
  it('空结果：0/0；非数组容错', () => {
    expect(deploySummary([]).text).toBe('部署完成：成功 0 个，失败 0 个');
    expect(deploySummary(undefined as unknown as ReturnType<typeof mapDeployRows>).ok).toBe(0);
  });
  it('outputText：空白输出 → 无输出；有值原样', () => {
    expect(outputText('')).toBe('无输出');
    expect(outputText('   \n ')).toBe('无输出');
    expect(outputText('Agent installed.')).toBe('Agent installed.');
  });
});
