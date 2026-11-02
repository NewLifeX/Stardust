import ServiceDetailDrawer from './ServiceDetailDrawer.vue';
import type { ServiceDetail, ServiceMemberRow } from './mesh.logic.ts';

const providers: ServiceMemberRow[] = [
  { id: 1, client: 'StarWeb', address: 'http://10.0.0.1:6680', enable: true, healthy: true, version: 'v2.3', updateTimeText: '2026-11-03 09:50' },
  { id: 2, client: 'IocApi', address: 'http://10.0.0.2:6680', enable: true, healthy: false, version: 'v2.1', updateTimeText: '2026-11-03 09:40' },
];

const consumers: ServiceMemberRow[] = [
  { id: 3, client: 'MindHub', address: 'http://10.0.0.9:7000', enable: true, updateTimeText: '2026-11-03 08:00' },
];

const detail: ServiceDetail = {
  id: 7,
  name: 'Star.Web',
  service: {
    displayName: '星尘主站',
    category: 'Web',
    enable: true,
    extranet: false,
    singleton: false,
    useScope: false,
    address: 'tcp://10.0.0.1:6680',
    healthCheck: true,
    remark: '',
  },
  providers,
  consumers,
};

export const stories = [
  // F13 详情：编辑表单 + 提供者 + 消费者
  {
    id: 'ServiceDetailDrawer/Default',
    component: ServiceDetailDrawer,
    props: {
      open: true,
      loading: false,
      failed: false,
      service: detail.service,
      providers,
      consumers,
      serviceName: 'Star.Web',
      busy: false,
    },
  },

  // F13 加载中
  {
    id: 'ServiceDetailDrawer/Loading',
    component: ServiceDetailDrawer,
    props: { open: true, loading: true, failed: false, service: null, providers: [], consumers: [], serviceName: 'Star.Web', busy: false },
  },

  // F13 加载失败：重试入口
  {
    id: 'ServiceDetailDrawer/Failed',
    component: ServiceDetailDrawer,
    props: { open: true, loading: false, failed: true, service: null, providers: [], consumers: [], serviceName: 'Star.Web', busy: false },
  },

  // F13 保存中（AGENTS.md §11）
  {
    id: 'ServiceDetailDrawer/Busy',
    component: ServiceDetailDrawer,
    props: {
      open: true,
      loading: false,
      failed: false,
      service: detail.service,
      providers,
      consumers,
      serviceName: 'Star.Web',
      busy: true,
    },
  },
];
