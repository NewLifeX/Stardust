<script setup lang="ts">
/**
 * StardustLayout2：按设计交接包 v2（此刻 / 应用 / 节点 / 发布 / 治理）重做的控制台外壳。
 * 结构：56px 顶栏 + 可滚动内容区（max 1240，左右 32，底部留 104 给右下角的控制台按钮）+ 底部控制台（展开 300px，在 flex 列里挤压内容区）。
 * 页面经框架 RootLayout 以默认插槽注入，不能用 router-view。
 * 命令面板（顶栏搜索与 ⌘K）、生产确认（GuardModal）、全局热键、底部控制台（日志 / 终端）和动词键 P/C/L/N/A（首次按键时拉取应用与节点）已接入；抽屉与接入向导在对应页面完成后接入（A 键目前提示「接入向导待接入」）。
 */
import { computed, onBeforeUnmount, onMounted, shallowRef, watch } from 'vue';
import { useRoute } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import TopBar from './TopBar.vue';
import KIcon from './KIcon.vue';
import CommandPalette from './CommandPalette/CommandPalette.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import ConsoleDock from './ConsoleDock/ConsoleDock.vue';
import InstallWizard from './InstallWizard.vue';
import { useConsoleShell } from './useConsoleShell';
import { useConsoleDock } from './useConsoleDock';
import { appNameById, buildBasePalette, parseAppId } from './palette.items';
import { isTypingTarget, type Verb } from './hotkeys.logic';
import { resolveVerb, type PaletteData } from './paletteData.logic';
import { createPaletteRuntime } from './paletteRuntime.logic';
import { toast } from './useToast';
import { stardustContext } from '@/stores/stardustContext';
import { contextKey } from './contextReload.logic';
import { useGlobalHotkeys } from './useGlobalHotkeys';
import type { DockLog, DockNode } from './dock.logic';
import {
  cancelGuard,
  closeDock,
  closePalette,
  closeWizard,
  confirmGuard,
  dockOpen,
  dockTab,
  guard,
  guardOpen,
  openDock,
  openPalette,
  openWizard,
  palette,
  paletteOpen,
  setDockTab,
  termNodeReq,
  toggleDock,
  wizardOpen,
} from './ui.store';

const { projects, projectName, active, initial, theme, toggleTheme, go, pickProject, logout } = useConsoleShell();

// 当前应用 Id：只有应用工作台路由 /app/:tab/:id 带 id，其余页面为 null（日志不过滤）
const route = useRoute();
const routeAppId = computed(() => parseAppId(route.params.id));

// 日志暂无真实数据源：先传空数组，接口接入后只改这处
const dockLogs: readonly DockLog[] = [];

// 应用/节点数据：动词键与底部控制台的节点列表共用，首次按动词键时才拉取。
// 缓存、并发去重、部分失败不缓存、上下文失效清缓存的逻辑在 paletteRuntime.logic.ts。
const paletteRuntime = createPaletteRuntime(request);
const paletteData = shallowRef<PaletteData | null>(null);
async function ensurePaletteData(): Promise<PaletteData> {
  const d = await paletteRuntime.ensure();
  paletteData.value = d;
  return d;
}

// 项目、环境变化后，缓存里的应用与节点属于旧上下文：清空缓存并让在途请求的结果作废；
// 正在应用工作台时立即重取，底部标签的应用名不丢。请求本身的 projectId 由全局拦截器注入。
watch(
  () => contextKey(stardustContext),
  () => {
    paletteRuntime.invalidate();
    paletteData.value = null;
    if (routeAppId.value !== null) void ensurePaletteData().catch(() => {});
  },
);

// 节点来自 /Nodes/Node；还没拉取或拉取失败时为空数组
const dockNodes = computed<readonly DockNode[]>(() =>
  (paletteData.value?.nodes ?? []).map((n) => ({ id: n.id, name: n.name, ip: n.ip ?? '', online: n.online })),
);

// 当前应用范围：由路由 Id 在应用列表里查出应用名（日志按应用名过滤、底部标签显示应用名）；非工作台页面或查不到时为 null
const scopeApp = computed<string | null>(() => appNameById(paletteData.value?.apps ?? [], routeAppId.value));

// 进入应用工作台时才需要 Id → 名称的映射，拉取一次；失败不拦截页面，名称取不到时标签只显示项目名
watch(
  routeAppId,
  (id) => {
    if (id !== null) void ensurePaletteData().catch(() => {});
  },
  { immediate: true },
);

const {
  termNode,
  lines: termLines,
  pickNode: pickTermNode,
  openTerm: openTermNode,
  submit: submitTerm,
  clear: clearTerm,
} = useConsoleDock({ nodes: () => dockNodes.value });

