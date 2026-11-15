<script setup lang="ts">
/**
 * GatewayCertDrawer：网关证书新增/编辑表单抽屉（CARD-GW-04，形态 2 分组抽屉）。
 * 纯 UI：状态（open/form/busy）由父组件持有；只发 close/submit。
 * 分组（全实体字段除 Id）：基础 / 证书文件 / 证书信息（折叠组）/ 备注。
 * 校验：Domain 必填「域名不能为空」（validateCertForm），内联 role="alert"。
 * 文件字段 = 服务端路径文本输入（WebApi 无上传端点，卡片回填 §6.2），title 统一「填写服务器上的证书文件路径」。
 * PfxPassword 硬条款（定稿）：② type=password；③ 编辑不回显（父组件 detailToCertForm 恒置 ''），
 *   留空=不提交=不修改（仅非空才入 payload，同 deploy RepoPassword 先例），编辑态显示说明；④ 不在任何只读处渲染。
 * 证书信息组（Issuer/Subject/NotBefore/NotAfter/Thumbprint/LastRenew）默认折叠——排查信息，展开即可看/改。
 */
import { ref, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import Btn from './Btn.vue';
import { H3 } from './deployView.styles.ts';
import { validateCertForm, type GatewayCertForm } from './gatewayCert.logic.ts';

const props = withDefaults(
  defineProps<{
    open: boolean;
    form: GatewayCertForm | null;
    /** 编辑模式标题「编辑证书」；新建「新增证书」 */
    editing?: boolean;
    busy?: boolean;
    failed?: boolean;
  }>(),
  { editing: false, busy: false, failed: false },
);

const emit = defineEmits<{ close: []; submit: [GatewayCertForm] }>();

/** 本地编辑副本：父组件 form 打开后深拷贝，提交时整份上抛 */
const local = ref<GatewayCertForm | null>(null);
watch(
  () => [props.open, props.form] as const,
  ([open]) => {
    local.value = open && props.form ? { ...props.form } : null;
  },
  { immediate: true },
);

const errs = ref<{ domain?: string }>({});
watch(local, () => {
  errs.value = {};
});

/** 证书信息折叠组：默认收起（排查信息，不抢主表单视线） */
const certInfoOpen = ref(false);

function onSubmit(): void {
  if (!local.value || props.busy) return;
  const e = validateCertForm(local.value);
  errs.value = e;
  if (e.domain) return;
  emit('submit', { ...local.value });
}
</script>

<template>
  <Drawer
    data-spec="gateway-cert-drawer"
    :open="open"
    :title="editing ? '编辑证书' : '新增证书'"
    :sub="editing ? '编辑后仅变更字段，其余保持不变' : '为网关域名配置 TLS 证书文件路径，证书由服务端按域名加载'"
    @close="emit('close')"
  >
    <div
      v-if="failed || !local"
      data-spec="gateway-cert-drawer-failed"
      class="py-[24px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      证书详情加载失败，请关闭后重试。
    </div>
    <div
      v-else
      class="flex flex-col gap-[16px] pb-[8px]"
    >
      <!-- ===== 基础 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          基础
        </h4>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">域名 Domain <span class="text-[var(--el-color-danger)]">*</span></span>
          <input
            v-model="local.domain"
            data-spec="gateway-cert-form-domain"
            type="text"
            placeholder="gw.example.com"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
          <span
            v-if="errs.domain"
            role="alert"
            data-spec="gateway-cert-form-domain-error"
            class="text-[11.5px] text-[var(--el-color-danger)]"
          >{{ errs.domain }}</span>
        </label>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label
            data-spec="gateway-cert-form-enable"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.enable"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            启用
          </label>
          <label
            data-spec="gateway-cert-form-autorenew"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.autoRenew"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            自动续期
          </label>
        </div>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">续期提供者</span>
            <input
              v-model="local.provider"
              data-spec="gateway-cert-form-provider"
              type="text"
              placeholder="留空使用默认"
              class="h-[28px] w-[160px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
            >
          </label>
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">续期提前天数</span>
            <input
              v-model.number="local.renewDays"
              data-spec="gateway-cert-form-renewdays"
              type="number"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
        </div>
      </section>

      <!-- ===== 证书文件（服务端路径；WebApi 无上传端点，卡片回填 §6.2） ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          证书文件
        </h4>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">PFX 文件</span>
          <input
            v-model="local.pfxFile"
            data-spec="gateway-cert-form-pfxfile"
            type="text"
            title="填写服务器上的证书文件路径"
            placeholder="/data/certs/gw.example.com.pfx"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">PFX 密码</span>
          <input
            v-model="local.pfxPassword"
            data-spec="gateway-cert-form-pfxpassword"
            type="password"
            autocomplete="new-password"
            placeholder="留空表示不修改"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
          <span
            v-if="editing"
            data-spec="gateway-cert-form-pfxpassword-note"
            class="text-[11px] text-[var(--el-text-color-secondary)]"
          >PFX 密码编辑不回显，留空表示不修改；仅在填写时提交，服务端加密存储。</span>
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">证书文件 Crt</span>
          <input
            v-model="local.crtFile"
            data-spec="gateway-cert-form-crtfile"
            type="text"
            title="填写服务器上的证书文件路径"
            placeholder="/data/certs/gw.example.com.crt"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">私钥文件 Key</span>
          <input
            v-model="local.keyFile"
            data-spec="gateway-cert-form-keyfile"
            type="text"
            title="填写服务器上的证书文件路径"
            placeholder="/data/certs/gw.example.com.key"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">PEM 文件</span>
          <input
            v-model="local.pemFile"
            data-spec="gateway-cert-form-pemfile"
            type="text"
            title="填写服务器上的证书文件路径"
            placeholder="/data/certs/gw.example.com.pem"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
      </section>

      <!-- ===== 证书信息（折叠组：默认收起） ===== -->
      <section class="flex flex-col gap-[10px]">
        <button
          type="button"
          data-spec="gateway-cert-form-certinfo-toggle"
          :aria-expanded="certInfoOpen"
          class="flex w-fit cursor-pointer items-center gap-[6px] text-left"
          @click="certInfoOpen = !certInfoOpen"
        >
          <span :class="H3">证书信息</span>
          <span class="text-[11px] text-[var(--el-text-color-secondary)]">{{ certInfoOpen ? '收起' : '展开' }}</span>
        </button>
        <div
          v-show="certInfoOpen"
          data-spec="gateway-cert-form-certinfo"
          class="flex flex-col gap-[10px]"
        >
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">颁发者 Issuer</span>
            <input
              v-model="local.issuer"
              data-spec="gateway-cert-form-issuer"
              type="text"
              class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
            >
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">使用者 Subject</span>
            <input
              v-model="local.subject"
              data-spec="gateway-cert-form-subject"
              type="text"
              class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
            >
          </label>
          <div class="flex flex-wrap items-center gap-[14px]">
            <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
              <span class="text-[12px] text-[var(--el-text-color-secondary)]">生效 NotBefore</span>
              <input
                v-model="local.notBefore"
                data-spec="gateway-cert-form-notbefore"
                type="text"
                placeholder="YYYY-MM-DD"
                class="h-[28px] w-[120px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px]"
              >
            </label>
            <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
              <span class="text-[12px] text-[var(--el-text-color-secondary)]">有效期至 NotAfter</span>
              <input
                v-model="local.notAfter"
                data-spec="gateway-cert-form-notafter"
                type="text"
                placeholder="YYYY-MM-DD"
                class="h-[28px] w-[120px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px]"
              >
            </label>
          </div>
          <div class="flex flex-wrap items-center gap-[14px]">
            <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
              <span class="text-[12px] text-[var(--el-text-color-secondary)]">指纹 Thumbprint</span>
              <input
                v-model="local.thumbprint"
                data-spec="gateway-cert-form-thumbprint"
                type="text"
                class="h-[28px] w-[240px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12px]"
              >
            </label>
            <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
              <span class="text-[12px] text-[var(--el-text-color-secondary)]">上次续期</span>
              <input
                v-model="local.lastRenew"
                data-spec="gateway-cert-form-lastrenew"
                type="text"
                placeholder="YYYY-MM-DD"
                class="h-[28px] w-[120px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px]"
              >
            </label>
          </div>
        </div>
      </section>

      <!-- ===== 备注 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          备注
        </h4>
        <textarea
          v-model="local.remark"
          data-spec="gateway-cert-form-remark"
          rows="2"
          class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        />
      </section>

      <!-- 底部操作（Drawer 无 footer slot，正文尾部放置——GatewayClusterDrawer 同款） -->
      <div class="flex justify-end gap-2 border-t border-[var(--el-border-color-light)] pt-[12px]">
        <Btn
          variant="ghost"
          data-spec="gateway-cert-cancel"
          @click="emit('close')"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="gateway-cert-save"
          :disabled="busy"
          @click="onSubmit"
        >
          {{ busy ? '保存中…' : '保存' }}
        </Btn>
      </div>
    </div>
  </Drawer>
</template>
