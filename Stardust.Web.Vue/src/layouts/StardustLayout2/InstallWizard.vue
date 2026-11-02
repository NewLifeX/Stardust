<script setup lang="ts">
/**
 * 接入向导（A 键打开）：复制安装命令 + 等待上线节点列表。
 * 复用 Drawer 组件，不走 useModal（与现有约定一致）。
 * 轮询 /Nodes/Node 最近注册节点，Drawer 关闭即停轮询；不做 4 秒自动判定。
 */
import { onBeforeUnmount, ref, shallowRef, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { toast } from './useToast';
import KIcon from './KIcon.vue';

const props = defineProps<{ open: boolean }>();
const emit = defineEmits<{ close: [] }>();

// 安装命令说明文本（基于旧页 AgentDeploy 的默认下载地址与 StarServer 地址）
// 不伪造后端不存在的一键下载端点；展示标准安装说明，用户在目标服务器上执行
const installCommand = `# 在目标服务器上执行（需要 .NET 9+ 运行时）
# 1. 下载 StarAgent 压缩包
wget http://x.newlifex.com/star/StarAgent-linux-x64.zip -O /tmp/star-agent.zip

# 2. 解压到安装目录
mkdir -p /opt/star-agent
unzip /tmp/star-agent.zip -d /opt/star-agent
cd /opt/star-agent

# 3. 配置 StarServer 地址并启动
./StarAgent --server http://你的服务器地址:6600
# 或使用 systemd 托管（见文档）`;

// 复制按钮状态
const copied = ref(false);
async function copyCommand() {
  try {
    await navigator.clipboard.writeText(installCommand);
    copied.value = true;
    toast('安装命令已复制到剪贴板', 'ok');
    setTimeout(() => {
      copied.value = false;
    }, 2000);
  } catch {
    toast('复制失败，请手动选择文本复制', 'err');
  }
}

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
    if (open) startPolling();
    else stopPolling();
  },
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
    <section>
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
  </Drawer>
</template>
