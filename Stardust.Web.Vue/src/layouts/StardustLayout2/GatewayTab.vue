<script setup lang="ts">
/**
 * GatewayTab：治理页「网关」页签——多区块壳（CARD-GW-03，2026-11-07）。
 * 当前区块自上而下：网关路由（本组件）→ 网关集群（GW-01）→ 网关节点（GW-02）→ 网关证书（GW-04，末位）。
 * 证书区块（CARD-GW-04）：7 列（无 Id/有效期列），有效期走行内展开（懒拉 Detail），PfxPassword 无列不回显。
 * 路由区块既有形态逐字保留：列=路由 / 匹配 / 目标集群 / 健康 / TLS，行尾追加「编辑/删除」操作列；
 * 文案与 data-spec（gateway-tab/row/loading/failed/retry/empty）为 J-GW-04 C01-C13 走查基线，不改动。
 * CARD-DEP-02 同款：筛选条（常用项 + 「更多筛选」折叠）插在表上方，容器持 filter，本组件只发 patch；
 * 工具条「新增路由」、行操作（确认与请求在容器 MeshPage）。
 */
import { ref } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import GatewayClusterBlock from './GatewayClusterBlock.vue';
import GatewayNodeBlock from './GatewayNodeBlock.vue';
import GatewayCertBlock from './GatewayCertBlock.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import type { GatewayRouteRow } from './mesh.logic.ts';
import type { GatewayRouteFilter } from './gatewayRoute.logic.ts';
import type { ClusterRow, GatewayClusterFilter } from './gatewayCluster.logic.ts';
import type { NodeRow, GatewayNodeFilter } from './gatewayNode.logic.ts';
import type { CertRow, GatewayCertFilter, GatewayCertForm } from './gatewayCert.logic.ts';

type Opt = { id: number; name: string };

withDefaults(
  defineProps<{
    rows: GatewayRouteRow[];
    loading?: boolean;
    error?: string;
    /** 集群下拉选项（Id→Name 归一），来自 fetchGateway 透出的 clusters */
    clusters?: Opt[];
    /** 集群区块归一行（CARD-GW-01） */
    clusterRows?: ClusterRow[];
    clusterLoading?: boolean;
    clusterError?: string;
    /** 证书区块归一行（CARD-GW-04） */
    /** 节点区块归一行（CARD-GW-02） */
    nodeRows?: NodeRow[];
    nodeLoading?: boolean;
    nodeError?: string;
    /** 证书区块归一行（CARD-GW-04） */
    certRows?: CertRow[];
    certLoading?: boolean;
    certError?: string;
    /** 证书行内展开（有效期）：展开行 id / 详情加载中 / 失败文案 / 详情归一（懒拉 Detail 由容器持有） */
    certExpandedId?: number | null;
    certExpandLoading?: boolean;
    certExpandError?: string;
    certExpandInfo?: GatewayCertForm | null;
  }>(),
  {
    loading: false,
    error: '',
    clusters: () => [],
    clusterRows: () => [],
    clusterLoading: false,
    clusterError: '',
    nodeRows: () => [],
    nodeLoading: false,
    nodeError: '',
    certRows: () => [],
    certLoading: false,
    certError: '',
    certExpandedId: null,
    certExpandLoading: false,
    certExpandError: '',
    certExpandInfo: null,
  },
);

const emit = defineEmits<{
  retry: [];
  /** 工具条「新增路由」 */
  create: [];
  /** 行操作：编辑（容器拉 Detail 回填抽屉） */
  edit: [row: GatewayRouteRow];
  /** 行操作：删除（容器 GuardModal 确认后请求） */
  remove: [row: GatewayRouteRow];
  /** 筛选变化（部分补，容器持有 filter 并重取） */
  filterChange: [patch: Partial<GatewayRouteFilter>];
  filterReset: [];
  /** 集群区块（CARD-GW-01）：新增/编辑/删除/筛选（容器持 clusterFilter 并重取） */
  clusterCreate: [];
  clusterEdit: [row: ClusterRow];
  clusterRemove: [row: ClusterRow];
  clusterFilterChange: [patch: Partial<GatewayClusterFilter>];
  /** 节点区块（CARD-GW-02）：新增/编辑/删除/筛选（容器持 nodeFilter 并重取） */
  nodeCreate: [];
  nodeEdit: [row: NodeRow];
  nodeRemove: [row: NodeRow];
  nodeFilterChange: [patch: Partial<GatewayNodeFilter>];
  /** 证书区块（CARD-GW-04）：新增/编辑/删除/筛选/行内展开（容器持 certFilter、展开态并懒拉 Detail） */
  certCreate: [];
  certEdit: [row: CertRow];
  certRemove: [row: CertRow];
  certFilterChange: [patch: Partial<GatewayCertFilter>];
  certExpand: [row: CertRow];
}>();

