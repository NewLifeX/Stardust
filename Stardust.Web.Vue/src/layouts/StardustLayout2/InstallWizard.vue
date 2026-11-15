<script setup lang="ts">
/**
 * 接入向导（A 键 / 节点页「接入节点」按钮打开）：复制安装命令 + 等待上线节点列表
 * + 下载资源（CARD-NODE-02，GET /Agent?format=json 五组）+ 本机探测（GET /Agent/Probe）。
 * 复用 Drawer 组件，不走 useModal（与现有约定一致）。
 * 轮询 /Nodes/Node 最近注册节点，Drawer 关闭即停轮询；不做 4 秒自动判定。
 */
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { toast } from './useToast';
import KIcon from './KIcon.vue';
import {
  buildInstallCommand,
  normalizeAgentData,
  probeState,
  type AgentDlGroups,
  type ProbeState,
} from './agentDl.logic';

const props = defineProps<{ open: boolean }>();
const emit = defineEmits<{ close: [] }>();

// 下载资源（五组）：GET /Agent?format=json，失败可重试（三态文案旅程 §7 R1）
const groups = shallowRef<AgentDlGroups | null>(null);
const resLoading = ref(false);
const resFailed = ref(false);
const resFailedMsg = ref('');

async function fetchResources() {
  resLoading.value = true;
  resFailed.value = false;
  resFailedMsg.value = '';
  try {
    const res = await request({ url: '/Agent', method: 'GET', params: { format: 'json' } });
    groups.value = normalizeAgentData(res?.data);
  } catch (e) {
    groups.value = null;
    resFailed.value = true;
    resFailedMsg.value = e instanceof Error ? e.message : String(e);
  } finally {
    resLoading.value = false;
  }
}

// 安装命令：清单加载成功→基址消费；失败→回退静态默认（AgentController.cs:91 语义）
const installCommand = computed(() => buildInstallCommand(groups.value));

// 复制按钮状态
const copied = ref(false);
async function copyCommand() {
  try {
    await navigator.clipboard.writeText(installCommand.value);
    copied.value = true;
    toast('安装命令已复制到剪贴板', 'ok');
    setTimeout(() => {
      copied.value = false;
    }, 2000);
  } catch {
    toast('复制失败，请手动选择文本复制', 'err');
  }
}

// 本机探测：GET /Agent/Probe（WebApi 代探测，不直连 5500）
const probe = ref<ProbeState>({ online: false, rows: [] });
const probeLoading = ref(false);
const probeFailed = ref(false);
const probeFailedMsg = ref('');

async function fetchProbe() {
  probeLoading.value = true;
  probeFailed.value = false;
  probeFailedMsg.value = '';
  try {
    const res = await request({ url: '/Agent/Probe', method: 'GET' });
    probe.value = probeState(res);
  } catch (e) {
    probe.value = { online: false, rows: [] };
    probeFailed.value = true;
    probeFailedMsg.value = e instanceof Error ? e.message : String(e);
  } finally {
    probeLoading.value = false;
  }
}

const resourceGroups = computed(() => [
  { key: 'zip', title: 'Windows / Linux 压缩包（.zip）', rows: groups.value?.zip ?? [] },
  { key: 'targz', title: 'Linux 压缩包（.tar.gz）', rows: groups.value?.targz ?? [] },
  { key: 'scripts', title: '自动安装脚本', rows: groups.value?.scripts ?? [] },
  { key: 'packages', title: '星尘整体安装包', rows: groups.value?.packages ?? [] },
  { key: 'dotnet', title: '.NET 运行时安装脚本', rows: groups.value?.dotnet ?? [] },
]);

const hasAnyResource = computed(() => resourceGroups.value.some((g) => g.rows.length > 0));

// 等待上线节点列表
interface WaitingNode {
  id: number | string;
  name: string;
  ip: string;
  lastActive: string;
}
const waitingNodes = shallowRef<WaitingNode[]>([]);
const polling = ref(false);
let timer: ReturnType<typeof setInterval> | null = null;

async function pollNodes() {
  if (polling.value) return;
  polling.value = true;
  try {
    const res = await request({ url: '/Nodes/Node', method: 'GET', params: { PageSize: 10, Sort: 'Id desc' } });
    const rows = res?.data;
    if (Array.isArray(rows)) {
      waitingNodes.value = rows.slice(0, 8).map((r: Record<string, unknown>) => ({
        id: (r.id ?? r.Id ?? '') as number | string,
        name: (r.name ?? r.Name ?? r.code ?? r.Code ?? '-') as string,
        ip: ((r.iP ?? r.IP ?? r.address ?? '') as string) || '-',
        lastActive: (r.lastActive ?? r.LastActive ?? '') as string,
      }));
    }
  } catch {
    // 轮询失败静默，下次再试
  } finally {
    polling.value = false;
  }
}

