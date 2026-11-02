<script setup lang="ts">
/**
 * 命令面板（纯 UI）。对照原型 kit-shell.jsx 的 Palette。
 * 状态与按键逻辑在 usePaletteState.ts；本组件只负责渲染、聚焦和挂 window 监听。
 * 由 index.vue 用 v-if 挂载：每次打开都是一份新状态（原型也是在打开时清空查询）。
 * 偏离原型：输入法组词期间（isComposing）不处理按键，避免拼音选词时按 Enter 误执行。
 */
import { onBeforeUnmount, onMounted, ref } from 'vue';
import type { PaletteConfig } from '../ui.store';
import { usePaletteState } from './usePaletteState';

const props = defineProps<{ config: PaletteConfig }>();
const emit = defineEmits<{ (e: 'close'): void }>();

const { query, sel, placeholder, view, flat, setQuery, hover, activate, onKey } = usePaletteState({
  config: () => props.config,
  close: () => emit('close'),
});

const inputRef = ref<HTMLInputElement | null>(null);
let focusTimer: ReturnType<typeof setTimeout> | undefined;

function onWindowKeydown(e: KeyboardEvent): void {
  if (e.isComposing) return;
  // 返回非 null 表示按键已被识别，需要阻止默认行为（原型四个按键都会）
  if (onKey(e.key)) e.preventDefault();
}

onMounted(() => {
  window.addEventListener('keydown', onWindowKeydown);
  // 原型：setTimeout(focus, 40)，等入场动画起步后再聚焦
  focusTimer = setTimeout(() => inputRef.value?.focus(), 40);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onWindowKeydown);
  if (focusTimer) clearTimeout(focusTimer);
});
</script>

<template>
  <div
    data-spec="command-palette"
    class="fixed inset-0 z-[60] flex animate-x-fade items-start justify-center bg-[color-mix(in_oklab,var(--el-color-black)_48%,transparent)] px-4 pb-4 pt-[12vh] backdrop-blur-[8px] motion-reduce:animate-none [.dark_&]:bg-[color-mix(in_oklab,var(--el-color-black)_72%,transparent)]"
    @click="emit('close')"
  >
    <div
      role="dialog"
      aria-modal="true"
      aria-label="命令面板"
      class="max-h-[78vh] w-[min(620px,96vw)] animate-x-pop overflow-hidden rounded-[16px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] p-[6px] shadow-[var(--el-box-shadow)] motion-reduce:animate-none"
      @click.stop
    >
      <input
        ref="inputRef"
        data-spec="palette-input"
        type="text"
        autocomplete="off"
        spellcheck="false"
        :value="query"
        :placeholder="placeholder"
        class="box-border block w-full border-b border-[var(--el-border-color-light)] bg-transparent px-3 py-[13px] text-[15px] font-medium text-[var(--el-text-color-primary)] outline-none placeholder:font-normal placeholder:text-[var(--el-text-color-placeholder)]"
        @input="setQuery(($event.target as HTMLInputElement).value)"
      >

      <div
        role="listbox"
        class="mt-[6px] max-h-[46vh] overflow-y-auto overflow-x-hidden"
      >
        <div
          v-if="flat.length === 0"
          class="p-5 text-[13px] text-[var(--el-text-color-secondary)]"
        >
          没有结果
        </div>

        <div
          v-for="group in view"
          :key="group.title"
        >
          <div class="px-3 pb-1 pt-2 font-mono text-[10px] uppercase tracking-[0.08em] text-[var(--el-text-color-placeholder)]">
            {{ group.title }}
          </div>
          <div
            v-for="row in group.rows"
            :key="row.index"
            role="option"
            :aria-selected="row.index === sel"
            class="flex cursor-pointer items-center gap-[10px] rounded-[8px] px-3 py-2 text-[13px]"
            :class="row.index === sel
              ? 'bg-[color-mix(in_oklab,var(--el-color-primary)_18%,transparent)] text-[var(--el-text-color-primary)]'
              : 'text-[var(--el-text-color-regular)]'"
            @mouseenter="hover(row.index)"
            @click="activate(row.index)"
          >
            <span>{{ row.item.text }}</span>
            <small
              v-if="row.item.hint"
              class="ml-auto font-mono text-[10.5px]"
              :class="row.index === sel ? 'text-[var(--el-text-color-secondary)]' : 'text-[var(--el-text-color-placeholder)]'"
            >
              {{ row.item.hint }}
            </small>
          </div>
        </div>
      </div>

      <div class="flex gap-[14px] overflow-hidden whitespace-nowrap border-t border-[var(--el-border-color-light)] px-3 py-2 font-mono text-[10px] text-[var(--el-text-color-placeholder)]">
        <span>↑↓ 选择</span>
        <span>↵ 执行</span>
        <span>esc 关闭</span>
        <span class="ml-auto">⌘K</span>
      </div>
    </div>
  </div>
</template>
