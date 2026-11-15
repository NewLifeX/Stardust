<script setup lang="ts">
/**
 * MeshPage：治理页（/mesh）智能容器。五个 tab：注册中心 / 网关 / AI·MCP / 平台设置 / 中间件。
 * 取数与归一在 mesh.logic（request 注入）；中间件 tab 自持取数（MiddlewareTab 容器，CARD-MW-01 起）；
 * 本组件只管 tab 切换、各 tab 独立加载与上下文联动。
 * projectId 取自 stardustContext（非空时随请求 params 透传，全局拦截器也会注入）；
 * 单个 tab 请求失败只让该 tab 显示错误+重试，不拖垮整页。展示细节见各 *Tab.vue（纯 UI）。
 */
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import PageHead from './PageHead.vue';
import Btn from './Btn.vue';
import KIcon from './KIcon.vue';
import RegistryTab from './RegistryTab.vue';
import ServiceDetailDrawer from './ServiceDetailDrawer.vue';
import GatewayTab from './GatewayTab.vue';
import GatewayRouteDrawer from './GatewayRouteDrawer.vue';
import GatewayClusterDrawer from './GatewayClusterDrawer.vue';
import GatewayCertDrawer from './GatewayCertDrawer.vue';
import GatewayNodeDrawer from './GatewayNodeDrawer.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import McpTab from './McpTab.vue';
import PlatformTab from './PlatformTab.vue';
import MiddlewareTab from './MiddlewareTab.vue';
import { buildClusterMap, createService, fetchGateway, fetchMcpServers, fetchPlatform, fetchRegistry, fetchServiceDetail, saveService } from './mesh.logic.ts';
import type { GatewayRouteRow, McpServerRow, MemberRow, PlatformTile, ProjectInfo, ProjectRow, RegistryRow, ServiceDetail, ServiceEditModel } from './mesh.logic.ts';
import {
  detailToRouteForm, emptyRouteFilter, emptyRouteForm, fetchProjects, fetchRouteDetail, removeRoute, submitRoute,
} from './gatewayRoute.logic.ts';
import type { GatewayRouteFilter, GatewayRouteForm } from './gatewayRoute.logic.ts';
import {
  detailToClusterForm, emptyClusterFilter, emptyClusterForm, fetchClusterDetail, isClusterFilterActive, removeCluster, submitCluster, toClusterRows,
} from './gatewayCluster.logic.ts';
import type { ClusterRow, GatewayClusterFilter, GatewayClusterForm } from './gatewayCluster.logic.ts';
import {
  detailToCertForm, emptyCertFilter, emptyCertForm, fetchCertDetail, removeCert, submitCert, toCertRows,
} from './gatewayCert.logic.ts';
import type { CertRow, GatewayCertFilter, GatewayCertForm } from './gatewayCert.logic.ts';
import {
  detailToNodeForm, emptyNodeFilter, emptyNodeForm, fetchGatewayNodes, fetchNodeDetail, filterNodesByVisibleClusters, removeNode, submitNode, toNodeRows,
} from './gatewayNode.logic.ts';
import type { GatewayNodeFilter, GatewayNodeForm, NodeRow } from './gatewayNode.logic.ts';
import { toast } from './useToast.ts';
import { fetchMcpManage, type McpManageData } from './mcpToken.logic.ts';
import type { MySqlNodeFilter } from './middleware.logic.ts';
import { stardustContext, bumpProjectsVersion } from '@/stores/stardustContext';
import { useContextReload } from './useContextReload.ts';

type MeshTab = 'registry' | 'gateway' | 'mcp' | 'platform' | 'middleware';

const TABS: ReadonlyArray<{ key: MeshTab; label: string }> = [
  { key: 'registry', label: '注册中心' },
  { key: 'gateway', label: '网关' },
  { key: 'mcp', label: 'AI / MCP' },
  { key: 'platform', label: '平台设置' },
  { key: 'middleware', label: '中间件' },
];

const MESH_TAB_KEY = 'stardust-mesh-tab';
const route = useRoute();
const router = useRouter();

function readInitialTab(): MeshTab {
  const q = String(route.query.tab ?? '');
  if (q === 'mcp' || q === 'registry' || q === 'gateway' || q === 'platform' || q === 'middleware') return q;
  const saved = localStorage.getItem(MESH_TAB_KEY);
  if (saved === 'mcp' || saved === 'registry' || saved === 'gateway' || saved === 'platform' || saved === 'middleware') return saved;
  return 'mcp';
}

const active = ref<MeshTab>(readInitialTab());

