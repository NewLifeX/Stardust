<script setup lang="ts">
/**
 * MiddlewareTab：治理页「中间件」页签（CARD-MW-01 + CARD-MW-02，后续 MW-03~05 区块同页追加）。
 * 容器：自持 MySQL 数据与 MySQL 节点两块状态（筛选/分页/节点候选），request 由 props.deps 可替注入
 * （story/CT 桩用），真实流回落到全局 request（同 TraceStatCard 先例）。
 * 节点候选与数据独立加载：候选失败不拖垮列表（筛选条节点下拉为空 + 底部降级提示）。
 * 加载顺序：先节点后数据——行内 nodeName 依赖候选映射；节点失败时行显示 #id 兜底。
 * 竞态防护：seq 自增，翻页/筛选连发时旧响应丢弃，不覆盖新查询结果（数据与节点列表各一份）。
 * MW-02（J-MW-02 落点）：
 *  - 深链：props.initialNodeFilter 由 MeshPage 从路由 query（id/server/port/projectId/enable/dtStart/dtEnd/q）
 *    注入，Id 单条直达（服务端 nodeId>0 直查），一经手动筛选即被合并覆盖。
 *  - 编辑（S2/S3）：行「编辑」→ 拉 Detail → 开 MySqlNodeDrawer；提交=submitMySqlNode 全量 PUT，结果 toast。
 *  - 刷新（S4/C05）：POST Refresh，§3.5——refreshingId 控制行内按钮转圈+禁用，动画保底 ≥300ms，结果 toast
 *    （成功透传后端「刷新[node]成功！」，失败透传 message），完成后重取列表。
 *  - 监控（旅程 -1 替代路径）：行「监控」→ 把上方 MySQL 数据筛选切到该节点并重查（页内下钻，不跳路由）。
 */
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { toast } from './useToast.ts';
import GuardModal from './GuardModal/GuardModal.vue';
import MySqlDataCard from './MySqlDataCard.vue';
import MySqlNodeCard from './MySqlNodeCard.vue';
import MySqlNodeDrawer from './MySqlNodeDrawer.vue';
import RedisDataCard from './RedisDataCard.vue';
import RedisNodeCard from './RedisNodeCard.vue';
import RedisNodeDrawer from './RedisNodeDrawer.vue';
import RedisQueueCard from './RedisQueueCard.vue';
import type { Rec } from './middleware.logic.ts';
import {
  emptyMySqlDataFilter,
  emptyMySqlNodeFilter,
  emptyRedisDataFilter,
  emptyRedisNodeFilter,
  emptyRedisQueueFilter,
  createRedisNode,
  deleteRedisNode,
  fetchMySqlData,
  fetchMySqlNodeDetail,
  fetchMySqlNodeList,
  fetchMySqlNodes,
  fetchRedisData,
  fetchRedisNodeDetail,
  fetchRedisNodeList,
  fetchRedisNodes,
  fetchRedisQueues,
  nodeDetailToForm,
  nodeDetailToRedisForm,
  refreshMySqlNode,
  refreshRedisNode,
  submitMySqlNode,
  submitRedisNode,
  type MySqlDataFilter,
  type MySqlDataRow,
  type MySqlNodeFilter,
  type MySqlNodeForm,
  type MySqlNodeOption,
  type MySqlNodeRow,
  type RedisDataFilter,
  type RedisDataRow,
  type RedisNodeFilter,
  type RedisNodeForm,
  type RedisNodeOption,
  type RedisNodeRow,
  type RedisQueueFilter,
  type RedisQueueRow,
} from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    deps?: { request?: Req };
    /** 深链初始筛选（MeshPage 从路由 query 注入；id/server/port/projectId/enable/dtStart/dtEnd/q） */
    initialNodeFilter?: Partial<MySqlNodeFilter>;
  }>(),
  { deps: undefined, initialNodeFilter: undefined },
);

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

/** 追踪下钻用（MW-04；命名路由跳转，TraceStatCard 先例） */
const router = useRouter();

/** 加载动画保底：请求太快也让用户看到刷新（AGENTS.md §11：≥300ms） */
function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// ==================== MW-01 · MySQL 数据 ====================

