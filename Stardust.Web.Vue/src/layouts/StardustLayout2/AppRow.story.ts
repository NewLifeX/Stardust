import AppRow from '../../../apps/stardust/src/views/Console/AppRow.vue';
import type { AppAggRow } from '../../../apps/stardust/src/views/Stardust/appsModel';

// 构造最小可用的 AppAggRow mock
function makeRow(overrides: Partial<AppAggRow> = {}): AppAggRow {
  return {
    id: 1,
    name: 'OrderService',
    displayName: '订单服务',
    category: '交易',
    version: 'v1.8.0',
    enable: true,
    status: 'running',
    instances: 3,
    services: 2,
    metric: { qps: 12, errorRate: 0.003, p99: 45 },
    trend: [],
    buildStatus: null,
    ...overrides,
  };
}

export const stories = [
  // 基线：无构建小标、无趋势图，只有错误率与状态点
  { id: 'AppRow/Normal', component: AppRow, props: { row: makeRow() } },

  // 构建中：标题旁蓝色「构建中」小标
  {
    id: 'AppRow/Building',
    component: AppRow,
    props: {
      row: makeRow({
        name: 'IotWeb',
        displayName: 'IoT 平台',
        status: 'deploying',
        buildStatus: 'building',
      }),
    },
  },

  // 构建失败：标题旁红色「失败」小标
  {
    id: 'AppRow/Failed',
    component: AppRow,
    props: {
      row: makeRow({
        name: 'PayService',
        displayName: '支付服务',
        status: 'publishFailed',
        buildStatus: 'failed',
      }),
    },
  },

  // 趋势小图：右侧 60×20 sparkline（近 7 天调用次数上升）
  {
    id: 'AppRow/Trend',
    component: AppRow,
    props: {
      row: makeRow({
        name: 'ReportJob',
        displayName: '报表任务',
        trend: [
          { date: '2026-10-24', total: 120 },
          { date: '2026-10-25', total: 180 },
          { date: '2026-10-26', total: 90 },
          { date: '2026-10-27', total: 240 },
          { date: '2026-10-28', total: 310 },
          { date: '2026-10-29', total: 280 },
          { date: '2026-10-30', total: 420 },
        ],
      }),
    },
  },

  // 空数据：离线应用，无实例、无监控、无趋势
  {
    id: 'AppRow/Empty',
    component: AppRow,
    props: {
      row: makeRow({
        id: 32,
        name: 'Lux.IOC.Api',
        displayName: 'Lux.IOC.Api',
        category: '',
        version: '',
        enable: false,
        status: 'offline',
        instances: 0,
        services: 0,
        metric: null,
        trend: [],
        buildStatus: null,
      }),
    },
  },

  // 编辑入口：hover 操作条「编辑」按钮发 edit 事件，进 window.__appRowEvents（CT 断言用）
  {
    id: 'AppRow/EditEmit',
    component: AppRow,
    props: {
      row: makeRow(),
      selectable: true,
      onEdit: (row: AppAggRow) => (window as unknown as { __appRowEvents?: string[] }).__appRowEvents?.push(`edit:${row.name}`),
    },
  },
];