/** 「更多筛选」折叠展开态（纯 UI 状态，不出组件；默认收起只显示常用项——旅程 C21） */
const moreOpen = ref(false);

function onEnable(e: Event): void {
  emit('filterChange', { enable: (e.target as HTMLSelectElement).value });
}
function onCluster(e: Event): void {
  emit('filterChange', { clusterId: Number((e.target as HTMLSelectElement).value) || 0 });
}
function onDomain(e: Event): void {
  emit('filterChange', { domain: (e.target as HTMLInputElement).value.trim() });
}
function onQ(e: Event): void {
  emit('filterChange', { q: (e.target as HTMLInputElement).value.trim() });
}
function onPriority(e: Event): void {
  const v = (e.target as HTMLInputElement).value.trim();
  emit('filterChange', { priority: v === '' ? null : Number(v) });
}
function onStripPrefix(e: Event): void {
  emit('filterChange', { stripPrefix: (e.target as HTMLInputElement).checked });
}
function onDtStart(e: Event): void {
  emit('filterChange', { dtStart: (e.target as HTMLInputElement).value });
}
function onDtEnd(e: Event): void {
  emit('filterChange', { dtEnd: (e.target as HTMLInputElement).value });
}
</script>

<template>
  <div data-spec="gateway-tab-shell">
    <!-- ===== 区块 1：网关路由（既有形态逐字保留 + CARD-GW-03 工具条/筛选条/操作列） ===== -->
    <Card
      padding="none"
      data-spec="gateway-tab"
    >
      <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
        <h3 :class="H3">
          网关路由
          <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ rows.length }} 条</span>
        </h3>
        <span class="flex-1" />
        <Btn
          size="sm"
          variant="default"
          data-spec="gateway-route-create"
          @click="emit('create')"
        >
          新增路由
        </Btn>
      </div>

      <!-- 筛选条：常用项默认显示，低频项收进「更多筛选」（C21）；加载/失败态不显示 -->
      <div
        v-if="!loading && !error"
        class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
        data-spec="gateway-route-filter"
      >
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">启用状态</span>
        <select
          :class="SELECT"
          aria-label="按启用状态筛选"
          data-spec="gateway-route-filter-enable"
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
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">目标集群</span>
        <select
          :class="SELECT"
          aria-label="按目标集群筛选"
          data-spec="gateway-route-filter-cluster"
          @change="onCluster"
        >
          <option :value="0">
            全部
          </option>
          <option
            v-for="c in clusters"
            :key="c.id"
            :value="c.id"
          >
            {{ c.name }}
          </option>
        </select>
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">域名</span>
        <input
          type="text"
          placeholder="精确匹配，如 iot.example.com"
          :class="SELECT"
          aria-label="按域名筛选"
          data-spec="gateway-route-filter-domain"
          @change="onDomain"
        >
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
        <input
          type="text"
          placeholder="名称 / 路径"
          :class="SELECT"
          aria-label="按关键字筛选"
          data-spec="gateway-route-filter-q"
          @change="onQ"
        >
        <Btn
          size="sm"
          variant="ghost"
          data-spec="gateway-route-filter-more"
          :aria-expanded="moreOpen"
          @click="moreOpen = !moreOpen"
        >
          {{ moreOpen ? '收起筛选' : '更多筛选' }}
        </Btn>
      </div>
      <div
        v-if="!loading && !error && moreOpen"
        class="mb-[10px] flex flex-wrap items-center gap-2 px-[14px]"
        data-spec="gateway-route-filter-more-open"
      >
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">优先级</span>
        <input
          type="number"
          :class="SELECT"
          aria-label="按优先级筛选"
          data-spec="gateway-route-filter-priority"
          @change="onPriority"
        >
        <label class="inline-flex cursor-pointer items-center gap-[6px] text-[11.5px] text-[var(--el-text-color-secondary)]">
          <input
            type="checkbox"
            class="h-[13px] w-[13px]"
            data-spec="gateway-route-filter-stripprefix"
            @change="onStripPrefix"
          >
          去前缀
        </label>
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
        <input
          type="date"
          :class="SELECT"
          aria-label="开始日期"
          data-spec="gateway-route-filter-dtstart"
          @change="onDtStart"
        >
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
        <input
          type="date"
          :class="SELECT"
          aria-label="结束日期"
          data-spec="gateway-route-filter-dtend"
          @change="onDtEnd"
        >
        <span
          class="text-[11px] text-[var(--el-text-color-secondary)]"
          data-spec="gateway-route-filter-time-note"
        >按更新时间过滤</span>
      </div>

      <div
        v-if="loading"
        data-spec="gateway-loading"
        class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        路由加载中…
      </div>

      <div
        v-else-if="error"
        data-spec="gateway-failed"
        class="px-[14px] py-[14px]"
      >
        <div class="text-[12.5px] text-[var(--el-color-danger)]">
          网关路由加载失败：{{ error }}
        </div>
        <button
          type="button"
          data-spec="gateway-retry"
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
              路由
            </th>
            <th :class="TH">
              匹配
            </th>
            <th :class="TH">
              目标集群
            </th>
            <th :class="[TH, 'text-right']">
              健康
            </th>
            <th :class="TH">
              TLS
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
            data-spec="gateway-row"
            class="hover:bg-[var(--el-bg-color-overlay)]"
          >
            <td :class="TD">
              <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
                {{ r.name }}
              </div>
              <div class="text-[11.5px] text-[var(--el-text-color-secondary)]">
                {{ r.domain || '—' }}
              </div>
            </td>
            <td :class="TD">
              <div class="font-mono text-[12px] text-[var(--el-text-color-primary)]">
                {{ r.methods || 'ALL' }}
              </div>
              <div class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">
                {{ r.path }}
              </div>
            </td>
            <td :class="TD">
              <span
                v-if="r.clusterName"
                class="text-[12.5px] text-[var(--el-text-color-primary)]"
              >{{ r.clusterName }}</span>
              <Tag
                v-else
                tone="default"
              >
                待接入
              </Tag>
            </td>
            <td :class="[TD, 'text-right']">
              <Tag :tone="r.enable ? 'ok' : 'default'">
                {{ r.enable ? '启用' : '停用' }}
              </Tag>
            </td>
            <td :class="TD">
              <div
                v-if="r.tls.present"
                class="flex items-center gap-[5px] text-[12px] text-[var(--el-text-color-secondary)]"
              >
                <KIcon
                  name="Lock"
                  :size="11"
                />
                <span class="font-mono">{{ r.tls.domain }}</span>
                <span
                  v-if="r.tls.autoRenew"
                  class="text-[11px]"
                >自动续期</span>
              </div>
              <Tag
                v-else
                tone="default"
              >
                待接入
              </Tag>
            </td>
            <td :class="[TD, 'text-right']">
              <span class="inline-flex justify-end gap-1">
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="gateway-route-edit"
                  @click="emit('edit', r)"
                >
                  编辑
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="gateway-route-remove"
                  @click="emit('remove', r)"
                >
                  删除
                </Btn>
              </span>
            </td>
          </tr>
          <tr v-if="!rows.length">
            <td
              colspan="6"
              data-spec="gateway-empty"
              class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            >
              还没有配置网关路由
            </td>
          </tr>
        </tbody>
      </table>
    </Card>

    <!-- ===== 区块 2：网关集群（CARD-GW-01，多区块第二位） ===== -->
    <GatewayClusterBlock
      class="mt-[12px]"
      :rows="clusterRows"
      :loading="clusterLoading"
      :error="clusterError"
      @create="emit('clusterCreate')"
      @edit="(r) => emit('clusterEdit', r)"
      @remove="(r) => emit('clusterRemove', r)"
      @filter-change="(p) => emit('clusterFilterChange', p)"
      @retry="emit('retry')"
    />

    <!-- ===== 区块 3：网关节点（CARD-GW-02） ===== -->
    <GatewayNodeBlock
      class="mt-[12px]"
      :rows="nodeRows"
      :loading="nodeLoading"
      :error="nodeError"
      :clusters="clusters"
      @create="emit('nodeCreate')"
      @edit="(r) => emit('nodeEdit', r)"
      @remove="(r) => emit('nodeRemove', r)"
      @filter-change="(p) => emit('nodeFilterChange', p)"
      @retry="emit('retry')"
    />

    <!-- ===== 区块 4：网关证书（CARD-GW-04，多区块末位） ===== -->
    <GatewayCertBlock
      class="mt-[12px]"
      :rows="certRows"
      :loading="certLoading"
      :error="certError"
      :expanded-id="certExpandedId"
      :expanded-loading="certExpandLoading"
      :expanded-error="certExpandError"
      :expanded-info="certExpandInfo"
      @create="emit('certCreate')"
      @edit="(r) => emit('certEdit', r)"
      @remove="(r) => emit('certRemove', r)"
      @filter-change="(p) => emit('certFilterChange', p)"
      @expand="(r) => emit('certExpand', r)"
      @retry="emit('retry')"
    />
  </div>
</template>
