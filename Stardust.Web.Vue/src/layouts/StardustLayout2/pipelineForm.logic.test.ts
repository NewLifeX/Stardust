import { describe, expect, it, vi } from 'vitest';
import {
  canReprocess,
  compileNodeChoices,
  createPipeline,
  deployTargetChoices,
  fetchPipelineCards,
  fetchPipelineDetail,
  fetchWebhookUrl,
  formatIdList,
  parseIdList,
  pipelineWriteBody,
  savePipeline,
  toPipelineCards,
  toPipelineForm,
  validatePipelineForm,
} from './pipelineForm.logic.ts';
import type { PipelineForm } from './pipelineForm.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const form = (patch: Partial<PipelineForm> = {}): PipelineForm => ({
  name: 'build-main',
  branch: 'main',
  buildNodeId: 2,
  deployNodeIds: [73],
  autoDeploy: true,
  enable: true,
  remark: '',
  ...patch,
});

describe('parseIdList / formatIdList', () => {
  it('去掉空段和重复', () => {
    expect(parseIdList(',73,73,')).toEqual([73]);
    expect(parseIdList('73, 74，73')).toEqual([73, 74]);
    expect(formatIdList([73, 73, 0, 74])).toBe('73,74');
  });
});

describe('toPipelineCards', () => {
  it('列表没有 deployNodeIds 时记 null，并补上查询用的 deployId', () => {
    const cards = toPipelineCards([{ id: 5, name: '测试', branch: 'main', buildNodeId: 2, autoDeploy: true, enable: true }], 25);
    expect(cards[0]).toMatchObject({ id: 5, deployId: 25, deployNodeIds: null, buildNodeId: 2, enable: true });
  });

  it('详情里的重复部署节点去重，不读取 token', () => {
    const f = toPipelineForm({ id: 5, name: '测试', branch: 'main', buildNodeId: 2, deployNodeIds: ',73,73,', token: 'secret-token', autoDeploy: true, enable: true, remark: null });
    expect(f.deployNodeIds).toEqual([73]);
    expect(f.remark).toBe('');
    expect(f).not.toHaveProperty('token');
  });
});

describe('validatePipelineForm / pipelineWriteBody', () => {
  it('名称、编译节点、部署节点缺一不可', () => {
    expect(validatePipelineForm(form({ name: '  ' }))).toContain('名称');
    expect(validatePipelineForm(form({ buildNodeId: 0 }))).toContain('编译节点');
    expect(validatePipelineForm(form({ deployNodeIds: [] }))).toContain('部署节点');
    expect(validatePipelineForm(form())).toBe('');
  });

  it('写入体不含 token，部署节点去重成逗号串', () => {
    const body = pipelineWriteBody(form({ deployNodeIds: [73, 73], remark: ' 备注 ' }), { deployId: 25, projectId: 2 });
    expect(body).toMatchObject({ name: 'build-main', deployNodeIds: '73', deployId: 25, projectId: 2, remark: '备注' });
    expect(body).not.toHaveProperty('token');
    expect(body).not.toHaveProperty('secret');
  });
});

describe('compileNodeChoices / deployTargetChoices', () => {
  it('按编译节点名称对齐物理节点编号，当前值对不上时保留', () => {
    const choices = compileNodeChoices(
      [{ nodeName: 'DGCWIAX10-Win', enable: true }, { nodeName: '停用机', enable: false }],
      [{ id: 2, name: 'DGCWIAX10-Win' }],
      9,
    );
    expect(choices.map((c) => c.nodeId)).toEqual([9, 2]);
    expect(choices[1]?.label).toBe('DGCWIAX10-Win');
  });

  it('详情里有、列表没有的部署节点仍然可选', () => {
    const choices = deployTargetChoices([{ id: 10, name: 'n1', ip: '10.0.0.1' }], [10, 73]);
    expect(choices.map((c) => c.id)).toEqual([10, 73]);
    expect(choices[1]?.label).toContain('#73');
  });
});

describe('canReprocess', () => {
  it('只有失败和已取消可以重跑', () => {
    expect(canReprocess('Failed')).toBe(true);
    expect(canReprocess('Cancelled')).toBe(true);
    expect(canReprocess('Success')).toBe(false);
    expect(canReprocess('Building')).toBe(false);
  });
});

describe('请求', () => {
  it('列表失败与空列表分开', async () => {
    const ok = vi.fn<Req>(async () => ({ code: 0, data: [] }));
    expect(await fetchPipelineCards(ok, 25)).toEqual([]);
    const bad = vi.fn<Req>(async () => ({ code: 500, message: 'boom', data: [] }));
    expect(await fetchPipelineCards(bad, 25)).toBeNull();
  });

  it('详情把重复部署节点收成表单', async () => {
    const req = vi.fn<Req>(async () => ({
      code: 0,
      data: { name: 'p', branch: 'main', buildNodeId: 2, deployNodeIds: ',73,73,', token: 't', enable: true, autoDeploy: false },
    }));
    const f = await fetchPipelineDetail(req, 5);
    expect(f?.deployNodeIds).toEqual([73]);
    expect(f).not.toHaveProperty('token');
  });

  it('创建先取项目编号，POST 不带 token', async () => {
    const calls: Array<{ url?: string; method?: string; data?: Record<string, unknown> }> = [];
    const req = vi.fn<Req>(async (opt) => {
      calls.push(opt as { url?: string; data?: Record<string, unknown> });
      if (String(opt.url).includes('GetDeployProject')) return { code: 0, data: 2 };
      return { code: 0, message: 'ok' };
    });
    const r = await createPipeline(req, 25, form());
    expect(r.ok).toBe(true);
    const post = calls.find((c) => c.method === 'POST');
    expect(post?.data).toMatchObject({ deployId: 25, projectId: 2, name: 'build-main' });
    expect(post?.data).not.toHaveProperty('token');
  });

  it('保存走 PUT /{id}', async () => {
    const req = vi.fn<Req>(async () => ({ code: 0 }));
    const r = await savePipeline(req, 5, form());
    expect(r).toMatchObject({ ok: true, message: '流水线已保存' });
    expect(req).toHaveBeenCalledWith(expect.objectContaining({ url: '/Deployment/AppPipeline/5', method: 'PUT' }));
  });

  it('Webhook 成功带回地址，失败不编造', async () => {
    const ok = vi.fn<Req>(async () => ({ code: 0, data: 'http://h/Deployment/Pipeline/Webhook?token=abc' }));
    const hit = await fetchWebhookUrl(ok, 5);
    expect(hit.ok).toBe(true);
    expect(hit.url).toContain('token=abc');
    const miss = vi.fn<Req>(async () => ({ code: 500, message: 'Pipeline does not exist.', data: null }));
    expect(await fetchWebhookUrl(miss, 5)).toMatchObject({ ok: false, url: '' });
  });
});
