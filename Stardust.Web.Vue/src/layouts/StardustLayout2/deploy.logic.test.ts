// deploy.logic.ts 单元测试：F05~F09 的请求契约与纯函数行为。request 全部注入 stub，不 mock 模块。
import { describe, expect, it } from 'vitest';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  batchOperateNodes,
  buildNodeInsertBody,
  buildNodeUpdateBody,
  createBuildNode,
  createDeployNode,
  deleteBuildNode,
  deleteDeployNode,
  deployNodeInsertBody,
  deployNodeUpdateBody,
  deployPutBody,
  fetchBuildNodeDetail,
  fetchDeployDetail,
  fetchDeployNodeDetail,
  fetchNodeOptions,
  requestFailText,
  rollbackCandidates,
  rollbackNotice,
  saveDeploy,
  syncAppDeploy,
  toBuildNodeForm,
  toDeployEditModel,
  toDeployNodeForm,
  updateBuildNode,
  updateDeployNode,
  uploadVersion,
  validateBuildNodeForm,
  validateDeployEdit,
  validateDeployNodeForm,
} from './deploy.logic.ts';

type Cfg = { url: string; method?: string; params?: Record<string, unknown>; data?: unknown; headers?: Record<string, string> };
function stubReq(responder: (cfg: Cfg) => unknown = () => ({ code: 200, data: null })): { req: Req; calls: Cfg[] } {
  const calls: Cfg[] = [];
  const req = (async (cfg: Cfg) => {
    calls.push(cfg);
    return responder(cfg);
  }) as Req;
  return { req, calls };
}

const deployForm = {
  name: '发布集A', category: 'web', enable: true, packageName: 'star.zip', autoPublish: true, multiVersion: false,
  repository: 'https://git/x', repoUserName: 'u', repoPassword: '', branch: 'main', projectPath: 'src',
  buildArgs: '', packageFilters: '*.zip', remark: 'r',
};

