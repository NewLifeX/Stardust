<script setup lang="ts">
/**
 * ConfigItemDrawer：配置项就地编辑/新增（F10）。
 * 编辑态按发布态收窄（源码 ConfigDataController.OnUpdate 语义）：
 *  - 已发布行（published）：Key/Scope/Value 只读，改动写入 NewValue 草稿；Enable 变化才提交（转 NewStatus）；
 *  - 未发布行：Key/Scope/Value 可直接改；
 *  - 新增行（initial=null）：全字段可填，POST 建 Key/Scope/Value。
 * 校验在视线内（inline error），保存中禁用按钮（AGENTS.md §11）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { validateConfigItemForm } from './appConfig.logic.ts';
import type { ConfigItemForm } from './appConfig.logic.ts';

const props = defineProps<{
  open: boolean;
  /** null=新增；否则编辑（published 决定字段读写） */
  initial: (ConfigItemForm & { published: boolean }) | null;
  title: string;
  sub: string;
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [form: ConfigItemForm] }>();

const form = reactive<ConfigItemForm>({ key: '', scope: '', value: '', newValue: '', enable: true, remark: '' });
const formError = ref('');

watch(
  [() => props.open, () => props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, { key: '', scope: '', value: '', newValue: '', enable: true, remark: '' }, init ?? {});
  },
  { immediate: true },
);

const published = (): boolean => props.initial?.published ?? false;
const isCreate = (): boolean => !props.initial;

function onSubmit(): void {
  const err = validateConfigItemForm(form, published(), props.initial ? { key: props.initial.key, scope: props.initial.scope } : undefined);
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
      data-spec="config-item-form"
    >
      <p
        v-if="initial?.published"
        class="rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-warning)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-warning)_8%,transparent)] px-[10px] py-[7px] text-[11.5px] text-[var(--el-text-color-regular)]"
        data-spec="config-item-draft-hint"
      >
        该配置项已发布：名称/作用域/生效值不可改，修改会写入「期望值」草稿，发布后生效。
      </p>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">名称（Key） *</span>
        <input
          v-model="form.key"
          type="text"
          :disabled="!isCreate() && published()"
          data-spec="config-item-key"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)] disabled:opacity-55"
        >
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">作用域（Scope，空=默认）</span>
        <input
          v-model="form.scope"
          type="text"
          :disabled="!isCreate() && published()"
          data-spec="config-item-scope"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)] disabled:opacity-55"
        >
      </label>

      <template v-if="!isCreate() && published()">
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">生效值（Value，只读）</span>
          <input
            :value="form.value"
            type="text"
            disabled
            data-spec="config-item-value"
            class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] font-mono text-[12.5px] text-[var(--el-text-color-secondary)] opacity-70 outline-none"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">期望值（草稿，发布后生效）</span>
          <textarea
            v-model="form.newValue"
            rows="3"
            data-spec="config-item-newvalue"
            class="w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[6px] font-mono text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          />
        </label>
      </template>
      <label
        v-else
        class="flex flex-col gap-[4px]"
      >
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">值（Value）</span>
        <textarea
          v-model="form.value"
          rows="3"
          data-spec="config-item-value"
          class="w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[6px] font-mono text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        />
      </label>

      <label class="flex items-center gap-[8px]">
        <input
          v-model="form.enable"
          type="checkbox"
          data-spec="config-item-enable"
          class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
        >
        <span class="text-[12.5px] text-[var(--el-text-color-primary)]">启用</span>
        <span
          v-if="!isCreate() && published()"
          class="text-[11px] text-[var(--el-text-color-secondary)]"
        >
          （变更会写入启用/禁用草稿，发布后生效）
        </span>
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">备注</span>
        <input
          v-model="form.remark"
          type="text"
          data-spec="config-item-remark"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
      </label>

      <p
        v-if="formError"
        class="text-[12px] text-[var(--el-color-danger)]"
        data-spec="config-item-error"
        role="alert"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="config-item-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        :disabled="busy"
        data-spec="config-item-submit"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
