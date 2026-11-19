import GatewayClusterBlock from './GatewayClusterBlock.vue';
import GatewayClusterDrawer from './GatewayClusterDrawer.vue';
import { emptyClusterForm } from './gatewayCluster.logic.ts';
import type { ClusterRow } from './gatewayCluster.logic.ts';

const WITH_DATA: ClusterRow[] = [
  {
    id: 21, projectId: 3, projectName: '演示项目', name: 'DeviceService', loadBalance: 'RoundRobin',
    healthPath: '/health', healthInterval: 10, healthTimeout: 3000, unhealthyThreshold: 3, healthyThreshold: 2,
    sessionSticky: false, sessionStickyName: '', enable: true, activeNodes: 0, updateTime: '2026-11-07 10:30',
  },
  {
    id: 22, projectId: 0, projectName: '', name: 'OrderService', loadBalance: 'LeastConnection',
    healthPath: '', healthInterval: 5, healthTimeout: 2000, unhealthyThreshold: 4, healthyThreshold: 1,
    sessionSticky: true, sessionStickyName: 'srv', enable: false, activeNodes: null, updateTime: '—',
  },
];

const EDIT_FORM = { ...emptyClusterForm(), name: 'DeviceService', sessionSticky: true, sessionStickyName: 'srv', projectId: 3 };

export const stories = [
  { id: 'GatewayCluster/Data', component: GatewayClusterBlock, props: { rows: WITH_DATA } },
  { id: 'GatewayCluster/Empty', component: GatewayClusterBlock, props: { rows: [] } },
  { id: 'GatewayCluster/Loading', component: GatewayClusterBlock, props: { rows: [], loading: true } },
  { id: 'GatewayCluster/Failed', component: GatewayClusterBlock, props: { rows: [], error: 'Request failed with status 500' } },
  { id: 'GatewayCluster/CreateDrawer', component: GatewayClusterDrawer, props: { open: true, form: emptyClusterForm(), projects: [{ id: 3, name: '演示项目' }] } },
  { id: 'GatewayCluster/EditDrawer', component: GatewayClusterDrawer, props: { open: true, form: EDIT_FORM, editing: true, projects: [{ id: 3, name: '演示项目' }] } },
];
