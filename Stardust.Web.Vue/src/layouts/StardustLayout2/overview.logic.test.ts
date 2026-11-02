// 应用概览「需关注事项」纯逻辑单测（Vitest）。2026-10-29 停点 19 第 7 步：先写测试，实现之前应当是红的。
import { describe, it, expect } from 'vitest';
import { LONG_BUILD_MINUTES, buildAttention } from './overview.logic';
import type { AttentionInput } from './overview.logic';

const NOW = Date.parse('2026-10-29T12:00:00');

function input(over: Partial<AttentionInput> = {}): AttentionInput {
  return {
    deploy: { version: '1.2.0', multiVersion: false },
    nodes: [],
    nodesFailed: false,
    runs: [],
    history: [],
    instances: 2,
    now: NOW,
    ...over,
  };
}

const find = (i: AttentionInput, key: string) => buildAttention(i).find((x) => x.key === key)!;

describe('buildAttention · 整体', () => {
  it('固定四项，次序：版本、流水线、部署历史、实例', () => {
    expect(buildAttention(input()).map((x) => x.key)).toEqual(['versions', 'pipeline', 'history', 'instances']);
  });

  it('每项都带跳转页签与按钮文案', () => {
    const tabs = buildAttention(input()).map((x) => `${x.key}:${x.tab}`);
    expect(tabs).toEqual(['versions:nodes', 'pipeline:pipeline', 'history:history', 'instances:instances']);
    for (const x of buildAttention(input())) expect(x.actionLabel).not.toBe('');
  });
});

describe('versions · 版本不一致的启用节点', () => {
  it('节点加载失败：待接入态，不判一致', () => {
    const r = find(input({ nodesFailed: true }), 'versions');
    expect(r.tone).toBe('pending');
    expect(r.detail).toContain('加载失败');
  });

  it('没有启用的节点：中性提示', () => {
    const r = find(input({ nodes: [{ enable: false, version: '1.0.0' }] }), 'versions');
    expect(r.tone).toBe('info');
    expect(r.detail).toContain('没有启用');
  });

  it('全部一致：ok，写明节点数', () => {
    const r = find(input({ nodes: [{ enable: true, version: '1.2.0' }, { enable: true, version: '1.2.0' }] }), 'versions');
    expect(r.tone).toBe('ok');
    expect(r.detail).toContain('2 个');
  });

  it('有不一致：warn，写明不一致个数与部署集版本；停用节点不计入', () => {
    const r = find(
      input({
        nodes: [
          { enable: true, version: '1.2.0' },
          { enable: true, version: '1.1.0' },
          { enable: true, version: '' },
          { enable: false, version: '0.9.0' },
        ],
      }),
      'versions',
    );
    expect(r.tone).toBe('warn');
    expect(r.detail).toContain('2 个');
    expect(r.detail).toContain('1.2.0');
  });

  it('开了多版本：版本不同属于允许，不告警', () => {
    const r = find(
      input({ deploy: { version: '1.2.0', multiVersion: true }, nodes: [{ enable: true, version: '1.1.0' }] }),
      'versions',
    );
    expect(r.tone).toBe('info');
    expect(r.detail).toContain('多版本');
  });

  it('没有部署集：待接入态', () => {
    const r = find(input({ deploy: null }), 'versions');
    expect(r.tone).toBe('pending');
  });
});

