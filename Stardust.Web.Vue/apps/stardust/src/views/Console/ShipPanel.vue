<script setup lang="ts">
/**
 * ShipPanel：部署内容容器，/ship 页（不传 section，纵向显示全部）与应用工作台的版本、节点、部署历史、流水线四个页签（传 section，只渲染一块）共用。
 * 概览页签（传 overview）：只渲染版本条与「需关注事项」（buildAttention 算出，点按钮发 go(tab)），不渲染四个操作块。
 * 2026-10-28 起退化为容器：取数、刷新、操作反馈、应用版本、节点操作、选中运行都在 useDeployData（有单测），
 * 展示拆成五个只收 props 的组件（各有 story 与 CT）：
 *  1. DeployVersionBar    当前生效版本与刷新按钮
 *  2. DeployVersionList   版本列表，可「应用此版本」（UseVersion）
 *  3. DeployNodes         部署节点，对单个节点安装、启动、重启、停止、卸载（Operate）
 *  4. DeployHistory       部署历史（AppDeployHistory，只是下发记录，不是节点执行结果）
 *  5. DeployPipelineRuns  流水线运行与按节点的部署结果
 * 容器只做三件事：把状态算成 props 传下去、在点击后用 openGuard 做二次确认、保留加载中/失败/没有部署集/操作反馈四块。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { pipelineSteps } from '../Stardust/appsModel';
import type { PipelineRunRow } from '../Stardust/appsModel';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Card from '@/layouts/StardustLayout2/Card.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import BuildNodes from '@/layouts/StardustLayout2/BuildNodes.vue';
import DeployHistory from '@/layouts/StardustLayout2/DeployHistory.vue';
import DeployNodes from '@/layouts/StardustLayout2/DeployNodes.vue';
import DeployPipelineRuns from '@/layouts/StardustLayout2/DeployPipelineRuns.vue';
import DeployVersionBar from '@/layouts/StardustLayout2/DeployVersionBar.vue';
import DeployVersionList from '@/layouts/StardustLayout2/DeployVersionList.vue';
import AttentionList from '@/layouts/StardustLayout2/AttentionList.vue';
import { openGuard } from '@/layouts/StardustLayout2/ui.store';
import type { DeploySection } from '@/layouts/StardustLayout2/workbench.logic';
import type { AppTab } from '@/layouts/StardustLayout2/palette.items';
import { buildAttention } from '@/layouts/StardustLayout2/overview.logic';
import { toast } from '@/layouts/StardustLayout2/useToast';
import { useDeployData } from '@/layouts/StardustLayout2/useDeployData';
import { NOTICE_CLASS } from '@/layouts/StardustLayout2/deployView.styles';
import { nodeStats as calcNodeStats, nodeVersionTag as calcNodeVersionTag } from '@/layouts/StardustLayout2/deployView.logic';
import type { VersionTag } from '@/layouts/StardustLayout2/deployView.logic';
import type { PipelineView, StageItem } from '@/layouts/StardustLayout2/deployPipeline.styles';
import {
  buildNodeResults,
  buildRunRows,
  buildVersionCells,
  operateNotice,
  useVersionNotice,
} from '@/layouts/StardustLayout2/ship.logic';
import type { NodeActionInfo, Notice, ShipNode, VersionOption } from '@/layouts/StardustLayout2/ship.logic';
import { fetchBuildNodes, runBuildUpload, runPackageUpload } from '@/layouts/StardustLayout2/buildNodes.logic';
import type { BuildNodeAct, BuildNodeRow } from '@/layouts/StardustLayout2/buildNodes.logic';

// section：只渲染哪一块（versions / nodes / history / pipeline）；不传表示纵向显示全部（/ship 整页用，停点 20 随该页一起去掉）
// overview：概览页签，只渲染版本条与需关注事项；instances 是在线实例数，由页面传入（未知不传）
const props = defineProps<{ appId: number; appName: string; section?: DeploySection; overview?: boolean; instances?: number }>();
const emit = defineEmits<{ go: [tab: AppTab] }>();

// 该块是否要渲染
function show(name: DeploySection): boolean {
  if (props.overview) return false;
  return props.section === undefined || props.section === name;
}

const {
  loading,
  loadError,
  deploy,
  versions,
  versionsFailed,
  nodes,
  nodesFailed,
  history,
  runs,
  selectedRunId,
  steps,
  busyKey,
  busyText,
  feedback,
  enabledNodes,
  refreshing,
  refresh: onRefresh,
  load,
  applyVersion,
  operate,
  selectRun,
} = useDeployData({
  appId: () => props.appId,
  appName: () => props.appName,
  request,
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

// ---------- 当前生效版本与节点版本标签 ----------

// 计算逻辑在 deployView.logic.ts（有单测），这里只负责把当前状态传进去
const nodeStats = computed(() => calcNodeStats(deploy.value?.version ?? '', nodes.value));

// 按节点行 id 索引，停用节点或没有部署集时为 null
const versionTags = computed(() => {
  const tags: Record<number, VersionTag | null> = {};
  for (const n of nodes.value) tags[n.id] = calcNodeVersionTag(deploy.value, n);
  return tags;
});

// ---------- 版本列表：应用此版本前二次确认 ----------

const versionNotice = computed<Notice | null>(() => useVersionNotice(deploy.value, enabledNodes.value.length));

function askApply(v: VersionOption): void {
  if (busyKey.value || v.disabled) return;
  const notice = versionNotice.value;
  openGuard(`${props.appName} 切换到 ${v.version}。${notice?.text ?? ''}`, () => applyVersion(v));
}

// ---------- 部署节点：操作前二次确认 ----------

function askOperate(n: ShipNode, a: NodeActionInfo): void {
  if (busyKey.value) return;
  const notice = operateNotice(a, n);
  openGuard(`${n.name} · ${a.label}。${notice.text}`, () => operate(n, a.act));
}

// ---------- 编译节点（子 tab） ----------

type NodeTab = 'deploy' | 'build';
const nodeTab = ref<NodeTab>('deploy');
const buildRows = ref<BuildNodeRow[]>([]);
const buildFailed = ref(false);
const buildBusyKey = ref('');

async function loadBuildNodes(): Promise<void> {
  const did = deploy.value?.id ?? 0;
  if (!did || did <= 0) {
    buildRows.value = [];
    buildFailed.value = false;
    return;
  }
  const rows = await fetchBuildNodes(request, did);
  if (rows === null) {
    buildFailed.value = true;
  } else {
    buildFailed.value = false;
    buildRows.value = rows;
  }
}

// deploy 出现/变化时拉一次编译节点；刷新按钮已在 useDeployData 里，这里不动它
watch(deploy, () => { void loadBuildNodes(); });

function askBuildOperate(row: BuildNodeRow, act: BuildNodeAct): void {
  if (buildBusyKey.value) return;
  const label = act === 'Build-Upload' ? '编译上传' : '打包上传';
  const desc = act === 'Build-Upload'
    ? '将在节点上拉取源代码、编译项目、打包并上传应用包'
    : '不拉取源代码、不编译，直接打包输出目录并上传应用包';
  openGuard(`${row.nodeName} · ${label}。${desc}。确认？`, async () => {
    buildBusyKey.value = `${row.id}:${act}`;
    try {
      const fn = act === 'Build-Upload' ? runBuildUpload : runPackageUpload;
      const r = await fn(request, row.id);
      toast(r.message, r.ok ? 'ok' : 'err');
      await loadBuildNodes();
      // 编译是长任务，4 秒后再刷新一次，让用户看到状态变化
      setTimeout(() => { void loadBuildNodes(); }, 4000);
    } finally {
      buildBusyKey.value = '';
    }
  });
}

// ---------- 流水线运行与按节点结果 ----------

const runRows = computed(() => buildRunRows(runs.value, versions.value));
const selectedRaw = computed(() => runs.value.find((r) => r.id === selectedRunId.value));

// 流水线四段（构建、上传、分发、部署）；只用运行状态推算，不读步骤表
const stages = computed<StageItem[]>(() => {
  const r = selectedRaw.value;
  return r ? pipelineSteps(r as unknown as PipelineRunRow) : [];
});

const view = computed<PipelineView | null>(() => {
  const d = deploy.value;
  if (!d) return null;
  const run = selectedRaw.value;
  if (run && steps.value) {
    const r = buildNodeResults(nodes.value, steps.value, run.status);
    if (r.deploySteps > 0) return { items: r.items, allSkipped: r.allSkipped, note: `运行 #${run.id} 按节点的部署步骤` };
    return {
      items: buildVersionCells(nodes.value, d.version),
      allSkipped: false,
      note: `运行 #${run.id} 没有按节点的部署步骤（只有流水线发起的部署才有），下面是节点当前版本`,
    };
  }
  if (run && steps.value === null) {
    return { items: buildVersionCells(nodes.value, d.version), allSkipped: false, note: '部署步骤加载失败，下面是节点当前版本' };
  }
  return {
    items: buildVersionCells(nodes.value, d.version),
    allSkipped: false,
    note: `点一条运行记录查看按节点的部署步骤；未选择时是节点当前版本与部署集版本 ${d.version || '—'} 的比对`,
  };
});

// ---------- 概览：需关注事项 ----------

// useDeployData 加载失败时不清旧的 deploy，所以加载失败一律当没有部署集，不拿旧数据判断
const attention = computed(() =>
  buildAttention({
    deploy: loadError.value ? null : deploy.value,
    deployFailed: !!loadError.value,
    nodes: nodes.value,
    nodesFailed: nodesFailed.value,
    runs: runs.value,
    history: history.value,
    instances: props.instances,
    now: Date.now(),
  }),
);
</script>

<template>
  <div
    data-spec="ship-panel"
    class="flex flex-col gap-3"
  >
    <Card
      v-if="loading"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
      data-spec="ship-loading"
    >
      加载中…
    </Card>

    <Card
      v-else-if="loadError"
      data-spec="ship-error"
    >
      <div class="flex items-center gap-3 text-[13px] text-[var(--el-color-danger)]">
        {{ loadError }}
        <Btn
          size="sm"
          @click="load()"
        >
          重试
        </Btn>
      </div>
    </Card>

    <Card
      v-else-if="!deploy && !overview"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
      data-spec="ship-nodeploy"
    >
      {{ appName }} 还没有部署集，无法在这里发布。
    </Card>

    <template v-else-if="deploy">
      <!-- 操作进行中与最近反馈：始终在用户视线内 -->
      <div
        v-if="busyText || feedback.length"
        class="flex flex-col gap-[6px]"
        data-spec="ship-feedback"
      >
        <div
          v-if="busyText"
          class="rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-primary)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_10%,transparent)] px-3 py-2 text-[12.5px] text-[var(--el-color-primary-light-3)]"
          data-spec="ship-busy"
        >
          {{ busyText }}
        </div>
        <div
          v-for="f in feedback"
          :key="f.key"
          data-spec="ship-feedback-item"
          class="flex items-start gap-2 rounded-[8px] border px-3 py-2 text-[12.5px]"
          :class="f.ok ? NOTICE_CLASS.info : 'border-[color-mix(in_oklab,var(--el-color-danger)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_10%,transparent)] text-[var(--el-color-danger)]'"
        >
          <KIcon
            :name="f.ok ? 'Check' : 'Alert'"
            :size="13"
            class="mt-[2px] shrink-0"
          />
          <span class="min-w-0 flex-1 break-words">{{ f.text }}</span>
          <span class="shrink-0 font-mono text-[11px] tabular-nums opacity-70">{{ f.at }}</span>
        </div>
      </div>

      <DeployVersionBar
        :version="deploy.version"
        :enable="deploy.enable"
        :auto-publish="deploy.autoPublish"
        :multi-version="deploy.multiVersion"
        :stats="nodeStats"
        :nodes-failed="nodesFailed"
        :refreshing="refreshing"
        :busy="!!busyKey"
        @refresh="onRefresh"
      />

      <div
        v-if="!overview"
        class="flex flex-col gap-3"
        data-spec="ship-body"
        :data-section="section ?? 'all'"
      >
        <div v-if="show('versions')">
          <DeployVersionList
            :versions="versions"
            :versions-failed="versionsFailed"
            :notice="versionNotice"
            :busy-key="busyKey"
            @apply="askApply"
          />
        </div>

        <div v-if="show('nodes')">
          <!-- 子 tab：部署节点 / 编译节点 -->
          <div
            class="mb-[10px] inline-flex gap-[2px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[3px]"
            data-spec="node-subtabs"
          >
            <button
              type="button"
              data-spec="node-subtab-deploy"
              :aria-current="nodeTab === 'deploy' ? 'page' : undefined"
              class="h-[26px] whitespace-nowrap rounded-[6px] px-[13px] text-[12.5px] font-medium transition-colors"
              :class="nodeTab === 'deploy'
                ? 'bg-[var(--el-fill-color-light)] text-[var(--el-text-color-primary)]'
                : 'text-[var(--el-text-color-secondary)] hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]'"
              @click="nodeTab = 'deploy'"
            >
              部署节点
            </button>
            <button
              type="button"
              data-spec="node-subtab-build"
              :aria-current="nodeTab === 'build' ? 'page' : undefined"
              class="h-[26px] whitespace-nowrap rounded-[6px] px-[13px] text-[12.5px] font-medium transition-colors"
              :class="nodeTab === 'build'
                ? 'bg-[var(--el-fill-color-light)] text-[var(--el-text-color-primary)]'
                : 'text-[var(--el-text-color-secondary)] hover:bg-[var(--el-bg-color-overlay)] hover:text-[var(--el-text-color-primary)]'"
              @click="nodeTab = 'build'"
            >
              编译节点
            </button>
          </div>

          <DeployNodes
            v-if="nodeTab === 'deploy'"
            :nodes="nodes"
            :nodes-failed="nodesFailed"
            :busy-key="busyKey"
            :version-tags="versionTags"
            @operate="askOperate"
          />
          <BuildNodes
            v-else
            :rows="buildRows"
            :failed="buildFailed"
            :busy-key="buildBusyKey"
            @operate="askBuildOperate"
          />
        </div>

        <div v-if="show('history')">
          <DeployHistory
            :history="history"
            :nodes="nodes"
          />
        </div>

        <div v-if="show('pipeline')">
          <DeployPipelineRuns
            :run-rows="runRows"
            :selected-run-id="selectedRunId"
            :stages="stages"
            :view="view"
            @select="selectRun"
          />
        </div>
      </div>
    </template>

    <!-- 概览：版本条之后是需关注事项；加载中不显示，加载失败时照常显示（部署相关项为待接入，实例数不依赖部署集） -->
    <AttentionList
      v-if="overview && !loading"
      :items="attention"
      @go="emit('go', $event)"
    />
  </div>
</template>
