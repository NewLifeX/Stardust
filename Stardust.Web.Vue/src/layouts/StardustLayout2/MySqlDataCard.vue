<script setup lang="ts">
/**
 * MySqlDataCard：治理页「中间件」页签——MySQL 数据区块（CARD-MW-01，形态 1 区块卡）。
 * 纯 UI：状态（rows/loading/error/total/page/filter/nodes）由容器持有；只发 filterChange/pageChange/retry。
 * 12 列：编号（雪花 Id）/ MySQL 节点（候选映射，缺「—」）/ 名称 / 启动时长 / 连接数（x / y）/
 * QPS / TPS / 流量（收 / 发）/ InnoDB 缓冲池 / 命中率 / 慢查询 / 线程（总 / 活跃）。
 * 只读浏览（C02）：无新增/编辑/删除按钮；导出不在此卡（定稿裁定）。
 * 三态：加载「MySQL 数据加载中…」/ 失败「MySQL 数据加载失败：{原因}」+重试 /
 * 空「暂无 MySQL 采样数据」——本地库常态即空（B1 一等公民），引导语=等节点上报。
 * 筛选条（C01-C04）：节点下拉 / 时间区间（注记「按采样时间过滤」）/ 名称关键字；@change 即查，无「查询」按钮。
 * 分页：服务端分页（pageIndex 从 0），上一页/下一页 + 页码；total<=pageSize 时整行隐藏。
 */
import { computed } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { mysqlDataTotalPages, type MySqlDataRow, type MySqlNodeOption } from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: MySqlDataRow[];
    loading?: boolean;
    error?: string;
    total?: number;
    /** 当前页，从 0 起 */
    page?: number;
    pageSize?: number;
    /** 节点候选（筛选条数据源），来自 fetchMySqlNodes */
    nodes?: MySqlNodeOption[];
  }>(),
  { loading: false, error: '', total: 0, page: 0, pageSize: 20, nodes: () => [] },
);

const emit = defineEmits<{
  /** 筛选变化（部分补，容器持有 filter 并重取，pageIndex 归零） */
  filterChange: [patch: { mysqlId?: number; dtStart?: string; dtEnd?: string; q?: string }];
  /** 翻页（容器改 pageIndex 后重取） */
  pageChange: [pageIndex: number];
  /** 失败态重试（容器重取数据接口） */
  retry: [];
}>();

const totalPages = computed(() => mysqlDataTotalPages(props.total, props.pageSize));
const hasPager = computed(() => props.total > props.pageSize && !props.loading && !props.error);

function onNode(e: Event): void {
  emit('filterChange', { mysqlId: Number((e.target as HTMLSelectElement).value) || 0 });
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
    data-spec="mw-mysql-data"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        MySQL 数据
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">共 {{ total }} 条</span>
      </h3>
    </div>

    <!-- 筛选条：加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="mw-mysql-data-filter"
    >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">节点</span>
      <select
        :class="SELECT"
        aria-label="按 MySQL 节点筛选"
        data-spec="mw-mysql-data-filter-node"
        @change="onNode"
      >
        <option value="">
          全部节点
        </option>
        <option
          v-for="n in nodes"
          :key="n.id"
          :value="n.id"
        >
          {{ n.name }}
        </option>
      </select>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="开始日期"
        data-spec="mw-mysql-data-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="mw-mysql-data-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-mysql-data-filter-time-note"
      >按采样时间过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="名称"
        :class="SELECT"
        aria-label="按名称关键字筛选"
        data-spec="mw-mysql-data-filter-q"
        @change="onQ"
      >
    </div>

    <div
      v-if="loading"
      data-spec="mw-mysql-data-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      MySQL 数据加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="mw-mysql-data-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        MySQL 数据加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="mw-mysql-data-retry"
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
            编号
          </th>
          <th :class="TH">
            MySQL 节点
          </th>
          <th :class="TH">
            名称
          </th>
          <th :class="[TH, 'text-right']">
            启动时长
          </th>
          <th :class="[TH, 'text-right']">
            连接数
          </th>
          <th :class="[TH, 'text-right']">
            QPS
          </th>
          <th :class="[TH, 'text-right']">
            TPS
          </th>
          <th :class="[TH, 'text-right']">
            流量
          </th>
          <th :class="[TH, 'text-right']">
            InnoDB 缓冲池
          </th>
          <th :class="[TH, 'text-right']">
            命中率
          </th>
          <th :class="[TH, 'text-right']">
            慢查询
          </th>
          <th :class="[TH, 'text-right']">
            线程
          </th>
        </tr>
      </thead>
      <tbody class="group">
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="mw-mysql-data-row"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="TD">
            <span
              class="font-mono text-[12px] text-[var(--el-text-color-secondary)]"
              :title="r.id"
            >{{ r.id }}</span>
          </td>
          <td :class="TD">
            <span
              v-if="r.nodeName || r.mysqlId > 0"
              class="text-[12.5px] text-[var(--el-text-color-primary)]"
            >{{ r.nodeName || `#${r.mysqlId}` }}</span>
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
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.uptime }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.connections }} / {{ r.maxConnections }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.qps }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.tps }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12px] text-[var(--el-text-color-primary)]">{{ r.bytesIn }} / {{ r.bytesOut }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12px] text-[var(--el-text-color-primary)]">{{ r.bufferPool }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.hitRate }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.slowQueries }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.threads }} / {{ r.threadsRunning }}</span>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="12"
            data-spec="mw-mysql-data-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>暂无 MySQL 采样数据</div>
            <div class="mt-[4px] text-[11.5px]">
              MySQL 节点接入采集后，状态采样会在这里展示
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- 分页：仅服务端有更多数据时显示 -->
    <div
      v-if="hasPager"
      class="flex items-center justify-end gap-[10px] px-[14px] pb-[12px] pt-[6px]"
      data-spec="mw-mysql-data-pager"
    >
      <span
        class="text-[11.5px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-mysql-data-page"
      >第 {{ page + 1 }} / {{ totalPages }} 页</span>
      <Btn
        size="sm"
        variant="default"
        :disabled="page <= 0"
        data-spec="mw-mysql-data-prev"
        @click="emit('pageChange', page - 1)"
      >
        上一页
      </Btn>
      <Btn
        size="sm"
        variant="default"
        :disabled="page + 1 >= totalPages"
        data-spec="mw-mysql-data-next"
        @click="emit('pageChange', page + 1)"
      >
        下一页
      </Btn>
    </div>
  </Card>
</template>
