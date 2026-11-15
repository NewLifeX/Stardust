<script setup lang="ts">
/**
 * MySqlNodeDrawer：MySQL 节点编辑表单抽屉（CARD-MW-02，形态 2 分组抽屉，仅编辑——控制器 RemoveCreateField 无新增）。
 * 纯 UI：状态（open/form/busy/failed）由父组件持有；只发 close/submit。
 * 分组（实体可编辑字段除 Id/审计列）：基础 / 监控参数 / 备注。
 * 校验：Name 必填「名称不能为空」（validateMySqlNodeForm；实体 Valid 无强校验，前端最小必填），内联 role="alert"。
 * Password：type=password 掩码回显（Detail 有值则显示占位，同 Cube 默认表单），保存随 PUT 全量提交；
 *   与证书 PfxPassword「留空=不修改」特例不同——节点实体无该契约（如实差异，头注释）。
 * 项目=数字手输（Lov 下拉待 PLT-01 接入，记差异）。
 */
import { ref, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import Btn from './Btn.vue';
import { H3 } from './deployView.styles.ts';
import { validateMySqlNodeForm, type MySqlNodeForm } from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    open: boolean;
    form: MySqlNodeForm | null;
    busy?: boolean;
    failed?: boolean;
  }>(),
  { busy: false, failed: false },
);

const emit = defineEmits<{ close: []; submit: [MySqlNodeForm] }>();

/** 本地编辑副本：父组件 form 打开后浅拷贝（字段全为原始类型），提交时整份上抛 */
const local = ref<MySqlNodeForm | null>(null);
watch(
  () => [props.open, props.form] as const,
  ([open]) => {
    local.value = open && props.form ? { ...props.form } : null;
  },
  { immediate: true },
);

const errs = ref<{ name?: string }>({});
watch(local, () => {
  errs.value = {};
});

const inputCls = 'h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]';

function onSubmit(): void {
  if (!local.value || props.busy) return;
  const e = validateMySqlNodeForm(local.value);
  errs.value = e;
  if (e.name) return;
  emit('submit', { ...local.value });
}
</script>

<template>
  <Drawer
    data-spec="mw-mysql-node-drawer"
    :open="open"
    title="编辑节点"
    sub="修改节点连接与监控参数，保存后立即生效"
    @close="emit('close')"
  >
    <div
      v-if="failed || !local"
      data-spec="mw-mysql-node-drawer-failed"
      class="py-[24px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      节点详情加载失败，请关闭后重试。
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
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">名称 Name <span class="text-[var(--el-color-danger)]">*</span></span>
          <input
            v-model="local.name"
            data-spec="mw-mysql-node-form-name"
            type="text"
            placeholder="主库"
            :class="[inputCls, 'font-mono']"
          >
          <span
            v-if="errs.name"
            role="alert"
            data-spec="mw-mysql-node-form-name-error"
            class="text-[11.5px] text-[var(--el-color-danger)]"
          >{{ errs.name }}</span>
        </label>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label
            data-spec="mw-mysql-node-form-enable"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.enable"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            启用该节点
          </label>
        </div>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">分类 Category</span>
          <input
            v-model="local.category"
            data-spec="mw-mysql-node-form-category"
            type="text"
            placeholder="MySQL"
            :class="inputCls"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">服务器地址 Server</span>
          <input
            v-model="local.server"
            data-spec="mw-mysql-node-form-server"
            type="text"
            placeholder="127.0.0.1"
            :class="[inputCls, 'font-mono']"
          >
        </label>
        <div class="flex flex-wrap gap-[14px]">
          <label class="flex min-w-[120px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">端口 Port</span>
            <input
              v-model.number="local.port"
              data-spec="mw-mysql-node-form-port"
              type="number"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">项目编号（下拉待接入）</span>
            <input
              v-model.number="local.projectId"
              data-spec="mw-mysql-node-form-project"
              type="number"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">版本 Version</span>
            <input
              v-model="local.version"
              data-spec="mw-mysql-node-form-version"
              type="text"
              placeholder="8.0.36"
              :class="inputCls"
            >
          </label>
        </div>
        <div class="flex flex-wrap gap-[14px]">
          <label class="flex min-w-[140px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">用户名 UserName</span>
            <input
              v-model="local.userName"
              data-spec="mw-mysql-node-form-user"
              type="text"
              autocomplete="off"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[140px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">密码 Password</span>
            <input
              v-model="local.password"
              data-spec="mw-mysql-node-form-password"
              type="password"
              autocomplete="new-password"
              :class="inputCls"
            >
            <span class="text-[11px] text-[var(--el-text-color-secondary)]">掩码显示；保存时随表单提交</span>
          </label>
          <label class="flex min-w-[140px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">数据库 DatabaseName</span>
            <input
              v-model="local.databaseName"
              data-spec="mw-mysql-node-form-database"
              type="text"
              :class="inputCls"
            >
          </label>
        </div>
      </section>

      <!-- ===== 监控参数 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          监控参数
        </h4>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">告警 WebHook</span>
          <input
            v-model="local.webHook"
            data-spec="mw-mysql-node-form-webhook"
            type="text"
            placeholder="https://hooks.example.com/xxx"
            :class="[inputCls, 'font-mono']"
          >
        </label>
        <div class="flex flex-wrap gap-[14px]">
          <label class="flex min-w-[120px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">连接数阈值</span>
            <input
              v-model.number="local.alarmConnections"
              data-spec="mw-mysql-node-form-alarm-connections"
              type="number"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">QPS 阈值</span>
            <input
              v-model.number="local.alarmQps"
              data-spec="mw-mysql-node-form-alarm-qps"
              type="number"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">慢查询阈值</span>
            <input
              v-model.number="local.alarmSlowQuery"
              data-spec="mw-mysql-node-form-alarm-slow"
              type="number"
              :class="inputCls"
            >
          </label>
        </div>
      </section>

      <!-- ===== 备注 ===== -->
      <section class="flex flex-col gap-[4px]">
        <h4 :class="H3">
          备注
        </h4>
        <textarea
          v-model="local.remark"
          data-spec="mw-mysql-node-form-remark"
          rows="2"
          class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        />
      </section>

      <!-- 底部操作（Drawer 无 footer slot，正文尾部放置——GatewayCertDrawer 同款） -->
      <div class="flex justify-end gap-2 border-t border-[var(--el-border-color-light)] pt-[12px]">
        <Btn
          variant="ghost"
          data-spec="mw-mysql-node-cancel"
          @click="emit('close')"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="mw-mysql-node-save"
          :disabled="busy"
          @click="onSubmit"
        >
          {{ busy ? '保存中…' : '保存' }}
        </Btn>
      </div>
    </div>
  </Drawer>
</template>
