<script setup lang="ts">
/**
 * PipelineConfigDrawer：流水线「新增 / 编辑」抽屉表单（CARD-DEP-01）。
 * 纯 UI + 本地表单状态：只收 props、只发 submit(form)；校验在 pipelineConfig.logic（validatePipelineForm），
 * 真正落库在容器（useDeployData.savePipeline）。级联数据由容器拉好传入：
 * 部署节点选项（GetDeployNodes）与编译节点 suggest（NodeSearch，按容器传入的 searchNodes 闭包查询）。
 * 敏感字段 Token/Secret/TraceId 不进表单（实现规范 §6.5）；ProjectId 经 GetDeployProject 只读展示。
 * 提交按钮遵守 AGENTS.md §11（busy 禁用 + 文案「保存中…」）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import KIcon from './KIcon.vue';
import {
  emptyPipelineForm,
  validatePipelineForm,
} from './pipelineConfig.logic.ts';
import type { BuildNodeSearchItem, PipelineForm, PipelineNodeOption } from './pipelineConfig.logic.ts';

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

const props = defineProps<{
  open: boolean;
  /** 编辑时的表单初值；null = 新增 */
  initial: PipelineForm | null;
  /** 抽屉标题（新增流水线 / 编辑流水线 · {name}） */
  title: string;
  sub: string;
  /** 部署集绑定的项目 Id（GetDeployProject，只读展示） */
  projectId: number;
  /** 部署节点多选选项（GetDeployNodes，仅启用节点，文案「节点名 (IP)」） */
  nodeOptions: PipelineNodeOption[];
  nodesLoading: boolean;
  /** 编译节点 suggest 查询闭包（容器注入 request）；返回 null 表示查询失败 */
  searchNodes: (key: string) => Promise<BuildNodeSearchItem[] | null>;
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [form: PipelineForm] }>();

const form = reactive<PipelineForm>(emptyPipelineForm());
const formError = ref('');
const searchKey = ref('');
const suggestions = ref<BuildNodeSearchItem[]>([]);
const searching = ref(false);
const searchFailed = ref(false);

watch(
  () => [props.open, props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, emptyPipelineForm(), init ?? {});
    // 回显编译节点：初值只有 Id；未选中 suggest 前显示 #Id
    searchKey.value = init?.buildNodeId ? `#${init.buildNodeId}` : '';
    suggestions.value = [];
    searchFailed.value = false;
  },
  { immediate: true },
);

async function onSearchInput(): Promise<void> {
  const key = searchKey.value.trim();
  if (!key) {
    suggestions.value = [];
    searchFailed.value = false;
    return;
  }
  searching.value = true;
  const list = await props.searchNodes(key);
  searching.value = false;
  if (list === null) {
    searchFailed.value = true;
    suggestions.value = [];
    return;
  }
  searchFailed.value = false;
  suggestions.value = list;
}

function pickNode(item: BuildNodeSearchItem): void {
  form.buildNodeId = item.id;
  searchKey.value = item.name;
  suggestions.value = [];
}

function toggleNode(id: number, e: Event): void {
  const on = (e.target as HTMLInputElement).checked;
  if (on && !form.deployNodeIds.includes(id)) form.deployNodeIds = [...form.deployNodeIds, id];
  if (!on) form.deployNodeIds = form.deployNodeIds.filter((x) => x !== id);
}

