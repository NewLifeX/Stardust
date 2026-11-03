// 发布部署收口纯逻辑（版本上传、部署集/节点、流水线等）。无 Vue 依赖，request 由调用方注入，便于单测。
// 路由契约（Cube/NewLife.Cube/Common/EntityController.cs + ReadOnlyEntityController.cs，2026-11-03 回读源码）：
//  - 单条：GET  /api/[area]/[controller]/{id}   → { code, data: 实体 }（Detail）
//  - 新增：POST /api/[area]/[controller]        → JSON 实体体（Insert，body 不带 id）
//  - 更新：PUT  /api/[area]/[controller]/{id}   → 部分更新：只覆盖请求 JSON 顶层字段（GetSubmittedFields 大小写不敏感），主键在路径
//  - 删除：DELETE /api/[area]/[controller]/{id}；批量 DELETE ?ids=a,b,c（DeleteAll）
//  - 自定义动作：GET/POST /api/[area]/[controller]/{action}（Operate/BatchOperate/SyncApp/Upload 先例）
// 响应字段大小写两种都认。注意：AppDeploy.RepoPassword 编辑语义 =「留空不修改」（避免把掩码/密文当新密码二次加密）。
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

/** 提取请求失败的人类可读信息；状态 404/405 识别为「接口未部署或未发布到当前 WebApi」 */
export function requestFailText(e: unknown, fallback: string): string {
  const anyE = e as { status?: unknown; response?: { status?: unknown }; message?: unknown } | null;
  const st = num(anyE?.status) || num(anyE?.response?.status);
  if (st === 404 || st === 405) return `${fallback}：接口部署中（后端未就绪，40${st === 404 ? '4' : '5'}），请稍后重试`;
  const msg = anyE?.message;
  if (typeof msg === 'string' && /\b(404|405)\b/.test(msg)) return `${fallback}：接口部署中（后端未就绪），请稍后重试`;
  return msg ? `${fallback}：${msg}` : fallback;
}

// ================= F06 部署集编辑 + 同步应用 =================

/** 部署集编辑表单。RepoPassword 永不回显：显示为空，留空提交=不修改 */
export interface DeployEditModel {
  name: string;
  category: string;
  enable: boolean;
  packageName: string;
  autoPublish: boolean;
  multiVersion: boolean;
  repository: string;
  repoUserName: string;
  /** 始终为空串；仅当用户输入新值才提交（repoPasswordInBody） */
  repoPassword: string;
  branch: string;
  projectPath: string;
  buildArgs: string;
  packageFilters: string;
  remark: string;
}

/** 单条实体（GET /Deployment/AppDeploy/{id} 的 data，全量字段）→ 编辑表单 */
export function toDeployEditModel(r: Rec): DeployEditModel {
  return {
    name: str(pick(r, 'Name', 'name')),
    category: str(pick(r, 'Category', 'category')),
    enable: bool(pick(r, 'Enable', 'enable')),
    packageName: str(pick(r, 'PackageName', 'packageName')),
    autoPublish: bool(pick(r, 'AutoPublish', 'autoPublish')),
    multiVersion: bool(pick(r, 'MultiVersion', 'multiVersion')),
    repository: str(pick(r, 'Repository', 'repository')),
    repoUserName: str(pick(r, 'RepoUserName', 'repoUserName')),
    repoPassword: '',
    branch: str(pick(r, 'Branch', 'branch')),
    projectPath: str(pick(r, 'ProjectPath', 'projectPath')),
    buildArgs: str(pick(r, 'BuildArgs', 'buildArgs')),
    packageFilters: str(pick(r, 'PackageFilters', 'packageFilters')),
    remark: str(pick(r, 'Remark', 'remark')),
  };
}

/** 编辑表单 → PUT 体（部分更新：只提交表单覆盖的顶层字段）。repoPassword 非空才进 body */
export function deployPutBody(m: DeployEditModel): Rec {
  const body: Rec = {
    name: m.name,
    category: m.category,
    enable: m.enable,
    packageName: m.packageName,
    autoPublish: m.autoPublish,
    multiVersion: m.multiVersion,
    repository: m.repository,
    repoUserName: m.repoUserName,
    branch: m.branch,
    projectPath: m.projectPath,
    buildArgs: m.buildArgs,
    packageFilters: m.packageFilters,
    remark: m.remark,
  };
  if (m.repoPassword) body.repoPassword = m.repoPassword;
  return body;
}

/** 校验：名称必填 */
export function validateDeployEdit(m: DeployEditModel): string {
  if (!m.name.trim()) return '部署集名称必填';
  return '';
}

