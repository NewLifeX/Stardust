// 跨应用「变更」时间线纯逻辑单测（Vitest）。2026-10-29 停点 20：先红后绿。
// 核心约束：三张历史表（部署历史 / 流水线运行 / 配置历史）不读 projectId，全局返回，
// 必须在前端按「项目内 id 集合」过滤，否则违反项目隔离。
import { describe, it, expect } from 'vitest';
import {
  buildConfigMap,
  buildDeployMap,
  buildPipelineMap,
  fetchChanges,
  mergeChanges,
  toConfigChanges,
  toDeployChanges,
  toPipelineChanges,
} from './changes.logic';
import type { ChangeItem } from './changes.logic';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 桩请求：按 url 返回预设 data；throwOn 时全部抛错 */
function stubReq(map: Record<string, unknown[]>, throwOn = false): Req {
  return async (opt) => {
    if (throwOn) throw new Error('stub 500');
    const url = String(opt.url ?? '');
    if (url in map) return { data: map[url] };
    return { data: [] };
  };
}

describe('buildDeployMap · 部署集映射（读 projectId）', () => {
  it('Id→{appId,appName}，跳过无效行', () => {
    const m = buildDeployMap([
      { Id: 11, AppId: 101, AppName: 'OrderService' },
      { Id: 12, AppId: 102, AppName: 'IotWeb' },
      { Id: 0, AppId: 103, AppName: 'Bad' },
    ]);
    expect(m.get(11)).toEqual({ appId: 101, appName: 'OrderService' });
    expect(m.get(12)).toEqual({ appId: 102, appName: 'IotWeb' });
    expect(m.has(0)).toBe(false);
  });
});

describe('buildConfigMap · 配置映射（读 projectId）', () => {
  it('Id→{appId, appName 取 Name 字段}', () => {
    const m = buildConfigMap([{ Id: 21, AppId: 101, Name: 'OrderService' }]);
    expect(m.get(21)).toEqual({ appId: 101, appName: 'OrderService' });
  });
});

describe('buildPipelineMap · 流水线映射（全局，不读 projectId）', () => {
  it('pipelineId→deployId', () => {
    const m = buildPipelineMap([
      { Id: 301, DeployId: 11 },
      { Id: 302, DeployId: 999 },
    ]);
    expect(m.get(301)).toBe(11);
    expect(m.get(302)).toBe(999);
  });
});

describe('toDeployChanges · 部署历史按 deployMap 过滤', () => {
  const deployMap = buildDeployMap([{ Id: 11, AppId: 101, AppName: 'OrderService' }]);

  it('只保留 deployId ∈ deployMap 的行，kind=D，跳 /app/history/<appId>', () => {
    const items = toDeployChanges(
      [
        { Id: 1, DeployId: 11, Action: 'install', Success: true, Remark: 'node-a', CreateUserId: 7, CreateTime: '2026-10-28T09:30:00' },
        { Id: 2, DeployId: 777, Action: 'install', Success: true, CreateTime: '2026-10-28T09:31:00' },
      ],
      deployMap,
    );
    expect(items).toHaveLength(1);
    const it = items[0];
    expect(it.kind).toBe('D');
    expect(it.appName).toBe('OrderService');
    expect(it.path).toBe('/app/history/101');
    expect(it.ok).toBe(true);
    expect(it.summary).toContain('node-a');
    expect(it.time).toContain('2026-10-28');
  });

  it('操作人取 CreateUserId，数字 id 显示出来；没有则空', () => {
    const withUser = toDeployChanges(
      [{ Id: 1, DeployId: 11, Action: 'start', Success: true, CreateUserId: 7, CreateTime: '2026-10-28T09:30:00' }],
      deployMap,
    );
    expect(withUser[0].operator).toContain('7');
    const noUser = toDeployChanges(
      [{ Id: 2, DeployId: 11, Action: 'start', Success: true, CreateTime: '2026-10-28T09:30:00' }],
      deployMap,
    );
    expect(noUser[0].operator).toBe('');
  });
});

