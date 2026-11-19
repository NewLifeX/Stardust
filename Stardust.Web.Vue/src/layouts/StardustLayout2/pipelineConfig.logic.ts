// 流水线配置纯逻辑（CARD-DEP-01，来源旅程 J-DEP-07，2026-11-07）。
// 无 Vue 依赖，request 由调用方注入，便于单测。接口据侦察报告 ①1.1（Doc/用户旅程/试点-流水线-实现侦察.md）：
//  - 列表：GET /Deployment/AppPipeline?deployId=&PageSize=50
//  - 新增：POST /Deployment/AppPipeline（Token 由后端 Valid 自动生成，前端不提交；流水线.Biz.cs:68-74）
//  - 更新：PUT /Deployment/AppPipeline/{id}（部分更新：只提交表单覆盖的顶层字段）
//  - 删除：DELETE /Deployment/AppPipeline/{id}
//  - 复制：GET /Deployment/AppPipeline/CopyWebhook?id= → { code, message, data: url }（url={ServiceAddress}/Deployment/Pipeline/Webhook?token={Token}）
//  - 启动：GET /Deployment/AppPipeline/Trigger?id= → 成功 message「流水线已触发，运行编号[N]」
//  - 级联：GET /Deployment/AppPipeline/GetDeployNodes?deployId= → [{ Id, name:"节点名 (IP)" }]（仅启用节点）
//          GET /Deployment/AppPipeline/GetDeployProject?deployId= → { ProjectId }
//          GET /Nodes/Node/NodeSearch?projectId=&product=StarDeploy&key=（编译节点 suggest；请求形态按旧壳
//            _Form_SelectBuildNode.cshtml:42 核实，响应为 { data: [{id, code, name, projectName, category, ip}] }）
// 敏感字段 Token/Secret/TraceId 不进表单、不提交（实现规范 §6.5 + 盘点 §8 隐藏清单）。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
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

function fail(e: unknown, fallback: string): { ok: boolean; message: string } {
  const anyE = e as { message?: unknown } | null;
  const msg = typeof anyE?.message === 'string' ? anyE.message : '';
  return { ok: false, message: msg ? `${fallback}：${msg}` : fallback };
}

// ---------- 流水线列表行 ----------

export interface PipelineRow {
  id: number;
  name: string;
  deployId: number;
  branch: string;
  /** 编译节点 Id（NodeSearch 选中的物理节点） */
  buildNodeId: number;
  /** 部署集绑定的项目 Id（GetDeployProject 级联结果，列表接口缺失按 0） */
  projectId: number;
  /** 部署节点 Id 逗号串（实体原样，提交前由表单同步） */
  deployNodeIds: string;
  autoDeploy: boolean;
  enable: boolean;
  updateTime: string;
}

export function toPipelineRows(rows: Rec[]): PipelineRow[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id')),
    name: str(pick(r, 'Name', 'name')),
    deployId: num(pick(r, 'DeployId', 'deployId')),
    branch: str(pick(r, 'Branch', 'branch')),
    buildNodeId: num(pick(r, 'BuildNodeId', 'buildNodeId')),
    projectId: num(pick(r, 'ProjectId', 'projectId')),
    deployNodeIds: str(pick(r, 'DeployNodeIds', 'deployNodeIds')),
    autoDeploy: bool(pick(r, 'AutoDeploy', 'autoDeploy')),
    enable: bool(pick(r, 'Enable', 'enable')),
    updateTime: str(pick(r, 'UpdateTime', 'updateTime')),
  }));
}

export async function fetchPipelineList(request: Req, deployId: number): Promise<PipelineRow[] | null> {
  if (!deployId || deployId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppPipeline', method: 'GET', params: { deployId, PageSize: 50 } });
    return toPipelineRows(listOf(res));
  } catch {
    return null;
  }
}

// ---------- 级联：部署节点选项 / 项目 Id ----------

export interface PipelineNodeOption {
  id: number;
  /** 后端拼好的「节点名 (IP)」 */
  label: string;
}

export function toDeployNodeOptions(rows: Rec[]): PipelineNodeOption[] {
  return rows.map((r) => ({ id: num(pick(r, 'Id', 'id')), label: str(pick(r, 'name', 'Name')) }));
}

/** GET /Deployment/AppPipeline/GetDeployNodes?deployId=：仅启用节点，选项文案「节点名 (IP)」由后端拼好 */
export async function fetchDeployNodeOptions(request: Req, deployId: number): Promise<PipelineNodeOption[] | null> {
  if (!deployId || deployId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppPipeline/GetDeployNodes', method: 'GET', params: { deployId } });
    return toDeployNodeOptions(listOf(res));
  } catch {
    return null;
  }
}

