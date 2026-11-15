// 服务端导出下载纯逻辑单测（Vitest）：cubeAxios 用 vi.mock 模块桩（不发真实请求，blob/headers 全桩）。
// 覆盖：url/params/responseType 透传、Content-Disposition 文件名解析（filename*=UTF-8'' 编码 / filename="x" /
// 缺失兜底）、blob 下载触发（objectURL→隐形 a→revoke）、失败与非文件响应降级。
import { beforeEach, describe, expect, it, vi } from 'vitest';

const axiosGet = vi.fn();
vi.mock('@newlifex/cube-vue/core/utils/request', () => ({
  cubeAxios: { get: (...args: unknown[]) => axiosGet(...args) },
}));

// jsdom 无 URL.createObjectURL 静态方法，统一桩
const createObjectURL = vi.fn(() => 'blob:mock');
const revokeObjectURL = vi.fn();
vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });

import { downloadEntityExport, parseExportFileName } from './exportDownload.logic';

describe('parseExportFileName · Content-Disposition 文件名解析', () => {
  it("filename*=UTF-8'' 编码段优先（URL 解码）", () => {
    const cd = "attachment; filename*=UTF-8''%E5%AE%A1%E8%AE%A1%E8%AE%B0%E5%BD%95_20261109.xlsx; filename=audit.xlsx";
    expect(parseExportFileName(cd)).toBe('审计记录_20261109.xlsx');
  });
  it('普通 filename="x" 次之；缺失返回空串', () => {
    expect(parseExportFileName('attachment; filename="audit_20261109.json"')).toBe('audit_20261109.json');
    expect(parseExportFileName(undefined)).toBe('');
    expect(parseExportFileName('')).toBe('');
  });
});

describe('downloadEntityExport · 通用实体导出下载', () => {
  const clicks: { href: string; download: string }[] = [];
  const origCreate = document.createElement.bind(document);

  beforeEach(() => {
    axiosGet.mockReset();
    createObjectURL.mockClear();
    revokeObjectURL.mockClear();
    clicks.length = 0;
    // 真实 <a> 元素（可 appendChild），仅 mock click 记录 href/download——appendChild 需真 Node
    const anchor = origCreate('a') as HTMLAnchorElement;
    vi.spyOn(anchor, 'click').mockImplementation(() => {
      clicks.push({ href: anchor.href, download: anchor.download });
    });
    vi.spyOn(document, 'createElement').mockImplementation(((tag: string) => {
      if (tag === 'a') return anchor;
      return origCreate(tag);
    }) as typeof document.createElement);
  });

  it('excel：GET export 端点（responseType=blob）带 format 与查询条件；文件名取 Content-Disposition', async () => {
    axiosGet.mockResolvedValue({
      data: new Blob(['excel-bytes']),
      headers: { 'content-disposition': "attachment; filename*=UTF-8''%E5%AE%A1%E8%AE%A1%E8%AE%B0%E5%BD%95_20261109.xlsx" },
    });
    const r = await downloadEntityExport('/Platform/McpAudit/export', { tokenId: 3, pageIndex: 0, pageSize: 50 }, 'excel', 'mcp-audit');
    expect(r.ok).toBe(true);
    expect(r.fileName).toBe('审计记录_20261109.xlsx');
    expect(axiosGet).toHaveBeenCalledWith('/Platform/McpAudit/export', {
      params: { tokenId: 3, pageIndex: 0, pageSize: 50, format: 'excel' },
      responseType: 'blob',
    });
    expect(clicks).toHaveLength(1);
    expect(clicks[0].download).toBe('审计记录_20261109.xlsx');
    expect(clicks[0].href).toBe('blob:mock');
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock');
  });

  it('json：无 Content-Disposition 时用兜底名 + .json 扩展名', async () => {
    axiosGet.mockResolvedValue({ data: new Blob(['[]']), headers: {} });
    const r = await downloadEntityExport('/Platform/McpServer/export', { enable: true, pageIndex: 0, pageSize: 50 }, 'json', 'mcp-server');
    expect(r.ok).toBe(true);
    expect(r.fileName).toBe('mcp-server.json');
    expect(axiosGet.mock.calls[0][1]).toEqual({ params: { enable: true, pageIndex: 0, pageSize: 50, format: 'json' }, responseType: 'blob' });
  });

  it('请求失败降级 ok:false（不发下载）', async () => {
    axiosGet.mockRejectedValue(new Error('net down'));
    const r = await downloadEntityExport('/Platform/McpAudit/export', {}, 'excel', 'mcp-audit');
    expect(r.ok).toBe(false);
    expect(r.message).toBe('net down');
    expect(clicks).toHaveLength(0);
  });

  it('响应非 Blob 拒绝下载', async () => {
    axiosGet.mockResolvedValue({ data: { code: -1 }, headers: {} });
    const r = await downloadEntityExport('/Platform/McpAudit/export', {}, 'excel', 'mcp-audit');
    expect(r.ok).toBe(false);
    expect(clicks).toHaveLength(0);
  });
});
