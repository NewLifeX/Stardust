<script setup lang="ts">
/**
 * AgentDeployPage：Agent 远程部署面板（独立路由 /nodes/agent-deploy，CARD-NODE-03）。
 * 数据真源：WebApi AgentDeployController —— Index 首屏预填（serverUrl/downloadUrl）、
 * TestConnection（host 仅取第一段）、Deploy（九字段 body + executeOn）。
 * 业务逻辑全部在 apps/stardust/src/views/Stardust/agentDeploy.model.ts（同目录单测），
 * 本组件只做状态接线与呈现。request 依赖注入（NodesPage 同款）：真身由 Console 薄壳传
 * cube request，story/CT 注入 mock。
 * 交互契约（Doc/用户旅程/节点代理/AgentDeploy.md C05-C10）：
 *  - C05 四项必填（hosts/userName/password/serverUrl）缺项给中文提示并拦截请求；
 *  - C06 测试连接仅测第一个地址（表单下有实时提示），结果绿/红条透出后端 message 原文；
 *  - C07 开始部署走 GuardModal 二次确认（SSH 远程安装语义），确认后 loading 到返回；
 *  - C08 汇总「部署完成：成功 N 个，失败 M 个」+ 逐行结果表 + 「查看」输出弹窗（空显示「无输出」）；
 *  - C09 executeOn=web：后端未实现，选中即提示 + 响应 message 原文透出，不硬编码吞掉；
 *  - C10 code=500/网络异常给可见错误条，不静默。
 * 样式只用 --el-* 令牌 + Tailwind 布局；确认框用 GuardModal，输出弹窗用 el-dialog（Apps.vue 先例）。
 */
