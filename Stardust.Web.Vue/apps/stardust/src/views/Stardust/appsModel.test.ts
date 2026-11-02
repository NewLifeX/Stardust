// 应用页纯逻辑单测（Vitest）
import { describe, it, expect } from 'vitest';
import {
  deriveAppStatus,
  aggregateKpis,
  filterByStatus,
  filterByKeyword,
  historyRows,
  toAggRows,
  instancesRows,
  configKv,
  configText,
  pipelineSteps,
  runStatusStyle,
  normalizeRunStatus,
  servicesGroups,
  type AppListItem,
} from './appsModel';

describe('deriveAppStatus · 应用运行态推导', () => {
  it('enable+在线 → running（现行语义：保守运行态）', () => {
    expect(deriveAppStatus({ enable: true }, true)).toBe('running');
  });

  it('enable=false → offline', () => {
    expect(deriveAppStatus({ enable: false }, true)).toBe('offline');
  });

  it('enable=true 但不在线 → offline', () => {
    expect(deriveAppStatus({ enable: true }, false)).toBe('offline');
  });
});

describe('toAggRows + aggregateKpis · 应用聚合与 KPI', () => {
  const apps: AppListItem[] = [
    { name: 'order-service', enable: true },
    { name: 'payment-gateway', enable: true },
    { name: 'user-center', enable: false },
  ];
  const counts = new Map<string, number>([
    ['order-service', 6],
    ['payment-gateway', 3],
  ]);
  const rows = toAggRows(apps, counts);

  it('order-service 在线（running）', () => {
    expect(rows[0].status).toBe('running');
  });

  it('order-service 实例数=6', () => {
    expect(rows[0].instances).toBe(6);
  });

  it('user-center 离线 (enable=false)', () => {
    expect(rows[2].status).toBe('offline');
  });

  it('user-center 实例数=0', () => {
    expect(rows[2].instances).toBe(0);
  });

  it('total=3', () => {
    expect(aggregateKpis(rows).total).toBe(3);
  });

  it('running=2', () => {
    expect(aggregateKpis(rows).running).toBe(2);
  });

  it('offline=1', () => {
    expect(aggregateKpis(rows).offline).toBe(1);
  });

  it('error=0', () => {
    expect(aggregateKpis(rows).error).toBe(0);
  });
});

describe('filterByStatus · 按状态过滤', () => {
  // toAggRows 以「实例数 > 0」作为在线依据，故须给出实例数才能产生 running 行
  const rows = toAggRows(
    [
      { name: 'order-service', enable: true },
      { name: 'payment-gateway', enable: true },
      { name: 'user-center', enable: false },
    ],
    new Map<string, number>([
      ['order-service', 6],
      ['payment-gateway', 3],
    ]),
  );

  it('all → 3 条', () => {
    expect(filterByStatus(rows, 'all')).toHaveLength(3);
  });

  it('running → 2 条', () => {
    expect(filterByStatus(rows, 'running')).toHaveLength(2);
  });

  it('offline → 1 条', () => {
    expect(filterByStatus(rows, 'offline')).toHaveLength(1);
  });

  it('error → 0 条', () => {
    expect(filterByStatus(rows, 'error')).toHaveLength(0);
  });
});

describe('filterByKeyword · 按关键字过滤', () => {
  const rows = toAggRows(
    [
      { name: 'order-service', enable: true },
      { name: 'payment-gateway', enable: true },
      { name: 'user-center', enable: false },
    ],
    new Map<string, number>([
      ['order-service', 6],
      ['payment-gateway', 3],
    ]),
  );

  it('order → 1 条', () => {
    expect(filterByKeyword(rows, 'order')).toHaveLength(1);
  });

  it('空关键字 → 3 条', () => {
    expect(filterByKeyword(rows, '')).toHaveLength(3);
  });
});

