// 节点抽屉 9 Tab 纯逻辑单测（Vitest）
import { describe, it, expect } from 'vitest';
import {
  overviewInfo,
  rings,
  historyRows,
  commandRows,
  serviceRows,
  appOnlineRows,
  type AppOnlineRow,
} from './nodes.model';

describe('overviewInfo · 概览键值表', () => {
  const kv = overviewInfo({
    name: 'node-a',
    iP: '10.0.0.1',
    projectName: 'prod',
    oS: 'Linux',
    architecture: 'x64',
    version: 'v2.1',
    lastActive: new Date(Date.now() - 30_000).toISOString(),
  });

  it('六项键值', () => {
    expect(kv).toHaveLength(6);
  });

  it('节点名称', () => {
    expect(kv[0].k).toBe('节点名称');
    expect(kv[0].v).toBe('node-a');
  });

  it('IP 地址优先 iP', () => {
    expect(kv[1].k).toBe('IP 地址');
    expect(kv[1].v).toBe('10.0.0.1');
  });

  it('集群取 projectName', () => {
    expect(kv[2].k).toBe('集群');
    expect(kv[2].v).toBe('prod');
  });

  it('系统拼接 oS+架构', () => {
    expect(kv[3].k).toBe('系统');
    expect(kv[3].v).toBe('Linux x64');
  });

  it('运行时长有值', () => {
    expect(kv[4].k).toBe('运行时长');
    expect(kv[4].v).toMatch(/^(\d+s|\d+m|\d+h \d+m|\d+d)$/);
  });

  it('Agent 版本', () => {
    expect(kv[5].k).toBe('Agent 版本');
    expect(kv[5].v).toBe('v2.1');
  });

  it('缺省值回退', () => {
    const fb = overviewInfo({});
    expect(fb[0].v).toBe('-');
    expect(fb[1].v).toBe('-');
    expect(fb[2].v).toBe('默认集群');
    expect(fb[3].v).toBe('-');
    expect(fb[4].v).toBe('-');
    expect(fb[5].v).toBe('-');
  });

  it('IP 回退 address', () => {
    expect(overviewInfo({ address: '10.0.0.2' })[1].v).toBe('10.0.0.2');
  });

  it('仅有 oS', () => {
    expect(overviewInfo({ oS: 'Linux' })[3].v).toBe('Linux');
  });
});

describe('rings · 三环数据', () => {
  it('三环', () => {
    expect(rings({ cpu: 45 })).toHaveLength(3);
  });

  it('CPU 取值 + success 色', () => {
    const r = rings({ cpu: 45 });
    expect(r[0].label).toBe('CPU');
    expect(r[0].value).toBe(45);
    expect(r[0].color).toBe('var(--el-color-success)');
  });

  it('内存无数据 → null', () => {
    expect(rings({ cpu: 45 })[1].value).toBeNull();
  });

  it('磁盘无数据 → null', () => {
    expect(rings({ cpu: 45 })[2].value).toBeNull();
  });

  it('无 CPU → null', () => {
    expect(rings({})[0].value).toBeNull();
  });

  it('CPU 非 number → null', () => {
    expect(rings({ cpu: '45' as unknown as number })[0].value).toBeNull();
  });
});

describe('historyRows · 节点事件历史', () => {
  it('两条事件', () => {
    expect(historyRows({ lastLogin: '2026-08-13T10:23:00', lastActive: '2026-08-13T09:05:00' })).toHaveLength(2);
  });

  it('登录更晚 → 在前', () => {
    const h = historyRows({ lastLogin: '2026-08-13T10:23:00', lastActive: '2026-08-13T09:05:00' });
    expect(h[0].status).toBe('login');
    expect(h[0].text).toBe('节点最近登录');
    expect(h[0].time).toBe('2026-08-13 10:23');
  });

  it('心跳更早 → 在后', () => {
    const h = historyRows({ lastLogin: '2026-08-13T10:23:00', lastActive: '2026-08-13T09:05:00' });
    expect(h[1].status).toBe('active');
    expect(h[1].text).toBe('节点心跳上报');
    expect(h[1].time).toBe('2026-08-13 09:05');
  });

  it('心跳更晚 → 在前（新→旧排序）', () => {
    const h = historyRows({ lastLogin: '2026-08-13T08:00:00', lastActive: '2026-08-13T11:11:00' });
    expect(h[0].status).toBe('active');
  });

  it('非法时间 → 显示 -', () => {
    const h = historyRows({});
    expect(h[0].status).toBe('login');
    expect(h[0].time).toBe('-');
    expect(h[1].status).toBe('active');
    expect(h[1].time).toBe('-');
  });
});