/** 中间件深链（J-MW-02）：路由 query → MySQL 节点初始筛选（id 单条直达/精确过滤），只读一次 */
const mwInitialNodeFilter: Partial<MySqlNodeFilter> = (() => {
  const q = route.query;
  const one = (k: string): string => (q[k] === undefined ? '' : String(q[k]));
  const id = Number(q.id ?? NaN);
  const port = one('port');
  const projectId = one('projectId');
  const enableRaw = one('enable');
  return {
    id: Number.isFinite(id) && id > 0 ? id : 0,
    server: one('server').trim(),
    port: /^\d+$/.test(port) ? port : '',
    projectId: /^\d+$/.test(projectId) ? projectId : '',
    enable: enableRaw === 'true' || enableRaw === 'false' ? (enableRaw as 'true' | 'false') : '',
    dtStart: one('dtStart').trim(),
    dtEnd: one('dtEnd').trim(),
    q: one('q').trim(),
  };
})();

onMounted(() => {
  if (route.query.tab !== active.value) {
    void router.replace({ query: { ...route.query, tab: active.value } });
  }
});

watch(active, (tab) => {
  localStorage.setItem(MESH_TAB_KEY, tab);
  if (route.query.tab !== tab) {
    void router.replace({ query: { ...route.query, tab } });
  }
});

interface State<T> {
  rows: T[];
  loading: boolean;
  error: string;
}
function initState<T>(): State<T> {
  return { rows: [], loading: true, error: '' };
}

const registry = ref<State<RegistryRow>>(initState());
/** 网关：路由行 + 集群原始行透出（CARD-GW-03 集群下拉/GW-01 数据源）+ 证书原始行透出（GW-04 数据源/matchCert 同源）+
 * 筛选条（容器持 filter，CARD-DEP-02 同款）；routeError/clusterError/certError 按接口归因（CARD-GW-01 裁定：
 * GatewayResult 三字段拆分 + aggregate error=三者或；证书请求永不带 projectId——C11）；
 * 节点为独立 State（GW-02 定稿裁定 5），节点请求同样永不带 projectId（GW-02） */
const gateway = ref<State<GatewayRouteRow> & { clusters: Rec[]; certs: Rec[]; routeError: string; clusterError: string; certError: string }>({
  ...initState(), clusters: [], certs: [], routeError: '', clusterError: '', certError: '',
});
const gatewayRouteFilter = ref<GatewayRouteFilter>(emptyRouteFilter());
const gatewayClusterFilter = ref<GatewayClusterFilter>(emptyClusterFilter());
const gatewayCertFilter = ref<GatewayCertFilter>(emptyCertFilter());
/** 节点独立 State（GW-02 定稿裁定 5：加载/错误不并入路由/集群/证书聚合，节点区块自带三态） */
const gatewayNodes = ref<{ rows: Rec[]; loading: boolean; error: string }>({ rows: [], loading: true, error: '' });
const gatewayNodeFilter = ref<GatewayNodeFilter>(emptyNodeFilter());
const mcp = ref<{ data: McpManageData; servers: McpServerRow[]; serversError: string; loading: boolean; error: string }>({
  data: { tokens: [], resources: [], audits: [], projects: [], users: [], nodes: [], apps: [], error: '' },
  servers: [], serversError: '', loading: true, error: '',
});
const platform = ref<{ rows: PlatformTile[]; projects: ProjectRow[]; members: MemberRow[]; projectInfo: ProjectInfo | null; loading: boolean; error: string }>({
  rows: [], projects: [], members: [], projectInfo: null, loading: true, error: '',
});

const anyLoading = computed(
  () => registry.value.loading || gateway.value.loading || mcp.value.loading || platform.value.loading,
);

const req = request as Req;

async function loadRegistry(): Promise<void> {
  registry.value.loading = true;
  const r = await fetchRegistry(req, stardustContext.projectId);
  registry.value.rows = r.rows;
  registry.value.error = r.error;
  registry.value.loading = false;
}
async function loadGateway(): Promise<void> {
  gateway.value.loading = true;
  gatewayNodes.value.loading = true;
  // 节点与路由/集群/证书并行取数；节点错误独立承载不并入聚合（CARD-GW-02 定稿裁定 5）
  const [r, n] = await Promise.all([
    fetchGateway(req, stardustContext.projectId, gatewayRouteFilter.value, gatewayClusterFilter.value, gatewayCertFilter.value),
    fetchGatewayNodes(req, gatewayNodeFilter.value),
  ]);
  gateway.value.rows = r.rows;
  gateway.value.clusters = r.clusters;
  gateway.value.certs = r.certs;
  gateway.value.error = r.error;
  gateway.value.routeError = r.routeError;
  gateway.value.clusterError = r.clusterError;
  gateway.value.certError = r.certError;
  gatewayNodes.value.rows = n.rows;
  gatewayNodes.value.error = n.error;
  gatewayNodes.value.loading = false;
  gateway.value.loading = false;
}