import { computed, onMounted, reactive, ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Card from './Card.vue';
import PageHead from './PageHead.vue';
import GuardModal from './GuardModal/GuardModal.vue';
import {
  DOTNET_VERSIONS,
  defaultForm,
  deploy,
  deploySummary,
  fetchDeployDefaults,
  firstHost,
  hostCount,
  outputText,
  resetState,
  testConnection,
  validateDeployForm,
  type AgentReq,
  type DeployForm,
  type DeployRow,
  type ExecuteOn,
} from './agentDeploy.model';

const props = defineProps<{
  /** 依赖注入（story/CT 传 mock request；运行态走 cube request） */
  deps?: { request?: AgentReq };
}>();

function req(): AgentReq {
  return props.deps?.request ?? (request as unknown as AgentReq);
}

const inputCls =
  'w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)] disabled:opacity-50';

// ---------- 状态 ----------
const form = reactive<DeployForm>(defaultForm());
const executeOn = ref<ExecuteOn>('server');
const errors = ref<string[]>([]);
const testOutcome = ref<{ ok: boolean; message: string } | null>(null);
const testing = ref(false);
const deploying = ref(false);
const deployError = ref('');
const rows = ref<DeployRow[]>([]);
const summary = ref('');
const deployGuard = ref(false);
const outputRow = ref<DeployRow | null>(null);
const outputOpen = ref(false);

/** 首屏默认值来源标注（Index 预填成功后展示） */
const defaultsReady = ref(false);

/** 测试连接目标地址（实时跟随 hosts 第一段，C06 语义可见） */
const testTarget = computed(() => firstHost(form.hosts));
/** 目标台数（确认框文案用） */
const targetCount = computed(() => hostCount(form.hosts));

onMounted(() => {
  // 无条件 loadDefaults：运行态薄壳不传 deps，原 `if (props.deps?.request)` 让真身 Index 预填
  // 死路（5288 走查 21:1x 实证：serverUrl/downloadUrl 停在 placeholder，CT 因 story 注 mock 测不出
  // 该运行态差异——story 静态 props 与运行态同族陷阱）；story stub 三例均兜底 Index 分支，不受影响
  void loadDefaults();
});

/** C02：消费 WebApi Index 默认值（serverUrl/downloadUrl 预填） */
async function loadDefaults(): Promise<void> {
  const d = await fetchDeployDefaults(req());
  if (d.serverUrl) form.serverUrl = d.serverUrl;
  if (d.downloadUrl) form.downloadUrl = d.downloadUrl;
  defaultsReady.value = Boolean(d.serverUrl || d.downloadUrl);
}

/** C05：四项必填校验（返回是否通过） */
function validate(): boolean {
  errors.value = validateDeployForm(form);
  return errors.value.length === 0;
}

/** C06：测试连接（仅第一地址；结果透出后端 message 原文） */
async function onTest(): Promise<void> {
  if (testing.value) return;
  if (!validate()) {
    testOutcome.value = null;
    return;
  }
  testing.value = true;
  try {
    testOutcome.value = await testConnection(req(), form, executeOn.value);
  } finally {
    testing.value = false;
  }
}

/** C07：开始部署 → 校验通过才弹确认框 */
function onDeployClick(): void {
  if (deploying.value) return;
  if (!validate()) {
    testOutcome.value = null;
    return;
  }
  deployGuard.value = true;
}

function onGuardCancel(): void {
  deployGuard.value = false;
}

/** C07/C08：确认后真正发部署，汇总+结果表 */
async function onGuardConfirm(): Promise<void> {
  deployGuard.value = false;
  if (deploying.value) return;
  deploying.value = true;
  deployError.value = '';
  try {
    const r = await deploy(req(), form, executeOn.value);
    if (r.ok) {
      rows.value = r.rows;
      summary.value = deploySummary(r.rows).text;
    } else {
      // C09/C10：executeOn=web 未实现等后端 message 原文透出，不静默
      deployError.value = r.message || '部署失败';
      rows.value = [];
      summary.value = '';
    }
  } finally {
    deploying.value = false;
  }
}

/** 重置：九字段回默认 + 执行位置 server + 清结果区，再重新预填 Index 默认值 */
function onReset(): void {
  const s = resetState();
  Object.assign(form, s.form);
  executeOn.value = s.executeOn;
  errors.value = [];
  testOutcome.value = null;
  deployError.value = '';
  rows.value = [];
  summary.value = '';
  void loadDefaults();
}

/** C08：查看单台部署输出（空输出显示「无输出」） */
function showOutput(row: DeployRow): void {
  outputRow.value = row;
  outputOpen.value = true;
}
</script>

<template>
  <div data-spec="agent-deploy-page">
    <PageHead
      eyebrow="节点 · 远程部署"
      title="Agent 远程部署"
      sub="通过 SSH 向目标主机批量安装/升级 StarAgent；仅支持 StarServer 执行，本页不保存密码"
    />

    <!-- 部署表单 -->
    <Card
      data-spec="agent-deploy-form-card"
      class="mb-4"
    >
      <div class="grid grid-cols-1 gap-[12px] md:grid-cols-2">
        <!-- 目标主机（占满两列） -->
        <label class="block md:col-span-2">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            目标主机 <span class="text-[var(--el-color-danger)]">*</span>
          </div>
          <textarea
            v-model="form.hosts"
            data-spec="agent-deploy-hosts"
            :class="inputCls"
            rows="3"
            placeholder="多台用逗号、分号或换行分隔，如 10.0.0.12,10.0.0.13"
          />
          <div
            data-spec="agent-deploy-firsthost-hint"
            class="mt-[4px] text-[11.5px] text-[var(--el-text-color-secondary)]"
          >
            测试连接仅测第一个地址：{{ testTarget || '（填写后显示）' }}；部署将逐台执行
          </div>
        </label>

        <!-- SSH 端口 -->
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            SSH 端口
          </div>
          <input
            v-model.number="form.port"
            data-spec="agent-deploy-port"
            :class="inputCls"
            type="number"
            min="1"
            max="65535"
          >
        </label>

        <!-- 系统类型 -->
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            系统类型
          </div>
          <select
            v-model="form.osType"
            data-spec="agent-deploy-ostype"
            :class="inputCls"
          >
            <option value="Linux">
              Linux
            </option>
            <option value="Windows">
              Windows
            </option>
          </select>
        </label>

        <!-- 用户名 -->
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            用户名 <span class="text-[var(--el-color-danger)]">*</span>
          </div>
          <input
            v-model="form.userName"
            data-spec="agent-deploy-username"
            :class="inputCls"
            placeholder="root"
          >
        </label>

        <!-- 密码 -->
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            密码 <span class="text-[var(--el-color-danger)]">*</span>
          </div>
          <input
            v-model="form.password"
            data-spec="agent-deploy-password"
            :class="inputCls"
            type="password"
            placeholder="SSH 登录密码"
          >
        </label>

        <!-- 服务器地址 -->
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            服务器地址 <span class="text-[var(--el-color-danger)]">*</span>
            <span
              v-if="defaultsReady"
              class="ml-[6px] font-normal text-[11px] text-[var(--el-text-color-secondary)]"
            >(已从服务端预填)</span>
          </div>
          <input
            v-model="form.serverUrl"
            data-spec="agent-deploy-serverurl"
            :class="inputCls"
            placeholder="http://10.0.0.2:6600"
          >
        </label>

        <!-- 下载地址 -->
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            下载地址
          </div>
          <input
            v-model="form.downloadUrl"
            data-spec="agent-deploy-downloadurl"
            :class="inputCls"
            placeholder="http://x.newlifex.com/star/"
          >
        </label>

        <!-- .NET 运行时版本 -->
        <label class="block">
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            .NET 运行时版本
          </div>
          <select
            v-model.number="form.dotnetVersion"
            data-spec="agent-deploy-dotnet"
            :class="inputCls"
          >
            <option
              v-for="v in DOTNET_VERSIONS"
              :key="v.value"
              :value="v.value"
            >
              {{ v.label }}
            </option>
          </select>
        </label>

        <!-- 执行位置 -->
        <div>
          <div class="mb-[5px] text-[12px] font-medium text-[var(--el-text-color-regular)]">
            执行位置
          </div>
          <div
            data-spec="agent-deploy-executeon"
            class="flex h-[30px] items-center gap-[14px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <label class="flex cursor-pointer items-center gap-[5px]">
              <input
                v-model="executeOn"
                data-spec="agent-deploy-executeon-server"
                type="radio"
                value="server"
              >
              StarServer 执行（推荐）
            </label>
            <label class="flex cursor-pointer items-center gap-[5px]">
              <input
                v-model="executeOn"
                data-spec="agent-deploy-executeon-web"
                type="radio"
                value="web"
              >
              StarWeb 本地执行
            </label>
          </div>
        </div>
      </div>

      <!-- C09：web 未实现前置提示 -->
      <div
        v-if="executeOn === 'web'"
        data-spec="agent-deploy-web-hint"
        class="mt-[12px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-warning)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-warning)_12%,transparent)] px-[12px] py-[8px] text-[12px] text-[var(--el-color-warning)]"
      >
        StarWeb 本地执行功能暂未实现，请选择 StarServer 执行。
      </div>

      <!-- C05：校验错误条 -->
      <div
        v-if="errors.length"
        data-spec="agent-deploy-errors"
        class="mt-[12px] rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_12%,transparent)] px-[12px] py-[8px] text-[12px] text-[var(--el-color-danger)]"
      >
        <div
          v-for="e in errors"
          :key="e"
        >
          {{ e }}
        </div>
      </div>

      <!-- 动作区 -->
      <div class="mt-[16px] flex items-center gap-[10px]">
        <Btn
          variant="default"
          size="md"
          data-spec="agent-deploy-test"
          :disabled="testing || deploying"
          @click="onTest"
        >
          <template #icon>
            <span
              v-if="testing"
              class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-current border-t-transparent align-[-2px]"
              data-spec="agent-deploy-test-spin"
            />
          </template>
          {{ testing ? '测试中…' : '测试连接' }}
        </Btn>
        <Btn
          variant="primary"
          size="md"
          data-spec="agent-deploy-start"
          :disabled="testing || deploying"
          @click="onDeployClick"
        >
          <template #icon>
            <span
              v-if="deploying"
              class="mr-[6px] inline-block h-[13px] w-[13px] animate-spin rounded-full border-2 border-current border-t-transparent align-[-2px]"
              data-spec="agent-deploy-start-spin"
            />
          </template>
          {{ deploying ? '部署中…' : '开始部署' }}
        </Btn>
        <Btn
          variant="ghost"
          size="md"
          data-spec="agent-deploy-reset"
          :disabled="testing || deploying"
          @click="onReset"
        >
          重置
        </Btn>
      </div>
    </Card>

    <!-- C06：测试连接结果条（后端 message 原文透出） -->
    <div
      v-if="testOutcome"
      data-spec="agent-deploy-test-result"
      class="mb-4 rounded-[8px] border px-[12px] py-[8px] text-[12px]"
      :class="testOutcome.ok
        ? 'border-[color-mix(in_oklab,var(--el-color-success)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-success)_12%,transparent)] text-[var(--el-color-success)]'
        : 'border-[color-mix(in_oklab,var(--el-color-danger)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_12%,transparent)] text-[var(--el-color-danger)]'"
    >
      {{ testOutcome.message }}
    </div>

    <!-- C09/C10：部署错误条（code=500/网络异常，不静默） -->
    <div
      v-if="deployError"
      data-spec="agent-deploy-error"
      class="mb-4 rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-danger)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-danger)_12%,transparent)] px-[12px] py-[8px] text-[12px] text-[var(--el-color-danger)]"
    >
      {{ deployError }}
    </div>

    <!-- C07：部署二次确认（GuardModal，SSH 远程安装语义） -->
    <GuardModal
      v-if="deployGuard"
      label="确定要开始部署吗？"
      :detail="`将通过 SSH 向 ${targetCount} 台目标主机远程安装 StarAgent。执行位置：${executeOn === 'web' ? 'StarWeb 本地（暂未实现）' : 'StarServer 执行'}。`"
      data-spec="agent-deploy-confirm"
      @cancel="onGuardCancel"
      @confirm="onGuardConfirm"
    />

    <!-- C08：部署结果 -->
    <Card
      v-if="rows.length"
      data-spec="agent-deploy-result-card"
      class="mb-4"
    >
      <div
        data-spec="agent-deploy-summary"
        class="mb-[10px] text-[13px] font-medium text-[var(--el-text-color-primary)]"
      >
        {{ summary }}
      </div>
      <div class="overflow-hidden rounded-[8px] border border-[var(--el-border-color-light)]">
        <table class="w-full text-[12.5px]">
          <thead>
            <tr class="bg-[var(--el-fill-color-light)] text-left text-[var(--el-text-color-regular)]">
              <th class="px-[12px] py-[7px] font-medium">
                主机
              </th>
              <th class="px-[12px] py-[7px] font-medium">
                状态
              </th>
              <th class="px-[12px] py-[7px] font-medium">
                消息
              </th>
              <th class="w-[70px] px-[12px] py-[7px] font-medium">
                输出
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in rows"
              :key="r.host"
              data-spec="agent-deploy-result-row"
              class="border-t border-[var(--el-border-color-lighter)] text-[var(--el-text-color-primary)]"
            >
              <td
                class="break-all px-[12px] py-[7px]"
                :data-spec="`agent-deploy-result-host-${r.host}`"
              >
                {{ r.host }}
              </td>
              <td class="px-[12px] py-[7px]">
                <span :class="r.success ? 'text-[var(--el-color-success)]' : 'text-[var(--el-color-danger)]'">{{ r.success ? '成功' : '失败' }}</span>
              </td>
              <td class="break-all px-[12px] py-[7px] text-[var(--el-text-color-regular)]">
                {{ r.message || '—' }}
              </td>
              <td class="px-[12px] py-[7px]">
                <Btn
                  variant="ghost"
                  size="sm"
                  :data-spec="`agent-deploy-output-btn-${r.host}`"
                  @click="showOutput(r)"
                >
                  查看
                </Btn>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </Card>

    <!-- C08：部署输出弹窗（Apps.vue el-dialog 先例；空输出「无输出」） -->
    <el-dialog
      v-model="outputOpen"
      data-spec="agent-deploy-output-modal"
      :title="`部署输出 - ${outputRow?.host ?? ''}`"
      width="640px"
      append-to-body
    >
      <pre
        data-spec="agent-deploy-output-text"
        class="max-h-[420px] overflow-auto whitespace-pre-wrap break-all rounded-[8px] bg-[var(--el-fill-color-light)] p-[12px] font-mono text-[12px] leading-[18px] text-[var(--el-text-color-regular)]"
      >{{ outputText(outputRow?.output ?? '') }}</pre>
    </el-dialog>
  </div>
</template>
