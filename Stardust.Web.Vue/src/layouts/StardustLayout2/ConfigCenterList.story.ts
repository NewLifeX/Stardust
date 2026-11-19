import ConfigCenterList from './ConfigCenterList.vue';
import type { ConfigCenterRow } from './configCenter.logic.ts';

const ROWS: ConfigCenterRow[] = [
  {
    configId: 9, appId: 101, appName: '订单服务', configName: 'OrderApi',
    version: 1, nextVersion: 2, enable: true, keyCount: 12, valueSummary: '',
    lastPublishText: '2026-10-28 10:00', lastPublishMs: Date.parse('2026-10-28T10:00:00'),
    category: '公共', pending: true,
  },
  {
    configId: 8, appId: 202, appName: 'IotWeb', configName: 'IotWeb',
    version: 3, nextVersion: 3, enable: true, keyCount: 5, valueSummary: '',
    lastPublishText: '2026-10-27 09:30', lastPublishMs: Date.parse('2026-10-27T09:30:00'),
    category: '物联网', pending: false,
  },
  {
    configId: 7, appId: 303, appName: '支付网关', configName: 'PayGw',
    version: 5, nextVersion: 5, enable: true, keyCount: 0, valueSummary: '',
    lastPublishText: '2026-10-20 14:05', lastPublishMs: Date.parse('2026-10-20T14:05:00'),
    category: '公共', pending: false,
  },
  // 全局配置（AppId=0）：只出现在列表，不进「应用」下拉（0 = 全部应用哨兵值，DEF-1）
  {
    configId: 6, appId: 0, appName: 'Common', configName: 'Common',
    version: 1, nextVersion: 1, enable: true, keyCount: 1, valueSummary: '',
    lastPublishText: '2026-10-13 16:31', lastPublishMs: Date.parse('2026-10-13T16:31:00'),
    category: '公共', pending: false,
  },
];

/** 类别选项（宿主传 Categories 接口结果；空则组件降级为行内去重） */
const CATEGORIES = ['公共', '物联网'];

export const stories = [
  // 有数据：待发布行排前（橙色 Tag），已发布绿色 Tag，最近更新时间
  { id: 'ConfigCenterList/Mixed', component: ConfigCenterList, props: { rows: ROWS, categories: CATEGORIES } },

  // 空态：还没有应用配置
  { id: 'ConfigCenterList/Empty', component: ConfigCenterList, props: { rows: [] } },

  // 加载态：spinner
  { id: 'ConfigCenterList/Loading', component: ConfigCenterList, props: { rows: [], loading: true } },

  // 失败态：可见错误 + 重试
  { id: 'ConfigCenterList/Failed', component: ConfigCenterList, props: { rows: [], error: 'Request failed with status 500' } },

  // 筛选工具条：类别下拉（首项「全部类别」）+ 最近更新日期区间（C05）
  { id: 'ConfigCenterList/Filters', component: ConfigCenterList, props: { rows: ROWS, categories: CATEGORIES } },

  // 点击录制：点行把 configId 追加进 window.__configOpen（CT 点击用例读它）
  {
    id: 'ConfigCenterList/OpenRecorder',
    component: ConfigCenterList,
    props: {
      rows: ROWS,
      onOpen: (configId: number) => {
        const w = window as unknown as { __configOpen?: number[] };
        (w.__configOpen ??= []).push(configId);
      },
    },
  },
];
