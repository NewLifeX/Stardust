<script setup lang="ts">
/**
 * AlarmGroupDrawer：告警组「新增 / 编辑」抽屉表单（CARD-MON-01）。
 * 纯 UI + 本地表单状态：只收 props、只发 submit(form)；校验在 alarmGroup.logic（validateAlarmGroupForm），
 * 真正落库在容器（AlarmsGroups.save）。级联数据由容器拉好传入：项目下拉选项（GET /Platform/GalaxyProject，
 * 经容器注入的 fetchProjectOptions 闭包查询）。
 * 可写字段集（源码核实）：ProjectId、Name（必填）、Enable、WebHook、Content（WebApi 无通用 GetFields 端点，见 alarmGroup.logic 头注释）。
 * Name 必填报错 `role="alert"`；提交按钮遵守 AGENTS.md §11（busy 禁用 + 文案「保存中…」）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { emptyAlarmGroupForm, validateAlarmGroupForm } from './alarmGroup.logic.ts';
import type { AlarmGroupForm, ProjectOption } from './alarmGroup.logic.ts';

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';
const labelCls = 'flex flex-col gap-[4px]';
const spanCls = 'text-[12px] text-[var(--el-text-color-secondary)]';

const props = defineProps<{
  open: boolean;
  /** 编辑时的表单初值；null = 新增 */
  initial: AlarmGroupForm | null;
  /** 抽屉标题（新增告警组 / 编辑告警组 · {name}） */
  title: string;
  sub: string;
  /** 项目下拉选项（容器注入） */
  projectOptions: ProjectOption[];
  projectsLoading: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [form: AlarmGroupForm] }>();

const form = reactive<AlarmGroupForm>(emptyAlarmGroupForm());
const formError = ref('');

watch(
  () => [props.open, props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, emptyAlarmGroupForm(), init ?? {});
  },
  { immediate: true },
);

function onSubmit(): void {
  const err = validateAlarmGroupForm(form);
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
      class="flex flex-col gap-[13px]"
      data-spec="alarm-group-form"
    >
      <label :class="labelCls">
        <span :class="spanCls">名称 <span class="text-[var(--el-color-danger)]">*</span></span>
        <input
          v-model="form.name"
          type="text"
          data-spec="alarm-group-form-name"
          :class="inputCls"
          placeholder="告警组名称"
          @keydown.enter="onSubmit"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">项目（资源归属）</span>
        <select
          v-model.number="form.projectId"
          data-spec="alarm-group-form-project"
          :class="inputCls"
          :disabled="projectsLoading"
        >
          <option :value="0">
            {{ projectsLoading ? '项目加载中…' : '不绑定项目' }}
          </option>
          <option
            v-for="p in projectOptions"
            :key="p.id"
            :value="p.id"
          >
            {{ p.name }}
          </option>
        </select>
      </label>

      <label class="flex items-center gap-[8px]">
        <input
          v-model="form.enable"
          type="checkbox"
          data-spec="alarm-group-form-enable"
          class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
        >
        <span class="text-[12.5px] text-[var(--el-text-color-primary)]">启用</span>
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">停用后该组不投递告警，「测试」不会真实发送</span>
      </label>

      <label :class="labelCls">
        <span :class="spanCls">告警机器人 WebHook</span>
        <input
          v-model="form.webHook"
          type="text"
          data-spec="alarm-group-form-webhook"
          :class="inputCls"
          placeholder="钉钉、企业微信等机器人地址"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">内容模版</span>
        <textarea
          v-model="form.content"
          data-spec="alarm-group-form-content"
          class="min-h-[88px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[7px] text-[12.5px] leading-relaxed text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          placeholder="用于测试机器人的模版内容"
        />
      </label>

      <p
        v-if="formError"
        role="alert"
        data-spec="alarm-group-form-error"
        class="m-0 text-[12px] text-[var(--el-color-danger)]"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="alarm-group-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        data-spec="alarm-group-form-submit"
        :disabled="busy"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
