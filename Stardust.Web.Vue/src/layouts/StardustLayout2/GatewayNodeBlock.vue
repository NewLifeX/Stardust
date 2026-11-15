<script setup lang="ts">
/**
 * GatewayNodeBlock：治理页「网关」页签——网关节点区块（CARD-GW-02，形态 1 区块卡）。
 * 纯 UI：状态（rows/loading/error/filter）由容器持有；只发 create/edit/remove/filterChange/retry。
 * 9 列：名称（缺省显 Address）/ 地址（mono）/ 所属集群（映射解析，缺「—」）/ 健康状态（三态 Tag）/
 * 权重 / 最大失败次数 / 失败超时 / 启用 / 操作；无 Id/元数据列（C05）。
 * 健康三态（旧壳旁证 gateway.model.ts:90-93）：启用且健康=运行 / 启用不健康=异常 / 停用=离线。
 * 三态：加载「节点加载中…」/ 失败「节点加载失败：{原因}」+重试 / 空「还没有网关节点」+「创建节点」引导（C14）。
 * 筛选条（C04）：集群下拉 / 地址（后端精确）/ 健康下拉 / 启用开关 / 时间区间（注记「按更新时间过滤」）/ Q；
 * 不设 projectId 控件（节点接口无 projectId——定稿裁定 1，本地过滤在容器）。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { nodeHealthTag, type GatewayNodeFilter, type NodeRow } from './gatewayNode.logic.ts';

type Opt = { id: number; name: string };

withDefaults(
  defineProps<{
    rows: NodeRow[];
    loading?: boolean;
    error?: string;
    /** 集群下拉选项（筛选条「所属集群」=盘点 clusterId 过滤的 UI 承载），来自 fetchGateway 透出 */
    clusters?: Opt[];
  }>(),
  { loading: false, error: '', clusters: () => [] },
);

const emit = defineEmits<{
  /** 工具条「新增」/空态引导「创建节点」 */
  create: [];
  /** 行操作：编辑（容器拉 Detail 回填抽屉） */
  edit: [row: NodeRow];
  /** 行操作：删除（容器 GuardModal 确认后请求） */
  remove: [row: NodeRow];
  /** 筛选变化（部分补，容器持有 filter 并重取） */
  filterChange: [patch: Partial<GatewayNodeFilter>];
  /** 失败态重试（容器重取节点接口） */
  retry: [];
}>();

function onCluster(e: Event): void {
  emit('filterChange', { clusterId: Number((e.target as HTMLSelectElement).value) || 0 });
}
function onAddress(e: Event): void {
  emit('filterChange', { address: (e.target as HTMLInputElement).value.trim() });
}
function onHealthy(e: Event): void {
  const v = (e.target as HTMLSelectElement).value;
  emit('filterChange', { isHealthy: v === 'healthy' || v === 'unhealthy' ? (v as 'healthy' | 'unhealthy') : '' });
}
function onEnable(e: Event): void {
  emit('filterChange', { enable: (e.target as HTMLInputElement).checked });
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
    data-spec="gateway-node"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        网关节点
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ rows.length }} 条</span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="default"
        data-spec="gateway-node-create"
        @click="emit('create')"
      >
        新增
      </Btn>
    </div>

    <!-- 筛选条：加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="gateway-node-filter"
    >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">所属集群</span>
      <select
        :class="SELECT"
        aria-label="按所属集群筛选"
        data-spec="gateway-node-filter-cluster"
        @change="onCluster"
      >
        <option value="">
          全部集群
        </option>
        <option
          v-for="c in clusters"
          :key="c.id"
          :value="c.id"
        >
          {{ c.name }}
        </option>
      </select>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">地址</span>
      <input
        type="text"
        placeholder="完整地址精确匹配"
        :class="SELECT"
        aria-label="按地址筛选"
        data-spec="gateway-node-filter-address"
        @change="onAddress"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">健康</span>
      <select
        :class="SELECT"
        aria-label="按健康状态筛选"
        data-spec="gateway-node-filter-healthy"
        @change="onHealthy"
      >
        <option value="">
          全部
        </option>
        <option value="healthy">
          健康
        </option>
        <option value="unhealthy">
          异常
        </option>
      </select>
      <label class="inline-flex cursor-pointer items-center gap-[6px] text-[11.5px] text-[var(--el-text-color-secondary)]">
        <input
          type="checkbox"
          class="h-[13px] w-[13px]"
          aria-label="只看启用节点"
          data-spec="gateway-node-filter-enable"
          @change="onEnable"
        >
        启用
      </label>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="开始日期"
        data-spec="gateway-node-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="gateway-node-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="gateway-node-filter-time-note"
      >按更新时间过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="名称"
        :class="SELECT"
        aria-label="按关键字筛选"
        data-spec="gateway-node-filter-q"
        @change="onQ"
      >
    </div>

    <div
      v-if="loading"
      data-spec="gateway-node-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      节点加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="gateway-node-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        节点加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="gateway-node-retry"
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
            地址
          </th>
          <th :class="TH">
            所属集群
          </th>
          <th :class="[TH, 'text-right']">
            健康状态
          </th>
          <th :class="[TH, 'text-right']">
            权重
          </th>
          <th :class="[TH, 'text-right']">
            最大失败次数
          </th>
          <th :class="[TH, 'text-right']">
            失败超时
          </th>
          <th :class="[TH, 'text-right']">
            启用
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
          data-spec="gateway-node-row"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="TD">
            <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
              {{ r.name || r.address || '—' }}
            </div>
          </td>
          <td :class="TD">
            <span class="font-mono text-[12px] text-[var(--el-text-color-primary)]">{{ r.address || '—' }}</span>
          </td>
          <td :class="TD">
            <span
              v-if="r.clusterName"
              class="text-[12.5px] text-[var(--el-text-color-primary)]"
            >{{ r.clusterName }}</span>
            <span
              v-else
              class="text-[12.5px] text-[var(--el-text-color-secondary)]"
            >—</span>
          </td>
          <td :class="[TD, 'text-right']">
            <Tag :tone="nodeHealthTag(r.enable, r.isHealthy).tone">
              {{ nodeHealthTag(r.enable, r.isHealthy).label }}
            </Tag>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.weight }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.maxFails }}</span>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.failTimeout }}s</span>
          </td>
          <td :class="[TD, 'text-right']">
            <Tag :tone="r.enable ? 'ok' : 'default'">
              {{ r.enable ? '启用' : '停用' }}
            </Tag>
          </td>
          <td :class="[TD, 'text-right']">
            <span class="inline-flex justify-end gap-1">
              <Btn
                size="sm"
                variant="ghost"
                data-spec="gateway-node-edit"
                @click="emit('edit', r)"
              >
                编辑
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                data-spec="gateway-node-remove"
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
            data-spec="gateway-node-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>还没有网关节点</div>
            <Btn
              size="sm"
              variant="default"
              class="mt-[8px]"
              data-spec="gateway-node-empty-create"
              @click="emit('create')"
            >
              创建节点
            </Btn>
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
