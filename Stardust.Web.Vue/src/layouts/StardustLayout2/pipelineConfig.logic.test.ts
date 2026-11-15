// 流水线配置纯逻辑单测（CARD-DEP-01）：级联数据流、逗号串同步、必填校验、Token 不提交、剪贴板降级。
// 数据手算；request 全部注入桩。
import { describe, it, expect, vi } from 'vitest';
import {
  toPipelineRows,
  toBuildNodeSearchItems,
  toPipelineForm,
  emptyPipelineForm,
  validatePipelineForm,
  joinDeployNodeIds,
  pipelineInsertBody,
  pipelineUpdateBody,
  fetchPipelineList,
  fetchDeployNodeOptions,
  fetchPipelineProjectId,
  searchBuildNodes,
  copyWebhookUrl,
  triggerPipeline,
  parseTriggerRunNo,
  createPipeline,
  updatePipeline,
  deletePipeline,
  copyTextToClipboard,
  fmtPipelineTime,
} from './pipelineConfig.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const ROW = {
  Id: 7,
  Name: 'build-main',
  DeployId: 10,
  Branch: 'release/*',
  BuildNodeId: 3,
  ProjectId: 88,
  DeployNodeIds: '1,2',
  AutoDeploy: true,
  Enable: true,
  UpdateTime: '2026-11-07T10:20:30',
};

function reqWith(res: unknown): { req: Req; calls: { url: string; init: Record<string, unknown> }[] } {
  const calls: { url: string; init: Record<string, unknown> }[] = [];
  const req: Req = async (init) => {
    calls.push({ url: String((init as { url: string }).url), init: init as unknown as Record<string, unknown> });
    return res as never;
  };
  return { req, calls };
}

describe('pipelineConfig · 行解析', () => {
  it('toPipelineRows：字段映射（大小写两种都认）', () => {
    const rows = toPipelineRows([ROW, { id: 8, name: 'b2', deployId: 10, branch: 'main', autoDeploy: 0, enable: false }]);
    expect(rows[0]).toEqual({
      id: 7, name: 'build-main', deployId: 10, branch: 'release/*', buildNodeId: 3, projectId: 88,
      deployNodeIds: '1,2', autoDeploy: true, enable: true, updateTime: '2026-11-07T10:20:30',
    });
    expect(rows[1].autoDeploy).toBe(false);
    expect(rows[1].enable).toBe(false);
  });

  it('fetchPipelineList：GET /Deployment/AppPipeline?deployId=&PageSize=50；失败返回 null', async () => {
    const ok = reqWith({ code: 0, data: [ROW] });
    const list = await fetchPipelineList(ok.req, 10);
    expect(list?.[0].id).toBe(7);
    expect(ok.calls[0].url).toBe('/Deployment/AppPipeline');
    expect((ok.calls[0].init.params as Record<string, unknown>).deployId).toBe(10);

    const bad = reqWith(null);
    bad.req = (async () => {
      throw new Error('boom');
    }) as Req;
    expect(await fetchPipelineList(bad.req, 10)).toBeNull();
  });

  it('fmtPipelineTime：T 转空格截到分；空为 —', () => {
    expect(fmtPipelineTime('2026-11-07T10:20:30')).toBe('2026-11-07 10:20');
    expect(fmtPipelineTime('')).toBe('—');
    expect(fmtPipelineTime(undefined)).toBe('—');
  });
});

