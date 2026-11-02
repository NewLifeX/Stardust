<script setup lang="ts">
/**
 * NowPage：「此刻」页（/now）容器，对照原型 console/page-now.jsx。
 * 自取数：now.logic.fetchNow 四段并行（告警 / 节点舰队 / 应用 / 最近变更），各段错误独立进 errors，
 * 页面在对应段落给可见横幅 + 重试，不整页打空、不静默空态、不造演示数据。
 * projectId 取自 stardustContext；上下文就绪 / 切换经 useContextReload 自动重取。
 * 跳转一律 emit('go', path)，薄壳 Console/Now.vue 接 router.push（gallery 里没有 router，CT 才能挂载）。
 * deps.request 是 story/CT 测试缝（默认接 cube-vue request）。
 *
 * 原型里 OrderService/PayService 的错误率图、链路瀑布、日志是演示数据，不进生产；
 * 告警展开后的证据区只展示 Incident 已有字段（content/category/action/since/relText/times）。
 * 处置按钮（回滚/调整配置/静默）后端没有对应接口，只发 toast 不发请求。
 */
import { computed, ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Dot from './Dot.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import {
  fetchNow,
  type FleetNode,
  type NowApp,
  type NowData,
  type NowErrors,
  type NowResult,
} from './now.logic.ts';
import { openPalette } from './ui.store.ts';
import { buildBasePalette } from './palette.items.ts';
import { toast } from './useToast.ts';
import { environmentLabel, stardustContext } from '@/stores/stardustContext';
import { useContextReload } from './useContextReload.ts';

const props = defineProps<{
  /** 测试缝：注入 request；生产不传 */
  deps?: { request?: Req };
}>();

const emit = defineEmits<{ go: [path: string] }>();

/** 取数入口：默认 cube-vue request，story/CT 注入桩 */
function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const EMPTY: NowData = {
  incidents: [],
  nodes: [],
  onlineNodes: 0,
  totalNodes: 0,
  instanceTotal: 0,
  apps: [],
  changes: [],
};

const loading = ref(true);
const refreshing = ref(false);
const data = ref<NowData>({ ...EMPTY });
const errors = ref<NowErrors>({});
/** 展开的告警卡片 id；数据回来后默认展开第一条 */
const openId = ref<number | null>(null);

async function load(isStale: () => boolean): Promise<void> {
  refreshing.value = true;
  const started = Date.now();
  let result: NowResult | null = null;
  try {
    result = await fetchNow(req(), { projectId: stardustContext.projectId });
  } catch {
    result = null;
  }
  if (isStale()) return;
  if (result) {
    data.value = result.data;
    errors.value = result.errors;
    if (openId.value === null && result.data.incidents.length > 0) {
      openId.value = result.data.incidents[0].id;
    }
  }
  loading.value = false;
  // AGENTS.md §11：刷新按钮至少旋转约 300ms，请求太快也要让用户看到
  const rest = 300 - (Date.now() - started);
  if (rest > 0) await new Promise((r) => setTimeout(r, rest));
  refreshing.value = false;
}

// 上下文（项目/环境）就绪后首轮取数，之后切换自动重取
const { reload } = useContextReload(load, stardustContext);

// ---------- Hero ----------

const envLabel = computed(() =>
  stardustContext.environment ? environmentLabel(stardustContext.environment) : '全部',
);
const hasIncidents = computed(() => data.value.incidents.length > 0);
const lede = computed(() => {
  if (loading.value) return '';
  return hasIncidents.value
    ? '按影响面排序。点开卡片，证据就在眼前，处置按钮就在下面。'
    : '过去一小时没有未处理的告警。';
});

/** ⌘K 搜索框：打开基础命令面板（与顶栏搜索、⌘K 同一个 openPalette） */
function onSearch(): void {
  openPalette(buildBasePalette({ go: (p) => emit('go', p) }));
}

const VERBS: ReadonlyArray<{ k: string; label: string }> = [
  { k: 'P', label: '发布' },
  { k: 'C', label: '改配置' },
  { k: 'L', label: '看日志' },
  { k: 'N', label: '连节点' },
  { k: 'A', label: '接入' },
];

