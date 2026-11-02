<script setup lang="ts">
/**
 * 应用工作台注册侧页签：应用历史 / 命令 / 性能 / 注册 / 消费。
 * 只收 appId + kind + 筛选；取数失败与没有数据分开显示。
 * 性能曲线只在当前结果能确定唯一实例时绘制内存和 CPU 走势。
 */
import { computed, ref, watch } from 'vue';
import { RouterLink } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { toast } from './useToast.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import {
  emptyObserveQuery,
  fetchObserve,
  formatBytes,
  formatCpu,
  meterChart,
  sparkPoints,
} from './appObserve.logic.ts';
import type {
  CommandRow,
  ConsumeRow,
  HistoryRow,
  MeterRow,
  ObserveKind,
  ObserveQuery,
  ObserveResult,
  ServiceRow,
} from './appObserve.logic.ts';

const props = defineProps<{
  appId: number;
  kind: ObserveKind;
  query?: ObserveQuery;
  deps?: { request?: Req };
}>();

const emit = defineEmits<{ query: [query: ObserveQuery] }>();

const TITLE: Record<ObserveKind, string> = {
  appHistory: '应用历史',
  commands: '命令',
  meter: '性能',
  services: '注册',
  consume: '消费',
};
const EMPTY: Record<ObserveKind, string> = {
  appHistory: '该应用还没有历史记录',
  commands: '该应用还没有命令记录',
  meter: '该应用在所选时间没有性能采样',
  services: '该应用还没有注册的服务实例',
  consume: '该应用还没有服务消费记录',
};
const FAIL: Record<ObserveKind, string> = {
  appHistory: '应用历史加载失败',
  commands: '命令加载失败',
  meter: '性能加载失败',
  services: '注册加载失败',
  consume: '消费加载失败',
};

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const local = ref<ObserveQuery>(emptyObserveQuery());
let loadedKey = '';
const loading = ref(true);
const loadFailed = ref(false);
const result = ref<ObserveResult | null>(null);
let token = 0;

const chart = computed(() => (result.value?.kind === 'meter' ? meterChart(result.value.rows) : null));
const memoryLine = computed(() => (chart.value ? sparkPoints(chart.value.points.map((p) => p.memory), 560, 96) : ''));
const cpuLine = computed(() => (chart.value ? sparkPoints(chart.value.points.map((p) => p.cpu), 560, 96) : ''));
const manyClients = computed(() => {
  if (result.value?.kind !== 'meter' || chart.value) return false;
  return new Set(result.value.rows.map((r) => r.clientId).filter(Boolean)).size > 1;
});

