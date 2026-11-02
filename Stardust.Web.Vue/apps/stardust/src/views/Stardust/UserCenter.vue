<script setup lang="ts">
/**
 * 用户管理 UserCenter（第七阶段 · 方案 §17.2）
 *
 * 设计：用户关联角色，角色决定权限。三层权限 = 功能 + 项目数据 + 操作。
 *  - 列表（§17.2）：用户名 / 姓名 / 角色（多标签）/ 所属项目 / 状态（StatusPill）/ 最后登录 / 操作
 *  - 详情 Drawer：基本信息 + 角色分配（可移除+添加下拉）+ 项目数据权限（多选）+ 操作权限（只读，角色继承）
 *
 * 数据源：后端无 /Admin/User（探测 404）→ 列表空状态「用户管理待接入」，完整 UI 保留。
 * 上下文联动：watch stardustContext.revision → 重载；cubeAxios 拦截器自动注入 projectId/environment。
 * 纪律：fetch 已在 model 层 try-catch，本页不弹全局错误；用户名/角色名/权限名均带中文说明。
 */
import { ref, computed, onMounted, watch } from 'vue'
import { request } from '@newlifex/cube-vue/core/utils/request'
import { stardustContext } from '@/stores/stardustContext'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue'
import * as m from './user.model'
import { fetchProjectOptions, type ProjectOption } from './role.model'
import type { RoleRow } from './role.model'

// ===== 列表状态 =====
const rows = ref<m.UserRow[]>([])
const loading = ref(false)
const keyword = ref('')
const statusFilter = ref<'all' | m.UserStatus>('all')

const filteredRows = computed(() => m.filterUsers(rows.value, keyword.value, statusFilter.value))
const kpi = computed(() => m.summarizeUsers(rows.value))

// 角色下拉（预置 运维/开发）与项目多选来源
const roleOptions = ref<RoleRow[]>([])
const projectOptions = ref<ProjectOption[]>([])

async function reload() {
  loading.value = true
  try {
    rows.value = await m.fetchUserList(request)
    roleOptions.value = await m.fetchUserRoleOptions(request)
    projectOptions.value = await fetchProjectOptions(request)
  } catch {
    rows.value = []
  } finally {
    loading.value = false
  }
}

// ===== 详情 Drawer =====
const drawerVisible = ref(false)
const current = ref<m.UserRow | null>(null)
const draftRoles = ref<string[]>([])
const draftProjects = ref<string[]>([])

/** 当前用户角色继承到的操作权限中文名（只读） */
const inheritedOps = computed(() => m.inheritedOperations(roleOptions.value.filter((r) => (current.value?.roles ?? []).includes(r.name))))

function openDetail(row: m.UserRow) {
  current.value = { ...row }
  draftRoles.value = [...row.roles]
  draftProjects.value = [...row.projects]
  drawerVisible.value = true
}

function openCreate() {
  current.value = { ...m.emptyUser }
  draftRoles.value = []
  draftProjects.value = []
  drawerVisible.value = true
}

function removeRole(role: string) {
  draftRoles.value = draftRoles.value.filter((r) => r !== role)
}

function onAddRole(role: string | null) {
  if (role && !draftRoles.value.includes(role)) draftRoles.value.push(role)
}

