// 应用流水线：列表、详情、新建/编辑、手动触发、复制 Webhook、失败重跑。
// 对照线上 StarWebApi（2026-11-05）：
//  - 列表 GET /Deployment/AppPipeline?deployId= 有 id/name/branch/buildNodeId/autoDeploy/enable，
//    不返回 deployId、deployNodeIds、token（ListFields 去掉了 Id 以外的敏感列；Id 仍在 JSON 里）。
//  - 详情 GET /Deployment/AppPipeline/{id} 含 deployNodeIds（形如 ",73,73,"）和 token。
//    token 只用于 CopyWebhook，表单与列表都不回显。
//  - 编译节点列表不返回 nodeId，BuildNodeId 是物理节点 Node.Id，用节点名称与 /Nodes/Node 对齐。
//  - 写入用部分更新：PUT /Deployment/AppPipeline/{id} 只提交表单字段，不带 token/secret。
import { okRes, triggerPipeline } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { PipelineRow, Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { requestFailText } from './deploy.logic.ts';

type Rec = Record<string, unknown>;
type ReqWithBody = Req & ((options: { url: string; method?: string; params?: Record<string, unknown>; data?: Rec }) => Promise<{ data?: unknown }>);

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
  return v === undefined || v === null ? '' : String(v);
}
function bool(v: unknown): boolean {
  if (typeof v === 'boolean') return v;
  if (typeof v === 'number') return v !== 0;
  const s = str(v).trim().toLowerCase();
  return s === 'true' || s === '1';
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

/** 业务码非成功（0/1/200）视为失败。没有 code 的空列表不当失败。 */
export function responseFailed(res: unknown): boolean {
  const r = (res ?? {}) as { code?: unknown; success?: unknown };
  if (r.success === false) return true;
  if (typeof r.code === 'number' && r.code !== 0 && r.code !== 1 && r.code !== 200) return true;
  return false;
}

/** 列表卡片。deployNodeIds 为 null 表示列表没带回，不能当成「没有部署节点」。 */
export interface PipelineCard {
  id: number;
  name: string;
  deployId: number;
  projectId: number;
  branch: string;
  buildNodeId: number;
  deployNodeIds: number[] | null;
  autoDeploy: boolean;
  enable: boolean;
  remark: string;
}

export interface PipelineForm {
  name: string;
  branch: string;
  buildNodeId: number;
  deployNodeIds: number[];
  autoDeploy: boolean;
  enable: boolean;
  remark: string;
}

export function emptyPipelineForm(): PipelineForm {
  return { name: '', branch: '', buildNodeId: 0, deployNodeIds: [], autoDeploy: true, enable: true, remark: '' };
}

/** ",73,73," / "73, 74" → 去重后的正整数 */
export function parseIdList(raw: unknown): number[] {
  const seen = new Set<number>();
  for (const part of str(raw).split(/[,，\s]+/)) {
    const n = num(part);
    if (n > 0) seen.add(n);
  }
  return [...seen];
}

export function formatIdList(ids: number[]): string {
  return [...new Set(ids.filter((n) => n > 0))].join(',');
}

export function toPipelineCards(rows: Rec[], deployId: number): PipelineCard[] {
  const out: PipelineCard[] = [];
  for (const r of rows) {
    const id = num(pick(r, 'Id', 'id'));
    if (!id) continue;
    const rawNodes = pick(r, 'DeployNodeIds', 'deployNodeIds');
    out.push({
      id,
      name: str(pick(r, 'Name', 'name')),
      deployId: num(pick(r, 'DeployId', 'deployId')) || deployId,
      projectId: num(pick(r, 'ProjectId', 'projectId')),
      branch: str(pick(r, 'Branch', 'branch')),
      buildNodeId: num(pick(r, 'BuildNodeId', 'buildNodeId')),
      deployNodeIds: rawNodes === undefined ? null : parseIdList(rawNodes),
      autoDeploy: bool(pick(r, 'AutoDeploy', 'autoDeploy')),
      enable: bool(pick(r, 'Enable', 'enable')),
      remark: str(pick(r, 'Remark', 'remark')),
    });
  }
  return out;
}

export function toPipelineForm(r: Rec): PipelineForm {
  return {
    name: str(pick(r, 'Name', 'name')),
    branch: str(pick(r, 'Branch', 'branch')),
    buildNodeId: num(pick(r, 'BuildNodeId', 'buildNodeId')),
    deployNodeIds: parseIdList(pick(r, 'DeployNodeIds', 'deployNodeIds')),
    autoDeploy: bool(pick(r, 'AutoDeploy', 'autoDeploy')),
    enable: bool(pick(r, 'Enable', 'enable')),
    remark: str(pick(r, 'Remark', 'remark')),
  };
}

export function cardToPipelineRow(c: PipelineCard): PipelineRow {
  return {
    id: c.id,
    name: c.name,
    deployId: c.deployId,
    branch: c.branch,
    buildNodeId: c.buildNodeId,
    autoDeploy: c.autoDeploy,
    enable: c.enable,
  };
}

export function validatePipelineForm(m: PipelineForm): string {
  if (!m.name.trim()) return '流水线名称必填';
  if (!m.buildNodeId) return '请选择编译节点';
  if (!m.deployNodeIds.length) return '请至少选择一个部署节点';
  return '';
}

/** 写入体只含表单字段。不带 token / secret，避免把详情里的凭据再提交一遍。 */
export function pipelineWriteBody(m: PipelineForm, extra?: { deployId?: number; projectId?: number }): Rec {
  const body: Rec = {
    name: m.name.trim(),
    branch: m.branch.trim(),
    buildNodeId: m.buildNodeId,
    deployNodeIds: formatIdList(m.deployNodeIds),
    autoDeploy: m.autoDeploy,
    enable: m.enable,
    remark: m.remark.trim(),
  };
  if (extra?.deployId) body.deployId = extra.deployId;
  if (extra?.projectId) body.projectId = extra.projectId;
  return body;
}

export interface CompileChoice { nodeId: number; label: string }
export interface DeployChoice { id: number; label: string }

/** 编译节点列表没有 nodeId，按节点名称对齐物理节点编号；当前值对不上时保留「节点 #id」。 */
export function compileNodeChoices(
  builds: ReadonlyArray<{ nodeName: string; enable: boolean }>,
  nodes: ReadonlyArray<{ id: number; name: string }>,
  currentId = 0,
): CompileChoice[] {
  const byName = new Map<string, number>();
  for (const n of nodes) if (n.id > 0 && n.name) byName.set(n.name, n.id);
  const out: CompileChoice[] = [];
  const seen = new Set<number>();
  for (const b of builds) {
    if (!b.enable || !b.nodeName) continue;
    const id = byName.get(b.nodeName) ?? 0;
    if (!id || seen.has(id)) continue;
    seen.add(id);
    out.push({ nodeId: id, label: b.nodeName });
  }
  if (currentId > 0 && !seen.has(currentId)) out.unshift({ nodeId: currentId, label: `节点 #${currentId}` });
  return out;
}

/** 部署目标是 AppDeployNode.Id。详情里有、当前列表没有的编号仍保留，避免保存时丢掉。 */
export function deployTargetChoices(
  nodes: ReadonlyArray<{ id: number; name: string; ip: string }>,
  selected: readonly number[],
): DeployChoice[] {
  const out: DeployChoice[] = nodes
    .filter((n) => n.id > 0)
    .map((n) => ({ id: n.id, label: n.ip ? `${n.name} · ${n.ip}` : n.name }));
  const have = new Set(out.map((x) => x.id));
  for (const id of selected) {
    if (id > 0 && !have.has(id)) out.push({ id, label: `部署节点 #${id}（当前列表没有）` });
  }
  return out;
}

/** 失败或已取消才允许重跑。进行中的运行再跑会叠一次编译。 */
export function canReprocess(status: string): boolean {
  return status === 'Failed' || status === 'Cancelled';
}

export async function fetchPipelineCards(request: Req, deployId: number): Promise<PipelineCard[] | null> {
  if (!deployId || deployId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppPipeline', method: 'GET', params: { deployId, PageSize: 50 } });
    if (responseFailed(res)) return null;
    return toPipelineCards(listOf(res), deployId);
  } catch {
    return null;
  }
}

export async function fetchPipelineDetail(request: Req, id: number): Promise<PipelineForm | null> {
  if (!id || id <= 0) return null;
  try {
    const res = await request({ url: `/Deployment/AppPipeline/${id}`, method: 'GET' });
    if (responseFailed(res)) return null;
    const d = (res as { data?: Rec } | undefined)?.data;
    return d ? toPipelineForm(d) : null;
  } catch {
    return null;
  }
}

async function projectIdOf(request: Req, deployId: number): Promise<number> {
  try {
    const res = await request({ url: '/Deployment/AppPipeline/GetDeployProject', method: 'GET', params: { deployId } });
    if (responseFailed(res)) return 0;
    return num((res as { data?: unknown }).data);
  } catch {
    return 0;
  }
}

export async function createPipeline(request: Req, deployId: number, form: PipelineForm): Promise<{ ok: boolean; message: string }> {
  const err = validatePipelineForm(form);
  if (err) return { ok: false, message: err };
  if (!deployId || deployId <= 0) return { ok: false, message: '缺少部署集编号，无法创建流水线' };
  const projectId = await projectIdOf(request, deployId);
  try {
    const res = await (request as ReqWithBody)({
      url: '/Deployment/AppPipeline',
      method: 'POST',
      data: pipelineWriteBody(form, { deployId, projectId: projectId || undefined }),
    });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '流水线已创建' : r.message || '流水线创建失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '流水线创建失败') };
  }
}

export async function savePipeline(request: Req, id: number, form: PipelineForm): Promise<{ ok: boolean; message: string }> {
  const err = validatePipelineForm(form);
  if (err) return { ok: false, message: err };
  if (!id || id <= 0) return { ok: false, message: '缺少流水线编号，无法保存' };
  try {
    const res = await (request as ReqWithBody)({
      url: `/Deployment/AppPipeline/${id}`,
      method: 'PUT',
      data: pipelineWriteBody(form),
    });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '流水线已保存' : r.message || '流水线保存失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '流水线保存失败') };
  }
}

export async function triggerPipelineRun(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少流水线编号，无法触发' };
  try {
    return await triggerPipeline(request, id);
  } catch (e) {
    return { ok: false, message: requestFailText(e, '触发失败') };
  }
}

/** 复制用的地址。成功时 url 含 token，调用方写入剪贴板，不要写进日志。 */
export async function fetchWebhookUrl(request: Req, id: number): Promise<{ ok: boolean; url: string; message: string }> {
  if (!id || id <= 0) return { ok: false, url: '', message: '缺少流水线编号，无法复制 Webhook' };
  try {
    const res = await request({ url: '/Deployment/AppPipeline/CopyWebhook', method: 'GET', params: { id } });
    const r = okRes(res);
    const url = typeof r.data === 'string' ? r.data.trim() : '';
    if (!r.ok || !url) return { ok: false, url: '', message: r.message || '获取 Webhook 地址失败' };
    return { ok: true, url, message: 'Webhook 地址已复制' };
  } catch (e) {
    return { ok: false, url: '', message: requestFailText(e, '获取 Webhook 地址失败') };
  }
}

export async function reprocessPipelineRun(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少运行编号，无法重跑' };
  try {
    const res = await request({ url: '/Deployment/AppPipelineRun/Reprocess', method: 'GET', params: { id } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '流水线已重新触发' : r.message || '重跑失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '重跑失败') };
  }
}
