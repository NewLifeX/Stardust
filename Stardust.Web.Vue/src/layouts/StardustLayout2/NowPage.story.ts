import NowPage from './NowPage.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { stardustContext } from '@/stores/stardustContext';

// gallery 里 localStorage 为空、上下文未就绪：useContextReload 的 canLoad 会拦掉首轮取数，
// 页面会永远停在 loading。story 直接把上下文置为就绪（projectId 空 = 不过滤，桩请求也不看 params）。
stardustContext.ready = true;

// ---------- 桩数据（仅 story/CT 用，不进生产） ----------

const ALARMS = [
  {
    Id: 11, Name: 'OrderService 错误率过高', Category: '错误数过高', Action: 'OrderService',
    Duration: 300, StartTime: '2026-10-30 09:00:00', Times: 3,
    Content: '最近 5 分钟错误率 12%（阈值 1%）\n样本：POST /api/order/pay 超时（10s）',
  },
  {
    Id: 10, Name: 'node-b 已下线', Category: '节点下线', Action: 'node-b',
    Duration: 600, StartTime: '2026-10-30 08:50:00', Times: 1,
    Content: '连续 6 次心跳丢失，同机房其余节点心跳正常。',
  },
];

const NODES = [
  { ID: 1, Name: 'node-a', IP: '10.0.0.11' },
  { ID: 2, Name: 'node-b', IP: '10.0.0.12' },
  { ID: 3, Name: 'node-c', IP: '10.0.0.13' },
  { ID: 4, Name: 'node-d', IP: '10.0.0.14' },
];
const NODES_ONLINE = [
  { NodeID: 1, CpuRate: 23 },
  { NodeID: 2, CpuRate: 91 },
  { NodeID: 3, CpuRate: 40 },
  // node-d(ID=4) 无在线会话 → 离线
];

const APP_ROWS = [
  { Id: 101, Name: 'OrderService', DisplayName: '订单服务', Category: '交易', Version: 'v1.8.0', Enable: true },
  { Id: 102, Name: 'PayService', DisplayName: '支付服务', Category: '交易', Version: 'v1.4.2', Enable: true },
  { Id: 202, Name: 'IotWeb', DisplayName: '物联网门户', Category: '平台', Version: 'v0.9.1', Enable: true },
];
const APP_ONLINE = [{ appName: 'OrderService' }, { appName: 'OrderService' }, { appName: 'PayService' }];

// fetchChanges 的六接口：部署集/配置/流水线映射 + 三张历史
const DEPLOY_MAP = [{ Id: 1, AppId: 101, AppName: 'OrderService' }];
const CONFIG_MAP = [{ Id: 9, AppId: 202, Name: 'IotWeb' }];
const DEPLOY_HIST = [
  { Id: 1, DeployId: 1, Action: 'install', Remark: '安装 · node-a', CreateTime: '2026-10-30 09:30:00', Success: true, CreateUserId: 7 },
];
const CONFIG_HIST = [
  { Id: 5, ConfigId: 9, Action: 'Publish', Success: true, CreateTime: '2026-10-30 08:00:00', CreateUserID: 3 },
];

/** 按 URL 返回 canned 响应；fail=true 时全部请求抛错（四段错误横幅） */
function stubReq(opts: { fail?: boolean; alarms?: boolean; empty?: boolean } = {}): Req {
  return async (opt) => {
    if (opts.fail) throw new Error('stub 500');
    const url = String(opt.url ?? '');
    switch (url) {
      case '/Monitors/AlarmRecord':
        return { data: opts.alarms === false ? [] : ALARMS };
      case '/Nodes/Node':
        return { data: opts.empty ? [] : NODES };
      case '/Nodes/NodeOnline':
        return { data: opts.empty ? [] : NODES_ONLINE };
      case '/Registry/App':
        return { data: opts.empty ? [] : APP_ROWS };
      case '/Registry/AppOnline':
        return { data: opts.empty ? [] : APP_ONLINE };
      case '/Registry/AppService':
        return { data: [] };
      case '/Monitors/AppMinuteStat':
        return { data: [] };
      case '/Deployment/AppDeploy':
        return { data: opts.empty ? [] : DEPLOY_MAP };
      case '/Configs/AppConfig':
        return { data: opts.empty ? [] : CONFIG_MAP };
      case '/Deployment/AppPipeline':
        return { data: [] };
      case '/Deployment/AppDeployHistory':
        return { data: opts.empty ? [] : DEPLOY_HIST };
      case '/Deployment/AppPipelineRun':
        return { data: [] };
      case '/Configs/ConfigHistory':
        return { data: opts.empty ? [] : CONFIG_HIST };
      default:
        return { data: [] };
    }
  };
}

export const stories = [
  // 有事态：2 条告警（1 条 danger 节点下线 + 1 条 warn 错误率，首条默认展开），
  // 舰队四格 ok/high/ok/off，应用 3 行，变更 2 行（D 安装 + C 发布配置）
  { id: 'NowPage/Busy', component: NowPage, props: { deps: { request: stubReq() } } },

  // 一切正常：无告警 → Hero「✓ 一切正常」，左栏「全部处理完毕」，舰队全绿
  { id: 'NowPage/Clear', component: NowPage, props: { deps: { request: stubReq({ alarms: false }) } } },

  // 各段空态：没有节点/应用/变更（告警为空，左栏显示无告警空态）
  { id: 'NowPage/Empty', component: NowPage, props: { deps: { request: stubReq({ alarms: false, empty: true }) } } },

  // 四段全失败：顶部与各段可见错误横幅 + 重试按钮，不是空态
  { id: 'NowPage/Failed', component: NowPage, props: { deps: { request: stubReq({ fail: true }) } } },

  // 点击录制：节点格 / 应用行 / 变更行发出的 go 路径追加进 window.__nowGo（CT 点击用例读它）
  {
    id: 'NowPage/SelectRecorder',
    component: NowPage,
    props: {
      deps: { request: stubReq() },
      onGo: (path: string) => {
        const w = window as unknown as { __nowGo?: string[] };
        (w.__nowGo ??= []).push(path);
      },
    },
  },
];
