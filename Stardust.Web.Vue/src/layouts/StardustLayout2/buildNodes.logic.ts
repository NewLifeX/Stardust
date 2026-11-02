// 编译节点纯逻辑（无 Vue 依赖，request 由调用方注入，便于单测）。
// 接口：
//  - 列表：GET /Deployment/AppBuildNode?deployId=&PageSize=200
//  - 动作：GET /Deployment/AppBuildNode/Operate?id=&act=Build-Upload|Package-Upload
// 响应字段大小写两种都认（后端 JSON 与旧 MVC 方言）。取数失败返回 null，由页面显示「加载失败」，不造数据。
import { okRes } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

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

// ---------- 编译节点行 ----------

export interface BuildNodeRow {
  id: number;
  deployId: number;
  nodeId: number;
  nodeName: string;
  nodeIp: string;
  enable: boolean;
  sourcePath: string;
  pullCode: boolean;
  buildProject: boolean;
  packageOutput: boolean;
  uploadPackage: boolean;
  outputPath: string;
  updateTime: string;
  remark: string;
}

/** 动作枚举：Build-Upload = 拉代码+编译+打包+上传；Package-Upload = 不拉代码不编译，直接打包上传 */
export type BuildNodeAct = 'Build-Upload' | 'Package-Upload';

export const BUILD_NODE_ACTIONS: { act: BuildNodeAct; label: string; description: string }[] = [
  { act: 'Build-Upload', label: '编译上传', description: '在节点上拉取源代码、编译项目、打包并上传应用包' },
  { act: 'Package-Upload', label: '打包上传', description: '不拉取源代码、不编译，直接打包输出目录并上传应用包' },
];

export function actionInfo(act: string): { act: BuildNodeAct; label: string; description: string } | undefined {
  return BUILD_NODE_ACTIONS.find((a) => a.act === act);
}

export function toBuildNodeRows(rows: Rec[]): BuildNodeRow[] {
  return rows.map((r) => {
    const nodeId = num(pick(r, 'NodeId', 'nodeId'));
    const rawName = str(pick(r, 'NodeName', 'nodeName', 'Name', 'name')).trim();
    const rawIp = str(pick(r, 'NodeIP', 'nodeIP', 'NodeIp', 'nodeIp', 'IP', 'ip')).trim();
    return {
      id: num(pick(r, 'Id', 'id')),
      deployId: num(pick(r, 'DeployId', 'deployId')),
      nodeId,
      nodeName: rawName || `#${nodeId}`,
      nodeIp: rawIp,
      enable: bool(pick(r, 'Enable', 'enable')),
      sourcePath: str(pick(r, 'SourcePath', 'sourcePath')),
      pullCode: bool(pick(r, 'PullCode', 'pullCode')),
      buildProject: bool(pick(r, 'BuildProject', 'buildProject')),
      packageOutput: bool(pick(r, 'PackageOutput', 'packageOutput')),
      uploadPackage: bool(pick(r, 'UploadPackage', 'uploadPackage')),
      outputPath: str(pick(r, 'OutputPath', 'outputPath')),
      updateTime: str(pick(r, 'UpdateTime', 'updateTime')),
      remark: str(pick(r, 'Remark', 'remark')),
    };
  });
}

export async function fetchBuildNodes(request: Req, deployId: number): Promise<BuildNodeRow[] | null> {
  if (!deployId || deployId <= 0) return [];
  try {
    const res = await request({ url: '/Deployment/AppBuildNode', method: 'GET', params: { deployId, PageSize: 200 } });
    return toBuildNodeRows(listOf(res));
  } catch {
    return null;
  }
}

// ---------- 动作 ----------

async function runAct(request: Req, act: BuildNodeAct, id: number): Promise<{ ok: boolean; message: string }> {
  const info = actionInfo(act);
  if (!info) return { ok: false, message: `不支持的操作：${act}` };
  if (!id || id <= 0) return { ok: false, message: `缺少编译节点编号，无法执行「${info.label}」` };
  try {
    const res = await request({ url: '/Deployment/AppBuildNode/Operate', method: 'GET', params: { id, act } });
    const r = okRes(res);
    if (r.ok) return { ok: true, message: r.message || `已在编译节点上执行「${info.label}」` };
    return { ok: false, message: r.message || `「${info.label}」未成功，请稍后到部署历史核对` };
  } catch (e) {
    return { ok: false, message: `「${info.label}」失败：${e instanceof Error && e.message ? e.message : '请求失败'}` };
  }
}

export function runBuildUpload(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  return runAct(request, 'Build-Upload', id);
}

export function runPackageUpload(request: Req, id: number): Promise<{ ok: boolean; message: string }> {
  return runAct(request, 'Package-Upload', id);
}

/** 把后端 UpdateTime（可能是 ISO 字符串或 Date）格式化成 YYYY-MM-DD HH:mm */
export function fmtBuildTime(s: string | undefined): string {
  if (!s) return '—';
  return s.replace('T', ' ').slice(0, 16);
}
