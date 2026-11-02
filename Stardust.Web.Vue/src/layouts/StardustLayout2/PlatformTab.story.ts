import PlatformTab from './PlatformTab.vue';
import { buildTiles, toMembers, toProjectInfo } from './mesh.logic.ts';

// 直接用真实归一函数造样例，避免与组件口径漂移
const GALAXY = [{ Id: 7, Name: 'prod', Environment: '生产', Nodes: 5, Apps: 12, WhiteIPs: '10.0.0.1,10.0.0.2', BlackIPs: '', CreateTime: '2026-01-01 00:00:00' }];
const PROJECT_USERS = [
  { Id: 1, UserId: 101, Name: '何炳宏', Role: '管理员', CreateTime: '2026-08-01 09:30:00' },
  { Id: 2, UserId: 202, Kind: '开发', CreateTime: '2026-09-15 14:00:00' },
];

const TILES = buildTiles(
  GALAXY,
  PROJECT_USERS,
  { DataRetention: 3, DataRetention2: 30, DataRetention3: 300, DotNetSyncPeriod: 43200, UplinkServer: 'https://u.p.cn' },
  7,
);
const MEMBERS = toMembers(PROJECT_USERS);
const PROJECT_INFO = toProjectInfo(GALAXY, 7);

export const stories = [
  { id: 'PlatformTab/Data', component: PlatformTab, props: { tiles: TILES, members: MEMBERS, projectInfo: PROJECT_INFO } },
  { id: 'PlatformTab/Empty', component: PlatformTab, props: { tiles: [], members: [], projectInfo: null } },
  { id: 'PlatformTab/Loading', component: PlatformTab, props: { tiles: [], loading: true } },
  { id: 'PlatformTab/Failed', component: PlatformTab, props: { tiles: [], error: 'Request failed with status 500' } },
  // 成员列表：一行有真实用户名，一行退化为 #UserId（触发表脚诚实标注）
  { id: 'PlatformTab/WithMembers', component: PlatformTab, props: { tiles: TILES, members: MEMBERS, projectInfo: PROJECT_INFO } },
];
