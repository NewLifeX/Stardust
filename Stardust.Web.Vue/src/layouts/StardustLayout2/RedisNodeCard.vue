<script setup lang="ts">
/**
 * RedisNodeCard：治理页「中间件」页签——Redis 节点区块（CARD-MW-05，J-MW-05 落点，MW 块枢纽）。
 * 纯 UI：状态（rows/loading/error/total/page/filter/refreshingId/categories）由容器持有；
 * 只发 filterChange/pageChange/retry/create/edit/monitor/queue/refresh。
 * 12 列：编号（Id=Int32 非雪花）/ 项目（ProjectName 文本，GalaxyProject 链接待 PLT-01 记差异 BL-11）/
 * 名称 / 分类 / 地址 / 用户名 / 版本 / 模式 / 角色 / 启用 / 更新时间 / 操作（编辑/监控/队列/刷新）。
 * 差异如实注记（§9.1）：MaxMemory/MemoryPolicy/MemoryAllocator/ScanQueue 列省略（MW-02 先例——列集
 * 裁定 13 列）；Password 列任何路径不渲染（安全）；审计列=【缺·待接入】（BL-10，不放假链接）。
 * 筛选条：分类下拉（categories 由容器从列表行聚合——GetCategoryList 为字段缓存无 WebApi 端点）/
 * 地址（注记「精确匹配」，服务端 ==）/ 启用三态 / 时间区间（注记「按更新时间过滤」，UpdateTime.Between）/
 * 关键字（Q → 12 字段 Contains）；@change 即查。
 * 下钻（C03，裁定③）：「监控」→ Redis 数据卡、「队列」→ Redis 队列卡，均组件状态带 redisId，不走路由。
 * 刷新（C04）：§3.5 纪律由容器保证——refreshingId 控制按钮转圈+禁用（≥300ms），toast 由容器弹。
 * 新增（裁定④）：卡头「新增节点」→ 容器开新增抽屉；编辑抽屉内含删除（GuardModal 确认）。
 * 三态：加载「Redis 节点加载中…」/ 失败「Redis 节点加载失败：{原因}」+重试 / 空「暂无 Redis 节点」。
 * 分页：服务端分页（pageIndex 从 0），total<=pageSize 时整行隐藏。
 */
import { computed } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { redisNodeTotalPages, type RedisNodeRow } from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: RedisNodeRow[];
    loading?: boolean;
    error?: string;
    total?: number;
    /** 当前页，从 0 起 */
    page?: number;
    pageSize?: number;
    /** 正在刷新的节点 Id（§3.5：按钮转圈+禁用，容器持有） */
    refreshingId?: number;
    /** 分类下拉候选（容器从列表行聚合，RedisQueue 先例） */
    categories?: string[];
  }>(),
  { loading: false, error: '', total: 0, page: 0, pageSize: 20, refreshingId: 0, categories: () => [] },
);

const emit = defineEmits<{
  /** 筛选变化（部分补，容器持有 filter 并重取，pageIndex 归零；id 深链不在其中——一经手动筛选即失效） */
  filterChange: [patch: { category?: string; server?: string; enable?: '' | 'true' | 'false'; dtStart?: string; dtEnd?: string; q?: string }];
  /** 翻页（容器改 pageIndex 后重取） */
  pageChange: [pageIndex: number];
  /** 失败态重试（容器重取列表接口） */
  retry: [];
  /** 新增（容器开新增抽屉，裁定④） */
  create: [];
  /** 编辑（容器拉 Detail → 开抽屉，S2/C05） */
  edit: [row: RedisNodeRow];
  /** 监控下钻（容器切 RedisData 筛选到该节点，组件状态带 redisId，C03） */
  monitor: [row: RedisNodeRow];
  /** 队列下钻（容器切 RedisMessageQueue 筛选到该节点，组件状态带 redisId，C03） */
  queue: [row: RedisNodeRow];
  /** 手动刷新（容器 GET Refresh + toast，C04） */
  refresh: [row: RedisNodeRow];
}>();

const totalPages = computed(() => redisNodeTotalPages(props.total, props.pageSize));
const hasPager = computed(() => props.total > props.pageSize && !props.loading && !props.error);

