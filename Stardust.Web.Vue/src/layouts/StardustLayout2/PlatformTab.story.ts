import PlatformTab from './PlatformTab.vue';
import { buildTiles, toMembers, toProjectInfo } from './mesh.logic.ts';
import { toProjectRows } from './galaxyProject.logic.ts';

// 直接用真实归一函数造样例，避免与组件口径漂移
// 注意：后端 GalaxyProject 实体无 Environment 字段（CARD-PLT-01 定稿裁定 1），桩数据不给该字段——
// 「环境」行值非空才显示（hasEnvironment），CT Data 用例断言环境行不渲染。
const GALAXY = [
  { Id: 7, Name: 'prod', Nodes: 5, Apps: 12, WhiteIPs: '10.0.0.1,10.0.0.2', BlackIPs: '', CreateTime: '2026-01-01 00:00:00', TenantName: '星尘', ManagerName: '张三', Sort: 2 },
  { Id: 8, Name: 'staging', Nodes: 2, Apps: 4, WhiteIPs: '', BlackIPs: '192.168.0.0/16', NewServer: 'https://s.p.cn', CreateTime: '2026-02-11 08:30:00', Sort: 9, IsGlobal: true },
];
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
const PROJECTS = toProjectRows(GALAXY);

export const stories = [
  {
    // projectChanged 录制：删除/创建成功后 PlatformTab 只上抛事件+toast，行列表刷新由父组件
    // （MeshPage 监听后 loadPlatform）承载——story 无父层，CT 断言到事件上抛为止
    id: 'PlatformTab/Data',
    component: PlatformTab,
    props: {
      tiles: TILES, projects: PROJECTS, members: MEMBERS, projectInfo: PROJECT_INFO,
      onProjectChanged: () => {
        const w = window as unknown as { __platformProjectChanged?: number };
        w.__platformProjectChanged = (w.__platformProjectChanged ?? 0) + 1;
      },
    },
  },
  { id: 'PlatformTab/Empty', component: PlatformTab, props: { tiles: [], projects: [], members: [], projectInfo: null } },
  { id: 'PlatformTab/Loading', component: PlatformTab, props: { tiles: [], loading: true } },
  { id: 'PlatformTab/Failed', component: PlatformTab, props: { tiles: [], error: 'Request failed with status 500' } },
  // 成员列表：一行有真实用户名，一行退化为 #UserId（触发表脚诚实标注）
  { id: 'PlatformTab/WithMembers', component: PlatformTab, props: { tiles: TILES, projects: PROJECTS, members: MEMBERS, projectInfo: PROJECT_INFO } },
  // CARD-PLT-01：项目管理区块（空库引导态——还没有项目 + 创建项目按钮）
  { id: 'PlatformTab/ProjectsEmpty', component: PlatformTab, props: { tiles: TILES, projects: [], members: MEMBERS, projectInfo: PROJECT_INFO } },
];
