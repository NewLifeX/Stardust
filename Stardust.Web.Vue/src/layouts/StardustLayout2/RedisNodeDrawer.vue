<script setup lang="ts">
/**
 * RedisNodeDrawer：Redis 节点新增/编辑表单抽屉（CARD-MW-05，裁定④ CRUD 全量——标准实体接口就绪）。
 * 纯 UI：状态（mode/open/form/busy/failed/deleting）由父组件持有；只发 close/submit/delete。
 * 分组（实体可编辑字段除 Id/审计列）：基础 / 监控与告警 / 备注。
 * 校验：Name 必填「名称不能为空」（validateRedisNodeForm；实体 Valid 无强校验，前端最小必填），内联 role="alert"。
 * Password：type=password 掩码回显（Detail 有值则显示占位，同 Cube 默认表单），保存随提交全量上送；
 *   与证书 PfxPassword「留空=不修改」特例不同——节点实体无该契约（如实差异，头注释）。
 * 删除（裁定④）：仅编辑模式显示「删除」——容器弹 GuardModal 确认后调 deleteRedisNode；
 *   新增模式无删除（无 Id 可删）。删除入口放抽屉内（行内已 4 按钮，避免溢出；§10 记裁量）。
 * 分类：文本手输（GetCategoryList 无 WebApi 端点，列表聚合仅够筛选；抽屉自由输入=旧页同款）。
 */
import { ref, watch } from 'vue';
import Drawer from './Drawer/Drawer.vue';
import Btn from './Btn.vue';
import { H3 } from './deployView.styles.ts';
import { validateRedisNodeForm, type RedisNodeForm } from './middleware.logic.ts';

const props = withDefaults(
  defineProps<{
    mode: 'create' | 'edit';
    open: boolean;
    form: RedisNodeForm | null;
    busy?: boolean;
    failed?: boolean;
    /** 删除进行中（GuardModal 确认后容器执行） */
    deleting?: boolean;
  }>(),
  { busy: false, failed: false, deleting: false },
);

const emit = defineEmits<{ close: []; submit: [RedisNodeForm]; delete: [] }>();

/** 本地编辑副本：父组件 form 打开后浅拷贝（字段全为原始类型），提交时整份上抛 */
const local = ref<RedisNodeForm | null>(null);
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
  const e = validateRedisNodeForm(local.value);
  errs.value = e;
  if (e.name) return;
  emit('submit', { ...local.value });
}
</script>