describe('pipelineConfig · 级联', () => {
  it('fetchDeployNodeOptions：GET GetDeployNodes，取 Id/name；deployId 非法不发请求', async () => {
    const { req, calls } = reqWith({ code: 0, data: [{ Id: 1, name: 'node-a (10.0.0.1)' }, { Id: 2, name: 'node-b (10.0.0.2)' }] });
    const list = await fetchDeployNodeOptions(req, 10);
    expect(list).toEqual([
      { id: 1, label: 'node-a (10.0.0.1)' },
      { id: 2, label: 'node-b (10.0.0.2)' },
    ]);
    expect(calls[0].url).toBe('/Deployment/AppPipeline/GetDeployNodes');
    expect(await fetchDeployNodeOptions(req, 0)).toEqual([]);
    expect(calls).toHaveLength(1);
  });

  it('fetchPipelineProjectId：GET GetDeployProject，取 data.ProjectId；失败/缺参返回 null', async () => {
    const { req, calls } = reqWith({ code: 0, data: { ProjectId: 88 } });
    expect(await fetchPipelineProjectId(req, 10)).toBe(88);
    expect(calls[0].url).toBe('/Deployment/AppPipeline/GetDeployProject');
    expect(await fetchPipelineProjectId(req, 0)).toBeNull();
    const bad = (async () => {
      throw new Error('x');
    }) as Req;
    expect(await fetchPipelineProjectId(bad, 10)).toBeNull();
  });

  it('searchBuildNodes：GET /Nodes/Node/NodeSearch 带 projectId/product=StarDeploy/key', async () => {
    const { req, calls } = reqWith({
      code: 0,
      data: [{ id: 3, name: 'build-a', ip: '10.0.0.11', projectName: 'StarDeploy' }],
    });
    const list = await searchBuildNodes(req, 88, 'a');
    expect(list).toEqual([{ id: 3, name: 'build-a', ip: '10.0.0.11', projectName: 'StarDeploy' }]);
    expect(calls[0].url).toBe('/Nodes/Node/NodeSearch');
    const p = calls[0].init.params as Record<string, unknown>;
    expect(p.projectId).toBe(88);
    expect(p.product).toBe('StarDeploy');
    expect(p.key).toBe('a');
  });

  it('toBuildNodeSearchItems：缺名称兜底「节点 {id}」', () => {
    const items = toBuildNodeSearchItems([{ Id: 9, ip: '10.0.0.9' }]);
    expect(items[0]).toEqual({ id: 9, name: '节点 9', ip: '10.0.0.9', projectName: '' });
  });
});

describe('pipelineConfig · 表单与提交体', () => {
  it('toPipelineForm：逗号串拆回数字数组，容错空段', () => {
    const f = toPipelineForm(toPipelineRows([ROW])[0]);
    expect(f).toEqual({ name: 'build-main', branch: 'release/*', buildNodeId: 3, deployNodeIds: [1, 2], autoDeploy: true, enable: true });
    const g = toPipelineForm(toPipelineRows([{ Id: 1, DeployNodeIds: ' 4, x, ,5 ' }])[0]);
    expect(g.deployNodeIds).toEqual([4, 5]);
  });

  it('joinDeployNodeIds：数组→逗号串（提交前同步）', () => {
    expect(joinDeployNodeIds([1, 2, 3])).toBe('1,2,3');
    expect(joinDeployNodeIds([])).toBe('');
  });

  it('validatePipelineForm：名称为空→「名称不能为空」（逐字）', () => {
    expect(validatePipelineForm({ ...emptyPipelineForm(), name: '  ' })).toBe('名称不能为空');
    expect(validatePipelineForm({ ...emptyPipelineForm(), name: 'p1' })).toBe('');
  });

  it('pipelineInsertBody：带 deployId、逗号串同步；Token/Secret/TraceId 永不出现（敏感字段）', () => {
    const f: Parameters<typeof pipelineInsertBody>[1] = {
      name: ' p1 ', branch: ' main ', buildNodeId: 3, deployNodeIds: [1, 2], autoDeploy: false, enable: true,
    };
    const body = pipelineInsertBody(10, f);
    expect(body).toEqual({
      deployId: 10, name: 'p1', branch: 'main', buildNodeId: 3, deployNodeIds: '1,2', autoDeploy: false, enable: true,
    });
    const keys = Object.keys(body).map((k) => k.toLowerCase());
    expect(keys).not.toContain('token');
    expect(keys).not.toContain('secret');
    expect(keys).not.toContain('traceid');
  });

  it('pipelineUpdateBody：同名段、无 deployId（主键在路径）', () => {
    const body = pipelineUpdateBody({ name: 'p1', branch: '', buildNodeId: 0, deployNodeIds: [], autoDeploy: true, enable: false });
    expect(body).toEqual({ name: 'p1', branch: '', buildNodeId: 0, deployNodeIds: '', autoDeploy: true, enable: false });
  });
});

