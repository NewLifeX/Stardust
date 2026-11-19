<script setup lang="ts">
/**
 * RedisQueueCard：治理页「中间件」页签——Redis 消息队列区块（CARD-MW-04，形态 1 区块卡）。
 * 纯 UI：状态（rows/loading/error/total/page/pageSize/nodes）由容器持有；只发 filterChange/pageChange/retry/trace。
 * 列集（BL-7，对齐迁移页核心列，扩展列 Id/CreateUser/CreateIP/UpdateUser/UpdateIP/Remark 裁剪，WebHook 服务端已 RemoveField）：
 * Redis 节点 / 名称 / 分类 / Db / 类型 / 消费组 / 消费者 / 总消费 / 消息数 / 最大积压 / 启用 / 最早消费 / 最后活跃 / 追踪 / 更新时间。
 * 追踪列（定稿裁定 1）：有 TraceId → 按钮「查看」发 trace 事件（容器命名路由 ConsoleTrace，禁路径跳转）；
 * 无 TraceId → 按钮禁用 + title 原因（§11：禁用原因在视线内）。
 * 审计列（定稿裁定 3）：平台【缺·待接入】，不做假入口——本卡不渲染审计按钮。
 * 三态：加载「Redis 队列加载中…」/ 失败「Redis 队列加载失败：{原因}」+重试 / 空「暂无 Redis 队列数据」（C05 空态一等公民）。
 * 筛选条（BL-6 四控件）：节点下拉（NodeSearch Lov 共享候选）/ 分类下拉（前端从当前列表聚合，后端无独立 action，旧页先例）/
 * 更新时间区间（Biz Search=UpdateTime Between，注记按实写）/ 关键字（Q 透传七字段 contains）；@change 即查，无「查询」按钮。
 * 标准服务端分页：pageIndex+pageSize 恒发（无 RedisData 1440 魔法）。
 */
import { computed } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { redisQueueTotalPages, type RedisNodeOption, type RedisQueueRow } from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: RedisQueueRow[];
    loading?: boolean;
    error?: string;
    total?: number;
    /** 当前页，从 0 起 */
    page?: number;
    pageSize?: number;
    /** 节点候选（筛选条数据源），与 Redis 数据卡共享 fetchRedisNodes 结果 */
    nodes?: RedisNodeOption[];
  }>(),
  { loading: false, error: '', total: 0, page: 0, pageSize: 20, nodes: () => [] },
);

const emit = defineEmits<{
  /** 筛选变化（部分补，容器持有 filter 并重取，pageIndex 归零） */
  filterChange: [patch: { redisId?: number; category?: string; dtStart?: string; dtEnd?: string; q?: string }];
  /** 翻页（容器改 pageIndex 后重取；标准服务端分页） */
  pageChange: [pageIndex: number];
  /** 失败态重试（容器重取数据接口） */
  retry: [];
  /** 追踪下钻（行 TraceId → 容器命名路由 ConsoleTrace） */
  trace: [traceId: string];
}>();

const totalPages = computed(() => redisQueueTotalPages(props.total, props.pageSize));
const hasPager = computed(() => props.total > props.pageSize && !props.loading && !props.error);
/** 分类下拉候选：前端从当前列表聚合去重（后端无独立 action；GetCategoryList 仅 Biz FieldCache 无 HTTP 端点） */
const categories = computed(() => [...new Set(props.rows.map((r) => r.category).filter(Boolean))]);

