import GatewayTab from './GatewayTab.vue';
import GatewayRouteDrawer from './GatewayRouteDrawer.vue';
import { emptyRouteForm } from './gatewayRoute.logic.ts';
import type { GatewayRouteRow } from './mesh.logic.ts';
import type { CertRow } from './gatewayCert.logic.ts';
import type { NodeRow } from './gatewayNode.logic.ts';

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

const CLUSTERS = [
  { id: 11, name: 'DeviceService' },
  { id: 12, name: 'OrderService' },
];

/** 证书区块数据（CARD-GW-04）：路由 TLS 列与证书行同源对照（iot 有证书 / spa 无） */
const CERT_ROWS: CertRow[] = [
  {
    id: 31, domain: 'iot.example.com', enable: true,
    pfxFile: '/data/certs/iot.example.com.pfx', crtFile: '/data/certs/iot.example.com.crt',
    keyFile: '/data/certs/iot.example.com.key', pemFile: '', notAfterMs: Date.parse('2026-12-31T00:00:00Z'),
  },
  {
    id: 32, domain: 'api.example.com', enable: false,
    pfxFile: '', crtFile: '/data/certs/api.example.com.crt', keyFile: '', pemFile: '/data/certs/api.example.com.pem',
    notAfterMs: 0,
  },
];

/** 节点区块数据（CARD-GW-02）：ClusterName 由 buildClusterMap 归一；含 name 空回退地址 / 未归属集群样例 */
const NODE_ROWS: NodeRow[] = [
  {
    id: 41, name: '', address: '10.0.0.11:8080', clusterId: 11, clusterName: 'DeviceService',
    enable: true, isHealthy: true, weight: 1, maxFails: 3, failTimeout: 60,
  },
  {
    id: 42, name: 'api-node', address: '10.0.0.12:8080', clusterId: 12, clusterName: 'OrderService',
    enable: false, isHealthy: false, weight: 2, maxFails: 5, failTimeout: 30,
  },
];

export const stories = [
  { id: 'GatewayTab/Data', component: GatewayTab, props: { rows: WITH_DATA, clusters: CLUSTERS, certRows: CERT_ROWS, nodeRows: NODE_ROWS } },
  { id: 'GatewayTab/Empty', component: GatewayTab, props: { rows: [] } },
  { id: 'GatewayTab/Loading', component: GatewayTab, props: { rows: [], loading: true } },
  { id: 'GatewayTab/Failed', component: GatewayTab, props: { rows: [], error: 'Request failed with status 500' } },
  { id: 'GatewayTab/CreateDrawer', component: GatewayRouteDrawer, props: { open: true, form: emptyRouteForm(), clusters: CLUSTERS, projects: [{ id: 3, name: '演示项目' }] } },
];
