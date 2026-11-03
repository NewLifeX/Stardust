<script setup lang="ts">
/**
 * NodeFormDrawer：部署节点 / 编译节点的「新增 / 编辑」抽屉表单（kind 区分，F07/F08）。
 * 纯 UI + 本地表单状态：只收 props、只发 submit(form)；校验在 deploy.logic（validateDeployNodeForm /
 * validateBuildNodeForm），真正落库在容器（useDeployData.saveNode / saveBuildNode）。
 * 节点选择器数据由容器 fetchNodeOptions 拉好传入；提交按钮遵守 AGENTS.md §11（busy 禁用 + 文案变化）。
 */
import { reactive, ref, watch } from 'vue';
import Btn from './Btn.vue';
import Drawer from './Drawer/Drawer.vue';
import { validateBuildNodeForm, validateDeployNodeForm } from './deploy.logic.ts';
import type { BuildNodeForm, DeployNodeForm, NodeOption } from './deploy.logic.ts';

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';

const props = defineProps<{
  open: boolean;
  /** deploy = 部署节点（AppDeployNode）；build = 编译节点（AppBuildNode） */
  kind: 'deploy' | 'build';
  /** 编辑时的表单初值；null = 新增 */
  initial: DeployNodeForm | BuildNodeForm | null;
  /** 编辑行的标题信息（节点名 / 编号），新增时给空串 */
  title: string;
  sub: string;
  nodeOptions: NodeOption[];
  nodesLoading: boolean;
  /** 提交请求进行中：按钮禁用并写「保存中…」 */
  busy: boolean;
}>();

const emit = defineEmits<{ close: []; submit: [form: DeployNodeForm | BuildNodeForm] }>();

// 全字段超集表单；submit 时按 kind 收窄成对应形状
const form = reactive<DeployNodeForm & BuildNodeForm>({
  nodeId: 0,
  enable: true,
  port: 0,
  fileName: '',
  arguments: '',
  workingDirectory: '',
  userName: '',
  environments: '',
  maxMemory: 0,
  delay: 0,
  healthCheck: '',
  logDirectory: '',
  logFilePattern: '*.log',
  remark: '',
  sourcePath: '',
  pullCode: true,
  buildProject: true,
  packageOutput: true,
  uploadPackage: true,
  outputPath: '',
});
const formError = ref('');

watch(
  () => [props.open, props.initial] as const,
  ([open, init]) => {
    if (!open) return;
    formError.value = '';
    Object.assign(form, {
      nodeId: 0, enable: true, port: 0, fileName: '', arguments: '', workingDirectory: '', userName: '',
      environments: '', maxMemory: 0, delay: 0, healthCheck: '', logDirectory: '', logFilePattern: '*.log', remark: '',
      sourcePath: '', pullCode: true, buildProject: true, packageOutput: true, uploadPackage: true, outputPath: '',
    }, init ?? {});
  },
  { immediate: true },
);

function onNodeChange(e: Event): void {
  form.nodeId = Number((e.target as HTMLSelectElement).value) || 0;
}