// 页面（节点页/此刻页）经 ui.store 的 requestTermNode 请求打开某节点终端：
// 选中该节点（离线会退到第一台在线节点）并展开底部控制台的终端页签。
watch(termNodeReq, (req) => {
  if (!req) return;
  openTermNode(req.id);
  openDock('term');
});

// 顶栏搜索按钮与 ⌘K 共用：打开基础面板。应用/节点数据与向导、控制台的处理函数待对应页面接入后再传，目前只有「页面」一组
const openSearch = () => openPalette(buildBasePalette({ go }));

// 动词键 P/C/L/N/A：A 不需要数据，不发请求；其余先取数再交给 resolveVerb（失败与空结果都会 toast，按键不静默）
const NO_DATA: PaletteData = { apps: [], nodes: [], failed: [] };
async function runVerb(verb: Verb): Promise<void> {
  try {
    const data = verb === 'A' ? NO_DATA : await ensurePaletteData();
    const out = resolveVerb(verb, data, {
      go,
      openTerm: (id) => {
        openTermNode(id);
        openDock('term');
      },
      startWizard: openWizard,
    });
    if (out.type === 'palette') openPalette(out.config);
    else if (out.type === 'toast') toast(out.message, out.tone);
    else if (out.type === 'wizard') openWizard();
  } catch {
    toast('列表加载失败', 'err');
  }
}

// 全局热键：⌘K、面板/向导/护栏/控制台的 Esc、反引号开合控制台、P/C/L/N/A 动词键
const { onKeydown } = useGlobalHotkeys({
  isTyping: () => isTypingTarget(document.activeElement),
  paletteOpen: () => paletteOpen.value,
  wizardOpen: () => wizardOpen.value,
  guardOpen: () => guardOpen.value,
  dockOpen: () => dockOpen.value,
  openPalette: openSearch,
  closePalette,
  closeWizard,
  cancelGuard,
  closeDock,
  toggleDock,
  onVerb: (verb) => void runVerb(verb),
});
const onWindowKeydown = (e: KeyboardEvent) => void onKeydown(e);
onMounted(() => window.addEventListener('keydown', onWindowKeydown));
onBeforeUnmount(() => window.removeEventListener('keydown', onWindowKeydown));
</script>

<template>
  <div
    data-spec="console-shell"
    class="flex h-screen flex-col overflow-hidden bg-[var(--el-bg-color-page)] text-[var(--el-text-color-primary)]"
  >
    <TopBar
      :projects="projects"
      :project-name="projectName"
      :theme="theme"
      :initial="initial"
      :active="active"
      @go="go"
      @project="pickProject"
      @toggle-theme="toggleTheme"
      @logout="logout"
      @search="openSearch"
    />
    <main class="min-h-0 flex-1 overflow-y-auto overflow-x-hidden px-8 pb-[104px] pt-7 max-[900px]:px-4 max-[900px]:pt-[18px]">
      <div class="mx-auto max-w-[1240px]">
        <slot />
      </div>
    </main>
    <ConsoleDock
      :open="dockOpen"
      :tab="dockTab"
      :project="projectName"
      :scope-app="scopeApp"
      :logs="dockLogs"
      :nodes="dockNodes"
      :term-node="termNode"
      :lines="termLines"
      @set-tab="setDockTab"
      @close="closeDock"
      @clear="clearTerm"
      @pick-node="pickTermNode"
      @submit="submitTerm"
    />
    <button
      v-if="!dockOpen"
      type="button"
      data-spec="dock-fab"
      class="fixed bottom-[18px] right-[20px] z-30 inline-flex h-[32px] cursor-pointer items-center gap-2 whitespace-nowrap rounded-[999px] border border-[var(--el-border-color)] bg-[color-mix(in_oklab,var(--el-bg-color-overlay)_88%,transparent)] px-[13px] text-[12.5px] font-medium text-[var(--el-text-color-regular)] backdrop-blur-[10px] transition-colors duration-[120ms] [box-shadow:var(--el-box-shadow)] hover:border-[var(--el-border-color-dark)] hover:text-[var(--el-text-color-primary)]"
      @click="openDock()"
    >
      <KIcon
        name="Terminal"
        :size="13"
      />
      控制台
      <span class="rounded-sm border border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[5px] py-px font-mono text-[10.5px] text-[var(--el-text-color-secondary)]">`</span>
    </button>
    <CommandPalette
      v-if="palette"
      :config="palette"
      @close="closePalette"
    />
    <InstallWizard
      :open="wizardOpen"
      @close="closeWizard"
    />
    <GuardModal
      v-if="guard"
      :label="guard.label"
      @cancel="cancelGuard"
      @confirm="confirmGuard"
    />
  </div>
</template>
