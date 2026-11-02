<script setup lang="ts">
/**
 * 底部控制台（日志 / 终端），纯 UI：只收 props、只发事件，不读 ui.store。
 * 对照原型 kit-shell.jsx 的 ConsoleDock 与 kit.css 的 .x-dock / .x-chead / .x-cbody / .x-termbar / .x-tin。
 *
 * 字体：原型的日志行与终端输出没有设 font-family（继承 body 的 14px/1.55 无衬线），
 * 只有提示符 $ 和输入框是 12px 等宽，所以日志行不加 font-mono。
 *
 * 偏离原型（均为有意）：
 *  - 终端缓冲区用文本插值渲染，不用 v-html。
 *  - 日志暂无真实数据源：logs 为空时显示「日志通道待接入」，日志页签不显示「清空」。
 *  - 没有在线节点时终端输入框禁用；原型的演示输出与「已连接」提示不带进来。
 *  - 关闭时加 inert，避免高度为 0 的 dock 里的输入框仍能被 Tab 聚焦。
 *  - 输入法组词期间 Enter 不提交。
 */
import { computed, nextTick, ref, watch } from 'vue';
import KIcon from '../KIcon.vue';
import { scopeLabel, scopedLogs, showClear } from '../dock.logic';
import type { DockLog, DockNode, NodeId, TermLine } from '../dock.logic';
import { PENDING_MESSAGE } from '../terminal.logic';
import type { DockTab } from '../ui.store';

const props = defineProps<{
  open: boolean;
  tab: DockTab;
  project: string;
  scopeApp: string | null;
  logs: readonly DockLog[];
  nodes: readonly DockNode[];
  termNode: NodeId | null;
  lines: readonly TermLine[];
}>();

const emit = defineEmits<{
  (e: 'set-tab', tab: DockTab): void;
  (e: 'close'): void;
  (e: 'clear'): void;
  (e: 'pick-node', id: NodeId): void;
  (e: 'submit', cmd: string): void;
}>();

const bodyRef = ref<HTMLElement | null>(null);
const draft = ref('');

const visibleLogs = computed(() => scopedLogs(props.logs, props.scopeApp));
const onlineNodes = computed(() => props.nodes.filter((n) => n.online));
const label = computed(() => scopeLabel(props.project, props.scopeApp));

// 级别色：Info 青、Warn 警告、Error 危险、Debug 次要文字（对照 kit.css 的 .L-*）
const LEVEL_COLOR: Record<DockLog['l'], string> = {
  Info: 'text-[var(--el-color-info)]',
  Warn: 'text-[var(--el-color-warning)]',
  Error: 'text-[var(--el-color-danger)]',
  Debug: 'text-[var(--el-text-color-secondary)]',
};

function onPickNode(e: Event) {
  const value = (e.target as HTMLSelectElement).value;
  const hit = props.nodes.find((n) => String(n.id) === value);
  if (hit) emit('pick-node', hit.id);
}

function onEnter(e: KeyboardEvent) {
  if (e.isComposing) return;
  const cmd = draft.value.trim();
  if (!cmd) return;
  emit('submit', cmd);
  draft.value = '';
}

// 对应原型 useEffect：打开 / 切页签 / 缓冲区变化 / 切节点后滚到底
watch(
  () => [props.open, props.tab, props.lines, props.termNode],
  async () => {
    await nextTick();
    if (bodyRef.value) bodyRef.value.scrollTop = bodyRef.value.scrollHeight;
  },
  { flush: 'post' },
);

const tabBase = 'h-[24px] cursor-pointer rounded-base px-[10px] text-[12px] font-medium transition-colors duration-[120ms] hover:text-[var(--el-text-color-primary)]';
const tabOn = 'bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-primary)]';
const tabOff = 'text-[var(--el-text-color-secondary)]';
</script>