/** GET /Deployment/AppPipeline/GetDeployProject?deployId=：返回部署集绑定的项目 Id（流水线.Biz 级联源） */
export async function fetchPipelineProjectId(request: Req, deployId: number): Promise<number | null> {
  if (!deployId || deployId <= 0) return null;
  try {
    const res = await request({ url: '/Deployment/AppPipeline/GetDeployProject', method: 'GET', params: { deployId } });
    const r = okRes(res);
    const d = (r.data ?? {}) as Rec;
    return r.ok ? num(pick(d, 'ProjectId', 'projectId')) : null;
  } catch {
    return null;
  }
}

// ---------- 级联：编译节点 NodeSearch suggest ----------

export interface BuildNodeSearchItem {
  id: number;
  name: string;
  ip: string;
  projectName: string;
}

export function toBuildNodeSearchItems(rows: Rec[]): BuildNodeSearchItem[] {
  return rows.map((r) => ({
    id: num(pick(r, 'Id', 'id', 'nodeID', 'nodeId')),
    name: str(pick(r, 'Name', 'name', 'code', 'Code')) || `节点 ${num(pick(r, 'Id', 'id'))}`,
    ip: str(pick(r, 'IP', 'ip', 'iP')),
    projectName: str(pick(r, 'ProjectName', 'projectName')),
  }));
}

/** GET /Nodes/Node/NodeSearch?projectId=&product=StarDeploy&key=：按项目过滤的节点 suggest（旧壳 _Form_SelectBuildNode 同参） */
export async function searchBuildNodes(request: Req, projectId: number, key: string): Promise<BuildNodeSearchItem[] | null> {
  try {
    const res = await request({
      url: '/Nodes/Node/NodeSearch',
      method: 'GET',
      params: { projectId, product: 'StarDeploy', key },
    });
    return toBuildNodeSearchItems(listOf(res));
  } catch {
    return null;
  }
}

// ---------- 表单 ----------

export interface PipelineForm {
  name: string;
  branch: string;
  buildNodeId: number;
  /** 部署节点 Id 数组（提交时同步为逗号串） */
  deployNodeIds: number[];
  autoDeploy: boolean;
  enable: boolean;
}

/** 列表行 → 表单初值（编辑回填）；敏感字段 Token/Secret/TraceId 不进表单 */
export function toPipelineForm(r: PipelineRow): PipelineForm {
  return {
    name: r.name,
    branch: r.branch,
    buildNodeId: r.buildNodeId,
    deployNodeIds: r.deployNodeIds
      .split(',')
      .map((s) => Number(s.trim()))
      .filter((n) => Number.isFinite(n) && n > 0),
    autoDeploy: r.autoDeploy,
    enable: r.enable,
  };
}

export function emptyPipelineForm(): PipelineForm {
  return { name: '', branch: '', buildNodeId: 0, deployNodeIds: [], autoDeploy: false, enable: true };
}

/** 校验：名称必填（后端另有唯一性校验，错误经 message 透传） */
export function validatePipelineForm(m: PipelineForm): string {
  if (!m.name.trim()) return '名称不能为空';
  return '';
}

/** 部署节点 Id 数组 → 逗号串（提交前同步） */
export function joinDeployNodeIds(ids: number[]): string {
  return ids.join(',');
}

/** 提交体：新增（Token 不提交，后端自动生成；DeployNodeIds 逗号串同步） */
export function pipelineInsertBody(deployId: number, m: PipelineForm): Rec {
  return {
    deployId,
    name: m.name.trim(),
    branch: m.branch.trim(),
    buildNodeId: m.buildNodeId,
    deployNodeIds: joinDeployNodeIds(m.deployNodeIds),
    autoDeploy: m.autoDeploy,
    enable: m.enable,
  };
}

/** 提交体：更新（部分更新，主键在路径） */
export function pipelineUpdateBody(m: PipelineForm): Rec {
  return {
    name: m.name.trim(),
    branch: m.branch.trim(),
    buildNodeId: m.buildNodeId,
    deployNodeIds: joinDeployNodeIds(m.deployNodeIds),
    autoDeploy: m.autoDeploy,
    enable: m.enable,
  };
}

// ---------- CRUD 与行操作 ----------

