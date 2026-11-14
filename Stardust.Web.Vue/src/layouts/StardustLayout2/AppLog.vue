<script setup lang="ts">
/**
 * AppLog：应用工作台「日志」页签（级别 chip 走服务端 kind 过滤）。
 * 自包含：只收 appId，内部处理 loading / error / empty 三态，取数经 deps 注入（默认接真实接口）。
 * 数据口径：GET /Registry/AppClientLog?appId=&kind=&dtStart=&dtEnd=&q=（appId 分表感知必填、
 * kind 精确匹配 Info/Warn/Error/Fatal、q 为消息关键字、SearchSafe 缺表兜底）。
 * 级别 chip / 日期范围 / 关键字任一变化=重发服务端请求。
 * 实时：未暂停时按 intervalMs 轮询；轮询链在每次取数完成后才排下一拍，显式刷新会重置轮询，不重叠不发双请求。
 */
import { computed, nextTick, onScopeDispose, ref, watch } from 'vue';
import realRequest from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Chip from './Chip.vue';
import KIcon from './KIcon.vue';
import { fetchAppLogs, LOG_LEVEL_CHIPS, todayRange } from './appLog.logic.ts';
import type { LogLevel, LogRange, LogRow } from './appLog.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

export interface LogDeps {
  fetchLogs: (
    request: Req,
    appId: number,
    range: LogRange,
    kind: 'all' | LogLevel,
    pageSize: number,
    keyword?: string,
  ) => Promise<LogRow[] | null>;
  /** 轮询间隔毫秒；0 表示不轮询（story/CT 用） */
  intervalMs?: number;
}

const DEFAULT_INTERVAL = 8000;

const props = defineProps<{
  appId: number;
  deps?: LogDeps;
}>();

const loading = ref(true);
const loadError = ref(false);
const rows = ref<LogRow[]>([]);
const level = ref<'all' | LogLevel>('all');
const paused = ref(false);
const busy = ref(false);

// —— 日期范围与关键字（dtStart/dtEnd/q 服务端过滤）——
const today = todayRange();
const dateStart = ref(today.dtStart);
const dateEnd = ref(today.dtStart.slice(0, 10));
const keyword = ref('');
const range = computed<LogRange>(() => ({
  dtStart: dateStart.value || today.dtStart,
  dtEnd: `${dateEnd.value || today.dtStart} 23:59:59`,
}));

const box = ref<HTMLElement | null>(null);
let timer: ReturnType<typeof setTimeout> | undefined;
let qTimer: ReturnType<typeof setTimeout> | undefined;
let loadToken = 0;

async function load(silent = false): Promise<void> {
  const token = ++loadToken;
  if (!silent) loading.value = true;
  busy.value = true;
  const fn = props.deps?.fetchLogs ?? fetchAppLogs;
  const res = await fn(realRequest, props.appId, range.value, level.value, 200, keyword.value.trim());
  // 显式/轮询取数完成后才排下一拍，天然消除「chip 刷新与轮询同拍」的双请求
  schedulePoll();
  if (token !== loadToken) return;
  busy.value = false;
  loading.value = false;
  if (res === null) {
    loadError.value = true;
    return;
  }
  loadError.value = false;
  rows.value = res;
  await nextTick();
  await tailToBottom();
}

/** 只有用户已经在底部附近时才跟随新日志滚到底 */
async function tailToBottom(): Promise<void> {
  const el = box.value;
  if (!el) return;
  if (el.scrollTop + el.clientHeight >= el.scrollHeight - 80) el.scrollTop = el.scrollHeight;
}

function schedulePoll(): void {
  if (timer) clearTimeout(timer);
  timer = undefined;
  const interval = props.deps?.intervalMs ?? DEFAULT_INTERVAL;
  if (interval <= 0 || paused.value) return;
  timer = setTimeout(() => {
    timer = undefined;
    void load(true);
  }, interval);
}

// 级别 chip=服务端 kind 过滤 → 切换时重发请求；load 内部会重置轮询节奏
watch([level, dateStart, dateEnd], () => {
  void load();
});

