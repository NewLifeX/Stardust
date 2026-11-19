<script setup lang="ts">
/**
 * TraceItemDrawer：跟踪项「新增 / 编辑」抽屉表单（CARD-MON-12）。基准 PipelineConfigDrawer v2。
 * 纯 UI + 本地表单状态：只收 props、只发 submit(form)；校验文案来自 validateTraceItemEdit（traceItem.logic），
 * 真正落库在容器（TraceItemsPanel.onDrawerSubmit → saveTraceItem）。
 * 字段（卡 §3.2 定稿）：操作名 Name 必填 / 种类 Kind（GetKinds 下拉，后端 Valid：空或 redismq 时按 Name 推导）/
 * 显示名 DisplayName / 启用 Enable / 克隆 Cloned / 超时 Timeout(秒,0=不判超时) / 规则 Rules。
 * Rules placeholder 逐字引实体注释示例（跟踪项.cs:79；完整通配串见下方 template placeholder 属性——
 * 注释内不便直书「星/斜杠」序列，逐字断言由 TraceItemDrawer.ct.spec.ts 锁定）。
 * 保存语义（Kind 补齐、Rules 补 name= 前缀）后端承载——表单直传原值，不预改写（裁定 2）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { traceItemSaveBody, validateTraceItemEdit, type TraceItemEditModel, type TraceItemKind } from './traceItem.logic.ts';

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

const props = defineProps<{
  open: boolean;
  /** 编辑时的表单初值；null = 新增 */
  initial: TraceItemEditModel | null;
  title: string;
  sub: string;
  /** 类别词典（GetKinds，value=Kind 值 / label=展示名） */
  kinds: TraceItemKind[];
  kindsLoading: boolean;
  busy: boolean;
  /** 容器回填的保存错误（保存失败：…） */
  error: string;
}>();

const emit = defineEmits<{ close: []; submit: [form: TraceItemEditModel] }>();

const form = reactive<TraceItemEditModel>({
  appId: 0,
  name: '',
  kind: '',
  displayName: '',
  enable: true,
  cloned: false,
  timeout: 0,
  rules: '',
});

// formError 必须先于 immediate watch 声明（TDZ：watch 回调引用它）。
const formError = ref('');

watch(
  () => [props.open, props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, {
      appId: 0,
      name: '',
      kind: '',
      displayName: '',
      enable: true,
      cloned: false,
      timeout: 0,
      rules: '',
    }, init ?? {});
  },
  { immediate: true },
);

function onSubmit(): void {
  if (props.busy) return;
  // 先本地校验（文案来自 logic，真源 validateTraceItemEdit），通过才 emit 容器落库。
  const err = validateTraceItemEdit(form);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  // 直传不改写：Kind 空与 Rules 无= 交给后端 Valid 补齐（裁定 2），组件只装配。
  emit('submit', { ...form, ...traceItemSaveBody(form) } as TraceItemEditModel);
}
</script>

<template>
  <Drawer
    :open="open"
    :title="title"
    :sub="sub"
    @close="emit('close')"
  >
    <div class="flex flex-col gap-[13px]">
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">操作名（Name，必填）</span>
        <input
          v-model="form.name"
          data-spec="trace-item-form-name"
          type="text"
          placeholder="接口名或埋点名，同一应用内唯一"
          :class="inputCls"
        >
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">种类（Kind，留空按操作名自动推导）</span>
        <select
          v-model="form.kind"
          data-spec="trace-item-form-kind"
          :class="inputCls"
        >
          <option value="">
            自动推导
          </option>
          <option
            v-for="k in kinds"
            :key="k.value"
            :value="k.value"
          >
            {{ k.label }}
          </option>
        </select>
        <span
          v-if="kindsLoading"
          data-spec="trace-item-form-kinds-loading"
          class="text-[11px] text-[var(--el-text-color-secondary)]"
        >类别加载中…</span>
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">显示名（DisplayName）</span>
        <input
          v-model="form.displayName"
          data-spec="trace-item-form-display-name"
          type="text"
          placeholder="留空时可用「修正显示名」从节点规则推导"
          :class="inputCls"
        >
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">规则（Rules）</span>
        <textarea
          v-model="form.rules"
          data-spec="trace-item-form-rules"
          rows="3"
          placeholder="name=*/check*,*/ping*;clientId=10.10.*"
          :class="inputCls"
          class="h-auto resize-y py-[7px] leading-[1.5]"
        />
        <span class="text-[11px] text-[var(--el-text-color-secondary)]">支持*模糊匹配，多个规则逗号隔开，多组规则分号隔开；无前缀时默认按操作名 name= 匹配</span>
      </label>

      <div class="flex gap-[18px]">
        <label class="flex items-center gap-[6px] text-[12px] text-[var(--el-text-color-primary)]">
          <input
            v-model="form.enable"
            data-spec="trace-item-form-enable"
            type="checkbox"
          >
          启用
        </label>
        <label class="flex items-center gap-[6px] text-[12px] text-[var(--el-text-color-primary)]">
          <input
            v-model="form.cloned"
            data-spec="trace-item-form-cloned"
            type="checkbox"
          >
          克隆
        </label>
      </div>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">超时（秒，0=不判超时）</span>
        <input
          v-model.number="form.timeout"
          data-spec="trace-item-form-timeout"
          type="number"
          min="0"
          :class="inputCls"
        >
      </label>

      <p
        v-if="error || formError"
        role="alert"
        data-spec="trace-item-form-error"
        class="m-0 text-[12px] text-[var(--el-color-danger)]"
      >
        {{ error || formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="trace-item-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        data-spec="trace-item-form-submit"
        :disabled="busy"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
