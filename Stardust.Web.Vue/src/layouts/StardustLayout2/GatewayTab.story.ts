import GatewayTab from './GatewayTab.vue';
import type { GatewayRouteRow } from './mesh.logic.ts';

const WITH_DATA: GatewayRouteRow[] = [
  {
    id: 1, name: 'iot-api', domain: 'iot.example.com', path: '/api/device/**', methods: 'GET,POST',
    clusterId: 11, clusterName: 'DeviceService', enable: true, isStatic: false,
    tls: { present: true, domain: 'iot.example.com', autoRenew: true },
  },
  {
    id: 2, name: 'order', domain: 'api.example.com', path: '/api/order/**', methods: '',
    clusterId: 12, clusterName: 'OrderService', enable: true, isStatic: false,
    tls: { present: true, domain: '*.example.com', autoRenew: false },
  },
  {
    id: 3, name: 'web', domain: 'spa.example.com', path: '/', methods: '',
    clusterId: 0, clusterName: '', enable: true, isStatic: true,
    tls: { present: false, domain: '', autoRenew: false },
  },
];

export const stories = [
  { id: 'GatewayTab/Data', component: GatewayTab, props: { rows: WITH_DATA } },
  { id: 'GatewayTab/Empty', component: GatewayTab, props: { rows: [] } },
  { id: 'GatewayTab/Loading', component: GatewayTab, props: { rows: [], loading: true } },
  { id: 'GatewayTab/Failed', component: GatewayTab, props: { rows: [], error: 'Request failed with status 500' } },
];
