<script setup lang="ts">
/**
 * TraceDetail：全局调用链页 /trace/:traceId（F14）。
 * 顶部 TraceId 输入框支持逗号多值（parseTraceIds 拆分去重，上限 5 个）：
 * 服务端 /Monitors/SampleData?traceId= 是单值查询、没有「一次返回多条链路」的接口，
 * 多值=前端逐条并行拉取后**并列展示各链路独立时间轴**（UI 明确标注，不是合并视图）。
 * 视图切换（2026-11-04 用户拍板接入 /api/trace/graph）：瀑布（默认）｜关系图
 * ——关系图内嵌 TraceGraph 组件（API 优先、SampleData 自解析回退，见 traceGraph.logic.ts）。
 * 复用 trace.logic 的 toSpans/buildTraceList/buildWaterfall/summarizeTrace。
 * 入口深链：AppObserve（应用历史/命令）、DeployHistory、TraceList 的 TraceId 链接。
 * 取数经 deps 注入（story/CT 假实现，生产走真实 request）。
 */
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import realRequest from '@newlifex/cube-vue/core/utils/request';
import TraceGraph from '../../components/TraceGraph.vue';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Card from '@/layouts/StardustLayout2/Card.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import Tag from '@/layouts/StardustLayout2/Tag.vue';
import {
  buildTraceList,
  buildWaterfall,
  fetchTraceSpans,
  parseTraceIds,
  shortId,
  summarizeTrace,
} from '@/layouts/StardustLayout2/trace.logic';
import type { Span, TraceSummary, WaterfallBar } from '@/layouts/StardustLayout2/trace.logic';
import type { GraphResult } from '../../components/traceGraph.logic';
import type { Req } from '../Stardust/appsModel';

export interface TraceDetailDeps {
  fetchSpans: (request: Req, traceId: string) => Promise<Span[] | null>;
  /** 覆盖关系图取数决策（透传给 TraceGraph deps.resolve）；story/CT 桩用 */
  graph?: (request: Req, traceId: string) => Promise<GraphResult>;
}

const DEFAULT_DEPS: TraceDetailDeps = { fetchSpans: fetchTraceSpans };

const route = useRoute();
const router = useRouter();

const props = defineProps<{
  deps?: TraceDetailDeps;
  /** story/CT 种子：无路由环境（gallery）直接从 prop 取初始 TraceId */
  traceId?: string;
  /** story/CT 种子：初始视图（默认瀑布，保持既有基线） */
  initialView?: 'waterfall' | 'graph';
}>();

function req(): Req {
  return realRequest as Req;
}

const input = ref('');
const loading = ref(false);
const loadError = ref(false);
/** 视图切换：瀑布（默认）｜关系图（TraceGraph：API 优先、SampleData 自解析回退） */
const view = ref<'waterfall' | 'graph'>(props.initialView ?? 'waterfall');
/** 每条链路一份：summary + 片段；null=取数失败 */
interface TraceUnit {
  summary: TraceSummary;
  spans: Span[] | null;
}
const units = ref<TraceUnit[]>([]);
const queried = ref(false);
/** 最近一次成功发起查询的归一化 ids（路由 watch 与手动查询共用，防止同值重复拉取） */
const lastQueried = ref('');

const multi = computed(() => units.value.length > 1);

const barsOf = (spans: Span[] | null): WaterfallBar[] => buildWaterfall(spans ?? []);
const diagOf = (spans: Span[] | null) => summarizeTrace(spans ?? []);

/** 关系图 deps：把页面 deps.graph 透传给 TraceGraph 的 resolve（无注入则组件走默认真实取数） */
const graphDeps = computed(() => (props.deps?.graph ? { resolve: props.deps.graph } : undefined));

