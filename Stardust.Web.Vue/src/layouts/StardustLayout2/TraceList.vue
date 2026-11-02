<script setup lang="ts">
/**
 * TraceList：应用工作台「调用链」页签（2026-10-29）。
 * 自包含：只收 appId，内部处理 loading / error / empty 三态，取数经 deps 注入（默认接真实接口）。
 * 数据口径：/Monitors/SampleData?appId= 取今日采样片段，前端按 TraceId 聚合成调用链列表；
 * 选中后 /Monitors/SampleData?traceId= 取整段片段画瀑布（后端没有「一次返回 N 条 trace」的接口）。
 * 底部「查看相关配置」「定位实例」只发事件，不实现跳转（由容器接线）。
 * 刷新遵守 AGENTS.md §11：旋转、禁用、至少转 300ms。
 */
import { computed, onScopeDispose, ref, watch } from 'vue';
import realRequest from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Dot from './Dot.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { buildWaterfall, fetchTraceSummaries, fetchTraceSpans, shortId, summarizeTrace } from './trace.logic.ts';
import type { Span, TraceSummary, WaterfallBar } from './trace.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 取数依赖注入：story/CT 传假实现，生产用默认 */
export interface TraceDeps {
  fetchTraces: (request: Req, appId: number) => Promise<TraceSummary[] | null>;
  fetchDetail: (request: Req, traceId: string) => Promise<Span[] | null>;
}

const DEFAULT_DEPS: TraceDeps = {
  fetchTraces: fetchTraceSummaries,
  fetchDetail: fetchTraceSpans,
};

const props = defineProps<{
  appId: number;
  deps?: TraceDeps;
}>();

const emit = defineEmits<{
  openConfig: [trace: TraceSummary];
  locateInstance: [trace: TraceSummary];
}>();

const deps = props.deps ?? DEFAULT_DEPS;

const loading = ref(true);
const loadError = ref(false);
const traces = ref<TraceSummary[]>([]);
const selectedId = ref('');
const detailLoading = ref(false);
const detail = ref<Span[] | null>(null);
const refreshing = ref(false);

let loadToken = 0;

const selected = computed<TraceSummary | null>(() => traces.value.find((t) => t.traceId === selectedId.value) ?? null);
const bars = computed<WaterfallBar[]>(() => buildWaterfall(detail.value ?? []));
const diag = computed(() => summarizeTrace(detail.value ?? []));

async function loadTraces(): Promise<void> {
  const token = ++loadToken;
  loading.value = true;
  loadError.value = false;
  const list = await deps.fetchTraces(realRequest, props.appId);
  if (token !== loadToken) return;
  loading.value = false;
  if (list === null) {
    loadError.value = true;
    traces.value = [];
    selectedId.value = '';
    detail.value = null;
    return;
  }
  traces.value = list;
  await select(list[0]?.traceId ?? '');
}

async function select(id: string): Promise<void> {
  selectedId.value = id;
  detail.value = null;
  if (!id) {
    detailLoading.value = false;
    return;
  }
  detailLoading.value = true;
  const spans = await deps.fetchDetail(realRequest, id);
  if (selectedId.value !== id) return;
  detailLoading.value = false;
  detail.value = spans;
}

async function refresh(): Promise<void> {
  if (refreshing.value) return;
  refreshing.value = true;
  const started = Date.now();
  await loadTraces();
  const rest = 300 - (Date.now() - started);
  if (rest > 0) await new Promise((resolve) => setTimeout(resolve, rest));
  refreshing.value = false;
}

watch(() => props.appId, () => void loadTraces(), { immediate: true });
onScopeDispose(() => {
  loadToken++;
});
</script>

