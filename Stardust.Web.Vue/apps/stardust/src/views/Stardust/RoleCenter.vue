<script setup lang="ts">
/**
 * 角色管理 RoleCenter（第七阶段 · 方案 §17.2）
 *
 * 设计：角色 = 权限集合，三层权限矩阵 ——
 *   功能权限（各模块 查看/编辑/删除）+ 项目数据权限（可访问项目多选）+ 操作权限（发布/重启/回滚等）。
 *  - 卡片列表：角色名 + 描述 + 用户数 + 权限摘要 + 操作（编辑权限/查看用户）
 *  - 详情 Drawer：基本信息 + 三层权限矩阵 + 保存/取消
 *
 * 数据源：后端无 /Admin/Role（探测 404）→ 展示预置角色（运维人员/开发人员）+「角色管理待接入」提示，权限矩阵 UI 完整保留。
 * 上下文联动：watch stardustContext.revision → 重载。
 * 纪律：fetch 已 try-catch；权限项均带中文说明；--el-* 样式。
 */
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { request } from '@newlifex/cube-vue/core/utils/request'
import { stardustContext } from '@/stores/stardustContext'
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue'
import {
  permissionModules,
  functionalKey,
  operationPermissions,
  fetchRoleList,
  fetchProjectOptions,
  ROLES_BACKEND_READY,
  type RoleRow,
  type ProjectOption,
} from './role.model'

// ===== 列表状态 =====
const roles = ref<RoleRow[]>([])
const loading = ref(false)
const projectOptions = ref<ProjectOption[]>([])

const kpi = computed(() => ({ total: roles.value.length, users: roles.value.reduce((s, r) => s + r.userCount, 0) }))

async function reload() {
  loading.value = true
  try {
    roles.value = await fetchRoleList(request)
    projectOptions.value = await fetchProjectOptions(request)
  } catch {
    roles.value = []
  } finally {
    loading.value = false
  }
}

// ===== 详情 Drawer + 权限矩阵草稿 =====
const drawerVisible = ref(false)
const current = ref<RoleRow | null>(null)

/** 功能权限草稿：模块 key → 三档勾选 */
interface FuncDraft {
  [moduleKey: string]: { view: boolean; edit: boolean; del: boolean }
}
const funcDraft = reactive<FuncDraft>({})
const opDraft = ref<string[]>([])
const projDraft = ref<string[]>([])

function buildFuncDraft(role: RoleRow) {
  for (const m of permissionModules) {
    funcDraft[m.key] = {
      view: role.functional.includes(functionalKey(m.key, 'view')),
      edit: role.functional.includes(functionalKey(m.key, 'edit')),
      del: role.functional.includes(functionalKey(m.key, 'delete')),
    }
  }
}

function openDetail(role: RoleRow) {
  current.value = { ...role }
  buildFuncDraft(role)
  opDraft.value = [...role.operation]
  projDraft.value = [...role.dataProjects]
  drawerVisible.value = true
}

function openCreate() {
  current.value = {
    id: 0,
    name: '',
    code: '',
    description: '',
    userCount: 0,
    createdAt: '',
    functional: [],
    operation: [],
    dataProjects: [],
  }
  buildFuncDraft(current.value)
  opDraft.value = []
  projDraft.value = []
  drawerVisible.value = true
}

/** 角色权限摘要文字（卡片用） */
function roleSummary(role: RoleRow): string {
  const funcs = role.functional.filter((k) => k.endsWith('.view')).length
  return `功能权限 ${funcs} 个模块 · 操作权限 ${role.operation.length} 项`
}

function save() {
  drawerVisible.value = false
}

// ===== 生命周期 + 上下文联动 =====
onMounted(reload)
watch(
  () => stardustContext.revision,
  () => void reload(),
)

/** A6：空态图标（Lucide 线性 path，stroke 1.5） */
const ICON_EMPTY = 'M3 7h18M3 12h18M3 17h10';

</script>

