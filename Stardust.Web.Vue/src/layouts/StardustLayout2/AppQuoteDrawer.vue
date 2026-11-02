<script setup lang="ts">
/**
 * AppQuoteDrawer：/configs「新增/编辑应用依赖」抽屉（F15）。
 * 表单：依赖方/被依赖应用（下拉，来自应用列表）、依赖类型（数字）、启用。
 * 编辑态两个应用 id 都可改（AppQuote 无草稿语义，PUT 全字段）。
 * 校验在视线内（AGENTS.md §11）：错误提示 data-spec app-quote-error。
 */
import { ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { validateAppQuoteForm } from './configCenter.logic.ts';
import type { AppQuoteForm } from './configCenter.logic.ts';

export interface AppOption {
  id: number;
  label: string;
}

const props = defineProps<{
  open: boolean;
  /** 应用下拉选项 */
  apps: AppOption[];
  initial: AppQuoteForm | null;
  title: string;
  sub: string;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [form: AppQuoteForm];
}>();

const EMPTY: AppQuoteForm = { appId: 0, targetAppId: 0, kind: 0, enable: true };

const form = ref<AppQuoteForm>({ ...EMPTY });
const error = ref('');

watch([() => props.open, () => props.initial] as const, ([open, init]) => {
  if (!open) return;
  error.value = '';
  form.value = init ? { ...init } : { ...EMPTY };
}, { immediate: true });

function onSubmit(): void {
  const msg = validateAppQuoteForm(form.value);
  if (msg) {
    error.value = msg;
    return;
  }
  emit('submit', { ...form.value });
}
</script>

<template>
  <Drawer
    :open="open"
    :title="title"
    :sub="sub"
    @close="emit('close')"
  >
    <div class="space-y-[14px]">
      <div>
        <label class="mb-[4px] block text-[12px] text-[var(--el-text-color-secondary)]">依赖方应用（谁引用别人）</label>
        <select
          v-model.number="form.appId"
          data-spec="app-quote-app"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
          <option
            :value="0"
            disabled
          >
            请选择依赖方应用
          </option>
          <option
            v-for="a in apps"
            :key="a.id"
            :value="a.id"
          >
            {{ a.label }}
          </option>
        </select>
      </div>

      <div>
        <label class="mb-[4px] block text-[12px] text-[var(--el-text-color-secondary)]">被依赖应用（被引用方）</label>
        <select
          v-model.number="form.targetAppId"
          data-spec="app-quote-target"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
          <option
            :value="0"
            disabled
          >
            请选择被依赖的应用
          </option>
          <option
            v-for="a in apps"
            :key="a.id"
            :value="a.id"
          >
            {{ a.label }}
          </option>
        </select>
      </div>

      <div>
        <label class="mb-[4px] block text-[12px] text-[var(--el-text-color-secondary)]">依赖类型（数字，实体无枚举）</label>
        <input
          v-model.number="form.kind"
          type="number"
          min="0"
          data-spec="app-quote-kind"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
      </div>

      <label class="flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
        <input
          v-model="form.enable"
          type="checkbox"
          data-spec="app-quote-enable"
          class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
        >
        启用
      </label>

      <p
        v-if="error"
        class="m-0 text-[12px] text-[var(--el-color-danger)]"
        data-spec="app-quote-error"
        role="alert"
      >
        {{ error }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="app-quote-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        :disabled="busy"
        :aria-busy="busy"
        data-spec="app-quote-submit"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
