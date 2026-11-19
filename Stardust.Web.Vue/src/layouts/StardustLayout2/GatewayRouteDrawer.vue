<script setup lang="ts">
/**
 * GatewayRouteDrawer：网关路由新增/编辑表单抽屉（CARD-GW-03，形态 2 分组抽屉）。
 * 纯 UI：状态（open/form/busy）由父组件持有；只发 close/submit。
 * 分组：基础 / 匹配 / 转发 / 静态文件（IsStaticRoute 关时置灰）/ 备注。
 * 校验：Name 必填、IsStaticRoute→StaticRoot 必填（gatewayRoute.logic.validateRouteForm），内联 role="alert"。
 * 集群/项目选项闭包注入（抽屉内不发请求）；项目选项由父组件延迟加载（fetchProjects）。
 */
import { computed, ref, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import Btn from './Btn.vue';
import { H3 } from './deployView.styles.ts';
import { validateRouteForm, type GatewayRouteForm } from './gatewayRoute.logic.ts';

type Opt = { id: number; name: string };

const props = withDefaults(
  defineProps<{
    open: boolean;
    form: GatewayRouteForm | null;
    /** 编辑模式标题「编辑路由」；新建「新增路由」 */
    editing?: boolean;
    busy?: boolean;
    failed?: boolean;
    /** 集群下拉选项（Id→Name），来自 fetchGateway 透出的 clusters */
    clusters?: Opt[];
    /** 项目下拉选项（延迟加载，首次打开抽屉由父组件拉取） */
    projects?: Opt[];
    projectsLoading?: boolean;
  }>(),
  { editing: false, busy: false, failed: false, clusters: () => [], projects: () => [], projectsLoading: false },
);

const emit = defineEmits<{ close: []; submit: [GatewayRouteForm] }>();

/** 本地编辑副本：父组件 form 打开后深拷贝，提交时整份上抛 */
const local = ref<GatewayRouteForm | null>(null);
watch(
  () => [props.open, props.form] as const,
  ([open]) => {
    local.value = open && props.form ? { ...props.form } : null;
  },
  { immediate: true },
);

const errs = ref<{ name?: string; staticRoot?: string }>({});
watch(local, () => {
  errs.value = {};
});

const clusterOptions = computed<Opt[]>(() => props.clusters);
const projectOptions = computed<Opt[]>(() => props.projects);

function onToggleStatic(v: boolean): void {
  if (!local.value) return;
  local.value.isStaticRoute = v;
  if (!v) errs.value.staticRoot = undefined;
}

function onSubmit(): void {
  if (!local.value || props.busy) return;
  const e = validateRouteForm(local.value);
  errs.value = e;
  if (e.name || e.staticRoot) return;
  emit('submit', { ...local.value });
}
</script>

<template>
  <Drawer
    data-spec="gateway-route-drawer"
    :open="open"
    :title="editing ? '编辑路由' : '新增路由'"
    :sub="editing ? '编辑后仅提交变更字段，其余保持不变' : '域名/路径匹配后转发到目标集群；静态文件路由不走反向代理'"
    @close="emit('close')"
  >
    <div
      v-if="failed || !local"
      data-spec="gateway-route-drawer-failed"
      class="py-[24px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      路由详情加载失败，请关闭后重试。
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
            data-spec="gateway-route-form-project"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <option :value="0">未指定</option>
            <option
              v-for="p in projectOptions"
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
            data-spec="gateway-route-form-name"
            type="text"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
          <span
            v-if="errs.name"
            role="alert"
            data-spec="gateway-route-form-name-error"
            class="text-[11.5px] text-[var(--el-color-danger)]"
          >{{ errs.name }}</span>
        </label>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label
            data-spec="gateway-route-form-enable"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.enable"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            启用
          </label>
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">优先级</span>
            <input
              v-model.number="local.priority"
              data-spec="gateway-route-form-priority"
              type="number"
              class="h-[28px] w-[90px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
          </label>
          <label class="inline-flex items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">目标集群 <span class="text-[var(--el-color-danger)]">*</span></span>
            <select
              v-model.number="local.clusterId"
              data-spec="gateway-route-form-cluster"
              class="h-[28px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] text-[12.5px]"
            >
              <option :value="0">未指定</option>
              <option
                v-for="c in clusterOptions"
                :key="c.id"
                :value="c.id"
              >
                {{ c.name }}
              </option>
            </select>
          </label>
        </div>
      </section>

      <!-- ===== 匹配 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          匹配
        </h4>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">域名（多值逗号分隔）</span>
          <input
            v-model="local.domain"
            data-spec="gateway-route-form-domain"
            type="text"
            placeholder="example.com,*.example.com"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">路径</span>
          <input
            v-model="local.path"
            data-spec="gateway-route-form-path"
            type="text"
            placeholder="/api/**"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">HTTP 方法（空=全部）</span>
          <input
            v-model="local.methods"
            data-spec="gateway-route-form-methods"
            type="text"
            placeholder="GET,POST"
            class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
          >
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">请求头匹配 Headers</span>
          <textarea
            v-model="local.headers"
            data-spec="gateway-route-form-headers"
            rows="2"
            class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] font-mono text-[12px] text-[var(--el-text-color-primary)]"
          />
        </label>
      </section>

      <!-- ===== 转发 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          转发
        </h4>
        <label
          data-spec="gateway-route-form-stripprefix"
          class="inline-flex w-fit cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        >
          <input
            v-model="local.stripPrefix"
            type="checkbox"
            class="h-[13px] w-[13px]"
          >
          去除前缀 StripPrefix
        </label>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">添加请求头 AddHeaders</span>
          <textarea
            v-model="local.addHeaders"
            data-spec="gateway-route-form-addheaders"
            rows="2"
            class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] font-mono text-[12px] text-[var(--el-text-color-primary)]"
          />
        </label>
        <label
          data-spec="gateway-route-form-websocket"
          class="inline-flex w-fit cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        >
          <input
            v-model="local.webSocket"
            type="checkbox"
            class="h-[13px] w-[13px]"
          >
          WebSocket
        </label>
      </section>

      <!-- ===== 静态文件（IsStaticRoute 关时置灰） ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          静态文件
        </h4>
        <label
          data-spec="gateway-route-form-isstatic"
          class="inline-flex w-fit cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        >
          <input
            type="checkbox"
            class="h-[13px] w-[13px]"
            :checked="local.isStaticRoute"
            @change="onToggleStatic(($event.target as HTMLInputElement).checked)"
          >
          静态文件路由 IsStaticRoute
        </label>
        <div
          class="flex flex-col gap-[10px]"
          :class="local.isStaticRoute ? '' : 'pointer-events-none opacity-50'"
        >
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">静态文件根目录 <span
              v-if="local.isStaticRoute"
              class="text-[var(--el-color-danger)]"
            >*</span></span>
            <input
              v-model="local.staticRoot"
              data-spec="gateway-route-form-staticroot"
              type="text"
              :disabled="!local.isStaticRoute"
              class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
            >
            <span
              v-if="errs.staticRoot"
              role="alert"
              data-spec="gateway-route-form-staticroot-error"
              class="text-[11.5px] text-[var(--el-color-danger)]"
            >{{ errs.staticRoot }}</span>
          </label>
          <label class="flex flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">默认首页 IndexFile</span>
            <input
              v-model="local.indexFile"
              data-spec="gateway-route-form-indexfile"
              type="text"
              class="h-[30px] rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] font-mono text-[12.5px] text-[var(--el-text-color-primary)]"
            >
          </label>
          <label
            data-spec="gateway-route-form-directorybrowse"
            class="inline-flex w-fit cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.directoryBrowse"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            目录浏览
          </label>
          <label
            data-spec="gateway-route-form-spafallback"
            class="inline-flex w-fit cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.spaFallback"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            SPA 回退
          </label>
        </div>
      </section>

      <!-- ===== 备注 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          备注
        </h4>
        <textarea
          v-model="local.remark"
          data-spec="gateway-route-form-remark"
          rows="2"
          class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        />
      </section>

      <!-- 底部操作（Drawer 无 footer slot，正文尾部放置——试点 PipelineConfigDrawer 同款） -->
      <div class="flex justify-end gap-2 border-t border-[var(--el-border-color-light)] pt-[12px]">
        <Btn
          variant="ghost"
          data-spec="gateway-route-cancel"
          @click="emit('close')"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="gateway-route-save"
          :disabled="busy"
          @click="onSubmit"
        >
          {{ busy ? '保存中…' : '保存' }}
        </Btn>
      </div>
    </div>
  </Drawer>
</template>