function onCategory(e: Event): void {
  emit('filterChange', { category: (e.target as HTMLSelectElement).value });
}
function onServer(e: Event): void {
  emit('filterChange', { server: (e.target as HTMLInputElement).value });
}
function onEnable(e: Event): void {
  const v = (e.target as HTMLSelectElement).value;
  emit('filterChange', { enable: v === 'true' || v === 'false' ? (v as 'true' | 'false') : '' });
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
    data-spec="mw-redis-node"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        Redis 节点
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">共 {{ total }} 条</span>
      </h3>
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-node-audit-note"
      >审计日志待平台接入</span>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="primary"
        data-spec="mw-redis-node-create"
        @click="emit('create')"
      >
        新增节点
      </Btn>
    </div>

    <!-- 筛选条：加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="mw-redis-node-filter"
    >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">分类</span>
      <select
        :class="SELECT"
        aria-label="按分类筛选"
        data-spec="mw-redis-node-filter-category"
        @change="onCategory"
      >
        <option value="">
          全部
        </option>
        <option
          v-for="c in categories"
          :key="c"
          :value="c"
        >
          {{ c }}
        </option>
      </select>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">地址</span>
      <input
        type="text"
        placeholder="127.0.0.1:6379"
        :class="SELECT"
        aria-label="按地址精确筛选"
        data-spec="mw-redis-node-filter-server"
        @change="onServer"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-node-filter-server-note"
      >精确匹配</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">启用</span>
      <select
        :class="SELECT"
        aria-label="按启用状态筛选"
        data-spec="mw-redis-node-filter-enable"
        @change="onEnable"
      >
        <option value="">
          全部
        </option>
        <option value="true">
          启用
        </option>
        <option value="false">
          停用
        </option>
      </select>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="开始日期"
        data-spec="mw-redis-node-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="mw-redis-node-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-node-filter-time-note"
      >按更新时间过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="名称"
        :class="SELECT"
        aria-label="按关键字筛选"
        data-spec="mw-redis-node-filter-q"
        @change="onQ"
      >
    </div>

    <div
      v-if="loading"
      data-spec="mw-redis-node-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      Redis 节点加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="mw-redis-node-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        Redis 节点加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="mw-redis-node-retry"
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
            项目
          </th>
          <th :class="TH">
            名称
          </th>
          <th :class="TH">
            分类
          </th>
          <th :class="TH">
            地址
          </th>
          <th :class="TH">
            用户名
          </th>
          <th :class="TH">
            版本
          </th>
          <th :class="TH">
            模式
          </th>
          <th :class="TH">
            角色
          </th>
          <th :class="TH">
            启用
          </th>
          <th :class="[TH, 'text-right']">
            更新时间
          </th>
          <th :class="[TH, 'text-right']">
            操作
          </th>
        </tr>
      </thead>
      <tbody class="group">
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="mw-redis-node-row"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="TD">
            <span
              class="font-mono text-[12px] text-[var(--el-text-color-secondary)]"
              :title="String(r.id)"
            >{{ r.id }}</span>
          </td>
          <td :class="TD">
            <span
              v-if="r.projectName"
              class="text-[12.5px] text-[var(--el-text-color-primary)]"
            >{{ r.projectName }}</span>
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
          <td :class="TD">
            <span class="font-mono text-[12px] text-[var(--el-text-color-primary)]">{{ r.server || '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.userName || '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.version || '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.mode || '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.role || '—' }}</span>
          </td>
          <td :class="TD">
            <span
              class="text-[12px]"
              :class="r.enable ? 'text-[var(--el-color-success)]' : 'text-[var(--el-text-color-secondary)]'"
            >{{ r.enable ? '启用' : '停用' }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12px] text-[var(--el-text-color-primary)]">{{ r.updateTime }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <div class="flex items-center justify-end gap-[6px]">
              <Btn
                size="sm"
                variant="default"
                data-spec="mw-redis-node-edit"
                @click="emit('edit', r)"
              >
                编辑
              </Btn>
              <Btn
                size="sm"
                variant="default"
                data-spec="mw-redis-node-monitor"
                @click="emit('monitor', r)"
              >
                监控
              </Btn>
              <Btn
                size="sm"
                variant="default"
                data-spec="mw-redis-node-queue"
                @click="emit('queue', r)"
              >
                队列
              </Btn>
              <Btn
                size="sm"
                variant="default"
                :disabled="props.refreshingId === r.id"
                data-spec="mw-redis-node-refresh"
                @click="emit('refresh', r)"
              >
                <span
                  v-if="props.refreshingId === r.id"
                  class="mr-[4px] inline-block h-[10px] w-[10px] animate-spin rounded-full border-[1.5px] border-[var(--el-border-color)] border-t-[var(--el-color-primary)]"
                />
                {{ props.refreshingId === r.id ? '刷新中…' : '刷新' }}
              </Btn>
            </div>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="12"
            data-spec="mw-redis-node-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>暂无 Redis 节点</div>
            <div class="mt-[4px] text-[11.5px]">
              在旧版节点管理添加 Redis 节点后，会在这里展示
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- 分页：仅服务端有更多数据时显示 -->
    <div
      v-if="hasPager"
      class="flex items-center justify-end gap-[10px] px-[14px] pb-[12px] pt-[6px]"
      data-spec="mw-redis-node-pager"
    >
      <span
        class="text-[11.5px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-redis-node-page"
      >第 {{ page + 1 }} / {{ totalPages }} 页</span>
      <Btn
        size="sm"
        variant="default"
        :disabled="page <= 0"
        data-spec="mw-redis-node-prev"
        @click="emit('pageChange', page - 1)"
      >
        上一页
      </Btn>
      <Btn
        size="sm"
        variant="default"
        :disabled="page + 1 >= totalPages"
        data-spec="mw-redis-node-next"
        @click="emit('pageChange', page + 1)"
      >
        下一页
      </Btn>
    </div>
  </Card>
</template>
