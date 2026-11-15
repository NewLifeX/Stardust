// 网关证书维护纯逻辑（CARD-GW-04，来源旅程 J-GW-01；2026-11-07）。
// 路由契约（deploy.logic.ts:2-8 同款；列表契约与 GW-01 实测的 GatewayCluster 同为 EntityController 标准形状，
// GatewayCertController.cs 44 行无自定义 Action）：
//  - 编辑一律按 id 拉 Detail 回填（列表只含 7 列字段，同 CARD-GW-01/03 先例）
//  - 请求统一相对路径（request 拼 baseURL，勿写 /api）
// 敏感处理（定稿硬条款，本卡验收重点）：
//  - PfxPassword「加密存储」（SSL证书.cs:59-63）：①列表无列（控制器 ListFields.RemoveField :19-21）；
//    ②新增 type=password 输入（组件层）；③**编辑不回显**——detailToCertForm 恒置空串（不读 Detail 值），
//    留空=不提交=不修改（PUT 部分更新，先例 deploy.logic.ts:86-103 RepoPassword）；④任何路径不输出该值；
//    Detail 序列化未核实（6681 库 0 行 + 只读冒烟红线，回填 CARD-GW-04 §6.2）→ 前端契约永不渲染。
//  - KeyFile/PemFile 仅登记服务端路径，不预览不下载私钥内容（组件层）。
// 上传：WebApi 无 ItemType="file" 上传端点（控制器无自定义 Action，回填 CARD-GW-04 开工冒烟）→ 纯路径输入。
// 筛选语义（控制器 Search :34-43 + 实体 Search SSL证书.cs:324-334）：enable、dtStart/dtEnd（过滤 **NotAfter**，
// 控件注记「按有效期至（NotAfter）过滤」）、Q；**domain 读取但不传 Search（:36 未用）→ 不提供域名搜索控件**
// （定稿裁量 2：复刻即假搜索）；autoRenew 不参与过滤；**projectId 永不传**（C11 硬断言：证书=全局资源，
// 实体无 ProjectId 字段，本文件任何请求不得携带 projectId）。
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

type Rec = Record<string, unknown>;

function pick(r: Rec, ...keys: string[]): unknown {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
}
function num(v: unknown): number {
  const n = Number(v);
  return Number.isFinite(n) ? n : 0;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v).trim();
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  return str(v).toLowerCase() === 'true' || str(v) === '1';
}
function errText(e: unknown, fallback: string): string {
  const msg = e instanceof Error ? e.message : typeof e === 'string' ? e : '';
  return msg || fallback;
}
function okOf(res: { message?: unknown } | undefined, fallback: string): { ok: boolean; message: string } {
  const code = res && (res as Rec).code !== undefined ? num((res as Rec).code) : -1;
  const message = res ? str(res.message) : '';
  return { ok: code === 0, message: message || fallback };
}

/** 日期归一：'YYYY-MM-DD HH:mm:ss' → 'YYYY-MM-DD'；DateTime 空值（0001-01-01）→ 空串（避免空日期回传） */
function fmtDate(v: unknown): string {
  const s = str(v);
  if (!s || s.startsWith('0001-01-01')) return '';
  return s.slice(0, 10);
}

/** 证书列表归一行（区块 7 列：域名/启用/PFX文件/证书文件/私钥文件/PEM文件/操作，无 Id 编号列）。
 * 列表 NotAfter 是否随 JSON 序列化未核实（控制器 ListFields.RemoveField 含 NotAfter，回填 CARD-GW-04）——
 * notAfterMs 仅作本地排序键（定稿：过期时间升序临期在前），列表无值时不参与排序（保持原序），不展示。 */
export interface CertRow {
  id: number;
  domain: string;
  enable: boolean;
  pfxFile: string;
  crtFile: string;
  keyFile: string;
  pemFile: string;
  /** 本地排序键：NotAfter 毫秒；取不到=0 排最后 */
  notAfterMs: number;
}

/** 列表原始行 → 归一行（本地排序=过期时间升序临期在前，定稿裁定；无值行保持相对次序排后） */
export function toCertRows(rows: Rec[]): CertRow[] {
  const out: CertRow[] = [];
  for (const r of rows) {
    const t = Date.parse(str(pick(r, 'NotAfter', 'notAfter')).replace(' ', 'T'));
    out.push({
      id: num(pick(r, 'Id', 'id')),
      domain: str(pick(r, 'Domain', 'domain')),
      enable: bool(pick(r, 'Enable', 'enable')),
      pfxFile: str(pick(r, 'PfxFile', 'pfxFile')),
      crtFile: str(pick(r, 'CrtFile', 'crtFile')),
      keyFile: str(pick(r, 'KeyFile', 'keyFile')),
      pemFile: str(pick(r, 'PemFile', 'pemFile')),
      notAfterMs: Number.isFinite(t) ? t : 0,
    });
  }
  return out.sort((a, b) => {
    if (a.notAfterMs && b.notAfterMs && a.notAfterMs !== b.notAfterMs) return a.notAfterMs - b.notAfterMs;
    if (a.notAfterMs && !b.notAfterMs) return -1;
    if (!a.notAfterMs && b.notAfterMs) return 1;
    return 0;
  });
}