/** 新增流水线（POST /Deployment/AppPipeline） */
export async function createPipeline(request: Req, deployId: number, m: PipelineForm): Promise<{ ok: boolean; message: string }> {
  if (!deployId || deployId <= 0) return { ok: false, message: '缺少部署集编号' };
  const err = validatePipelineForm(m);
  if (err) return { ok: false, message: err };
  try {
    const res = await request({ url: '/Deployment/AppPipeline', method: 'POST', data: pipelineInsertBody(deployId, m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '流水线已创建' : r.message || '流水线创建失败' };
  } catch (e) {
    return fail(e, '流水线创建失败');
  }
}

/** 更新流水线（PUT /Deployment/AppPipeline/{id}） */
export async function updatePipeline(request: Req, id: number, m: PipelineForm): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少流水线编号' };
  const err = validatePipelineForm(m);
  if (err) return { ok: false, message: err };
  try {
    const res = await request({ url: `/Deployment/AppPipeline/${id}`, method: 'PUT', data: pipelineUpdateBody(m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '流水线已保存' : r.message || '流水线保存失败' };
  } catch (e) {
    return fail(e, '流水线保存失败');
  }
}

/** 删除流水线（DELETE /Deployment/AppPipeline/{id}） */
export async function deletePipeline(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少流水线编号' };
  try {
    const res = await request({ url: `/Deployment/AppPipeline/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '流水线已删除' : r.message || '流水线删除失败' };
  } catch (e) {
    return fail(e, '流水线删除失败');
  }
}

/** 复制 WebHook 地址（GET /Deployment/AppPipeline/CopyWebhook?id=，data=完整 URL） */
export async function copyWebhookUrl(request: Req, id: number): Promise<{ ok: boolean; message: string; url: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少流水线编号', url: '' };
  try {
    const res = await request({ url: '/Deployment/AppPipeline/CopyWebhook', method: 'GET', params: { id } });
    const r = okRes(res);
    const url = str(r.data);
    if (!r.ok || !url) return { ok: false, message: r.message || '未取得 WebHook 地址', url: '' };
    return { ok: true, message: r.message || 'ok', url };
  } catch (e) {
    return { ...fail(e, '复制 WebHook 地址失败'), url: '' };
  }
}

/** 手动启动流水线（GET /Deployment/AppPipeline/Trigger?id=；成功 message 含运行编号） */
export async function triggerPipeline(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少流水线编号' };
  try {
    const res = await request({ url: '/Deployment/AppPipeline/Trigger', method: 'GET', params: { id } });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '触发成功' : r.message || '触发失败' };
  } catch (e) {
    return fail(e, '触发失败');
  }
}

/**
 * 从触发/重跑成功 message 中解析运行编号（task-88 2026-11-12）。
 * 后端格式：Trigger「流水线已触发，运行编号[N]」/ Reprocess「流水线已重新触发，运行编号[N]」，
 * N = AppPipelineRun.Id（WebApi AppPipelineController.Trigger → PipelineService.Trigger 新建运行记录），
 * 即 fetchRunDetail / 运行行 / 运行详情页共用的运行 Id，可直接开详情。解析不到返回 0（不带直达动作）。
 */
export function parseTriggerRunNo(message: string): number {
  const m = /运行编号\[(\d+)\]/.exec(message ?? '');
  return m ? Number(m[1]) || 0 : 0;
}

// ---------- 剪贴板（依赖注入 DOM，便于单测） ----------

/** 写剪贴板：优先 navigator.clipboard，不可用（非安全上下文/无权限）降级为临时 textarea。成功返回 true */
export async function copyTextToClipboard(
  text: string,
  nav: { clipboard?: { writeText?: (t: string) => Promise<void> } },
  doc: Document,
): Promise<boolean> {
  if (!text) return false;
  if (nav.clipboard?.writeText) {
    try {
      await nav.clipboard.writeText(text);
      return true;
    } catch {
      // 落到 textarea 降级
    }
  }
  try {
    const ta = doc.createElement('textarea');
    ta.value = text;
    ta.style.position = 'fixed';
    ta.style.opacity = '0';
    doc.body.appendChild(ta);
    ta.select();
    const ok = doc.execCommand('copy');
    doc.body.removeChild(ta);
    return ok;
  } catch {
    return false;
  }
}

/** 更新时间展示：YYYY-MM-DD HH:mm，空为「—」 */
export function fmtPipelineTime(s: string | undefined): string {
  if (!s) return '—';
  return s.replace('T', ' ').slice(0, 16);
}