describe('instancesRows · 实例列表过滤', () => {
  const onlineRows = [
    { appName: 'order-service', client: 'c1', version: '1.0' },
    { name: 'pay-gateway', client: 'c2' },
    { appName: 'order-service', client: 'c3' },
  ];

  it('appName 匹配 2 行', () => {
    expect(instancesRows(onlineRows, 'order-service')).toHaveLength(2);
  });

  it('name 回退匹配', () => {
    expect(instancesRows(onlineRows, 'pay-gateway')).toHaveLength(1);
  });

  it('无匹配 → 0', () => {
    expect(instancesRows(onlineRows, 'nope')).toHaveLength(0);
  });

  it('空输入 → 0', () => {
    expect(instancesRows([], 'order-service')).toHaveLength(0);
  });
});

describe('historyRows · 应用变更历史', () => {
  const hist = historyRows([
    { action: 'Insert', time: '2026-08-13T08:00:00' },
    { action: 'Update', time: '2026-08-13T09:00:00' },
    { action: 'Delete', time: '2026-08-13T10:00:00' },
    { action: 'Read', time: '2026-08-13T11:00:00' },
    { action: 'View', time: '2026-08-13T07:00:00' },
    { action: 'Publish', time: '2026-08-13T12:00:00' },
  ]);

  it('剔除 Insert/Update/Delete 3 行', () => {
    expect(hist).toHaveLength(3);
  });

  it('按 time 倒序', () => {
    expect(hist.map((r) => r.action).join(',')).toBe('Publish,Read,View');
  });

  it('空输入 → 空数组', () => {
    expect(historyRows([])).toHaveLength(0);
  });
});

describe('configKv / configText · 配置键值', () => {
  it('空 key 行剔除', () => {
    const cfg = configKv([
      { key: 'enable', value: true },
      { field: 'port', content: '6681' },
      { name: 'timeout', value: 30 },
      { key: '  ', value: 'blank' },
      { key: 'empty', value: null },
    ]);
    expect(cfg).toHaveLength(4);
  });

  it('key/value 直取', () => {
    const cfg = configKv([{ key: 'enable', value: true }]);
    expect(cfg[0].key).toBe('enable');
    expect(cfg[0].value).toBe('true');
  });

  it('field/content 回退', () => {
    const cfg = configKv([{ field: 'port', content: '6681' }]);
    expect(cfg[0].key).toBe('port');
    expect(cfg[0].value).toBe('6681');
  });

  it('数字转字符串', () => {
    const cfg = configKv([{ name: 'timeout', value: 30 }]);
    expect(cfg[0].key).toBe('timeout');
    expect(cfg[0].value).toBe('30');
  });

  it('null 值 → 空串', () => {
    const cfg = configKv([{ key: 'empty', value: null }]);
    expect(cfg[0].key).toBe('empty');
    expect(cfg[0].value).toBe('');
  });

  it('空 → 空串', () => {
    expect(configText([])).toBe('');
  });

  it('key=value 每行一条', () => {
    expect(configText([{ key: 'a', value: 1 }, { key: 'b', value: 'x' }])).toBe('a=1\nb=x');
  });

  it('content 回退', () => {
    expect(configText([{ key: 'c', content: 'v' }])).toBe('c=v');
  });
});