async function query(raw: string): Promise<void> {
  const ids = parseTraceIds(raw);
  lastQueried.value = ids.join(',');
  units.value = [];
  loadError.value = false;
  if (!ids.length) {
    queried.value = true;
    return;
  }
  loading.value = true;
  try {
    const deps = props.deps ?? DEFAULT_DEPS;
    const results = await Promise.all(ids.map((id) => deps.fetchSpans(req(), id)));
    const out: TraceUnit[] = [];
    for (let i = 0; i < ids.length; i++) {
      const spans = results[i];
      if (spans === null) {
        out.push({ summary: { traceId: ids[i], name: ids[i], ok: false, cost: 0, startAt: 0, spanCount: 0 }, spans: null });
        continue;
      }
      // 单条链路的摘要（名称/耗时/成败/片段数）从真实片段聚合
      const list = buildTraceList(spans, 1);
      out.push({
        summary: list[0] ?? { traceId: ids[i], name: ids[i], ok: true, cost: 0, startAt: 0, spanCount: 0 },
        spans,
      });
    }
    // 有失败（任一 null）→ 顶部给失败提示但不吞掉成功链路
    loadError.value = results.some((r) => r === null);
    units.value = out;
  } catch {
    // deps 实现抛错（如桩形状错/网络异常）：按整页失败处理，不留 loading 半态
    loadError.value = true;
    units.value = [];
  } finally {
    loading.value = false;
    queried.value = true;
  }
}

/** 路由参数进来自动查询（/trace/:traceId，参数可含逗号多值）；兼容旧 MVC 链 /trace?id=xxx（SampleDataController TraceUrl）；无路由环境退回 props.traceId */
watch(
  () => {
    const r = route as unknown as { params?: Record<string, unknown>; query?: Record<string, unknown> } | undefined;
    const v = r?.params?.traceId;
    if (v !== undefined && v !== null && v !== '') return Array.isArray(v) ? v.join(',') : String(v);
    // 旧链形态：/trace?id=<traceId>（无 path 参数时看 query.id）
    const q = r?.query?.id;
    if (q !== undefined && q !== null && q !== '') return Array.isArray(q) ? q.join(',') : String(q);
    return props.traceId ?? '';
  },
  (raw) => {
    if (raw && raw !== lastQueried.value) {
      input.value = raw;
      void query(raw);
    }
  },
  { immediate: true },
);

function onSearch(): void {
  const ids = parseTraceIds(input.value);
  if (!ids.length) return;
  const joined = ids.join(',');
  if (joined === lastQueried.value) return; // 已在展示同值结果，不重复拉取
  input.value = joined;
  // 本地先查（路由 watch 对同值由 lastQueried 去重）；同时更新路由保持可回退/可分享
  void query(joined);
  const r = router as unknown as { push?: (to: string) => void } | undefined;
  if (r?.push) r.push(`/trace/${joined}`);
}

function goBack(): void {
  const r = router as unknown as { back?: () => void; push?: (to: string) => void } | undefined;
  if (r?.back && window.history.length > 1) r.back();
  else if (r?.push) r.push('/now');
}
</script>

