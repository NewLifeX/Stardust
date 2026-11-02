<script setup lang="ts">
/**
 * DeployEditDrawer：部署集「编辑」抽屉（F06）。纯 UI + 本地表单状态。
 * RepoPassword 永不回显：占位提示「留空不修改」；仅当用户输入新值才随 PUT 提交（deployPutBody 已按此裁剪），
 * 避免把掩码/密文当新密码造成二次加密（后端 Valid 对 "*****" 做还原，但前端空值语义更稳）。
 * 校验在 deploy.logic.validateDeployEdit；真正保存走容器（useDeployData.saveDeployEdit）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { validateDeployEdit } from './deploy.logic.ts';
import type { DeployEditModel } from './deploy.logic.ts';

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

const props = defineProps<{
  open: boolean;
  /** 编辑初值（fetchDeployDetail 的结果） */
  initial: DeployEditModel | null;
  /** 保存请求进行中 */
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [m: DeployEditModel] }>();

const form = reactive<DeployEditModel>({
  name: '', category: '', enable: false, packageName: '', autoPublish: false, multiVersion: false,
  repository: '', repoUserName: '', repoPassword: '', branch: '', projectPath: '', buildArgs: '',
  packageFilters: '', remark: '',
});
const formError = ref('');

watch(
  () => [props.open, props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, {
      name: '', category: '', enable: false, packageName: '', autoPublish: false, multiVersion: false,
      repository: '', repoUserName: '', repoPassword: '', branch: '', projectPath: '', buildArgs: '',
      packageFilters: '', remark: '',
    }, init ?? {});
  },
  { immediate: true },
);

function onSubmit(): void {
  const err = validateDeployEdit(form);
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
    title="编辑部署集"
    :sub="initial?.name ? `名称 ${initial.name}` : ''"
    @close="emit('close')"
  >
    <div
      class="flex flex-col gap-[12px]"
      data-spec="deploy-form"
    >
      <div class="grid grid-cols-2 gap-[10px]">
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">名称 *</span>
          <input
            v-model="form.name"
            :class="inputCls"
            data-spec="deploy-form-name"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">类别</span>
          <input
            v-model="form.category"
            :class="inputCls"
            placeholder="如 web / service"
            data-spec="deploy-form-category"
          >
        </label>
      </div>
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">应用包名（与上传文件名匹配）</span>
        <input
          v-model="form.packageName"
          :class="inputCls"
          placeholder="如 star.zip"
          data-spec="deploy-form-packagename"
        >
      </label>
      <div class="flex flex-wrap gap-[16px] text-[12.5px] text-[var(--el-text-color-primary)]">
        <label class="flex items-center gap-[6px]">
          <input
            v-model="form.enable"
            type="checkbox"
            data-spec="deploy-form-enable"
          >
          启用部署集
        </label>
        <label class="flex items-center gap-[6px]">
          <input
            v-model="form.autoPublish"
            type="checkbox"
            data-spec="deploy-form-autopublish"
          >
          自动发布（有新版本即下发节点）
        </label>
        <label class="flex items-center gap-[6px]">
          <input
            v-model="form.multiVersion"
            type="checkbox"
            data-spec="deploy-form-multiversion"
          >
          多版本
        </label>
      </div>

      <p class="m-0 border-t border-[var(--el-border-color-light)] pt-[10px] text-[12px] font-medium text-[var(--el-text-color-secondary)]">
        仓库（编译节点拉取源代码用；密码留空=不修改）
      </p>
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">仓库地址</span>
        <input
          v-model="form.repository"
          :class="inputCls"
          placeholder="https://git.example.com/x/y.git"
          data-spec="deploy-form-repository"
        >
      </label>
      <div class="grid grid-cols-2 gap-[10px]">
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">分支</span>
          <input
            v-model="form.branch"
            :class="inputCls"
            data-spec="deploy-form-branch"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">仓库用户名</span>
          <input
            v-model="form.repoUserName"
            :class="inputCls"
            data-spec="deploy-form-repousername"
          >
        </label>
      </div>
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">仓库密码（留空不修改；已加密存储）</span>
        <input
          v-model="form.repoPassword"
          type="password"
          autocomplete="new-password"
          :class="inputCls"
          placeholder="留空=不修改"
          data-spec="deploy-form-repopassword"
        >
      </label>
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">项目路径（仓库内相对路径）</span>
        <input
          v-model="form.projectPath"
          :class="inputCls"
          data-spec="deploy-form-projectpath"
        >
      </label>
      <div class="grid grid-cols-2 gap-[10px]">
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">编译参数</span>
          <input
            v-model="form.buildArgs"
            :class="inputCls"
            data-spec="deploy-form-buildargs"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">打包过滤（分号分割）</span>
          <input
            v-model="form.packageFilters"
            :class="inputCls"
            placeholder="*.dll;*.exe;*.json"
            data-spec="deploy-form-packagefilters"
          >
        </label>
      </div>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">备注</span>
        <input
          v-model="form.remark"
          :class="inputCls"
          data-spec="deploy-form-remark"
        >
      </label>

      <p
        v-if="formError"
        data-spec="deploy-form-error"
        class="m-0 text-[12px] text-[var(--el-color-danger)]"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="deploy-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        data-spec="deploy-form-submit"
        :disabled="busy"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存修改' }}
      </Btn>
    </template>
  </Drawer>
</template>
