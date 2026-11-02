import ConfigCenterList from './ConfigCenterList.vue';
import type { ConfigCenterRow } from './configCenter.logic.ts';

const ROWS: ConfigCenterRow[] = [
  {
    configId: 9, appId: 101, appName: '订单服务', configName: 'OrderApi',
    version: 1, nextVersion: 2, enable: true, keyCount: 12, valueSummary: '',
    lastPublishText: '2026-10-28 10:00', pending: true,
  },
  {
    configId: 8, appId: 202, appName: 'IotWeb', configName: 'IotWeb',
    version: 3, nextVersion: 3, enable: true, keyCount: 5, valueSummary: '',
    lastPublishText: '2026-10-27 09:30', pending: false,
  },
  {
    configId: 7, appId: 303, appName: '支付网关', configName: 'PayGw',
    version: 5, nextVersion: 5, enable: true, keyCount: 0, valueSummary: '',
    lastPublishText: '2026-10-20 14:05', pending: false,
  },
];

export const stories = [
  // 有数据：待发布行排前（橙色 Tag），已发布绿色 Tag，最近更新时间
  { id: 'ConfigCenterList/Mixed', component: ConfigCenterList, props: { rows: ROWS } },

  // 空态：还没有应用配置
  { id: 'ConfigCenterList/Empty', component: ConfigCenterList, props: { rows: [] } },

  // 加载态：spinner
  { id: 'ConfigCenterList/Loading', component: ConfigCenterList, props: { rows: [], loading: true } },

  // 失败态：可见错误 + 重试
  { id: 'ConfigCenterList/Failed', component: ConfigCenterList, props: { rows: [], error: 'Request failed with status 500' } },

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