// ==================== CARD-GW-03 网关路由维护（新增/编辑/删除/筛选） ====================

type Opt = { id: number; name: string };
type Rec = Record<string, unknown>;

/** 集群下拉选项（Id→Name 归一，来自 fetchGateway 透出） */
const clusterOptions = computed<Opt[]>(() =>
  gateway.value.clusters.map((c) => ({ id: Number(c.Id ?? c.id) || 0, name: String(c.Name ?? c.name ?? '') })).filter((o) => o.id > 0),
);

/** 项目下拉选项（抽屉首次打开延迟拉取，避免给网关页签加第四请求——保 J-GW-04 C05 三请求断言） */
const projectOptions = ref<Opt[]>([]);
const projectsLoading = ref(false);
async function ensureProjects(): Promise<void> {
  if (projectsLoading.value || projectOptions.value.length) return;
  projectsLoading.value = true;
  const rows = await fetchProjects(req);
  projectOptions.value = rows
    .map((p) => ({ id: Number(p.Id ?? p.id) || 0, name: String(p.Name ?? p.name ?? '') }))
    .filter((o) => o.id > 0);
  projectsLoading.value = false;
}

/** 筛选变更（GatewayTab 发 patch）：合并后重取（后端 Search 语义） */
async function onRouteFilterChange(patch: Partial<GatewayRouteFilter>): Promise<void> {
  gatewayRouteFilter.value = { ...gatewayRouteFilter.value, ...patch };
  await loadGateway();
}
async function onRouteFilterReset(): Promise<void> {
  gatewayRouteFilter.value = emptyRouteFilter();
  await loadGateway();
}

const routeDrawerOpen = ref(false);
const routeEditing = ref(false);
const routeEditingId = ref(0);
const routeForm = ref<GatewayRouteForm | null>(null);
const routeFormFailed = ref(false);
const routeSaveBusy = ref(false);

async function openRouteCreate(): Promise<void> {
  routeEditing.value = false;
  routeEditingId.value = 0;
  routeFormFailed.value = false;
  routeForm.value = emptyRouteForm();
  routeDrawerOpen.value = true;
  await ensureProjects();
}

/** 编辑：按 id 拉 Detail 回填（列表归一行缺 9 字段，卡定稿裁定 5） */
async function openRouteEdit(row: GatewayRouteRow): Promise<void> {
  routeEditing.value = true;
  routeEditingId.value = row.id;
  routeFormFailed.value = false;
  routeForm.value = null;
  routeDrawerOpen.value = true;
  await ensureProjects();
  const d = await fetchRouteDetail(req, row.id);
  routeFormFailed.value = d === null;
  routeForm.value = d ? detailToRouteForm(d) : null;
}

async function onRouteSubmit(m: GatewayRouteForm): Promise<void> {
  if (routeSaveBusy.value) return;
  routeSaveBusy.value = true;
  try {
    const r = await submitRoute(req, m, routeEditing.value ? routeEditingId.value : undefined);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      routeDrawerOpen.value = false;
      await loadGateway();
    }
  } finally {
    routeSaveBusy.value = false;
  }
}

/** 删除确认（GuardModal：标题+正文两段，J-GW-04 C20） */
const routeDeleteTarget = ref<GatewayRouteRow | null>(null);
const routeDeleteBusy = ref(false);

function askRemoveRoute(row: GatewayRouteRow): void {
  routeDeleteTarget.value = row;
}

async function onRemoveRouteConfirm(): Promise<void> {
  const row = routeDeleteTarget.value;
  if (!row || routeDeleteBusy.value) return;
  routeDeleteBusy.value = true;
  try {
    const r = await removeRoute(req, row.id);
    toast(r.message, r.ok ? 'ok' : 'err');
    routeDeleteTarget.value = null;
    if (r.ok) await loadGateway();
  } finally {
    routeDeleteBusy.value = false;
  }
}

// ==================== CARD-GW-01 网关集群维护（新增/编辑/删除/筛选） ====================

/** 集群区块归一行（容器持状态并归一，区块纯渲染） */
const clusterRows = computed<ClusterRow[]>(() => toClusterRows(gateway.value.clusters));

/** 集群筛选变更（GatewayClusterBlock 发 patch）：合并后重取（后端 Search 语义，J-GW-02 C07） */
async function onClusterFilterChange(patch: Partial<GatewayClusterFilter>): Promise<void> {
  gatewayClusterFilter.value = { ...gatewayClusterFilter.value, ...patch };
  await loadGateway();
}

const clusterDrawerOpen = ref(false);
const clusterEditing = ref(false);
const clusterEditingId = ref(0);
const clusterForm = ref<GatewayClusterForm | null>(null);
const clusterFormFailed = ref(false);
const clusterSaveBusy = ref(false);