// 关键字防抖 400ms 后重发（服务端 q 模糊匹配）
watch(keyword, () => {
  if (qTimer) clearTimeout(qTimer);
  qTimer = setTimeout(() => {
    qTimer = undefined;
    void load();
  }, 400);
});

watch(() => props.appId, () => {
  void load();
}, { immediate: true });

watch(paused, (p) => {
  if (p) {
    if (timer) clearTimeout(timer);
    timer = undefined;
  } else {
    void load(true);
  }
});

onScopeDispose(() => {
  loadToken++;
  if (timer) clearTimeout(timer);
  if (qTimer) clearTimeout(qTimer);
});

/** 级别颜色：Info 青 / Warn 琥珀 / Error 珊瑚 / Debug 灰（全部走 --el-* 令牌） */
function levelStyle(l: LogLevel | ''): Record<string, string> {
  switch (l) {
    case 'error':
      return { color: 'var(--el-color-danger)' };
    case 'warn':
      return { color: 'var(--el-color-warning)' };
    case 'info':
      return { color: 'var(--el-color-primary)' };
    default:
      return { color: 'var(--el-text-color-secondary)' };
  }
}
</script>

<template>
  <Card
    padding="none"
    data-spec="log-panel"
  >
    <div class="flex flex-wrap items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[18px] py-3">
      <h3 class="text-[13px] font-semibold text-[var(--el-text-color-primary)]">
        日志
      </h3>
      <Chip
        v-for="c in LOG_LEVEL_CHIPS"
        :key="c.key"
        :on="level === c.key"
        data-spec="log-chip"
        @click="level = c.key"
      >
        {{ c.label }}
      </Chip>
      <label
        class="flex items-center gap-[4px]"
        title="日志日期范围"
      >
        <input
          v-model="dateStart"
          type="date"
          class="h-[26px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[6px] text-[12px]"
          data-spec="log-date-start"
        >
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">至</span>
        <input
          v-model="dateEnd"
          type="date"
          class="h-[26px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[6px] text-[12px]"
          data-spec="log-date-end"
        >
      </label>
      <input
        v-model="keyword"
        placeholder="搜索消息关键字"
        class="h-[26px] w-[160px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[8px] text-[12px] outline-none focus:border-[var(--el-color-primary)]"
        data-spec="log-q"
      >
      <span class="flex-1" />
      <Btn
        size="sm"
        :disabled="busy"
        data-spec="log-pause"
        @click="paused = !paused"
      >
        <template #icon>
          <span :class="busy ? 'inline-flex animate-spin' : 'inline-flex'">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        {{ paused ? '恢复实时' : '暂停实时' }}
      </Btn>
    </div>

    <div
      ref="box"
      class="m-[12px] h-[460px] overflow-auto rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] px-[14px] py-[10px] font-mono text-[12px] leading-[1.85]"
      data-spec="log-area"
    >
      <div
        v-if="loading"
        class="text-[var(--el-text-color-secondary)]"
        data-spec="log-loading"
      >
        正在加载日志…
      </div>
      <div
        v-else-if="loadError"
        class="text-[var(--el-color-danger)]"
        data-spec="log-error"
      >
        日志加载失败，请点「恢复实时」重试
      </div>
      <div
        v-else-if="!rows.length"
        class="text-[var(--el-text-color-secondary)]"
        data-spec="log-empty"
      >
        {{ level === 'all'
          ? `该应用 ${dateStart}~${dateEnd} 没有日志记录${keyword.trim() ? '（含关键字过滤）' : ''}`
          : `该应用 ${dateStart}~${dateEnd} 没有 ${level.toUpperCase()} 级别日志${keyword.trim() ? '（含关键字过滤）' : ''}` }}
      </div>
      <div
        v-for="r in rows"
        :key="r.id"
        class="flex gap-[10px] whitespace-pre-wrap break-all"
        data-spec="log-row"
      >
        <span class="shrink-0 text-[var(--el-text-color-secondary)]">{{ r.time }}</span>
        <span
          class="w-[42px] shrink-0"
          :style="levelStyle(r.level)"
        >{{ r.level || '—' }}</span>
        <span class="text-[var(--el-text-color-primary)]">{{ r.message }}</span>
      </div>
    </div>
  </Card>
</template>
