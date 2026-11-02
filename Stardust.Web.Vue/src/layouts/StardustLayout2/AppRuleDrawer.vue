<script setup lang="ts">
/**
 * AppRuleDrawer：应用规则新增/编辑（F11）。AppRule 是全局规则表（无 AppId 列），
 * 规则表达式分号分段（IP=10.*,172.*;LocalIP=192.*），命中结果如 Scope=dev。
 * 校验 inline；保存中禁用（§11）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { validateAppRuleForm } from './configCenter.logic.ts';
import type { AppRuleForm } from './configCenter.logic.ts';

const props = defineProps<{
  open: boolean;
  initial: AppRuleForm | null;
  title: string;
  sub: string;
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [form: AppRuleForm] }>();

const form = reactive<AppRuleForm>({ rule: '', result: '', priority: 0, enable: true, remark: '' });
const formError = ref('');

watch(
  [() => props.open, () => props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, { rule: '', result: '', priority: 0, enable: true, remark: '' }, init ?? {});
  },
  { immediate: true },
);

function onSubmit(): void {
  const err = validateAppRuleForm(form);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  emit('submit', { ...form });
}
</script>

<template>
  <Drawer
    :open="open"
    :title="title"
    :sub="sub"
    @close="emit('close')"
  >
    <div
      class="flex flex-col gap-[12px]"
      data-spec="app-rule-form"
    >
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">规则表达式 *</span>
        <textarea
          v-model="form.rule"
          rows="2"
          placeholder="IP=10.*,172.*;LocalIP=192.*"
          data-spec="app-rule-rule"
          class="w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[6px] font-mono text-[12.5px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
        />
        <span class="text-[11px] text-[var(--el-text-color-placeholder)]">分号分段，逗号间隔多项匹配值</span>
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">命中结果 *</span>
        <input
          v-model="form.result"
          type="text"
          placeholder="Scope=dev"
          data-spec="app-rule-result"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] font-mono text-[12.5px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
        >
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">优先级（数字越大越高）</span>
        <input
          v-model.number="form.priority"
          type="number"
          min="0"
          data-spec="app-rule-priority"
          class="h-[30px] w-[140px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
      </label>

      <label class="flex items-center gap-[8px]">
        <input
          v-model="form.enable"
          type="checkbox"
          data-spec="app-rule-enable"
          class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
        >
        <span class="text-[12.5px] text-[var(--el-text-color-primary)]">启用</span>
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">备注</span>
        <input
          v-model="form.remark"
          type="text"
          data-spec="app-rule-remark"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
      </label>

      <p
        v-if="formError"
        class="text-[12px] text-[var(--el-color-danger)]"
        data-spec="app-rule-error"
        role="alert"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="app-rule-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        :disabled="busy"
        data-spec="app-rule-submit"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