<template>
  <div class="flex h-full flex-col gap-5">
    <!-- 页头 -->
    <div class="flex items-start justify-between gap-3">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
          角色管理
        </h1>
        <p class="text-xs text-[var(--el-text-color-regular)]">
          角色 · 权限矩阵 — 功能权限 + 项目数据权限 + 操作权限
        </p>
      </div>
      <el-button
        size="small"
        type="primary"
        @click="openCreate"
      >
        新建角色
      </el-button>
    </div>

    <!-- 后端未接入提示 -->
    <div
      v-if="!ROLES_BACKEND_READY"
      class="rounded-lg border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-4 py-2.5 text-xs text-[var(--el-text-color-regular)]"
    >
      当前展示预置角色（运维人员 / 开发人员）作为权限矩阵示例；后端角色接口就绪后将接入真实数据。
    </div>

    <!-- KPI 统计行 -->
    <div class="grid grid-cols-2 gap-4">
      <KpiStat
        label="角色总数"
        :value="kpi.total"
        accent="primary"
      />
      <KpiStat
        label="关联用户数"
        :value="kpi.users"
        accent="success"
      />
    </div>

    <!-- 角色卡片列表 -->
    <div
      v-loading="loading"
      class="grid flex-1 grid-cols-2 gap-4 overflow-y-auto"
    >
      <div
        v-for="role in roles"
        :key="role.id"
        class="flex cursor-pointer flex-col gap-3 rounded-xl border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] p-5 transition hover:border-[var(--el-color-primary)]"
        @click="openDetail(role)"
      >
        <div class="flex items-center justify-between">
          <span class="text-base font-semibold text-[var(--el-text-color-primary)]">{{ role.name }}</span>
          <el-tag
            size="small"
            effect="plain"
          >
            {{ role.userCount }} 名用户
          </el-tag>
        </div>
        <p class="text-xs text-[var(--el-text-color-regular)]">
          {{ role.description || '暂无描述' }}
        </p>
        <div class="flex flex-wrap gap-1.5">
          <el-tag
            v-for="op in role.operation"
            :key="op"
            size="small"
            type="warning"
            effect="plain"
          >
            {{ operationPermissions.find((o) => o.key === op)?.label ?? op }}
          </el-tag>
        </div>
        <div class="mt-auto flex items-center justify-between pt-1">
          <span class="text-xs text-[var(--el-text-color-secondary)]">{{ roleSummary(role) }}</span>
          <div class="flex gap-2">
            <el-link
              type="primary"
              @click.stop="openDetail(role)"
            >
              编辑权限
            </el-link>
            <el-link
              type="primary"
              @click.stop="openDetail(role)"
            >
              查看用户
            </el-link>
          </div>
        </div>
      </div>

      <EmptyState
        :icon="ICON_EMPTY"
        title="角色管理待接入"
      />
    </div>

    <!-- ===== 角色详情 + 权限矩阵 Drawer ===== -->
    <el-drawer
      v-model="drawerVisible"
      size="680px"
      destroy-on-close
      class="stardust-drawer"
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col gap-1">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ current && current.id ? `角色权限 · ${current.name}` : '新建角色' }}
            </span>
            <span class="text-xs text-[var(--el-text-color-secondary)]">
              功能权限 · 项目数据权限 · 操作权限
            </span>
          </div>
          <button
            class="sd-close"
            aria-label="关闭"
            @click="drawerVisible = false"
          >
            ×
          </button>
        </div>
      </template>

      <div
        v-if="current"
        class="flex flex-col gap-5"
      >
        <!-- 基本信息 -->
        <div>
          <div class="sd-section-title">
            基本信息
          </div>
          <div class="sd-overview-grid">
            <div class="sd-overview-item">
              <span class="sd-overview-label">角色名称</span>
              <el-input
                v-model="current.name"
                placeholder="如 运维人员"
                size="small"
              />
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">角色编码</span>
              <el-input
                v-model="current.code"
                placeholder="如 ops"
                size="small"
              />
            </div>
            <div class="sd-overview-item col-span-2">
              <span class="sd-overview-label">角色描述</span>
              <el-input
                v-model="current.description"
                type="textarea"
                :rows="2"
                placeholder="该角色的职责与权限范围"
              />
            </div>
          </div>
        </div>

        <!-- 功能权限矩阵 -->
        <div>
          <div class="sd-section-title">
            功能权限（各模块的查看 / 编辑 / 删除）
          </div>
          <table class="perm-table">
            <thead>
              <tr>
                <th>功能模块</th>
                <th class="perm-center">
                  查看
                </th>
                <th class="perm-center">
                  编辑
                </th>
                <th class="perm-center">
                  删除
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="m in permissionModules"
                :key="m.key"
              >
                <td class="perm-module">
                  {{ m.label }}
                </td>
                <td class="perm-center">
                  <el-checkbox v-model="funcDraft[m.key].view" />
                </td>
                <td class="perm-center">
                  <el-checkbox v-model="funcDraft[m.key].edit" />
                </td>
                <td class="perm-center">
                  <el-checkbox v-model="funcDraft[m.key].del" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 操作权限 -->
        <div>
          <div class="sd-section-title">
            操作权限（高危 / 原子操作）
          </div>
          <div class="grid grid-cols-2 gap-2">
            <label
              v-for="op in operationPermissions"
              :key="op.key"
              class="perm-op"
            >
              <el-checkbox
                :model-value="opDraft.includes(op.key)"
                @change="(v: boolean) => {
                  if (v && !opDraft.includes(op.key)) opDraft.push(op.key)
                  if (!v) opDraft = opDraft.filter((k) => k !== op.key)
                }"
              />
              <span class="perm-op-text">
                <span class="perm-op-label">{{ op.label }}</span>
                <span class="perm-op-desc">{{ op.desc }}</span>
              </span>
            </label>
          </div>
        </div>

        <!-- 项目数据权限 -->
        <div>
          <div class="sd-section-title">
            项目数据权限（可访问的项目）
          </div>
          <el-select
            v-model="projDraft"
            multiple
            placeholder="选择可访问的项目"
            size="small"
            class="w-full"
          >
            <el-option
              v-for="p in projectOptions"
              :key="p.id"
              :label="p.name"
              :value="p.name"
            />
          </el-select>
        </div>

        <!-- 底部 -->
        <div class="flex items-center gap-2 pt-1">
          <el-button
            type="primary"
            size="small"
            @click="save"
          >
            保存
          </el-button>
          <el-button
            size="small"
            @click="drawerVisible = false"
          >
            取消
          </el-button>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<style scoped>
