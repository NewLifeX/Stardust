<script setup lang="ts">
/**
 * Nodes/NodeData/Index — 节点性能时序数据（自定义迁移页 + ECharts 曲线）
 *
 * MVC 源：隐藏页 [Menu(60,false)]，原 cshtml 用 ECharts 渲染节点性能时序曲线，后端迁移时已删除绘图。
 * 前端自建视图兜底（框架 DefaultEntity 已支持无菜单时按视图目录兜底），#/Nodes/NodeData 可访问。
 *
 * 数据：GET /Nodes/NodeData（GetPage，后端 Search 支持 nodeId/dtStart/dtEnd，默认按 Id 降序），
 *       行数据即图表数据源。字段见 Stardust.Data.Nodes.NodeData：
 *       NodeID / Name / LocalTime(采集时间) /
 *       SystemLoad(系统负载) / CpuRate(CPU率) /
 *       AvailableMemory(可用内存 M) / FreeMemory(空闲内存 M) / AvailableFreeSpace(可用磁盘 M) /
 *       DiskIOPS(磁盘IOPS) / DiskActiveTime(磁盘活动%) /
 *       Temperature(温度) / Battery(电量%) / Signal(信号) /
 *       UplinkSpeed(上行速度 B/s) / DownlinkSpeed(下行速度 B/s) /
 *       ProcessCount(进程数) / TcpConnections(连接数) / TcpTimeWait / TcpCloseWait /
 *       IntranetScore / InternetScore / GatewayLatency / GatewayLossRate /
 *       DnsLatency / DnsLossRate / ServerLatency / ServerLossRate /
 *       Delay / Offset / Uptime / TraceId / Creator / CreateTime / CreateIP
 *
 * 图表：X=LocalTime（采集时间），Y=CPU率/内存/磁盘/网络/温度 多条曲线（line）。
 *       种子无数据时显示空状态提示。
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径（request 自动拼 baseURL）。
 */
import { ref, computed, onMounted } from 'vue';
import EChart from '@/components/EChart.vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Nodes', 'NodeData');

interface Row {
  Id?: number;
  NodeID?: number;
  NodeName?: string;
  Name?: string;
  LocalTime?: string;
  SystemLoad?: number;
  CpuRate?: number;
  AvailableMemory?: number;
  FreeMemory?: number;
  AvailableFreeSpace?: number;
  DiskIOPS?: number;
  DiskActiveTime?: number;
  Temperature?: number;
  Battery?: number;
  Signal?: number;
  UplinkSpeed?: number;
  DownlinkSpeed?: number;
  ProcessCount?: number;
  TcpConnections?: number;
  TcpTimeWait?: number;
  TcpCloseWait?: number;
  IntranetScore?: number;
  InternetScore?: number;
  Delay?: number;
  Offset?: number;
  UptimeName?: string;
  TraceId?: string;
  Creator?: string;
  CreateTime?: string;
  CreateIP?: string;
  [k: string]: unknown;
}

/** 标准列表列（与后端 NodeData GetPage 的 ListFields 完全对齐，MVC 同源） */
type Col = {
  name: string;
  label: string;
  minWidth?: number;
  align?: 'left' | 'center' | 'right';
  itemType?: 'percent' | 'GMK' | 'TimeSpan' | '速度';
};
const ALL_COLUMNS: Col[] = [
  { name: 'NodeName', label: '节点', minWidth: 140 },
  { name: 'Name', label: '名称', minWidth: 160 },
  { name: 'SystemLoad', label: '系统负载', minWidth: 90, align: 'right', itemType: 'percent' },
  { name: 'CpuRate', label: 'CPU率', minWidth: 90, align: 'right', itemType: 'percent' },
  { name: 'AvailableMemory', label: '可用内存(M)', minWidth: 100, align: 'right', itemType: 'GMK' },
  { name: 'FreeMemory', label: '空闲内存(M)', minWidth: 100, align: 'right', itemType: 'GMK' },
  { name: 'AvailableFreeSpace', label: '可用磁盘(M)', minWidth: 100, align: 'right', itemType: 'GMK' },
  { name: 'DiskIOPS', label: '磁盘IOPS', minWidth: 90, align: 'right' },
  { name: 'DiskActiveTime', label: '磁盘活动', minWidth: 90, align: 'right', itemType: 'percent' },
  { name: 'Temperature', label: '温度', minWidth: 80, align: 'right' },
  { name: 'Battery', label: '电量', minWidth: 80, align: 'right', itemType: 'percent' },
  { name: 'Signal', label: '信号', minWidth: 80, align: 'right' },
  { name: 'UplinkSpeed', label: '上行速度', minWidth: 100, align: 'right', itemType: '速度' },
  { name: 'DownlinkSpeed', label: '下行速度', minWidth: 100, align: 'right', itemType: '速度' },
  { name: 'ProcessCount', label: '进程数', minWidth: 90, align: 'right' },
  { name: 'TcpConnections', label: '连接数', minWidth: 90, align: 'right' },
  { name: 'TcpTimeWait', label: '主动关闭', minWidth: 90, align: 'right' },
  { name: 'TcpCloseWait', label: '被动关闭', minWidth: 90, align: 'right' },
  { name: 'IntranetScore', label: '内网质量', minWidth: 90, align: 'right', itemType: 'percent' },
  { name: 'InternetScore', label: '外网质量', minWidth: 90, align: 'right', itemType: 'percent' },
  { name: 'Delay', label: '延迟', minWidth: 80, align: 'right' },
  { name: 'Offset', label: '偏移', minWidth: 80, align: 'right' },
  { name: 'LocalTime', label: '本地时间', minWidth: 150 },
  { name: 'UptimeName', label: '开机时间', minWidth: 110, itemType: 'TimeSpan' },
  { name: 'TraceId', label: '追踪', minWidth: 200 },
  { name: 'Creator', label: '创建者', minWidth: 100 },
  { name: 'CreateTime', label: '创建时间', minWidth: 150 },
  { name: 'CreateIP', label: '创建地址', minWidth: 120 },
];