async function openClusterCreate(): Promise<void> {
  clusterEditing.value = false;
  clusterEditingId.value = 0;
  clusterFormFailed.value = false;
  clusterForm.value = emptyClusterForm();
  clusterDrawerOpen.value = true;
  await ensureProjects();
}

/** 编辑：按 id 拉 Detail 回填（列表归一行缺表单字段，同 GW-03 定稿裁定 5） */
async function openClusterEdit(row: ClusterRow): Promise<void> {
  clusterEditing.value = true;
  clusterEditingId.value = row.id;
  clusterFormFailed.value = false;
  clusterForm.value = null;
  clusterDrawerOpen.value = true;
  await ensureProjects();
  const d = await fetchClusterDetail(req, row.id);
  clusterFormFailed.value = d === null;
  clusterForm.value = d ? detailToClusterForm(d) : null;
}

async function onClusterSubmit(m: GatewayClusterForm): Promise<void> {
  if (clusterSaveBusy.value) return;
  clusterSaveBusy.value = true;
  try {
    const r = await submitCluster(req, m, clusterEditing.value ? clusterEditingId.value : undefined);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      clusterDrawerOpen.value = false;
      await loadGateway();
    }
  } finally {
    clusterSaveBusy.value = false;
  }
}

/** 删除确认（GuardModal：标题+正文两段，J-GW-02 C17——正文写明级联删节点与路由「待接入」） */
const clusterDeleteTarget = ref<ClusterRow | null>(null);
const clusterDeleteBusy = ref(false);

function askRemoveCluster(row: ClusterRow): void {
  clusterDeleteTarget.value = row;
}

async function onRemoveClusterConfirm(): Promise<void> {
  const row = clusterDeleteTarget.value;
  if (!row || clusterDeleteBusy.value) return;
  clusterDeleteBusy.value = true;
  try {
    const r = await removeCluster(req, row.id);
    toast(r.message, r.ok ? 'ok' : 'err');
    clusterDeleteTarget.value = null;
    if (r.ok) await loadGateway();
  } finally {
    clusterDeleteBusy.value = false;
  }
}

// ==================== CARD-GW-04 网关证书维护（新增/编辑/删除/筛选/行内展开） ====================

/** 证书区块归一行（容器持状态并归一，区块纯渲染）；NotAfter 升序临期在前，列表缺值排末尾（逻辑层降级） */
const certRows = computed<CertRow[]>(() => toCertRows(gateway.value.certs));

/** 证书筛选变更（GatewayCertBlock 发 patch）：合并后重取；projectId 永不随证书请求（C11，逻辑层已断言） */
async function onCertFilterChange(patch: Partial<GatewayCertFilter>): Promise<void> {
  gatewayCertFilter.value = { ...gatewayCertFilter.value, ...patch };
  await loadGateway();
}

const certDrawerOpen = ref(false);
const certEditing = ref(false);
const certEditingId = ref(0);
const certForm = ref<GatewayCertForm | null>(null);
const certFormFailed = ref(false);
const certSaveBusy = ref(false);

/** 新建：不拉 Detail，直接空表单（enable 开 / autoRenew 关 / renewDays 30） */
function openCertCreate(): void {
  certEditing.value = false;
  certEditingId.value = 0;
  certFormFailed.value = false;
  certForm.value = emptyCertForm();
  certDrawerOpen.value = true;
}

/** 编辑：按 id 拉 Detail 回填（列表行缺证书信息组字段，同 GW-03 定稿裁定 5）；PfxPassword 恒不回显（硬条款③） */
async function openCertEdit(row: CertRow): Promise<void> {
  certEditing.value = true;
  certEditingId.value = row.id;
  certFormFailed.value = false;
  certForm.value = null;
  certDrawerOpen.value = true;
  const d = await fetchCertDetail(req, row.id);
  certFormFailed.value = d === null;
  certForm.value = d ? detailToCertForm(d) : null;
}

async function onCertSubmit(m: GatewayCertForm): Promise<void> {
  if (certSaveBusy.value) return;
  certSaveBusy.value = true;
  try {
    const r = await submitCert(req, m, certEditing.value ? certEditingId.value : undefined);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      certDrawerOpen.value = false;
      await loadGateway();
    }
  } finally {
    certSaveBusy.value = false;
  }
}

/** 行内展开「有效期」（定稿裁定 4）：懒拉 Detail 归一承载；再点收起 */
const certExpandedId = ref<number | null>(null);
const certExpandLoading = ref(false);
const certExpandError = ref('');
const certExpandInfo = ref<GatewayCertForm | null>(null);

