<script setup lang="ts">
/**
 * MySqlNodeCard：治理页「中间件」页签——MySQL 节点区块（CARD-MW-02，J-MW-02 落点，形态 1 区块卡）。
 * 纯 UI：状态（rows/loading/error/total/page/filter/refreshingId）由容器持有；
 * 只发 filterChange/pageChange/retry/edit/monitor/refresh。
 * 12 列：编号（Id=Int32）/ 项目（ProjectName 文本，链接待 PLT-01 记差异）/ 名称 / 分类 / 地址 / 端口 /
 * 用户名 / 数据库 / 版本 / 启用 / 更新时间 / 操作（编辑/监控/刷新）。
 * 差异如实注记（§9.1）：旧列表 Password 列与审计列不复刻——密码任何路径不渲染（安全）；
 * 审计列=平台 backlog 待接入（头部仅注记，不放假链接）；CreateTime 列省略（仅保留更新时间）。
 * 编号 Id 非控件（定稿裁定：URL ?id= 深链单条直达由容器经 initialNodeFilter 下发，不作界面筛选）。
 * 筛选条：地址（注记「精确匹配」，服务端 ==）/ 端口 / 项目编号（数字手输，下拉待 PLT-01）/
 * 启用三态 / 时间区间（注记「按更新时间过滤」，服务端 UpdateTime.Between）/ 名称关键字；@change 即查。
 * 监控=旅程 -1 替代路径：发出 monitor(row) 后由容器把 MySqlData 筛选切到该节点（MW-01 下钻）。
 * 刷新（C05）：§3.5 纪律由容器保证——refreshingId 控制按钮转圈+禁用（≥300ms），结果 toast 由容器弹。
 * 三态：加载「MySQL 节点加载中…」/ 失败「MySQL 节点加载失败：{原因}」+重试 / 空「暂无 MySQL 节点」。
 * 分页：服务端分页（pageIndex 从 0），total<=pageSize 时整行隐藏。
 */
import { computed } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { mysqlDataTotalPages, type MySqlNodeRow } from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: MySqlNodeRow[];
    loading?: boolean;
    error?: string;
    total?: number;
    /** 当前页，从 0 起 */
    page?: number;
    pageSize?: number;
    /** 正在刷新的节点 Id（§3.5：按钮转圈+禁用，容器持有） */
    refreshingId?: number;
  }>(),
  { loading: false, error: '', total: 0, page: 0, pageSize: 20, refreshingId: 0 },
);

const emit = defineEmits<{
  /** 筛选变化（部分补，容器持有 filter 并重取，pageIndex 归零；id 深链不在其中——一经手动筛选即失效） */
  filterChange: [patch: { server?: string; port?: string; projectId?: string; enable?: '' | 'true' | 'false'; dtStart?: string; dtEnd?: string; q?: string }];
  /** 翻页（容器改 pageIndex 后重取） */
  pageChange: [pageIndex: number];
  /** 失败态重试（容器重取列表接口） */
  retry: [];
  /** 编辑（容器拉 Detail → 开抽屉，S2） */
  edit: [row: MySqlNodeRow];
  /** 监控下钻（容器切 MySqlData 筛选到该节点，旅程 -1 替代路径） */
  monitor: [row: MySqlNodeRow];
  /** 手动刷新（容器 POST Refresh + toast，S4/C05） */
  refresh: [row: MySqlNodeRow];
}>();

const totalPages = computed(() => mysqlDataTotalPages(props.total, props.pageSize));
const hasPager = computed(() => props.total > props.pageSize && !props.loading && !props.error);

function onServer(e: Event): void {
  emit('filterChange', { server: (e.target as HTMLInputElement).value });
}
function onPort(e: Event): void {
  emit('filterChange', { port: (e.target as HTMLInputElement).value });
}
function onProject(e: Event): void {
  emit('filterChange', { projectId: (e.target as HTMLInputElement).value });
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
    data-spec="mw-mysql-node"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        MySQL 节点
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">共 {{ total }} 条</span>
      </h3>
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-mysql-node-audit-note"
      >审计日志待平台接入</span>
    </div>

    <!-- 筛选条：加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="mw-mysql-node-filter"
    >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">地址</span>
      <input
        type="text"
        placeholder="服务器地址"
        :class="SELECT"
        aria-label="按服务器地址精确筛选"
        data-spec="mw-mysql-node-filter-server"
        @change="onServer"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-mysql-node-filter-server-note"
      >精确匹配</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">端口</span>
      <input
        type="number"
        placeholder="端口"
        :class="SELECT"
        aria-label="按端口筛选"
        data-spec="mw-mysql-node-filter-port"
        @change="onPort"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">项目编号</span>
      <input
        type="number"
        placeholder="项目编号"
        :class="SELECT"
        aria-label="按项目编号筛选"
        data-spec="mw-mysql-node-filter-project"
        @change="onProject"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">启用</span>
      <select
        :class="SELECT"
        aria-label="按启用状态筛选"
        data-spec="mw-mysql-node-filter-enable"
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
        data-spec="mw-mysql-node-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="mw-mysql-node-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-mysql-node-filter-time-note"
      >按更新时间过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="名称"
        :class="SELECT"
        aria-label="按名称关键字筛选"
        data-spec="mw-mysql-node-filter-q"
        @change="onQ"
      >
    </div>

    <div
      v-if="loading"
      data-spec="mw-mysql-node-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      MySQL 节点加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="mw-mysql-node-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        MySQL 节点加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="mw-mysql-node-retry"
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
          <th :class="[TH, 'text-right']">
            端口
          </th>
          <th :class="TH">
            用户名
          </th>
          <th :class="TH">
            数据库
          </th>
          <th :class="TH">
            版本
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
          data-spec="mw-mysql-node-row"
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
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.port }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.userName || '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.databaseName || '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.version || '—' }}</span>
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
                data-spec="mw-mysql-node-edit"
                @click="emit('edit', r)"
              >
                编辑
              </Btn>
              <Btn
                size="sm"
                variant="default"
                data-spec="mw-mysql-node-monitor"
                @click="emit('monitor', r)"
              >
                监控
              </Btn>
              <Btn
                size="sm"
                variant="default"
                :disabled="props.refreshingId === r.id"
                data-spec="mw-mysql-node-refresh"
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
            data-spec="mw-mysql-node-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>暂无 MySQL 节点</div>
            <div class="mt-[4px] text-[11.5px]">
              在旧版节点管理添加 MySQL 节点后，会在这里展示
            </div>
          </td>
        </tr>
      </tbody>
    </table>

    <!-- 分页：仅服务端有更多数据时显示 -->
    <div
      v-if="hasPager"
      class="flex items-center justify-end gap-[10px] px-[14px] pb-[12px] pt-[6px]"
      data-spec="mw-mysql-node-pager"
    >
      <span
        class="text-[11.5px] text-[var(--el-text-color-secondary)]"
        data-spec="mw-mysql-node-page"
      >第 {{ page + 1 }} / {{ totalPages }} 页</span>
      <Btn
        size="sm"
        variant="default"
        :disabled="page <= 0"
        data-spec="mw-mysql-node-prev"
        @click="emit('pageChange', page - 1)"
      >
        上一页
      </Btn>
      <Btn
        size="sm"
        variant="default"
        :disabled="page + 1 >= totalPages"
        data-spec="mw-mysql-node-next"
        @click="emit('pageChange', page + 1)"
      >
        下一页
      </Btn>
    </div>
  </Card>
</template>