<template>
  <Drawer
    data-spec="mw-redis-node-drawer"
    :open="open"
    :title="mode === 'create' ? '新增节点' : '编辑节点'"
    :sub="mode === 'create' ? '录入 Redis 节点连接信息，保存后立即可用' : '修改节点连接与监控参数，保存后立即生效'"
    @close="emit('close')"
  >
    <div
      v-if="failed || !local"
      data-spec="mw-redis-node-drawer-failed"
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
            data-spec="mw-redis-node-form-name"
            type="text"
            placeholder="主缓存"
            :class="[inputCls, 'font-mono']"
          >
          <span
            v-if="errs.name"
            role="alert"
            data-spec="mw-redis-node-form-name-error"
            class="text-[11.5px] text-[var(--el-color-danger)]"
          >{{ errs.name }}</span>
        </label>
        <div class="flex flex-wrap items-center gap-[14px]">
          <label
            data-spec="mw-redis-node-form-enable"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.enable"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            启用该节点
          </label>
          <label
            data-spec="mw-redis-node-form-scanqueue"
            class="inline-flex cursor-pointer items-center gap-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
          >
            <input
              v-model="local.scanQueue"
              type="checkbox"
              class="h-[13px] w-[13px]"
            >
            自动扫描发现消息队列
          </label>
        </div>
        <div class="flex flex-wrap gap-[10px]">
          <label class="flex min-w-[160px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">分类 Category</span>
            <input
              v-model="local.category"
              data-spec="mw-redis-node-form-category"
              type="text"
              placeholder="缓存"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[200px] flex-[2] flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">地址 Server</span>
            <input
              v-model="local.server"
              data-spec="mw-redis-node-form-server"
              type="text"
              placeholder="127.0.0.1:6379"
              :class="[inputCls, 'font-mono']"
            >
          </label>
        </div>
        <div class="flex flex-wrap gap-[10px]">
          <label class="flex min-w-[140px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">用户名 UserName</span>
            <input
              v-model="local.userName"
              data-spec="mw-redis-node-form-username"
              type="text"
              placeholder="default"
              :class="[inputCls, 'font-mono']"
            >
          </label>
          <label class="flex min-w-[140px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">密码 Password</span>
            <input
              v-model="local.password"
              data-spec="mw-redis-node-form-password"
              type="password"
              placeholder="••••••"
              autocomplete="new-password"
              :class="[inputCls, 'font-mono']"
            >
          </label>
        </div>
        <div class="flex flex-wrap gap-[10px]">
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">版本 Version</span>
            <input
              v-model="local.version"
              data-spec="mw-redis-node-form-version"
              type="text"
              placeholder="7.2.4"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">模式 Mode</span>
            <input
              v-model="local.mode"
              data-spec="mw-redis-node-form-mode"
              type="text"
              placeholder="standalone"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">角色 Role</span>
            <input
              v-model="local.role"
              data-spec="mw-redis-node-form-role"
              type="text"
              placeholder="master"
              :class="inputCls"
            >
          </label>
        </div>
      </section>

      <!-- ===== 监控与告警 ===== -->
      <section class="flex flex-col gap-[10px]">
        <h4 :class="H3">
          监控与告警
        </h4>
        <div class="flex flex-wrap gap-[10px]">
          <label class="flex min-w-[140px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">最大内存（MB）</span>
            <input
              v-model.number="local.maxMemory"
              data-spec="mw-redis-node-form-maxmemory"
              type="number"
              min="0"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[140px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">内存淘汰策略</span>
            <input
              v-model="local.memoryPolicy"
              data-spec="mw-redis-node-form-memorypolicy"
              type="text"
              placeholder="noeviction"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[140px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">内存分配器</span>
            <input
              v-model="local.memoryAllocator"
              data-spec="mw-redis-node-form-memoryallocator"
              type="text"
              placeholder="jemalloc"
              :class="inputCls"
            >
          </label>
        </div>
        <div class="flex flex-wrap gap-[10px]">
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">内存告警（%）</span>
            <input
              v-model.number="local.alarmMemoryRate"
              data-spec="mw-redis-node-form-alarmmemoryrate"
              type="number"
              min="0"
              max="100"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">连接数告警</span>
            <input
              v-model.number="local.alarmConnections"
              data-spec="mw-redis-node-form-alarmconnections"
              type="number"
              min="0"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">速度告警</span>
            <input
              v-model.number="local.alarmSpeed"
              data-spec="mw-redis-node-form-alarmspeed"
              type="number"
              min="0"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">入流量告警（Kbps）</span>
            <input
              v-model.number="local.alarmInputKbps"
              data-spec="mw-redis-node-form-alarminputkbps"
              type="number"
              min="0"
              :class="inputCls"
            >
          </label>
          <label class="flex min-w-[120px] flex-1 flex-col gap-[4px]">
            <span class="text-[12px] text-[var(--el-text-color-secondary)]">出流量告警（Kbps）</span>
            <input
              v-model.number="local.alarmOutputKbps"
              data-spec="mw-redis-node-form-alarmoutputkbps"
              type="number"
              min="0"
              :class="inputCls"
            >
          </label>
        </div>
        <label class="flex flex-col gap-[4px]">
          <span class="text-[12px] text-[var(--el-text-color-secondary)]">告警回调 WebHook</span>
          <input
            v-model="local.webHook"
            data-spec="mw-redis-node-form-webhook"
            type="text"
            placeholder="https://"
            :class="[inputCls, 'font-mono']"
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
          data-spec="mw-redis-node-form-remark"
          rows="2"
          class="rounded-[6px] border border-[var(--el-border-color)] bg-[var(--el-bg-color)] px-[8px] py-[6px] text-[12.5px] text-[var(--el-text-color-primary)]"
        />
      </section>

      <!-- 底部操作（Drawer 无 footer slot，正文尾部放置——MySqlNodeDrawer 同款）；编辑模式加删除（GuardModal 在容器） -->
      <div class="flex items-center justify-between gap-2 border-t border-[var(--el-border-color-light)] pt-[12px]">
        <Btn
          v-if="mode === 'edit'"
          variant="danger"
          size="sm"
          data-spec="mw-redis-node-delete"
          :disabled="busy || deleting"
          @click="emit('delete')"
        >
          {{ deleting ? '删除中…' : '删除' }}
        </Btn>
        <span
          v-else
          class="flex-1"
        />
        <div class="flex gap-2">
          <Btn
            variant="ghost"
            data-spec="mw-redis-node-cancel"
            @click="emit('close')"
          >
            取消
          </Btn>
          <Btn
            variant="primary"
            data-spec="mw-redis-node-save"
            :disabled="busy"
            @click="onSubmit"
          >
            {{ busy ? '保存中…' : '保存' }}
          </Btn>
        </div>
      </div>
    </div>
  </Drawer>
</template>
