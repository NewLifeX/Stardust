<script setup lang="ts">
/**
 * GatewayClusterDrawer：网关集群新增/编辑表单抽屉（CARD-GW-01，形态 2 分组抽屉）。
 * 纯 UI：状态（open/form/busy）由父组件持有；只发 close/submit。
 * 分组：基础 / 健康检查 / 会话保持（开关开启时显示标识）/ 备注。
 * 校验：Name 必填（gatewayCluster.logic.validateClusterForm），内联 role="alert"。
 * 项目选项闭包注入（抽屉内不发请求）；选项由父组件延迟加载（fetchProjects，与路由抽屉共用）。
 * 默认值（emptyClusterForm）=实体 DefaultValue：LoadBalance RoundRobin、间隔 10 秒、超时 3000 毫秒、阈值 3/2、Enable 开。
 */
import { ref, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import Btn from './Btn.vue';
import { H3 } from './deployView.styles.ts';
import { validateClusterForm, type GatewayClusterForm } from './gatewayCluster.logic.ts';

type Opt = { id: number; name: string };

const props = withDefaults(
  defineProps<{
    open: boolean;
    form: GatewayClusterForm | null;
    /** 编辑模式标题「编辑集群」；新建「新增集群」 */
    editing?: boolean;
    busy?: boolean;
    failed?: boolean;
    /** 项目下拉选项（延迟加载，首次打开抽屉由父组件拉取） */
    projects?: Opt[];
    projectsLoading?: boolean;
  }>(),
  { editing: false, busy: false, failed: false, projects: () => [], projectsLoading: false },
);

const emit = defineEmits<{ close: []; submit: [GatewayClusterForm] }>();

/** 本地编辑副本：父组件 form 打开后深拷贝，提交时整份上抛 */
const local = ref<GatewayClusterForm | null>(null);
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

/** 负载均衡下拉（值=实体枚举串；文案沿旧壳映射 gateway.model.ts:60-74） */
const LB_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'RoundRobin', label: '轮询（RoundRobin）' },
  { value: 'LeastConnection', label: '最少连接（LeastConnection）' },
  { value: 'IPHash', label: 'IP 哈希（IPHash）' },
];

function onSubmit(): void {
  if (!local.value || props.busy) return;
  const e = validateClusterForm(local.value);
  errs.value = e;
  if (e.name) return;
  emit('submit', { ...local.value });
}
</script>

<template>
  <Drawer
    data-spec="gateway-cluster-drawer"
    :open="open"
    :title="editing ? '编辑集群' : '新增集群'"
    :sub="editing ? '编辑后仅提交变更字段，其余保持不变' : '网关节点分组与负载均衡/健康检查配置，供路由转发目标引用'"
    @close="emit('close')"
  >
    <div
      v-if="failed || !local"
      data-spec="gateway-cluster-drawer-failed"
      class="py-[24px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      集群详情加载失败，请关闭后重试。
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
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">项目</span>
          <select
            v-model.number="local.projectId"
            data-spec="gateway-cluster-form-project"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <option :value="0">未指定</option>
            <option
              v-for="p in props.projects"
              :key="p.id"
              :value="p.id"
            >
              {{ p.name }}
            </option>
          </select>
          <span
            v-if="projectsLoading"
            class="text-[11px] text-[var(--el-text-color-secondary)]"
          >项目选项加载中…</span>
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">名称 <span class="text-[var(--el-color-danger)]">*</span></span>
          <input
            v-model="local.name"
            data-spec="gateway-cluster-form-name"
            type="text"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
          <span
            v-if="errs.name"
            role="alert"
            data-spec="gateway-cluster-form-name-error"
            class="text-[11.5px] text-[var(--el-color-danger)]"
          >{{ errs.name }}</span>
        </label>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">负载均衡</span>
            <select
              v-model="local.loadBalance"
              data-spec="gateway-cluster-form-loadbalance"
              class="h-[28px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
              <option
                v-for="o in LB_OPTIONS"
                :key="o.value"
                :value="o.value"
              >
                {{ o.label }}
              </option>
            </select>
          </label>
          <label
            data-spec="gateway-cluster-form-enable"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.enable"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            启用
          </label>
        </div>
      </section>

      <!-- ===== 健康检查 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          健康检查
        </h4>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">健康检查路径 HealthPath</span>
          <input
            v-model="local.healthPath"
            data-spec="gateway-cluster-form-healthpath"
            type="text"
            placeholder="/health"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">检查间隔（秒）</span>
            <input
              v-model.number="local.healthInterval"
              data-spec="gateway-cluster-form-healthinterval"
              type="number"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">超时（毫秒）</span>
            <input
              v-model.number="local.healthTimeout"
              data-spec="gateway-cluster-form-healthtimeout"
              type="number"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
        </div>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">不健康阈值</span>
            <input
              v-model.number="local.unhealthyThreshold"
              data-spec="gateway-cluster-form-unhealthy"
              type="number"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">健康阈值</span>
            <input
              v-model.number="local.healthyThreshold"
              data-spec="gateway-cluster-form-healthy"
              type="number"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
        </div>
      </section>

      <!-- ===== 会话保持（开关开启时显示标识） ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          会话保持
        </h4>
        <label
          data-spec="gateway-cluster-form-sessionsticky"
          class="inline-flex w-fit cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        >
          <input
            v-model="local.sessionSticky"
            type="checkbox"
            class="h-[13px] w-[13px]"
          >
          会话保持 SessionSticky
        </label>
        <label
          v-if="local.sessionSticky"
          class="flex flex-col gap-[4px]"
        >
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">会话保持标识 SessionStickyName</span>
          <input
            v-model="local.sessionStickyName"
            data-spec="gateway-cluster-form-sessionstickyname"
            type="text"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
      </section>

      <!-- ===== 备注 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          备注
        </h4>
        <textarea
          v-model="local.remark"
          data-spec="gateway-cluster-form-remark"
          rows="2"
          class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        />
      </section>

      <!-- 底部操作（Drawer 无 footer slot，正文尾部放置——GatewayRouteDrawer 同款） -->
      <div class="flex justify-end gap-2 border-t border-[var(--el-border-color-light)] pt-[12px]">
        <Btn
          variant="ghost"
          data-spec="gateway-cluster-cancel"
          @click="emit('close')"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="gateway-cluster-save"
          :disabled="busy"
          @click="onSubmit"
        >
          {{ busy ? '保存中…' : '保存' }}
        </Btn>
      </div>
    </div>
  </Drawer>
</template>
