// Agent 远程部署面板纯逻辑（与 UI 解耦，request 由调用方注入，便于单测）。
// 真实接口（Stardust.WebApi/Areas/Nodes/Controllers/AgentDeployController.cs，2026-11-10 全文核对）：
//  - 首屏默认值 GET /Nodes/AgentDeploy/Index        → { code:0, data:{ serverUrl, downloadUrl } }（:34-40）
//  - 测试连接   POST /Nodes/AgentDeploy/TestConnection，body=JSON {host,port,userName,password,executeOn}（N3 安全改道，2026-11-12）
//      code=0 且 data.success=true 成功；data.success=false（含 executeOn=web 未实现）取 data.message（:98-129）
//  - 开始部署   POST /Nodes/AgentDeploy/Deploy?executeOn=server|web，body=AgentDeployModel 九字段（:47-88）
//      code=0 → data=List<AgentDeployResult>{host,success,message,output}；code=500 → message（executeOn=web 未实现）
// 行为契约（旅程 Doc/用户旅程/节点代理/AgentDeploy.md + 盘点 §3/§7）：
//  - 测试连接仅测 hosts 按逗号/分号/换行拆分后的第一个非空段（C06，修复旧壳传整串差异 index.vue:49）
//  - 四项必填校验：hosts/userName/password/serverUrl 缺项给中文提示并中止请求（C05，旧壳无拦截差异）
//  - executeOn=web：后端未实现，错误/提示文案原样透出，不发 SSH（C09）
//  - 部署结果：汇总「部署完成：成功 N 个，失败 M 个」+ 逐行表格 + 「查看」输出弹窗（空显示「无输出」）（C08）

type Rec = Record<string, unknown>;

/** 请求函数结构类型（兼容 @newlifex/cube-vue request；比共享 Req 多 data 字段，供 Deploy JSON body） */
export interface AgentReqOptions {
  url: string;
  method?: string;
  params?: Record<string, unknown>;
  data?: unknown;
}
export type AgentReq = (options: AgentReqOptions) => Promise<unknown>;

/** 执行位置：server=StarServer执行（推荐）；web=StarWeb本地执行（后端未实现） */
export type ExecuteOn = 'server' | 'web';

/** 部署表单（九字段 + 执行位置） */
export interface DeployForm {
  hosts: string;
  port: number;
  userName: string;
  password: string;
  osType: string;
  serverUrl: string;
  downloadUrl: string;
  dotnetVersion: number;
}

/** .NET 版本选项（MVC Index.cshtml 8/9/10，默认 9） */
export const DOTNET_VERSIONS: ReadonlyArray<{ value: number; label: string }> = [
  { value: 9, label: '9.0' },
  { value: 8, label: '8.0' },
  { value: 10, label: '10.0' },
];

/** 九字段默认值（SSH 端口 22、Linux、dotnet 9；ServerUrl/DownloadUrl 空待 Index 预填） */
export function defaultForm(): DeployForm {
  return {
    hosts: '',
    port: 22,
    userName: '',
    password: '',
    osType: 'Linux',
    serverUrl: '',
    downloadUrl: '',
    dotnetVersion: 9,
  };
}

/** hosts 按 [ , ; 换行 回车] 拆分取第一个非空段；全空返回 ''（C06：仅测第一个地址） */
export function firstHost(hosts: string): string {
  const segs = String(hosts ?? '').split(/[,;\n\r]/);
  for (const s of segs) {
    const t = s.trim();
    if (t) return t;
  }
  return '';
}

/** 四项必填校验（C05）：返回中文提示数组，空数组=通过 */
export function validateDeployForm(f: DeployForm): string[] {
  const errs: string[] = [];
  if (!String(f.hosts ?? '').trim()) errs.push('目标主机不能为空');
  if (!String(f.userName ?? '').trim()) errs.push('用户名不能为空');
  if (!String(f.password ?? '').trim()) errs.push('密码不能为空');
  if (!String(f.serverUrl ?? '').trim()) errs.push('服务器地址不能为空');
  return errs;
}

/** hosts 按分隔符拆分后的非空地址台数（确认框文案「共 N 台」用；分隔规则同 firstHost） */
export function hostCount(hosts: string): number {
  return String(hosts ?? '')
    .split(/[,;\n\r]/)
    .filter((s) => s.trim().length > 0).length;
}

/** 部署请求体：PascalCase 对齐 AgentDeployModel 字段（ASP.NET Core 绑定大小写不敏感，同旧壳 body 同构） */
export function toDeployBody(f: DeployForm): Rec {
  return {
    Hosts: f.hosts,
    Port: f.port,
    UserName: f.userName,
    Password: f.password,
    OSType: f.osType,
    ServerUrl: f.serverUrl,
    DownloadUrl: f.downloadUrl,
    DotnetVersion: f.dotnetVersion,
  };
}

