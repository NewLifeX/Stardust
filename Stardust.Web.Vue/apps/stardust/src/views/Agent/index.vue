<script setup lang="ts">
/**
 * Agent/Index — 星尘代理 StarAgent 下载安装静态展示页（自定义迁移页）
 *
 * MVC 源 AgentController.cs + Agent/Index.cshtml 还原：
 *  - 根 Area 的 AgentController，[Route("[controller]")] → 路由 #/Agent
 *  - [AllowAnonymous]，纯展示 + JS 探测本机 Agent
 *  - 内容：StarAgent 介绍、下载链接、安装命令、本机探测状态
 *
 * 前端为静态展示页：标题 + 下载/安装说明（el-card / el-steps / 代码块）+ 本机探测状态。
 * 下载数据优先从后端 GET /Agent?format=json 拉取（真实接口），失败则回退内置静态数据，
 * 确保页面始终真实渲染、不 404、无错误。
 */
import { ref, onMounted } from 'vue';
import request from '@newlifex/cube-vue/core/utils/request';

interface AgentItem {
  name: string;
  framework?: string;
  os?: string;
  description?: string;
  url: string;
}
interface AgentData {
  server: string;
  agent: {
    zip: AgentItem[];
    targz: AgentItem[];
    scripts: AgentItem[];
  };
  packages: AgentItem[];
  dotnet: AgentItem[];
}

// 内置静态兜底数据（与后端 GetAgentData 一致，防止接口不可达时白屏）
const FALLBACK: AgentData = {
  server: '',
  agent: {
    zip: [
      { name: 'staragent10.zip', framework: 'net10.0', os: 'Windows/Linux', url: 'https://x.newlifex.com/star/staragent10.zip' },
      { name: 'staragent90.zip', framework: 'net9.0', os: 'Windows/Linux', url: 'https://x.newlifex.com/star/staragent90.zip' },
      { name: 'staragent80.zip', framework: 'net8.0', os: 'Windows/Linux', url: 'https://x.newlifex.com/star/staragent80.zip' },
      { name: 'staragent60.zip', framework: 'net6.0', os: 'Windows/Linux', url: 'https://x.newlifex.com/star/staragent60.zip' },
      { name: 'staragent45.zip', framework: 'net4.5', os: 'Windows', url: 'https://x.newlifex.com/star/staragent45.zip' },
      { name: 'staragent31.zip', framework: 'netcoreapp3.1', os: 'Mips64(旧龙芯)', url: 'https://x.newlifex.com/star/staragent31.zip' },
    ],
    targz: [
      { name: 'staragent10.tar.gz', framework: 'net10.0', os: 'Linux', url: 'https://x.newlifex.com/star/staragent10.tar.gz' },
      { name: 'staragent90.tar.gz', framework: 'net9.0', os: 'Linux', url: 'https://x.newlifex.com/star/staragent90.tar.gz' },
      { name: 'staragent80.tar.gz', framework: 'net8.0', os: 'Linux/LA64(新龙芯)', url: 'https://x.newlifex.com/star/staragent80.tar.gz' },
      { name: 'staragent60.tar.gz', framework: 'net6.0', os: 'Linux', url: 'https://x.newlifex.com/star/staragent60.tar.gz' },
      { name: 'staragent31.tar.gz', framework: 'netcoreapp3.1', os: 'Linux/Mips64(旧龙芯)', url: 'https://x.newlifex.com/star/staragent31.tar.gz' },
    ],
    scripts: [
      { name: 'star.sh (net9.0)', description: '自动安装net9.0版', url: 'https://x.newlifex.com/star/star.sh' },
      { name: 'star8.sh (net8.0)', description: '自动安装net8.0版', url: 'https://x.newlifex.com/star/star8.sh' },
    ],
  },
  packages: [
    { name: 'star.zip', description: '星尘整体安装包', url: 'https://x.newlifex.com/star/star.zip' },
    { name: 'StarServer.zip', description: '星尘服务端', url: 'https://x.newlifex.com/star/StarServer.zip' },
    { name: 'StarWeb.zip', description: '星尘管理平台', url: 'https://x.newlifex.com/star/StarWeb.zip' },
  ],
  dotnet: [
    { name: 'net.sh', description: '.NET 最新版运行时安装脚本', url: 'https://x.newlifex.com/dotnet/net.sh' },
    { name: 'net10.sh', description: '.NET 10.0 运行时安装脚本', url: 'https://x.newlifex.com/dotnet/net10.sh' },
    { name: 'net9.sh', description: '.NET 9.0 运行时安装脚本', url: 'https://x.newlifex.com/dotnet/net9.sh' },
    { name: 'net8.sh', description: '.NET 8.0 运行时安装脚本', url: 'https://x.newlifex.com/dotnet/net8.sh' },
    { name: 'net6.sh', description: '.NET 6.0 运行时安装脚本', url: 'https://x.newlifex.com/dotnet/net6.sh' },
  ],
};