<template>
  <div
    data-spec="console-dock"
    :inert="!open"
    :class="[
      'flex shrink-0 flex-col overflow-hidden border-t border-[var(--el-border-color)] bg-[var(--el-bg-color-page)] transition-[height] duration-200 ease-[cubic-bezier(0.16,1,0.3,1)] motion-reduce:transition-none',
      open ? 'h-[300px]' : 'h-0',
    ]"
  >
    <div class="flex shrink-0 items-center gap-1 border-b border-[var(--el-border-color-light)] px-3 py-[6px]">
      <button
        type="button"
        data-spec="dock-tab-log"
        :class="[tabBase, tab === 'log' ? tabOn : tabOff]"
        @click="emit('set-tab', 'log')"
      >
        日志
      </button>
      <button
        type="button"
        data-spec="dock-tab-term"
        :class="[tabBase, tab === 'term' ? tabOn : tabOff]"
        @click="emit('set-tab', 'term')"
      >
        终端
      </button>
      <span
        data-spec="dock-scope"
        class="ml-[10px] font-mono text-[10.5px] tracking-[0.03em] text-[var(--el-color-primary-light-3)]"
      >{{ label }}</span>
      <span class="flex-1" />
      <button
        v-if="showClear(tab)"
        type="button"
        data-spec="dock-clear"
        :class="[tabBase, tabOff]"
        @click="emit('clear')"
      >
        清空
      </button>
      <button
        type="button"
        data-spec="dock-close"
        title="关闭 (esc)"
        aria-label="关闭控制台"
        :class="[tabBase, tabOff, 'grid place-items-center']"
        @click="emit('close')"
      >
        <KIcon
          name="X"
          :size="13"
        />
      </button>
    </div>

    <div
      ref="bodyRef"
      data-spec="dock-body"
      class="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-4 py-2 text-[14px] leading-[1.55]"
    >
      <template v-if="tab === 'log'">
        <div
          v-if="visibleLogs.length === 0"
          data-spec="dock-log-empty"
          class="text-[var(--el-text-color-secondary)]"
        >
          日志通道待接入
        </div>
        <div
          v-for="(x, i) in visibleLogs"
          v-else
          :key="i"
          data-spec="dock-log-line"
          class="whitespace-pre-wrap"
        >
          <span class="mr-[10px] text-[var(--el-text-color-placeholder)]">{{ x.t }}</span>
          <b :class="['inline-block w-[42px]', LEVEL_COLOR[x.l]]">{{ x.l }}</b>
          <span
            v-if="!scopeApp"
            class="inline-block w-[78px] overflow-hidden text-ellipsis whitespace-nowrap align-bottom font-medium text-[var(--el-text-color-regular)]"
          >{{ x.app }}</span>
          <span>{{ x.m }}</span>
        </div>
      </template>

      <template v-else>
        <div class="mb-2 flex items-center gap-2 text-[11.5px] text-[var(--el-text-color-secondary)]">
          <span>连接到</span>
          <span
            v-if="onlineNodes.length === 0"
            data-spec="dock-no-node"
          >暂无在线节点</span>
          <span
            v-else
            class="relative inline-flex items-center"
          >
            <select
              data-spec="dock-node-select"
              aria-label="连接到节点"
              :value="termNode ?? ''"
              class="h-[24px] w-auto cursor-pointer appearance-none rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] pl-[11px] pr-[26px] text-[11.5px] text-[var(--el-text-color-primary)] transition-[border-color,box-shadow] duration-[120ms] focus:border-[var(--el-color-primary)] focus:shadow-[0_0_0_3px_color-mix(in_oklab,var(--el-color-primary)_18%,transparent)] focus:outline-none"
              @change="onPickNode"
            >
              <option
                v-for="n in onlineNodes"
                :key="String(n.id)"
                :value="String(n.id)"
              >
                {{ n.name }} · {{ n.ip }}
              </option>
            </select>
            <span class="pointer-events-none absolute right-[9px] flex text-[var(--el-text-color-secondary)]">
              <KIcon
                name="ChevronDown"
                :size="12"
              />
            </span>
          </span>
          <span>高危命令会被拦截（审计写入待后端接入）</span>
        </div>

        <div
          v-if="lines.length === 0"
          data-spec="dock-term-empty"
          class="text-[var(--el-text-color-secondary)]"
        >
          {{ PENDING_MESSAGE }}
        </div>
        <template v-else>
          <div
            v-for="(l, i) in lines"
            :key="i"
            data-spec="dock-term-line"
            :class="l.kind === 'cmd' ? 'text-[var(--el-color-primary-light-3)]' : 'whitespace-pre-wrap opacity-[.85]'"
          >
            {{ l.kind === 'cmd' ? '$ ' + l.text : l.text }}
          </div>
        </template>

        <div class="sticky bottom-0 flex items-center gap-2 bg-[var(--el-bg-color-page)] pb-[2px] pt-[6px]">
          <span class="font-mono text-[var(--el-color-primary-light-3)]">$</span>
          <input
            v-model="draft"
            data-spec="dock-term-input"
            aria-label="终端命令"
            placeholder="输入命令,回车执行"
            autocomplete="off"
            :disabled="termNode === null"
            class="flex-1 border-0 bg-transparent font-mono text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] disabled:cursor-not-allowed"
            @keydown.enter="onEnter"
          >
        </div>
      </template>
    </div>
  </div>
</template>
