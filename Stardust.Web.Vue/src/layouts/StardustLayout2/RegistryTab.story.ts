import RegistryTab from './RegistryTab.vue';
import type { RegistryRow } from './mesh.logic.ts';

const WITH_DATA: RegistryRow[] = [
  { id: 1, name: '订单服务', serviceName: 'order', category: 'biz', instances: 8, consumers: 3, enable: true, healthCheck: true },
  { id: 2, name: '支付网关', serviceName: 'payment', category: 'pay', instances: 4, consumers: 12, enable: true, healthCheck: false },
  { id: 3, name: '旧版报表', serviceName: 'report-v1', category: 'bi', instances: 0, consumers: 0, enable: false, healthCheck: false },
];

export const stories = [
  { id: 'RegistryTab/Data', component: RegistryTab, props: { rows: WITH_DATA } },
  { id: 'RegistryTab/Empty', component: RegistryTab, props: { rows: [] } },
  { id: 'RegistryTab/Loading', component: RegistryTab, props: { rows: [], loading: true } },
  { id: 'RegistryTab/Failed', component: RegistryTab, props: { rows: [], error: 'Request failed with status 500' } },
];