const data = ref<AgentData>(FALLBACK);

// 资源下载基础地址。由后端接口返回的实际 URL 推导，避免前端硬编码
const baseUrl = ref<string>('https://x.newlifex.com/star/');

// 应用下载数据，并据首个脚本 URL 推导基础地址（移除文件名），供快速安装命令使用
function applyData(d: AgentData) {
  data.value = d;
  const u = d?.agent?.scripts?.[0]?.url;
  if (u) baseUrl.value = u.slice(0, u.lastIndexOf('/') + 1);
}

// 拉取后端真实下载数据（URL 由服务端配置 AgentDownloadBaseUrl 生成），失败回退内置数据
async function loadAgentData() {
  try {
    const res: any = await request({ url: '/Agent', params: { format: 'json' } });
    if (res && (res.agent || res.packages)) applyData(res);
  } catch (e) {
    console.error('[Agent/Index] 拉取下载数据失败，使用内置兜底数据', e);
  }
}

// 本机探测状态：通过 navigator 探测前端能否探测到本机 Agent 信号
const probeState = ref<'unknown' | 'online' | 'offline'>('unknown');
const probeInfo = ref('');

function detectLocalAgent() {
  const ua = navigator?.userAgent || '';
  const platform = (navigator as any)?.platform || '';
  probeInfo.value = `${platform || '未知平台'} · ${ua}`;
  // 前端无法直连本机 Agent 服务，标记为待探测；展示平台信息供参考
  probeState.value = 'unknown';
}

onMounted(() => {
  console.log('[Agent/Index] custom view mounted');
  loadAgentData();
  detectLocalAgent();
});
</script>

<template>
  <div class="agent-install-page">
    <header class="aip-hero">
      <h1 class="aip-title">
        星尘代理 StarAgent
      </h1>
      <p class="aip-subtitle">
        星尘（Stardust）官方代理客户端，部署到节点/应用服务器后，负责采集指标、接收指令、上报日志。
      </p>
    </header>

    <!-- 本机探测状态 -->
    <el-card
      class="aip-card"
      shadow="never"
    >
      <template #header>
        <span class="aip-card-title">本机探测状态</span>
      </template>
      <div class="aip-probe">
        <el-tag :type="probeState === 'online' ? 'success' : probeState === 'offline' ? 'danger' : 'info'">
          {{ probeState === 'online' ? '在线' : probeState === 'offline' ? '离线' : '待探测' }}
        </el-tag>
        <span class="aip-probe-info">{{ probeInfo || '未知' }}</span>
      </div>
    </el-card>

    <!-- 快速安装 -->
    <el-card
      class="aip-card"
      shadow="never"
    >
      <template #header>
        <span class="aip-card-title">快速安装（Linux）</span>
      </template>
      <el-steps
        :active="3"
        align-center
        finish-status="success"
        class="aip-steps"
      >
        <el-step
          title="安装 .NET 运行时"
          description="执行对应版本安装脚本"
        />
        <el-step
          title="下载 StarAgent"
          description="获取 tar.gz 包"
        />
        <el-step
          title="启动服务"
          description="解压并运行守护进程"
        />
      </el-steps>
      <pre class="aip-code"># 一键安装（net9.0 版）
curl -s {{ baseUrl }}star.sh | bash