/** 证书表单（除 Id 外全部业务字段，CARD-GW-04 数据契约：基础 5 / 证书文件 5 / 证书信息 6 / 备注） */
export interface GatewayCertForm {
  // 基础
  domain: string;
  enable: boolean;
  autoRenew: boolean;
  provider: string;
  renewDays: number;
  // 证书文件（服务端路径；WebApi 无上传端点 → 纯路径输入，title 注明）
  pfxFile: string;
  /** PFX密码：新增可输入；**编辑恒为空串**（detailToCertForm 不回填），留空=不提交=不修改（硬条款③） */
  pfxPassword: string;
  crtFile: string;
  keyFile: string;
  pemFile: string;
  // 证书信息（一般由工具自动维护，抽屉内折叠组；行内展开亦以此承载有效期）
  issuer: string;
  subject: string;
  notBefore: string;
  notAfter: string;
  thumbprint: string;
  lastRenew: string;
  // 备注
  remark: string;
}

/** 新建表单默认值（=SSL证书.cs BindColumn DefaultValue：RenewDays 30；Enable XCode 布尔默认 false——
 * 旅程/卡片定稿「启用 Enable（默认开）」按业务默认置 true） */
export function emptyCertForm(): GatewayCertForm {
  return {
    domain: '',
    enable: true,
    autoRenew: false,
    provider: '',
    renewDays: 30,
    pfxFile: '',
    pfxPassword: '',
    crtFile: '',
    keyFile: '',
    pemFile: '',
    issuer: '',
    subject: '',
    notBefore: '',
    notAfter: '',
    thumbprint: '',
    lastRenew: '',
    remark: '',
  };
}

/** Detail 实体 → 表单（编辑回填全字段；字段大小写两种都认）。
 * **PfxPassword 恒置空串**（硬条款③编辑不回显——不读 Detail 值，无论 Detail 是否序列化该字段）。 */
export function detailToCertForm(d: Rec): GatewayCertForm {
  return {
    domain: str(pick(d, 'Domain', 'domain')),
    enable: bool(pick(d, 'Enable', 'enable')),
    autoRenew: bool(pick(d, 'AutoRenew', 'autoRenew')),
    provider: str(pick(d, 'Provider', 'provider')),
    renewDays: num(pick(d, 'RenewDays', 'renewDays')),
    pfxFile: str(pick(d, 'PfxFile', 'pfxFile')),
    pfxPassword: '',
    crtFile: str(pick(d, 'CrtFile', 'crtFile')),
    keyFile: str(pick(d, 'KeyFile', 'keyFile')),
    pemFile: str(pick(d, 'PemFile', 'pemFile')),
    issuer: str(pick(d, 'Issuer', 'issuer')),
    subject: str(pick(d, 'Subject', 'subject')),
    notBefore: fmtDate(pick(d, 'NotBefore', 'notBefore')),
    notAfter: fmtDate(pick(d, 'NotAfter', 'notAfter')),
    thumbprint: str(pick(d, 'Thumbprint', 'thumbprint')),
    lastRenew: fmtDate(pick(d, 'LastRenew', 'lastRenew')),
    remark: str(pick(d, 'Remark', 'remark')),
  };
}

/** 表单字段 → 提交体字段名（实体 PascalCase；GetSubmittedFields 大小写不敏感） */
const FORM_FIELDS: ReadonlyArray<readonly [keyof GatewayCertForm, string]> = [
  ['domain', 'Domain'],
  ['enable', 'Enable'],
  ['autoRenew', 'AutoRenew'],
  ['provider', 'Provider'],
  ['renewDays', 'RenewDays'],
  ['pfxFile', 'PfxFile'],
  ['pfxPassword', 'PfxPassword'],
  ['crtFile', 'CrtFile'],
  ['keyFile', 'KeyFile'],
  ['pemFile', 'PemFile'],
  ['issuer', 'Issuer'],
  ['subject', 'Subject'],
  ['notBefore', 'NotBefore'],
  ['notAfter', 'NotAfter'],
  ['thumbprint', 'Thumbprint'],
  ['lastRenew', 'LastRenew'],
  ['remark', 'Remark'],
];

