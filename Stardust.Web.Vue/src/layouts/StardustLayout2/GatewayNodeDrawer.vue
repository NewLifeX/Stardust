<script setup lang="ts">
/**
 * GatewayNodeDrawer：网关节点新增/编辑表单抽屉（CARD-GW-02，形态 2 分组抽屉）。
 * 纯 UI：状态（open/form/busy）由父组件持有；只发 close/submit。
 * 分组：基础 / 转发与失败参数 / 备注。字段=除 Id 外全部业务字段含 Remark 与 IsHealthy（C08）。
 * 校验：Address 必填「地址不能为空」（C07，后端 ArgumentNullException 同义）+ ClusterId 必选（卡标注 *）。
 * IsHealthy 开关旁注「运行时状态，网关探测会回写，一般无需手改」（C15，运行时状态如实承载——定稿裁定 2）。
 * 集群选项闭包注入（抽屉内不发请求）；无集群时就地提示「请先创建网关集群」并禁用保存（§3.2 分支空态）。
 * 默认值（emptyNodeForm）=实体 DefaultValue：Weight 1、MaxFails 3、FailTimeout 60 秒、Enable/IsHealthy 开。
 */
import { ref, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import Btn from './Btn.vue';
import { H3 } from './deployView.styles.ts';
import { validateNodeForm, type GatewayNodeForm } from './gatewayNode.logic.ts';

type Opt = { id: number; name: string };

const props = withDefaults(
  defineProps<{
    open: boolean;
    form: GatewayNodeForm | null;
    /** 编辑模式标题「编辑节点」；新建「新增节点」 */
    editing?: boolean;
    busy?: boolean;
    failed?: boolean;
    /** 集群下拉选项（来自 fetchGateway 透出的 clusters，闭包注入不发请求） */
    clusters?: Opt[];
  }>(),
  { editing: false, busy: false, failed: false, clusters: () => [] },
);

const emit = defineEmits<{ close: []; submit: [GatewayNodeForm] }>();

/** 本地编辑副本：父组件 form 打开后深拷贝，提交时整份上抛 */
const local = ref<GatewayNodeForm | null>(null);
watch(
  () => [props.open, props.form] as const,
  ([open]) => {
    local.value = open && props.form ? { ...props.form } : null;
  },
  { immediate: true },
);

const errs = ref<{ address?: string; clusterId?: string }>({});
watch(local, () => {
  errs.value = {};
});

function onSubmit(): void {
  if (!local.value || props.busy) return;
  const e = validateNodeForm(local.value);
  errs.value = e;
  if (e.address || e.clusterId) return;
  emit('submit', { ...local.value });
}
</script>

<template>
  <Drawer
    data-spec="gateway-node-drawer"
    :open="open"
    :title="editing ? '编辑节点' : '新增节点'"
    :sub="editing ? '编辑后仅提交变更字段，其余保持不变' : '登记集群内可转发的服务地址与负载/失败参数'"
    @close="emit('close')"
  >
    <div
      v-if="failed || !local"
      data-spec="gateway-node-drawer-failed"
      class="py-[24px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      节点详情加载失败，请关闭后重试。
    </div>
    <div
      v-else
      class="flex flex-col gap-[16px] pb-[8px]"
    >
      <!-- 无集群分支空态（§3.2）：就地提示并禁用保存 -->
      <div
        v-if="!clusters.length"
        data-spec="gateway-node-form-nocluster"
        class="rounded-[6px] border border-[var(--el-border-color-light)] px-[10px] py-[8px] text-[12px] text-[var(--el-text-color-secondary)]"
      >
        请先创建网关集群——节点必须归属集群后才能被路由转发引用。
      </div>

      <!-- ===== 基础 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          基础
        </h4>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">集群 <span class="text-[var(--el-color-danger)]">*</span></span>
          <select
            v-model.number="local.clusterId"
            data-spec="gateway-node-form-cluster"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <option :value="0">请选择</option>
            <option
              v-for="c in props.clusters"
              :key="c.id"
              :value="c.id"
            >
              {{ c.name }}
            </option>
          </select>
          <span
            v-if="errs.clusterId"
            role="alert"
            data-spec="gateway-node-form-cluster-error"
            class="text-[11.5px] text-[var(--el-color-danger)]"
          >{{ errs.clusterId }}</span>
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">名称</span>
          <input
            v-model="local.name"
            data-spec="gateway-node-form-name"
            type="text"
            placeholder="缺省显示地址"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">地址 <span class="text-[var(--el-color-danger)]">*</span></span>
          <input
            v-model="local.address"
            data-spec="gateway-node-form-address"
            type="text"
            placeholder="如 http://192.168.1.100:5000"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
          <span
            v-if="errs.address"
            role="alert"
            data-spec="gateway-node-form-address-error"
            class="text-[11.5px] text-[var(--el-color-danger)]"
          >{{ errs.address }}</span>
        </label>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label
            data-spec="gateway-node-form-enable"
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
            data-spec="gateway-node-form-ishealthy"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.isHealthy"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            健康状态 IsHealthy
          </label>
          <span
            data-spec="gateway-node-form-ishealthy-note"
            class="text-[11px] text-[var(--el-text-color-secondary)]"
          >运行时状态，网关探测会回写，一般无需手改</span>
        </div>
      </section>

      <!-- ===== 转发与失败参数 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          转发与失败参数
        </h4>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">权重</span>
            <input
              v-model.number="local.weight"
              data-spec="gateway-node-form-weight"
              type="number"
              min="1"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">最大失败次数</span>
            <input
              v-model.number="local.maxFails"
              data-spec="gateway-node-form-maxfails"
              type="number"
              min="1"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">失败超时（秒）</span>
            <input
              v-model.number="local.failTimeout"
              data-spec="gateway-node-form-failtimeout"
              type="number"
              min="1"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
        </div>
        <p class="text-[11px] text-[var(--el-text-color-secondary)]">
          非正数提交后由服务端按默认值 1 / 3 / 60 修补，前端已按默认值预填。
        </p>
      </section>

      <!-- ===== 备注 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          备注
        </h4>
        <textarea
          v-model="local.remark"
          data-spec="gateway-node-form-remark"
          rows="2"
          class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        />
      </section>

      <!-- 底部操作（Drawer 无 footer slot，正文尾部放置——集群抽屉同款） -->
      <div class="flex justify-end gap-2 border-t border-[var(--el-border-color-light)] pt-[12px]">
        <Btn
          variant="ghost"
          data-spec="gateway-node-cancel"
          @click="emit('close')"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="gateway-node-save"
          :disabled="busy || !clusters.length"
          @click="onSubmit"
        >
          {{ busy ? '保存中…' : '保存' }}
        </Btn>
      </div>
    </div>
  </Drawer>
</template>