function startPolling() {
  void pollNodes();
  stopPolling();
  timer = setInterval(pollNodes, 4000);
}

function stopPolling() {
  if (timer) {
    clearInterval(timer);
    timer = null;
  }
}

watch(
  () => props.open,
  (open) => {
    if (open) {
      startPolling();
      void fetchResources();
      void fetchProbe();
    } else stopPolling();
  },
  { immediate: true },
);

onBeforeUnmount(stopPolling);
</script>

<template>
  <Drawer
    :open="open"
    title="接入向导"
    sub="在新服务器上安装 StarAgent 并等待上线"
    @close="emit('close')"
  >
    <!-- 第一块：复制安装命令 -->
    <section class="mb-6">
      <h3 class="mb-2 text-[14px] font-semibold text-[var(--el-text-color-primary)]">
        1. 复制安装命令
      </h3>
      <p class="mb-3 text-[12.5px] leading-relaxed text-[var(--el-text-color-secondary)]">
        在目标服务器上执行以下命令，下载并启动 StarAgent。安装完成后节点会自动注册到列表中。
      </p>
      <div class="relative rounded-[8px] border border-[var(--el-border-color)] bg-[var(--el-fill-color-darker)] p-3">
        <pre class="max-h-[200px] overflow-auto whitespace-pre-wrap break-all font-mono text-[11.5px] leading-relaxed text-[var(--el-text-color-primary)]">{{ installCommand }}</pre>
        <button
          type="button"
          data-spec="wizard-copy"
          :disabled="copied"
          class="absolute right-2 top-2 inline-flex h-[26px] items-center gap-1 rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-2 text-[11.5px] text-[var(--el-text-color-regular)] transition-colors hover:bg-[var(--el-fill-color-light)] disabled:opacity-60"
          @click="copyCommand"
        >
          <KIcon
            :name="copied ? 'Check' : 'Copy'"
            :size="12"
          />
          {{ copied ? '已复制' : '复制' }}
        </button>
      </div>
    </section>

    <!-- 第二块：等待上线列表 -->
    <section class="mb-6">
      <h3 class="mb-2 text-[14px] font-semibold text-[var(--el-text-color-primary)]">
        2. 等待节点上线
      </h3>
      <p class="mb-3 text-[12.5px] text-[var(--el-text-color-secondary)]">
        最近注册的节点会自动出现在下方列表（每 4 秒刷新一次）。
      </p>

      <div
        v-if="waitingNodes.length === 0"
        class="rounded-[8px] border border-dashed border-[var(--el-border-color)] px-4 py-8 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <div class="mb-1 flex items-center justify-center gap-2">
          <span
            class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]"
          />
          正在等待节点注册…
        </div>
        <div>在服务器上执行安装命令后，节点会自动出现在这里</div>
      </div>

      <ul
        v-else
        class="divide-y divide-[var(--el-border-color-light)] rounded-[8px] border border-[var(--el-border-color)]"
      >
        <li
          v-for="node in waitingNodes"
          :key="node.id"
          class="flex items-center gap-3 px-3 py-2"
        >
          <span class="h-[6px] w-[6px] rounded-full bg-[var(--el-color-success)]" />
          <span class="min-w-0 flex-1 truncate text-[13px] text-[var(--el-text-color-primary)]">{{ node.name }}</span>
          <span class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">{{ node.ip }}</span>
        </li>
      </ul>
    </section>

    <!-- 第三块：下载资源（CARD-NODE-02，五组） -->
    <section
      class="mb-6"
      data-spec="agent-dl"
    >
      <h3 class="mb-2 text-[14px] font-semibold text-[var(--el-text-color-primary)]">
        3. 下载资源
      </h3>
      <p class="mb-3 text-[12.5px] text-[var(--el-text-color-secondary)]">
        StarAgent 压缩包与安装脚本，来自下载清单接口。
      </p>

      <div
        v-if="resLoading"
        data-spec="agent-dl-loading"
        class="rounded-[8px] border border-[var(--el-border-color)] px-4 py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="mr-1 inline-block h-[12px] w-[12px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        下载资源加载中…
      </div>

      <div
        v-else-if="resFailed"
        data-spec="agent-dl-failed"
        class="rounded-[8px] border border-[var(--el-border-color)] px-4 py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <div class="mb-2">
          下载资源加载失败：{{ resFailedMsg }}
        </div>
        <button
          type="button"
          data-spec="agent-dl-retry"
          class="inline-flex h-[28px] items-center rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-3 text-[12px] text-[var(--el-text-color-regular)] transition-colors hover:bg-[var(--el-fill-color-light)]"
          @click="fetchResources"
        >
          重试
        </button>
      </div>

      <div
        v-else-if="!hasAnyResource"
        data-spec="agent-dl-empty"
        class="rounded-[8px] border border-dashed border-[var(--el-border-color)] px-4 py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        暂无下载资源
      </div>

      <template v-else>
        <div
          v-for="g in resourceGroups"
          v-show="g.rows.length > 0"
          :key="g.key"
          class="mb-3"
        >
          <div class="mb-1 text-[12.5px] font-medium text-[var(--el-text-color-regular)]">
            {{ g.title }}
          </div>
          <table
            :data-spec="`agent-dl-table-${g.key}`"
            class="w-full border-collapse rounded-[8px] border border-[var(--el-border-color)] text-left text-[12px]"
          >
            <thead>
              <tr class="border-b border-[var(--el-border-color)] bg-[var(--el-fill-color-light)] text-[var(--el-text-color-secondary)]">
                <th class="px-3 py-1.5 font-medium">
                  文件名
                </th>
                <th class="px-3 py-1.5 font-medium">
                  说明 / 运行时
                </th>
                <th class="px-3 py-1.5 font-medium">
                  下载
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="(r, i) in g.rows"
                :key="r.url + i"
                class="border-b border-[var(--el-border-color-light)] last:border-b-0"
              >
                <td class="px-3 py-1.5 font-mono text-[11.5px] text-[var(--el-text-color-primary)]">
                  {{ r.name }}
                </td>
                <td class="px-3 py-1.5 text-[var(--el-text-color-secondary)]">
                  {{ r.meta || '—' }}
                </td>
                <td class="px-3 py-1.5">
                  <a
                    :href="r.url"
                    target="_blank"
                    rel="noopener"
                    class="text-[var(--el-color-primary)] hover:underline"
                  >下载</a>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </section>

    <!-- 第四块：本机探测（CARD-NODE-02，GET /Agent/Probe） -->
    <section data-spec="agent-probe">
      <h3 class="mb-2 text-[14px] font-semibold text-[var(--el-text-color-primary)]">
        4. 本机探测
      </h3>
      <p class="mb-3 text-[12.5px] text-[var(--el-text-color-secondary)]">
        检测本机 StarAgent 是否在线（由服务端代为探测）。
      </p>

      <div
        v-if="probeLoading"
        data-spec="agent-probe-loading"
        class="rounded-[8px] border border-[var(--el-border-color)] px-4 py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="mr-1 inline-block h-[12px] w-[12px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        本机探测中…
      </div>

      <div
        v-else-if="probeFailed"
        data-spec="agent-probe-failed"
        class="rounded-[8px] border border-[var(--el-border-color)] px-4 py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <div class="mb-2">
          探测失败：{{ probeFailedMsg }}
        </div>
        <button
          type="button"
          data-spec="agent-probe-retry"
          class="inline-flex h-[28px] items-center rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-3 text-[12px] text-[var(--el-text-color-regular)] transition-colors hover:bg-[var(--el-fill-color-light)]"
          @click="fetchProbe"
        >
          重试
        </button>
      </div>

      <div
        v-else-if="!probe.online"
        data-spec="agent-probe-offline"
        class="rounded-[8px] border border-dashed border-[var(--el-border-color)] px-4 py-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        未检测到本机 StarAgent
      </div>

      <table
        v-else
        data-spec="agent-probe-table"
        class="w-full border-collapse rounded-[8px] border border-[var(--el-border-color)] text-left text-[12px]"
      >
        <tbody>
          <tr
            v-for="row in probe.rows"
            :key="row.label"
            class="border-b border-[var(--el-border-color-light)] last:border-b-0"
          >
            <th class="w-[110px] px-3 py-1.5 font-medium text-[var(--el-text-color-secondary)]">
              {{ row.label }}
            </th>
            <td class="px-3 py-1.5 font-mono text-[11.5px] text-[var(--el-text-color-primary)]">
              {{ row.value }}
            </td>
          </tr>
        </tbody>
      </table>
    </section>
  </Drawer>
</template>
