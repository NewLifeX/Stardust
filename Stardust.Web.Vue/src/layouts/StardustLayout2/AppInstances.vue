<script setup lang="ts">
/**
 * AppInstances：应用工作台「实例」页签（原型 README §4.3 第 197-200 行）。
 * 纯 UI + 自带三态取数：props 只收 appId；deps.request 是测试缝（默认接 cube-vue request）。
 * 数据源：GET /Registry/AppOnline?appId=（实例列表）+ GET /Registry/AppOnline/Snapshot?appId=
 * （F04 快照：按 clientId 取 AppMeter 最新一条 + 节点可用内存；无性能数据的行仍标「待接入」）。
 *
 * F03 批量命令：行首勾选 → 工具条（命令/参数输入 + 执行命令 + 释放内存 + 取消选择）。
 * 「执行命令」GET /Registry/AppOnline/Execute?command=&argument=&keys=（AppOnline.Id 逗号串），
 * 「释放内存」/Registry/AppOnline/FreeMemory?keys=；后端返回 JsonRefresh 汇总
 * （「操作成功！下发N个，响应N个，超时N个」+ 各实例返回），结果面板完整展示（pre-line）。
 * 危险命令（restart/shutdown/kill 等清单）GuardModal 二次确认；§11 loading/禁用/toast。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  cpuText,
  executeCommand,
  fetchInstances,
  fetchSnapshots,
  freeMemory,
  isDangerousCommand,
  mergeSnapshots,
} from './appInstances.logic.ts';
import type { InstanceDetail, InstanceRow } from './appInstances.logic.ts';
import { formatBytes } from './appObserve.logic.ts';
import { toast } from './useToast.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';

const props = defineProps<{
  appId: number;
  /** 测试缝：注入 request；生产不传 */
  deps?: { request?: Req };
}>();

