<script setup lang="ts">
/**
 * DeployLogDrawer：部署节点本地文件日志（DEPLOY-1 MVP）。容器传入 deployNodeId / 节点名，内部拉 LogFiles + LogTail。
 */
import { ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { fetchDeployLogFiles, fetchDeployLogTail } from './deployLog.logic.ts';
import type { DeployLogFileRow } from './deployLog.logic.ts';

const props = defineProps<{
  open: boolean;
  deployNodeId: number;
  nodeName: string;
}>();

const emit = defineEmits<{ close: [] }>();

const files = ref<DeployLogFileRow[]>([]);
const selectedFile = ref('');
const content = ref('');
const truncated = ref(false);
const error = ref('');
const loadingFiles = ref(false);
const loadingTail = ref(false);

async function loadFiles(): Promise<void> {
  if (!props.deployNodeId) return;
  loadingFiles.value = true;
  error.value = '';
  const rs = await fetchDeployLogFiles(request, props.deployNodeId);
  loadingFiles.value = false;
  if (!rs.ok) {
    error.value = rs.message;
    files.value = [];
    selectedFile.value = '';
    content.value = '';
    return;
  }
  files.value = rs.files;
  if (!selectedFile.value && rs.files.length) selectedFile.value = rs.files[0].name;
  if (selectedFile.value) await loadTail();
  else content.value = '';
}

async function loadTail(): Promise<void> {
  if (!props.deployNodeId || !selectedFile.value) return;
  loadingTail.value = true;
  error.value = '';
  const rs = await fetchDeployLogTail(request, props.deployNodeId, selectedFile.value);
  loadingTail.value = false;
  if (!rs.ok || !rs.tail) {
    error.value = rs.message;
    content.value = '';
    truncated.value = false;
    return;
  }
  content.value = rs.tail.content;
  truncated.value = rs.tail.truncated;
}

async function onRefresh(): Promise<void> {
  await loadFiles();
}

watch(
  () => [props.open, props.deployNodeId] as const,
  ([open]) => {
    if (!open) return;
    selectedFile.value = '';
    files.value = [];
    content.value = '';
    error.value = '';
    void loadFiles();
  },
);

watch(selectedFile, () => {
  if (props.open && selectedFile.value) void loadTail();
});
</script>

<template>
  <Drawer
    :open="open"
    title="节点文件日志"
    :sub="`${nodeName} · 部署节点 #${deployNodeId}`"
    @close="emit('close')"
  >
    <div
      class="flex flex-col gap-[10px]"
      data-spec="deploy-log-drawer"
    >
      <p class="text-[11.5px] text-[var(--el-text-color-secondary)]">
        读取 StarAgent 节点上配置的日志目录；与「应用日志」页签（SDK 上报）不同。未配置日志目录请在部署节点编辑里填写。
      </p>
      <div class="flex flex-wrap items-center gap-[8px]">
        <label class="flex flex-1 min-w-[200px] flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">日志文件</span>
          <select
            v-model="selectedFile"
            class="h-[30px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px]"
            data-spec="deploy-log-file-select"
            :disabled="loadingFiles || loadingTail || !files.length"
          >
            <option
              v-if="!files.length"
              value=""
            >
              {{ loadingFiles ? '加载文件列表…' : '无匹配文件' }}
            </option>
            <option
              v-for="f in files"
              :key="f.name"
              :value="f.name"
            >
              {{ f.name }} ({{ f.size }} B)
            </option>
          </select>
        </label>
        <Btn
          size="sm"
          variant="default"
          data-spec="deploy-log-refresh"
          :disabled="loadingFiles || loadingTail"
          @click="onRefresh"
        >
          {{ loadingFiles || loadingTail ? '加载中…' : '刷新' }}
        </Btn>
      </div>
      <p
        v-if="error"
        class="text-[12px] text-[var(--el-color-danger)]"
        data-spec="deploy-log-error"
      >
        {{ error }}
      </p>
      <p
        v-if="truncated"
        class="text-[11px] text-[var(--el-text-color-secondary)]"
      >
        仅显示文件尾部片段；更大范围查看请登录节点或使用后续版本分页。
      </p>
      <pre
        class="max-h-[420px] overflow-auto rounded-[8px] border border-[var(--el-border-color-lighter)] bg-[var(--el-fill-color-darker)] p-3 font-mono text-[11.5px] leading-relaxed text-[var(--el-text-color-primary)] whitespace-pre-wrap break-all"
        data-spec="deploy-log-content"
      >{{ content || (loadingTail ? '读取中…' : '—') }}</pre>
    </div>
  </Drawer>
</template>