describe('commandRows · 可下发指令', () => {
  const cmds = commandRows();

  it('三行指令', () => {
    expect(cmds).toHaveLength(3);
  });

  it('重启行', () => {
    expect(cmds[0].role).toBe('restart');
    expect(cmds[0].name).toBe('重启节点服务');
  });

  it('更新行', () => {
    expect(cmds[1].role).toBe('update');
    expect(cmds[1].name).toBe('更新节点 Agent');
  });

  it('采集行', () => {
    expect(cmds[2].role).toBe('collect');
    expect(cmds[2].name).toBe('采集节点指标');
  });
});

describe('serviceRows · 节点服务列表', () => {
  const svcJson = JSON.stringify([
    { name: 'user-service', role: 'provider', port: '8080', instances: 2 },
    { serviceName: 'svc-b', type: 'client', count: 3 },
    { name: 'order-client', role: 'consumer', address: '10.0.0.1:9090' },
  ]);
  const svcs = serviceRows(svcJson);

  it('解析 3 行', () => {
    expect(svcs).toHaveLength(3);
  });

  it('提供在前', () => {
    expect(svcs[0].role).toBe('provider');
    expect(svcs[0].name).toBe('user-service');
  });

  it('meta 拼端口+实例', () => {
    expect(svcs[0].meta).toBe('端口 8080 2 实例');
  });

  it('type=client → 消费组', () => {
    expect(svcs[1].role).toBe('consumer');
    expect(svcs[1].name).toBe('svc-b');
  });

  it('meta 仅实例', () => {
    expect(svcs[1].meta).toBe('3 实例');
  });

  it('消费在后', () => {
    expect(svcs[2].role).toBe('consumer');
    expect(svcs[2].name).toBe('order-client');
  });

  it('meta 仅端口', () => {
    expect(svcs[2].meta).toBe('端口 10.0.0.1:9090');
  });

  it('非法 JSON → 空数组', () => {
    expect(serviceRows('not-json')).toHaveLength(0);
  });

  it('非字符串 → 空数组', () => {
    expect(serviceRows(undefined)).toHaveLength(0);
  });

  it('无名称行剔除', () => {
    expect(serviceRows(JSON.stringify([{ role: 'provider' }]))).toHaveLength(0);
  });
});

describe('appOnlineRows · 节点上的在线应用', () => {
  const online: AppOnlineRow[] = [
    { appName: 'order', nodeName: 'n1' },
    { appName: 'pay', nodeName: 'n2' },
    { appName: 'user', nodeName: 'n1' },
  ];

  it('按 nodeName 过滤', () => {
    const rows = appOnlineRows(online, 'n1');
    expect(rows).toHaveLength(2);
    expect(rows.every((r) => r.nodeName === 'n1')).toBe(true);
  });

  it('无匹配 → 空', () => {
    expect(appOnlineRows(online, 'nope')).toHaveLength(0);
  });

  it('空 nodeName → 空', () => {
    expect(appOnlineRows(online, '')).toHaveLength(0);
  });

  it('空输入 → 空', () => {
    expect(appOnlineRows([], 'n1')).toHaveLength(0);
  });

  it('undefined 输入容错', () => {
    expect(appOnlineRows(undefined as unknown as AppOnlineRow[], 'n1')).toHaveLength(0);
  });
});
