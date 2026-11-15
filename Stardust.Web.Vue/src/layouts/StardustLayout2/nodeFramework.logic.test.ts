import { describe, expect, it } from 'vitest';
import {
  buildFrameworkInstallParams,
  buildFrameworkKeys,
  buildFrameworkUninstallParams,
  fetchFrameworkKinds,
  fetchFrameworkVersions,
  runFrameworkInstall,
  runFrameworkUninstall,
  toFrameworkOptions,
} from './nodeFramework.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel';

type Rec = Record<string, unknown>;

function okReq(message = '操作成功！下发1个，响应1个，超时0个'): { req: Req; calls: Rec[] } {
  const calls: Rec[] = [];
  const req: Req = (async (cfg: Rec) => {
    calls.push(cfg);
    return { code: 0, message, data: null };
  }) as unknown as Req;
  return { req, calls };
}

describe('buildFrameworkKeys', () => {
  it('节点 Id 集合转逗号串并去重过滤非法值', () => {
    expect(buildFrameworkKeys([3, 1, 3, 0, -2, Number.NaN])).toBe('3,1');
  });
  it('空集合给空串', () => {
    expect(buildFrameworkKeys([])).toBe('');
  });
});

describe('buildFrameworkInstallParams', () => {
  it('ver「自动（最新）」=传空（ResolveForPush 自动匹配），显式版本 trim 传值', () => {
    expect(buildFrameworkInstallParams([1, 2], '', 'runtime')).toEqual({ keys: '1,2', ver: '', kind: 'runtime' });
    expect(buildFrameworkInstallParams([1], ' 8.0.11 ', ' runtime ')).toEqual({ keys: '1', ver: '8.0.11', kind: 'runtime' });
  });
  it('kind 空串原样传空（后端 auto 等效，Biz :337）', () => {
    expect(buildFrameworkInstallParams([5], '9.0.0', '')).toEqual({ keys: '5', ver: '9.0.0', kind: '' });
  });
  it('无有效节点返回 null 不发请求', () => {
    expect(buildFrameworkInstallParams([0, -1], '8.0.11', '')).toBeNull();
  });
});

describe('buildFrameworkUninstallParams', () => {
  it('keys/ver 齐备且无 kind 字段', () => {
    const p = buildFrameworkUninstallParams([1, 2], '8.0.11');
    expect(p).toEqual({ keys: '1,2', ver: '8.0.11' });
    expect(p && 'kind' in p).toBe(false);
  });
  it('ver 空或未选 → null（后端 ArgumentNullException 防御）', () => {
    expect(buildFrameworkUninstallParams([1], '')).toBeNull();
    expect(buildFrameworkUninstallParams([1], '   ')).toBeNull();
  });
});

describe('toFrameworkOptions', () => {
  it('字典对象转选项数组', () => {
    expect(toFrameworkOptions({ '8.0.11': '8.0.11', '9.0.0': '9.0.0' })).toEqual([
      { value: '8.0.11', label: '8.0.11' },
      { value: '9.0.0', label: '9.0.0' },
    ]);
  });
  it('label 兜底为键并丢空键', () => {
    expect(toFrameworkOptions({ auto: '自动', '': 'x', k: null })).toEqual([
      { value: 'auto', label: '自动' },
      { value: 'k', label: 'k' },
    ]);
  });
  it('非对象/null 给空数组', () => {
    expect(toFrameworkOptions(null)).toEqual([]);
    expect(toFrameworkOptions('x')).toEqual([]);
  });
});