describe('pipelineSteps · 发布流水线阶段', () => {
  it('四步顺序', () => {
    expect(pipelineSteps({ status: 'Success' }).map((s) => s.label).join(',')).toBe('构建,上传,分发,部署');
  });

  it('Success → 全 done', () => {
    expect(pipelineSteps({ status: 'Success' }).every((s) => s.state === 'done')).toBe(true);
  });

  it('Pending → 构建 run 其余 wait', () => {
    const steps = pipelineSteps({ status: 'Pending' });
    expect(steps[0].state).toBe('run');
    expect(steps.slice(1).every((s) => s.state === 'wait')).toBe(true);
  });

  it('UploadSucceeded → 上传 run', () => {
    const steps = pipelineSteps({ status: 'UploadSucceeded' });
    expect(steps[0].state).toBe('done');
    expect(steps[1].state).toBe('run');
    expect(steps[2].state).toBe('wait');
    expect(steps[3].state).toBe('wait');
  });

  it('Deploying → 分发 run', () => {
    const steps = pipelineSteps({ status: 'Deploying' });
    expect(steps[0].state).toBe('done');
    expect(steps[1].state).toBe('done');
    expect(steps[2].state).toBe('run');
    expect(steps[3].state).toBe('wait');
  });

  it('Failed 仅构建开始 → 上传 run', () => {
    const steps = pipelineSteps({ status: 'Failed', buildStartedTime: '2026-08-13T08:00:00' });
    expect(steps[0].state).toBe('done');
    expect(steps[1].state).toBe('run');
  });

  it('Failed 构建完成 → 分发 run', () => {
    const steps = pipelineSteps({
      status: 'Failed',
      buildStartedTime: '2026-08-13T08:00:00',
      buildFinishedTime: '2026-08-13T08:05:00',
    });
    expect(steps[0].state).toBe('done');
    expect(steps[1].state).toBe('done');
    expect(steps[2].state).toBe('run');
  });

  it('Failed 无时间 → 构建 run', () => {
    const steps = pipelineSteps({ status: 'Failed' });
    expect(steps[0].state).toBe('run');
    expect(steps[1].state).toBe('wait');
  });

  it('未知状态（normalize 为空）→ 全 wait，不误标进行中', () => {
    expect(pipelineSteps({ status: 'Whatever' }).every((s) => s.state === 'wait')).toBe(true);
  });
});

describe('normalizeRunStatus · 运行状态归一（后端数字枚举 / 字符串双方言）', () => {
  it.each([
    [4, 'Success'],
    [0, 'Pending'],
    [1, 'Building'],
    [2, 'UploadSucceeded'],
    [3, 'Deploying'],
    [5, 'Failed'],
    [6, 'Cancelled'],
  ])('数字 %i → %s', (input, expected) => {
    expect(normalizeRunStatus(input)).toBe(expected);
  });

  it('字符串数字 "4" → Success', () => {
    expect(normalizeRunStatus('4')).toBe('Success');
  });

  it('原样字符串透传', () => {
    expect(normalizeRunStatus('Success')).toBe('Success');
  });

  it('带空白字符串归一', () => {
    expect(normalizeRunStatus('Success '.trim())).toBe('Success');
  });

  it('空 → 空串', () => {
    expect(normalizeRunStatus(undefined)).toBe('');
  });

  it('未知数字 → 空串', () => {
    expect(normalizeRunStatus(99)).toBe('');
  });

  it('数字 status=4 → 四步全 done（码表直通）', () => {
    // 后端返回数字枚举，运行时入参类型是 string，故显式转字符串以匹配模型签名
    expect(pipelineSteps({ status: String(4) }).every((s) => s.state === 'done')).toBe(true);
  });

  it('数字 status=1 → 构建 run', () => {
    expect(pipelineSteps({ status: String(1) })[0].state).toBe('run');
  });

  it('runStatusStyle(4) → 成功标签（数字入参）', () => {
    expect(runStatusStyle(4).label).toBe('成功');
  });

  it('runStatusStyle("1") → 编译中标签', () => {
    expect(runStatusStyle('1').label).toBe('编译中');
  });
});

describe('servicesGroups · 服务提供/消费分组', () => {
  const srv = servicesGroups([
    { serviceName: 'svc-a' },
    { serviceName: 'svc-b', direction: 'consume' },
    { serviceName: 'svc-c', scope: 'CONSUMER' },
    { serviceName: 'svc-d', direction: '消费' },
    { serviceName: 'svc-e', scope: 'internal' },
  ]);

  it('默认提供 2 行', () => {
    expect(srv.provide).toHaveLength(2);
  });

  it('消费 3 行', () => {
    expect(srv.consume).toHaveLength(3);
  });

  it('consume/consumer/消费 均识别', () => {
    expect(srv.consume.every((r) => ['svc-b', 'svc-c', 'svc-d'].includes(r.serviceName ?? ''))).toBe(true);
  });

  it('空输入 → 双空', () => {
    const empty = servicesGroups([]);
    expect(empty.provide).toHaveLength(0);
    expect(empty.consume).toHaveLength(0);
  });
});
