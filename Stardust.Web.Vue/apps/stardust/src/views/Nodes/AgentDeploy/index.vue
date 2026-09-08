<script setup lang="ts">
/**
 * Nodes/远程部署（AgentDeploy）操作面板
 * 后端 AgentDeployController 是纯操作型控制器（仅 POST Deploy/TestConnection，无 GET 列表），
 * 故不走通用 CRUD，而是自定义操作面板：填写 SSH 信息 → 测试连接 / 执行远程部署。
 */
import { reactive, ref } from 'vue';
import { ElMessage } from 'element-plus';

const model = reactive({
  Hosts: '',
  Port: 22,
  UserName: '',
  Password: '',
  OSType: 'Linux',
  ServerUrl: '',
  DownloadUrl: '',
  DotnetVersion: '9',
});

const DOTNET_VERSIONS = [
  { value: '9', label: '9.0' },
  { value: '8', label: '8.0' },
  { value: '10', label: '10.0' },
];

const executeOn = ref<'server' | 'web'>('server');
const testing = ref(false);
const deploying = ref(false);
const testResult = ref<string>('');
const deployResult = ref<string>('');
const deployRows = ref<{ host: string; success: boolean; message: string; output: string }[]>([]);

async function postJson(url: string, body: unknown) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    credentials: 'include',
  });
  return await res.json();
}

async function onTest() {
  testing.value = true;
  testResult.value = '';
  try {
    const r: any = await postJson(
      `/api/Nodes/AgentDeploy/TestConnection?host=${encodeURIComponent(model.Hosts)}&port=${model.Port}&userName=${encodeURIComponent(model.UserName)}&password=${encodeURIComponent(model.Password)}&executeOn=${executeOn.value}`,
      null,
    );
    if (r && r.code === 0) {
      testResult.value = '连接成功：' + JSON.stringify(r.data ?? '');
      ElMessage.success('连接测试成功');
    } else {
      testResult.value = '失败：' + (r?.message ?? '未知错误');
      ElMessage.error('连接测试失败');
    }
  } catch (e: any) {
    testResult.value = '异常：' + e.message;
    ElMessage.error('连接测试异常');
  } finally {
    testing.value = false;
  }
}

async function onDeploy() {
  deploying.value = true;
  deployResult.value = '';
  deployRows.value = [];
  try {
    const r: any = await postJson(`/api/Nodes/AgentDeploy/Deploy?executeOn=${executeOn.value}`, { ...model });
    if (r && r.code === 0) {
      const rows: any[] = Array.isArray(r.data) ? r.data : [];
      deployRows.value = rows.map((x) => ({
        host: x.host ?? '',
        success: !!x.success,
        message: x.message ?? '',
        output: x.output ?? '',
      }));
      const ok = deployRows.value.filter((x) => x.success).length;
      const fail = deployRows.value.length - ok;
      deployResult.value = `部署完成：成功 ${ok} 个，失败 ${fail} 个`;
      ElMessage.success('部署完成');
    } else {
      deployResult.value = '失败：' + (r?.message ?? '未知错误');
      ElMessage.error('部署失败');
    }
  } catch (e: any) {
    deployResult.value = '异常：' + e.message;
    ElMessage.error('部署异常');
  } finally {
    deploying.value = false;
  }
}

function handleReset() {
  model.Hosts = '';
  model.Port = 22;
  model.UserName = '';
  model.Password = '';
  model.OSType = 'Linux';
  model.ServerUrl = '';
  model.DownloadUrl = '';
  model.DotnetVersion = '9';
  executeOn.value = 'server';
  testResult.value = '';
  deployResult.value = '';
  deployRows.value = [];
}

const outputVisible = ref(false);
const currentOutput = ref('');
function showOutput(row: { host: string; output: string }) {
  currentOutput.value = row.output || '无输出';
  outputVisible.value = true;
}
</script>