<template>
  <Card
    padding="none"
    data-spec="trace-panel"
  >
    <div class="flex items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[18px] py-3">
      <h3 class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">
        调用链
      </h3>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">
        今日采样片段按 TraceId 聚合，点左侧看瀑布
      </span>
      <span class="flex-1" />
      <Btn
        size="sm"
        :disabled="refreshing"
        :aria-busy="refreshing"
        data-spec="trace-refresh"
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

    <div
      v-if="loading"
      class="p-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="trace-loading"
    >
      <span class="inline-flex animate-spin">
        <KIcon
          name="Refresh"
          :size="14"
        />
      </span>
      正在加载调用链…
    </div>
    <div
      v-else-if="loadError"
      class="p-10 text-center text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="trace-error"
    >
      调用链加载失败，请点「刷新」重试
    </div>
    <div
      v-else-if="!traces.length"
      class="p-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="trace-empty"
    >
      该应用今天没有采样追踪记录
    </div>

    <div
      v-else
      class="grid grid-cols-[minmax(0,1fr)_minmax(0,1.6fr)]"
    >
      <!-- 左：trace 列表 -->
      <div
        class="max-h-[460px] overflow-y-auto border-r border-[var(--el-border-color-light)]"
        data-spec="trace-list"
      >
        <button
          v-for="t in traces"
          :key="t.traceId"
          type="button"
          class="flex w-full items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] text-left"
          :class="
            selectedId === t.traceId
              ? 'bg-[var(--el-fill-color-light)] shadow-[inset_2px_0_0_var(--el-color-primary)]'
              : 'hover:bg-[var(--el-fill-color-light)]'
          "
          data-spec="trace-row"
          @click="select(t.traceId)"
        >
          <Dot :tone="t.ok ? 'ok' : 'err'" />
          <span class="min-w-0 flex-1">
            <span class="block truncate text-[12.5px] text-[var(--el-text-color-primary)]">{{ t.name }}</span>
            <span class="block font-mono text-[11px] text-[var(--el-text-color-secondary)]">
              {{ shortId(t.traceId) }} · {{ t.cost }}ms
            </span>
          </span>
        </button>
      </div>

      <!-- 右：详情卡 -->
      <div
        class="p-[16px]"
        data-spec="trace-detail"
      >
        <template v-if="selected">
          <div
            v-if="detailLoading"
            class="p-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            data-spec="trace-detail-loading"
          >
            <span class="inline-flex animate-spin">
              <KIcon
                name="Refresh"
                :size="13"
              />
            </span>
            加载链路片段…
          </div>
          <template v-else-if="detail">
            <h3 class="flex flex-wrap items-center gap-[8px] text-[13px] font-semibold text-[var(--el-text-color-primary)]">
              {{ selected.name }}
              <Tag :tone="selected.ok ? 'ok' : 'err'">
                {{ selected.ok ? '成功' : '失败' }}
              </Tag>
              <Tag tone="brand">
                {{ selected.cost }}ms
              </Tag>
            </h3>
            <div
              class="mt-[6px] font-mono text-[11px] text-[var(--el-text-color-secondary)]"
              data-spec="trace-detail-id"
            >
              TraceId {{ selected.traceId }}
            </div>

            <!-- 瀑布：深度缩进 + 时间轴条 -->
            <div
              v-if="bars.length"
              class="mt-[12px] space-y-[4px]"
              data-spec="trace-waterfall"
            >
              <div
                v-for="b in bars"
                :key="b.key"
                class="flex items-center gap-[8px]"
                :style="{ paddingLeft: `${b.depth * 14 + 4}px` }"
              >
                <span class="w-[150px] shrink-0 truncate font-mono text-[11px] text-[var(--el-text-color-regular)]">
                  {{ b.name }} {{ b.cost }}ms
                </span>
                <span class="relative h-[12px] flex-1">
                  <span
                    class="absolute h-full rounded-[3px]"
                    :class="
                      b.ok
                        ? 'bg-[color-mix(in_oklab,var(--el-color-primary)_35%,transparent)]'
                        : 'bg-[color-mix(in_oklab,var(--el-color-danger)_45%,transparent)]'
                    "
                    :style="{ left: `${b.startOffset}%`, width: `${b.widthPct}%` }"
                    :title="b.error || b.name"
                  />
                </span>
              </div>
            </div>
            <div
              v-else
              class="mt-[12px] rounded-[8px] border border-dashed border-[var(--el-border-color)] p-4 text-center text-[12px] text-[var(--el-text-color-secondary)]"
              data-spec="trace-waterfall-empty"
            >
              该追踪没有采样片段，瀑布待接入
            </div>

            <!-- 诊断框：从真实片段推导，不造数据 -->
            <div
              class="mt-[12px] rounded-[8px] bg-[var(--el-fill-color-light)] p-[12px] text-[12px] leading-[1.6] text-[var(--el-text-color-regular)]"
              data-spec="trace-diag"
            >
              共 {{ diag.spanCount }} 个片段，墙钟 {{ diag.cost }}ms；
              <b
                v-if="diag.errorCount"
                class="text-[var(--el-color-danger)]"
              >{{ diag.errorCount }} 个失败</b>
              <template v-if="diag.slowestName">
                最耗时片段「{{ diag.slowestName }}」{{ diag.slowestCost }}ms，占 {{ diag.slowestPct }}%。
              </template>
            </div>

            <div class="mt-[14px] flex gap-[10px]">
              <Btn
                size="sm"
                data-spec="trace-open-config"
                @click="emit('openConfig', selected)"
              >
                查看相关配置
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                data-spec="trace-locate"
                @click="emit('locateInstance', selected)"
              >
                定位实例
              </Btn>
            </div>
          </template>
          <div
            v-else
            class="mt-6 text-[12.5px] text-[var(--el-color-danger)]"
            data-spec="trace-detail-error"
          >
            链路片段加载失败，请点「刷新」重试
          </div>
        </template>
        <div
          v-else
          class="p-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          data-spec="trace-detail-empty"
        >
          在左侧选择一条调用链查看瀑布
        </div>
      </div>
    </div>
  </Card>
</template>
