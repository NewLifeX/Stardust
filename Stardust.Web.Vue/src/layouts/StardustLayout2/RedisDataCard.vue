<script setup lang="ts">
/**
 * RedisDataCard：治理页「中间件」页签——Redis 数据区块（CARD-MW-03，形态 1 区块卡）。
 * 纯 UI：状态（rows/chartRows/loading/error/total/page/nodes/nodeSelected）由容器持有；只发 filterChange/pageChange/retry。
 * 八列（对齐迁移页，C02）：Redis 节点 / 名称 / 速度 / 连接数 / 已用内存 / Keys 数 / 命令数 / 创建时间
 * （Id/TopCommand 服务端已静态隐藏，本列集亦不含）。
 * 曲线区（C03 定稿裁定②）：选中节点时 ECharts 六序列（速度/入流量/出流量/连接数/已用内存/Keys 数，不含 Commands），
 * X=采样时间；未选节点 → 提示「选择节点后查看性能曲线」；选中无采样 → 提示「暂无采样数据」（B1 空态一等公民）。
 * 只读浏览（C05）：无新增/编辑/删除按钮。
 * 三态：加载「Redis 数据加载中…」/ 失败「Redis 数据加载失败：{原因}」+重试 / 空「暂无 Redis 采样数据」。
 * 筛选条（C04）：节点下拉（NodeSearch Lov 数据源）/ 时间区间（注记「按采样时间过滤」）/ 名称关键字；
 * @change 即查，无「查询」按钮。**请求参数绝不携带 pageSize**（C04 定稿裁定①，逻辑层硬负向守护）。
 */
import { computed } from 'vue';
import EChart from '@/components/EChart.vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { buildRedisDataChartOption, redisDataTotalPages, type RedisDataRow, type RedisNodeOption } from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: RedisDataRow[];
    /** 曲线数据源（选中节点=全量行；无节点=空） */
    chartRows?: RedisDataRow[];
    /** 是否选中节点（决定曲线区形态：未选=引导文案；选中无数据=B1 空态） */
    nodeSelected?: boolean;
    loading?: boolean;
    error?: string;
    total?: number;
    /** 当前页，从 0 起 */
    page?: number;
    pageSize?: number;
    /** 节点候选（筛选条数据源），来自 fetchRedisNodes（NodeSearch Lov） */
    nodes?: RedisNodeOption[];
  }>(),
  { chartRows: () => [], nodeSelected: false, loading: false, error: '', total: 0, page: 0, pageSize: 20, nodes: () => [] },
);

const emit = defineEmits<{
  /** 筛选变化（部分补，容器持有 filter 并重取，pageIndex 归零） */
  filterChange: [patch: { redisId?: number; dtStart?: string; dtEnd?: string; q?: string }];
  /** 翻页（容器改 pageIndex 后重取；选中节点=本地切片，无节点=服务端分页） */
  pageChange: [pageIndex: number];
  /** 失败态重试（容器重取数据接口） */
  retry: [];
}>();

const totalPages = computed(() => redisDataTotalPages(props.total, props.pageSize));
const hasPager = computed(() => props.total > props.pageSize && !props.loading && !props.error);
const chartOption = computed<Record<string, unknown>>(() => buildRedisDataChartOption(props.chartRows));

function onNode(e: Event): void {
  emit('filterChange', { redisId: Number((e.target as HTMLSelectElement).value) || 0 });
}
function onDtStart(e: Event): void {
  emit('filterChange', { dtStart: (e.target as HTMLInputElement).value });
}
function onDtEnd(e: Event): void {
  emit('filterChange', { dtEnd: (e.target as HTMLInputElement).value });
}
function onQ(e: Event): void {
  emit('filterChange', { q: (e.target as HTMLInputElement).value.trim() });
}
</script>