/** 搜索：nodeId（加分项）+ 时间范围 dtStart/dtEnd */
const nodeIdFilter = ref<number | null>(null);
const timeRange = ref<[string, string] | null>(null);

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const hasActiveFilter = computed(
  () => (nodeIdFilter.value != null && nodeIdFilter.value > 0) || timeRange.value != null,
);

const hasData = computed(() => rows.value.length > 0);

function formatTime(v: unknown): string {
  if (v == null) return '';
  return String(v).replace('T', ' ').slice(0, 19);
}

/** 字节/秒 → 自动单位（B/KiB/MiB/GiB） */
function formatSpeed(v: unknown): string {
  const n = Number(v ?? 0);
  if (!n) return '0';
  const units = ['B', 'KiB', 'MiB', 'GiB'];
  let i = 0;
  let val = n;
  while (val >= 1024 && i < units.length - 1) {
    val /= 1024;
    i++;
  }
  return `${val.toFixed(1)} ${units[i]}/s`;
}

/** 字节数 → 自动单位（B/KiB/MiB/GiB） */
function formatGMK(v: unknown): string {
  const n = Number(v ?? 0);
  if (!n) return '0';
  const units = ['B', 'KiB', 'MiB', 'GiB'];
  let i = 0;
  let val = n;
  while (val >= 1024 && i < units.length - 1) {
    val /= 1024;
    i++;
  }
  return `${val.toFixed(1)} ${units[i]}`;
}

/** 秒数 → 时分秒 */
function formatUptime(sec: number): string {
  if (!sec || sec <= 0) return '0';
  const d = Math.floor(sec / 86400);
  const h = Math.floor((sec % 86400) / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = Math.floor(sec % 60);
  const parts: string[] = [];
  if (d) parts.push(`${d}天`);
  if (h) parts.push(`${h}时`);
  if (m) parts.push(`${m}分`);
  if (s || parts.length === 0) parts.push(`${s}秒`);
  return parts.join('');
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'LocalTime' || key === 'CreateTime') return formatTime(v);
  if (key === 'UptimeName') return formatUptime(Number(v));
  if (key === 'UplinkSpeed' || key === 'DownlinkSpeed') return formatSpeed(v);
  if (key === 'CpuRate' || key === 'SystemLoad' || key === 'DiskActiveTime' || key === 'Battery' || key === 'IntranetScore' || key === 'InternetScore')
    return `${(Number(v) * 100).toFixed(1)}%`;
  if (key === 'AvailableMemory' || key === 'FreeMemory' || key === 'AvailableFreeSpace')
    return formatGMK(Number(v));
  return String(v);
}

