<script setup lang="ts">
/**
 * GatewayClusterBlock：治理页「网关」页签——网关集群区块（CARD-GW-01，形态 1 区块卡）。
 * 纯 UI：状态（rows/loading/error/filter）由容器持有；只发 create/edit/remove/filterChange/retry。
 * 9 列：名称 / 项目 / 负载均衡 / 健康检查（间隔/超时主行 + HealthPath 副行）/ 会话保持 / 启用 / 在线节点 / 更新时间 / 操作。
 * ActiveNodes 取不到显「—」（定稿裁定 3：扩展属性序列化未核实，不造数）。
 * 三态：加载「集群加载中…」/ 失败「集群加载失败：{原因}」+重试 / 空「还没有网关集群」+「创建集群」引导（J-GW-02 C13/C18）。
 * 筛选条：enable/sessionSticky 开关（勾选=只看该状态）、时间区间（注记「按更新时间过滤」）、Q；projectId 走顶栏不设控件。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { loadBalanceLabel, type ClusterRow, type GatewayClusterFilter } from './gatewayCluster.logic.ts';

withDefaults(
  defineProps<{
    rows: ClusterRow[];
    loading?: boolean;
    error?: string;
  }>(),
  { loading: false, error: '' },
);

const emit = defineEmits<{
  /** 工具条「新增集群」/空态引导「创建集群」 */
  create: [];
  /** 行操作：编辑（容器拉 Detail 回填抽屉） */
  edit: [row: ClusterRow];
  /** 行操作：删除（容器 GuardModal 确认后请求） */
  remove: [row: ClusterRow];
  /** 筛选变化（部分补，容器持有 filter 并重取） */
  filterChange: [patch: Partial<GatewayClusterFilter>];
  /** 失败态重试（容器整页签重取） */
  retry: [];
}>();

function onEnable(e: Event): void {
  emit('filterChange', { enable: (e.target as HTMLInputElement).checked });
}
function onSession(e: Event): void {
  emit('filterChange', { sessionSticky: (e.target as HTMLInputElement).checked });
}
function onQ(e: Event): void {
  emit('filterChange', { q: (e.target as HTMLInputElement).value.trim() });
}
function onDtStart(e: Event): void {
  emit('filterChange', { dtStart: (e.target as HTMLInputElement).value });
}
function onDtEnd(e: Event): void {
  emit('filterChange', { dtEnd: (e.target as HTMLInputElement).value });
}
</script>

<template>
  <Card
    padding="none"
    data-spec="gateway-cluster"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        网关集群
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ rows.length }} 条</span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="default"
        data-spec="gateway-cluster-create"
        @click="emit('create')"
      >
        新增集群
      </Btn>
    </div>

    <!-- 筛选条：开关勾选=只看该状态；加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="gateway-cluster-filter"
    >
      <label class="inline-flex cursor-pointer items-center gap-[6px] text-[11.5px] text-[var(--el-text-color-secondary)]">
        <input
          type="checkbox"
          class="h-[13px] w-[13px]"
          aria-label="只看启用集群"
          data-spec="gateway-cluster-filter-enable"
          @change="onEnable"
        >
        启用
      </label>
      <label class="inline-flex cursor-pointer items-center gap-[6px] text-[11.5px] text-[var(--el-text-color-secondary)]">
        <input
          type="checkbox"
          class="h-[13px] w-[13px]"
          aria-label="只看会话保持集群"
          data-spec="gateway-cluster-filter-session"
          @change="onSession"
        >
        会话保持
      </label>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="开始日期"
        data-spec="gateway-cluster-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="gateway-cluster-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="gateway-cluster-filter-time-note"
      >按更新时间过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="名称"
        :class="SELECT"
        aria-label="按关键字筛选"
        data-spec="gateway-cluster-filter-q"
        @change="onQ"
      >
    </div>

    <div
      v-if="loading"
      data-spec="gateway-cluster-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      集群加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="gateway-cluster-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        集群加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="gateway-cluster-retry"
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
            名称
          </th>
          <th :class="TH">
            项目
          </th>
          <th :class="TH">
            负载均衡
          </th>
          <th :class="TH">
            健康检查
          </th>
          <th :class="TH">
            会话保持
          </th>
          <th :class="[TH, 'text-right']">
            启用
          </th>
          <th :class="[TH, 'text-right']">
            在线节点
          </th>
          <th :class="TH">
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
          data-spec="gateway-cluster-row"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="TD">
            <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
              {{ r.name }}
            </div>
          </td>
          <td :class="TD">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.projectName || '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[12px] text-[var(--el-text-color-primary)]">{{ loadBalanceLabel(r.loadBalance) || '—' }}</span>
          </td>
          <td :class="TD">
            <div class="font-mono text-[12px] text-[var(--el-text-color-primary)]">
              {{ r.healthInterval }}s / {{ r.healthTimeout }}ms
            </div>
            <div class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">
              {{ r.healthPath || '—' }}
            </div>
          </td>
          <td :class="TD">
            <Tag :tone="r.sessionSticky ? 'ok' : 'default'">
              {{ r.sessionSticky ? '开启' : '关闭' }}
            </Tag>
          </td>
          <td :class="[TD, 'text-right']">
            <Tag :tone="r.enable ? 'ok' : 'default'">
              {{ r.enable ? '启用' : '停用' }}
            </Tag>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.activeNodes ?? '—' }}</span>
          </td>
          <td :class="TD">
            <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">{{ r.updateTime }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="inline-flex justify-end gap-1">
              <Btn
                size="sm"
                variant="ghost"
                data-spec="gateway-cluster-edit"
                @click="emit('edit', r)"
              >
                编辑
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                data-spec="gateway-cluster-remove"
                @click="emit('remove', r)"
              >
                删除
              </Btn>
            </span>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="9"
            data-spec="gateway-cluster-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>还没有网关集群</div>
            <Btn
              size="sm"
              variant="default"
              class="mt-[8px]"
              data-spec="gateway-cluster-empty-create"
              @click="emit('create')"
            >
              创建集群
            </Btn>
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