async function onCertExpand(row: CertRow): Promise<void> {
  if (certExpandedId.value === row.id) {
    certExpandedId.value = null;
    certExpandInfo.value = null;
    certExpandError.value = '';
    return;
  }
  certExpandedId.value = row.id;
  certExpandLoading.value = true;
  certExpandError.value = '';
  certExpandInfo.value = null;
  const d = await fetchCertDetail(req, row.id);
  certExpandLoading.value = false;
  if (d === null) {
    certExpandError.value = '证书详情加载失败，请重试';
    return;
  }
  certExpandInfo.value = detailToCertForm(d);
}

/** 删除确认（GuardModal：标题+正文两段，J-GW-01 C15——正文写明路由 TLS 列「待接入」后果） */
const certDeleteTarget = ref<CertRow | null>(null);
const certDeleteBusy = ref(false);

function askRemoveCert(row: CertRow): void {
  certDeleteTarget.value = row;
}

async function onRemoveCertConfirm(): Promise<void> {
  const row = certDeleteTarget.value;
  if (!row || certDeleteBusy.value) return;
  certDeleteBusy.value = true;
  try {
    const r = await removeCert(req, row.id);
    toast(r.message, r.ok ? 'ok' : 'err');
    certDeleteTarget.value = null;
    if (r.ok) await loadGateway();
  } finally {
    certDeleteBusy.value = false;
  }
}
// ==================== CARD-GW-02 网关节点维护（新增/编辑/删除/筛选） ====================

/** 节点区块归一行：ClusterName 经 buildClusterMap 就地解析（GW-01 集群同源透出）；id 升序 */
const nodeRowsRaw = computed<NodeRow[]>(() => toNodeRows(gatewayNodes.value.rows, buildClusterMap(gateway.value.clusters)));
/** 定稿裁定 1（本地过滤）：仅保留「当前项目可见集群」的节点；可见集=fetchGateway 透出 clusters（服务端已按 projectId 圈定）。
 * 集群为空或集群筛选生效中（结果集已是子集，再过滤会假空）→ 不过滤，宁多勿错；clusterId≤0 脏数据节点恒保留。 */
const nodeRows = computed<NodeRow[]>(() =>
  filterNodesByVisibleClusters(nodeRowsRaw.value, gateway.value.clusters, isClusterFilterActive(gatewayClusterFilter.value)),
);

/** 节点筛选变更（GatewayNodeBlock 发 patch）：合并后重取；节点请求永不带 projectId（逻辑层断言） */
async function onNodeFilterChange(patch: Partial<GatewayNodeFilter>): Promise<void> {
  gatewayNodeFilter.value = { ...gatewayNodeFilter.value, ...patch };
  await loadGateway();
}

const nodeDrawerOpen = ref(false);
const nodeEditing = ref(false);
const nodeEditingId = ref(0);
const nodeForm = ref<GatewayNodeForm | null>(null);
const nodeFormFailed = ref(false);
const nodeSaveBusy = ref(false);

/** 新建：不拉 Detail，直接空表单（Weight 1 / MaxFails 3 / FailTimeout 60 秒 / Enable、IsHealthy 开——实体 DefaultValue） */
function openNodeCreate(): void {
  nodeEditing.value = false;
  nodeEditingId.value = 0;
  nodeFormFailed.value = false;
  nodeForm.value = emptyNodeForm();
  nodeDrawerOpen.value = true;
}

/** 编辑：按 id 拉 Detail 回填（列表行缺 Remark 等表单字段） */
async function openNodeEdit(row: NodeRow): Promise<void> {
  nodeEditing.value = true;
  nodeEditingId.value = row.id;
  nodeFormFailed.value = false;
  nodeForm.value = null;
  nodeDrawerOpen.value = true;
  const d = await fetchNodeDetail(req, row.id);
  nodeFormFailed.value = d === null;
  nodeForm.value = d ? detailToNodeForm(d) : null;
}

async function onNodeSubmit(m: GatewayNodeForm): Promise<void> {
  if (nodeSaveBusy.value) return;
  nodeSaveBusy.value = true;
  try {
    const r = await submitNode(req, m, nodeEditing.value ? nodeEditingId.value : undefined);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      nodeDrawerOpen.value = false;
      await loadGateway();
    }
  } finally {
    nodeSaveBusy.value = false;
  }
}

/** 删除确认（GuardModal：C13 文案逐字；Name 为空时回退显示地址——裁量 ②） */
const nodeDeleteTarget = ref<NodeRow | null>(null);
const nodeDeleteBusy = ref(false);

function askRemoveNode(row: NodeRow): void {
  nodeDeleteTarget.value = row;
}

