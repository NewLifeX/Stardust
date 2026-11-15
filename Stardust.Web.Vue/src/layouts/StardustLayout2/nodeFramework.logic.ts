// 节点框架批量管理纯逻辑（CARD-NODE-08，2026-11-10；对齐 NODE-11 判例独立成文件）。
//
// 契约真源 Stardust.WebApi/Areas/Nodes/Controllers/NodeFrameworkController.cs：
//  - InstallFramework(ver, kind)  GET /Nodes/NodeFramework/InstallFramework?keys=…&ver=…&kind=…
//      keys=Node 主键逗号串（NodesEntityController<SelectKeys> :64/:70，与 NODE-11 在线会话 Id 不同）；
//      ver 留空或 auto=自动匹配最新启用安装包（:80 注释 + ResolveForPush :334 唯一解析真源）→
//      前端「自动（最新）」首项=ver 传空（定稿裁定：显式传值引入 GetVersionList 排序耦合，否决）；
//      kind 留空或 auto 等效（Biz :337 都按节点自动判断）→ 类型下拉首项用 GetKindList 给的 auto 项。
//  - UninstallFramework(ver)      GET /Nodes/NodeFramework/UninstallFramework?keys=…&ver=…（:134 无 kind；ver 必填）
//  - GetVersionList / GetKindList GET → 信封 {data:{key:value,…}}（IDictionary<String,String>，
//      Biz :138 FindAllName=版本→版本；Biz :271 GetKindList 首项 auto=「自动」）。
//  - 汇总消息契约（:111/:160）：「操作成功！下发N个，响应S个，超时T个」+ 逐节点「name: reply」；
//      全跳过 → 「没有可推送的节点！\n{skips}」；无匹配包 → 「没有找到匹配的安装包，请检查版本和安装类型」。
//      判据与 NODE-11 同（nodeOnline.logic.ts:268 code 0/200/success），汇总条复用 parseBatchSummary。
// 权限位 16：前端无权限数据源 → 不做前端权限禁用，不足由后端拒绝经失败条呈现（NODE-11 同判例）。
// 高危红线：安装/卸载为远程指令，仅本地栈节点实走；前端先弹本地 GuardModal 再发请求。

import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel';

type Rec = Record<string, unknown>;

function str(v: unknown): string {
  if (v === undefined || v === null) return '';
  return String(v).trim();
}

function errText(e: unknown, fallback: string): string {
  return e instanceof Error && e.message ? e.message : fallback;
}

function resMessage(res: Rec | undefined): { message: string; error: string } {
  const ok = !!res && (res.code === 0 || res.code === 200 || res.success === true);
  if (ok) return { message: str(res?.message), error: '' };
  return { message: '', error: str(res?.message) || '操作失败' };
}

/** 框架安装请求参数（ver=「自动（最新）」传空；kind 可为 auto/空/具体类型） */
export interface FrameworkInstallParams {
  keys: string;
  ver: string;
  kind: string;
}

/** 节点 Id 集合 → keys 逗号串（Node 主键，无映射；去重去非法值） */
export function buildFrameworkKeys(ids: number[]): string {
  return [...new Set(ids.filter((n) => Number.isFinite(n) && n > 0))].map(String).join(',');
}

/**
 * 安装参数（C09b）：ver「自动（最新）」=传空（ResolveForPush 自动匹配），显式版本 trim 传值；
 * kind trim 原样传（空/auto 后端等效，Biz :337）；无有效节点 → null（不发请求）。
 */
export function buildFrameworkInstallParams(
  ids: number[],
  ver: string,
  kind: string,
): FrameworkInstallParams | null {
  const keys = buildFrameworkKeys(ids);
  if (!keys) return null;
  return { keys, ver: str(ver), kind: str(kind) };
}

/** 卸载参数（C09b）：ver 必填（:137 空/null 抛 ArgumentNullException），无 kind；缺任一 → null */
export function buildFrameworkUninstallParams(
  ids: number[],
  ver: string,
): { keys: string; ver: string } | null {
  const keys = buildFrameworkKeys(ids);
  const v = str(ver);
  if (!keys || !v) return null;
  return { keys, ver: v };
}

/** 版本/类型字典 → 下拉选项（IDictionary 序列化为对象；丢空键，label 兜底为键） */
export function toFrameworkOptions(data: unknown): { value: string; label: string }[] {
  if (!data || typeof data !== 'object') return [];
  return Object.entries(data as Rec)
    .map(([value, label]) => ({ value, label: label == null ? value : String(label) }))
    .filter((o) => o.value !== '');
}

/** 版本列表 GET（C08b）：信封 {data:{…}}；失败静默空列表（可重开下拉重试） */
export async function fetchFrameworkVersions(request: Req): Promise<{ value: string; label: string }[]> {
  try {
    const res = await request({ url: '/Nodes/NodeFramework/GetVersionList', method: 'GET' });
    return toFrameworkOptions((res as Rec | undefined)?.data);
  } catch {
    return [];
  }
}

/** 类型列表 GET（C08b）：服务端首项 auto=「自动」（Biz :271）；失败静默空列表 */
export async function fetchFrameworkKinds(request: Req): Promise<{ value: string; label: string }[]> {
  try {
    const res = await request({ url: '/Nodes/NodeFramework/GetKindList', method: 'GET' });
    return toFrameworkOptions((res as Rec | undefined)?.data);
  } catch {
    return [];
  }
}

/** 批量安装（C06）：keys=节点 Id；ver「自动（最新）」传空；返回后端汇总消息或错误（§11 失败可见） */
export async function runFrameworkInstall(
  request: Req,
  ids: number[],
  ver: string,
  kind: string,
): Promise<{ message: string; error: string }> {
  const p = buildFrameworkInstallParams(ids, ver, kind);
  if (!p) return { message: '', error: '请先勾选节点' };
  try {
    const res = await request({ url: '/Nodes/NodeFramework/InstallFramework', method: 'GET', params: { ...p } });
    return resMessage(res as Rec | undefined);
  } catch (e) {
    return { message: '', error: errText(e, '框架安装失败') };
  }
}

/** 批量卸载（C09）：keys=节点 Id；ver 必填（无 kind）；未选版本 → 「请选择框架版本」（C07 逐字） */
export async function runFrameworkUninstall(
  request: Req,
  ids: number[],
  ver: string,
): Promise<{ message: string; error: string }> {
  const p = buildFrameworkUninstallParams(ids, ver);
  if (!p) return { message: '', error: '请选择框架版本' };
  try {
    const res = await request({ url: '/Nodes/NodeFramework/UninstallFramework', method: 'GET', params: { ...p } });
    return resMessage(res as Rec | undefined);
  } catch (e) {
    return { message: '', error: errText(e, '框架卸载失败') };
  }
}