// ---------- 首屏默认值（C02：消费 WebApi Index 预填，修复旧壳未消费差异） ----------

/** GET Index → {serverUrl, downloadUrl}；网络失败返回空串（页面显示默认空表单） */
export async function fetchDeployDefaults(request: AgentReq): Promise<{ serverUrl: string; downloadUrl: string }> {
  try {
    const res = (await request({ url: '/Nodes/AgentDeploy/Index', method: 'GET' })) as Rec;
    const d = (res?.data ?? {}) as Rec;
    return {
      serverUrl: String(d.serverUrl ?? d.ServerUrl ?? ''),
      downloadUrl: String(d.downloadUrl ?? d.DownloadUrl ?? ''),
    };
  } catch {
    return { serverUrl: '', downloadUrl: '' };
  }
}

// ---------- 测试连接（C06/C09） ----------

export interface TestOutcome {
  ok: boolean;
  /** 成功为「连接测试成功」；失败/未实现透出后端 message 原文 */
  message: string;
}

/** 信封判定：code=0 且 data.success=true 成功；code=0+success=false（web 未实现）与 code=500 均取 message 原文 */
export function parseTestResponse(res: unknown): TestOutcome {
  const r = (res ?? {}) as Rec;
  if (Number(r.code) === 0) {
    const d = (r.data ?? {}) as Rec;
    const ok = Boolean(d.success ?? d.Success ?? false);
    return { ok, message: ok ? '连接测试成功' : String(d.message ?? d.Message ?? '连接失败') };
  }
  return { ok: false, message: String(r.message ?? '连接失败') };
}

/** 测试连接：host 仅取第一段（C06）；POST + JSON body（安全 N3：密码不再入查询串，旧 query 形态后端返回改道指引） */
export async function testConnection(request: AgentReq, f: DeployForm, executeOn: ExecuteOn): Promise<TestOutcome> {
  try {
    const res = await request({
      url: '/Nodes/AgentDeploy/TestConnection',
      method: 'POST',
      data: { host: firstHost(f.hosts), port: f.port, userName: f.userName, password: f.password, executeOn },
    });
    return parseTestResponse(res);
  } catch (e) {
    const msg = (e as Error | undefined)?.message ?? String(e);
    return { ok: false, message: `连接测试异常：${msg}` };
  }
}

// ---------- 开始部署（C07/C08） ----------

export interface DeployRow {
  host: string;
  success: boolean;
  message: string;
  output: string;
}

/** 响应 data → 结果行归一（camelCase 为主，兼容 PascalCase） */
export function mapDeployRows(data: unknown): DeployRow[] {
  const arr = Array.isArray(data) ? data : [];
  return arr.map((r) => {
    const rec = (r ?? {}) as Rec;
    return {
      host: String(rec.host ?? rec.Host ?? ''),
      success: Boolean(rec.success ?? rec.Success ?? false),
      message: String(rec.message ?? rec.Message ?? ''),
      output: String(rec.output ?? rec.Output ?? ''),
    };
  });
}

export interface DeployOutcome {
  ok: boolean;
  message: string;
  rows: DeployRow[];
}

/** 部署成功/失败归一；code=500（含 executeOn=web 未实现）透出后端 message 原文 */
export function parseDeployResponse(res: unknown): DeployOutcome {
  const r = (res ?? {}) as Rec;
  if (Number(r.code) === 0) return { ok: true, message: '', rows: mapDeployRows(r.data) };
  return { ok: false, message: String(r.message ?? '部署失败'), rows: [] };
}

/** 开始部署：POST body 九字段 + query executeOn */
export async function deploy(request: AgentReq, f: DeployForm, executeOn: ExecuteOn): Promise<DeployOutcome> {
  try {
    const res = await request({
      url: '/Nodes/AgentDeploy/Deploy',
      method: 'POST',
      params: { executeOn },
      data: toDeployBody(f),
    });
    return parseDeployResponse(res);
  } catch (e) {
    const msg = (e as Error | undefined)?.message ?? String(e);
    return { ok: false, message: `部署异常：${msg}`, rows: [] };
  }
}

/** 汇总：「部署完成：成功 N 个，失败 M 个」（MVC 语义对齐，C08） */
export function deploySummary(rows: DeployRow[]): { ok: number; fail: number; text: string } {
  const list = Array.isArray(rows) ? rows : [];
  const ok = list.filter((r) => r.success).length;
  const fail = list.length - ok;
  return { ok, fail, text: `部署完成：成功 ${ok} 个，失败 ${fail} 个` };
}

/** 输出弹窗文本：空白输出显示「无输出」（C08） */
export function outputText(output: string): string {
  return String(output ?? '').trim() ? output : '无输出';
}

/** 重置：九字段默认 + 执行位置 server（重置后由页面重新预填 Index 默认值） */
export function resetState(): { form: DeployForm; executeOn: ExecuteOn } {
  return { form: defaultForm(), executeOn: 'server' };
}