function onSubmit(): void {
  const err = validatePipelineForm(form);
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
    <div class="flex flex-col gap-[13px]">
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">名称 <i class="not-italic text-[var(--el-color-danger)]">*</i></span>
        <input
          v-model="form.name"
          :class="inputCls"
          placeholder="流水线名称（同一平台内唯一）"
          data-spec="pipeline-form-name"
        >
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">分支</span>
        <input
          v-model="form.branch"
          :class="inputCls"
          placeholder="main"
          data-spec="pipeline-form-branch"
        >
        <span class="text-[11px] text-[var(--el-text-color-secondary)]">精确分支或前缀通配，如 release/*</span>
      </label>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">编译节点</span>
        <input
          v-model="searchKey"
          :class="inputCls"
          placeholder="搜索节点"
          data-spec="pipeline-form-buildnode"
          @input="onSearchInput"
        >
        <span
          v-if="searching"
          class="text-[11px] text-[var(--el-text-color-secondary)]"
          data-spec="pipeline-form-searching"
        >
          搜索中…
        </span>
        <span
          v-else-if="searchFailed"
          class="text-[11px] text-[var(--el-color-danger)]"
          data-spec="pipeline-form-searchfailed"
        >
          节点搜索失败，请稍后重试
        </span>
        <span
          v-else-if="form.buildNodeId > 0 && !suggestions.length"
          class="text-[11px] text-[var(--el-text-color-secondary)]"
        >
          当前编译节点 #{{ form.buildNodeId }}（输入关键词可更换）
        </span>
        <ul
          v-if="suggestions.length"
          class="m-0 max-h-[132px] list-none overflow-auto rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] p-0"
          data-spec="pipeline-form-suggestions"
        >
          <li
            v-for="s in suggestions"
            :key="s.id"
            class="flex cursor-pointer items-center gap-2 px-[10px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)] hover:bg-[var(--el-fill-color-light)]"
            @click="pickNode(s)"
          >
            <KIcon
              name="Server"
              :size="13"
              class="text-[var(--el-text-color-secondary)]"
            />
            <b class="font-medium">{{ s.name }}</b>
            <span class="text-[11px] text-[var(--el-text-color-secondary)]">{{ s.ip || `#${s.id}` }}</span>
          </li>
        </ul>
      </label>

      <div class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">部署节点（仅启用节点）</span>
        <span
          v-if="nodesLoading"
          class="text-[11.5px] text-[var(--el-text-color-secondary)]"
          data-spec="pipeline-form-nodesloading"
        >
          节点列表加载中…
        </span>
        <span
          v-else-if="!nodeOptions.length"
          class="text-[11.5px] text-[var(--el-text-color-secondary)]"
          data-spec="pipeline-form-nodesempty"
        >
          部署集还没有启用节点，可稍后在「部署节点」区块添加
        </span>
        <div
          v-else
          class="flex max-h-[136px] flex-col gap-[4px] overflow-auto rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] p-[8px]"
          data-spec="pipeline-form-nodes"
        >
          <label
            v-for="o in nodeOptions"
            :key="o.id"
            class="flex cursor-pointer items-center gap-2 text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              type="checkbox"
              :checked="form.deployNodeIds.includes(o.id)"
              :data-spec="`pipeline-form-node-${o.id}`"
              @change="toggleNode(o.id, $event)"
            >
            {{ o.label }}
          </label>
        </div>
      </div>

      <label class="flex items-center justify-between gap-3">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">自动部署（编译成功后自动安装到部署节点）</span>
        <input
          v-model="form.autoDeploy"
          type="checkbox"
          data-spec="pipeline-form-autodeploy"
        >
      </label>

      <label class="flex items-center justify-between gap-3">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">启用</span>
        <input
          v-model="form.enable"
          type="checkbox"
          data-spec="pipeline-form-enable"
        >
      </label>

      <div
        v-if="projectId > 0"
        class="flex items-center justify-between gap-3 rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[7px]"
        data-spec="pipeline-form-project"
      >
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">项目（部署集级联，只读）</span>
        <span class="font-mono text-[12px] tabular-nums text-[var(--el-text-color-primary)]">#{{ projectId }}</span>
      </div>

      <p
        v-if="formError"
        role="alert"
        data-spec="pipeline-form-error"
        class="m-0 text-[12px] text-[var(--el-color-danger)]"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="pipeline-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        data-spec="pipeline-form-submit"
        :disabled="busy"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
