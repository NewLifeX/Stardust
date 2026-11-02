import AppDepsPanel from './AppDepsPanel.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/**
 * F15 工作台配置页签「上下游依赖」面板。
 * 桩：/Registry/App 出应用名映射；/Configs/AppQuote 按 appId / targetAppId 分流双向数据。
 */
function stubReq(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    const params = (opt.params ?? {}) as Record<string, unknown>;
    if (url === '/Registry/App') {
      return { data: [
        { id: 7, name: 'OrderService', displayName: '订单服务' },
        { id: 9, name: 'StarWeb', displayName: '' },
        { id: 11, name: 'MindHub', displayName: '智能工单' },
      ] };
    }
    if (url === '/Configs/AppQuote') {
      const all = [
        { Id: 2, AppId: 7, TargetAppId: 9, Kind: 1, Enable: true },
        { Id: 3, AppId: 11, TargetAppId: 7, Kind: 0, Enable: false },
      ];
      const rows = params.appId ? all.filter((r) => r.AppId === params.appId) : all.filter((r) => r.TargetAppId === params.targetAppId);
      return { data: rows };
    }
    return { data: [] };
  };
}

export const stories = [
  // 测试状态：appId=7 —— 上游 StarWeb（启用）；下游 MindHub（停用）
  { id: 'AppDepsPanel/Both', component: AppDepsPanel, props: { appId: 7, deps: { request: stubReq() } } },

  // 测试状态：appId=77 没有任何依赖 → 两侧空态
  { id: 'AppDepsPanel/Empty', component: AppDepsPanel, props: { appId: 77, deps: { request: stubReq() } } },
];