describe('pipelineConfig · CRUD 与行操作', () => {
  it('createPipeline：POST /Deployment/AppPipeline；校验失败不发请求', async () => {
    const { req, calls } = reqWith({ code: 0, message: 'ok' });
    const ok = await createPipeline(req, 10, { ...emptyPipelineForm(), name: 'p1' });
    expect(ok.ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Deployment/AppPipeline' });
    expect((calls[0].init as { method: string }).method).toBe('POST');

    const bad = reqWith({ code: 0 });
    const err = await createPipeline(bad.req, 10, emptyPipelineForm());
    expect(err).toEqual({ ok: false, message: '名称不能为空' });
    expect(bad.calls).toHaveLength(0);
  });

  it('updatePipeline：PUT /Deployment/AppPipeline/{id}', async () => {
    const { req, calls } = reqWith({ code: 0, message: 'ok' });
    const r = await updatePipeline(req, 7, { ...emptyPipelineForm(), name: 'p' });
    expect(r.ok).toBe(true);
    expect(calls[0].url).toBe('/Deployment/AppPipeline/7');
    expect((calls[0].init as { method: string }).method).toBe('PUT');
  });

  it('deletePipeline：DELETE /Deployment/AppPipeline/{id}', async () => {
    const { req, calls } = reqWith({ code: 0, message: 'ok' });
    const r = await deletePipeline(req, 7);
    expect(r.ok).toBe(true);
    expect(calls[0].url).toBe('/Deployment/AppPipeline/7');
    expect((calls[0].init as { method: string }).method).toBe('DELETE');
  });

  it('copyWebhookUrl：GET CopyWebhook?id=，返回 data 为 URL；失败带 message', async () => {
    const { req, calls } = reqWith({ code: 0, message: 'ok', data: 'http://s/Deployment/Pipeline/Webhook?token=t1' });
    const r = await copyWebhookUrl(req, 7);
    expect(r).toEqual({ ok: true, message: 'ok', url: 'http://s/Deployment/Pipeline/Webhook?token=t1' });
    expect((calls[0].init.params as Record<string, unknown>).id).toBe(7);

    const bad = reqWith({ code: 500, message: 'nope' });
    expect(await copyWebhookUrl(bad.req, 7)).toEqual({ ok: false, message: 'nope', url: '' });
  });

  it('triggerPipeline：GET Trigger?id=，成功 message 透传（含运行编号）', async () => {
    const { req, calls } = reqWith({ code: 0, message: '流水线已触发，运行编号[42]' });
    const r = await triggerPipeline(req, 5);
    expect(r).toEqual({ ok: true, message: '流水线已触发，运行编号[42]' });
    expect((calls[0].init.params as Record<string, unknown>).id).toBe(5);
  });
});

// task-88（2026-11-12）：触发/重跑成功 message「运行编号[N]」→ N = AppPipelineRun.Id，用于「查看运行」直达
describe('pipelineConfig · parseTriggerRunNo', () => {
  it('解析手动触发与重跑两种后端文案', () => {
    expect(parseTriggerRunNo('流水线已触发，运行编号[42]')).toBe(42);
    expect(parseTriggerRunNo('流水线已重新触发，运行编号[7]')).toBe(7);
  });

  it('无编号、格式异常、空串返回 0', () => {
    expect(parseTriggerRunNo('触发成功')).toBe(0);
    expect(parseTriggerRunNo('流水线已触发，运行编号[abc]')).toBe(0);
    expect(parseTriggerRunNo('流水线已触发，运行编号[]')).toBe(0);
    expect(parseTriggerRunNo('')).toBe(0);
    expect(parseTriggerRunNo(undefined as unknown as string)).toBe(0);
  });
});

describe('pipelineConfig · 剪贴板', () => {
  it('优先 navigator.clipboard.writeText', async () => {
    const writeText = vi.fn(async () => undefined);
    const doc = { createElement: vi.fn(), body: { appendChild: vi.fn(), removeChild: vi.fn() } } as unknown as Document;
    const ok = await copyTextToClipboard('u', { clipboard: { writeText } }, doc);
    expect(ok).toBe(true);
    expect(writeText).toHaveBeenCalledWith('u');
    expect(doc.createElement).not.toHaveBeenCalled();
  });

  it('clipboard 不可用降级 textarea+execCommand；仍失败返回 false', async () => {
    const ta = { value: '', style: {}, select: vi.fn() };
    const doc = {
      createElement: vi.fn(() => ta),
      body: { appendChild: vi.fn(), removeChild: vi.fn() },
      execCommand: vi.fn(() => true),
    } as unknown as Document;
    const ok = await copyTextToClipboard('u', {}, doc);
    expect(ok).toBe(true);
    expect(doc.execCommand).toHaveBeenCalledWith('copy');
    expect(doc.body.removeChild).toHaveBeenCalled();

    const failDoc = {
      createElement: vi.fn(() => ta),
      body: { appendChild: vi.fn(), removeChild: vi.fn() },
      execCommand: vi.fn(() => false),
    } as unknown as Document;
    expect(await copyTextToClipboard('u', {}, failDoc)).toBe(false);
    expect(await copyTextToClipboard('', {}, failDoc)).toBe(false);
  });
});
