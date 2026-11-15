<script setup lang="ts">
/**
 * TraceStatCard：工作台「监控大屏」页签 ·「埋点统计」区块（CARD-MON-07 路径 B 定稿）。
 * 追踪三统计（TraceDayStat/TraceHourStat/TraceMinuteStat）同宿主页签内：
 * 埋点下拉（GET /Monitors/TraceItem）+ 粒度三态（每天/每小时/每5分钟）+ 种类过滤标签
 * （AppDayStat C17 列链经 drill prop 触发：单日 + 种类，无需选埋点），单列表
 * （种类着色 Tag + 错误率行底色 + 总计行口径注）+ 260px 单图（调用/错误双序列，
 * 裁定不做上下双图与 DataZoom）+ 追踪列深链（resolveTrace → /trace?id=，失败 toast 逐字）。
 * 数据真实接口：GET /api/Monitors/Trace{Day|Hour|Minute}Stat?appId=&itemId=&type=&dtStart=&dtEnd=；
 * 追踪解析 GET /api/Monitors/{entity}/Trace?id= → 命名跳转 ConsoleTrace（query.id 由组件兜底消费）。
 * 口径锁定（C13 教训）：错误率一律 fmtErrorRate（×100 两位）；环比一律 fmtRingRate（倍率 1.2=+20%）。
 * 颜色：EChart 是 canvas，令牌经 cssVar() 求值后注入 option（cssVar.ts）。
 * 交互反馈按 AGENTS.md §11：加载/追踪按钮一律 loading + 禁用 + 结果 toast。
 */
import { computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import EChart from '@/components/EChart.vue';
import { cssVar } from '@/utils/cssVar';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { toast } from './useToast.ts';
import { TD, TH } from './deployView.styles.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { buildDayStatParams, fmtErrorRate, localDateStr } from './daystat.logic.ts';
import {
  TRACE_GRANULARITIES,
  buildTraceStatParams,
  fetchTraceItems,
  fetchTraceStats,
  fmtRingRate,
  traceStatChartOption,
  traceStatTotalRow,
  resolveTrace,
  type ChartPalette,
  type TraceGranularity,
  type TraceItemOption,
  type TraceStatQuery,
  type TraceStatRow,
} from './traceStat.logic.ts';

const props = defineProps<{
  appId: number;
  appName?: string;
  /** AppDayStat C17 列链钻取：单日 + 种类（seq 递增触发重拉，无需选埋点） */
  drill?: { date: string; type: string; seq: number } | null;
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const router = useRouter();

/** 埋点下拉数据源（GET /Monitors/TraceItem?appId=&pageSize=100） */
const items = ref<TraceItemOption[]>([]);
/** 选中的埋点 id：0=未选（显示引导空态；C17 列链走种类过滤，无需选埋点） */
const itemId = ref(0);
/** 粒度三态：每天 / 每小时 / 每5分钟（默认每天，与列链衔接自然） */
const granularity = ref<TraceGranularity>('day');
const days = ref<7 | 30>(7);
const rangeStart = ref('');
const rangeEnd = ref('');
/** 种类过滤（Api/Http/Db/Mq/Redis/Other；''=不过滤） */
const typeFilter = ref('');

const rangeActive = computed(() => rangeStart.value !== '' && rangeEnd.value !== '');
/** 天数下拉显示代理（与 DayStatTab 同构）：区间激活时显示「自定义」（value 0）并禁用 */
const daysModel = computed<number>({
  get: () => (rangeActive.value ? 0 : days.value),
  set: (v) => {
    if (v === 7 || v === 30) days.value = v;
  },
});

const rows = ref<TraceStatRow[]>([]);
const loading = ref(false);
const loadFailed = ref(false);
const failText = ref('');
let token = 0;

const totalRow = computed(() => traceStatTotalRow(rows.value));
/** 查询主体：选了埋点，或 C17 列链给了种类过滤 */
const canQuery = computed(() => itemId.value > 0 || typeFilter.value !== '');
/** 动态主体描述：埋点名 / 种类+时间窗 */
const subjectLabel = computed(() => {
  const it = items.value.find((x) => x.id === itemId.value);
  if (it) return it.name;
  if (typeFilter.value) {
    return `${typeLabel(typeFilter.value)} · ${rangeActive.value ? `${rangeStart.value} ~ ${rangeEnd.value}` : `最近 ${days.value} 天`}`;
  }
  return '';
});

const TYPE_LABELS: Record<string, string> = { Api: '接口', Http: 'Http', Db: '数据库', Mq: 'MQ', Redis: 'Redis', Other: '其它' };
function typeLabel(t: string): string {
  return TYPE_LABELS[t] ?? t;
}
/** 种类着色（Tag tone）：与 AppDayStat 列集语义一致（brand=主色接口/ok=Http/warn=数据与MQ/err=Redis） */
function typeTone(t: string): 'default' | 'brand' | 'ok' | 'warn' | 'err' {
  switch (t) {
    case 'Api':
      return 'brand';
    case 'Http':
      return 'ok';
    case 'Db':
      return 'warn';
    case 'Mq':
      return 'warn';
    case 'Redis':
      return 'err';
    default:
      return 'default';
  }
}

async function loadItems(): Promise<void> {
  items.value = [];
  itemId.value = 0;
  // fetchTraceItems 吞错返回 {options, error}：error 非空 toast 提示，不打断统计区
  const r = await fetchTraceItems(req(), props.appId);
  items.value = r.options;
  if (r.error) toast(`埋点下拉加载失败：${r.error}`, 'err');
}

async function load(): Promise<void> {
  const mine = ++token;
  loading.value = true;
  loadFailed.value = false;
  try {
    const win = rangeActive.value
      ? { dtStart: rangeStart.value, dtEnd: rangeEnd.value }
      : buildDayStatParams(props.appId, days.value);
    const q: TraceStatQuery = {
      appId: props.appId,
      itemId: itemId.value,
      type: typeFilter.value || undefined,
      ...win,
    };
    // 参数构造与请求同源：buildTraceStatParams 负责剔除空值并按粒度补 pageSize；吞错经 error 透传
    void buildTraceStatParams(granularity.value, q);
    const { rows: list, error } = await fetchTraceStats(req(), granularity.value, q);
    if (mine !== token) return;
    if (error) throw new Error(error);
    rows.value = list;
  } catch (e) {
    if (mine !== token) return;
    loadFailed.value = true;
    failText.value = e instanceof Error ? e.message : String(e);
    rows.value = [];
  } finally {
    if (mine === token) loading.value = false;
  }
}

/** 追踪列：解析成功跳 /trace?id={traceId}（ConsoleTrace query.id 兜底消费），失败 toast 逐字文案 */
const tracingId = ref<number | null>(null);
async function onTrace(r: TraceStatRow): Promise<void> {
  if (tracingId.value !== null) return;
  tracingId.value = r.id;
  try {
    const res = await resolveTrace(req(), granularity.value, r.id);
    if (!res.ok) {
      toast(res.message, 'err');
      return;
    }
    await router.push({ name: 'ConsoleTrace', query: { id: String(res.traceId) } });
  } catch (e) {
    toast(e instanceof Error ? e.message : String(e), 'err');
  } finally {
    tracingId.value = null;
  }
}

// ---------- 联动 ----------
// 注意顺序：appId watch 必须**声明在 drill watch 之前**——两个 immediate watch 按声明顺序执行，
// 挂载时 appId 先重置（清空过滤），随后 drill 才落单日+种类（CT 静态 props 与真实流同序，反序会清掉 drill 种类）。
watch(
  () => props.appId,
  () => {
    rows.value = [];
    loadFailed.value = false;
    typeFilter.value = '';
    void loadItems();
    if (canQuery.value) void load();
  },
  { immediate: true },
);

watch([itemId, granularity, days], () => {
  if (canQuery.value) void load();
});

watch(rangeActive, (on) => {
  if (on && canQuery.value) void load();
});

// ---------- C17 列链钻取（AppDayStat 表格行内链接 → 单日 + 种类） ----------
const sectionRef = ref<HTMLElement | null>(null);

watch(
  () => props.drill?.seq,
  () => {
    const d = props.drill;
    if (!d) return;
    granularity.value = 'day';
    itemId.value = 0;
    typeFilter.value = d.type;
    rangeStart.value = d.date;
    rangeEnd.value = d.date;
    // 二次钻取必须显式重载：rangeActive 已为 true（无边沿）时 [itemId,granularity,days] 全同值也不触发，
    // 只靠级联会出现「副标题已切种类、列表仍是旧数据」的零重载（真实流实锤，CT Drill story 初挂载测不出）。
    if (canQuery.value) void load();
    sectionRef.value?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  },
  { immediate: true },
);

function clearTypeFilter(): void {
  typeFilter.value = '';
  if (itemId.value > 0) void load();
  else rows.value = [];
}

function clearRange(): void {
  rangeStart.value = '';
  rangeEnd.value = '';
  if (canQuery.value) void load();
  else rows.value = [];
}

// ---------- 图表调色板（canvas 需 cssVar 求值注入） ----------
const palette = computed<ChartPalette>(() => ({
  primary: cssVar('--el-color-primary', '#5B8DB8'),
  danger: cssVar('--el-color-danger', '#B08C6A'),
  axis: cssVar('--el-text-color-secondary', '#8E8E8E'),
  splitLine: cssVar('--el-border-color-light', '#e4e7ed'),
}));

const chartOption = computed(() => traceStatChartOption(rows.value, palette.value));
const today = localDateStr(new Date());
</script>

<template>
  <Card data-spec="trace-stat">
    <div
      ref="sectionRef"
      class="scroll-mt-[64px]"
    >
      <div class="mb-[8px] flex flex-wrap items-center gap-2">
        <span class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">埋点统计</span>
        <span
          v-if="subjectLabel"
          class="text-[12px] text-[var(--el-text-color-secondary)]"
        >{{ subjectLabel }}</span>
        <span class="flex-1" />
        <Tag
          v-if="typeFilter"
          data-spec="trace-stat-type-tag"
          :tone="typeTone(typeFilter)"
        >
          {{ typeLabel(typeFilter) }}
          <button
            data-spec="trace-stat-type-clear"
            type="button"
            title="清除种类过滤"
            class="ml-[2px] text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]"
            @click="clearTypeFilter"
          >
            ×
          </button>
        </Tag>
        <select
          v-model.number="itemId"
          data-spec="trace-stat-item"
          aria-label="选择埋点"
          class="h-[28px] max-w-[220px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
          <option :value="0">
            选择埋点…
          </option>
          <option
            v-for="it in items"
            :key="it.id"
            :value="it.id"
          >
            {{ it.name }}{{ it.kind ? `（${it.kind}）` : '' }}
          </option>
        </select>
        <select
          v-model.number="daysModel"
          data-spec="trace-stat-days"
          :disabled="rangeActive"
          title="已选自定义日期区间，点「清除」恢复预设"
          aria-label="统计天数"
          class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)] disabled:cursor-not-allowed disabled:opacity-60"
        >
          <option :value="7">
            最近 7 天
          </option>
          <option :value="30">
            最近 30 天
          </option>
          <option
            :value="0"
            disabled
          >
            自定义
          </option>
        </select>
        <div
          data-spec="trace-stat-range"
          class="flex items-center gap-1"
        >
          <input
            v-model="rangeStart"
            type="date"
            data-spec="trace-stat-range-start"
            aria-label="开始日期"
            class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[6px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          >
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">~</span>
          <input
            v-model="rangeEnd"
            type="date"
            data-spec="trace-stat-range-end"
            aria-label="结束日期"
            class="h-[28px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[6px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          >
          <Btn
            v-if="rangeActive"
            variant="default"
            size="sm"
            data-spec="trace-stat-range-clear"
            title="清除自定义区间，恢复最近 7 天"
            @click="clearRange"
          >
            清除
          </Btn>
        </div>
        <div
          data-spec="trace-stat-granularity"
          class="flex items-center gap-1"
          role="group"
          aria-label="统计粒度"
        >
          <Btn
            v-for="g in TRACE_GRANULARITIES"
            :key="g.key"
            variant="default"
            size="sm"
            :data-spec="`trace-stat-gran-${g.key}`"
            :aria-pressed="granularity === g.key"
            @click="granularity = g.key"
          >
            {{ g.label }}
          </Btn>
        </div>
        <Btn
          variant="default"
          size="sm"
          data-spec="trace-stat-refresh"
          :disabled="loading"
          :aria-pressed="loading"
          title="重新加载埋点统计"
          @click="load"
        >
          <template #icon>
            <span
              v-if="loading"
              class="inline-flex animate-spin"
            >
              <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-text-color-secondary)]" />
            </span>
            <KIcon
              v-else
              name="Refresh"
              :size="12"
            />
          </template>
          {{ loading ? '刷新中…' : '刷新' }}
        </Btn>
      </div>

      <!-- 引导空态：未选埋点且无种类过滤 -->
      <div
        v-if="!canQuery && !loading"
        data-spec="trace-stat-empty"
        class="py-5 text-center text-[12px] text-[var(--el-text-color-secondary)]"
      >
        请选择埋点查看按天 / 每小时 / 每 5 分钟的调用、错误与耗时统计（数据截至 {{ today }}）。
      </div>

      <template v-else>
        <div
          v-if="loadFailed"
          data-spec="trace-stat-failed"
          class="mb-[8px] rounded-[8px] border border-[var(--el-color-danger)] bg-[var(--el-color-danger-light-9)] px-[10px] py-[8px] text-[12px] text-[var(--el-color-danger)]"
        >
          埋点统计加载失败：{{ failText }}
          <Btn
            variant="default"
            size="sm"
            data-spec="trace-stat-retryload"
            class="ml-[8px]"
            @click="load"
          >
            重试
          </Btn>
        </div>

        <div
          v-else-if="rows.length === 0 && !loading"
          data-spec="trace-stat-nodata"
          class="py-4 text-center text-[12px] text-[var(--el-text-color-secondary)]"
        >
          该条件下没有追踪统计数据。应用上报监控并产生调用后，这里会显示按粒度聚合的调用与耗时。
        </div>

        <template v-if="rows.length > 0">
          <EChart
            data-spec="trace-stat-chart"
            :option="chartOption"
            height="260px"
          />

          <div class="mt-[8px] overflow-x-auto">
            <table class="w-full border-collapse">
              <thead>
                <tr>
                  <th :class="TH">
                    统计时间
                  </th>
                  <th :class="TH">
                    操作名
                  </th>
                  <th :class="TH">
                    种类
                  </th>
                  <th :class="TH">
                    总次数
                  </th>
                  <th :class="TH">
                    环比
                  </th>
                  <th :class="TH">
                    错误数
                  </th>
                  <th :class="TH">
                    错误率
                  </th>
                  <th :class="TH">
                    平均耗时(ms)
                  </th>
                  <th :class="TH">
                    最大耗时(ms)
                  </th>
                  <th :class="TH">
                    最小耗时(ms)
                  </th>
                  <th :class="TH">
                    数值
                  </th>
                  <th :class="TH">
                    追踪
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="r in rows"
                  :key="r.id"
                  data-spec="trace-stat-row"
                  :data-stat-time="r.statTime"
                  :class="{
                    'bg-[var(--el-color-danger-light-9)]': r.rateClass === 'danger',
                    'bg-[var(--el-color-warning-light-9)]': r.rateClass === 'warning',
                  }"
                >
                  <td :class="TD">
                    {{ r.statTime }}
                  </td>
                  <td :class="TD">
                    {{ r.name }}
                  </td>
                  <td :class="TD">
                    <Tag
                      data-spec="trace-stat-row-type"
                      :tone="typeTone(r.type)"
                    >
                      {{ typeLabel(r.type) }}
                    </Tag>
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ r.total }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ fmtRingRate(r.ringRate) }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ r.errors }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ fmtErrorRate(r.errorRate) }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ r.cost }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ r.maxCost }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ r.minCost }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ r.totalValue > 0 ? r.totalValue : '—' }}
                  </td>
                  <td :class="TD">
                    <Btn
                      variant="default"
                      size="sm"
                      data-spec="trace-stat-trace-btn"
                      :disabled="tracingId !== null"
                      :title="`解析该统计的追踪数据（${r.statTime} ${r.name}）`"
                      @click="onTrace(r)"
                    >
                      <template #icon>
                        <span
                          v-if="tracingId === r.id"
                          class="inline-flex animate-spin"
                        >
                          <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-text-color-secondary)]" />
                        </span>
                        <KIcon
                          v-else
                          name="Activity"
                          :size="12"
                        />
                      </template>
                      {{ tracingId === r.id ? '解析中…' : '追踪' }}
                    </Btn>
                  </td>
                </tr>
                <tr
                  v-if="totalRow"
                  data-spec="trace-stat-totalrow"
                  class="border-t border-[var(--el-border-color-light)] font-medium"
                >
                  <td
                    :class="TD"
                    colspan="3"
                  >
                    {{ totalRow.label }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ totalRow.total }}
                  </td>
                  <td :class="TD">
                    —
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ totalRow.errors }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ fmtErrorRate(totalRow.errorRate) }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ totalRow.cost }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ totalRow.maxCost }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    {{ totalRow.minCost }}
                  </td>
                  <td
                    :class="TD"
                    class="tabular-nums"
                  >
                    —
                  </td>
                  <td :class="TD">
                    —
                  </td>
                </tr>
              </tbody>
            </table>
            <p class="mt-[4px] text-[11px] text-[var(--el-text-color-secondary)]">
              口径=已加载行合计，非服务端全量；错误率与总计行同源（ΣErrors/ΣTotal）。
            </p>
          </div>
        </template>
      </template>
    </div>
  </Card>
</template>