describe('toPipelineChanges · 流水线运行经 pipelineMap→deployMap 两跳取应用', () => {
  const deployMap = buildDeployMap([{ Id: 11, AppId: 101, AppName: 'OrderService' }]);
  const pipelineMap = buildPipelineMap([
    { Id: 301, DeployId: 11 },
    { Id: 302, DeployId: 777 },
  ]);

  it('只保留 pipelineMap 里 deployId ∈ deployMap 的运行；跳 /app/pipeline/<appId>', () => {
    const items = toPipelineChanges(
      [
        { Id: 9, PipelineId: 301, Status: 4, CommitMessage: 'feat: deploy', Branch: 'main', CommitAuthor: 'zhang', UpdateTime: '2026-10-28T10:00:00' },
        { Id: 10, PipelineId: 302, Status: 1, Branch: 'dev', UpdateTime: '2026-10-28T10:01:00' },
      ],
      pipelineMap,
      deployMap,
    );
    expect(items).toHaveLength(1);
    const it = items[0];
    expect(it.kind).toBe('D');
    expect(it.appName).toBe('OrderService');
    expect(it.path).toBe('/app/pipeline/101');
    expect(it.summary).toBe('feat: deploy');
    expect(it.operator).toBe('zhang');
    expect(it.ok).toBe(true);
  });

  it('摘要兜底：无 CommitMessage 用 Branch，再兜底状态文案', () => {
    const items = toPipelineChanges(
      [
        { Id: 9, PipelineId: 301, Status: 1, Branch: 'release/2.0', UpdateTime: '2026-10-28T10:00:00' },
        { Id: 10, PipelineId: 301, Status: 0, UpdateTime: '2026-10-28T10:00:00' },
      ],
      pipelineMap,
      deployMap,
    );
    expect(items[0].summary).toBe('release/2.0');
    expect(items[1].summary).toContain('等待'); // Pending 状态文案
    expect(items[1].ok).toBeNull();
  });

  it('排序时间优先 UpdateTime，兜底 BuildStartedTime / CreateTime', () => {
    const items = toPipelineChanges(
      [
        { Id: 9, PipelineId: 301, Status: 4, CreateTime: '2026-10-01T00:00:00', BuildStartedTime: '2026-10-02T00:00:00', UpdateTime: '2026-10-03T00:00:00' },
      ],
      pipelineMap,
      deployMap,
    );
    expect(items[0].time).toContain('2026-10-03');
  });
});

describe('toConfigChanges · 配置历史按 configMap 过滤', () => {
  const configMap = buildConfigMap([{ Id: 21, AppId: 101, Name: 'OrderService' }]);

  it('只保留 configId ∈ configMap 的行，kind=C，跳 /app/config/<appId>', () => {
    const items = toConfigChanges(
      [
        { Id: 5, ConfigId: 21, Action: 'Publish', Success: true, CreateUserID: 3, CreateTime: '2026-10-28T08:00:00' },
        { Id: 6, ConfigId: 888, Action: 'Publish', Success: true, CreateTime: '2026-10-28T08:01:00' },
      ],
      configMap,
    );
    expect(items).toHaveLength(1);
    const it = items[0];
    expect(it.kind).toBe('C');
    expect(it.appName).toBe('OrderService');
    expect(it.path).toBe('/app/config/101');
    expect(it.summary).toContain('发布');
    expect(it.operator).toContain('3');
  });
});

describe('mergeChanges · 三类合并按时间倒序、截断', () => {
  const mk = (sortAt: number, id: string): ChangeItem => ({
    id, kind: 'D', appName: 'a', appId: 1, time: '', sortAt, summary: '', operator: '', path: '/app/history/1', ok: null,
  });

  it('时间倒序（新的在前）', () => {
    const merged = mergeChanges([mk(1000, 'd-1'), mk(3000, 'd-2'), mk(2000, 'd-3')]);
    expect(merged.map((m) => m.id)).toEqual(['d-2', 'd-3', 'd-1']);
  });

  it('limit 截断为前 N 条', () => {
    const merged = mergeChanges([mk(1, 'd-1'), mk(2, 'd-2'), mk(3, 'd-3'), mk(4, 'd-4')], 2);
    expect(merged).toHaveLength(2);
    expect(merged[0].sortAt).toBe(4);
  });
});