/** 内联校验：Domain 必填（后端 ArgumentNullException 同义，SSL证书.Biz.cs:64） */
export function validateCertForm(m: GatewayCertForm): { domain?: string } {
  const errs: { domain?: string } = {};
  if (!m.domain.trim()) errs.domain = '域名不能为空';
  return errs;
}

/** 新建提交体：全字段（body 不带 Id，布尔发布尔；PfxPassword 可为空串=不设密码） */
export function certInsertPayload(m: GatewayCertForm): Rec {
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) body[backing] = m[key];
  return body;
}

/** 编辑提交体：部分更新——与 Detail 逐字段比对，只提交变更顶层字段（不提交=不改，机制同 CARD-GW-01/03）。
 * **PfxPassword 特例（硬条款③）**：编辑恒为空串，与 Detail 比对必「变更」，故跳过 diff——
 * 仅当用户输入了新密码（非空）才提交（留空=不提交=不修改，RepoPassword 先例 deploy.logic.ts:86-103）。 */
export function certUpdatePayload(detail: Rec, m: GatewayCertForm): Rec {
  const base = detailToCertForm(detail);
  const body: Rec = {};
  for (const [key, backing] of FORM_FIELDS) {
    if (key === 'pfxPassword') continue;
    if (m[key] !== base[key]) body[backing] = m[key];
  }
  if (m.pfxPassword) body.PfxPassword = m.pfxPassword;
  return body;
}

/** 证书筛选条状态（C04：enable/dtStart-dtEnd 过滤 NotAfter/Q；autoRenew 不参与；不提供域名控件——裁量 2） */
export interface GatewayCertFilter {
  /** 勾选=只看启用证书；不勾=不过滤 */
  enable: boolean;
  /** 时间区间，后端过滤 NotAfter（控件注记「按有效期至（NotAfter）过滤」） */
  dtStart: string;
  dtEnd: string;
  /** 关键字 → Q */
  q: string;
}

export function emptyCertFilter(): GatewayCertFilter {
  return { enable: false, dtStart: '', dtEnd: '', q: '' };
}

export function isCertFilterActive(f: GatewayCertFilter): boolean {
  const d = emptyCertFilter();
  return f.enable !== d.enable || f.dtStart !== d.dtStart || f.dtEnd !== d.dtEnd || f.q !== d.q;
}

/** 筛选参数拼装：开关不勾/时间空/Q 空=不传（Q 映射后端参数名）。
 * **C11 硬断言：本函数永不产生 projectId/domain/autoRenew 键**（证书=全局资源；域名过滤无效不复刻）。 */
export function certFilterParams(f: GatewayCertFilter): Record<string, unknown> {
  const p: Record<string, unknown> = {};
  if (f.enable) p.enable = true;
  if (f.dtStart.trim()) p.dtStart = f.dtStart.trim();
  if (f.dtEnd.trim()) p.dtEnd = f.dtEnd.trim();
  if (f.q.trim()) p.Q = f.q.trim();
  return p;
}

/** 单条 Detail：GET /Gateway/GatewayCert/{id}；无数据/HTTP 错 → null（调用方显示失败态） */
export async function fetchCertDetail(request: Req, id: number): Promise<Rec | null> {
  try {
    const res = await request({ url: `/Gateway/GatewayCert/${id}`, method: 'GET' });
    const d = (res as Rec)?.data;
    return d && !Array.isArray(d) ? (d as Rec) : null;
  } catch {
    return null;
  }
}

/** 新增/编辑提交：编辑只提交变更字段（PUT /{id}，PfxPassword 留空不提交）；失败返回 ok=false 由调用方 toast */
export async function submitCert(request: Req, m: GatewayCertForm, id?: number): Promise<{ ok: boolean; message: string }> {
  try {
    if (id) {
      const detail = await fetchCertDetail(request, id);
      if (!detail) return { ok: false, message: '证书不存在或已被删除' };
      const res = await request({ url: `/Gateway/GatewayCert/${id}`, method: 'PUT', data: certUpdatePayload(detail, m) } as ReqOptions & { data: Rec });
      return okOf(res as Rec, '证书已保存');
    }
    const res = await request({ url: '/Gateway/GatewayCert', method: 'POST', data: certInsertPayload(m) } as ReqOptions & { data: Rec });
    return okOf(res as Rec, '证书已创建');
  } catch (e) {
    return { ok: false, message: errText(e, '证书保存失败') };
  }
}

/** 删除：DELETE /Gateway/GatewayCert/{id}（删除后依赖它的路由 TLS 列显示「待接入」，J-GW-01 C15） */
export async function removeCert(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: `/Gateway/GatewayCert/${id}`, method: 'DELETE' });
    return okOf(res as Rec, '证书已删除');
  } catch (e) {
    return { ok: false, message: errText(e, '证书删除失败') };
  }
}
