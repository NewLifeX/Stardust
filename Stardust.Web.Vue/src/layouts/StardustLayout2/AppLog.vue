<script setup lang="ts">
/**
 * AppLog：应用工作台「日志」页签（2026-10-29；F17 2026-11-03 改服务端过滤）。
 * 自包含：只收 appId，内部处理 loading / error / empty 三态，取数经 deps 注入（默认接真实接口）。
 * 数据口径：GET /Registry/AppClientLog?appId=&kind=&dtStart=&dtEnd=（BE-1 ⑤：appId 分表感知必填、
 * kind 精确匹配 Info/Warn/Error/Fatal、默认当天 00:00 起、SearchSafe 缺表兜底）。
 * 级别 chip 切换=重发服务端请求（kind 精确匹配，Kind 为空/其它值只在「全部」可见——如实展示）。
 * 实时：未暂停时按 intervalMs 轮询；用户在底部附近才跟随滚动，避免打断向上翻看。
 */
import { nextTick, onScopeDispose, ref, watch } from 'vue';
import realRequest from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Chip from './Chip.vue';
import KIcon from './KIcon.vue';
import { fetchAppLogs, LOG_LEVEL_CHIPS, todayRange } from './appLog.logic.ts';
import type { LogLevel, LogRange, LogRow } from './appLog.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

export interface LogDeps {
  fetchLogs: (request: Req, appId: number, range: LogRange, kind: 'all' | LogLevel, pageSize: number) => Promise<LogRow[] | null>;
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

const box = ref<HTMLElement | null>(null);
const range: LogRange = todayRange();
let timer: ReturnType<typeof setTimeout> | undefined;
let loadToken = 0;

async function load(silent = false): Promise<void> {
  const token = ++loadToken;
  if (!silent) loading.value = true;
  busy.value = true;
  const fn = props.deps?.fetchLogs ?? fetchAppLogs;
  const res = await fn(realRequest, props.appId, range, level.value, 200);
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
  const interval = props.deps?.intervalMs ?? DEFAULT_INTERVAL;
  if (interval <= 0 || paused.value) return;
  timer = setTimeout(() => {
    void load(true);
    schedulePoll();
  }, interval);
}

// F17：级别 chip=服务端 kind 过滤 → 切换时重发请求（不再本地过滤）
watch(level, () => {
  void load();
});

watch(() => props.appId, () => {
  void load().then(() => schedulePoll());
}, { immediate: true });

watch(paused, (p) => {
  if (!p) void load(true);
  schedulePoll();
});

onScopeDispose(() => {
  loadToken++;
  if (timer) clearTimeout(timer);
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
        {{ level === 'all' ? '该应用今天没有日志记录' : `该应用今天没有 ${level.toUpperCase()} 级别日志` }}
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