describe('fetchChanges · 端到端取数编排（request 注入）', () => {
  it('全部成功：构建映射、过滤全局历史、合并倒序，error 为空', async () => {
    const req = stubReq({
      '/Deployment/AppDeploy': [{ Id: 11, AppId: 101, AppName: 'OrderService' }],
      '/Configs/AppConfig': [{ Id: 21, AppId: 101, Name: 'OrderService' }],
      '/Deployment/AppPipeline': [{ Id: 301, DeployId: 11 }],
      '/Deployment/AppDeployHistory': [
        { Id: 1, DeployId: 11, Action: 'install', Success: true, CreateTime: '2026-10-28T09:00:00' },
        { Id: 2, DeployId: 999, Action: 'install', Success: true, CreateTime: '2026-10-28T09:01:00' },
      ],
      '/Deployment/AppPipelineRun': [
        { Id: 9, PipelineId: 301, Status: 4, CommitMessage: 'feat', UpdateTime: '2026-10-28T10:00:00' },
      ],
      '/Configs/ConfigHistory': [
        { Id: 5, ConfigId: 21, Action: 'Publish', Success: true, CreateTime: '2026-10-28T08:00:00' },
        { Id: 6, ConfigId: 888, Action: 'Publish', Success: true, CreateTime: '2026-10-28T08:01:00' },
      ],
    });
    const r = await fetchChanges(req);
    expect(r.error).toBe('');
    // 被过滤掉的：deployId=999、configId=888 各 1 条；保留 部署历史1 + 流水线1 + 配置1 = 3
    expect(r.items).toHaveLength(3);
    expect(r.items[0].sortAt).toBeGreaterThanOrEqual(r.items[1].sortAt);
    // 类型齐全
    expect(new Set(r.items.map((i) => i.kind))).toEqual(new Set(['D', 'C']));
  });

  it('「全部项目」：请求不带 projectId，三张映射表全局返回，历史行自然全部放行', async () => {
    // 项目上下文为空时，后端不按 projectId 收窄：AppDeploy/AppConfig/AppPipeline 返回全部行，
    // 映射表因此包含所有 id，全局历史行的 deployId/configId/pipelineId 必然落在表里 → 全放行。
    const req = stubReq({
      '/Deployment/AppDeploy': [
        { Id: 11, AppId: 101, AppName: 'OrderService' },
        { Id: 55, AppId: 202, AppName: 'OtherApp' },
      ],
      '/Configs/AppConfig': [{ Id: 21, AppId: 101, Name: 'OrderService' }],
      '/Deployment/AppPipeline': [{ Id: 301, DeployId: 11 }],
      '/Deployment/AppDeployHistory': [
        { Id: 1, DeployId: 11, Action: 'install', Success: true, CreateTime: '2026-10-28T09:00:00' },
        { Id: 2, DeployId: 55, Action: 'start', Success: true, CreateTime: '2026-10-28T09:01:00' },
      ],
      '/Deployment/AppPipelineRun': [],
      '/Configs/ConfigHistory': [],
    });
    const r = await fetchChanges(req);
    expect(r.error).toBe('');
    // 两行部署历史都不在本项目内被过滤（55 是另一个应用，但全部项目模式放行）
    expect(r.items).toHaveLength(2);
    expect(r.items.map((i) => i.appName).sort()).toEqual(['OrderService', 'OtherApp']);
  });

  it('任一请求失败：返回 error、items 为空（不静默空态）', async () => {
    const r = await fetchChanges(stubReq({}, true));
    expect(r.items).toEqual([]);
    expect(r.error).not.toBe('');
  });
});
