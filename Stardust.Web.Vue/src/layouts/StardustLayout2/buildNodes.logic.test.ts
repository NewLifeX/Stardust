// 编译节点纯逻辑单测（Vitest）：request 用桩函数注入，不发真实请求。
import { describe, it, expect } from 'vitest';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  actionInfo,
  BUILD_NODE_ACTIONS,
  fetchBuildNodes,
  fmtBuildTime,
  runBuildUpload,
  runPackageUpload,
  toBuildNodeRows,
} from './buildNodes.logic';

// 桩：返回什么就是什么；函数内抛错模拟请求失败
const mk = (fn: (o: ReqOptions) => unknown): Req => ((o: ReqOptions) => Promise.resolve(fn(o))) as Req;

describe('toBuildNodeRows · 字段映射', () => {
  it('PascalCase 字段正确映射', () => {
    const r = toBuildNodeRows([{
      Id: 5, DeployId: 7, NodeId: 11, NodeName: 'node-a', NodeIP: '10.0.0.1',
      Enable: true, SourcePath: '/src/a', PullCode: true, BuildProject: false,
      PackageOutput: true, UploadPackage: false, OutputPath: '/out',
      UpdateTime: '2026-10-30T10:00:00', Remark: 'note',
    }]);
    expect(r[0]).toMatchObject({
      id: 5, deployId: 7, nodeId: 11, nodeName: 'node-a', nodeIp: '10.0.0.1',
      enable: true, sourcePath: '/src/a', pullCode: true, buildProject: false,
      packageOutput: true, uploadPackage: false, outputPath: '/out',
      updateTime: '2026-10-30T10:00:00', remark: 'note',
    });
  });

  it('camelCase 字段也认', () => {
    const r = toBuildNodeRows([{
      id: 6, deployId: 7, nodeId: 12, nodeName: 'node-b', nodeIp: '10.0.0.2',
      enable: 1, sourcePath: '/src/b', pullCode: 0, buildProject: 'true',
      packageOutput: 'false', uploadPackage: true, outputPath: '',
      updateTime: '2026-10-30T11:00:00',
    }]);
    expect(r[0]).toMatchObject({
      id: 6, nodeName: 'node-b', nodeIp: '10.0.0.2',
      enable: true, pullCode: false, buildProject: true, packageOutput: false,
    });
  });

  it('NodeName 缺失时用 #nodeId 兜底', () => {
    const r = toBuildNodeRows([{ Id: 1, DeployId: 7, NodeId: 11, Enable: true }]);
    expect(r[0].nodeName).toBe('#11');
  });

  it('NodeName 为空字符串时也兜底', () => {
    const r = toBuildNodeRows([{ Id: 1, DeployId: 7, NodeId: 12, NodeName: '  ' }]);
    expect(r[0].nodeName).toBe('#12');
  });

  it('布尔字段多种取值：true/false/1/0/"true"/"false"', () => {
    const r = toBuildNodeRows([{
      Id: 1, NodeId: 1, Enable: true, PullCode: false, BuildProject: 1, PackageOutput: 0, UploadPackage: 'true', SourcePath: '',
    }]);
    expect(r[0].enable).toBe(true);
    expect(r[0].pullCode).toBe(false);
    expect(r[0].buildProject).toBe(true);
    expect(r[0].packageOutput).toBe(false);
    expect(r[0].uploadPackage).toBe(true);
  });

  it('空行字段都安全降级', () => {
    const r = toBuildNodeRows([{ Id: 1 }]);
    expect(r[0]).toMatchObject({
      id: 1, deployId: 0, nodeId: 0, nodeName: '#0', nodeIp: '',
      enable: false, sourcePath: '', pullCode: false, buildProject: false,
      packageOutput: false, uploadPackage: false, outputPath: '', updateTime: '', remark: '',
    });
  });
});

