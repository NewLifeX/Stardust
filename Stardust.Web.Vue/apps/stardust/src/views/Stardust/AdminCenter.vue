<script setup lang="ts">
/**
 * 平台管理 AdminCenter（设计稿 src/page-admin.jsx，169 行 · 第五/七阶段合并屏）
 *
 * 设计稿为**单页 5 Tab**：项目 / 用户 / 角色 / Token / 系统设置，
 * 工程原实现拆为 /stardust/users（UserCenter）、/stardust/roles（RoleCenter）、
 * /Platform/GalaxyProject、/Platform/McpToken、/stardust/settings 五处；
 * 本页按设计稿合为一页（原页面与路由保留，未删除）。
 *
 * 数据源：/Platform/GalaxyProject、/Admin/User、/Admin/Role、/Platform/McpToken（逐请求 fail-soft）。
 * 样式纪律：颜色一律 --el-* 令牌，禁 emoji，图标用 Lucide 线性内联 SVG（stroke-width 1.5）。
 */
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { request } from '@newlifex/cube-vue/core/utils/request'
import { stardustContext } from '@/stores/stardustContext'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue'
import * as m from './adminCenter.model'

type TabKey = 'projects' | 'users' | 'roles' | 'tokens' | 'settings'

const route = useRoute()
const router = useRouter()

const tabs: { key: TabKey; label: string }[] = [
  { key: 'projects', label: '项目' },
  { key: 'users', label: '用户' },
  { key: 'roles', label: '角色' },
  { key: 'tokens', label: 'Token' },
  { key: 'settings', label: '系统设置' },
]
const tab = ref<TabKey>(((route.query.tab as TabKey) || 'projects') as TabKey)

function switchTab(k: TabKey) {
  tab.value = k
  void router.replace({ query: { ...route.query, tab: k } })
}
watch(
  () => route.query.tab as TabKey | undefined,
  (k) => {
    if (k && k !== tab.value) tab.value = k
  },
)

// ===== 数据 =====
const projects = ref<m.ProjectRow[]>([])
const users = ref<m.AdminUserRow[]>([])
const roles = ref<m.AdminRoleRow[]>([])
const tokens = ref<m.TokenRow[]>([])
const loading = ref(false)

/**
 * 取数：四个 Tab **各自独立**，任一接口慢或失败都不阻塞其它 Tab。
 *
 * 实测（2026-10-14，真实后端）：单请求 2-3s，但在 20 余路并发下膨胀到 15-30s。
 * 原实现把四个请求 Promise.all 成一个原子操作 —— 只要有一个慢接口未返回，
 * 四个 Tab 就全部停留在加载态、计数全 0 并渲染空态，与数据是否存在无关。
 * 另加 loadSeq 守卫：项目上下文切换会触发二次 load，慢响应回程时不得覆盖新结果。
 */
let loadSeq = 0
let inflight = 0
function load() {
  const seq = ++loadSeq
  inflight = 4
  loading.value = true
  const apply = <T>(setter: (v: T) => void) => (v: T) => {
    if (seq === loadSeq) setter(v)
  }
  const finish = () => {
    inflight = Math.max(0, inflight - 1)
    if (inflight === 0) loading.value = false
  }
  void m
    .fetchProjects(request as m.Req)
    .then(apply((v) => (projects.value = m.toProjectRows(v))))
    .finally(finish)
  void m
    .fetchAdminUsers(request as m.Req)
    .then(apply((v) => (users.value = m.toAdminUserRows(v))))
    .finally(finish)
  void m
    .fetchAdminRoles(request as m.Req)
    .then(apply((v) => (roles.value = m.toAdminRoleRows(v))))
    .finally(finish)
  void m
    .fetchTokens(request as m.Req)
    .then(apply((v) => (tokens.value = m.toTokenRows(v))))
    .finally(finish)
}

/**
 * 首轮取数门控：等 stardustContext.ready（TopBar 完成项目解析）后再加载。
 *
 * 原先 onMounted 就直接 load()，会先按 null 上下文打一整轮请求；解析完成后
 * revision 递增又整轮重打 —— 实测在后端并发下等于把请求量翻倍。
 * 改为 watch 上下文键（ready|projectId|environment）：就绪与后续切换共用一条触发路径，
 * 天然合并「首次就绪」与「切换项目/环境」两类变更，不再重复取数。
 */