async function onRemoveNodeConfirm(): Promise<void> {
  const row = nodeDeleteTarget.value;
  if (!row || nodeDeleteBusy.value) return;
  nodeDeleteBusy.value = true;
  try {
    const r = await removeNode(req, row.id);
    toast(r.message, r.ok ? 'ok' : 'err');
    nodeDeleteTarget.value = null;
    if (r.ok) await loadGateway();
  } finally {
    nodeDeleteBusy.value = false;
  }
}

async function loadMcp(): Promise<void> {
  mcp.value.loading = true;
  const [mgd, srv] = await Promise.all([
    fetchMcpManage(req, stardustContext.projectId),
    fetchMcpServers(req, stardustContext.projectId),
  ]);
  mcp.value.data = mgd;
  mcp.value.error = mgd.error;
  mcp.value.servers = srv.rows;
  mcp.value.serversError = srv.error;
  mcp.value.loading = false;
}
async function loadPlatform(): Promise<void> {
  platform.value.loading = true;
  const r = await fetchPlatform(req, stardustContext.projectId);
  platform.value.rows = r.tiles;
  platform.value.projects = r.projects;
  platform.value.members = r.members;
  platform.value.projectInfo = r.projectInfo;
  platform.value.error = r.error;
  platform.value.loading = false;
}

/** CARD-PLT-01 项目联动：项目增删改成功 → 重拉平台 tab（区块/磁贴同源）+ bump projectsVersion（顶栏下拉重拉） */
function onProjectChanged(): Promise<void> {
  bumpProjectsVersion();
  return loadPlatform();
}

/** 上下文就绪/切换后并行重取四个 tab（各自独立，一个失败不影响其它） */
async function loadAll(): Promise<void> {
  await Promise.allSettled([loadRegistry(), loadGateway(), loadMcp(), loadPlatform()]);
}

/** F13 服务详情 Drawer：点服务名打开，拉详情（Service + 提供者 + 消费者）；REG-09 新建态无取数 */
const serviceDrawerOpen = ref(false);
const serviceCreateMode = ref(false);
const serviceDetailLoading = ref(false);
const serviceDetailFailed = ref(false);
const serviceDetail = ref<ServiceDetail | null>(null);
const serviceDetailName = ref('');
// C05 修复（wave4 排程令）：失败态重试需要 attempted id——fetchServiceDetail 失败时
// serviceDetail 保持 null，旧绑定 `serviceDetail && openService(...)` 恒短路（REG-09 走查实证）。
const serviceDetailId = ref<number | null>(null);
const serviceSaveBusy = ref(false);

async function openService(id: number, name: string): Promise<void> {
  serviceCreateMode.value = false;
  serviceDrawerOpen.value = true;
  serviceDetailFailed.value = false;
  serviceDetailName.value = name;
  serviceDetailId.value = id;
  serviceDetailLoading.value = true;
  try {
    const d = await fetchServiceDetail(req, id);
    serviceDetail.value = d;
    serviceDetailFailed.value = d === null;
  } finally {
    serviceDetailLoading.value = false;
  }
}

/** 失败态重试：按 attempted id 重开（serviceDetail 为 null 也可用，C05 短路修复点）。 */
function retryServiceDetail(): void {
  const id = serviceDetailId.value;
  if (id != null) void openService(id, serviceDetailName.value);
}

/** REG-09 新建服务：create 态开抽屉（service=null 时抽屉显示空表单，单例默认勾选） */
function openServiceCreate(): void {
  serviceCreateMode.value = true;
  serviceDetail.value = null;
  serviceDetailName.value = '';
  serviceDetailId.value = null; // create 态无 attempted 详情，防误重试
  serviceDetailLoading.value = false;
  serviceDetailFailed.value = false;
  serviceDrawerOpen.value = true;
}

/** 保存服务：编辑=PUT 部分更新后重拉详情与列表；新建=POST 后关抽屉并刷新列表 */
async function onServiceSubmit(m: ServiceEditModel): Promise<void> {
  if (serviceSaveBusy.value) return;
  if (serviceCreateMode.value) {
    serviceSaveBusy.value = true;
    try {
      const r = await createService(req, m);
      toast(r.message, r.ok ? 'ok' : 'err');
      if (r.ok) {
        serviceDrawerOpen.value = false;
        await loadRegistry();
      }
    } finally {
      serviceSaveBusy.value = false;
    }
    return;
  }
  if (!serviceDetail.value) return;
  serviceSaveBusy.value = true;
  try {
    const r = await saveService(req, serviceDetail.value.id, m);
    toast(r.message, r.ok ? 'ok' : 'err');
    if (r.ok) {
      await openService(serviceDetail.value.id, serviceDetail.value.name);
      await loadRegistry();
    }
  } finally {
    serviceSaveBusy.value = false;
  }
}

