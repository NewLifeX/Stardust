// 服务端导出下载（CARD-PLT-02/03 共用逻辑）：Cube base-class 统一导出端点
// GET /api/[area]/[controller]/export?format=excel|json（ReadOnlyEntityController.Export，
// EntityAuthorize(PermissionFlags.Detail)，FileDownloadName → Content-Disposition）。
//
// 实现要点：
// 1. 走 cubeAxios（@newlifex/cube-vue 原始 axios 实例）——baseURL 与 Token 头注入由拦截器统一处理，
//    与既有 stardustContext.ts 消费方式一致；禁用裸 <a href> 直链（不带 Token 会 401）。
// 2. responseType: 'blob' 接文件流；文件名优先解析 Content-Disposition
//    （filename*=UTF-8'' 编码段 > filename="x" > 兜底名+扩展名，Excel=.xlsx / JSON=.json）。
// 3. 下载触发：objectURL → 隐形 <a download> 点击 → revoke；失败降级为 { ok:false, message }，
//    不向用户抛栈（导出按钮据此 toast）。
import { cubeAxios } from '@newlifex/cube-vue/core/utils/request';

export type ExportFormat = 'excel' | 'json';

export interface ExportResult {
  ok: boolean;
  message: string;
  /** 实际落盘文件名（服务端 Content-Disposition 优先；取不到时为兜底名） */
  fileName: string;
}

/** 从 Content-Disposition 解析 filename：filename*=UTF-8''编码段优先（URL 解码），其次 filename="x"，缺失返回空串 */
export function parseExportFileName(cd: unknown): string {
  const s = String(cd ?? '');
  const star = s.match(/filename\*=(?:UTF-8|utf-8)''([^;]+)/);
  if (star) {
    try {
      return decodeURIComponent(star[1].trim());
    } catch {
      // 编码异常时退回普通 filename 段
    }
  }
  const plain = s.match(/filename="?([^";]+)"?/);
  return plain ? plain[1].trim() : '';
}

/** blob → 隐形 <a download> 点击 → revoke（浏览器侧下载触发，无业务逻辑） */
function saveBlob(blob: Blob, fileName: string): void {
  const objUrl = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = objUrl;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(objUrl);
}

/**
 * 通用实体导出下载：GET <url>?format=<format>&<查询条件> → blob 落盘。
 * @param url 形如 '/Platform/McpAudit/export'（无 /api 前缀，baseURL 由 cubeAxios 统一补）
 * @param params 当前 Search 条件（与列表查询同参，后端 Export 复用 Search 的 Pager 绑定）
 * @param format 导出格式：excel（.xlsx）或 json
 * @param fallbackBase 无 Content-Disposition 时的兜底文件名（不含扩展名）
 */
export async function downloadEntityExport(
  url: string,
  params: Record<string, unknown>,
  format: ExportFormat,
  fallbackBase: string,
): Promise<ExportResult> {
  try {
    const res = await cubeAxios.get(url, { params: { ...params, format }, responseType: 'blob' });
    const blob = res?.data;
    if (!(blob instanceof Blob)) {
      return { ok: false, message: '导出失败：响应不是文件', fileName: '' };
    }
    const ext = format === 'json' ? 'json' : 'xlsx';
    const fileName = parseExportFileName(res?.headers?.['content-disposition']) || `${fallbackBase}.${ext}`;
    saveBlob(blob, fileName);
    return { ok: true, message: '导出成功', fileName };
  } catch (e) {
    const msg = e instanceof Error && e.message ? e.message : '导出失败';
    return { ok: false, message: msg, fileName: '' };
  }
}
