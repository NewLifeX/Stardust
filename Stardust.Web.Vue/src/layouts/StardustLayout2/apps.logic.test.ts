import { describe, expect, it } from 'vitest';
import {
  ALL_CATEGORY,
  NO_CATEGORY,
  avatarTone,
  buildStatusByApp,
  categoryOf,
  dotToneOf,
  filterByCategory,
  formatErrorRate,
  isErrorRateHigh,
  listCategories,
  rowSubtitle,
  sparklinePath,
  trendPointsByApp,
} from './apps.logic';

const metric = (errorRate: number) => ({ qps: 1, errorRate, p99: 10 });

describe('categoryOf / listCategories / filterByCategory', () => {
  it('分类为空或只有空白时归入「未分类」', () => {
    expect(categoryOf({ category: '' })).toBe(NO_CATEGORY);
    expect(categoryOf({ category: '  ' })).toBe(NO_CATEGORY);
    expect(categoryOf({})).toBe(NO_CATEGORY);
    expect(categoryOf({ category: ' 交易 ' })).toBe('交易');
  });

  it('chip 列表以「全部」开头，其余去重并保持首次出现的顺序', () => {
    const rows = [{ category: 'IoT 平台' }, { category: '交易' }, { category: 'IoT 平台' }, {}];
    expect(listCategories(rows)).toEqual([ALL_CATEGORY, 'IoT 平台', '交易', NO_CATEGORY]);
  });

  it('没有任何应用时只剩「全部」', () => {
    expect(listCategories([])).toEqual([ALL_CATEGORY]);
  });

  it('「全部」原样返回，具体分类只留匹配的行', () => {
    const rows = [{ category: '交易', n: 1 }, { category: '网关', n: 2 }, { n: 3 }];
    expect(filterByCategory(rows, ALL_CATEGORY)).toBe(rows);
    expect(filterByCategory(rows, '交易')).toEqual([rows[0]]);
    expect(filterByCategory(rows, NO_CATEGORY)).toEqual([rows[2]]);
  });
});

describe('rowSubtitle', () => {
  it('拼成「分类 · 版本 · N 实例」', () => {
    expect(rowSubtitle({ category: '交易', version: 'v1.8.0', instances: 4 })).toBe('交易 · v1.8.0 · 4 实例');
  });

  it('缺分类或版本时用占位词，0 实例照常显示', () => {
    expect(rowSubtitle({ version: '', instances: 0 })).toBe('未分类 · 无版本 · 0 实例');
  });
});

describe('formatErrorRate / isErrorRateHigh', () => {
  it('错误率是比例，显示时乘 100 并最多保留两位小数', () => {
    expect(formatErrorRate(metric(0.0031))).toBe('0.31%');
    expect(formatErrorRate(metric(0.026))).toBe('2.6%');
    expect(formatErrorRate(metric(0))).toBe('0%');
  });

  it('没有统计或值非法时显示「—」，不显示 0%', () => {
    expect(formatErrorRate(null)).toBe('—');
    expect(formatErrorRate(undefined)).toBe('—');
    expect(formatErrorRate(metric(Number.NaN))).toBe('—');
  });

  it('高于 1% 才算偏高，恰好 1% 不算（与原型 err > 1 一致）', () => {
    expect(isErrorRateHigh(metric(0.026))).toBe(true);
    expect(isErrorRateHigh(metric(0.01))).toBe(false);
    expect(isErrorRateHigh(metric(0.0031))).toBe(false);
    expect(isErrorRateHigh(null)).toBe(false);
  });
});

describe('dotToneOf', () => {
  it('运行绿、离线空心、异常与发布失败红、部署中黄', () => {
    expect(dotToneOf('running')).toBe('ok');
    expect(dotToneOf('offline')).toBe('off');
    expect(dotToneOf('error')).toBe('err');
    expect(dotToneOf('publishFailed')).toBe('err');
    expect(dotToneOf('deploying')).toBe('warn');
    expect(dotToneOf('maintenance')).toBe('warn');
  });

  it('未知状态按离线处理，不显示成健康', () => {
    expect(dotToneOf('unknown')).toBe('off');
  });
});

describe('avatarTone', () => {
  it('同名稳定同色', () => {
    expect(avatarTone('OrderService')).toBe(avatarTone('OrderService'));
  });

  it('只用 --el-color-* 令牌，且永远不是 danger', () => {
    const names = ['IotWeb', 'DeviceService', 'OrderService', 'PayService', 'UserService', 'MqttBroker', 'StarGateway', '订单服务', ''];
    for (const n of names) {
      const t = avatarTone(n);
      expect(t).toMatch(/^var\(--el-color-(primary|primary-light-3|success|warning|info)\)$/);
    }
  });

  it('不同应用名不会全落在同一个颜色上', () => {
    const tones = new Set(['IotWeb', 'DeviceService', 'OrderService', 'PayService', 'UserService', 'MqttBroker', 'StarGateway', 'ReportJob'].map(avatarTone));
    expect(tones.size).toBeGreaterThan(1);
  });
});