/** ECharts option：X=LocalTime，Y=CPU率/内存/磁盘/网络/温度 多曲线（line） */
const chartOption = computed<Record<string, unknown>>(() => {
  // 时间升序展示（后端默认 Id 降序 = 最新在前）
  const ordered = [...rows.value].sort(
    (a, b) => String(a.LocalTime ?? '').localeCompare(String(b.LocalTime ?? '')),
  );
  const times = ordered.map((r) => formatTime(r.LocalTime));
  const systemLoad = ordered.map((r) => Number(r.SystemLoad ?? 0) * 100);
  const cpu = ordered.map((r) => Number(r.CpuRate ?? 0) * 100);
  const freeMem = ordered.map((r) => Number(r.FreeMemory ?? 0));
  const disk = ordered.map((r) => Number(r.AvailableFreeSpace ?? 0));
  const diskActive = ordered.map((r) => Number(r.DiskActiveTime ?? 0) * 100);
  const battery = ordered.map((r) => Number(r.Battery ?? 0) * 100);
  const up = ordered.map((r) => Number(r.UplinkSpeed ?? 0));
  const down = ordered.map((r) => Number(r.DownlinkSpeed ?? 0));
  const temp = ordered.map((r) => Number(r.Temperature ?? 0));
  const diskIops = ordered.map((r) => Number(r.DiskIOPS ?? 0));
  const tcp = ordered.map((r) => Number(r.TcpConnections ?? 0));
  const intranet = ordered.map((r) => Number(r.IntranetScore ?? 0) * 100);
  const internet = ordered.map((r) => Number(r.InternetScore ?? 0) * 100);

  return {
    tooltip: { trigger: 'axis' },
    legend: {
      data: [
        '系统负载(%)', 'CPU率(%)', '空闲内存(M)', '可用磁盘(M)', '磁盘活动(%)',
        '电量(%)', '上行(B/s)', '下行(B/s)', '温度', '磁盘IOPS', '连接数', '内网质量(%)', '外网质量(%)',
      ],
      bottom: 0,
      type: 'scroll',
    },
    grid: { left: 48, right: 24, top: 24, bottom: 56, containLabel: true },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: times,
      axisLabel: { color: '#606266' },
    },
    yAxis: {
      type: 'value',
      axisLabel: { color: '#606266' },
      splitLine: { lineStyle: { color: '#EBEDF0' } },
    },
    series: [
      { name: '系统负载(%)', type: 'line', smooth: true, showSymbol: false, data: systemLoad },
      { name: 'CPU率(%)', type: 'line', smooth: true, showSymbol: false, data: cpu },
      { name: '空闲内存(M)', type: 'line', smooth: true, showSymbol: false, data: freeMem },
      { name: '可用磁盘(M)', type: 'line', smooth: true, showSymbol: false, data: disk },
      { name: '磁盘活动(%)', type: 'line', smooth: true, showSymbol: false, data: diskActive },
      { name: '电量(%)', type: 'line', smooth: true, showSymbol: false, data: battery },
      { name: '上行(B/s)', type: 'line', smooth: true, showSymbol: false, data: up },
      { name: '下行(B/s)', type: 'line', smooth: true, showSymbol: false, data: down },
      { name: '温度', type: 'line', smooth: true, showSymbol: false, data: temp },
      { name: '磁盘IOPS', type: 'line', smooth: true, showSymbol: false, data: diskIops },
      { name: '连接数', type: 'line', smooth: true, showSymbol: false, data: tcp },
      { name: '内网质量(%)', type: 'line', smooth: true, showSymbol: false, data: intranet },
      { name: '外网质量(%)', type: 'line', smooth: true, showSymbol: false, data: internet },
    ],
  };
});

function buildParams(): Record<string, unknown> {
  const p: Record<string, unknown> = {
    pageIndex: page.value - 1,
    pageSize: pageSize.value,
  };
  if (nodeIdFilter.value != null && nodeIdFilter.value > 0) p.nodeId = nodeIdFilter.value;
  if (timeRange.value?.[0]) p.dtStart = timeRange.value[0];
  if (timeRange.value?.[1]) p.dtEnd = timeRange.value[1];
  return p;
}

async function fetchList() {
  loading.value = true;
  try {
    const res: any = await api.getList<Row>(buildParams());
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[NodeData] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}

function handleReset() {
  nodeIdFilter.value = null;
  timeRange.value = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  // 从 hash 读取 nodeId（来自其他页跳转）
  const m = location.hash.match(/[?&]nodeId=(\d+)/);
  if (m) nodeIdFilter.value = Number(m[1]);
  await fetchList();
});
</script>