function onSubmit(): void {
  const err = props.kind === 'deploy' ? validateDeployNodeForm(form) : validateBuildNodeForm(form);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  if (props.kind === 'deploy') {
    const m: DeployNodeForm = {
      nodeId: form.nodeId, enable: form.enable, port: form.port, fileName: form.fileName, arguments: form.arguments,
      workingDirectory: form.workingDirectory, userName: form.userName, environments: form.environments,
      maxMemory: form.maxMemory, delay: form.delay, healthCheck: form.healthCheck,
      logDirectory: form.logDirectory, logFilePattern: form.logFilePattern, remark: form.remark,
    };
    emit('submit', m);
  } else {
    const m: BuildNodeForm = {
      nodeId: form.nodeId, enable: form.enable, sourcePath: form.sourcePath, pullCode: form.pullCode,
      buildProject: form.buildProject, packageOutput: form.packageOutput, uploadPackage: form.uploadPackage,
      outputPath: form.outputPath, remark: form.remark,
    };
    emit('submit', m);
  }
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
      data-spec="node-form"
    >
      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">物理节点（只列启用中的节点）</span>
        <span
          v-if="nodesLoading"
          class="text-[12px] text-[var(--el-text-color-secondary)]"
          data-spec="node-form-loading"
        >节点列表加载中…</span>
        <select
          v-else
          :class="inputCls"
          data-spec="node-form-node"
          :value="form.nodeId || ''"
          @change="onNodeChange"
        >
          <option
            value=""
            disabled
          >
            请选择节点
          </option>
          <option
            v-for="n in nodeOptions"
            :key="n.id"
            :value="n.id"
          >
            {{ n.name }}{{ n.ip ? `（${n.ip}）` : '' }}
          </option>
        </select>
      </label>

      <template v-if="kind === 'deploy'">
        <label class="flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
          <input
            v-model="form.enable"
            type="checkbox"
            data-spec="node-form-enable"
          >
          启用（参与自动发布）
        </label>
        <div class="grid grid-cols-2 gap-[10px]">
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">端口（0=默认）</span>
            <input
              v-model.number="form.port"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="node-form-port"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">启动延迟（秒）</span>
            <input
              v-model.number="form.delay"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="node-form-delay"
            >
          </label>
        </div>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">进程文件名（空=跟随部署集）</span>
          <input
            v-model="form.fileName"
            :class="inputCls"
            data-spec="node-form-filename"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">启动参数（空=跟随部署集）</span>
          <input
            v-model="form.arguments"
            :class="inputCls"
            data-spec="node-form-arguments"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">工作目录（空=跟随部署集）</span>
          <input
            v-model="form.workingDirectory"
            :class="inputCls"
            data-spec="node-form-workdir"
          >
        </label>
        <div class="grid grid-cols-2 gap-[10px]">
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">运行用户（Windows 服务用）</span>
            <input
              v-model="form.userName"
              :class="inputCls"
              data-spec="node-form-username"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">最大内存（MB，0=默认）</span>
            <input
              v-model.number="form.maxMemory"
              type="number"
              min="0"
              :class="inputCls"
              data-spec="node-form-maxmemory"
            >
          </label>
        </div>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">环境变量（KEY1=VALUE1;KEY2=VALUE2）</span>
          <input
            v-model="form.environments"
            :class="inputCls"
            data-spec="node-form-environments"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">健康检查地址（空=不检查）</span>
          <input
            v-model="form.healthCheck"
            :class="inputCls"
            data-spec="node-form-healthcheck"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">日志目录（空=跟随部署集；相对路径基于工作目录）</span>
          <input
            v-model="form.logDirectory"
            :class="inputCls"
            data-spec="node-form-logdir"
            placeholder="如 logs 或 D:/app/logs"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">日志文件匹配</span>
          <input
            v-model="form.logFilePattern"
            :class="inputCls"
            data-spec="node-form-logpattern"
            placeholder="*.log"
          >
        </label>
      </template>

      <template v-else>
        <label class="flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
          <input
            v-model="form.enable"
            type="checkbox"
            data-spec="node-form-enable"
          >
          启用
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">源代码目录（编译节点上的路径）</span>
          <input
            v-model="form.sourcePath"
            :class="inputCls"
            data-spec="node-form-sourcepath"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">输出目录（打包产物的位置）</span>
          <input
            v-model="form.outputPath"
            :class="inputCls"
            data-spec="node-form-outputpath"
          >
        </label>
        <div class="flex flex-wrap gap-[16px] text-[12.5px] text-[var(--el-text-color-primary)]">
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.pullCode"
              type="checkbox"
              data-spec="node-form-pullcode"
            >
            拉取源代码
          </label>
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.buildProject"
              type="checkbox"
              data-spec="node-form-build"
            >
            编译项目
          </label>
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.packageOutput"
              type="checkbox"
              data-spec="node-form-package"
            >
            打包输出
          </label>
          <label class="flex items-center gap-[6px]">
            <input
              v-model="form.uploadPackage"
              type="checkbox"
              data-spec="node-form-upload"
            >
            上传应用包
          </label>
        </div>
      </template>

      <label class="flex flex-col gap-[4px]">
        <span class="text-[12px] text-[var(--el-text-color-secondary)]">备注</span>
        <input
          v-model="form.remark"
          :class="inputCls"
          data-spec="node-form-remark"
        >
      </label>

      <p
        v-if="formError"
        data-spec="node-form-error"
        class="m-0 text-[12px] text-[var(--el-color-danger)]"
      >
        {{ formError }}
      </p>
    </div>

    <template #actions>
      <Btn
        variant="ghost"
        data-spec="node-form-cancel"
        @click="emit('close')"
      >
        取消
      </Btn>
      <Btn
        variant="primary"
        data-spec="node-form-submit"
        :disabled="busy"
        @click="onSubmit"
      >
        {{ busy ? '保存中…' : '保存' }}
      </Btn>
    </template>
  </Drawer>
</template>
