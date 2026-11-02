<script setup lang="ts">
/**
 * AppFormDrawer：应用新增/编辑（F12）。
 * 新建走 POST /Registry/App（返回 data.id）；编辑走 PUT /Registry/App/{id} 部分更新。
 * Secret：列表隐藏、表单可见；新建留空由服务端自动生成，编辑留空=不修改（不提交该字段）。
 * 名称（Name）编辑时只读——它是接入端注册与服务匹配的键。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { validateAppForm } from './apps.logic.ts';
import type { AppForm } from './apps.logic.ts';

const props = defineProps<{
  open: boolean;
  /** null=新建 */
  initial: AppForm | null;
  title: string;
  sub: string;
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [form: AppForm] }>();

const form = reactive<AppForm>({ name: '', displayName: '', category: '', enable: true, period: 60, secret: '', remark: '' });
const formError = ref('');

watch(
  [() => props.open, () => props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, { name: '', displayName: '', category: '', enable: true, period: 60, secret: '', remark: '' }, init ?? {});
  },
  { immediate: true },
);

function onSubmit(): void {
  const err = validateAppForm(form, !props.initial);
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
      data-spec="app-form"
    >
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">应用名（Name） *</span>
        <input
          v-model="form.name"
          type="text"
          :disabled="!!initial"
          placeholder="小写字母/数字/点，如 star.web"
          data-spec="app-form-name"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] font-mono text-[12.5px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)] disabled:opacity-55"
        >
        <span
          v-if="initial"
          class="text-[11px] text-[var(--el-text-color-placeholder)]"
        >
          应用名是接入注册的键，创建后不可修改
        </span>
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">显示名</span>
        <input
          v-model="form.displayName"
          type="text"
          data-spec="app-form-displayname"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">类别</span>
        <input
          v-model="form.category"
          type="text"
          data-spec="app-form-category"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
      </label>

      <div class="flex gap-[16px]">
        <label class="flex items-center gap-[8px]">
          <input
            v-model="form.enable"
            type="checkbox"
            data-spec="app-form-enable"
            class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
          >
          <span class="text-[12.5px] text-[var(--el-text-color-primary)]">启用</span>
        </label>
        <label class="flex items-center gap-[8px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">轮询间隔（秒）</span>
          <input
            v-model.number="form.period"
            type="number"
            min="0"
            data-spec="app-form-period"
            class="h-[30px] w-[110px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          >
        </label>
      </div>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">密钥（Secret）</span>
        <input
          v-model="form.secret"
          type="text"
          autocomplete="new-password"
          :placeholder="initial ? '留空=不修改' : '留空由服务端自动生成'"
          data-spec="app-form-secret"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] font-mono text-[12.5px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)] focus:border-[var(--el-color-primary)]"
        >
        <span class="text-[11px] text-[var(--el-text-color-placeholder)]">
          {{ initial ? '密钥不回显；留空保存=保持原值' : '接入端用它鉴权；列表里不显示' }}
        </span>
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">备注</span>
        <input
          v-model="form.remark"
          type="text"
          data-spec="app-form-remark"
          class="h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
      </label>

      <p
        v-if="formError"
        class="text-[12px] text-[var(--el-color-danger)]"
        data-spec="app-form-error"
        role="alert"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="app-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        :disabled="busy"
        data-spec="app-form-submit"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