<template>
  <div class="list-page node-data-page">
    <div class="lp-header">
      <h2 class="lph-title">
        节点性能时序数据
      </h2>
      <div class="lp-toolbar">
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
      </div>
    </div>

    <!-- 搜索栏：nodeId + 时间范围 -->
    <div class="lp-card-search">
      <div class="nd-search-grid">
        <div class="nd-search-field">
          <label class="nd-search-label">节点编号</label>
          <el-input
            v-model.number="nodeIdFilter"
            type="number"
            placeholder="NodeId"
            class="nd-search-input"
            clearable
          />
        </div>

        <div class="nd-search-field nd-search-field--wide">
          <label class="nd-search-label">采集时间</label>
          <el-date-picker
            v-model="timeRange"
            type="daterange"
            range-separator="~"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            value-format="YYYY-MM-DD"
            class="nd-search-input nd-search-datepicker"
          />
        </div>

        <div class="nd-search-actions">
          <el-button
            :class="{ 'is-active': hasActiveFilter }"
            @click="handleReset"
          >
            重置
          </el-button>
          <el-button
            type="primary"
            @click="handleSearch"
          >
            <el-icon><Search /></el-icon>搜索
          </el-button>
        </div>
      </div>
    </div>

    <!-- 图表卡片 -->
    <div class="lp-card">
      <div class="lp-card-header">
        <span class="lp-card-title">节点性能时序曲线</span>
        <span class="lp-card-sub">CPU率 / 内存 / 磁盘 / 网络 / 温度</span>
      </div>
      <div class="lp-card-body nd-chart-body">
        <EChart
          v-if="hasData"
          :option="chartOption"
          height="360px"
        />
        <el-empty
          v-else
          description="暂无性能数据"
        />
      </div>
    </div>

    <!-- 列表卡片 -->
    <div class="lp-card">
      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="nd-table"
          stripe
          border
          empty-text="暂无数据"
        >
          <el-table-column
            v-for="c in ALL_COLUMNS"
            :key="c.name"
            :prop="c.name"
            :label="c.label"
            :min-width="c.minWidth"
            :align="c.align || 'left'"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, c.name) }}</span>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <div class="lp-card-footer">
        <el-pagination
          :current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next, sizes"
          :page-sizes="[10, 20, 50, 100]"
          @current-change="onPageChange"
          @size-change="
            (s: number) => {
              pageSize = s;
              page = 1;
              fetchList();
            }
          "
        />
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.node-data-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: auto;

  .lp-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .lph-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .lp-toolbar {
      display: flex;
      gap: 8px;
    }
  }

  .lp-card-search {
    margin: 16px var(--el-main-padding, 24px) 0;
    padding: 16px 20px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
  }

  .lp-card {
    margin: 16px var(--el-main-padding, 24px);
    padding: 16px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: hidden;
  }

  .lp-card-header {
    display: flex;
    align-items: baseline;
    gap: 12px;
    padding: 0 4px 12px;
    border-bottom: 1px solid var(--el-border-color-light);

    .lp-card-title {
      font-size: 15px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .lp-card-sub {
      font-size: 12px;
      color: var(--el-text-color-secondary);
    }
  }

  .lp-card-body {
    padding-top: 16px;
    min-height: 200px;
  }

  .nd-chart-body {
    min-height: 360px;
    display: flex;
    align-items: stretch;
  }

  .lp-card-footer {
    padding: 12px 4px 0;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.nd-search-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px 16px;
  align-items: end;
}

.nd-search-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;

  &--wide {
    grid-column: span 2;
  }
}

.nd-search-label {
  font-size: 12px;
  font-weight: 500;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.nd-search-input {
  width: 100%;

  :deep(.el-input__wrapper),
  :deep(.el-range-editor) {
    min-height: 36px;
    border-radius: var(--el-border-radius-base);
    background: var(--el-bg-color);
    border: 1px solid var(--el-border-color-light);
    box-shadow: none;
    transition:
      border-color 0.15s ease,
      background 0.15s ease,
      box-shadow 0.15s ease;
  }

  :deep(.el-input__wrapper.is-focus),
  :deep(.el-range-editor.is-active) {
    background: var(--el-fill-color-blank);
    border-color: var(--el-color-primary);
    box-shadow:
      0 0 0 1px var(--el-color-primary) inset,
      0 0 0 3px var(--el-color-primary-light-8);
  }

  :deep(.el-input__inner) {
    font-size: 13px;
    color: var(--el-text-color-primary);
  }
}

.nd-search-datepicker {
  :deep(.el-range-editor) {
    width: 100%;
  }
}

.nd-search-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-bottom: 0;
}

.nd-table {
  width: 100%;
}

@media (max-width: 900px) {
  .nd-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .nd-search-grid {
    grid-template-columns: 1fr;
  }

  .nd-search-field--wide {
    grid-column: span 1;
  }
}
</style>