/** 读取部署集单条（GET /Deployment/AppDeploy/{id}，Detail 返回 { code, data }）。失败返回 null */
export async function fetchDeployDetail(request: Req, deployId: number): Promise<DeployEditModel | null> {
  if (!deployId || deployId <= 0) return null;
  try {
    const res = await request({ url: `/Deployment/AppDeploy/${deployId}`, method: 'GET' });
    const d = (res as { data?: Rec } | undefined)?.data;
    return d ? toDeployEditModel(d as Rec) : null;
  } catch {
    return null;
  }
}

/** 保存部署集编辑（PUT /Deployment/AppDeploy/{id}，部分更新） */
export async function saveDeploy(request: Req, id: number, m: DeployEditModel): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少部署集编号' };
  const err = validateDeployEdit(m);
  if (err) return { ok: false, message: err };
  try {
    const res = await request({ url: `/Deployment/AppDeploy/${id}`, method: 'PUT', data: deployPutBody(m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '部署集已保存' : r.message || '部署集保存失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '部署集保存失败') };
  }
}

/** 同步应用（POST /Deployment/AppDeploy/SyncApp，无参数，同步全部应用部署集；后端 AppDeployController.SyncApp） */
export async function syncAppDeploy(request: Req): Promise<{ ok: boolean; message: string }> {
  try {
    const res = await request({ url: '/Deployment/AppDeploy/SyncApp', method: 'POST' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? r.message || '同步完成' : r.message || '同步失败' };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '同步失败') };
  }
}

// ================= F07 部署节点 CRUD + 批量远程 =================

/** 节点选择器选项（GET /Nodes/Node?enable=true&PageSize=200，行字段 camelCase 实证同 AppTracer） */
export interface NodeOption {
  id: number;
  name: string;
  ip: string;
}

/** 拉取启用中的物理节点供下拉选择。失败返回 null（页面提示重试） */
export async function fetchNodeOptions(request: Req): Promise<NodeOption[] | null> {
  try {
    const res = await request({ url: '/Nodes/Node', method: 'GET', params: { enable: true, PageSize: 200 } });
    const d = (res as { data?: Rec[] } | undefined)?.data ?? [];
    return (Array.isArray(d) ? d : []).map((r) => ({
      id: num(pick(r, 'Id', 'id')),
      name: str(pick(r, 'Name', 'name')) || `#${num(pick(r, 'Id', 'id'))}`,
      ip: str(pick(r, 'IP', 'ip', 'Ip')),
    }));
  } catch {
    return null;
  }
}

/** 部署节点行编辑表单（实体：Stardust.Data/Deployment/应用节点.cs；DeployId 由容器按当前部署集传入，不在表单里） */
export interface DeployNodeForm {
  nodeId: number;
  enable: boolean;
  port: number;
  fileName: string;
  arguments: string;
  workingDirectory: string;
  userName: string;
  environments: string;
  maxMemory: number;
  delay: number;
  healthCheck: string;
  logDirectory: string;
  logFilePattern: string;
  remark: string;
}

/** 单条部署节点（GET /Deployment/AppDeployNode/{id}）→ 表单初值 */
export function toDeployNodeForm(r: Rec): DeployNodeForm {
  return {
    nodeId: num(pick(r, 'NodeId', 'nodeId')),
    enable: bool(pick(r, 'Enable', 'enable')),
    port: num(pick(r, 'Port', 'port')),
    fileName: str(pick(r, 'FileName', 'fileName')),
    arguments: str(pick(r, 'Arguments', 'arguments')),
    workingDirectory: str(pick(r, 'WorkingDirectory', 'workingDirectory')),
    userName: str(pick(r, 'UserName', 'userName')),
    environments: str(pick(r, 'Environments', 'environments')),
    maxMemory: num(pick(r, 'MaxMemory', 'maxMemory')),
    delay: num(pick(r, 'Delay', 'delay')),
    healthCheck: str(pick(r, 'HealthCheck', 'healthCheck')),
    logDirectory: str(pick(r, 'LogDirectory', 'logDirectory')),
    logFilePattern: str(pick(r, 'LogFilePattern', 'logFilePattern')) || '*.log',
    remark: str(pick(r, 'Remark', 'remark')),
  };
}

/** 新增体：deployId 必带 */
export function deployNodeInsertBody(deployId: number, m: DeployNodeForm): Rec {
  return {
    deployId,
    nodeId: m.nodeId,
    enable: m.enable,
    port: m.port,
    fileName: m.fileName,
    arguments: m.arguments,
    workingDirectory: m.workingDirectory,
    userName: m.userName,
    environments: m.environments,
    maxMemory: m.maxMemory,
    delay: m.delay,
    healthCheck: m.healthCheck,
    logDirectory: m.logDirectory,
    logFilePattern: m.logFilePattern,
    remark: m.remark,
  };
}

/** 编辑体：nodeId 也允许换绑（部分更新只覆盖提交字段） */
export function deployNodeUpdateBody(m: DeployNodeForm): Rec {
  return {
    nodeId: m.nodeId,
    enable: m.enable,
    port: m.port,
    fileName: m.fileName,
    arguments: m.arguments,
    workingDirectory: m.workingDirectory,
    userName: m.userName,
    environments: m.environments,
    maxMemory: m.maxMemory,
    delay: m.delay,
    healthCheck: m.healthCheck,
    logDirectory: m.logDirectory,
    logFilePattern: m.logFilePattern,
    remark: m.remark,
  };
}

/** 表单校验：必须选节点；数值 ≥0 */
export function validateDeployNodeForm(m: DeployNodeForm): string {
  if (!m.nodeId || m.nodeId <= 0) return '请选择物理节点';
  if (m.port < 0) return '端口不能为负数';
  if (m.maxMemory < 0) return '最大内存不能为负数';
  if (m.delay < 0) return '启动延迟不能为负数';
  return '';
}

function deployNodeFail(e: unknown, fallback: string): { ok: boolean; message: string } {
  return { ok: false, message: requestFailText(e, fallback) };
}

/** 读取单条部署节点（编辑表单回填用）。失败返回 null */
export async function fetchDeployNodeDetail(request: Req, id: number): Promise<DeployNodeForm | null> {
  if (!id || id <= 0) return null;
  try {
    const res = await request({ url: `/Deployment/AppDeployNode/${id}`, method: 'GET' });
    const d = (res as { data?: Rec } | undefined)?.data;
    return d ? toDeployNodeForm(d as Rec) : null;
  } catch {
    return null;
  }
}

/** 新增部署节点（POST /Deployment/AppDeployNode，实体体不带 id） */
export async function createDeployNode(request: Req, deployId: number, m: DeployNodeForm): Promise<{ ok: boolean; message: string }> {
  if (!deployId || deployId <= 0) return { ok: false, message: '缺少部署集编号' };
  const err = validateDeployNodeForm(m);
  if (err) return { ok: false, message: err };
  try {
    const res = await request({ url: '/Deployment/AppDeployNode', method: 'POST', data: deployNodeInsertBody(deployId, m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '部署节点已添加' : r.message || '部署节点添加失败' };
  } catch (e) {
    return deployNodeFail(e, '部署节点添加失败');
  }
}

/** 更新部署节点（PUT /Deployment/AppDeployNode/{id}，部分更新） */
export async function updateDeployNode(request: Req, id: number, m: DeployNodeForm): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少部署节点编号' };
  const err = validateDeployNodeForm(m);
  if (err) return { ok: false, message: err };
  try {
    const res = await request({ url: `/Deployment/AppDeployNode/${id}`, method: 'PUT', data: deployNodeUpdateBody(m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '部署节点已保存' : r.message || '部署节点保存失败' };
  } catch (e) {
    return deployNodeFail(e, '部署节点保存失败');
  }
}

/** 删除部署节点（DELETE /Deployment/AppDeployNode/{id}） */
export async function deleteDeployNode(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少部署节点编号' };
  try {
    const res = await request({ url: `/Deployment/AppDeployNode/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '部署节点已删除' : r.message || '部署节点删除失败' };
  } catch (e) {
    return deployNodeFail(e, '部署节点删除失败');
  }
}

/** 批量远程操作（GET /Deployment/AppDeployNode/BatchOperate?act=&keys=1,2,3；
 *  SelectKeys=GetRequest("Keys") 逗号串，后端逐节点 Control 并等全部返回）。keys 为 AppDeployNode.Id */
export async function batchOperateNodes(request: Req, act: string, ids: number[]): Promise<{ ok: boolean; message: string }> {
  if (ids.length === 0) return { ok: false, message: '请先勾选要操作的节点' };
  try {
    const res = await request({ url: '/Deployment/AppDeployNode/BatchOperate', method: 'GET', params: { act, keys: ids.join(',') } });
    const r = okRes(res);
    const label = act;
    return { ok: r.ok, message: r.ok ? r.message || `已批量下发「${label}」（${ids.length} 个节点）` : r.message || `批量「${label}」未成功，请到部署历史核对` };
  } catch (e) {
    return deployNodeFail(e, `批量「${act}」失败`);
  }
}

// ================= F08 编译节点 CRUD =================

/** 编译节点行编辑表单（实体：Stardust.Data/Deployment/编译节点.cs） */
export interface BuildNodeForm {
  nodeId: number;
  enable: boolean;
  sourcePath: string;
  pullCode: boolean;
  buildProject: boolean;
  packageOutput: boolean;
  uploadPackage: boolean;
  outputPath: string;
  remark: string;
}

/** 单条编译节点（GET /Deployment/AppBuildNode/{id}）→ 表单初值 */
export function toBuildNodeForm(r: Rec): BuildNodeForm {
  return {
    nodeId: num(pick(r, 'NodeId', 'nodeId')),
    enable: bool(pick(r, 'Enable', 'enable')),
    sourcePath: str(pick(r, 'SourcePath', 'sourcePath')),
    pullCode: bool(pick(r, 'PullCode', 'pullCode')),
    buildProject: bool(pick(r, 'BuildProject', 'buildProject')),
    packageOutput: bool(pick(r, 'PackageOutput', 'packageOutput')),
    uploadPackage: bool(pick(r, 'UploadPackage', 'uploadPackage')),
    outputPath: str(pick(r, 'OutputPath', 'outputPath')),
    remark: str(pick(r, 'Remark', 'remark')),
  };
}

export function buildNodeInsertBody(deployId: number, m: BuildNodeForm): Rec {
  return {
    deployId,
    nodeId: m.nodeId,
    enable: m.enable,
    sourcePath: m.sourcePath,
    pullCode: m.pullCode,
    buildProject: m.buildProject,
    packageOutput: m.packageOutput,
    uploadPackage: m.uploadPackage,
    outputPath: m.outputPath,
    remark: m.remark,
  };
}

export function buildNodeUpdateBody(m: BuildNodeForm): Rec {
  return {
    nodeId: m.nodeId,
    enable: m.enable,
    sourcePath: m.sourcePath,
    pullCode: m.pullCode,
    buildProject: m.buildProject,
    packageOutput: m.packageOutput,
    uploadPackage: m.uploadPackage,
    outputPath: m.outputPath,
    remark: m.remark,
  };
}

export function validateBuildNodeForm(m: BuildNodeForm): string {
  if (!m.nodeId || m.nodeId <= 0) return '请选择物理节点';
  if (!m.sourcePath.trim()) return '源代码目录必填（编译节点上拉取代码的位置）';
  return '';
}

function buildNodeFail(e: unknown, fallback: string): { ok: boolean; message: string } {
  return { ok: false, message: requestFailText(e, fallback) };
}

/** 读取单条编译节点（编辑表单回填用）。失败返回 null */
export async function fetchBuildNodeDetail(request: Req, id: number): Promise<BuildNodeForm | null> {
  if (!id || id <= 0) return null;
  try {
    const res = await request({ url: `/Deployment/AppBuildNode/${id}`, method: 'GET' });
    const d = (res as { data?: Rec } | undefined)?.data;
    return d ? toBuildNodeForm(d as Rec) : null;
  } catch {
    return null;
  }
}

/** 新增编译节点（POST /Deployment/AppBuildNode） */
export async function createBuildNode(request: Req, deployId: number, m: BuildNodeForm): Promise<{ ok: boolean; message: string }> {
  if (!deployId || deployId <= 0) return { ok: false, message: '缺少部署集编号' };
  const err = validateBuildNodeForm(m);
  if (err) return { ok: false, message: err };
  try {
    const res = await request({ url: '/Deployment/AppBuildNode', method: 'POST', data: buildNodeInsertBody(deployId, m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '编译节点已添加' : r.message || '编译节点添加失败' };
  } catch (e) {
    return buildNodeFail(e, '编译节点添加失败');
  }
}

/** 更新编译节点（PUT /Deployment/AppBuildNode/{id}，部分更新） */
export async function updateBuildNode(request: Req, id: number, m: BuildNodeForm): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少编译节点编号' };
  const err = validateBuildNodeForm(m);
  if (err) return { ok: false, message: err };
  try {
    const res = await request({ url: `/Deployment/AppBuildNode/${id}`, method: 'PUT', data: buildNodeUpdateBody(m) } as ReqOptions & { data: Rec });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '编译节点已保存' : r.message || '编译节点保存失败' };
  } catch (e) {
    return buildNodeFail(e, '编译节点保存失败');
  }
}

/** 删除编译节点（DELETE /Deployment/AppBuildNode/{id}） */
export async function deleteBuildNode(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  if (!id || id <= 0) return { ok: false, message: '缺少编译节点编号' };
  try {
    const res = await request({ url: `/Deployment/AppBuildNode/${id}`, method: 'DELETE' });
    const r = okRes(res);
    return { ok: r.ok, message: r.ok ? '编译节点已删除' : r.message || '编译节点删除失败' };
  } catch (e) {
    return buildNodeFail(e, '编译节点删除失败');
  }
}

// ================= 上传部署版本包（multipart POST AppDeployVersion/Upload） =================

/** 上传结果 */
export interface UploadResult {
  ok: boolean;
  message: string;
  /** 后端生成的版本号（yyyyMMdd-HHmmss），成功时尽量带回 */
  version?: string;
}

/** POST /Deployment/AppDeployVersion/Upload：multipart deployId+file+remark。
 *  后端包名与部署集 PackageName 不匹配会拒绝；部署集开自动发布时上传即发布。
 *  ⚠️ 必须请求级覆盖 Content-Type：api-core client 实例默认头是 application/json，
 *  axios v1 的 transformRequest 遇到「FormData + application/json」会把 FormData 转成 JSON
 *  （{"deployId":"5","file":{}}，后端 [FromForm]+IFormFile 绑定失败 → 404）。与 api-core
 *  自家上传助手（src/api.ts uploadApp/uploadFile）同一写法：显式 multipart/form-data，
 *  axios 检测到 FormData 后剥掉该值，让浏览器自动补 boundary。 */
export async function uploadVersion(request: Req, deployId: number, file: File, remark?: string): Promise<UploadResult> {
  if (!deployId || deployId <= 0) return { ok: false, message: '缺少部署集编号，无法上传' };
  if (!file || file.size <= 0) return { ok: false, message: '请选择要上传的应用包文件' };
  const fd = new FormData();
  fd.append('deployId', String(deployId));
  fd.append('file', file);
  if (remark) fd.append('remark', remark);
  try {
    const res = await request({
      url: '/Deployment/AppDeployVersion/Upload',
      method: 'POST',
      headers: { 'Content-Type': 'multipart/form-data' },
      data: fd,
    } as ReqOptions & { data: FormData });
    const r = okRes(res);
    if (!r.ok) return { ok: false, message: r.message || '上传失败' };
    const d = (r.data ?? {}) as Rec;
    return { ok: true, message: r.message || '上传成功', version: str(pick(d, 'Version', 'version')) || undefined };
  } catch (e) {
    return { ok: false, message: requestFailText(e, '上传失败') };
  }
}

// ================= F09 一键回滚（候选版本挑选，UseVersion 在 ship.logic 里） =================

/** 版本候选（与 ship.logic 的 VersionOption 结构解耦的最小面；enable 不给=可用，disabled 为真=不可用） */
export interface RollbackCandidate {
  id?: number;
  version: string;
  enable?: boolean;
  disabled?: boolean;
}

/** 从版本列表挑出可回滚的候选：可用（enable !== false 且未 disabled）、有版本号、排除当前生效版本；按版本号倒序。
 *  versions 已是列表序（新在前），稳定排序保留原相对次序 */
export function rollbackCandidates(versions: RollbackCandidate[], currentVersion: string): RollbackCandidate[] {
  const cur = currentVersion.trim();
  const out: RollbackCandidate[] = [];
  const seen = new Set<string>();
  for (const v of versions) {
    const ver = v.version.trim();
    if (!ver) continue;
    if (v.enable === false || v.disabled === true) continue;
    if (ver === cur) continue;
    if (seen.has(ver)) continue;
    seen.add(ver);
    out.push(v);
  }
  return out.sort((a, b) => b.version.localeCompare(a.version));
}

/** 回滚确认文案（Guard 用）：说明回滚只切版本号，是否下发看自动发布 */
export function rollbackNotice(target: { version: string }, autoPublish: boolean, enabledNodes: number): string {
  const base = `回滚到 ${target.version}：把部署集生效版本切回上一个成功版本。`;
  if (!autoPublish) return `${base}部署集未开启「自动发布」，只切换版本号，不会下发到节点。`;
  if (enabledNodes <= 0) return `${base}已开启自动发布，但没有启用节点，不会实际下发。`;
  return `${base}已开启自动发布：将向 ${enabledNodes} 个启用节点下发该版本的安装命令。`;
}