function toggleStatus(row: m.UserRow) {
  row.status = row.status === 'enabled' ? 'disabled' : 'enabled'
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
    <div class="flex flex-col gap-1">
      <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
        用户管理
      </h1>
      <p class="text-xs text-[var(--el-text-color-regular)]">
        用户 · 角色 · 权限 — 平台访问控制
      </p>
    </div>

    <!-- KPI 统计行 -->
    <div class="grid grid-cols-3 gap-4">
      <KpiStat
        label="用户总数"
        :value="kpi.total"
        accent="primary"
      />
      <KpiStat
        label="已启用"
        :value="kpi.enabled"
        accent="success"
      />
      <KpiStat
        label="已禁用"
        :value="kpi.disabled"
        accent="warning"
      />
    </div>

    <!-- 搜索条 -->
    <div class="flex flex-wrap items-center gap-3">
      <el-input
        v-model="keyword"
        placeholder="搜索用户名 / 姓名 / 邮箱"
        clearable
        size="small"
        class="w-64"
      />
      <div class="flex items-center gap-1">
        <button
          v-for="c in m.userStatusChips"
          :key="c.key"
          class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
          :class="statusFilter === c.key ? 'bg-[var(--el-color-primary)] text-white' : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
          @click="statusFilter = c.key"
        >
          {{ c.label }}
        </button>
      </div>
      <div class="ml-auto flex items-center gap-2">
        <el-button
          size="small"
          :loading="loading"
          @click="reload"
        >
          刷新
        </el-button>
        <el-button
          size="small"
          type="primary"
          @click="openCreate"
        >
          新建用户
        </el-button>
      </div>
    </div>

    <!-- 用户表格 -->
    <el-table
      v-loading="loading"
      :data="filteredRows"
      class="sd-table"
      @row-click="openDetail"
    >
      <el-table-column
        label="用户名"
        min-width="140"
      >
        <template #default="{ row }">
          <span class="font-medium text-[var(--el-text-color-primary)]">{{ (row as m.UserRow).name || '—' }}</span>
        </template>
      </el-table-column>
      <el-table-column
        label="姓名"
        min-width="120"
      >
        <template #default="{ row }">
          <span class="text-[var(--el-text-color-regular)]">{{ (row as m.UserRow).displayName || '—' }}</span>
        </template>
      </el-table-column>
      <el-table-column
        label="角色"
        min-width="180"
      >
        <template #default="{ row }">
          <div class="flex flex-wrap gap-1">
            <el-tag
              v-for="r in (row as m.UserRow).roles"
              :key="r"
              size="small"
              effect="plain"
            >
              {{ r }}
            </el-tag>
            <span
              v-if="!(row as m.UserRow).roles.length"
              class="text-xs text-[var(--el-text-color-secondary)]"
            >未分配</span>
          </div>
        </template>
      </el-table-column>
      <el-table-column
        label="所属项目"
        min-width="140"
      >
        <template #default="{ row }">
          <span class="text-xs text-[var(--el-text-color-regular)]">
            {{ (row as m.UserRow).projects.join('、') || '—' }}
          </span>
        </template>
      </el-table-column>
      <el-table-column
        label="状态"
        width="90"
      >
        <template #default="{ row }">
          <StatusPill :status="(row as m.UserRow).status === 'enabled' ? 'running' : 'offline'">
            {{ (row as m.UserRow).status === 'enabled' ? '启用' : '禁用' }}
          </StatusPill>
        </template>
      </el-table-column>
      <el-table-column
        label="最后登录"
        width="150"
      >
        <template #default="{ row }">
          <span class="text-xs text-[var(--el-text-color-secondary)]">{{ m.fmtTime((row as m.UserRow).lastLogin) }}</span>
        </template>
      </el-table-column>
      <el-table-column
        label="操作"
        width="180"
        fixed="right"
      >
        <template #default="{ row }">
          <el-link
            type="primary"
            @click.stop="openDetail(row as m.UserRow)"
          >
            编辑
          </el-link>
          <el-link
            type="primary"
            class="ml-2"
            @click.stop="openDetail(row as m.UserRow)"
          >
            分配角色
          </el-link>
          <el-link
            :type="(row as m.UserRow).status === 'enabled' ? 'danger' : 'success'"
            class="ml-2"
            @click.stop="toggleStatus(row as m.UserRow)"
          >
            {{ (row as m.UserRow).status === 'enabled' ? '禁用' : '启用' }}
          </el-link>
        </template>
      </el-table-column>
      <template #empty>
        <EmptyState
          :icon="ICON_EMPTY"
          title="用户管理待接入 — 后端用户接口就绪后将在此展示真实用户列表"
        />
      </template>
    </el-table>

    <!-- ===== 用户详情 Drawer ===== -->
    <el-drawer
      v-model="drawerVisible"
      size="640px"
      destroy-on-close
      class="stardust-drawer"
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col gap-1">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ current && current.id ? `用户详情 · ${current.displayName || current.name}` : '新建用户' }}
            </span>
            <span class="text-xs text-[var(--el-text-color-secondary)]">
              基本信息 · 角色分配 · 项目数据权限 · 操作权限
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
              <span class="sd-overview-label">用户名</span>
              <el-input
                v-model="current.name"
                placeholder="登录账号"
                size="small"
              />
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">姓名</span>
              <el-input
                v-model="current.displayName"
                placeholder="中文姓名"
                size="small"
              />
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">邮箱</span>
              <el-input
                v-model="current.email"
                placeholder="name@example.com"
                size="small"
              />
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">手机号</span>
              <el-input
                v-model="current.phone"
                placeholder="手机号"
                size="small"
              />
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">状态</span>
              <el-switch
                :model-value="current.status === 'enabled'"
                active-text="启用"
                inactive-text="禁用"
                @change="(v: boolean) => (current!.status = v ? 'enabled' : 'disabled')"
              />
            </div>
            <div class="sd-overview-item">
              <span class="sd-overview-label">最后登录</span>
              <span class="sd-overview-value">{{ m.fmtTime(current.lastLogin) }}</span>
            </div>
          </div>
        </div>

        <!-- 角色分配 -->
        <div>
          <div class="sd-section-title">
            角色分配（角色决定权限）
          </div>
          <div class="flex flex-col gap-2">
            <div class="flex flex-wrap gap-1.5">
              <el-tag
                v-for="r in draftRoles"
                :key="r"
                closable
                size="small"
                type="primary"
                @close="removeRole(r)"
              >
                {{ r }}
              </el-tag>
              <span
                v-if="!draftRoles.length"
                class="text-xs text-[var(--el-text-color-secondary)]"
              >尚未分配角色</span>
            </div>
            <el-select
              :model-value="null"
              placeholder="添加角色"
              size="small"
              class="w-48"
              @change="onAddRole"
            >
              <el-option
                v-for="r in roleOptions.filter((o) => !draftRoles.includes(o.name))"
                :key="r.id"
                :label="r.name"
                :value="r.name"
              />
            </el-select>
          </div>
        </div>

        <!-- 项目数据权限 -->
        <div>
          <div class="sd-section-title">
            项目数据权限（可访问的项目）
          </div>
          <el-select
            v-model="draftProjects"
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

        <!-- 操作权限（只读，按角色继承） -->
        <div>
          <div class="sd-section-title">
            操作权限（由角色继承，只读）
          </div>
          <div
            v-if="inheritedOps.length"
            class="flex flex-wrap gap-1.5"
          >
            <el-tag
              v-for="op in inheritedOps"
              :key="op"
              size="small"
              effect="plain"
            >
              {{ op }}
            </el-tag>
          </div>
          <EmptyState
            :icon="ICON_EMPTY"
            title="未分配角色，暂无操作权限"
          />
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
.sd-table {
  --el-table-border-color: var(--el-border-color-lighter);
  --el-table-header-bg-color: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  --el-table-tr-bg-color: color-mix(in srgb, var(--el-bg-color) 55%, transparent);
  --el-table-row-hover-bg-color: color-mix(in srgb, var(--el-color-primary) 10%, transparent);
  --el-table-bg-color: transparent;
  border-radius: 12px;
  overflow: hidden;
}
.sd-table :deep(.el-table__inner-wrapper::before) {
  display: none;
}
.sd-table :deep(th.el-table__cell) {
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  color: var(--el-text-color-secondary);
  font-weight: 600;
}
.sd-table :deep(td.el-table__cell) {
  border-bottom-color: var(--el-border-color-lighter);
  cursor: pointer;
}

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
.sd-overview-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
</style>