<template>
  <div data-spec="page-trace">
    <PageHead
      eyebrow="观测 · 调用链"
      title="链路详情"
      sub="按 TraceId 查看整段链路瀑布或关系图（瀑布/关系图可切换）。支持逗号分隔多值（最多 5 个），多值并列展示各链路独立结果。"
    >
      <template #actions>
        <Btn
          size="sm"
          variant="ghost"
          data-spec="trace-back"
          @click="goBack"
        >
          返回
        </Btn>
      </template>
    </PageHead>

    <Card
      padding="none"
      class="mt-3"
      data-spec="trace-search"
    >
      <div class="flex flex-wrap items-center gap-[10px] px-[18px] py-3">
        <input
          v-model="input"
          type="text"
          placeholder="输入 TraceId，多个用逗号分隔（如 405, 369299）"
          data-spec="trace-input"
          class="h-[30px] w-[420px] rounded-[8px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[10px] font-mono text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          @keyup.enter="onSearch"
        >
        <Btn
          variant="primary"
          :disabled="loading || !input.trim()"
          data-spec="trace-search-btn"
          @click="onSearch"
        >
          <template #icon>
            <span :class="loading ? 'inline-flex animate-spin' : 'inline-flex'">
              <KIcon
                name="Search"
                :size="12"
              />
            </span>
          </template>
          {{ loading ? '查询中…' : '查询' }}
        </Btn>
        <span
          class="flex items-center gap-[4px]"
          data-spec="trace-view-toggle"
        >
          <Btn
            size="sm"
            :variant="view === 'waterfall' ? 'primary' : 'ghost'"
            data-spec="trace-view-waterfall"
            @click="view = 'waterfall'"
          >
            瀑布
          </Btn>
          <Btn
            size="sm"
            :variant="view === 'graph' ? 'primary' : 'ghost'"
            data-spec="trace-view-graph"
            @click="view = 'graph'"
          >
            关系图
          </Btn>
        </span>
        <span
          v-if="multi && !loading"
          class="text-[11.5px] text-[var(--el-text-color-secondary)]"
          data-spec="trace-multi-note"
        >
          多值并列展示：{{ units.length }} 条链路各自独立{{ view === 'waterfall' ? '时间轴' : '关系图' }}（服务端按 TraceId 单值查询，无合并接口）
        </span>
      </div>
    </Card>

    <div
      v-if="loading"
      class="mt-4 rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="trace-detail-loading"
    >
      正在拉取链路片段…
    </div>

    <template v-else>
      <!-- 部分失败：横幅提示与成功的链路卡并存（不互相吞掉） -->
      <div
        v-if="queried && loadError"
        class="mt-4 rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] py-6 text-center text-[12.5px] text-[var(--el-color-danger)]"
        data-spec="trace-detail-error"
      >
        {{ units.length ? '部分链路片段拉取失败（其余正常展示）' : '链路片段拉取失败，请重试' }}
      </div>

      <div
        v-if="queried && !loadError && !units.length"
        class="mt-4 rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
        data-spec="trace-detail-empty-input"
      >
        请输入至少一个 TraceId
      </div>

      <Card
        v-for="(u, i) in units"
        :key="u.summary.traceId"
        padding="none"
        class="mt-4"
        data-spec="trace-unit"
        :data-trace-id="u.summary.traceId"
      >
        <div class="flex flex-wrap items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[18px] py-3">
          <h3 class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">
            {{ u.summary.name }}
          </h3>
          <Tag :tone="u.summary.ok ? 'ok' : 'err'">
            {{ u.summary.ok ? '成功' : '失败' }}
          </Tag>
          <Tag tone="brand">
            {{ u.summary.cost }}ms
          </Tag>
          <span
            v-if="multi"
            class="text-[11.5px] text-[var(--el-text-color-secondary)]"
          >
            #{{ i + 1 }}
          </span>
          <span class="flex-1" />
          <span
            class="font-mono text-[11px] text-[var(--el-text-color-secondary)]"
            data-spec="trace-unit-id"
          >
            {{ shortId(u.summary.traceId) }} · {{ u.summary.spanCount }} 片段
          </span>
        </div>

        <div class="px-[18px] py-[14px]">
          <!-- 关系图视图：TraceGraph 自取数（API 优先、SampleData 自解析回退），与瀑布取数相互独立 -->
          <template v-if="view === 'graph'">
            <TraceGraph
              :trace-id="u.summary.traceId"
              height="360px"
              :deps="graphDeps"
            />
          </template>
          <template v-else-if="u.spans === null">
            <div
              class="py-6 text-center text-[12.5px] text-[var(--el-color-danger)]"
              data-spec="trace-unit-failed"
            >
              该链路片段拉取失败
            </div>
          </template>
          <template v-else-if="!u.spans.length">
            <div
              class="py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
              data-spec="trace-unit-empty"
            >
              该 TraceId 近 30 天没有采样片段（后端按 TraceId 查询窗口为近 30 天）
            </div>
          </template>
          <template v-else>
            <div
              class="space-y-[4px]"
              data-spec="trace-waterfall"
            >
              <div
                v-for="b in barsOf(u.spans)"
                :key="b.key"
                class="flex items-center gap-[8px]"
                :style="{ paddingLeft: `${b.depth * 14 + 4}px` }"
              >
                <span class="w-[180px] shrink-0 truncate font-mono text-[11px] text-[var(--el-text-color-regular)]">
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
              class="mt-[12px] rounded-[8px] bg-[var(--el-fill-color-light)] p-[12px] text-[12px] leading-[1.6] text-[var(--el-text-color-regular)]"
              data-spec="trace-diag"
            >
              共 {{ diagOf(u.spans).spanCount }} 个片段，墙钟 {{ diagOf(u.spans).cost }}ms；
              <b
                v-if="diagOf(u.spans).errorCount"
                class="text-[var(--el-color-danger)]"
              >{{ diagOf(u.spans).errorCount }} 个失败</b>
              <template v-if="diagOf(u.spans).slowestName">
                最耗时片段「{{ diagOf(u.spans).slowestName }}」{{ diagOf(u.spans).slowestCost }}ms，占 {{ diagOf(u.spans).slowestPct }}%。
              </template>
            </div>
          </template>
        </div>
      </Card>
    </template>
  </div>
</template>
