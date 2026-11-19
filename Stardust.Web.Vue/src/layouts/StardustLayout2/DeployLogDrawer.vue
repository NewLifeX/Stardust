<script setup lang="ts">
/**
 * DeployLogDrawer：部署节点本地文件日志（DEPLOY-1 MVP + P2/P3 自动轮询 + task-90 日志目录配置引导）。
 * 容器传入 deployNodeId / 节点名，内部拉 LogFiles + LogTail。
 * 自动刷新开启时按选定间隔（1~5 秒）以 offset=上次 fileSize 增量追新；失败自动停止并给出可见提示。
 * task-90：容器还传生效日志目录 logDir（节点级优先、空=跟随部署集）；未配置时前置拦截不发请求，
 * 渲染配置引导空态并上抛 configure 事件，由容器打开节点编辑抽屉一步配置。
 */
import { computed, onUnmounted, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import {
  PAGE_UP_CHUNK,
  PAGE_UP_KEEP,
  applyTailSnapshot,
  canLoadEarlier,
  earlierOffset,
  fetchDeployLogFiles,
  fetchDeployLogTail,
  formatFileSize,
  prependTailPage,
} from './deployLog.logic.ts';
import type { DeployLogFileRow } from './deployLog.logic.ts';

const props = defineProps<{
  open: boolean;
  deployNodeId: number;
  nodeName: string;
  /** 生效日志目录（deploy.logic.resolveEffectiveLogDir 产出）；trim 后为空 = 节点与部署集均未配置 */
  logDir?: string;
}>();

const emit = defineEmits<{ close: []; configure: [] }>();

const files = ref<DeployLogFileRow[]>([]);
const selectedFile = ref('');
const content = ref('');
const truncated = ref(false);
const error = ref('');
const loadingFiles = ref(false);
const loadingTail = ref(false);

// —— 自动轮询（P2/P3 准实时：offset=上次 fileSize 增量追新）——
const autoRefresh = ref(false);
const intervalSec = ref(2);
const INTERVALS = [1, 2, 3, 4, 5];
let pollTimer: ReturnType<typeof setInterval> | undefined;
let polling = false;
let lastFileSize = 0;
/** 当前内容窗口的起始字节偏移（轮询替换与向上翻页时更新） */
let windowStart = 0;

/** 「加载更早」的禁用原因（随状态实时变化，显示在按钮 title 与提示行） */
const earlierHint = computed(() => {
  if (autoRefresh.value) return '自动刷新中不可回看历史，请先关闭自动刷新';
  if (content.value.length >= PAGE_UP_KEEP) return '已累计查看上限（2MB），可下载后查看全部';
  if (windowStart <= 1) return '已到可读窗口起点';
  return '加载当前窗口之前的更早日志（Agent 只支持从某偏移向后读，文件开头 0 字节窗口不可达）';
});

/** 未配置日志目录（trim 后空）= true：不发请求，只展示配置引导 */
const needLogDirConfig = computed(() => !(props.logDir ?? '').trim());

async function loadFiles(): Promise<void> {
  if (needLogDirConfig.value || !props.deployNodeId) return;
  loadingFiles.value = true;
  error.value = '';
  const rs = await fetchDeployLogFiles(request, props.deployNodeId);
  loadingFiles.value = false;
  if (!rs.ok) {
    error.value = rs.message;
    files.value = [];
    selectedFile.value = '';
    content.value = '';
    return;
  }
  files.value = rs.files;
  if (!selectedFile.value && rs.files.length) selectedFile.value = rs.files[0].name;
  if (selectedFile.value) await loadTail();
  else content.value = '';
}

async function loadTail(): Promise<void> {
  if (needLogDirConfig.value || !props.deployNodeId || !selectedFile.value) return;
  loadingTail.value = true;
  error.value = '';
  const rs = await fetchDeployLogTail(request, props.deployNodeId, selectedFile.value);
 loadingTail.value = false;
 if (!rs.ok || !rs.tail) {
    error.value = rs.message;
    content.value = '';
    truncated.value = false;
    return;
  }
  content.value = rs.tail.content;
  truncated.value = rs.tail.truncated;
  lastFileSize = rs.tail.fileSize;
  windowStart = rs.tail.offset;
}

/** 轮询一次：以上次 fileSize 为 offset 增量读；进行中跳过本拍，避免请求堆积。 */
async function pollOnce(): Promise<void> {
  if (!props.open || polling || !props.deployNodeId || !selectedFile.value) return;
  polling = true;
  const rs = await fetchDeployLogTail(request, props.deployNodeId, selectedFile.value, lastFileSize);
  polling = false;
  if (!autoRefresh.value) return;
  if (!rs.ok || !rs.tail) {
    autoRefresh.value = false; // watch 里停表
    error.value = `${rs.message}（自动刷新已停止）`;
    return;
  }
  const merged = applyTailSnapshot(content.value, lastFileSize, rs.tail);
  content.value = merged.content;
  truncated.value = rs.tail.truncated;
  lastFileSize = rs.tail.fileSize;
  // 追加语义窗口起点不变；替换语义（无新增回读尾部/回卷）窗口回到本次快照起点
  if (!merged.appended) windowStart = rs.tail.offset;
  error.value = '';
}

function startTimer(): void {
  stopTimer();
  const sec = Math.min(5, Math.max(1, intervalSec.value));
  pollTimer = setInterval(() => {
    void pollOnce();
  }, sec * 1000);
}

function stopTimer(): void {
  if (pollTimer) {
    clearInterval(pollTimer);
    pollTimer = undefined;
  }
}

function toggleAuto(): void {
  autoRefresh.value = !autoRefresh.value;
  if (autoRefresh.value) void pollOnce(); // 开启立即拉一次，给用户即时反馈
}

async function onRefresh(): Promise<void> {
  await loadFiles();
}

/** 向上翻页：读当前窗口之前的一页并与窗口裁齐拼接；自动刷新开启或请求进行中时不可用 */
async function loadEarlier(): Promise<void> {
  if (!props.deployNodeId || !selectedFile.value) return;
  if (autoRefresh.value || loadingTail.value || polling) return;
  if (!canLoadEarlier(windowStart, content.value.length)) return;
  loadingTail.value = true;
  error.value = '';
  const rs = await fetchDeployLogTail(request, props.deployNodeId, selectedFile.value, earlierOffset(windowStart), PAGE_UP_CHUNK);
  loadingTail.value = false;
  if (!rs.ok || !rs.tail) {
    error.value = rs.message;
    return;
  }
  const merged = prependTailPage(content.value, windowStart, rs.tail);
  content.value = merged.content;
  windowStart = merged.start;
  truncated.value = true; // 现在展示的只是文件中的一段
}

/** 下载当前查看窗口内容为 .log 文件（Agent 单次最多 512KB，这里导出的是抽屉内已加载内容） */
function downloadLog(): void {
  if (!content.value || !selectedFile.value) return;
  const blob = new Blob([content.value], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  const stamp = new Date().toISOString().replace(/[-:T]/g, '').slice(0, 14);
  a.href = url;
  a.download = `${selectedFile.value}.${stamp}.log`;
  a.click();
  URL.revokeObjectURL(url);
}

watch(autoRefresh, (on) => {
  if (on && props.open && selectedFile.value) startTimer();
  else stopTimer();
});

watch(intervalSec, () => {
  if (autoRefresh.value) startTimer();
});

watch(
  () => [props.open, props.deployNodeId] as const,
  ([open]) => {
    stopTimer();
    if (!open) return;
    selectedFile.value = '';
    files.value = [];
    content.value = '';
    truncated.value = false;
    error.value = '';
    // 未配置日志目录时 loadFiles 内部前置拦截（不发请求），视图呈现配置引导空态
    void loadFiles();
  },
);

// task-90：保存节点后生效日志目录由空变非空 → 清错误并自动拉取；反向变化 → 清数据回到配置引导
watch(
  () => props.logDir,
  () => {
    if (!props.open) return;
    error.value = '';
    if (needLogDirConfig.value) {
      selectedFile.value = '';
      files.value = [];
      content.value = '';
      truncated.value = false;
      return;
    }
    lastFileSize = 0;
    windowStart = 0;
    void loadFiles();
    if (autoRefresh.value) startTimer(); // 重开抽屉且开关仍开启：选中文件后下一拍生效
  },
);

watch(selectedFile, () => {
  if (props.open && selectedFile.value) void loadTail();
});

onUnmounted(stopTimer);
</script>

<template>
  <Drawer
    :open="open"
    title="节点文件日志"
    :sub="`${nodeName} · 部署节点 #${deployNodeId}`"
    @close="emit('close')"
  >
    <div
      class="flex flex-col gap-[10px]"
      data-spec="deploy-log-drawer"
    >
      <!-- task-90 配置引导空态：未配置日志目录时替代文件列表（不发任何请求） -->
      <div
        v-if="needLogDirConfig"
        class="flex flex-col items-start gap-[10px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] p-4"
        data-spec="deploy-log-config-empty"
      >
        <p class="text-[13px] font-medium text-[var(--el-text-color-primary)]">
          尚未配置日志目录
        </p>
        <p class="text-[12px] leading-relaxed text-[var(--el-text-color-secondary)]">
          查看节点文件日志需要读取 StarAgent 节点上配置的日志目录；与「应用日志」页签（SDK 上报）不同。日志目录在部署节点上维护，留空 = 跟随部署集的日志目录；当前该节点与所属部署集均未配置。
        </p>
        <Btn
          size="sm"
          variant="primary"
          data-spec="deploy-log-configure"
          @click="emit('configure')"
        >
          配置日志目录
        </Btn>
      </div>

      <template v-else>
        <p class="text-[11.5px] text-[var(--el-text-color-secondary)]">
          读取 StarAgent 节点上配置的日志目录；与「应用日志」页签（SDK 上报）不同。未配置日志目录请在部署节点编辑里填写。
        </p>
        <div class="flex flex-wrap items-center gap-[8px]">
          <label class="flex flex-1 min-w-[200px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">日志文件</span>
            <select
              v-model="selectedFile"
              class="h-[30px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px]"
              data-spec="deploy-log-file-select"
              :disabled="loadingFiles || loadingTail || !files.length"
            >
              <option
                v-if="!files.length"
                value=""
              >
                {{ loadingFiles ? '加载文件列表…' : '无匹配文件' }}
              </option>
              <option
                v-for="f in files"
                :key="f.name"
                :value="f.name"
              >
                {{ f.name }} ({{ formatFileSize(f.size) }})
              </option>
            </select>
          </label>
          <Btn
            size="sm"
            variant="default"
            data-spec="deploy-log-refresh"
            :disabled="loadingFiles || loadingTail"
            @click="onRefresh"
          >
            {{ loadingFiles || loadingTail ? '加载中…' : '刷新' }}
          </Btn>
          <Btn
            size="sm"
            variant="default"
            data-spec="deploy-log-earlier"
            :disabled="loadingFiles || loadingTail || autoRefresh || !canLoadEarlier(windowStart, content.length)"
            :title="earlierHint"
            @click="loadEarlier"
          >
            加载更早
          </Btn>
          <Btn
            size="sm"
            variant="default"
            data-spec="deploy-log-download"
            :disabled="!content"
            title="下载当前查看窗口内容为 .log 文件"
            @click="downloadLog"
          >
            下载
          </Btn>
          <Btn
            size="sm"
            :variant="autoRefresh ? 'primary' : 'default'"
            data-spec="deploy-log-auto-toggle"
            @click="toggleAuto"
          >
            {{ autoRefresh ? `自动刷新中 · 每 ${intervalSec}s` : '自动刷新' }}
          </Btn>
          <label
            class="flex items-center gap-[4px]"
            :title="autoRefresh ? '轮询间隔' : '开启自动刷新后可调'"
          >
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">间隔</span>
            <select
              v-model.number="intervalSec"
              class="h-[26px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[6px] text-[12px] disabled:opacity-40"
              data-spec="deploy-log-interval"
              :disabled="!autoRefresh"
            >
              <option
                v-for="s in INTERVALS"
                :key="s"
                :value="s"
              >
                {{ s }}s
              </option>
            </select>
          </label>
        </div>
        <p
          v-if="error"
          class="text-[12px] text-[var(--el-color-danger)]"
          data-spec="deploy-log-error"
        >
          {{ error }}
        </p>
        <p
          v-if="truncated"
          class="text-[11px] text-[var(--el-text-color-secondary)]"
          data-spec="deploy-log-truncated"
        >
          {{ earlierHint === '已到可读窗口起点' ? '已显示全部可读内容' : '仅显示文件片段；可用「加载更早」查看历史，或下载后查看全部。' }}
        </p>
        <pre
          class="max-h-[420px] overflow-auto rounded-[8px] border border-[var(--el-border-color-lighter)] bg-[var(--el-fill-color-darker)] p-3 font-mono text-[11.5px] leading-relaxed text-[var(--el-text-color-primary)] whitespace-pre-wrap break-all"
          data-spec="deploy-log-content"
        >{{ content || (loadingTail ? '读取中…' : '—') }}</pre>
      </template>
    </div>
  </Drawer>
</template>