describe('F06 部署集编辑', () => {
  it('toDeployEditModel：大小写都认，密码一律置空', () => {
    const m = toDeployEditModel({ Id: 3, Name: 'n', category: 'c', Enable: true, PackageName: 'p.zip', AutoPublish: 1, MultiVersion: 0, Repository: 'r', RepoUserName: 'u', RepoPassword: 'masked', Branch: 'b', ProjectPath: 'pp', BuildArgs: 'ba', PackageFilters: 'pf', Remark: 'rm' });
    expect(m.name).toBe('n');
    expect(m.enable).toBe(true);
    expect(m.autoPublish).toBe(true);
    expect(m.multiVersion).toBe(false);
    expect(m.repoPassword).toBe('');
  });

  it('deployPutBody：repoPassword 留空不进 body（留空=不修改）', () => {
    const body = deployPutBody(deployForm);
    expect(body).not.toHaveProperty('repoPassword');
    expect(body.name).toBe('发布集A');
    expect(Object.keys(body)).not.toContain('id');
  });

  it('deployPutBody：输入新密码才提交', () => {
    const body = deployPutBody({ ...deployForm, repoPassword: 'np' });
    expect(body.repoPassword).toBe('np');
  });

  it('validateDeployEdit：名称必填', () => {
    expect(validateDeployEdit(deployForm)).toBe('');
    expect(validateDeployEdit({ ...deployForm, name: ' ' })).toBe('部署集名称必填');
  });

  it('fetchDeployDetail：GET /{id}，失败返回 null', async () => {
    const { req, calls } = stubReq(() => ({ code: 200, data: { Name: 'x' } }));
    const m = await fetchDeployDetail(req, 7);
    expect(calls[0].url).toBe('/Deployment/AppDeploy/7');
    expect(m?.name).toBe('x');
    expect(await fetchDeployDetail(req, 0)).toBeNull();
  });

  it('saveDeploy：PUT /{id} 部分更新', async () => {
    const { req, calls } = stubReq();
    const r = await saveDeploy(req, 7, deployForm);
    expect(r.ok).toBe(true);
    expect(calls[0].url).toBe('/Deployment/AppDeploy/7');
    expect(calls[0].method).toBe('PUT');
    expect((calls[0].data as Record<string, unknown>).name).toBe('发布集A');
  });

  it('saveDeploy：校验不过不发请求', async () => {
    const { req, calls } = stubReq();
    const r = await saveDeploy(req, 7, { ...deployForm, name: '' });
    expect(r.ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('syncAppDeploy：POST /SyncApp', async () => {
    const { req, calls } = stubReq(() => ({ code: 0, message: '成功同步[3]个应用！' }));
    const r = await syncAppDeploy(req);
    expect(calls[0].url).toBe('/Deployment/AppDeploy/SyncApp');
    expect(calls[0].method).toBe('POST');
    expect(r).toEqual({ ok: true, message: '成功同步[3]个应用！' });
  });
});

describe('F07 部署节点 CRUD + 批量', () => {
  it('fetchNodeOptions：/Nodes/Node?enable=true&PageSize=200', async () => {
    const { req, calls } = stubReq(() => ({ code: 200, data: [{ Id: 1, Name: 'n1', IP: '10.0.0.1' }, { id: 2, name: 'n2', ip: '10.0.0.2' }] }));
    const list = await fetchNodeOptions(req);
    expect(calls[0].url).toBe('/Nodes/Node');
    expect(calls[0].params).toEqual({ enable: true, PageSize: 200 });
    expect(list).toEqual([{ id: 1, name: 'n1', ip: '10.0.0.1' }, { id: 2, name: 'n2', ip: '10.0.0.2' }]);
  });

  it('fetchNodeOptions：失败返回 null', async () => {
    const req = (async () => { throw new Error('x'); }) as Req;
    expect(await fetchNodeOptions(req)).toBeNull();
  });

  it('toDeployNodeForm：大小写都认', () => {
    const m = toDeployNodeForm({ NodeId: 5, ENABLE: false, port: 8080, WorkingDirectory: '/w', MaxMemory: 1024, Delay: 2 });
    expect(m.nodeId).toBe(5);
    expect(m.enable).toBe(false);
    expect(m.port).toBe(8080);
    expect(m.maxMemory).toBe(1024);
    expect(m.delay).toBe(2);
  });

  it('toDeployNodeForm / update 体：日志目录与匹配规则', () => {
    const m = toDeployNodeForm({
      NodeId: 2,
      LogDirectory: 'D:/app/logs',
      LogFilePattern: '*.log',
    });
    expect(m.logDirectory).toBe('D:/app/logs');
    expect(m.logFilePattern).toBe('*.log');
    const body = deployNodeUpdateBody(m);
    expect(body.logDirectory).toBe('D:/app/logs');
    expect(body.logFilePattern).toBe('*.log');
  });

  it('insert 体带 deployId；update 体不带 id（主键在路径）', () => {
    const m = toDeployNodeForm({ NodeId: 5 });
    expect(deployNodeInsertBody(9, m).deployId).toBe(9);
    expect(deployNodeUpdateBody(m)).not.toHaveProperty('id');
    expect(deployNodeUpdateBody(m)).not.toHaveProperty('deployId');
  });

  it('validateDeployNodeForm：节点必选、数值非负', () => {
    const base = toDeployNodeForm({ NodeId: 1 });
    expect(validateDeployNodeForm(base)).toBe('');
    expect(validateDeployNodeForm({ ...base, nodeId: 0 })).toBe('请选择物理节点');
    expect(validateDeployNodeForm({ ...base, port: -1 })).toBe('端口不能为负数');
    expect(validateDeployNodeForm({ ...base, delay: -2 })).toBe('启动延迟不能为负数');
  });

  it('createDeployNode：POST 集合路由；updateDeployNode：PUT /{id}；deleteDeployNode：DELETE /{id}', async () => {
    const { req, calls } = stubReq();
    const m = toDeployNodeForm({ NodeId: 5 });
    expect((await createDeployNode(req, 9, m)).ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Deployment/AppDeployNode', method: 'POST' });
    expect((await updateDeployNode(req, 12, m)).ok).toBe(true);
    expect(calls[1].url).toBe('/Deployment/AppDeployNode/12');
    expect(calls[1].method).toBe('PUT');
    expect((await deleteDeployNode(req, 12)).ok).toBe(true);
    expect(calls[2]).toEqual({ url: '/Deployment/AppDeployNode/12', method: 'DELETE' });
  });

  it('createDeployNode：校验不过不发请求', async () => {
    const { req, calls } = stubReq();
    expect((await createDeployNode(req, 9, toDeployNodeForm({}))).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('fetchDeployNodeDetail：GET /{id}', async () => {
    const { req, calls } = stubReq(() => ({ code: 200, data: { NodeId: 5 } }));
    const m = await fetchDeployNodeDetail(req, 12);
    expect(calls[0].url).toBe('/Deployment/AppDeployNode/12');
    expect(m?.nodeId).toBe(5);
  });

  it('batchOperateNodes：GET BatchOperate?act=&keys= 逗号串', async () => {
    const { req, calls } = stubReq(() => ({ code: 0, message: '批量执行[restart]操作' }));
    const r = await batchOperateNodes(req, 'restart', [3, 5, 8]);
    expect(calls[0].url).toBe('/Deployment/AppDeployNode/BatchOperate');
    expect(calls[0].params).toEqual({ act: 'restart', keys: '3,5,8' });
    expect(r.ok).toBe(true);
  });

  it('batchOperateNodes：空选择不发请求', async () => {
    const { req, calls } = stubReq();
    const r = await batchOperateNodes(req, 'stop', []);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('勾选');
    expect(calls).toHaveLength(0);
  });
});

describe('F08 编译节点 CRUD', () => {
  it('toBuildNodeForm：大小写都认', () => {
    const m = toBuildNodeForm({ NodeId: 2, Enable: 1, SourcePath: 'D:/src', PullCode: true, UploadPackage: false });
    expect(m.nodeId).toBe(2);
    expect(m.enable).toBe(true);
    expect(m.pullCode).toBe(true);
    expect(m.uploadPackage).toBe(false);
  });

  it('insert 体带 deployId；update 体不带 id', () => {
    const m = toBuildNodeForm({ NodeId: 2, SourcePath: 'D:/src' });
    expect(buildNodeInsertBody(9, m).deployId).toBe(9);
    expect(buildNodeUpdateBody(m)).not.toHaveProperty('deployId');
  });

  it('validateBuildNodeForm：节点与源码目录必填', () => {
    const base = toBuildNodeForm({ NodeId: 2, SourcePath: 'D:/src' });
    expect(validateBuildNodeForm(base)).toBe('');
    expect(validateBuildNodeForm({ ...base, sourcePath: '' })).toBe('源代码目录必填（编译节点上拉取代码的位置）');
    expect(validateBuildNodeForm({ ...base, nodeId: 0 })).toBe('请选择物理节点');
  });

  it('create/update/delete 三个路由', async () => {
    const { req, calls } = stubReq();
    const m = toBuildNodeForm({ NodeId: 2, SourcePath: 'D:/src' });
    expect((await createBuildNode(req, 9, m)).ok).toBe(true);
    expect(calls[0]).toMatchObject({ url: '/Deployment/AppBuildNode', method: 'POST' });
    expect((await updateBuildNode(req, 4, m)).ok).toBe(true);
    expect(calls[1]).toMatchObject({ url: '/Deployment/AppBuildNode/4', method: 'PUT' });
    expect((await deleteBuildNode(req, 4)).ok).toBe(true);
    expect(calls[2]).toEqual({ url: '/Deployment/AppBuildNode/4', method: 'DELETE' });
  });

  it('fetchBuildNodeDetail：GET /{id}，失败 null', async () => {
    const { req, calls } = stubReq(() => ({ code: 200, data: { NodeId: 2 } }));
    expect((await fetchBuildNodeDetail(req, 4))?.nodeId).toBe(2);
    expect(calls[0].url).toBe('/Deployment/AppBuildNode/4');
  });
});

describe('F05 上传新版本', () => {
  it('uploadVersion：multipart POST，请求体是含 deployId/file 的 FormData，且显式 multipart 头', async () => {
    const { req, calls } = stubReq(() => ({ code: 0, message: '上传成功', data: { Version: '20261103-120001' } }));
    const fd = new FormData();
    fd.append('deployId', '9');
    fd.append('file', new File([new Uint8Array([1, 2, 3])], 'star.zip'));
    const r = await uploadVersion(req, 9, fd.get('file') as File);
    expect(calls[0].url).toBe('/Deployment/AppDeployVersion/Upload');
    expect(calls[0].method).toBe('POST');
    expect(r).toEqual({ ok: true, message: '上传成功', version: '20261103-120001' });
    // 请求体必须是 FormData 本身（不能被 JSON 序列化），字段齐全
    const body = calls[0].data as FormData;
    expect(body).toBeInstanceOf(FormData);
    expect(String(body.get('deployId'))).toBe('9');
    expect(body.get('file')).toBeInstanceOf(File);
    // api-core client 默认头 application/json 会把 FormData 转 JSON（formDataToJSON）→ 后端 [FromForm] 404；
    // 必须请求级覆盖 multipart/form-data（与 api-core 上传助手同一约定）
    expect((calls[0].headers as Record<string, string>)['Content-Type']).toBe('multipart/form-data');
  });

  it('uploadVersion：带 remark 时 multipart 里包含 remark', async () => {
    const { req, calls } = stubReq(() => ({ code: 0, message: '上传成功', data: { Version: 'v9' } }));
    await uploadVersion(req, 9, new File([new Uint8Array([1])], 'a.zip'), 'hotfix');
    const body = calls[0].data as FormData;
    expect(String(body.get('remark'))).toBe('hotfix');
  });

  it('uploadVersion：404/405 → 接口部署中文案', async () => {
    const err = Object.assign(new Error('Request failed with status code 404'), { status: 404 });
    const req = (async () => { throw err; }) as Req;
    const fd = new FormData();
    fd.append('file', new File([new Uint8Array([1])], 'a.zip'));
    const r = await uploadVersion(req, 9, fd.get('file') as File);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('接口部署中');
  });

  it('uploadVersion：无文件/无部署集直接拦截', async () => {
    const { req, calls } = stubReq();
    expect((await uploadVersion(req, 0, new File([new Uint8Array([1])], 'a.zip'))).ok).toBe(false);
    expect((await uploadVersion(req, 9, null as unknown as File)).ok).toBe(false);
    expect(calls).toHaveLength(0);
  });
});

describe('F09 一键回滚候选', () => {
  it('rollbackCandidates：过滤禁用/当前/无版本号，按版本倒序去重', () => {
    const list = [
      { id: 1, version: '20261101-1000', enable: true },
      { id: 2, version: '20261102-0900', enable: false },
      { id: 3, version: '', enable: true },
      { id: 4, version: '20261030-0800', enable: true },
      { id: 5, version: '20261101-1000', enable: true },
    ];
    const out = rollbackCandidates(list, '20261031-1200');
    expect(out.map((v) => v.version)).toEqual(['20261101-1000', '20261030-0800']);
  });

  it('rollbackCandidates：当前版本为空时不过滤', () => {
    const out = rollbackCandidates([{ id: 1, version: 'v1', enable: true }], '');
    expect(out).toHaveLength(1);
  });

  it('rollbackNotice：按自动发布与节点数给不同后果', () => {
    const v = { id: 1, version: 'v1', enable: true };
    expect(rollbackNotice(v, false, 3)).toContain('不会下发到节点');
    expect(rollbackNotice(v, true, 0)).toContain('没有启用节点');
    expect(rollbackNotice(v, true, 2)).toContain('2 个启用节点');
  });
});

describe('requestFailText', () => {
  it('识别 status 404/405 与 message 中的状态码', () => {
    expect(requestFailText({ status: 404 }, '上传失败')).toContain('404');
    expect(requestFailText({ response: { status: 405 } }, '上传失败')).toContain('405');
    expect(requestFailText({ message: 'Request failed with status code 404' }, '上传失败')).toContain('接口部署中');
    expect(requestFailText(new Error('boom'), '上传失败')).toBe('上传失败：boom');
    expect(requestFailText(null, '上传失败')).toBe('上传失败');
  });
});