/** 动词键按钮：复用全局热键通道——派发同一种 keydown，index.vue 的 useGlobalHotkeys 统一处理 */
function onVerb(k: string): void {
  window.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true }));
}

// ---------- 节点舰队 ----------

const FLEET_TONE: Record<FleetNode['status'], string> = {
  ok: 'bg-[var(--el-color-success)]',
  high: 'bg-[var(--el-color-warning)]',
  off: 'bg-[var(--el-text-color-placeholder)] opacity-40',
};

// ---------- 告警卡片 ----------

function toggle(id: number): void {
  openId.value = openId.value === id ? null : id;
}

/** 处置按钮：后端没有 open→fixing→resolved 状态机，只发 toast 不发请求（AGENTS.md §11 要有反馈） */
function onFix(what: string): void {
  toast(`「${what}」：告警处置流程待后端接口支持`, 'warn');
}

// ---------- 右栏应用 ----------

const HEALTH_TONE: Record<NowApp['health'], 'ok' | 'err' | 'warn' | 'off'> = {
  ok: 'ok',
  err: 'err',
  warn: 'warn',
  off: 'off',
};
</script>

<template>
  <div data-spec="now-page">
    <!-- Hero：eyebrow 环境/在线数/实例数 → 大标题 → lede → ⌘K 搜索框 → 动词键 -->
    <section data-spec="now-hero">
      <div class="font-mono text-[11px] uppercase tracking-[0.08em] text-[var(--el-text-color-secondary)]">
        {{ envLabel }}环境 · 节点
        {{ loading ? '…' : `${data.onlineNodes}/${data.totalNodes}` }} 在线 ·
        {{ loading ? '…' : data.instanceTotal }} 个应用实例
      </div>

      <div class="mt-[6px] flex items-center gap-[16px]">
        <h1 class="m-0 text-[26px] font-semibold tracking-[-0.02em] text-[var(--el-text-color-primary)]">
          <template v-if="loading">
            正在拉取现状…
          </template>
          <template v-else-if="hasIncidents">
            <span class="font-mono text-[30px] font-semibold text-[var(--el-color-primary)]">{{ data.incidents.length }}</span>
            件事需要你
          </template>
          <template v-else>
            <span class="text-[var(--el-color-success)]">✓</span>
            一切正常
          </template>
        </h1>
        <span class="flex-1" />
        <Btn
          size="sm"
          :disabled="refreshing"
          data-spec="now-refresh"
          @click="reload"
        >
          <template #icon>
            <KIcon
              name="Refresh"
              :size="13"
              :class="refreshing ? 'animate-spin' : ''"
            />
          </template>
          {{ refreshing ? '刷新中…' : '刷新' }}
        </Btn>
      </div>

      <p
        v-if="lede"
        class="mt-[3px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        {{ lede }}
      </p>

      <!-- ⌘K 搜索框：点击打开命令面板 -->
      <button
        type="button"
        data-spec="now-ask"
        class="mt-5 flex w-full cursor-pointer items-center gap-[10px] rounded-[10px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[14px] py-[11px] text-left transition-colors hover:border-[var(--el-border-color-dark)]"
        @click="onSearch"
      >
        <KIcon
          name="Search"
          :size="14"
          class="text-[var(--el-text-color-secondary)]"
        />
        <span class="text-[13px] font-medium text-[var(--el-text-color-primary)]">想做什么？</span>
        <span class="min-w-0 flex-1 truncate text-[12px] text-[var(--el-text-color-secondary)]">
          搜应用 / 节点 / TraceId，或直接找「发布 OrderService」
        </span>
        <span class="rounded-[5px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-light)] px-[5px] py-px font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">
          ⌘K
        </span>
      </button>

      <!-- 动词键：P 发布 / C 改配置 / L 看日志 / N 连节点 / A 接入 -->
      <div class="mt-3 flex flex-wrap gap-[6px]">
        <button
          v-for="v in VERBS"
          :key="v.k"
          type="button"
          data-spec="now-verb"
          :data-verb="v.k"
          class="flex cursor-pointer items-center gap-[6px] rounded-[7px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] px-[9px] py-[5px] text-[12px] text-[var(--el-text-color-regular)] transition-colors hover:border-[var(--el-border-color)] hover:text-[var(--el-text-color-primary)]"
          @click="onVerb(v.k)"
        >
          <span class="rounded-[4px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-light)] px-[4px] py-px font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">
            {{ v.k }}
          </span>
          {{ v.label }}
        </button>
      </div>
    </section>

    <!-- 节点舰队：三色小方块，点击跳 /nodes -->
    <div class="mb-2 mt-6 flex items-center gap-[10px]">
      <b class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">节点舰队</b>
      <span class="flex items-center gap-[4px] text-[11px] text-[var(--el-text-color-secondary)]">
        <Dot tone="ok" />正常
        <Dot tone="warn" />高负载
        <Dot tone="off" />离线
      </span>
      <span class="flex-1" />
      <button
        type="button"
        data-spec="now-fleet-more"
        class="cursor-pointer text-[12px] text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]"
        @click="emit('go', '/nodes')"
      >
        星图 →
      </button>
    </div>

    <div data-spec="now-fleet">
      <div
        v-if="errors.nodes"
        data-spec="now-fleet-failed"
        class="flex items-center gap-[10px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_30%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)] px-[12px] py-[9px] text-[12px] text-[var(--el-color-danger)]"
      >
        节点舰队加载失败：{{ errors.nodes }}
        <Btn
          size="sm"
          data-spec="now-fleet-retry"
          @click="reload"
        >
          重试
        </Btn>
      </div>
      <div
        v-else-if="loading"
        data-spec="now-fleet-loading"
        class="flex items-center gap-[8px] py-[6px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        节点加载中…
      </div>
      <div
        v-else-if="data.nodes.length === 0"
        data-spec="now-fleet-empty"
        class="py-[6px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        还没有接入节点
      </div>
      <div
        v-else
        class="flex flex-wrap gap-[4px]"
      >
        <i
          v-for="n in data.nodes"
          :key="n.id"
          data-spec="now-fleet-cell"
          :data-status="n.status"
          :title="`${n.name} · CPU ${n.cpu}%`"
          class="h-[18px] w-[18px] cursor-pointer rounded-[3px]"
          :class="FLEET_TONE[n.status]"
          @click="emit('go', '/nodes')"
        />
      </div>
    </div>

    <!-- 左：需要你处理（告警卡片）；右：应用前 6 + 最近变更前 5 -->
    <div class="mt-6 grid grid-cols-1 items-start gap-x-6 gap-y-6 min-[1180px]:grid-cols-[2fr_1fr]">
      <div>
        <div class="mb-3 flex items-center gap-[8px]">
          <h2 class="m-0 text-[14px] font-semibold text-[var(--el-text-color-primary)]">
            需要你处理
          </h2>
          <span
            v-if="!loading"
            class="text-[11.5px] text-[var(--el-text-color-secondary)]"
          >
            {{ data.incidents.length }} 件
          </span>
          <span class="flex-1" />
          <button
            type="button"
            data-spec="now-alarms-more"
            class="cursor-pointer text-[12px] text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]"
            @click="emit('go', '/alarms')"
          >
            查看全部 →
          </button>
        </div>

        <div
          v-if="errors.alarms"
          data-spec="now-incidents-failed"
          class="mb-[10px] flex items-center gap-[10px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_30%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_8%,transparent)] px-[12px] py-[9px] text-[12px] text-[var(--el-color-danger)]"
        >
          告警加载失败：{{ errors.alarms }}
          <Btn
            size="sm"
            data-spec="now-incidents-retry"
            @click="reload"
          >
            重试
          </Btn>
        </div>
        <div
          v-else-if="loading"
          data-spec="now-incidents-loading"
          class="flex items-center gap-[8px] py-[14px] text-[12.5px] text-[var(--el-text-color-secondary)]"
        >
          <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
          告警加载中…
        </div>
        <div
          v-else-if="data.incidents.length === 0"
          data-spec="now-incidents-empty"
          class="rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] px-[18px] py-[16px]"
        >
          <b class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">全部处理完毕</b>
          <div class="mt-[2px] text-[12px] text-[var(--el-text-color-secondary)]">
            过去一小时没有新的告警
          </div>
        </div>

        <div
          v-for="inc in data.incidents"
          :key="inc.id"
          data-spec="now-incident"
          :data-severity="inc.severity"
          :data-open="openId === inc.id"
          class="mb-[10px] overflow-hidden rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)]"
        >
          <button
            type="button"
            data-spec="now-incident-head"
            class="flex w-full cursor-pointer items-stretch gap-[10px] px-[14px] py-[11px] text-left"
            @click="toggle(inc.id)"
          >
            <span
              class="w-[3px] self-stretch rounded"
              :class="inc.severity === 'danger' ? 'bg-[var(--el-color-danger)]' : 'bg-[var(--el-color-warning)]'"
            />
            <span class="min-w-0 flex-1">
              <span class="flex items-center gap-[8px]">
                <b class="truncate text-[13px] font-semibold text-[var(--el-text-color-primary)]">{{ inc.title }}</b>
                <Tag
                  v-if="inc.times > 1"
                  tone="warn"
                >
                  已通知 {{ inc.times }} 次
                </Tag>
              </span>
              <span class="mt-[2px] block text-[11.5px] text-[var(--el-text-color-secondary)]">
                {{ inc.category || '未分类' }} · {{ inc.action || '—' }} · 持续 {{ inc.sinceText }}<template v-if="inc.relText"> · {{ inc.relText }}</template>
              </span>
            </span>
            <KIcon
              :name="openId === inc.id ? 'ChevronDown' : 'ChevronRight'"
              :size="14"
              class="self-center text-[var(--el-text-color-secondary)]"
            />
          </button>

          <div
            v-if="openId === inc.id"
            data-spec="now-incident-body"
            class="border-t border-[var(--el-border-color-light)] px-[14px] py-[12px]"
          >
            <!-- 证据块：只有 Incident 已有的字段；原型的错误率图/链路瀑布/日志是演示数据，不进生产 -->
            <pre
              v-if="inc.content"
              data-spec="now-incident-content"
              class="m-0 whitespace-pre-wrap rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] p-[10px] font-mono text-[12px] leading-relaxed text-[var(--el-text-color-regular)]"
            >{{ inc.content }}</pre>
            <div class="mt-[8px] grid grid-cols-2 gap-x-[16px] gap-y-[4px] text-[11.5px] text-[var(--el-text-color-secondary)]">
              <span>类别：{{ inc.category || '—' }}</span>
              <span>对象：{{ inc.action || '—' }}</span>
              <span>持续：{{ inc.sinceText }}</span>
              <span>开始：{{ inc.relText || '—' }}</span>
              <span>通知次数：{{ inc.times }}</span>
            </div>
            <div class="mt-[10px] flex flex-wrap gap-[8px]">
              <Btn
                size="sm"
                variant="primary"
                data-spec="now-fix-rollback"
                @click="onFix('回滚')"
              >
                回滚
              </Btn>
              <Btn
                size="sm"
                data-spec="now-fix-config"
                @click="onFix('调整配置')"
              >
                调整配置
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                data-spec="now-fix-snooze"
                @click="onFix('静默')"
              >
                静默 1 小时
              </Btn>
            </div>
          </div>
        </div>
      </div>

      <div>
        <div class="flex items-center">
          <h2 class="m-0 text-[14px] font-semibold text-[var(--el-text-color-primary)]">
            应用
          </h2>
          <span class="flex-1" />
          <button
            type="button"
            data-spec="now-apps-more"
            class="cursor-pointer text-[12px] text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]"
            @click="emit('go', '/apps')"
          >
            全部 →
          </button>
        </div>

        <Card
          padding="none"
          class="mt-2"
        >
          <div
            v-if="errors.apps"
            data-spec="now-apps-failed"
            class="px-[14px] py-[12px] text-[12px] text-[var(--el-color-danger)]"
          >
            应用加载失败：{{ errors.apps }}
            <Btn
              size="sm"
              data-spec="now-apps-retry"
              @click="reload"
            >
              重试
            </Btn>
          </div>
          <div
            v-else-if="loading"
            data-spec="now-apps-loading"
            class="flex items-center gap-[8px] px-[14px] py-[14px] text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
            应用加载中…
          </div>
          <div
            v-else-if="data.apps.length === 0"
            data-spec="now-apps-empty"
            class="px-[14px] py-[16px] text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            还没有接入应用
          </div>
          <template v-else>
            <div
              v-for="a in data.apps"
              :key="a.id"
              data-spec="now-app"
              :data-id="a.id"
              class="flex cursor-pointer items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] transition-colors last:border-b-0 hover:bg-[var(--el-fill-color-light)]"
              @click="emit('go', `/app/overview/${a.id}`)"
            >
              <span class="grid h-[24px] w-[24px] flex-none place-items-center rounded-[6px] bg-[color-mix(in_oklab,var(--el-color-primary)_18%,transparent)] text-[11px] font-semibold text-[var(--el-color-primary-light-3)]">
                {{ (a.displayName || a.name).slice(0, 1) || '?' }}
              </span>
              <span class="min-w-0 flex-1">
                <b class="block truncate text-[12.5px] font-medium text-[var(--el-text-color-primary)]">{{ a.displayName || a.name }}</b>
                <small class="block truncate text-[11px] text-[var(--el-text-color-secondary)]">
                  {{ [a.category, a.version].filter(Boolean).join(' · ') || '未分类' }}
                </small>
              </span>
              <span class="shrink-0 text-[11px] text-[var(--el-text-color-secondary)]">{{ a.instances }} 实例</span>
              <Dot :tone="HEALTH_TONE[a.health]" />
            </div>
          </template>
        </Card>

        <div class="mb-2 mt-6 flex items-center">
          <h2 class="m-0 text-[14px] font-semibold text-[var(--el-text-color-primary)]">
            最近变更
          </h2>
          <span class="flex-1" />
          <button
            type="button"
            data-spec="now-changes-more"
            class="cursor-pointer text-[12px] text-[var(--el-text-color-secondary)] hover:text-[var(--el-text-color-primary)]"
            @click="emit('go', '/changes')"
          >
            全部 →
          </button>
        </div>

        <Card data-spec="now-changes">
          <div
            v-if="errors.changes"
            data-spec="now-changes-failed"
            class="text-[12px] text-[var(--el-color-danger)]"
          >
            最近变更加载失败：{{ errors.changes }}
            <Btn
              size="sm"
              data-spec="now-changes-retry"
              @click="reload"
            >
              重试
            </Btn>
          </div>
          <div
            v-else-if="loading"
            data-spec="now-changes-loading"
            class="flex items-center gap-[8px] py-[4px] text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
            变更加载中…
          </div>
          <div
            v-else-if="data.changes.length === 0"
            data-spec="now-changes-empty"
            class="py-[4px] text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            最近没有变更记录
          </div>
          <template v-else>
            <div
              v-for="c in data.changes"
              :key="c.id"
              data-spec="now-change"
              :data-path="c.path"
              class="flex cursor-pointer gap-[10px] border-b border-[var(--el-border-color-light)] py-[8px] transition-colors last:border-b-0 hover:bg-[var(--el-fill-color-light)]"
              @click="emit('go', c.path)"
            >
              <span
                class="inline-flex h-[20px] w-[20px] flex-none items-center justify-center rounded-[5px] border font-mono text-[11px] font-semibold"
                :class="c.kind === 'D'
                  ? 'border-[color-mix(in_oklab,var(--el-color-primary)_32%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_12%,transparent)] text-[var(--el-color-primary-light-3)]'
                  : 'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)]'"
              >
                {{ c.kind }}
              </span>
              <span class="min-w-0 flex-1">
                <span
                  class="block truncate text-[12.5px] text-[var(--el-text-color-primary)]"
                  :title="c.summary"
                >{{ c.summary }}</span>
                <small class="text-[11px] text-[var(--el-text-color-secondary)]">{{ c.time }}<template v-if="c.operator"> · {{ c.operator }}</template></small>
              </span>
              <Tag
                v-if="c.ok === true"
                tone="ok"
              >
                成功
              </Tag>
              <Tag
                v-else-if="c.ok === false"
                tone="err"
              >
                失败
              </Tag>
            </div>
          </template>
        </Card>
      </div>
    </div>
  </div>
</template>