<template>
  <Card
    padding="none"
    data-spec="mw-redis-data"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        Redis 数据
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">共 {{ total }} 条</span>
      </h3>
    </div>

    <!-- 筛选条：加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="mw-redis-data-filter"
    >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">节点</span>
      <select
        :class="SELECT"
        aria-label="按 Redis 节点筛选"
        data-spec="mw-redis-node-lov"
        @change="onNode"
      >
        <option value="">
          全部节点
        </option>
        <option
          v-for="n in nodes"
          :key="n.id"
          :value="n.id"
          :title="n.server"
        >
          {{ n.name }}
        </option>
      </select>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="开始日期"
        data-spec="mw-redis-data-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="mw-redis-data-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-data-filter-time-note"
      >按采样时间过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="名称"
        :class="SELECT"
        aria-label="按名称关键字筛选"
        data-spec="mw-redis-data-filter-q"
        @change="onQ"
      >
    </div>

    <!-- 曲线区（C03）：未选节点=引导文案；选中=六序列曲线或 B1 空态 -->
    <div
      v-if="!loading && !error"
      class="border-t border-[var(--el-border-color-lighter)] px-[14px] py-[12px]"
      data-spec="mw-redis-data-chart"
    >
      <div class="mb-[6px] text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
        性能曲线
      </div>
      <EChart
        v-if="nodeSelected && chartRows.length"
        :option="chartOption"
        height="240px"
        data-spec="mw-redis-data-chart-canvas"
      />
      <div
        v-else
        data-spec="mw-redis-data-chart-empty"
        class="flex h-[240px] items-center justify-center rounded-[8px] border border-dashed border-[var(--el-border-color-lighter)] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        {{ nodeSelected ? '暂无采样数据' : '选择节点后查看性能曲线' }}
      </div>
    </div>

    <div
      v-if="loading"
      data-spec="mw-redis-data-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      Redis 数据加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="mw-redis-data-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        Redis 数据加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="mw-redis-data-retry"
        class="mt-[8px] cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[10px] py-[4px] text-[12px] text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]"
        @click="emit('retry')"
      >
        重试
      </button>
    </div>

    <table
      v-else
      class="w-full border-collapse"
    >
      <thead>
        <tr>
          <th :class="TH">
            Redis 节点
          </th>
          <th :class="TH">
            名称
          </th>
          <th :class="[TH, 'text-right']">
            速度
          </th>
          <th :class="[TH, 'text-right']">
            连接数
          </th>
          <th :class="[TH, 'text-right']">
            已用内存
          </th>
          <th :class="[TH, 'text-right']">
            Keys 数
          </th>
          <th :class="[TH, 'text-right']">
            命令数
          </th>
          <th :class="[TH, 'text-right']">
            创建时间
          </th>
        </tr>
      </thead>
      <tbody class="group">
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="mw-redis-data-row"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="TD">
            <span
              v-if="r.nodeName || r.redisId > 0"
              class="text-[12.5px] text-[var(--el-text-color-primary)]"
            >{{ r.nodeName || `#${r.redisId}` }}</span>
            <span
              v-else
              class="text-[12.5px] text-[var(--el-text-color-secondary)]"
            >—</span>
          </td>
          <td :class="TD">
            <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
              {{ r.name || '—' }}
            </div>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.speed }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.connections }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.usedMemory }} MB</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.keys }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.commands }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">{{ r.sampledAt }}</span>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="8"
            data-spec="mw-redis-data-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>暂无 Redis 采样数据</div>
            <div class="mt-[4px] text-[11.5px]">
              Redis 节点接入采集后，状态采样会在这里展示
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- 分页：无节点=服务端分页；选中节点=1440 行本地切片 -->
    <div
      v-if="hasPager"
      class="flex items-center justify-end gap-[10px] px-[14px] pb-[12px] pt-[6px]"
      data-spec="mw-redis-data-pager"
    >
      <span
        class="text-[11.5px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-data-page"
      >第 {{ page + 1 }} / {{ totalPages }} 页</span>
      <Btn
        size="sm"
        variant="default"
        :disabled="page <= 0"
        data-spec="mw-redis-data-prev"
        @click="emit('pageChange', page - 1)"
      >
        上一页
      </Btn>
      <Btn
        size="sm"
        variant="default"
        :disabled="page + 1 >= totalPages"
        data-spec="mw-redis-data-next"
        @click="emit('pageChange', page + 1)"
      >
        下一页
      </Btn>
    </div>
  </Card>
</template>