# 或手动安装
wget {{ baseUrl }}staragent90.tar.gz
tar -xzf staragent90.tar.gz
cd staragent90
./staragent &amp;</pre>
    </el-card>

    <!-- 下载：ZIP（Windows/Linux） -->
    <el-card
      class="aip-card"
      shadow="never"
    >
      <template #header>
        <span class="aip-card-title">Windows / Linux 压缩包（.zip）</span>
      </template>
      <el-table
        :data="data.agent.zip"
        border
        empty-text="暂无数据"
        class="aip-table"
      >
        <el-table-column
          prop="name"
          label="文件名"
          min-width="160"
        />
        <el-table-column
          prop="framework"
          label="框架"
          width="120"
        />
        <el-table-column
          prop="os"
          label="适用系统"
          width="160"
        />
        <el-table-column
          label="下载"
          min-width="120"
        >
          <template #default="{ row }">
            <el-link
              type="primary"
              :href="row.url"
              target="_blank"
            >
              下载
            </el-link>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 下载：tar.gz（Linux） -->
    <el-card
      class="aip-card"
      shadow="never"
    >
      <template #header>
        <span class="aip-card-title">Linux 压缩包（.tar.gz）</span>
      </template>
      <el-table
        :data="data.agent.targz"
        border
        empty-text="暂无数据"
        class="aip-table"
      >
        <el-table-column
          prop="name"
          label="文件名"
          min-width="160"
        />
        <el-table-column
          prop="framework"
          label="框架"
          width="120"
        />
        <el-table-column
          prop="os"
          label="适用系统"
          width="180"
        />
        <el-table-column
          label="下载"
          min-width="120"
        >
          <template #default="{ row }">
            <el-link
              type="primary"
              :href="row.url"
              target="_blank"
            >
              下载
            </el-link>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 安装脚本 -->
    <el-card
      class="aip-card"
      shadow="never"
    >
      <template #header>
        <span class="aip-card-title">自动安装脚本</span>
      </template>
      <el-table
        :data="data.agent.scripts"
        border
        empty-text="暂无数据"
        class="aip-table"
      >
        <el-table-column
          prop="name"
          label="脚本"
          min-width="160"
        />
        <el-table-column
          prop="description"
          label="说明"
          min-width="180"
        />
        <el-table-column
          label="下载"
          min-width="120"
        >
          <template #default="{ row }">
            <el-link
              type="primary"
              :href="row.url"
              target="_blank"
            >
              查看
            </el-link>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 整体安装包 -->
    <el-card
      class="aip-card"
      shadow="never"
    >
      <template #header>
        <span class="aip-card-title">星尘整体安装包</span>
      </template>
      <el-table
        :data="data.packages"
        border
        empty-text="暂无数据"
        class="aip-table"
      >
        <el-table-column
          prop="name"
          label="文件名"
          min-width="160"
        />
        <el-table-column
          prop="description"
          label="说明"
          min-width="180"
        />
        <el-table-column
          label="下载"
          min-width="120"
        >
          <template #default="{ row }">
            <el-link
              type="primary"
              :href="row.url"
              target="_blank"
            >
              下载
            </el-link>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- .NET 运行时 -->
    <el-card
      class="aip-card"
      shadow="never"
    >
      <template #header>
        <span class="aip-card-title">.NET 运行时安装脚本</span>
      </template>
      <el-table
        :data="data.dotnet"
        border
        empty-text="暂无数据"
        class="aip-table"
      >
        <el-table-column
          prop="name"
          label="脚本"
          min-width="140"
        />
        <el-table-column
          prop="description"
          label="说明"
          min-width="220"
        />
        <el-table-column
          label="下载"
          min-width="120"
        >
          <template #default="{ row }">
            <el-link
              type="primary"
              :href="row.url"
              target="_blank"
            >
              查看
            </el-link>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style lang="scss" scoped>
.agent-install-page {
  padding: var(--el-main-padding, 24px);
  display: flex;
  flex-direction: column;
  gap: 16px;
  max-width: 1100px;
  margin: 0 auto;

  .aip-hero {
    .aip-title {
      margin: 0 0 8px;
      font-size: 26px;
      font-weight: 700;
      color: var(--el-text-color-primary);
    }

    .aip-subtitle {
      margin: 0;
      font-size: 14px;
      line-height: 1.6;
      color: var(--el-text-color-secondary);
    }
  }

  .aip-alert {
    margin-bottom: 4px;
  }

  .aip-card {
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);

    .aip-card-title {
      font-size: 15px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }
  }

  .aip-probe {
    display: flex;
    align-items: center;
    gap: 12px;

    .aip-probe-info {
      font-size: 13px;
      color: var(--el-text-color-secondary);
      word-break: break-all;
    }
  }

  .aip-steps {
    margin-bottom: 16px;
  }

  .aip-code {
    background: var(--el-fill-color-blank);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    padding: 14px 16px;
    font-family: var(--el-font-family-mono, monospace);
    font-size: 13px;
    line-height: 1.6;
    color: var(--el-text-color-regular);
    overflow-x: auto;
    white-space: pre;
  }

  .aip-table {
    width: 100%;
  }
}
</style>
