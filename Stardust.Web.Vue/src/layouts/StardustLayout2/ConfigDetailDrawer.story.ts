import ConfigDetailDrawer from './ConfigDetailDrawer.vue';
import type { ConfigDetail, ConfigPreview } from './configCenter.logic.ts';

const FULL: ConfigDetail = {
  configId: 9,
  appId: 101,
  appName: '订单服务',
  items: [
    { id: 1, key: 'App.Trace.Enable', value: 'true', newValue: 'true', newStatus: '' },
    { id: 2, key: 'Log.Level', value: 'Info', newValue: 'Warn', newStatus: '' },
    { id: 3, key: 'Timeout.Http', value: '2000', newValue: '3000', newStatus: '' },
    { id: 4, key: 'Old.Key', value: '1', newValue: '', newStatus: '[[Deleted]]' },
  ],
  history: [
    { id: 21, action: 'Publish', success: true, version: 2, time: '2026-10-28 09:30' },
    { id: 20, action: 'Publish', success: true, version: 1, time: '2026-10-20 09:30' },
    { id: 19, action: 'Insert', success: true, version: 0, time: '2026-10-19 18:00' },
  ],
  pendingCount: 3,
};

const EMPTY: ConfigDetail = {
  configId: 9,
  appId: 101,
  appName: '订单服务',
  items: [],
  history: [],
  pendingCount: 0,
};

/** 预览替身（C19）：/config/getall 实拉结果的形态 */
const PREVIEW: ConfigPreview = {
  appId: 101,
  appName: '订单服务',
  version: 1,
  scope: '',
  nextVersion: 1,
  nextPublish: '',
  updateTime: '2026-10-28 10:00',
  configs: { 'App.Trace.Enable': 'true', 'Log.Level': 'Info', 'Timeout.Http': '3000' },
};

/** 预览空：拉到 ConfigInfo 但没有键 */
const PREVIEW_EMPTY: ConfigPreview = { ...PREVIEW, configs: {} };

export const stories = [
  // 完整：待发布差异表（3 项变化）+ KV 表 + 版本历史 + 底部跳转按钮
  { id: 'ConfigDetailDrawer/Full', component: ConfigDetailDrawer, props: { open: true, detail: FULL } },

  // 空配置：没有配置项、没有历史，KV 表显示空态
  { id: 'ConfigDetailDrawer/Empty', component: ConfigDetailDrawer, props: { open: true, detail: EMPTY } },

  // 加载态
  { id: 'ConfigDetailDrawer/Loading', component: ConfigDetailDrawer, props: { open: true, detail: null, loading: true } },

  // 失败态
  { id: 'ConfigDetailDrawer/Failed', component: ConfigDetailDrawer, props: { open: true, detail: null, error: 'Request failed' } },

  // 跳转录制：点「前往编辑」把 appId 写进 window.__configGoto
  {
    id: 'ConfigDetailDrawer/GotoRecorder',
    component: ConfigDetailDrawer,
    props: {
      open: true,
      detail: FULL,
      onGotoAppConfig: (appId: number) => {
        const w = window as unknown as { __configGoto?: number[] };
        (w.__configGoto ??= []).push(appId);
      },
    },
  },

  // 预览：头部「预览」钮 + 弹层，fetchPreview 替身返回 3 个键（CT 点钮后断言内容）
  { id: 'ConfigDetailDrawer/Preview', component: ConfigDetailDrawer, props: { open: true, detail: FULL, fetchPreview: async () => PREVIEW } },

  // 预览空态：拉到结果但没有键
  { id: 'ConfigDetailDrawer/PreviewEmpty', component: ConfigDetailDrawer, props: { open: true, detail: FULL, fetchPreview: async () => PREVIEW_EMPTY } },

  // 预览失败：fetchPreview 返回 null → 可见失败 + 重试
  { id: 'ConfigDetailDrawer/PreviewFailed', component: ConfigDetailDrawer, props: { open: true, detail: FULL, fetchPreview: async () => null } },
];