describe('buildStatusByApp', () => {
  // 部署集映射：deployId=100 → appId=1，deployId=200 → appId=2
  const deployMap = new Map([
    [100, { appId: 1, appName: 'AppA' }],
    [200, { appId: 2, appName: 'AppB' }],
  ]);
  // 流水线映射：pipelineId=10 → deployId=100，pipelineId=20 → deployId=200
  const pipelineMap = new Map([
    [10, 100],
    [20, 200],
  ]);

  it('构建中（Pending/Building/Deploying）标记为 building', () => {
    const runs = [
      { PipelineId: 10, Status: 'Building', UpdateTime: '2026-10-30 10:00:00' },
      { PipelineId: 20, Status: 3, UpdateTime: '2026-10-30 11:00:00' }, // Deploying=3
    ];
    const result = buildStatusByApp(runs, pipelineMap, deployMap);
    expect(result.get(1)).toBe('building');
    expect(result.get(2)).toBe('building');
  });

  it('失败（Failed）标记为 failed', () => {
    const runs = [
      { PipelineId: 10, Status: 5, UpdateTime: '2026-10-30 10:00:00' }, // Failed=5
    ];
    const result = buildStatusByApp(runs, pipelineMap, deployMap);
    expect(result.get(1)).toBe('failed');
  });

  it('成功/取消不显示（不出现在 map 里）', () => {
    const runs = [
      { PipelineId: 10, Status: 4, UpdateTime: '2026-10-30 10:00:00' }, // Success=4
      { PipelineId: 20, Status: 'Cancelled', UpdateTime: '2026-10-30 11:00:00' },
    ];
    const result = buildStatusByApp(runs, pipelineMap, deployMap);
    expect(result.has(1)).toBe(false);
    expect(result.has(2)).toBe(false);
  });

  it('同一应用多条 run 取时间最新的状态', () => {
    const runs = [
      { PipelineId: 10, Status: 'Failed', UpdateTime: '2026-10-30 08:00:00' },
      { PipelineId: 10, Status: 'Building', UpdateTime: '2026-10-30 12:00:00' },
    ];
    const result = buildStatusByApp(runs, pipelineMap, deployMap);
    expect(result.get(1)).toBe('building'); // 最新的是 Building，不是 Failed
  });

  it('流水线不在本项目映射里的行被丢弃', () => {
    const runs = [
      { PipelineId: 999, Status: 'Building', UpdateTime: '2026-10-30 10:00:00' },
    ];
    const result = buildStatusByApp(runs, pipelineMap, deployMap);
    expect(result.size).toBe(0);
  });

  it('空输入返回空 map', () => {
    expect(buildStatusByApp([], pipelineMap, deployMap).size).toBe(0);
  });
});

describe('trendPointsByApp', () => {
  it('按 appId 分组、按日期升序、取最近 limit 天', () => {
    const rows = [
      { AppId: 1, StatDate: '2026-10-25', Total: 100 },
      { AppId: 1, StatDate: '2026-10-26', Total: 200 },
      { AppId: 1, StatDate: '2026-10-27', Total: 150 },
      { AppId: 2, StatDate: '2026-10-27', Total: 50 },
    ];
    const result = trendPointsByApp(rows, 14);
    expect(result.get(1)).toEqual([
      { date: '2026-10-25', total: 100 },
      { date: '2026-10-26', total: 200 },
      { date: '2026-10-27', total: 150 },
    ]);
    expect(result.get(2)).toEqual([{ date: '2026-10-27', total: 50 }]);
  });

  it('camelCase 字段也能识别', () => {
    const rows = [{ appId: 3, statDate: '2026-10-27', total: 77 }];
    const result = trendPointsByApp(rows);
    expect(result.get(3)).toEqual([{ date: '2026-10-27', total: 77 }]);
  });

  it('limit 截断：只保留最近 N 条', () => {
    const rows = [
      { AppId: 1, StatDate: '2026-10-20', Total: 10 },
      { AppId: 1, StatDate: '2026-10-25', Total: 20 },
      { AppId: 1, StatDate: '2026-10-27', Total: 30 },
    ];
    const result = trendPointsByApp(rows, 2);
    expect(result.get(1)).toEqual([
      { date: '2026-10-25', total: 20 },
      { date: '2026-10-27', total: 30 },
    ]);
  });

  it('无 appId 或无日期的行被跳过', () => {
    const rows = [
      { StatDate: '2026-10-27', Total: 100 },
      { AppId: 0, StatDate: '2026-10-27', Total: 200 },
      { AppId: 1 },
    ];
    expect(trendPointsByApp(rows).size).toBe(0);
  });
});

describe('sparklinePath', () => {
  it('少于 2 个点返回空串', () => {
    expect(sparklinePath([])).toBe('');
    expect(sparklinePath([5])).toBe('');
  });

  it('生成 M/L 折线 path，首点 M 后续 L', () => {
    const path = sparklinePath([1, 3, 2], 60, 20);
    expect(path).toMatch(/^M0\.0,/);
    expect(path).toContain('L30.0,');
    expect(path).toContain('L60.0,');
  });

  it('所有值相同时不除以零，线在中间', () => {
    const path = sparklinePath([5, 5, 5], 60, 20);
    // y 都应在 10（中间）附近
    const ys = path.match(/y=([\d.]+)/gi);
    expect(ys).toBeNull(); // path 用逗号分隔，不是 y= 属性
    expect(path).toContain(',10.0');
  });

  it('值越大 y 越小（上方），值越小 y 越大（下方）', () => {
    const path = sparklinePath([0, 100], 60, 20);
    // 第一个点 y=18（底部），第二个 y=2（顶部）
    expect(path).toBe('M0.0,18.0 L60.0,2.0');
  });
});
