/**
 * CARD-NODE-02 Agent 下载页逻辑：六组字段归一、安装命令拼装、探测状态映射。
 * 纯函数，无 Vue/DOM 依赖；请求由调用方注入。
 * 契约（Stardust.WebApi/AgentController.cs）：
 * - GET /Agent → { code, message, data: { server, agent: { zip[], targz[], scripts[] }, packages[], dotnet[] } }
 *   zip/targz 行 = { name, framework, os, url }；scripts/packages/dotnet 行 = { name, description, url }
 * - GET /Agent/Probe → { code, message, data: { online, data } }，data 为 StarAgent info
 */
export interface AgentDlResource {
  name: string;
  meta: string;
  url: string;
}

export interface AgentDlGroups {
  zip: AgentDlResource[];
  targz: AgentDlResource[];
  scripts: AgentDlResource[];
  packages: AgentDlResource[];
  dotnet: AgentDlResource[];
}

export interface ProbeRow {
  label: string;
  value: string;
}

export interface ProbeState {
  online: boolean;
  rows: ProbeRow[];
}

/** 探测状态卡字段（旅程 §7 R2：Name/Title/FileVersion/Compile/OS/MachineName/LocalIP/Server；空值「—」） */
export const PROBE_FIELDS: ReadonlyArray<readonly [string, readonly string[]]> = [
  ['Name', ['name', 'Name']],
  ['Title', ['title', 'Title']],
  ['FileVersion', ['fileVersion', 'FileVersion', 'version', 'Version']],
  ['Compile', ['compile', 'Compile']],
  ['OS', ['os', 'OS']],
  ['MachineName', ['machineName', 'MachineName']],
  ['LocalIP', ['localIP', 'LocalIP']],
  ['Server', ['server', 'Server']],
] as const;

/** 加载失败回退静态命令（与既有 InstallWizard 文案一致，基址回退语义 AgentController.cs:91） */
export const DEFAULT_INSTALL_COMMAND = `# 在目标服务器上执行（需要 .NET 9+ 运行时）
# 1. 下载 StarAgent 压缩包
wget http://x.newlifex.com/star/StarAgent-linux-x64.zip -O /tmp/star-agent.zip

# 2. 解压到安装目录
mkdir -p /opt/star-agent
unzip /tmp/star-agent.zip -d /opt/star-agent
cd /opt/star-agent

# 3. 配置 StarServer 地址并启动
./StarAgent --server http://你的服务器地址:6600
# 或使用 systemd 托管（见文档）`;

type AnyRecord = Record<string, unknown>;

function asRecord(v: unknown): AnyRecord {
  return v && typeof v === 'object' ? (v as AnyRecord) : {};
}

function asArray(v: unknown): AnyRecord[] {
  return Array.isArray(v) ? (v as AnyRecord[]) : [];
}

function pick(r: AnyRecord, keys: readonly string[]): string {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null && v !== '') return String(v);
  }
  return '';
}

/** 六组字段归一：zip/targz 取 framework，其余取 description；大小写兼容、缺省「—」/空 */
export function normalizeAgentData(data: unknown): AgentDlGroups {
  const d = asRecord(data);
  const agent = asRecord(d.agent);
  const row = (r: unknown, metaKeys: readonly string[]): AgentDlResource => {
    const rec = asRecord(r);
    return {
      name: pick(rec, ['name', 'Name']) || '—',
      meta: pick(rec, metaKeys),
      url: pick(rec, ['url', 'Url']),
    };
  };
  return {
    zip: asArray(agent.zip).map((r) => row(r, ['framework', 'Framework'])),
    targz: asArray(agent.targz).map((r) => row(r, ['framework', 'Framework'])),
    scripts: asArray(agent.scripts).map((r) => row(r, ['description', 'Description'])),
    packages: asArray(d.packages).map((r) => row(r, ['description', 'Description'])),
    dotnet: asArray(d.dotnet).map((r) => row(r, ['description', 'Description'])),
  };
}

/** 安装命令：基址消费资源清单（tar.gz 首行 url），清单为空回退静态默认命令 */
export function buildInstallCommand(groups: AgentDlGroups | null): string {
  const first = groups?.targz[0] ?? groups?.zip[0];
  if (!first?.url) return DEFAULT_INSTALL_COMMAND;
  return `# 在目标服务器上执行（需要 .NET 9+ 运行时）
# 1. 下载 StarAgent 压缩包
wget ${first.url} -O /tmp/star-agent.tar.gz

# 2. 解压到安装目录
mkdir -p /opt/star-agent
tar -xzf /tmp/star-agent.tar.gz -C /opt/star-agent
cd /opt/star-agent

# 3. 配置 StarServer 地址并启动
./StarAgent --server http://你的服务器地址:6600
# 或使用 systemd 托管（见文档）`;
}

/** 探测状态映射：online=false 或无 info → 离线；在线按 PROBE_FIELDS 出 8 行，缺字段「—」 */
export function probeState(res: unknown): ProbeState {
  const env = asRecord(asRecord(res).data);
  if (env.online !== true) return { online: false, rows: [] };
  const info = asRecord(env.data);
  if (Object.keys(info).length === 0) return { online: false, rows: [] };
  const rows: ProbeRow[] = PROBE_FIELDS.map(([label, keys]) => ({
    label,
    value: pick(info, keys) || '—',
  }));
  return { online: true, rows };
}
