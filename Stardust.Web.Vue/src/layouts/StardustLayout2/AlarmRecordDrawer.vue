<script setup lang="ts">
/**
 * AlarmRecordDrawer：告警记录「编辑」抽屉表单（CARD-MON-03）。
 * 纯 UI + 本地表单状态：只收 props、只发 submit(form)；校验在 alarmRecord.logic
 * （validateAlarmRecordForm，Name 必填），真正落库在容器（Alarms.save，PUT /Monitors/AlarmRecord/{id}）。
 * 本卡只做编辑（定稿 1：记录由系统产生，无新增入口）；告警组下拉选项由容器拉好传入
 * （GET /Monitors/AlarmGroup），''=不分组（组 Id 字符串透传，走查缺陷②全链 string）。
 * 可写字段集（源码核实，AlarmRecordController.cs Add/EditFormFields）：Name/GroupId/Category/Action/
 *   Status/StartTime/EndTime/Duration/Times/Content（AckTime/AckUser/SilenceTime 不可写）。
 * Content 列宽 2000（报警记录.Biz.cs:46-47 服务端超长截断）→ 前端 maxlength=2000 兜底。
 * Name 必填报错 `role="alert"`；提交按钮遵守 AGENTS.md §11（busy 禁用 + 文案「保存中…」）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { validateAlarmRecordForm } from './alarmRecord.logic.ts';
import type { AlarmRecordForm } from './alarmRecord.logic.ts';
import type { AlarmGroupOption } from './alarms.logic.ts';

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';
const labelCls = 'flex flex-col gap-[4px]';
const spanCls = 'text-[12px] text-[var(--el-text-color-secondary)]';

function blankForm(): AlarmRecordForm {
  return {
    id: '',
    name: '',
    groupId: '',
    category: '',
    action: '',
    status: 1,
    startTime: '',
    endTime: '',
    durationSec: 0,
    times: 0,
    content: '',
  };
}

const props = defineProps<{
  open: boolean;
  /** 编辑时的表单初值（alarmRecordFormFromRow 产出；本卡只编辑，null 仅兜底） */
  initial: AlarmRecordForm | null;
  /** 抽屉标题（编辑告警记录 · {name}） */
  title: string;
  sub: string;
  /** 告警组下拉选项（容器注入） */
  groups: AlarmGroupOption[];
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [form: AlarmRecordForm] }>();

const form = reactive<AlarmRecordForm>(blankForm());
const formError = ref('');

watch(
  () => [props.open, props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, blankForm(), init ?? {});
  },
  { immediate: true },
);

function onSubmit(): void {
  const err = validateAlarmRecordForm(form);
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
      data-spec="alarm-record-form"
    >
      <label :class="labelCls">
        <span :class="spanCls">名称 <span class="text-[var(--el-color-danger)]">*</span></span>
        <input
          v-model="form.name"
          type="text"
          data-spec="alarm-record-form-name"
          :class="inputCls"
          placeholder="告警名称"
          @keydown.enter="onSubmit"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">告警组</span>
        <select
          v-model="form.groupId"
          data-spec="alarm-record-form-group"
          :class="inputCls"
        >
          <option value="">
            不分组
          </option>
          <option
            v-for="g in groups"
            :key="g.id"
            :value="g.id"
          >
            {{ g.name }}
          </option>
        </select>
      </label>

      <label :class="labelCls">
        <span :class="spanCls">类别</span>
        <input
          v-model="form.category"
          type="text"
          data-spec="alarm-record-form-category"
          :class="inputCls"
          placeholder="应用下线 / 节点下线 / 错误数过高等"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">对象</span>
        <input
          v-model="form.action"
          type="text"
          data-spec="alarm-record-form-action"
          :class="inputCls"
          placeholder="报警对象：应用名或节点名"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">状态</span>
        <select
          v-model.number="form.status"
          data-spec="alarm-record-form-status"
          :class="inputCls"
        >
          <option :value="1">
            报警中
          </option>
          <option :value="2">
            已恢复
          </option>
        </select>
      </label>

      <label :class="labelCls">
        <span :class="spanCls">开始时间</span>
        <input
          v-model="form.startTime"
          type="datetime-local"
          data-spec="alarm-record-form-start"
          :class="inputCls"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">结束时间</span>
        <input
          v-model="form.endTime"
          type="datetime-local"
          data-spec="alarm-record-form-end"
          :class="inputCls"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">持续时间（秒）</span>
        <input
          v-model.number="form.durationSec"
          type="number"
          data-spec="alarm-record-form-duration"
          :class="inputCls"
          min="0"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">通知次数</span>
        <input
          v-model.number="form.times"
          type="number"
          data-spec="alarm-record-form-times"
          :class="inputCls"
          min="0"
        >
      </label>

      <label :class="labelCls">
        <span :class="spanCls">内容</span>
        <textarea
          v-model="form.content"
          data-spec="alarm-record-form-content"
          class="min-h-[88px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[7px] text-[12.5px] leading-relaxed text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
          maxlength="2000"
          placeholder="告警详情（列宽 2000，超长将被截断）"
        />
      </label>

      <p
        v-if="formError"
        role="alert"
        data-spec="alarm-record-form-error"
        class="m-0 text-[12px] text-[var(--el-color-danger)]"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="alarm-record-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        data-spec="alarm-record-form-submit"
        :disabled="busy"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