const filter = ref<MySqlDataFilter>(emptyMySqlDataFilter());
const rows = ref<MySqlDataRow[]>([]);
const total = ref(0);
const loading = ref(true);
const error = ref('');
const nodes = ref<MySqlNodeOption[]>([]);
const nodeNames = ref(new Map<number, string>());
const nodeError = ref('');
/** 竞态防护：每次查询自增；响应回来时 seq 不一致=已被更新的查询取代，丢弃 */
const seq = ref(0);

async function loadNodes(): Promise<void> {
  const r = await fetchMySqlNodes(req());
  nodes.value = r.options;
  nodeError.value = r.error;
  nodeNames.value = new Map(r.options.map((o) => [o.id, o.name]));
}

async function loadData(): Promise<void> {
  const mySeq = ++seq.value;
  loading.value = true;
  error.value = '';
  const r = await fetchMySqlData(req(), filter.value, nodeNames.value);
  if (mySeq !== seq.value) return;
  rows.value = r.rows;
  total.value = r.total;
  error.value = r.error;
  loading.value = false;
}

/** 筛选变化：合并补丁 + 页码归零 + 重查 */
function onFilterChange(patch: Partial<MySqlDataFilter>): void {
  filter.value = { ...filter.value, ...patch, pageIndex: 0 };
  void loadData();
}

function onPageChange(pageIndex: number): void {
  if (loading.value || pageIndex < 0) return;
  filter.value = { ...filter.value, pageIndex };
  void loadData();
}

/** 失败重试：候选与数据都重拉（候选可能是瞬时故障） */
function onRetry(): void {
  void loadNodes();
  void loadData();
}

// ==================== MW-02 · MySQL 节点 ====================

const nodeFilter = ref<MySqlNodeFilter>({ ...emptyMySqlNodeFilter(), ...(props.initialNodeFilter ?? {}) });
const nodeRows = ref<MySqlNodeRow[]>([]);
const nodeTotal = ref(0);
const nodeLoading = ref(true);
/** 节点列表错误（独立于 MW-01 候选降级 nodeError：候选失败不拖垮节点卡，反之亦然） */
const nodeListError = ref('');
/** 竞态防护：节点列表独立一份 seq（与数据查询互不丢弃） */
const nodeSeq = ref(0);
/** 正在刷新的节点 Id（行内按钮转圈+禁用，§3.5） */
const refreshingId = ref(0);

async function loadNodeList(): Promise<void> {
  const mySeq = ++nodeSeq.value;
  nodeLoading.value = true;
  nodeListError.value = '';
  const r = await fetchMySqlNodeList(req(), nodeFilter.value);
  if (mySeq !== nodeSeq.value) return;
  nodeRows.value = r.rows;
  nodeTotal.value = r.total;
  nodeListError.value = r.error;
  nodeLoading.value = false;
}

function onNodeFilterChange(patch: Partial<MySqlNodeFilter>): void {
  nodeFilter.value = { ...nodeFilter.value, ...patch, pageIndex: 0 };
  void loadNodeList();
}

function onNodePageChange(pageIndex: number): void {
  if (nodeLoading.value || pageIndex < 0) return;
  nodeFilter.value = { ...nodeFilter.value, pageIndex };
  void loadNodeList();
}

function onNodeRetry(): void {
  void loadNodeList();
}