<template>
  <div class="agent-deploy">
    <h2 class="ad-title">
      StarAgent 远程部署
    </h2>
    <el-card
      class="ad-card"
      shadow="never"
    >
      <el-form
        :model="model"
        label-width="110px"
        class="ad-form"
      >
        <el-form-item
          label="目标主机"
          required
        >
          <el-input
            v-model="model.Hosts"
            placeholder="支持 IP 或网段(CIDR)，如 192.168.1.0/24"
          />
        </el-form-item>
        <el-form-item label="SSH 端口">
          <el-input-number
            v-model="model.Port"
            :min="1"
            :max="65535"
          />
        </el-form-item>
        <el-form-item
          label="用户名"
          required
        >
          <el-input
            v-model="model.UserName"
            placeholder="SSH 用户名"
          />
        </el-form-item>
        <el-form-item
          label="密码"
          required
        >
          <el-input
            v-model="model.Password"
            type="password"
            show-password
            placeholder="SSH 密码"
          />
        </el-form-item>
        <el-form-item label="操作系统">
          <el-select
            v-model="model.OSType"
            style="width: 160px"
          >
            <el-option
              label="Linux"
              value="Linux"
            />
            <el-option
              label="Windows"
              value="Windows"
            />
          </el-select>
        </el-form-item>
        <el-form-item
          label="服务器地址"
          required
        >
          <el-input
            v-model="model.ServerUrl"
            placeholder="StarServer 地址（安装后指向）"
          />
        </el-form-item>
        <el-form-item label="下载地址">
          <el-input
            v-model="model.DownloadUrl"
            placeholder="默认 http://x.newlifex.com/star/"
          />
        </el-form-item>
        <el-form-item label="dotnet 版本">
          <el-select
            v-model="model.DotnetVersion"
            style="width: 160px"
          >
            <el-option
              v-for="v in DOTNET_VERSIONS"
              :key="v.value"
              :label="v.label"
              :value="v.value"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="执行位置">
          <el-radio-group v-model="executeOn">
            <el-radio value="server">
              StarServer
            </el-radio>
            <el-radio value="web">
              StarWeb(本地)
            </el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item>
          <el-button
            type="primary"
            :loading="testing"
            @click="onTest"
          >
            测试连接
          </el-button>
          <el-button
            type="success"
            :loading="deploying"
            @click="onDeploy"
          >
            开始部署
          </el-button>
          <el-button @click="handleReset">
            重置
          </el-button>
        </el-form-item>
      </el-form>

      <el-alert
        v-if="testResult"
        :title="'连接测试结果'"
        type="info"
        :closable="false"
        class="ad-result"
      >
        <pre>{{ testResult }}</pre>
      </el-alert>
      <el-alert
        v-if="deployResult"
        :title="'部署结果'"
        type="success"
        :closable="false"
        class="ad-result"
      >
        {{ deployResult }}
      </el-alert>
      <el-table
        v-if="deployRows.length"
        :data="deployRows"
        border
        stripe
        class="ad-result"
        empty-text="暂无部署结果"
      >
        <el-table-column
          prop="host"
          label="主机"
          min-width="140"
        />
        <el-table-column
          label="状态"
          width="90"
          align="center"
        >
          <template #default="{ row }">
            <el-tag :type="row.success ? 'success' : 'danger'">
              {{ row.success ? '成功' : '失败' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column
          prop="message"
          label="消息"
          min-width="160"
          show-overflow-tooltip
        />
        <el-table-column
          label="详细输出"
          width="100"
          align="center"
        >
          <template #default="{ row }">
            <el-button
              size="small"
              type="info"
              plain
              @click="showOutput(row)"
            >
              查看
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-dialog
        v-model="outputVisible"
        title="部署输出"
        width="70%"
      >
        <pre class="ad-output">{{ currentOutput }}</pre>
        <template #footer>
          <el-button @click="outputVisible = false">
            关闭
          </el-button>
        </template>
      </el-dialog>
    </el-card>
  </div>
</template>

<style lang="scss" scoped>
.agent-deploy {
  padding: 16px;
  .ad-title {
    margin: 0 0 16px;
    font-size: 18px;
    color: var(--el-text-color-primary);
  }
  .ad-card {
    max-width: 720px;
  }
  .ad-result pre {
    white-space: pre-wrap;
    word-break: break-all;
    margin: 0;
  }

  .ad-output {
    max-height: 400px;
    overflow: auto;
    white-space: pre-wrap;
    word-break: break-all;
    margin: 0;
  }
}
</style>