const ctxKey = computed(
  () => `${stardustContext.ready ? 1 : 0}|${stardustContext.projectId ?? ''}|${stardustContext.environment ?? ''}`,
)
watch(
  ctxKey,
  () => {
    if (stardustContext.ready) load()
  },
  { immediate: true },
)

function tabCount(k: TabKey): number {
  if (k === 'projects') return projects.value.length
  if (k === 'users') return users.value.length
  if (k === 'roles') return roles.value.length
  if (k === 'tokens') return tokens.value.length
  return 0
}

// ===== 筛选 =====
const projKeyword = ref('')
const tokenKeyword = ref('')
const userKeyword = ref('')
const userRoleFilter = ref('all')
const userStatusFilter = ref<'all' | 'active' | 'inactive'>('all')

const filteredProjects = computed(() => {
  const kw = projKeyword.value.trim().toLowerCase()
  return kw ? projects.value.filter((p) => (p.name + p.code).toLowerCase().includes(kw)) : projects.value
})
const filteredTokens = computed(() => {
  const kw = tokenKeyword.value.trim().toLowerCase()
  return kw ? tokens.value.filter((t) => t.name.toLowerCase().includes(kw)) : tokens.value
})
const filteredUsers = computed(() => {
  let list = users.value
  const kw = userKeyword.value.trim().toLowerCase()
  if (kw) list = list.filter((u) => (u.name + u.email).toLowerCase().includes(kw))
  if (userRoleFilter.value !== 'all') list = list.filter((u) => u.role === userRoleFilter.value)
  if (userStatusFilter.value !== 'all') list = list.filter((u) => u.status === userStatusFilter.value)
  return list
})
const roleOptions = computed(() => [...new Set(users.value.map((u) => u.role).filter(Boolean))])

// 角色成员数：由真实用户数据派生（/Admin/Role 无 userCount 字段）
function roleUserCount(name: string): number {
  return users.value.filter((u) => String(u.rawRoles || '').split(',').map((s) => s.trim()).indexOf(name) >= 0).length
}

const ICON_EMPTY = 'M3 7h18v13H3zM3 7l2-4h14l2 4M9 12h6'
</script>