const emit = defineEmits<{
  /** 行点击：查看实例详情（节点抽屉由上层接） */
  select: [row: InstanceRow];
  /** 重启：只发事件，实际调用不在本页签 */
  restart: [row: InstanceRow];
  /** 终端：只发事件，实际调用不在本页签 */
  terminal: [row: InstanceRow];
  /** 打开应用历史（client）或性能（clientId） */
  drill: [payload: { tab: 'appHistory' | 'meter'; client: string }];
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const loading = ref(true);
const loadFailed = ref(false);
const rows = ref<InstanceDetail[]>([]);

// ---------- F03 勾选与批量命令 ----------
// 注意：状态必须声明在 watch({ immediate: true }) 之前——immediate 回调里的
// clearSelection() 会读 selectedIds，声明在后会触发 TDZ（Cannot access before initialization）。

const selectedIds = ref<Set<number>>(new Set());
const command = ref('');
const argument = ref('');
const commandError = ref('');
/** 正在执行的批量动作：'exec' | 'freemem' | null */
const batchBusy = ref<string | null>(null);
/** 最近一次批量命令的汇总（JsonRefresh 多行文案，完整展示） */
const resultText = ref('');
const resultOk = ref(false);

const selectedCount = computed(() => selectedIds.value.size);
const onlineCount = computed(() => rows.value.filter((r) => r.online).length);

function toggleSelect(id: number): void {
  const next = new Set(selectedIds.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  selectedIds.value = next;
}

function toggleAll(e: Event): void {
  const checked = (e.target as HTMLInputElement).checked;
  selectedIds.value = checked ? new Set(rows.value.map((r) => r.id)) : new Set();
}

const allSelected = computed(() => rows.value.length > 0 && selectedIds.value.size === rows.value.length);

function clearSelection(): void {
  selectedIds.value = new Set();
  commandError.value = '';
}

// 本地 Guard（McpTab 同款模式）：CT gallery 不挂全局 GuardModal 宿主，
// 组件内自渲染，外壳与 gallery 都可见。
const guardLabel = ref<string | null>(null);
let guardAction: (() => void) | null = null;

function askGuard(label: string, fn: () => void): void {
  guardLabel.value = label;
  guardAction = fn;
}

function onGuardCancel(): void {
  guardLabel.value = null;
  guardAction = null;
}

function onGuardConfirm(): void {
  const fn = guardAction;
  guardLabel.value = null;
  guardAction = null;
  fn?.();
}

async function load(): Promise<void> {
  loadFailed.value = false;
  const list = await fetchInstances(req(), props.appId);
  if (list === null) {
    loadFailed.value = true;
    return;
  }
  // 快照失败不阻塞列表（CPU/内存回退「待接入」）
  const snaps = await fetchSnapshots(req(), props.appId);
  rows.value = mergeSnapshots(list, snaps);
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    loading.value = true;
    try {
      await load();
    } finally {
      loading.value = false;
    }
  },
  outcome: () => ({
    loadError: loadFailed.value ? '实例加载失败' : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

watch(
  () => props.appId,
  async () => {
    loading.value = true;
    clearSelection();
    try {
      await load();
    } finally {
      loading.value = false;
    }
  },
  { immediate: true },
);

/** 执行命令（含危险命令 Guard）；成功后完整展示汇总文案 */
function askExecute(): void {
  if (batchBusy.value) return;
  commandError.value = '';
  if (!selectedCount.value) {
    commandError.value = '请先勾选要操作的实例';
    return;
  }
  if (!command.value.trim()) {
    commandError.value = '请输入要下发的命令';
    return;
  }
  const keys = [...selectedIds.value];
  const cmd = command.value.trim();
  const arg = argument.value.trim();
  const run = async () => {
    batchBusy.value = 'exec';
    try {
      const r = await executeCommand(req(), keys, cmd, arg);
      resultText.value = r.message;
      resultOk.value = r.ok;
      toast(r.ok ? '命令已执行' : '命令执行失败', r.ok ? 'ok' : 'err');
    } finally {
      batchBusy.value = null;
    }
  };
  if (isDangerousCommand(cmd)) {
    askGuard(`确认向 ${keys.length} 个实例执行危险命令「${cmd}」${arg ? `（参数：${arg}）` : ''}？命令会立即下发到实例所在进程。`, run);
    return;
  }
  void run();
}

/** 释放内存：固定下发 app/freeMemory，同样完整展示汇总 */
function askFreeMemory(): void {
  if (batchBusy.value) return;
  commandError.value = '';
  if (!selectedCount.value) {
    commandError.value = '请先勾选要操作的实例';
    return;
  }
  const keys = [...selectedIds.value];
  askGuard(`确认向 ${keys.length} 个实例下发「释放内存」命令？实例进程会尝试回收内存并返回结果。`, async () => {
    batchBusy.value = 'freemem';
    try {
      const r = await freeMemory(req(), keys);
      resultText.value = r.message;
      resultOk.value = r.ok;
      toast(r.ok ? '释放内存命令已执行' : '释放内存执行失败', r.ok ? 'ok' : 'err');
    } finally {
      batchBusy.value = null;
    }
  });
}
</script>

<template>
  <Card data-spec="app-instances">
    <div class="flex flex-wrap items-center gap-x-3 gap-y-2">
      <h3 :class="H3">
        实例
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">
          {{ loading || loadFailed ? '—' : `${rows.length} 个实例（${onlineCount} 在线）` }}
        </span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        :disabled="refreshing"
        :aria-busy="refreshing"
        data-spec="app-instances-refresh"
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
      class="py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="app-instances-loading"
    >
      实例加载中…
    </div>
    <div
      v-else-if="loadFailed"
      class="py-10 text-center text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="app-instances-failed"
    >
      实例加载失败，请点「刷新」重试
    </div>

    <template v-else>
      <!-- F03 批量命令工具条（勾选即显示） -->
      <div
        v-if="selectedCount > 0"
        data-spec="app-instances-batchbar"
        class="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-[10px] border border-[color-mix(in_oklab,var(--el-color-primary)_35%,transparent)] bg-[var(--el-bg-color-overlay)] px-[14px] py-[10px]"
      >
        <span class="text-[12.5px] text-[var(--el-text-color-primary)]">
          已选 {{ selectedCount }} 个实例
        </span>
        <input
          v-model="command"
          type="text"
          placeholder="命令，如 app/restart"
          data-spec="app-instances-command"
          class="h-[28px] w-[180px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] font-mono text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
          @keyup.enter="askExecute"
        >
        <input
          v-model="argument"
          type="text"
          placeholder="参数（可选）"
          data-spec="app-instances-argument"
          class="h-[28px] w-[150px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] font-mono text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
          @keyup.enter="askExecute"
        >
        <Btn
          size="sm"
          variant="primary"
          :disabled="!!batchBusy"
          :aria-busy="batchBusy === 'exec'"
          data-spec="app-instances-execute"
          title="向勾选实例下发命令（AppOnline.Execute）"
          @click="askExecute"
        >
          {{ batchBusy === 'exec' ? '执行中…' : '执行命令' }}
        </Btn>
        <Btn
          size="sm"
          variant="default"
          :disabled="!!batchBusy"
          :aria-busy="batchBusy === 'freemem'"
          data-spec="app-instances-freemem"
          title="向勾选实例下发 app/freeMemory（AppOnline.FreeMemory）"
          @click="askFreeMemory"
        >
          {{ batchBusy === 'freemem' ? '执行中…' : '释放内存' }}
        </Btn>
        <Btn
          size="sm"
          variant="ghost"
          :disabled="!!batchBusy"
          data-spec="app-instances-batch-clear"
          @click="clearSelection"
        >
          取消选择
        </Btn>
        <p
          v-if="commandError"
          class="w-full text-[12px] text-[var(--el-color-danger)]"
          data-spec="app-instances-command-error"
          role="alert"
        >
          {{ commandError }}
        </p>
      </div>

      <!-- F03 批量命令汇总（JsonRefresh 多行文案完整展示） -->
      <div
        v-if="resultText"
        data-spec="app-instances-result"
        :class="[
          'mt-2 rounded-[8px] border px-[14px] py-[8px] font-mono text-[11.5px] leading-[1.7] whitespace-pre-line',
          resultOk
            ? 'border-[color-mix(in_oklab,var(--el-color-success)_40%,transparent)] text-[var(--el-text-color-regular)]'
            : 'border-[color-mix(in_oklab,var(--el-color-danger)_40%,transparent)] text-[var(--el-color-danger)]',
        ]"
      >
        {{ resultText }}
      </div>

      <table class="mt-3 w-full border-collapse">
        <thead>
          <tr>
            <th :class="[TH, 'w-[36px]']">
              <input
                type="checkbox"
                :checked="allSelected"
                aria-label="选择全部实例"
                data-spec="app-instances-check-all"
                class="h-[13px] w-[13px] accent-[var(--el-color-primary)]"
                @click.stop
                @change="toggleAll"
              >
            </th>
            <th :class="TH">
              节点
            </th>
            <th :class="[TH, 'w-[92px]']">
              CPU
            </th>
            <th :class="[TH, 'w-[110px]']">
              内存
            </th>
            <th :class="[TH, 'w-[80px]']">
              状态
            </th>
            <th :class="TH">
              版本
            </th>
            <th :class="[TH, 'w-[220px]']">
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="r in rows"
            :key="r.id"
            data-spec="app-instances-row"
            :data-instance-id="r.id"
            class="group cursor-pointer hover:bg-[var(--el-bg-color-overlay)]"
            @click="emit('select', r)"
          >
            <td
              :class="TD"
              @click.stop
            >
              <input
                type="checkbox"
                :checked="selectedIds.has(r.id)"
                :aria-label="`选择实例 ${r.node || r.client}`"
                data-spec="app-instances-check"
                class="h-[13px] w-[13px] accent-[var(--el-color-primary)]"
                @change="toggleSelect(r.id)"
              >
            </td>
            <td :class="TD">
              <div class="flex items-center gap-2">
                <span
                  class="inline-block h-[7px] w-[7px] rounded-full"
                  :class="r.online ? 'bg-[var(--el-color-success)]' : 'bg-[var(--el-border-color)]'"
                  data-spec="app-instances-dot"
                />
                <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.node || '—' }}</span>
                <span class="font-mono text-[12px] text-[var(--el-text-color-secondary)]">{{ r.ip }}</span>
              </div>
            </td>
            <!-- F04：快照 CPU；无性能数据仍「待接入」 -->
            <td :class="[TD, 'font-mono text-[12px] tabular-nums text-[var(--el-text-color-primary)]']">
              <Tag
                v-if="r.cpu === null"
                tone="default"
                data-spec="app-instances-cpu"
              >
                待接入
              </Tag>
              <span
                v-else
                data-spec="app-instances-cpu"
              >{{ cpuText(r.cpu) }}</span>
            </td>
            <!-- F04：快照内存（工作集）+ 可用内存/线程/连接进 title -->
            <td :class="[TD, 'font-mono text-[12px] tabular-nums text-[var(--el-text-color-primary)]']">
              <Tag
                v-if="r.memory === null"
                tone="default"
                data-spec="app-instances-memory"
              >
                待接入
              </Tag>
              <span
                v-else
                data-spec="app-instances-memory"
                :title="[
                  r.availableMemory !== null ? `节点可用内存 ${formatBytes(r.availableMemory)}` : '',
                  r.threads !== null ? `线程 ${r.threads}` : '',
                  r.connections !== null ? `连接 ${r.connections}` : '',
                  r.updateTimeText ? `采样于 ${r.updateTimeText}` : '',
                ].filter(Boolean).join(' · ')"
              >{{ formatBytes(r.memory) }}</span>
            </td>
            <td :class="TD">
              <Tag :tone="r.online ? 'ok' : 'default'">
                {{ r.online ? '在线' : '离线' }}
              </Tag>
            </td>
            <td :class="[TD, 'font-mono text-[12px] text-[var(--el-text-color-primary)]']">
              {{ r.version || '—' }}
            </td>
            <td :class="TD">
              <div
                class="flex items-center gap-1"
                @click.stop
              >
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="app-instances-history"
                  @click="emit('drill', { tab: 'appHistory', client: r.client })"
                >
                  历史
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="app-instances-meter"
                  @click="emit('drill', { tab: 'meter', client: r.client })"
                >
                  性能
                </Btn>
                <span class="inline-flex items-center gap-1 opacity-0 transition-opacity duration-[120ms] group-hover:opacity-100">
                  <Btn
                    size="sm"
                    variant="ghost"
                    data-spec="app-instances-restart"
                    @click="emit('restart', r)"
                  >
                    重启
                  </Btn>
                  <Btn
                    size="sm"
                    variant="ghost"
                    data-spec="app-instances-terminal"
                    @click="emit('terminal', r)"
                  >
                    终端
                  </Btn>
                </span>
              </div>
            </td>
          </tr>
          <tr v-if="!rows.length">
            <td
              colspan="7"
              class="p-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
              data-spec="app-instances-empty"
            >
              暂无在线实例
            </td>
          </tr>
        </tbody>
      </table>
    </template>

    <!-- F03 批量命令二次确认（危险命令 / 释放内存） -->
    <GuardModal
      v-if="guardLabel"
      :label="guardLabel"
      @cancel="onGuardCancel"
      @confirm="onGuardConfirm"
    />
  </Card>
</template>
