<script setup lang="ts">
/**
 * PipelineFormDrawer：新建或编辑流水线。纯 UI。
 * 不回显 Token。部署节点是 AppDeployNode.Id，编译节点是物理节点 Node.Id。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { SELECT } from './deployView.styles.ts';
import { emptyPipelineForm, validatePipelineForm } from './pipelineForm.logic.ts';
import type { CompileChoice, DeployChoice, PipelineForm } from './pipelineForm.logic.ts';

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

const props = defineProps<{
  open: boolean;
  /** null 表示新建 */
  pipelineId: number | null;
  initial: PipelineForm | null;
  busy: boolean;
  loading: boolean;
  compileChoices: CompileChoice[];
  deployChoices: DeployChoice[];
}>();

const emit = defineEmits<{ close: []; submit: [form: PipelineForm] }>();

const form = reactive<PipelineForm>(emptyPipelineForm());
const formError = ref('');

watch(
  () => [props.open, props.initial, props.pipelineId] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, emptyPipelineForm(), init ?? {});
    form.deployNodeIds = [...(init?.deployNodeIds ?? [])];
  },
  { immediate: true },
);

function toggleNode(id: number): void {
  form.deployNodeIds = form.deployNodeIds.includes(id)
    ? form.deployNodeIds.filter((x) => x !== id)
    : [...form.deployNodeIds, id];
}

function onSubmit(): void {
  const err = validatePipelineForm(form);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  emit('submit', { ...form, deployNodeIds: [...form.deployNodeIds] });
}
</script>

<template>
  <Drawer
    :open="open"
    :title="pipelineId ? '编辑流水线' : '新建流水线'"
    :sub="pipelineId ? (initial?.name || `编号 ${pipelineId}`) : '关联本部署集的编译节点和部署节点'"
    @close="emit('close')"
  >
    <div
      class="flex flex-col gap-[12px]"
      data-spec="pipeline-form"
    >
      <p
        v-if="loading"
        class="m-0 text-[12.5px] text-[var(--el-text-color-secondary)]"
        data-spec="pipeline-form-loading"
      >
        正在读取流水线…
      </p>
      <template v-else>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">名称 *</span>
          <input
            v-model="form.name"
            :class="inputCls"
            data-spec="pipeline-form-name"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">分支</span>
          <input
            v-model="form.branch"
            :class="inputCls"
            placeholder="main，支持 release/*"
            data-spec="pipeline-form-branch"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">编译节点 *</span>
          <select
            v-model.number="form.buildNodeId"
            :class="SELECT"
            data-spec="pipeline-form-build"
          >
            <option :value="0">
              请选择
            </option>
            <option
              v-for="c in compileChoices"
              :key="c.nodeId"
              :value="c.nodeId"
            >
              {{ c.label }}
            </option>
          </select>
          <span
            v-if="!compileChoices.length"
            class="text-[11.5px] text-[var(--el-color-warning)]"
          >
            本部署集还没有启用的编译节点。先到「节点」页签添加，再回来选择。
          </span>
        </label>
        <fieldset class="m-0 min-w-0 border-0 p-0">
          <legend class="mb-[6px] text-[12px] text-[var(--el-text-color-secondary)]">
            部署节点 *（编译成功且开了自动部署时，安装到这些节点）
          </legend>
          <div
            v-if="!deployChoices.length"
            class="text-[11.5px] text-[var(--el-color-warning)]"
          >
            本部署集还没有部署节点。先到「节点」页签添加。
          </div>
          <label
            v-for="n in deployChoices"
            :key="n.id"
            class="mb-[4px] flex min-w-0 items-start gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              class="mt-[3px]"
              type="checkbox"
              :checked="form.deployNodeIds.includes(n.id)"
              data-spec="pipeline-form-node"
              @change="toggleNode(n.id)"
            >
            <span class="min-w-0 break-all">{{ n.label }}</span>
          </label>
        </fieldset>
        <label class="flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
          <input
            v-model="form.autoDeploy"
            type="checkbox"
            data-spec="pipeline-form-autodeploy"
          >
          编译成功后自动部署
        </label>
        <label class="flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
          <input
            v-model="form.enable"
            type="checkbox"
            data-spec="pipeline-form-enable"
          >
          启用（停用后 Webhook 和手动触发都不会跑）
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">备注</span>
          <input
            v-model="form.remark"
            :class="inputCls"
            data-spec="pipeline-form-remark"
          >
        </label>
        <p
          v-if="formError"
          class="m-0 text-[12.5px] text-[var(--el-color-danger)]"
          data-spec="pipeline-form-error"
        >
          {{ formError }}
        </p>
        <div class="flex justify-end gap-2">
          <Btn
            size="sm"
            :disabled="busy"
            @click="emit('close')"
          >
            取消
          </Btn>
          <Btn
            size="sm"
            variant="primary"
            :disabled="busy"
            :aria-busy="busy"
            data-spec="pipeline-form-submit"
            @click="onSubmit"
          >
            {{ busy ? '保存中…' : (pipelineId ? '保存' : '创建') }}
          </Btn>
        </div>
      </template>
    </div>
  </Drawer>
</template>