function onNode(e: Event): void {
  emit('filterChange', { redisId: Number((e.target as HTMLSelectElement).value) || 0 });
}
function onCategory(e: Event): void {
  emit('filterChange', { category: (e.target as HTMLSelectElement).value.trim() });
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
    data-spec="mw-redis-queue"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        Redis 消息队列
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">共 {{ total }} 条</span>
      </h3>
    </div>

    <!-- 筛选条：加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="mw-redis-queue-filter"
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
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">分类</span>
      <select
        :class="SELECT"
        aria-label="按分类筛选"
        data-spec="mw-redis-queue-filter-category"
        @change="onCategory"
      >
        <option value="">
          全部分类
        </option>
        <option
          v-for="c in categories"
          :key="c"
          :value="c"
        >
          {{ c }}
        </option>
      </select>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="开始日期"
        data-spec="mw-redis-queue-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="mw-redis-queue-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-queue-filter-time-note"
      >按更新时间过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="名称/类型"
        :class="SELECT"
        aria-label="按关键字筛选"
        data-spec="mw-redis-queue-filter-q"
        @change="onQ"
      >
    </div>

    <div
      v-if="loading"
      data-spec="mw-redis-queue-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      Redis 队列加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="mw-redis-queue-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        Redis 队列加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="mw-redis-queue-retry"
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
          <th :class="TH">
            分类
          </th>
          <th :class="[TH, 'text-right']">
            Db
          </th>
          <th :class="TH">
            类型
          </th>
          <th :class="TH">
            消费组
          </th>
          <th :class="[TH, 'text-right']">
            消费者
          </th>
          <th :class="[TH, 'text-right']">
            总消费
          </th>
          <th :class="[TH, 'text-right']">
            消息数
          </th>
          <th :class="[TH, 'text-right']">
            最大积压
          </th>
          <th :class="[TH, 'text-center']">
            启用
          </th>
          <th :class="[TH, 'text-right']">
            最早消费
          </th>
          <th :class="[TH, 'text-right']">
            最后活跃
          </th>
          <th :class="[TH, 'text-center']">
            追踪
          </th>
          <th :class="[TH, 'text-right']">
            更新时间
          </th>
        </tr>
      </thead>
      <tbody class="group">
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="mw-redis-queue-row"
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
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.category || '—' }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.db }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.type || '—' }}</span>
          </td>
          <td :class="TD">
            <span
              class="block max-w-[180px] truncate text-[12.5px] text-[var(--el-text-color-primary)]"
              :title="r.groups"
            >{{ r.groups || '—' }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.consumers }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.total }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.messages }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.maxMessages || '—' }}</span>
          </td>
          <td :class="[TD, 'text-center']">
            <span
              :class="r.enable ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-secondary)]'"
              class="text-[12.5px]"
            >{{ r.enable ? '是' : '否' }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">{{ r.firstConsume }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">{{ r.lastActive }}</span>
          </td>
          <td :class="[TD, 'text-center']">
            <button
              v-if="r.traceId"
              type="button"
              data-spec="mw-redis-queue-trace"
              class="cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[8px] py-[2px] text-[12px] text-[var(--el-color-primary)] hover:bg-[var(--el-bg-color-overlay)]"
              title="查看链路详情"
              @click="emit('trace', r.traceId)"
            >
              查看
            </button>
            <button
              v-else
              type="button"
              data-spec="mw-redis-queue-trace-disabled"
              disabled
              class="cursor-not-allowed rounded-[6px] border border-[var(--el-border-color-lighter)] px-[8px] py-[2px] text-[12px] text-[var(--el-text-color-disabled)]"
              title="无 TraceId：该队列尚未发起追踪"
            >
              查看
            </button>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">{{ r.updateTime }}</span>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="15"
            data-spec="mw-redis-queue-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>暂无 Redis 队列数据</div>
            <div class="mt-[4px] text-[11.5px]">
              Redis 节点接入队列消费后，队列状态会在这里展示
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- 分页：标准服务端分页（pageIndex+pageSize 恒发） -->
    <div
      v-if="hasPager"
      class="flex items-center justify-end gap-[10px] px-[14px] pb-[12px] pt-[6px]"
      data-spec="mw-redis-queue-pager"
    >
      <span
        class="text-[11.5px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-queue-page"
      >第 {{ page + 1 }} / {{ totalPages }} 页</span>
      <Btn
        size="sm"
        variant="default"
        :disabled="page <= 0"
        data-spec="mw-redis-queue-prev"
        @click="emit('pageChange', page - 1)"
      >
        上一页
      </Btn>
      <Btn
        size="sm"
        variant="default"
        :disabled="page + 1 >= totalPages"
        data-spec="mw-redis-queue-next"
        @click="emit('pageChange', page + 1)"
      >
        下一页
      </Btn>
    </div>
  </Card>
</template>
