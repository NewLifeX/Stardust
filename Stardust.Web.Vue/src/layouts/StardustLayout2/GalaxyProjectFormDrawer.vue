<script setup lang="ts">
/**
 * GalaxyProjectFormDrawer：星系项目 新建/编辑表单抽屉（CARD-PLT-01，§3.2 形态 2）。
 * 纯 UI：open/model/nameError/busy 由父组件持有；submit 只回传表单模型，不自己发请求。
 * 字段与 hint 依据定稿数据契约；「项目名称（唯一）」placeholder 不照抄（后端无唯一校验，裁定 2）。
 * 节点数/应用数为后端统计字段，不进表单。
 */
import { computed } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import type { ProjectFormModel } from './galaxyProject.logic.ts';

const props = withDefaults(
  defineProps<{
    open: boolean;
    /** 编辑对象名（抽屉标题）；null 表示新建 */
    editName?: string | null;
    model: ProjectFormModel;
    /** 名称校验错误；空串表示无错 */
    nameError?: string;
    busy?: boolean;
  }>(),
  { editName: null, nameError: '', busy: false },
);

const emit = defineEmits<{ close: []; submit: []; 'update:model': [ProjectFormModel] }>();

const title = computed(() => (props.editName != null && props.editName !== '' ? '编辑项目' : '新建项目'));
const sub = computed(() => (props.editName != null && props.editName !== '' ? `项目 ${props.editName}` : '新建后可从顶栏选择器切换到该项目'));

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';
const numberCls = `${inputCls} w-[110px]`;
const labelCls = 'mb-[5px] block text-[12px] font-medium text-[var(--el-text-color-regular)]';
const hintCls = 'mt-[3px] block text-[11px] leading-[1.5] text-[var(--el-text-color-secondary)]';

function set<K extends keyof ProjectFormModel>(key: K, value: ProjectFormModel[K]): void {
  emit('update:model', { ...props.model, [key]: value });
}

function numInput(key: 'tenantId' | 'managerId' | 'sort', e: Event): void {
  const n = Number((e.target as HTMLInputElement).value);
  set(key, Number.isFinite(n) && n > 0 ? Math.floor(n) : 0);
}
</script>

<template>
  <Drawer
    :open="open"
    :title="title"
    :sub="sub"
    @close="emit('close')"
  >
    <div class="flex flex-col gap-[14px]">
      <label class="block">
        <div :class="labelCls">
          名称 <span class="text-[var(--el-color-danger)]">*</span>
        </div>
        <input
          type="text"
          data-spec="platform-project-form-name"
          :value="model.name"
          placeholder="项目名称"
          :disabled="busy"
          class="w-full rounded-[8px] border bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          :class="nameError ? 'h-[32px] border-[var(--el-color-danger)]' : 'h-[30px] border-[var(--el-border-color-light)]'"
          @input="set('name', ($event.target as HTMLInputElement).value)"
        >
        <span
          v-if="nameError"
          role="alert"
          data-spec="platform-project-form-name-error"
          class="mt-[3px] block text-[11.5px] text-[var(--el-color-danger)]"
        >{{ nameError }}</span>
      </label>

      <div class="flex items-center gap-[22px]">
        <label class="inline-flex cursor-pointer items-center gap-[6px]">
          <input
            type="checkbox"
            data-spec="platform-project-form-enable"
            class="h-[13px] w-[13px] accent-[var(--el-color-primary)]"
            :checked="model.enable"
            :disabled="busy"
            @change="set('enable', ($event.target as HTMLInputElement).checked)"
          >
          <span :class="labelCls">启用</span>
        </label>
        <label class="inline-flex cursor-pointer items-center gap-[6px]">
          <input
            type="checkbox"
            data-spec="platform-project-form-global"
            class="h-[13px] w-[13px] accent-[var(--el-color-primary)]"
            :checked="model.isGlobal"
            :disabled="busy"
            @change="set('isGlobal', ($event.target as HTMLInputElement).checked)"
          >
          <span :class="labelCls">全局</span>
        </label>
      </div>
      <div :class="hintCls">
        该项目的节点可以允许其它项目下应用选用
      </div>

      <div class="flex flex-wrap gap-x-[18px] gap-y-[10px]">
        <label class="block">
          <div :class="labelCls">
            租户 ID
          </div>
          <input
            type="number"
            min="0"
            data-spec="platform-project-form-tenant"
            :value="model.tenantId"
            :disabled="busy"
            :class="numberCls"
            @input="numInput('tenantId', $event)"
          >
          <span :class="hintCls">0 = 无</span>
        </label>
        <label class="block">
          <div :class="labelCls">
            管理者 ID
          </div>
          <input
            type="number"
            min="0"
            data-spec="platform-project-form-manager"
            :value="model.managerId"
            :disabled="busy"
            :class="numberCls"
            @input="numInput('managerId', $event)"
          >
          <span :class="hintCls">0 = 无</span>
        </label>
        <label class="block">
          <div :class="labelCls">
            排序
          </div>
          <input
            type="number"
            min="0"
            data-spec="platform-project-form-sort"
            :value="model.sort"
            :disabled="busy"
            :class="numberCls"
            @input="numInput('sort', $event)"
          >
        </label>
      </div>

      <label class="block">
        <div :class="labelCls">
          IP 白名单
        </div>
        <input
          type="text"
          data-spec="platform-project-form-whiteips"
          :value="model.whiteIPs"
          :disabled="busy"
          :class="inputCls"
          @input="set('whiteIPs', ($event.target as HTMLInputElement).value)"
        >
        <span :class="hintCls">符合条件的来源IP才允许访问，支持*通配符，多个逗号隔开</span>
      </label>

      <label class="block">
        <div :class="labelCls">
          IP 黑名单
        </div>
        <input
          type="text"
          data-spec="platform-project-form-blackips"
          :value="model.blackIPs"
          :disabled="busy"
          :class="inputCls"
          @input="set('blackIPs', ($event.target as HTMLInputElement).value)"
        >
        <span :class="hintCls">符合条件的来源IP禁止访问，支持*通配符，多个逗号隔开</span>
      </label>

      <label class="block">
        <div :class="labelCls">
          新服务器
        </div>
        <input
          type="text"
          data-spec="platform-project-form-newserver"
          :value="model.newServer"
          :disabled="busy"
          :class="inputCls"
          @input="set('newServer', ($event.target as HTMLInputElement).value)"
        >
        <span :class="hintCls">该项目下的节点自动迁移到新的服务器地址</span>
      </label>

      <label class="block">
        <div :class="labelCls">
          备注
        </div>
        <textarea
          data-spec="platform-project-form-remark"
          :value="model.remark"
          rows="2"
          :disabled="busy"
          class="w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          @input="set('remark', ($event.target as HTMLTextAreaElement).value)"
        />
      </label>
    </div>

    <div class="mt-[16px] flex justify-end gap-[8px]">
      <Btn
        size="sm"
        data-spec="platform-project-form-cancel"
        :disabled="busy"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        size="sm"
        variant="primary"
        data-spec="platform-project-form-save"
        :disabled="busy"
        :aria-busy="busy"
        @click="emit('submit')"
      >
        <template #icon>
          <span :class="busy ? 'inline-flex animate-spin' : 'inline-flex'">
            <span class="h-[12px] w-[12px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-white)]" />
          </span>
        </template>
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </div>
  </Drawer>
</template>