/** 打开平台设置编辑页（Cube 单对象配置页） */
function openPlatformSettings(): void {
  void router.push('/Platform/StarServer');
}

// 上下文（项目/环境）就绪后首轮取数，之后切换自动重取
const { reload } = useContextReload(() => { void loadAll(); }, stardustContext);
</script>

<template>
  <div data-spec="mesh-page">
    <PageHead
      eyebrow="治理 · 低频但重要，一次配置长期受益"
      title="治理"
      sub="注册中心、网关、MCP 与平台设置都在这里。日常你不需要打开它。"
    >
      <template #actions>
        <Btn
          size="sm"
          :disabled="anyLoading"
          data-spec="mesh-refresh"
          @click="reload"
        >
          <template #icon>
            <span :class="anyLoading ? 'inline-flex animate-spin' : 'inline-flex'">
              <KIcon
                name="Refresh"
                :size="13"
              />
            </span>
          </template>
          {{ anyLoading ? '刷新中…' : '刷新' }}
        </Btn>
      </template>
    </PageHead>

    <div
      role="tablist"
      data-spec="mesh-tabs"
      class="mb-5 mt-4 flex flex-wrap gap-[2px] border-b border-[var(--el-border-color-light)]"
    >
      <button
        v-for="t in TABS"
        :key="t.key"
        type="button"
        role="tab"
        data-spec="mesh-tab"
        :data-key="t.key"
        :aria-selected="t.key === active"
        :class="[
          '-mb-px inline-flex h-[34px] cursor-pointer items-center rounded-t-[6px] border-0 border-b-2 border-solid px-[13px] text-[13px] font-medium outline-none transition-colors focus-visible:bg-[var(--el-fill-color-light)]',
          t.key === active
            ? 'border-b-[var(--el-color-primary)] bg-[color-mix(in_oklab,var(--el-color-primary)_10%,transparent)] text-[var(--el-color-primary-light-3)]'
            : 'border-b-transparent bg-transparent text-[var(--el-text-color-secondary)] hover:bg-[var(--el-fill-color-light)] hover:text-[var(--el-text-color-primary)]',
        ]"
        @click="active = t.key"
      >
        {{ t.label }}
      </button>
    </div>

    <RegistryTab
      v-show="active === 'registry'"
      :rows="registry.rows"
      :loading="registry.loading"
      :error="registry.error"
      @retry="loadRegistry"
      @open="(id) => openService(id, String(registry.rows.find((r) => r.id === id)?.name || ''))"
      @create="openServiceCreate"
    />
    <GatewayTab
      v-show="active === 'gateway'"
      :rows="gateway.rows"
      :loading="gateway.loading"
      :error="gateway.routeError"
      :clusters="clusterOptions"
      :cluster-rows="clusterRows"
      :cluster-loading="gateway.loading"
      :cluster-error="gateway.clusterError"
      :node-rows="nodeRows"
      :node-loading="gatewayNodes.loading"
      :node-error="gatewayNodes.error"
      :cert-rows="certRows"
      :cert-loading="gateway.loading"
      :cert-error="gateway.certError"
      :cert-expanded-id="certExpandedId"
      :cert-expand-loading="certExpandLoading"
      :cert-expand-error="certExpandError"
      :cert-expand-info="certExpandInfo"
      @retry="loadGateway"
      @create="openRouteCreate"
      @edit="openRouteEdit"
      @remove="askRemoveRoute"
      @filter-change="onRouteFilterChange"
      @filter-reset="onRouteFilterReset"
      @cluster-create="openClusterCreate"
      @cluster-edit="openClusterEdit"
      @cluster-remove="askRemoveCluster"
      @cluster-filter-change="onClusterFilterChange"
      @node-create="openNodeCreate"
      @node-edit="openNodeEdit"
      @node-remove="askRemoveNode"
      @node-filter-change="onNodeFilterChange"
      @cert-create="openCertCreate"
      @cert-edit="openCertEdit"
      @cert-remove="askRemoveCert"
      @cert-filter-change="onCertFilterChange"
      @cert-expand="onCertExpand"
    />
    <McpTab
      v-show="active === 'mcp'"
      :tokens="mcp.data.tokens"
      :resources="mcp.data.resources"
      :audits="mcp.data.audits"
      :projects="mcp.data.projects"
      :users="mcp.data.users"
      :nodes="mcp.data.nodes"
      :apps="mcp.data.apps"
      :servers="mcp.servers"
      :servers-error="mcp.serversError"
      :project-id="stardustContext.projectId ?? undefined"
      :loading="mcp.loading"
      :error="mcp.error"
      @retry="loadMcp"
      @changed="loadMcp"
    />
    <PlatformTab
      v-show="active === 'platform'"
      :tiles="platform.rows"
      :projects="platform.projects"
      :members="platform.members"
      :project-info="platform.projectInfo"
      :project-id="stardustContext.projectId"
      :loading="platform.loading"
      :error="platform.error"
      @retry="loadPlatform"
      @edit="openPlatformSettings"
      @project-changed="onProjectChanged"
    />
    <!-- 中间件 tab：自持取数容器（CARD-MW-01 MySQL 数据起，MW-02~05 同页追加） -->
    <MiddlewareTab
      v-show="active === 'middleware'"
      :initial-node-filter="mwInitialNodeFilter"
    />

    <!-- F13 服务详情 Drawer：编辑服务信息 + 提供者/消费者列表；REG-09 create 态=新建服务 -->
    <ServiceDetailDrawer
      :open="serviceDrawerOpen"
      :mode="serviceCreateMode ? 'create' : 'edit'"
      :loading="serviceDetailLoading"
      :failed="serviceDetailFailed"
      :service="serviceDetail?.service ?? null"
      :providers="serviceDetail?.providers ?? []"
      :consumers="serviceDetail?.consumers ?? []"
      :service-name="serviceDetailName"
      :busy="serviceSaveBusy"
      @close="serviceDrawerOpen = false"
      @retry="retryServiceDetail"
      @submit="onServiceSubmit"
    />

    <!-- CARD-GW-03 路由抽屉 + 删除确认 -->
    <GatewayRouteDrawer
      :open="routeDrawerOpen"
      :form="routeForm"
      :editing="routeEditing"
      :busy="routeSaveBusy"
      :failed="routeFormFailed"
      :clusters="clusterOptions"
      :projects="projectOptions"
      :projects-loading="projectsLoading"
      @close="routeDrawerOpen = false"
      @submit="onRouteSubmit"
    />
    <!-- CARD-GW-01 集群抽屉 + 删除确认（项目选项与路由抽屉共用延迟加载） -->
    <GatewayClusterDrawer
      :open="clusterDrawerOpen"
      :form="clusterForm"
      :editing="clusterEditing"
      :busy="clusterSaveBusy"
      :failed="clusterFormFailed"
      :projects="projectOptions"
      :projects-loading="projectsLoading"
      @close="clusterDrawerOpen = false"
      @submit="onClusterSubmit"
    />
    <GuardModal
      v-if="routeDeleteTarget"
      label="删除路由"
      :detail="`确认删除路由「${routeDeleteTarget.name}」？删除后 ${routeDeleteTarget.domain || '—'}${routeDeleteTarget.path} 的流量将不再转发。`"
      @cancel="routeDeleteTarget = null"
      @confirm="onRemoveRouteConfirm"
    />
    <GuardModal
      v-if="clusterDeleteTarget"
      label="删除集群"
      :detail="`确认删除集群「${clusterDeleteTarget.name}」？其下全部网关节点记录将一并删除；指向该集群的路由不会被删除，将显示为「待接入」。`"
      @cancel="clusterDeleteTarget = null"
      @confirm="onRemoveClusterConfirm"
    />
    <!-- CARD-GW-04 证书抽屉 + 删除确认（无项目依赖：证书为全局资源，不拉 projects——C11 同源） -->
    <GatewayCertDrawer
      :open="certDrawerOpen"
      :form="certForm"
      :editing="certEditing"
      :busy="certSaveBusy"
      :failed="certFormFailed"
      @close="certDrawerOpen = false"
      @submit="onCertSubmit"
    />
    <GuardModal
      v-if="certDeleteTarget"
      label="删除证书"
      :detail="`确认删除证书「${certDeleteTarget.domain}」？删除后依赖它的路由 TLS 列将显示「待接入」，网关将无法为该域名加载证书。`"
      @cancel="certDeleteTarget = null"
      @confirm="onRemoveCertConfirm"
    />
    <!-- CARD-GW-02 节点抽屉 + 删除确认（集群选项与集群下拉同源：fetchGateway 透出，闭包注入不发请求） -->
    <GatewayNodeDrawer
      :open="nodeDrawerOpen"
      :form="nodeForm"
      :editing="nodeEditing"
      :busy="nodeSaveBusy"
      :failed="nodeFormFailed"
      :clusters="clusterOptions"
      @close="nodeDrawerOpen = false"
      @submit="onNodeSubmit"
    />
    <GuardModal
      v-if="nodeDeleteTarget"
      label="删除节点"
      :detail="`确认删除节点「${nodeDeleteTarget.name || nodeDeleteTarget.address}」（${nodeDeleteTarget.address}）？该地址将从集群转发列表移除。`"
      @cancel="nodeDeleteTarget = null"
      @confirm="onRemoveNodeConfirm"
    />
  </div>
</template>