describe('fetchBuildNodes · 取数', () => {
  it('正常返回数组', async () => {
    const r = await fetchBuildNodes(mk(() => ({ data: [{ Id: 1, NodeId: 11, NodeName: 'n1', Enable: true }] })), 7);
    expect(r).toHaveLength(1);
    expect(r?.[0].nodeName).toBe('n1');
  });

  it('deployId 无效返回空数组且不发请求', async () => {
    let called = 0;
    const req = mk(() => { called++; return { data: [] }; });
    expect(await fetchBuildNodes(req, 0)).toEqual([]);
    expect(await fetchBuildNodes(req, -1)).toEqual([]);
    expect(called).toBe(0);
  });

  it('请求失败返回 null', async () => {
    const r = await fetchBuildNodes(mk(() => { throw new Error('x'); }), 7);
    expect(r).toBeNull();
  });

  it('请求参数：url 和 params', async () => {
    let seen: ReqOptions | undefined;
    await fetchBuildNodes(mk((o) => { seen = o; return { data: [] }; }), 7);
    expect(seen?.url).toBe('/Deployment/AppBuildNode');
    expect(seen?.method).toBe('GET');
    expect(seen?.params).toMatchObject({ deployId: 7, PageSize: 200 });
  });

  it('空列表返回空数组（不是 null）', async () => {
    const r = await fetchBuildNodes(mk(() => ({ data: [] })), 7);
    expect(r).toEqual([]);
  });
});

describe('runBuildUpload / runPackageUpload · 动作', () => {
  it('Build-Upload 请求参数正确', async () => {
    let seen: ReqOptions | undefined;
    await runBuildUpload(mk((o) => { seen = o; return { code: 0 }; }), 5);
    expect(seen?.url).toBe('/Deployment/AppBuildNode/Operate');
    expect(seen?.params).toEqual({ id: 5, act: 'Build-Upload' });
  });

  it('Package-Upload 请求参数正确', async () => {
    let seen: ReqOptions | undefined;
    await runPackageUpload(mk((o) => { seen = o; return { code: 0 }; }), 8);
    expect(seen?.url).toBe('/Deployment/AppBuildNode/Operate');
    expect(seen?.params).toEqual({ id: 8, act: 'Package-Upload' });
  });

  it('code 为 0 视为成功', async () => {
    const r = await runBuildUpload(mk(() => ({ code: 0, message: '已执行' })), 5);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('已执行');
  });

  it('成功但无文案时用默认文案', async () => {
    const r = await runPackageUpload(mk(() => ({ code: 0 })), 5);
    expect(r.ok).toBe(true);
    expect(r.message).toContain('打包上传');
  });

  it('业务失败返回 ok=false 并带后端文案', async () => {
    const r = await runBuildUpload(mk(() => ({ code: 500, message: '节点不存在' })), 5);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('节点不存在');
  });

  it('请求抛错带回错误信息', async () => {
    const r = await runPackageUpload(mk(() => { throw new Error('超时'); }), 5);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('超时');
  });

  it('id 无效不发请求', async () => {
    let called = 0;
    const req = mk(() => { called++; return { code: 0 }; });
    expect((await runBuildUpload(req, 0)).ok).toBe(false);
    expect((await runPackageUpload(req, -3)).ok).toBe(false);
    expect(called).toBe(0);
  });
});

describe('actionInfo · 动作元数据', () => {
  it('BUILD_NODE_ACTIONS 有两个动作', () => {
    expect(BUILD_NODE_ACTIONS.map((a) => a.act)).toEqual(['Build-Upload', 'Package-Upload']);
  });

  it('actionInfo 能查到已知动作，查不到返回 undefined', () => {
    expect(actionInfo('Build-Upload')?.label).toBe('编译上传');
    expect(actionInfo('Package-Upload')?.label).toBe('打包上传');
    expect(actionInfo('Delete')).toBeUndefined();
  });
});

describe('fmtBuildTime · 时间格式化', () => {
  it('去掉 T 和秒', () => {
    expect(fmtBuildTime('2026-10-30T10:20:30')).toBe('2026-10-30 10:20');
  });

  it('空值返回破折号', () => {
    expect(fmtBuildTime('')).toBe('—');
    expect(fmtBuildTime(undefined)).toBe('—');
  });
});
