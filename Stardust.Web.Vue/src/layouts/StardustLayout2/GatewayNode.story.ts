import GatewayNodeBlock from './GatewayNodeBlock.vue';
import GatewayNodeDrawer from './GatewayNodeDrawer.vue';
import { emptyNodeForm } from './gatewayNode.logic.ts';
import type { GatewayNodeForm, NodeRow } from './gatewayNode.logic.ts';

const CLUSTERS = [
  { id: 2, name: 'iot-cluster' },
  { id: 5, name: 'api-cluster' },
];

const WITH_DATA: NodeRow[] = [
  {
    id: 41, name: '', address: '10.0.0.11:8080', clusterId: 2, clusterName: 'iot-cluster',
    enable: true, isHealthy: true, weight: 1, maxFails: 3, failTimeout: 60,
  },
  {
    id: 42, name: 'api-node', address: '10.0.0.12:8080', clusterId: 0, clusterName: '',
    enable: false, isHealthy: false, weight: 2, maxFails: 5, failTimeout: 30,
  },
];

const EDIT_FORM: GatewayNodeForm = {
  ...emptyNodeForm(),
  clusterId: 2,
  name: 'api-node',
  address: '10.0.0.12:8080',
  enable: false,
  isHealthy: false,
  weight: 2,
  maxFails: 5,
  failTimeout: 30,
  remark: '边缘机房备用节点',
};

export const stories = [
  { id: 'GatewayNode/Data', component: GatewayNodeBlock, props: { rows: WITH_DATA, clusters: CLUSTERS } },
  { id: 'GatewayNode/Empty', component: GatewayNodeBlock, props: { rows: [], clusters: CLUSTERS } },
  { id: 'GatewayNode/Loading', component: GatewayNodeBlock, props: { rows: [], clusters: CLUSTERS, loading: true } },
  { id: 'GatewayNode/Failed', component: GatewayNodeBlock, props: { rows: [], clusters: CLUSTERS, error: 'Request failed with status 500' } },
  { id: 'GatewayNode/CreateDrawer', component: GatewayNodeDrawer, props: { open: true, form: emptyNodeForm(), clusters: CLUSTERS } },
  { id: 'GatewayNode/CreateDrawerNoCluster', component: GatewayNodeDrawer, props: { open: true, form: emptyNodeForm(), clusters: [] } },
  { id: 'GatewayNode/EditDrawer', component: GatewayNodeDrawer, props: { open: true, form: EDIT_FORM, editing: true, clusters: CLUSTERS } },
];