<template>
  <div
    class="flex h-full flex-col gap-5"
    data-spec="ac-page"
  >
    <!-- 页头（设计稿 PageHeader） -->
    <div class="flex flex-col gap-1">
      <h1
        class="text-xl font-semibold text-[var(--el-text-color-primary)]"
        data-spec="ac-title"
      >
        平台管理
      </h1>
      <p class="text-xs text-[var(--el-text-color-regular)]">
        项目 · 用户 · 角色 · Token · 系统设置
      </p>
    </div>

    <!-- Tab 栏（设计稿 Tabs，带 count） -->
    <div class="sd-tabs">
      <button
        v-for="t in tabs"
        :key="t.key"
        class="sd-tab"
        :class="{ 'sd-tab-active': tab === t.key }"
        data-spec="ac-tab"
        :data-tab="t.key"
        @click="switchTab(t.key)"
      >
        {{ t.label }}<span
          v-if="t.key !== 'settings'"
          class="ac-tab-count"
          data-spec="ac-tab-count"
        >{{ tabCount(t.key) }}</span>
      </button>
    </div>

    <!-- ===== 项目 Tab ===== -->
    <div
      v-if="tab === 'projects'"
      class="flex flex-col gap-4"
      data-spec="ac-projects"
    >
      <div class="flex flex-wrap items-center gap-3">
        <el-input
          v-model="projKeyword"
          placeholder="项目名"
          clearable
          size="small"
          class="w-64"
          data-spec="ac-proj-search"
        />
        <span class="flex-1" />
        <button
          class="ac-btn primary"
          type="button"
          data-spec="ac-proj-new"
        >
          <svg
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          ><path d="M12 5v14M5 12h14" /></svg>
          新建项目
        </button>
      </div>

      <div class="ac-panel">
        <table
          class="ac-table"
          data-spec="ac-proj-table"
        >
          <thead>
            <tr>
              <th>项目</th>
              <th class="ac-num">
                应用
              </th>
              <th class="ac-num">
                节点
              </th>
              <th class="ac-num">
                成员
              </th>
              <th>环境</th>
              <th>创建时间</th>
              <th class="ac-ops" />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="p in filteredProjects"
              :key="p.id"
              data-spec="ac-proj-row"
            >
              <td class="ac-link ac-strong">
                {{ p.name }}
              </td>
              <td class="ac-num ac-dim">
                {{ p.apps || '—' }}
              </td>
              <td class="ac-num ac-dim">
                {{ p.nodes || '—' }}
              </td>
              <td class="ac-num ac-dim">
                {{ p.members || '—' }}
              </td>
              <td>
                <span
                  v-for="e in p.envs"
                  :key="e"
                  class="ac-tag"
                >{{ e }}</span>
              </td>
              <td class="ac-dim">
                {{ p.createdAt || '—' }}
              </td>
              <td>
                <button
                  class="ac-btn sm"
                  type="button"
                >
                  设置
                </button>
              </td>
            </tr>
          </tbody>
        </table>
        <div
          v-if="!filteredProjects.length"
          class="ac-empty"
          data-spec="ac-proj-empty"
        >
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无项目数据"
          />
        </div>
      </div>
    </div>

    <!-- ===== 用户 Tab ===== -->
    <div
      v-else-if="tab === 'users'"
      class="flex flex-col gap-4"
      data-spec="ac-users"
    >
      <div class="flex flex-wrap items-center gap-3">
        <el-input
          v-model="userKeyword"
          placeholder="姓名 / 邮箱"
          clearable
          size="small"
          class="w-64"
          data-spec="ac-user-search"
        />
        <el-select
          v-model="userRoleFilter"
          size="small"
          class="w-32"
          data-spec="ac-user-role"
        >
          <el-option
            label="角色: 全部"
            value="all"
          />
          <el-option
            v-for="r in roleOptions"
            :key="r"
            :label="r"
            :value="r"
          />
        </el-select>
        <el-select
          v-model="userStatusFilter"
          size="small"
          class="w-32"
          data-spec="ac-user-status"
        >
          <el-option
            label="状态: 全部"
            value="all"
          />
          <el-option
            label="active"
            value="active"
          />
          <el-option
            label="inactive"
            value="inactive"
          />
        </el-select>
        <span class="flex-1" />
        <button
          class="ac-btn primary"
          type="button"
          data-spec="ac-user-invite"
        >
          <svg
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          ><path d="M12 5v14M5 12h14" /></svg>
          邀请用户
        </button>
      </div>

      <div class="ac-panel">
        <table
          class="ac-table"
          data-spec="ac-user-table"
        >
          <thead>
            <tr>
              <th>姓名</th>
              <th>邮箱</th>
              <th>角色</th>
              <th>最近活跃</th>
              <th>状态</th>
              <th class="ac-ops" />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="u in filteredUsers"
              :key="u.id"
              data-spec="ac-user-row"
            >
              <td>
                <div class="ac-hstack">
                  <span class="ac-avatar">{{ u.initial }}</span>
                  <span class="ac-link">{{ u.name }}</span>
                </div>
              </td>
              <td class="ac-mono ac-dim ac-sm">
                {{ u.email }}
              </td>
              <td>
                <span
                  v-if="u.role"
                  class="ac-tag brand"
                >{{ u.role }}</span>
                <span
                  v-else
                  class="ac-dim"
                >—</span>
              </td>
              <td class="ac-dim">
                {{ u.lastActive || '—' }}
              </td>
              <td>
                <StatusPill :status="m.statusLabel(u.status)" />
                <span class="ac-dim ac-sm ac-ml">{{ m.statusText(u.status) }}</span>
              </td>
              <td>
                <button
                  class="ac-btn sm ghost"
                  type="button"
                >
                  <svg
                    width="14"
                    height="14"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="1.5"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  ><path d="M5 12h.01M12 12h.01M19 12h.01" /></svg>
                </button>
              </td>
            </tr>
          </tbody>
        </table>
        <div
          v-if="!filteredUsers.length"
          class="ac-empty"
          data-spec="ac-user-empty"
        >
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无用户数据"
          />
        </div>
      </div>
    </div>

    <!-- ===== 角色 Tab ===== -->
    <div
      v-else-if="tab === 'roles'"
      class="flex flex-col gap-4"
      data-spec="ac-roles"
    >
      <div class="ac-panel">
        <table
          class="ac-table"
          data-spec="ac-role-table"
        >
          <thead>
            <tr>
              <th>角色</th>
              <th class="ac-num">
                成员数
              </th>
              <th>权限</th>
              <th class="ac-ops-lg" />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in roles"
              :key="r.id"
              data-spec="ac-role-row"
            >
              <td>
                <span class="ac-tag brand">{{ r.name }}</span>
              </td>
              <td class="ac-num ac-mono">
                {{ roleUserCount(r.name) }}
              </td>
              <td class="ac-dim">
                {{ r.perms || '—' }}
              </td>
              <td>
                <button
                  class="ac-btn sm"
                  type="button"
                >
                  编辑
                </button>
              </td>
            </tr>
          </tbody>
        </table>
        <div
          v-if="!roles.length"
          class="ac-empty"
          data-spec="ac-role-empty"
        >
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无角色数据"
          />
        </div>
      </div>
    </div>

    <!-- ===== Token Tab ===== -->
    <div
      v-else-if="tab === 'tokens'"
      class="flex flex-col gap-4"
      data-spec="ac-tokens"
    >
      <div class="flex flex-wrap items-center gap-3">
        <el-input
          v-model="tokenKeyword"
          placeholder="Token 名"
          clearable
          size="small"
          class="w-64"
          data-spec="ac-token-search"
        />
        <span class="flex-1" />
        <button
          class="ac-btn primary"
          type="button"
          data-spec="ac-token-new"
        >
          <svg
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          ><path d="M12 5v14M5 12h14" /></svg>
          生成 Token
        </button>
      </div>

      <div class="ac-panel">
        <table
          class="ac-table"
          data-spec="ac-token-table"
        >
          <thead>
            <tr>
              <th>名称</th>
              <th>角色</th>
              <th>创建时间</th>
              <th>最近使用</th>
              <th>状态</th>
              <th class="ac-ops-lg" />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="t in filteredTokens"
              :key="t.id"
              data-spec="ac-token-row"
            >
              <td class="ac-mono ac-link">
                {{ t.name }}
              </td>
              <td>
                <span
                  v-if="t.role"
                  class="ac-tag brand"
                >{{ t.role }}</span>
                <span
                  v-else
                  class="ac-dim"
                >—</span>
              </td>
              <td class="ac-dim">
                {{ t.created || '—' }}
              </td>
              <td class="ac-dim">
                {{ t.last || '—' }}
              </td>
              <td><StatusPill :status="m.statusLabel(t.status)" /></td>
              <td>
                <button
                  class="ac-btn sm danger"
                  type="button"
                >
                  吊销
                </button>
              </td>
            </tr>
          </tbody>
        </table>
        <div
          v-if="!filteredTokens.length"
          class="ac-empty"
          data-spec="ac-token-empty"
        >
          <EmptyState
            :icon="ICON_EMPTY"
            title="暂无 Token 数据"
          />
        </div>
      </div>
    </div>

    <!-- ===== 系统设置 Tab（设计稿 two-col g-1-1 四面板）===== -->
    <div
      v-else
      class="ac-settings"
      data-spec="ac-settings"
    >
      <section
        v-for="p in m.SETTINGS_PANELS"
        :key="p.key"
        class="ac-panel ac-pad"
        data-spec="ac-panel"
        :data-panel="p.key"
      >
        <div class="ac-panel-title">
          {{ p.title }}
          <span
            v-if="p.sample"
            class="ac-tag warn"
            data-spec="ac-sample-tag"
          >示例值 · 待接入</span>
        </div>
        <dl class="ac-dl">
          <template
            v-for="it in p.items"
            :key="it.k"
          >
            <dt>{{ it.k }}</dt>
            <dd :class="{ 'ac-mono': it.mono }">
              {{ it.v }}
            </dd>
          </template>
        </dl>
      </section>

      <section
        class="ac-panel ac-pad"
        data-spec="ac-panel"
        data-panel="agent"
      >
        <div class="ac-panel-title">
          接入配置
          <span class="ac-tag warn">示例值 · 待接入</span>
        </div>
        <div class="ac-section-label">
          Agent 下载
        </div>
        <div class="ac-tag-row">
          <span
            v-for="a in m.AGENT_PLATFORMS"
            :key="a"
            class="ac-tag"
            data-spec="ac-platform-tag"
          >{{ a }}</span>
        </div>
        <pre
          class="ac-pre"
          data-spec="ac-install-cmd"
        >{{ m.INSTALL_CMD }}</pre>
      </section>

      <section
        class="ac-panel ac-pad"
        data-spec="ac-panel"
        data-panel="audit"
      >
        <div class="ac-panel-title">
          审计日志
          <span class="ac-tag warn">示例值 · 待接入</span>
        </div>
        <div class="ac-dim ac-sm ac-mb2">
          保留 <span class="ac-mono ac-fg">{{ m.AUDIT_INFO.retainDays }}</span> · {{ m.AUDIT_INFO.archive }}
        </div>
        <div class="ac-dim ac-sm">
          导出格式：{{ m.AUDIT_INFO.formats }} · 平均 <span class="ac-mono">{{ m.AUDIT_INFO.perDay }}</span>
        </div>
        <button
          class="ac-btn sm ac-mt3"
          type="button"
          data-spec="ac-audit-export"
        >
          <svg
            width="12"
            height="12"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
          ><path d="M19 21H5a2 2 0 01-2-2V5a2 2 0 012-2h11l5 5v11a2 2 0 01-2 2z" /><path d="M17 21v-8H7v8M7 3v5h8" /></svg>
          导出
        </button>
      </section>
    </div>
  </div>