describe('fetchFrameworkVersions / fetchFrameworkKinds', () => {
  it('GET 正确 URL 并拆 {data:{…}} 信封', async () => {
    const calls: Rec[] = [];
    const req = (async (cfg: Rec) => {
      calls.push(cfg);
      return cfg.url === '/Nodes/NodeFramework/GetVersionList'
        ? { code: 0, data: { '8.0.11': '8.0.11' } }
        : { code: 0, data: { auto: '自动' } };
    }) as unknown as Req;
    const vers = await fetchFrameworkVersions(req);
    const kinds = await fetchFrameworkKinds(req);
    expect((calls[0] as Rec).url).toBe('/Nodes/NodeFramework/GetVersionList');
    expect((calls[1] as Rec).url).toBe('/Nodes/NodeFramework/GetKindList');
    expect(vers).toEqual([{ value: '8.0.11', label: '8.0.11' }]);
    expect(kinds).toEqual([{ value: 'auto', label: '自动' }]);
  });
  it('请求失败静默空列表', async () => {
    const fail: Req = (async () => {
      throw new Error('network');
    }) as unknown as Req;
    expect(await fetchFrameworkVersions(fail)).toEqual([]);
    expect(await fetchFrameworkKinds(fail)).toEqual([]);
  });
});

describe('runFrameworkInstall', () => {
  it('GET InstallFramework 且 ver「自动（最新）」=传空（C09b 裁定断言）', async () => {
    const { req, calls } = okReq();
    const r = await runFrameworkInstall(req, [7, 9], '', 'auto');
    expect(calls[0]).toMatchObject({
      url: '/Nodes/NodeFramework/InstallFramework',
      method: 'GET',
      params: { keys: '7,9', ver: '', kind: 'auto' },
    });
    expect(r).toEqual({ message: '操作成功！下发1个，响应1个，超时0个', error: '' });
  });
  it('code 非 0 → 失败条错误（§11）', async () => {
    const calls: Rec[] = [];
    const req = (async (cfg: Rec) => {
      calls.push(cfg);
      return { code: 400, message: '没有可推送的节点！' };
    }) as unknown as Req;
    const r = await runFrameworkInstall(req, [1], '', '');
    expect(r.error).toBe('没有可推送的节点！');
    expect(r.message).toBe('');
  });
  it('异常归 error；无有效节点不发请求', async () => {
    const fail: Req = (async () => {
      throw new Error('boom');
    }) as unknown as Req;
    expect((await runFrameworkInstall(fail, [1], '', '')).error).toBe('boom');
    const { req, calls } = okReq();
    const r = await runFrameworkInstall(req, [], '', '');
    expect(r.error).toBe('请先勾选节点');
    expect(calls.length).toBe(0);
  });
});

describe('runFrameworkUninstall', () => {
  it('GET UninstallFramework 带 keys/ver 且无 kind（C09b）', async () => {
    const { req, calls } = okReq();
    const r = await runFrameworkUninstall(req, [4], '8.0.11');
    const params = (calls[0] as Rec).params as Rec;
    expect(calls[0]).toMatchObject({ url: '/Nodes/NodeFramework/UninstallFramework', method: 'GET' });
    expect(params).toEqual({ keys: '4', ver: '8.0.11' });
    expect('kind' in params).toBe(false);
    expect(r.error).toBe('');
  });
  it('未选版本 → 「请选择框架版本」（C07 逐字）不发请求', async () => {
    const { req, calls } = okReq();
    const r = await runFrameworkUninstall(req, [4], '');
    expect(r.error).toBe('请选择框架版本');
    expect(calls.length).toBe(0);
  });
  it('异常归 error', async () => {
    const fail: Req = (async () => {
      throw new Error('net down');
    }) as unknown as Req;
    expect((await runFrameworkUninstall(fail, [1], '8.0.11')).error).toBe('net down');
  });
});

describe('成功判据（NODE-11 同判例）', () => {
  it('success=true 亦算成功并取 message', async () => {
    const req = (async () => ({ success: true, message: '已下发' })) as unknown as Req;
    expect(await runFrameworkInstall(req, [1], '', '')).toEqual({ message: '已下发', error: '' });
  });
  it('message 缺失兜底「操作失败」', async () => {
    const req = (async () => ({ code: 500 })) as unknown as Req;
    expect((await runFrameworkUninstall(req, [1], '8.0.11')).error).toBe('操作失败');
  });
});