/** 手动刷新（S4/C05）：保底 300ms 动画 + 结果 toast（成功透传后端 message）+ 重取列表 */
async function onNodeRefresh(row: MySqlNodeRow): Promise<void> {
  if (refreshingId.value) return;
  refreshingId.value = row.id;
  try {
    const [r] = await Promise.all([refreshMySqlNode(req(), row.id), sleep(300)]);
    toast(r.message || (r.ok ? `节点「${row.name || row.id}」已刷新` : '刷新失败'), r.ok ? 'ok' : 'err');
    if (r.ok) void loadNodeList();
  } catch (e) {
    toast(`刷新节点「${row.name || row.id}」失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  } finally {
    refreshingId.value = 0;
  }
}

/** 监控下钻（旅程 -1 替代路径）：上方 MySQL 数据切到该节点并重查 */
function onNodeMonitor(row: MySqlNodeRow): void {
  filter.value = { ...emptyMySqlDataFilter(), mysqlId: row.id };
  void loadData();
  toast(`已切到节点「${row.name || row.id}」的采样监控`, 'ok');
}

// ---- 编辑抽屉（S2/S3） ----

const drawerOpen = ref(false);
const nodeForm = ref<MySqlNodeForm | null>(null);
const nodeDetail = ref<Rec | null>(null);
const nodeBusy = ref(false);
const nodeDetailFailed = ref(false);

async function onNodeEdit(row: MySqlNodeRow): Promise<void> {
  nodeDetail.value = null;
  nodeForm.value = null;
  nodeDetailFailed.value = false;
  drawerOpen.value = true;
  const d = await fetchMySqlNodeDetail(req(), row.id);
  if (!d) {
    nodeDetailFailed.value = true;
    return;
  }
  nodeDetail.value = d;
  nodeForm.value = nodeDetailToForm(d);
}

async function onNodeSubmit(form: MySqlNodeForm): Promise<void> {
  if (nodeBusy.value) return;
  nodeBusy.value = true;
  try {
    const r = await submitMySqlNode(req(), nodeDetail.value ?? {}, form);
    toast(r.message || (r.ok ? '节点已保存' : '保存失败'), r.ok ? 'ok' : 'err');
    if (r.ok) {
      drawerOpen.value = false;
      void loadNodeList();
    }
  } finally {
    nodeBusy.value = false;
  }
}

function onNodeDrawerClose(): void {
  if (nodeBusy.value) return;
  drawerOpen.value = false;
}

// ==================== MW-03 · Redis 数据 ====================

const redisFilter = ref<RedisDataFilter>(emptyRedisDataFilter());
const redisRows = ref<RedisDataRow[]>([]);
/** 曲线数据源（选中节点=全量 1440；无节点=空 → 卡片曲线区空态） */
const redisChartRows = ref<RedisDataRow[]>([]);
const redisTotal = ref(0);
const redisLoading = ref(true);
const redisError = ref('');
const redisNodes = ref<RedisNodeOption[]>([]);
const redisNodeNames = ref(new Map<number, string>());
/** 节点候选错误（独立降级：候选失败不拖垮数据卡，仍可按时间/名称筛选） */
const redisNodeError = ref('');
/** 竞态防护：Redis 数据独立一份 seq */
const redisSeq = ref(0);

async function loadRedisNodes(): Promise<void> {
  const r = await fetchRedisNodes(req());
  redisNodes.value = r.options;
  redisNodeError.value = r.error;
  redisNodeNames.value = new Map(r.options.map((o) => [o.id, o.name]));
}

async function loadRedisData(): Promise<void> {
  const mySeq = ++redisSeq.value;
  redisLoading.value = true;
  redisError.value = '';
  const r = await fetchRedisData(req(), redisFilter.value, redisNodeNames.value);
  if (mySeq !== redisSeq.value) return;
  redisRows.value = r.rows;
  redisChartRows.value = r.chartRows;
  redisTotal.value = r.total;
  redisError.value = r.error;
  redisLoading.value = false;
}

/** 筛选变化：合并补丁 + 页码归零 + 重查 */
function onRedisFilterChange(patch: Partial<RedisDataFilter>): void {
  redisFilter.value = { ...redisFilter.value, ...patch, pageIndex: 0 };
  void loadRedisData();
}

function onRedisPageChange(pageIndex: number): void {
  if (redisLoading.value || pageIndex < 0) return;
  redisFilter.value = { ...redisFilter.value, pageIndex };
  void loadRedisData();
}

/** 失败重试：候选与数据都重拉（候选可能是瞬时故障） */
function onRedisRetry(): void {
  void loadRedisNodes();
  void loadRedisData();
}

// ==================== MW-04 · Redis 消息队列 ====================
// 节点候选与 Redis 数据卡共享（redisNodes/redisNodeNames，不重复请求 NodeSearch）；数据独立一份 seq 竞态防护。
// 追踪列（CARD-MW-04 裁定 1）：命名路由 ConsoleTrace（/trace/:traceId）——禁路径跳转（重定向打分撞 /Trace/:id
// → RangeError 无限递归深链白屏，routes.ts:28 方案 A 铁律）。

const queueFilter = ref<RedisQueueFilter>(emptyRedisQueueFilter());
const queueRows = ref<RedisQueueRow[]>([]);
const queueTotal = ref(0);
const queueLoading = ref(true);
const queueError = ref('');
const queueSeq = ref(0);

async function loadRedisQueues(): Promise<void> {
  const mySeq = ++queueSeq.value;
  queueLoading.value = true;
  queueError.value = '';
  const r = await fetchRedisQueues(req(), queueFilter.value, redisNodeNames.value);
  if (mySeq !== queueSeq.value) return;
  queueRows.value = r.rows;
  queueTotal.value = r.total;
  queueError.value = r.error;
  queueLoading.value = false;
}

function onQueueFilterChange(patch: Partial<RedisQueueFilter>): void {
  queueFilter.value = { ...queueFilter.value, ...patch, pageIndex: 0 };
  void loadRedisQueues();
}

function onQueuePageChange(pageIndex: number): void {
  if (queueLoading.value || pageIndex < 0) return;
  queueFilter.value = { ...queueFilter.value, pageIndex };
  void loadRedisQueues();
}

function onQueueRetry(): void {
  void loadRedisQueues();
}

function onQueueTrace(traceId: string): void {
  void router.push({ name: 'ConsoleTrace', params: { traceId } });
}

// ==================== MW-05 · Redis 节点 ====================
// 裁定②：Lov 枢纽=NodeSearch（fetchRedisNodes 共享）；mw-redis-node-lov data-spec 已实挂两消费卡节点下拉
// （RedisDataCard/RedisQueueCard，2026-11-11 task-43 落毕；统一登记本卡，消费卡引用不重复定义）。
// 裁定①：projectId 顶栏透传=全局拦截器机制性满足（stardustContext.ts ensureStardustContextInterceptor，
// >0 才注入；后端 Search 不读 projectId=规范透传，alarms.logic withProject 先例）。
// 裁定③：双下钻=组件状态 redisId（Redis 数据卡/Redis 队列卡筛选切换），不跳路由。
// 裁定④：CRUD 全量——新增/编辑双模抽屉（RedisNodeDrawer）+ 删除 GuardModal 二次确认。

const redisNodeFilter = ref<RedisNodeFilter>(emptyRedisNodeFilter());
const redisNodeRows = ref<RedisNodeRow[]>([]);
const redisNodeTotal = ref(0);
const redisNodeListLoading = ref(true);
const redisNodeListError = ref('');
const redisNodeSeq = ref(0);
/** 正在刷新的节点 Id（行内按钮转圈+禁用，§3.5） */
const redisRefreshingId = ref(0);
/** 分类下拉候选：从当前列表行聚合（GetCategoryList 为字段缓存无 WebApi 端点，RedisQueue 先例） */
const redisNodeCategories = computed(() =>
  [...new Set(redisNodeRows.value.map((r) => r.category).filter((c) => c))].sort(),
);

async function loadRedisNodeList(): Promise<void> {
  const mySeq = ++redisNodeSeq.value;
  redisNodeListLoading.value = true;
  redisNodeListError.value = '';
  const r = await fetchRedisNodeList(req(), redisNodeFilter.value);
  if (mySeq !== redisNodeSeq.value) return;
  redisNodeRows.value = r.rows;
  redisNodeTotal.value = r.total;
  redisNodeListError.value = r.error;
  redisNodeListLoading.value = false;
}

function onRedisNodeFilterChange(patch: Partial<RedisNodeFilter>): void {
  redisNodeFilter.value = { ...redisNodeFilter.value, ...patch, pageIndex: 0 };
  void loadRedisNodeList();
}

function onRedisNodePageChange(pageIndex: number): void {
  if (redisNodeListLoading.value || pageIndex < 0) return;
  redisNodeFilter.value = { ...redisNodeFilter.value, pageIndex };
  void loadRedisNodeList();
}

function onRedisNodeRetry(): void {
  void loadRedisNodeList();
}

/** 手动刷新（C04）：GET Refresh，§3.5——保底 300ms + toast 透传「刷新[node]成功！」+ 重取列表 */
async function onRedisNodeRefresh(row: RedisNodeRow): Promise<void> {
  if (redisRefreshingId.value) return;
  redisRefreshingId.value = row.id;
  try {
    const [r] = await Promise.all([refreshRedisNode(req(), row.id), sleep(300)]);
    toast(r.message || (r.ok ? `节点「${row.name || row.id}」已刷新` : '刷新失败'), r.ok ? 'ok' : 'err');
    if (r.ok) void loadRedisNodeList();
  } catch (e) {
    toast(`刷新节点「${row.name || row.id}」失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  } finally {
    redisRefreshingId.value = 0;
  }
}

/** 监控下钻（裁定③，C03）：上方 Redis 数据卡切到该节点并重查（组件状态带 redisId） */
function onRedisNodeMonitor(row: RedisNodeRow): void {
  redisFilter.value = { ...emptyRedisDataFilter(), redisId: row.id };
  void loadRedisData();
  toast(`已切到节点「${row.name || row.id}」的监控数据`, 'ok');
}

/** 队列下钻（裁定③，C03）：上方 Redis 队列卡切到该节点并重查（组件状态带 redisId） */
function onRedisNodeQueue(row: RedisNodeRow): void {
  queueFilter.value = { ...emptyRedisQueueFilter(), redisId: row.id };
  void loadRedisQueues();
  toast(`已切到节点「${row.name || row.id}」的消息队列`, 'ok');
}

// ---- 新增/编辑抽屉（裁定④） + 删除 GuardModal ----

const redisDrawerMode = ref<'create' | 'edit'>('edit');
const redisDrawerOpen = ref(false);
const redisForm = ref<RedisNodeForm | null>(null);
const redisDetail = ref<Rec | null>(null);
const redisBusy = ref(false);
const redisDetailFailed = ref(false);
/** GuardModal 删除确认开关（生产环境二次确认，§11） */
const redisDeleteOpen = ref(false);
const redisDeleting = ref(false);

/** 新增（裁定④）：空 Detail 默认表单，启用+自动扫描缺省打开 */
function onRedisNodeCreate(): void {
  redisDrawerMode.value = 'create';
  redisDetail.value = null;
  redisDetailFailed.value = false;
  redisForm.value = { ...nodeDetailToRedisForm({}), enable: true };
  redisDrawerOpen.value = true;
}

async function onRedisNodeEdit(row: RedisNodeRow): Promise<void> {
  redisDrawerMode.value = 'edit';
  redisDetail.value = null;
  redisForm.value = null;
  redisDetailFailed.value = false;
  redisDrawerOpen.value = true;
  const d = await fetchRedisNodeDetail(req(), row.id);
  if (!d) {
    redisDetailFailed.value = true;
    return;
  }
  redisDetail.value = d;
  redisForm.value = nodeDetailToRedisForm(d);
}

async function onRedisNodeSubmit(form: RedisNodeForm): Promise<void> {
  if (redisBusy.value) return;
  redisBusy.value = true;
  try {
    const r =
      redisDrawerMode.value === 'create'
        ? await createRedisNode(req(), form)
        : await submitRedisNode(req(), redisDetail.value ?? {}, form);
    toast(r.message || (r.ok ? '节点已保存' : '保存失败'), r.ok ? 'ok' : 'err');
    if (r.ok) {
      redisDrawerOpen.value = false;
      void loadRedisNodeList();
    }
  } finally {
    redisBusy.value = false;
  }
}

function onRedisNodeDrawerClose(): void {
  if (redisBusy.value || redisDeleting.value) return;
  redisDrawerOpen.value = false;
}

/** 抽屉「删除」→ 弹 GuardModal（真正执行在确认回调） */
function onRedisNodeDelete(): void {
  redisDeleteOpen.value = true;
}

/** GuardModal 正文：写明删除对象（§11：提示要写明对象；Detail 未载入时用兜底文案） */
const redisNodeDeleteLabel = computed(() => {
  const d = redisDetail.value;
  const name = d ? String(d.Name ?? d.name ?? '') : '';
  return name
    ? `即将删除节点「${name}」，删除后该节点的监控与队列数据将不再展示。`
    : '即将删除该节点，删除后该节点的监控与队列数据将不再展示。';
});

async function onRedisNodeDeleteConfirm(): Promise<void> {
  const target = redisDetail.value;
  const id = target ? Number(target.Id ?? target.id) : 0;
  if (!id || redisDeleting.value) {
    redisDeleteOpen.value = false;
    return;
  }
  redisDeleting.value = true;
  try {
    const r = await deleteRedisNode(req(), id);
    toast(r.message || (r.ok ? '节点已删除' : '删除失败'), r.ok ? 'ok' : 'err');
    redisDeleteOpen.value = false;
    if (r.ok) {
      redisDrawerOpen.value = false;
      void loadRedisNodeList();
    }
  } finally {
    redisDeleting.value = false;
  }
}

onMounted(() => {
  void loadNodes().then(loadData);
  void loadNodeList();
  void loadRedisNodes().then(() => {
    void loadRedisData();
    void loadRedisQueues();
  });
  void loadRedisNodeList();
});
</script>

<template>
  <div data-spec="middleware-tab">
    <MySqlDataCard
      :rows="rows"
      :loading="loading"
      :error="error"
      :total="total"
      :page="filter.pageIndex"
      :page-size="filter.pageSize"
      :nodes="nodes"
      @filter-change="onFilterChange"
      @page-change="onPageChange"
      @retry="onRetry"
    />
    <div
      v-if="nodeError"
      data-spec="middleware-node-error"
      class="mt-[6px] px-[2px] text-[11.5px] text-[var(--el-color-warning)]"
    >
      节点候选加载失败：{{ nodeError }}（仍可按时间/名称筛选）
    </div>
    <MySqlNodeCard
      class="mt-[16px]"
      :rows="nodeRows"
      :loading="nodeLoading"
      :error="nodeListError"
      :total="nodeTotal"
      :page="nodeFilter.pageIndex"
      :page-size="nodeFilter.pageSize"
      :refreshing-id="refreshingId"
      @filter-change="onNodeFilterChange"
      @page-change="onNodePageChange"
      @retry="onNodeRetry"
      @edit="onNodeEdit"
      @monitor="onNodeMonitor"
      @refresh="onNodeRefresh"
    />
    <MySqlNodeDrawer
      :open="drawerOpen"
      :form="nodeForm"
      :busy="nodeBusy"
      :failed="nodeDetailFailed"
      @close="onNodeDrawerClose"
      @submit="onNodeSubmit"
    />
    <RedisDataCard
      class="mt-[16px]"
      :rows="redisRows"
      :chart-rows="redisChartRows"
      :node-selected="redisFilter.redisId > 0"
      :loading="redisLoading"
      :error="redisError"
      :total="redisTotal"
      :page="redisFilter.pageIndex"
      :page-size="redisFilter.pageSize"
      :nodes="redisNodes"
      @filter-change="onRedisFilterChange"
      @page-change="onRedisPageChange"
      @retry="onRedisRetry"
    />
    <div
      v-if="redisNodeError"
      data-spec="middleware-redis-node-error"
      class="mt-[6px] px-[2px] text-[11.5px] text-[var(--el-color-warning)]"
    >
      Redis 节点候选加载失败：{{ redisNodeError }}（仍可按时间/名称筛选）
    </div>
    <RedisQueueCard
      class="mt-[16px]"
      :rows="queueRows"
      :loading="queueLoading"
      :error="queueError"
      :total="queueTotal"
      :page="queueFilter.pageIndex"
      :page-size="queueFilter.pageSize"
      :nodes="redisNodes"
      @filter-change="onQueueFilterChange"
      @page-change="onQueuePageChange"
      @retry="onQueueRetry"
      @trace="onQueueTrace"
    />
    <RedisNodeCard
      class="mt-[16px]"
      :rows="redisNodeRows"
      :loading="redisNodeListLoading"
      :error="redisNodeListError"
      :total="redisNodeTotal"
      :page="redisNodeFilter.pageIndex"
      :page-size="redisNodeFilter.pageSize"
      :refreshing-id="redisRefreshingId"
      :categories="redisNodeCategories"
      @filter-change="onRedisNodeFilterChange"
      @page-change="onRedisNodePageChange"
      @retry="onRedisNodeRetry"
      @create="onRedisNodeCreate"
      @edit="onRedisNodeEdit"
      @monitor="onRedisNodeMonitor"
      @queue="onRedisNodeQueue"
      @refresh="onRedisNodeRefresh"
    />
    <RedisNodeDrawer
      :mode="redisDrawerMode"
      :open="redisDrawerOpen"
      :form="redisForm"
      :busy="redisBusy"
      :failed="redisDetailFailed"
      :deleting="redisDeleting"
      @close="onRedisNodeDrawerClose"
      @submit="onRedisNodeSubmit"
      @delete="onRedisNodeDelete"
    />
    <GuardModal
      v-if="redisDeleteOpen"
      label="删除 Redis 节点"
      :detail="redisNodeDeleteLabel"
      @cancel="redisDeleteOpen = false"
      @confirm="onRedisNodeDeleteConfirm"
    />
  </div>
</template>