async function load(): Promise<void> {
  const mine = ++token;
  loadFailed.value = false;
  const res = await fetchObserve(req(), props.kind, props.appId, local.value);
  if (mine !== token) return;
  if (res === null) {
    loadFailed.value = true;
    result.value = null;
    return;
  }
  result.value = res;
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    loadedKey = '';
    await run(local.value, true);
  },
  outcome: () => ({
    loadError: loadFailed.value ? FAIL[props.kind] : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

function sameQuery(a: ObserveQuery, b: ObserveQuery): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

function queryKey(q: ObserveQuery): string {
  return `${props.appId}|${props.kind}|${JSON.stringify(q)}`;
}

async function run(q: ObserveQuery, force = false): Promise<void> {
  const key = queryKey(q);
  if (!force && key === loadedKey) return;
  loadedKey = key;
  local.value = q;
  loading.value = true;
  try {
    await load();
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.appId, props.kind, JSON.stringify(props.query ?? emptyObserveQuery())] as const,
  () => {
    void run({ ...emptyObserveQuery(), ...(props.query ?? {}) });
  },
  { immediate: true },
);

function apply(patch: Partial<ObserveQuery>): void {
  const next = { ...local.value, ...patch };
  if (sameQuery(next, local.value)) return;
  emit('query', next);
  void run(next, true);
}

function successText(v: boolean | null): string {
  if (v === null) return '—';
  return v ? '成功' : '失败';
}
function enableText(v: boolean | null): string {
  if (v === null) return '—';
  return v ? '启用' : '停用';
}
</script>

<template>
  <Card
    padding="none"
    data-spec="app-observe"
    :data-kind="kind"
  >
    <div class="flex flex-wrap items-center gap-2 border-b border-[var(--el-border-color-light)] px-[18px] py-3">
      <h3 :class="H3">
        {{ TITLE[kind] }}
      </h3>
      <template v-if="kind === 'appHistory'">
        <input
          :value="local.client"
          class="h-[28px] w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="客户端"
          data-spec="observe-client"
          @change="apply({ client: ($event.target as HTMLInputElement).value })"
        >
        <input
          :value="local.action"
          class="h-[28px] w-[120px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="操作"
          data-spec="observe-action"
          @change="apply({ action: ($event.target as HTMLInputElement).value })"
        >
        <select
          :class="SELECT"
          :value="local.success"
          data-spec="observe-success"
          @change="apply({ success: ($event.target as HTMLSelectElement).value as ObserveQuery['success'] })"
        >
          <option value="">
            全部状态
          </option>
          <option value="1">
            成功
          </option>
          <option value="0">
            失败
          </option>
        </select>
      </template>
      <template v-else-if="kind === 'commands'">
        <input
          :value="local.command"
          class="h-[28px] w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="命令名"
          data-spec="observe-command"
          @change="apply({ command: ($event.target as HTMLInputElement).value })"
        >
        <input
          :value="local.q"
          class="h-[28px] w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="关键字"
          data-spec="observe-q"
          @change="apply({ q: ($event.target as HTMLInputElement).value })"
        >
      </template>
      <template v-else-if="kind === 'meter'">
        <input
          :value="local.clientId"
          class="h-[28px] w-[160px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="实例"
          data-spec="observe-client-id"
          @change="apply({ clientId: ($event.target as HTMLInputElement).value })"
        >
        <input
          :value="local.source"
          class="h-[28px] w-[120px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="来源"
          data-spec="observe-source"
          @change="apply({ source: ($event.target as HTMLInputElement).value })"
        >
      </template>
      <template v-else>
        <input
          :value="local.client"
          class="h-[28px] w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="客户端"
          data-spec="observe-client"
          @change="apply({ client: ($event.target as HTMLInputElement).value })"
        >
        <input
          :value="local.q"
          class="h-[28px] w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-2 text-[12px] text-[var(--el-text-color-primary)] outline-none"
          placeholder="关键字"
          data-spec="observe-q"
          @change="apply({ q: ($event.target as HTMLInputElement).value })"
        >
        <select
          :class="SELECT"
          :value="local.enable"
          data-spec="observe-enable"
          @change="apply({ enable: ($event.target as HTMLSelectElement).value as ObserveQuery['enable'] })"
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
      </template>
      <input
        :value="local.dtStart"
        type="date"
        :class="SELECT"
        data-spec="observe-start"
        @change="apply({ dtStart: ($event.target as HTMLInputElement).value })"
      >
      <input
        :value="local.dtEnd"
        type="date"
        :class="SELECT"
        data-spec="observe-end"
        @change="apply({ dtEnd: ($event.target as HTMLInputElement).value })"
      >
      <span class="flex-1" />
      <Btn
        size="sm"
        :disabled="refreshing"
        :aria-busy="refreshing"
        data-spec="observe-refresh"
        @click="refresh"
      >
        <template #icon>
          <span :class="refreshing ? 'inline-flex animate-spin' : 'inline-flex'">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        {{ refreshing ? '刷新中…' : '刷新' }}
      </Btn>
    </div>

    <p
      v-if="loading"
      class="px-[18px] py-8 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="observe-loading"
    >
      {{ TITLE[kind] }}加载中…
    </p>
    <p
      v-else-if="loadFailed"
      class="px-[18px] py-8 text-center text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="observe-failed"
    >
      {{ FAIL[kind] }}，请点「刷新」重试
    </p>
    <p
      v-else-if="!result || result.rows.length === 0"
      class="px-[18px] py-8 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="observe-empty"
    >
      {{ EMPTY[kind] }}
    </p>
    <template v-else>
      <div
        v-if="kind === 'meter' && chart"
        class="border-b border-[var(--el-border-color-light)] px-[18px] py-3"
        data-spec="meter-chart"
      >
        <div class="mb-2 flex flex-wrap items-center gap-3 text-[12px] text-[var(--el-text-color-secondary)]">
          <span class="font-mono text-[var(--el-text-color-primary)]">{{ chart.clientId }}</span>
          <span class="text-[var(--el-color-primary)]">内存</span>
          <span class="text-[var(--el-color-warning)]">CPU负载</span>
          <span>两条线各自按最小最大值拉伸，只看走势</span>
        </div>
        <svg
          viewBox="0 0 560 96"
          class="h-[96px] w-full"
          role="img"
          aria-label="性能走势"
        >
          <polyline
            :points="memoryLine"
            fill="none"
            stroke="var(--el-color-primary)"
            stroke-width="1.5"
          />
          <polyline
            :points="cpuLine"
            fill="none"
            stroke="var(--el-color-warning)"
            stroke-width="1.5"
          />
        </svg>
      </div>
      <p
        v-else-if="manyClients"
        class="border-b border-[var(--el-border-color-light)] px-[18px] py-2 text-[12px] text-[var(--el-text-color-secondary)]"
        data-spec="meter-pick"
      >
        多个实例，点「实例」列查看该实例的曲线
      </p>

      <div class="overflow-x-auto">
        <table
          v-if="result.kind === 'appHistory'"
          class="w-full border-collapse"
        >
          <thead>
            <tr>
              <th :class="TH">
                时间
              </th>
              <th :class="TH">
                客户端
              </th>
              <th :class="TH">
                节点
              </th>
              <th :class="TH">
                操作
              </th>
              <th :class="TH">
                状态
              </th>
              <th :class="TH">
                内容
              </th>
              <th :class="TH">
                创建者
              </th>
              <th :class="TH">
                追踪
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in (result.rows as HistoryRow[])"
              :key="r.id"
              class="group"
              data-spec="observe-row"
            >
              <td :class="[TD, 'whitespace-nowrap font-mono text-[12px]']">
                {{ r.createTime || '—' }}
              </td>
              <td :class="TD">
                <button
                  type="button"
                  class="cursor-pointer border-0 bg-transparent p-0 font-mono text-[12px] text-[var(--el-color-primary)]"
                  data-spec="observe-drill-client"
                  @click="apply({ client: r.client })"
                >
                  {{ r.client || '—' }}
                </button>
              </td>
              <td :class="TD">
                {{ r.node || '—' }}
              </td>
              <td :class="TD">
                <button
                  type="button"
                  class="cursor-pointer border-0 bg-transparent p-0 text-[12.5px] text-[var(--el-color-primary)]"
                  data-spec="observe-drill-action"
                  @click="apply({ action: r.action })"
                >
                  {{ r.action || '—' }}
                </button>
              </td>
              <td :class="TD">
                <Tag :tone="r.success === false ? 'err' : r.success ? 'ok' : 'default'">
                  {{ successText(r.success) }}
                </Tag>
              </td>
              <td :class="[TD, 'max-w-[280px] truncate']">
                {{ r.remark || '—' }}
              </td>
              <td :class="TD">
                {{ r.creator || '—' }}
              </td>
              <td :class="[TD, 'font-mono text-[11px] text-[var(--el-text-color-secondary)]']">
                <RouterLink
                  v-if="r.traceId"
                  :to="`/trace/${r.traceId}`"
                  class="text-[var(--el-color-primary)] hover:underline"
                  data-spec="observe-trace-link"
                >
                  {{ r.traceId }}
                </RouterLink>
                <template v-else>
                  —
                </template>
              </td>
            </tr>
          </tbody>
        </table>

        <table
          v-else-if="result.kind === 'commands'"
          class="w-full border-collapse"
        >
          <thead>
            <tr>
              <th :class="TH">
                创建者
              </th>
              <th :class="TH">
                更新时间
              </th>
              <th :class="TH">
                命令
              </th>
              <th :class="TH">
                参数
              </th>
              <th :class="TH">
                状态
              </th>
              <th :class="TH">
                次数
              </th>
              <th :class="TH">
                过期
              </th>
              <th :class="TH">
                结果
              </th>
              <th :class="TH">
                追踪
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in (result.rows as CommandRow[])"
              :key="r.id"
              class="group"
              data-spec="observe-row"
            >
              <td :class="TD">
                {{ r.createUser || '—' }}
              </td>
              <td :class="[TD, 'whitespace-nowrap font-mono text-[12px]']">
                {{ r.updateTime || '—' }}
              </td>
              <td :class="TD">
                <button
                  type="button"
                  class="cursor-pointer border-0 bg-transparent p-0 font-mono text-[12px] text-[var(--el-color-primary)]"
                  data-spec="observe-drill-command"
                  @click="apply({ command: r.command })"
                >
                  {{ r.command || '—' }}
                </button>
              </td>
              <td :class="[TD, 'max-w-[200px] truncate font-mono text-[12px]']">
                {{ r.argument || '—' }}
              </td>
              <td :class="TD">
                {{ r.status || '—' }}
              </td>
              <td :class="TD">
                {{ r.times || '—' }}
              </td>
              <td :class="[TD, 'whitespace-nowrap font-mono text-[12px]']">
                {{ r.expire || '—' }}
              </td>
              <td :class="[TD, 'max-w-[240px] truncate']">
                {{ r.result || '—' }}
              </td>
              <td :class="[TD, 'font-mono text-[11px] text-[var(--el-text-color-secondary)]']">
                <RouterLink
                  v-if="r.traceId"
                  :to="`/trace/${r.traceId}`"
                  class="text-[var(--el-color-primary)] hover:underline"
                  data-spec="observe-trace-link"
                >
                  {{ r.traceId }}
                </RouterLink>
                <template v-else>
                  —
                </template>
              </td>
            </tr>
          </tbody>
        </table>

        <table
          v-else-if="result.kind === 'meter'"
          class="w-full border-collapse"
        >
          <thead>
            <tr>
              <th :class="TH">
                时间
              </th>
              <th :class="TH">
                实例
              </th>
              <th :class="TH">
                来源
              </th>
              <th :class="TH">
                内存
              </th>
              <th :class="TH">
                CPU
              </th>
              <th :class="TH">
                线程
              </th>
              <th :class="TH">
                句柄
              </th>
              <th :class="TH">
                连接
              </th>
              <th :class="TH">
                堆
              </th>
              <th :class="TH">
                GC
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in (result.rows as MeterRow[])"
              :key="r.id"
              class="group"
              data-spec="observe-row"
            >
              <td :class="[TD, 'whitespace-nowrap font-mono text-[12px]']">
                {{ r.time || '—' }}
              </td>
              <td :class="TD">
                <button
                  type="button"
                  class="cursor-pointer border-0 bg-transparent p-0 font-mono text-[12px] text-[var(--el-color-primary)]"
                  data-spec="observe-drill-client-id"
                  @click="apply({ clientId: r.clientId })"
                >
                  {{ r.clientId || '—' }}
                </button>
              </td>
              <td :class="TD">
                <button
                  type="button"
                  class="cursor-pointer border-0 bg-transparent p-0 text-[12.5px] text-[var(--el-color-primary)]"
                  @click="apply({ source: r.source, clientId: r.clientId })"
                >
                  {{ r.source || '—' }}
                </button>
              </td>
              <td :class="TD">
                {{ formatBytes(r.memory) }}
              </td>
              <td :class="TD">
                {{ formatCpu(r.cpu) }}
              </td>
              <td :class="TD">
                {{ r.threads }}
              </td>
              <td :class="TD">
                {{ r.handles }}
              </td>
              <td :class="TD">
                {{ r.connections }}
              </td>
              <td :class="TD">
                {{ formatBytes(r.heap) }}
              </td>
              <td :class="TD">
                {{ r.gc }}
              </td>
            </tr>
          </tbody>
        </table>

        <table
          v-else
          class="w-full border-collapse"
        >
          <thead>
            <tr>
              <th :class="TH">
                服务
              </th>
              <th :class="TH">
                客户端
              </th>
              <th :class="TH">
                节点
              </th>
              <th :class="TH">
                启用
              </th>
              <th :class="TH">
                地址
              </th>
              <th
                v-if="result.kind === 'consume'"
                :class="TH"
              >
                作用域
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in (result.rows as Array<ServiceRow | ConsumeRow>)"
              :key="r.id"
              class="group"
              data-spec="observe-row"
            >
              <td :class="TD">
                <button
                  type="button"
                  class="cursor-pointer border-0 bg-transparent p-0 text-[12.5px] text-[var(--el-color-primary)]"
                  data-spec="observe-drill-service"
                  @click="apply({ q: r.serviceName })"
                >
                  {{ r.serviceName || '—' }}
                </button>
              </td>
              <td :class="TD">
                <button
                  type="button"
                  class="cursor-pointer border-0 bg-transparent p-0 font-mono text-[12px] text-[var(--el-color-primary)]"
                  data-spec="observe-drill-client"
                  @click="apply({ client: r.client })"
                >
                  {{ r.client || '—' }}
                </button>
              </td>
              <td :class="TD">
                {{ r.node || '—' }}
              </td>
              <td :class="TD">
                <Tag :tone="r.enable === false ? 'err' : r.enable ? 'ok' : 'default'">
                  {{ enableText(r.enable) }}
                </Tag>
              </td>
              <td :class="[TD, 'max-w-[280px] truncate font-mono text-[12px]']">
                {{ r.address || '—' }}
              </td>
              <td
                v-if="result.kind === 'consume'"
                :class="TD"
              >
                {{ (r as ConsumeRow).scope || '—' }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </Card>
</template>