describe('pipeline · 最近一次运行', () => {
  it('没有运行记录：中性提示', () => {
    expect(find(input({ runs: [] }), 'pipeline').tone).toBe('info');
  });

  it('最近一次失败：warn', () => {
    const r = find(input({ runs: [{ id: 9, status: 'Failed', pipelineName: 'main' }, { id: 8, status: 'Success' }] }), 'pipeline');
    expect(r.tone).toBe('warn');
    expect(r.detail).toContain('失败');
    expect(r.detail).toContain('main');
  });

  it('只看最近一次：更早的失败不告警', () => {
    const r = find(input({ runs: [{ id: 9, status: 'Success' }, { id: 8, status: 'Failed' }] }), 'pipeline');
    expect(r.tone).toBe('ok');
  });

  it('构建中超过阈值：warn，写明已过分钟数', () => {
    const start = new Date(NOW - (LONG_BUILD_MINUTES + 5) * 60000).toISOString();
    const r = find(input({ runs: [{ id: 1, status: 'Building', buildStartedTime: start }] }), 'pipeline');
    expect(r.tone).toBe('warn');
    expect(r.detail).toContain(`${LONG_BUILD_MINUTES + 5} 分钟`);
  });

  it('构建中未超阈值：中性提示', () => {
    const start = new Date(NOW - 3 * 60000).toISOString();
    const r = find(input({ runs: [{ id: 1, status: 'Building', buildStartedTime: start }] }), 'pipeline');
    expect(r.tone).toBe('info');
    expect(r.detail).toContain('构建中');
  });

  it('构建中但开始时间解析不了：不判超时', () => {
    const r = find(input({ runs: [{ id: 1, status: 'Building', buildStartedTime: '' }] }), 'pipeline');
    expect(r.tone).toBe('info');
  });

  it('最近一次成功：ok；已取消：中性', () => {
    expect(find(input({ runs: [{ id: 1, status: 'Success' }] }), 'pipeline').tone).toBe('ok');
    expect(find(input({ runs: [{ id: 1, status: 'Cancelled' }] }), 'pipeline').tone).toBe('info');
  });
});

describe('history · 最近一次部署下发', () => {
  it('加载失败：待接入态，不写成没有记录', () => {
    const r = find(input({ history: null }), 'history');
    expect(r.tone).toBe('pending');
    expect(r.detail).toContain('加载失败');
  });

  it('没有记录：中性提示', () => {
    expect(find(input({ history: [] }), 'history').tone).toBe('info');
  });

  it('最近一次下发失败：warn，写明动作、节点、时间', () => {
    const r = find(
      input({ history: [{ success: false, actionLabel: '安装', node: 'node-a', time: '2026-10-29 11:50:00' }] }),
      'history',
    );
    expect(r.tone).toBe('warn');
    expect(r.detail).toContain('安装');
    expect(r.detail).toContain('node-a');
    expect(r.detail).toContain('11:50');
  });

  it('最近一次下发成功：ok，并写明只代表命令已发出', () => {
    const r = find(input({ history: [{ success: true, actionLabel: '安装', node: 'n', time: 't' }] }), 'history');
    expect(r.tone).toBe('ok');
    expect(r.detail).toContain('命令已发出');
  });
});

describe('没有部署集与部署集加载失败 · 取不到不等于没有记录', () => {
  it('没有部署集：流水线与部署历史待接入，不写成没有记录', () => {
    const i = input({ deploy: null });
    for (const key of ['pipeline', 'history']) {
      const r = find(i, key);
      expect(r.tone).toBe('pending');
      expect(r.detail).toContain('没有部署集');
    }
  });

  it('部署集加载失败：版本、流水线、部署历史都待接入，写明加载失败', () => {
    const i = input({ deploy: null, deployFailed: true });
    for (const key of ['versions', 'pipeline', 'history']) {
      const r = find(i, key);
      expect(r.tone).toBe('pending');
      expect(r.detail).toContain('部署集加载失败');
    }
  });

  it('没有部署集或部署集加载失败：在线实例不受影响', () => {
    expect(find(input({ deploy: null, instances: 3 }), 'instances').tone).toBe('info');
    expect(find(input({ deploy: null, deployFailed: true, instances: 0 }), 'instances').tone).toBe('warn');
  });
});

describe('instances · 在线实例数', () => {
  it('数量未知：待接入', () => {
    expect(find(input({ instances: undefined }), 'instances').tone).toBe('pending');
  });

  it('0 个在线：warn', () => {
    expect(find(input({ instances: 0 }), 'instances').tone).toBe('warn');
  });

  it('有在线实例：中性，写明个数', () => {
    const r = find(input({ instances: 3 }), 'instances');
    expect(r.tone).toBe('info');
    expect(r.detail).toContain('3 个');
  });
});