.stardust-drawer :deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.stardust-drawer :deep(.el-drawer__body) {
  padding: 20px;
  background: color-mix(in srgb, var(--el-bg-color) 92%, transparent);
}
.stardust-drawer :deep(.el-drawer__close-btn) {
  display: none;
}
.sd-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  font-size: 18px;
  line-height: 1;
  color: var(--el-text-color-regular);
  transition: background-color 0.2s, color 0.2s;
}
.sd-close:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}

.sd-section-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}
.sd-overview-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 14px 12px;
}
.sd-overview-item {
  display: flex;
  flex-direction: column;
  gap: 5px;
}
.sd-overview-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}

/* 功能权限矩阵表 */
.perm-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}
.perm-table th,
.perm-table td {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  text-align: left;
}
.perm-table th {
  color: var(--el-text-color-secondary);
  font-weight: 600;
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
}
.perm-center {
  text-align: center !important;
  width: 80px;
}
.perm-module {
  color: var(--el-text-color-primary);
  font-weight: 500;
}

/* 操作权限行 */
.perm-op {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 8px 10px;
  border-radius: 8px;
  background: color-mix(in srgb, var(--el-text-color-primary) 4%, transparent);
  cursor: pointer;
}
.perm-op-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.perm-op-label {
  font-size: 13px;
  color: var(--el-text-color-primary);
}
.perm-op-desc {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
</style>