</template>

<style scoped>
.ac-tab-count {
  margin-left: 5px;
  padding: 1px 6px;
  border-radius: 999px;
  font-size: 10px;
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
}
.ac-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-overlay);
  overflow: hidden;
}
.ac-pad { padding: 16px; }
.ac-panel-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 12px;
}
.ac-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.ac-table th {
  padding: 8px 12px;
  text-align: left;
  font-weight: 500;
  font-size: 11px;
  color: var(--el-text-color-secondary);
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.ac-table td {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-regular);
}
.ac-table tbody tr:hover { background: var(--el-fill-color-light); }
.ac-num { text-align: right; font-variant-numeric: tabular-nums; }
.ac-ops { width: 60px; }
.ac-ops-lg { width: 100px; }
.ac-dim { color: var(--el-text-color-secondary); }
.ac-sm { font-size: 11px; }
.ac-mono { font-family: 'JetBrains Mono', ui-monospace, monospace; }
.ac-link { color: var(--el-color-primary); }
.ac-strong { font-weight: 500; }
.ac-fg { color: var(--el-text-color-primary); }
.ac-ml { margin-left: 6px; }
.ac-mb2 { margin-bottom: 8px; }
.ac-mt3 { margin-top: 12px; }
.ac-hstack { display: flex; align-items: center; gap: 8px; }
.ac-avatar {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 999px;
  font-size: 10px;
  font-weight: 600;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  border: 1px solid var(--el-color-primary-light-5);
}
.ac-tag {
  display: inline-block;
  margin-right: 4px;
  padding: 1px 7px;
  border-radius: 4px;
  font-size: 10px;
  border: 1px solid var(--el-border-color);
  color: var(--el-text-color-regular);
}
.ac-tag.brand {
  color: var(--el-color-primary);
  border-color: var(--el-color-primary-light-5);
  background: var(--el-color-primary-light-9);
}
.ac-tag.warn {
  color: var(--el-color-warning);
  border-color: var(--el-color-warning-light-5);
  background: var(--el-color-warning-light-9);
}
.ac-tag-row { display: flex; flex-wrap: wrap; gap: 4px; }
.ac-btn {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  height: 28px;
  padding: 0 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.ac-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.ac-btn.primary {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary);
  color: var(--el-color-white);
}
.ac-btn.sm { height: 24px; padding: 0 10px; font-size: 11px; }
.ac-btn.ghost { border-color: transparent; color: var(--el-text-color-secondary); }
.ac-btn.danger {
  border-color: var(--el-color-danger-light-5);
  color: var(--el-color-danger);
}
.ac-empty { padding: 24px; }
.ac-settings {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}
.ac-dl {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 8px 12px;
  margin: 0;
  font-size: 12px;
}
.ac-dl dt { color: var(--el-text-color-secondary); }
.ac-dl dd { margin: 0; color: var(--el-text-color-regular); }
.ac-section-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  margin-bottom: 8px;
}
.ac-pre {
  margin: 0;
  margin-top: 12px;
  padding: 12px;
  border-radius: 8px;
  background: var(--el-fill-color-light);
  font-size: 12px;
  font-family: 'JetBrains Mono', ui-monospace, monospace;
  color: var(--el-text-color-regular);
  overflow-x: auto;
  white-space: pre;
}
</style>
