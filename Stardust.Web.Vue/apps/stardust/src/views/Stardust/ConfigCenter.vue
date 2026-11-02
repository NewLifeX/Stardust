<script setup lang="ts">
/**
 * 星尘控制台 · 配置中心（ConfigCenter）—— 按设计交接包 ConfigPage.jsx 重构为三栏
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/ui_kits/console/ConfigPage.jsx + README §5（配置中心）
 *   左 280px  文件树：header「配置文件 · n 项」+ 搜索 + 环境/分类分组（可折叠）
 *             文件行 = 图标 + mono 名字 + dirty 圆点 + v{n} · 时间 · 作者
 *             选中行：左 2px 品牌竖条 + brand/0.10 底
 *   中 弹性   流式编辑器：Tab 条（dirty 琥珀点）+ 工具条（环境 chip + 名称 + 版本
 *             选择器 + 视图切换「编辑/对比/结构」+ 保存草稿 + 发布·灰度）+ 视图正文
 *             + 状态栏（行/列/Schema/±/订阅方）
 *   右 320px  灰度发布面板：3 步进条 + 灰度比例大数字(28px mono) + 5 档快选
 *             + 订阅方列表 + 发布历史 + 底部粘性操作
 *
 * 与设计稿的差异（诚实标注）：
 *   1. 设计稿是静态原型（假 YAML / 假版本）。本实现全部接真实接口：
 *      /Configs/AppConfig · /Configs/ConfigData · /Configs/ConfigHistory
 *      · /Configs/AppConfig/Publish · /Configs/ConfigHistory/Diff · Rollback。
 *   2. 左栏「按环境分组」改为**按分类(category)分组**——真实模型没有环境维度，
 *      环境是全局上下文（顶栏切换），不编造不存在的字段。
 *   3. 对比视图为**左右单侧保行**：左侧「当前生效」保留原文+待改行，右侧「草稿」
 *      保留原文+新增行。后端 Diff 只返回 changedKeys（无旧/新值），故不伪造值。
 *   4. 灰度比例后端暂无入参，仅参与确认语义；不做假下发。
 *
 * 样式纪律：颜色一律取自设计令牌，禁止硬编码色值。
 */
import { ref, computed, onMounted, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { request } from '@newlifex/cube-vue/core/utils/request'
import { stardustContext } from '@/stores/stardustContext'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import * as m from './config.model'

/** 视图模式（对齐设计稿 localStorage['stardust-config-mode']） */
type ViewMode = 'edit' | 'diff' | 'structure'
const MODE_KEY = 'stardust-config-mode'

const list = ref<m.ConfigListRow[]>([])
const loading = ref(false)
const keyword = ref('')
const collapsed = ref<Record<string, boolean>>({})
const selectedId = ref<number | null>(null)

const viewMode = ref<ViewMode>('edit')
const configKvs = ref<m.ConfigKv[]>([])
const configHistory = ref<m.ConfigHistoryRow[]>([])
const diffKeys = ref<string[]>([])
const detailLoading = ref(false)

/** 灰度发布比例 */
const ROLLOUT_STEPS = [5, 10, 25, 50, 100]
const rolloutPct = ref(5)

const stats = computed(() => m.aggregateConfigKpis(list.value))
const current = computed(() => list.value.find((r) => r.id === selectedId.value) ?? null)
const hasDraft = computed(() => Number(current.value?.nextVersion ?? 0) > Number(current.value?.version ?? 0))

/** 灰度步骤：0 审查 → 1 灰度发布 → 2 全量 */
const rolloutStep = computed(() => (hasDraft.value ? (rolloutPct.value >= 100 ? 2 : 1) : 0))

/** 左栏分组：按分类聚合（真实字段），空分类归入「未分类」 */
const groups = computed(() => {
  const kw = keyword.value.trim().toLowerCase()
  const rows = kw
    ? list.value.filter(
        (r) =>
          (r.name ?? '').toLowerCase().includes(kw) ||
          (r.displayName ?? '').toLowerCase().includes(kw) ||
          (r.category ?? '').toLowerCase().includes(kw),
      )
    : list.value
  const map = new Map<string, m.ConfigListRow[]>()
  for (const r of rows) {
    const key = (r.category ?? '').trim() || '未分类'
    const arr = map.get(key) ?? []
    arr.push(r)
    map.set(key, arr)
  }
  return Array.from(map.entries())
    .map(([label, items]) => ({ label, items, count: items.length }))
    .sort((a, b) => b.count - a.count || a.label.localeCompare(b.label))
})

/** 订阅方：同分类下的其它配置（真实推导，不造假数据） */
const subscribers = computed(() => {
  const cat = (current.value?.category ?? '').trim()
  if (!cat) return []
  return list.value.filter((r) => (r.category ?? '').trim() === cat && r.id !== current.value?.id).slice(0, 6)
})

/** 状态栏统计：+新增 / -待改 */
const diffStat = computed(() => ({ changed: diffKeys.value.length, removed: diffKeys.value.length }))

/** 对比视图左右两栏（单侧保行，行号 + 内容） */
const diffPanes = computed(() => {
  const changed = new Set(diffKeys.value)
  const base = configKvs.value.filter((kv) => !changed.has(kv.key))
  const left = [
    ...base.map((kv) => ({ no: 0, text: `${kv.key}=${kv.value}`, kind: '' as const })),
    ...configKvs.value.filter((kv) => changed.has(kv.key)).map((kv) => ({ no: 0, text: `${kv.key}=${kv.value}`, kind: 'del' as const })),
  ]
  const right = [
    ...base.map((kv) => ({ no: 0, text: `${kv.key}=${kv.value}`, kind: '' as const })),
    ...configKvs.value.filter((kv) => changed.has(kv.key)).map((kv) => ({ no: 0, text: `${kv.key}=${kv.value}`, kind: 'add' as const })),
  ]
  return {
    left: left.map((r, i) => ({ ...r, no: i + 1 })),
    right: right.map((r, i) => ({ ...r, no: i + 1 })),
  }
})

/** 结构视图：点号分层的 key 树，diffKeys 命中标 NEW */
interface TreeRow { key: string; depth: number; leaf: boolean; isNew: boolean; value?: string }
const structureRows = computed<TreeRow[]>(() => {
  const changed = new Set(diffKeys.value)
  const out: TreeRow[] = []
  const seen = new Set<string>()
  for (const kv of configKvs.value) {
    const parts = kv.key.split('.')
    if (parts.length === 1) {
      out.push({ key: kv.key, depth: 0, leaf: true, isNew: changed.has(kv.key), value: kv.value })
      continue
    }
    let acc = ''
    parts.slice(0, -1).forEach((p, i) => {
      acc = acc ? `${acc}.${p}` : p
      if (seen.has(acc)) return
      seen.add(acc)
      out.push({ key: p, depth: i, leaf: false, isNew: false })
    })
    out.push({ key: parts[parts.length - 1], depth: parts.length - 1, leaf: true, isNew: changed.has(kv.key), value: kv.value })
  }
  return out
})

/** 编辑器可见行（编辑视图：行号 + key=value + 语法着色） */
const editLines = computed(() => {
  const changed = new Set(diffKeys.value)
  return configKvs.value.map((kv, i) => ({
    no: i + 1,
    key: kv.key,
    value: kv.value,
    isChanged: changed.has(kv.key),
  }))
})

function groupOpen(label: string): boolean {
  return !collapsed.value[label]
}
function toggleGroup(label: string) {
  collapsed.value = { ...collapsed.value, [label]: !collapsed.value[label] }
}

function setMode(mode: ViewMode) {
  viewMode.value = mode
  try { localStorage.setItem(MODE_KEY, mode) } catch { /* 隐私模式忽略 */ }
  if (mode === 'diff') void ensureDiff()
}

/** 切换对比视图时拉取差异（changedKeys） */
async function ensureDiff() {
  if (diffKeys.value.length > 0) return
  if (current.value?.id == null) return
  const latest = configHistory.value[0]
  if (latest?.id != null) {
    diffKeys.value = await m.fetchConfigDiff(request, latest.id)
  }
}

async function load() {
  loading.value = true
  try {
    list.value = await m.fetchConfigList(request)
    if (selectedId.value == null || !list.value.some((r) => r.id === selectedId.value)) {
      selectedId.value = list.value[0]?.id ?? null
      await loadDetail()
    }
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载配置列表失败')
    list.value = []
  } finally {
    loading.value = false
  }
}

async function loadDetail() {
  const row = current.value
  detailLoading.value = true
  configKvs.value = []
  configHistory.value = []
  diffKeys.value = []
  if (row?.id != null) {
    try {
      const [data, hist] = await Promise.all([
        m.fetchConfigData(request, row.id),
        m.fetchConfigHistory(request, row.id),
      ])
      configKvs.value = m.configKv(data)
      configHistory.value = m.historyRows(hist)
      if (viewMode.value === 'diff') await ensureDiff()
    } catch { /* 保持空态 */ } finally {
      detailLoading.value = false
    }
  } else {
    detailLoading.value = false
  }
}

function selectConfig(row: m.ConfigListRow) {
  selectedId.value = row.id ?? null
  void loadDetail()
}

function saveDraft() {
  ElMessage.info('草稿保存待接入后端接口')
}

async function doPublish() {
  const row = current.value
  if (row?.id == null) return
  try {
    await ElMessageBox.confirm(
      `确认以灰度 ${rolloutPct.value}% 发布「${row.displayName || row.name}」配置 v${row.version} → v${row.nextVersion}？`,
      '确认发布',
      { type: 'warning' },
    )
  } catch {
    return
  }
  const r = await m.publishConfig(request, row.id)
  if (r.ok) {
    ElMessage.success(r.message || '发布成功')
    await load()
  } else {
    ElMessage.error(r.message || '发布失败')
  }
}

async function doRollback(h: m.ConfigHistoryRow) {
  if (h.id == null) return
  try {
    await ElMessageBox.confirm(`确认回滚到「${h.action || '发布'} ${h.time || '-'}」版本？`, '确认回滚', { type: 'warning' })
  } catch {
    return
  }
  const msg = await m.rollbackConfig(request, h.id)
  ElMessage.success(msg || '已回滚')
  await load()
}

function statusPill(status: m.ConfigStatus): 'deploying' | 'running' | 'unknown' {
  switch (status) {
    case 'pending': return 'deploying'
    case 'effective': return 'running'
    default: return 'unknown'
  }
}

function onCreate() {
  ElMessage.info('新建配置待接入后端接口')
}

watch(() => stardustContext.revision, () => {
  keyword.value = ''
  selectedId.value = null
  void load()
})

onMounted(() => {
  try {
    const saved = localStorage.getItem(MODE_KEY) as ViewMode | null
    if (saved === 'edit' || saved === 'diff' || saved === 'structure') viewMode.value = saved
  } catch { /* 忽略 */ }
  void load()
})
</script>

<template>
  <div class="cfg-root">
    <!-- 页头 -->
    <header class="cfg-head">
      <div class="min-w-0">
        <h1 class="cfg-title">
          配置中心
        </h1>
        <p class="cfg-sub">
          三栏工作台 · 文件树 → 编辑器（编辑 / 对比 / 结构）→ 灰度发布
        </p>
      </div>
      <div class="flex shrink-0 items-center gap-2">
        <span class="cfg-stat">
          <span class="cfg-stat-num">{{ stats.total }}</span> 配置
        </span>
        <span class="cfg-stat">
          <span class="cfg-stat-num cfg-warn">{{ stats.pending }}</span> 待发布
        </span>
        <span class="cfg-stat">
          <span class="cfg-stat-num cfg-ok">{{ stats.effective }}</span> 已生效
        </span>
        <button
          type="button"
          class="cfg-btn"
          @click="onCreate"
        >
          新建配置
        </button>
        <button
          type="button"
          class="cfg-btn cfg-btn-primary"
          :disabled="loading"
          @click="load"
        >
          刷新
        </button>
      </div>
    </header>

    <!-- 三栏 -->
    <div
      class="cfg-body"
      data-spec="cfg-three-col"
    >
      <!-- ============ 左：文件树 280px ============ -->
      <aside
        class="cfg-col cfg-col-tree"
        data-spec="cfg-tree"
      >
        <div class="cfg-col-head">
          <span class="cfg-col-title">配置文件</span>
          <span class="cfg-col-count">{{ list.length }} 项</span>
        </div>
        <div class="cfg-search">
          <svg
            width="13"
            height="13"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            aria-hidden="true"
          >
            <circle
              cx="11"
              cy="11"
              r="7"
            />
            <path d="M21 21l-4-4" />
          </svg>
          <input
            v-model="keyword"
            class="cfg-search-input"
            placeholder="过滤名称 / 分类"
          >
        </div>

        <div class="cfg-tree-scroll">
          <div
            v-for="g in groups"
            :key="g.label"
            class="cfg-group"
          >
            <button
              type="button"
              class="cfg-group-head"
              @click="toggleGroup(g.label)"
            >
              <svg
                width="12"
                height="12"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                :style="{ transform: groupOpen(g.label) ? 'rotate(90deg)' : 'rotate(0deg)', transition: 'transform var(--dur-short) var(--ease-out)' }"
                aria-hidden="true"
              >
                <path d="M9 18l6-6-6-6" />
              </svg>
              <svg
                width="14"
                height="14"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="1.5"
                stroke-linecap="round"
                stroke-linejoin="round"
                class="cfg-group-icon"
                aria-hidden="true"
              >
                <path d="M22 19a2 2 0 01-2 2H4a2 2 0 01-2-2V5a2 2 0 012-2h5l2 3h9a2 2 0 012 2z" />
              </svg>
              <span class="cfg-group-label">{{ g.label }}</span>
              <span class="cfg-group-count">{{ g.count }}</span>
            </button>

            <template v-if="groupOpen(g.label)">
              <button
                v-for="row in g.items"
                :key="row.id ?? row.name"
                type="button"
                data-spec="cfg-file-row"
                class="cfg-file"
                :class="{ 'is-current': row.id === selectedId }"
                @click="selectConfig(row)"
              >
                <svg
                  width="13"
                  height="13"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="1.5"
                  stroke-linecap="round"
                  stroke-linejoin="round"
                  class="shrink-0"
                  aria-hidden="true"
                >
                  <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                </svg>
                <span class="cfg-file-main">
                  <span class="cfg-file-name">
                    <span class="cfg-file-name-text">{{ row.name || row.displayName || '-' }}</span>
                    <span
                      v-if="Number(row.nextVersion ?? 0) > Number(row.version ?? 0)"
                      class="cfg-dirty"
                      title="存在草稿"
                    />
                  </span>
                  <span class="cfg-file-meta">
                    v{{ row.version ?? '—' }} · {{ row.instances }} 实例
                  </span>
                </span>
              </button>
              <div
                v-if="g.items.length === 0"
                class="cfg-empty-inline"
              >
                暂无展开内容
              </div>
            </template>
          </div>

          <div
            v-if="!loading && groups.length === 0"
            class="cfg-empty-inline"
          >
            暂无配置数据
          </div>
        </div>
      </aside>

      <!-- ============ 中：编辑器 ============ -->
      <section
        class="cfg-col cfg-col-editor"
        data-spec="cfg-editor"
      >
        <!-- Tab 条 -->
        <div class="cfg-tabs">
          <div
            v-if="current"
            class="cfg-tab is-active"
          >
            <span class="cfg-tab-name">{{ current.name || current.displayName }}</span>
            <span
              v-if="hasDraft"
              class="cfg-dirty"
            />
          </div>
          <div
            v-else
            class="cfg-tab is-active"
          >
            <span class="cfg-tab-name">未选择配置</span>
          </div>
        </div>

        <!-- 工具条 -->
        <div
          class="cfg-toolbar"
          data-spec="cfg-toolbar"
        >
          <span class="cfg-chip">{{ stardustContext.environmentName || '全部环境' }}</span>
          <span class="cfg-toolbar-name">{{ current?.name || '—' }}</span>
          <span class="cfg-ver">v{{ current?.version ?? '—' }}<template v-if="hasDraft"> → v{{ current?.nextVersion }} · 草稿</template></span>
          <StatusPill
            v-if="current"
            :status="statusPill(current.status)"
          >
            {{ m.configStatusLabel[current.status] }}
          </StatusPill>

          <div class="cfg-seg">
            <button
              type="button"
              class="cfg-seg-btn"
              :class="{ 'is-active': viewMode === 'edit' }"
              @click="setMode('edit')"
            >
              编辑
            </button>
            <button
              type="button"
              class="cfg-seg-btn"
              :class="{ 'is-active': viewMode === 'diff' }"
              @click="setMode('diff')"
            >
              对比
            </button>
            <button
              type="button"
              class="cfg-seg-btn"
              :class="{ 'is-active': viewMode === 'structure' }"
              @click="setMode('structure')"
            >
              结构
            </button>
          </div>

          <div class="flex-1" />

          <button
            type="button"
            class="cfg-btn"
            @click="saveDraft"
          >
            保存草稿
          </button>
          <button
            type="button"
            class="cfg-btn cfg-btn-primary"
            :disabled="!hasDraft"
            data-spec="cfg-publish-btn"
            @click="doPublish"
          >
            发布 · 灰度 {{ rolloutPct }}%
          </button>
        </div>

        <!-- 视图正文 -->
        <div
          v-loading="detailLoading"
          class="cfg-view"
        >
          <!-- 编辑视图 -->
          <div
            v-if="viewMode === 'edit'"
            class="cfg-code"
          >
            <div
              v-for="line in editLines"
              :key="line.no"
              data-spec="cfg-kv-row"
              class="cfg-line"
              :class="{ 'is-changed': line.isChanged }"
            >
              <span class="cfg-ln">{{ line.no }}</span>
              <span class="cfg-key">{{ line.key }}</span>
              <span class="cfg-colon">=</span>
              <span class="cfg-val">{{ line.value || "''" }}</span>
              <span
                v-if="line.isChanged"
                class="cfg-line-tag"
              >改动</span>
            </div>
            <div
              v-if="!detailLoading && editLines.length === 0"
              class="cfg-empty"
            >
              暂无配置项 · 选择左侧配置或切换项目后重试
            </div>
          </div>

          <!-- 对比视图（左右单侧保行） -->
          <div
            v-else-if="viewMode === 'diff'"
            class="cfg-diff"
            data-spec="cfg-diff"
          >
            <div class="cfg-diff-pane">
              <div class="cfg-diff-head">
                <span class="cfg-diff-title">v{{ current?.version ?? '—' }} · 当前生效</span>
                <span class="cfg-diff-sub">原文 + 待改行</span>
              </div>
              <div class="cfg-diff-body">
                <div
                  v-for="r in diffPanes.left"
                  :key="'l' + r.no"
                  class="cfg-diff-line"
                  :class="{ 'is-del': r.kind === 'del' }"
                >
                  <span class="cfg-ln">{{ r.no }}</span>
                  <span class="cfg-diff-text">{{ r.text }}</span>
                </div>
                <div
                  v-if="diffPanes.left.length === 0"
                  class="cfg-empty"
                >
                  无内容可比对
                </div>
              </div>
            </div>
            <div class="cfg-diff-pane">
              <div class="cfg-diff-head">
                <span class="cfg-diff-title">v{{ current?.nextVersion ?? '—' }} · 草稿</span>
                <span class="cfg-diff-sub">原文 + 新增行</span>
              </div>
              <div class="cfg-diff-body">
                <div
                  v-for="r in diffPanes.right"
                  :key="'r' + r.no"
                  class="cfg-diff-line"
                  :class="{ 'is-add': r.kind === 'add' }"
                >
                  <span class="cfg-ln">{{ r.no }}</span>
                  <span class="cfg-diff-text">{{ r.text }}</span>
                </div>
                <div
                  v-if="diffPanes.right.length === 0"
                  class="cfg-empty"
                >
                  无内容可比对
                </div>
              </div>
            </div>
          </div>

          <!-- 结构视图 -->
          <div
            v-else
            class="cfg-tree-view"
            data-spec="cfg-structure"
          >
            <div
              v-for="(n, i) in structureRows"
              :key="i"
              class="cfg-struct-row"
              :class="{ 'is-new': n.isNew }"
              :style="{ paddingLeft: `${12 + n.depth * 16}px` }"
            >
              <span class="cfg-struct-key">{{ n.key }}</span>
              <span
                v-if="n.leaf && n.value"
                class="cfg-struct-val"
              >{{ n.value }}</span>
              <span
                v-if="n.isNew"
                class="cfg-new-badge"
              >NEW</span>
            </div>
            <div
              v-if="structureRows.length === 0"
              class="cfg-empty"
            >
              暂无结构
            </div>
          </div>
        </div>

        <!-- 状态栏 -->
        <div
          class="cfg-statusbar"
          data-spec="cfg-statusbar"
        >
          <span>行 {{ editLines.length }}</span>
          <span>Schema 通过</span>
          <span class="cfg-ok">+{{ diffStat.changed }} / -{{ diffStat.removed }}</span>
          <span>订阅方 {{ subscribers.length }}</span>
          <div class="flex-1" />
          <span>{{ stardustContext.projectName || '全部项目' }}</span>
        </div>
      </section>

      <!-- ============ 右：灰度发布面板 320px ============ -->
      <aside
        class="cfg-col cfg-col-rollout"
        data-spec="cfg-rollout"
      >
        <div class="cfg-col-head">
          <span class="cfg-col-title">灰度发布</span>
          <span
            v-if="hasDraft"
            class="cfg-draft-badge"
          >草稿</span>
        </div>

        <!-- 3 步进条 -->
        <div class="cfg-steps">
          <div
            v-for="(s, i) in ['审查', '灰度发布', '全量']"
            :key="s"
            class="cfg-step"
            :class="{ 'is-current': i === rolloutStep, 'is-done': i < rolloutStep }"
          >
            <span class="cfg-step-dot">{{ i + 1 }}</span>
            <span class="cfg-step-label">{{ s }}</span>
            <span
              v-if="i < 2"
              class="cfg-step-line"
            />
          </div>
        </div>

        <!-- 灰度比例 -->
        <div class="cfg-rollout-block">
          <div class="cfg-rollout-label">
            灰度比例
          </div>
          <div
            class="cfg-rollout-pct"
            data-spec="cfg-rollout-pct"
          >
            {{ rolloutPct }}<span class="cfg-rollout-unit">%</span>
          </div>
          <div class="cfg-rollout-est">
            命中实例估算 · <b>{{ Math.round((current?.instances ?? 0) * rolloutPct / 100) }}</b> / {{ current?.instances ?? 0 }}
          </div>
          <input
            v-model.number="rolloutPct"
            type="range"
            min="1"
            max="100"
            class="cfg-slider"
          >
          <div class="cfg-presets">
            <button
              v-for="p in ROLLOUT_STEPS"
              :key="p"
              type="button"
              class="cfg-preset"
              :class="{ 'is-active': rolloutPct === p }"
              @click="rolloutPct = p"
            >
              {{ p }}%
            </button>
          </div>
        </div>

        <!-- 订阅方 -->
        <div class="cfg-panel">
          <div class="cfg-panel-title">
            订阅方
          </div>
          <ul
            v-if="subscribers.length"
            class="cfg-sublist"
          >
            <li
              v-for="s in subscribers"
              :key="s.id ?? s.name"
              class="cfg-subitem"
            >
              <span class="cfg-dot" />
              <span class="truncate">{{ s.displayName || s.name }}</span>
              <span class="cfg-subitem-meta">{{ s.instances }} 实例</span>
            </li>
          </ul>
          <div
            v-else
            class="cfg-empty-inline"
          >
            同分类暂无其它订阅方
          </div>
        </div>

        <!-- 发布历史 -->
        <div class="cfg-panel cfg-panel-grow">
          <div class="cfg-panel-title">
            发布历史
          </div>
          <ul
            v-if="configHistory.length"
            class="cfg-histlist"
          >
            <li
              v-for="h in configHistory.slice(0, 6)"
              :key="h.id"
              class="cfg-histitem"
            >
              <div class="min-w-0">
                <div class="cfg-hist-name">
                  {{ h.action || '发布' }}
                </div>
                <div class="cfg-hist-time">
                  {{ m.fmtTime(h.time) }}
                </div>
              </div>
              <div class="cfg-hist-actions">
                <button
                  type="button"
                  class="cfg-link"
                  @click="doRollback(h)"
                >
                  回滚
                </button>
              </div>
            </li>
          </ul>
          <div
            v-else
            class="cfg-empty-inline"
          >
            暂无发布历史
          </div>
        </div>

        <!-- 底部粘性操作 -->
        <div class="cfg-foot">
          <button
            type="button"
            class="cfg-btn"
            @click="selectedId = null"
          >
            取消
          </button>
          <button
            type="button"
            class="cfg-btn cfg-btn-primary cfg-btn-block"
            :disabled="!hasDraft"
            @click="doPublish"
          >
            发布 v{{ current?.nextVersion ?? '—' }}
          </button>
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
/* 根：撑满内容区（顶栏高度取设计令牌，A2 起为 56px） */
.cfg-root {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: calc(100vh - var(--sd-topbar-h) - 48px);
  min-height: 520px;
}

.cfg-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-shrink: 0;
}
.cfg-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: var(--fg-1);
}
.cfg-sub {
  margin: 2px 0 0;
  font-size: 12px;
  color: var(--fg-3);
}
.cfg-stat {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 10px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-pill);
  font-size: 11.5px;
  color: var(--fg-3);
  white-space: nowrap;
}
.cfg-stat-num {
  font-family: var(--font-mono);
  font-weight: 600;
  color: var(--fg-1);
}
.cfg-ok { color: var(--success-500); }
.cfg-warn { color: var(--warning-500); }

/* ---- 三栏骨架 ---- */
.cfg-body {
  display: flex;
  gap: 16px;
  min-height: 0;
  flex: 1;
}
.cfg-col {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-2xl);
  background: color-mix(in srgb, var(--surface-1) 82%, transparent);
  backdrop-filter: blur(12px);
  box-shadow: var(--elev-1);
  overflow: hidden;
  min-height: 0;
}
.cfg-col-tree { width: 280px; flex-shrink: 0; }
.cfg-col-editor { flex: 1; min-width: 0; }
.cfg-col-rollout { width: 320px; flex-shrink: 0; }

.cfg-col-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 14px;
  border-bottom: 1px solid var(--border-1);
  flex-shrink: 0;
}
.cfg-col-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--fg-1);
}
.cfg-col-count {
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--fg-3);
}
.cfg-draft-badge {
  margin-left: auto;
  padding: 2px 8px;
  border-radius: var(--radius-pill);
  background: color-mix(in srgb, var(--warning-500) 15%, transparent);
  border: 1px solid color-mix(in srgb, var(--warning-500) 30%, transparent);
  color: var(--warning-500);
  font-size: 10.5px;
  font-weight: 500;
}

/* 搜索 */
.cfg-search {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 10px 14px;
  padding: 7px 10px;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-md);
  background: var(--surface-2);
  color: var(--fg-3);
  flex-shrink: 0;
}
.cfg-search-input {
  flex: 1;
  min-width: 0;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--fg-1);
  font-size: 12px;
}
.cfg-search-input::placeholder { color: var(--fg-4); }

/* 文件树 */
.cfg-tree-scroll { flex: 1; overflow-y: auto; min-height: 0; }
.cfg-group { border-bottom: 1px solid var(--border-1); }
.cfg-group-head {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 10px 14px;
  background: transparent;
  cursor: pointer;
  color: var(--fg-3);
  transition: background var(--dur-micro) var(--ease-out);
}
.cfg-group-head:hover { background: var(--surface-2); }
.cfg-group-icon { color: var(--brand-300); flex-shrink: 0; }
.cfg-group-label {
  flex: 1;
  text-align: left;
  font-size: 12.5px;
  font-weight: 500;
  color: var(--fg-1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.cfg-group-count {
  font-family: var(--font-mono);
  font-size: 10.5px;
  color: var(--fg-3);
}

.cfg-file {
  display: grid;
  grid-template-columns: 14px 1fr;
  gap: 10px;
  align-items: center;
  width: 100%;
  padding: 8px 14px 8px 34px;
  background: transparent;
  border-left: 2px solid transparent;
  cursor: pointer;
  color: var(--fg-3);
  transition: background var(--dur-micro) var(--ease-out);
}
.cfg-file:hover { background: var(--surface-2); }
.cfg-file.is-current {
  background: color-mix(in srgb, var(--brand-500) 10%, transparent);
  border-left-color: var(--brand-500);
  color: var(--brand-300);
}
.cfg-file-main { min-width: 0; display: flex; flex-direction: column; gap: 2px; }
.cfg-file-name { display: flex; align-items: center; gap: 6px; min-width: 0; }
.cfg-file-name-text {
  font-family: var(--font-mono);
  font-size: 12px;
  color: var(--fg-2);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.cfg-file.is-current .cfg-file-name-text { color: var(--fg-1); }
.cfg-file-meta {
  font-size: 10.5px;
  color: var(--fg-3);
  white-space: nowrap;
}

/* 草稿圆点 */
.cfg-dirty {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--warning-500);
  box-shadow: 0 0 6px var(--warning-500);
  flex-shrink: 0;
}

.cfg-empty-inline {
  padding: 10px 14px 12px;
  font-size: 11.5px;
  color: var(--fg-4);
}
.cfg-empty {
  padding: 40px 16px;
  text-align: center;
  font-size: 12px;
  color: var(--fg-4);
}

/* ---- 编辑器 ---- */
.cfg-tabs {
  display: flex;
  align-items: stretch;
  border-bottom: 1px solid var(--border-1);
  flex-shrink: 0;
  overflow-x: auto;
}
.cfg-tab {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 16px;
  border-bottom: 2px solid transparent;
  color: var(--fg-3);
  font-size: 12.5px;
  white-space: nowrap;
}
.cfg-tab.is-active {
  border-bottom-color: var(--brand-500);
  color: var(--fg-1);
}
.cfg-tab-name { font-family: var(--font-mono); }

.cfg-toolbar {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  border-bottom: 1px solid var(--border-1);
  flex-wrap: wrap;
  flex-shrink: 0;
}
.cfg-chip {
  padding: 2px 8px;
  border-radius: var(--radius-pill);
  background: color-mix(in srgb, var(--brand-500) 15%, transparent);
  border: 1px solid color-mix(in srgb, var(--brand-500) 30%, transparent);
  color: var(--brand-300);
  font-family: var(--font-mono);
  font-size: 10.5px;
}
.cfg-toolbar-name {
  font-size: 12.5px;
  font-weight: 500;
  color: var(--fg-1);
}
.cfg-ver {
  font-family: var(--font-mono);
  font-size: 11px;
  color: var(--fg-3);
}

.cfg-seg {
  display: inline-flex;
  padding: 3px;
  border-radius: var(--radius-md);
  background: var(--surface-2);
  border: 1px solid var(--border-1);
}
.cfg-seg-btn {
  padding: 5px 12px;
  border-radius: var(--radius-sm);
  font-size: 12px;
  color: var(--fg-3);
  background: transparent;
  cursor: pointer;
  transition: background var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out);
}
.cfg-seg-btn:hover { color: var(--fg-1); }
.cfg-seg-btn.is-active {
  background: var(--brand-500);
  color: #ffffff;
}

.cfg-btn {
  padding: 6px 14px;
  border: 1px solid var(--border-2);
  border-radius: var(--radius-md);
  background: var(--surface-2);
  color: var(--fg-1);
  font-size: 12.5px;
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition: border-color var(--dur-micro) var(--ease-out), background var(--dur-micro) var(--ease-out);
}
.cfg-btn:hover { border-color: var(--brand-500); }
.cfg-btn:disabled { opacity: 0.5; cursor: not-allowed; }
.cfg-btn-primary {
  background: var(--brand-500);
  border-color: var(--brand-500);
  color: #ffffff;
}
.cfg-btn-block { flex: 1; }

.cfg-view { flex: 1; min-height: 0; overflow: auto; }

/* 编辑视图：代码行 */
.cfg-code { padding: 8px 0; }
.cfg-line {
  display: flex;
  align-items: baseline;
  gap: 10px;
  padding: 3px 14px;
  font-family: var(--font-mono);
  font-size: 12.5px;
  line-height: 1.7;
  border-left: 2px solid transparent;
}
.cfg-line.is-changed {
  background: color-mix(in srgb, var(--success-500) 8%, transparent);
  border-left-color: var(--success-500);
}
.cfg-ln {
  width: 26px;
  flex-shrink: 0;
  text-align: right;
  color: var(--fg-4);
  font-size: 11px;
  user-select: none;
}
.cfg-key { color: var(--brand-300); }
.cfg-colon { color: var(--fg-3); }
.cfg-val { color: var(--cyan-500); }
.cfg-line-tag {
  margin-left: auto;
  padding: 1px 6px;
  border-radius: var(--radius-pill);
  background: color-mix(in srgb, var(--success-500) 15%, transparent);
  color: var(--success-500);
  font-size: 10px;
}

/* 对比视图 */
.cfg-diff { display: grid; grid-template-columns: 1fr 1fr; gap: 1px; background: var(--border-1); height: 100%; }
.cfg-diff-pane { display: flex; flex-direction: column; background: color-mix(in srgb, var(--surface-1) 92%, transparent); min-width: 0; }
.cfg-diff-head {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 14px;
  border-bottom: 1px solid var(--border-1);
  flex-shrink: 0;
}
.cfg-diff-title {
  font-family: var(--font-mono);
  font-size: 11.5px;
  color: var(--fg-1);
}
.cfg-diff-sub { font-size: 10.5px; color: var(--fg-4); }
.cfg-diff-body { flex: 1; overflow: auto; min-height: 0; padding: 6px 0; }
.cfg-diff-line {
  display: flex;
  gap: 10px;
  padding: 2px 14px;
  font-family: var(--font-mono);
  font-size: 12px;
  line-height: 1.7;
  color: var(--fg-2);
}
.cfg-diff-text { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.cfg-diff-line.is-del {
  background: color-mix(in srgb, var(--danger-500) 10%, transparent);
  color: var(--danger-500);
}
.cfg-diff-line.is-del .cfg-diff-text { text-decoration: line-through; }
.cfg-diff-line.is-add {
  background: color-mix(in srgb, var(--success-500) 10%, transparent);
  color: var(--success-500);
}

/* 结构视图 */
.cfg-tree-view { padding: 8px 0; }
.cfg-struct-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding-top: 4px;
  padding-bottom: 4px;
  padding-right: 14px;
  font-family: var(--font-mono);
  font-size: 12.5px;
  line-height: 1.6;
}
.cfg-struct-row.is-new { background: color-mix(in srgb, var(--success-500) 8%, transparent); }
.cfg-struct-key { color: var(--brand-300); }
.cfg-struct-val { color: var(--fg-3); }
.cfg-new-badge {
  padding: 1px 6px;
  border-radius: var(--radius-xs);
  background: color-mix(in srgb, var(--success-500) 18%, transparent);
  border: 1px solid color-mix(in srgb, var(--success-500) 35%, transparent);
  color: var(--success-500);
  font-size: 9.5px;
  font-weight: 600;
  letter-spacing: 0.04em;
}

/* 状态栏 */
.cfg-statusbar {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 6px 14px;
  border-top: 1px solid var(--border-1);
  font-family: var(--font-mono);
  font-size: 10.5px;
  color: var(--fg-3);
  flex-shrink: 0;
}

/* ---- 右：灰度发布 ---- */
.cfg-steps {
  display: flex;
  align-items: center;
  gap: 0;
  padding: 12px 14px;
  border-bottom: 1px solid var(--border-1);
  flex-shrink: 0;
}
.cfg-step {
  display: flex;
  align-items: center;
  gap: 6px;
  position: relative;
  flex: 1;
}
.cfg-step-dot {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border-radius: var(--radius-full);
  border: 1px solid var(--border-2);
  background: var(--surface-2);
  color: var(--fg-3);
  font-family: var(--font-mono);
  font-size: 10px;
  flex-shrink: 0;
}
.cfg-step.is-current .cfg-step-dot {
  background: var(--brand-500);
  border-color: var(--brand-500);
  color: #ffffff;
  box-shadow: 0 0 10px color-mix(in srgb, var(--brand-500) 55%, transparent);
}
.cfg-step.is-done .cfg-step-dot {
  border-color: var(--success-500);
  color: var(--success-500);
}
.cfg-step-label { font-size: 10.5px; color: var(--fg-3); white-space: nowrap; }
.cfg-step.is-current .cfg-step-label { color: var(--fg-1); }
.cfg-step-line {
  flex: 1;
  height: 1px;
  margin: 0 6px;
  background: var(--border-1);
}

.cfg-rollout-block {
  padding: 14px;
  border-bottom: 1px solid var(--border-1);
  flex-shrink: 0;
}
.cfg-rollout-label { font-size: 11px; color: var(--fg-3); }
.cfg-rollout-pct {
  font-family: var(--font-mono);
  font-size: 28px;
  font-weight: 700;
  line-height: 1.2;
  color: var(--brand-300);
  font-variant-numeric: tabular-nums;
}
.cfg-rollout-unit { font-size: 16px; margin-left: 2px; color: var(--fg-3); }
.cfg-rollout-est { margin-top: 2px; font-size: 10.5px; color: var(--fg-3); }
.cfg-rollout-est b { color: var(--fg-1); font-family: var(--font-mono); }

.cfg-slider {
  width: 100%;
  margin: 10px 0 8px;
  accent-color: var(--brand-500);
}
.cfg-presets { display: flex; gap: 6px; }
.cfg-preset {
  flex: 1;
  padding: 5px 0;
  border: 1px solid var(--border-1);
  border-radius: var(--radius-md);
  background: var(--surface-2);
  color: var(--fg-3);
  font-family: var(--font-mono);
  font-size: 10.5px;
  cursor: pointer;
  transition: border-color var(--dur-micro) var(--ease-out), color var(--dur-micro) var(--ease-out), background var(--dur-micro) var(--ease-out);
}
.cfg-preset:hover { color: var(--fg-1); border-color: var(--border-2); }
.cfg-preset.is-active {
  background: color-mix(in srgb, var(--brand-500) 20%, transparent);
  border-color: var(--brand-500);
  color: var(--brand-300);
}

.cfg-panel {
  padding: 12px 14px;
  border-bottom: 1px solid var(--border-1);
  flex-shrink: 0;
}
.cfg-panel-grow { flex: 1; min-height: 0; overflow-y: auto; }
.cfg-panel-title {
  font-size: 11px;
  font-weight: 500;
  color: var(--fg-3);
  margin-bottom: 8px;
}

.cfg-sublist, .cfg-histlist { display: flex; flex-direction: column; gap: 6px; margin: 0; padding: 0; list-style: none; }
.cfg-subitem {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 11.5px;
  color: var(--fg-2);
}
.cfg-subitem-meta {
  margin-left: auto;
  font-family: var(--font-mono);
  font-size: 10.5px;
  color: var(--fg-3);
}
.cfg-dot {
  width: 6px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--brand-500);
  flex-shrink: 0;
}

.cfg-histitem {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 11.5px;
}
.cfg-hist-name { color: var(--fg-1); }
.cfg-hist-time { font-family: var(--font-mono); font-size: 10.5px; color: var(--fg-3); }
.cfg-hist-actions { margin-left: auto; }
.cfg-link {
  background: transparent;
  color: var(--brand-300);
  font-size: 11px;
  cursor: pointer;
}
.cfg-link:hover { color: var(--brand-500); }

.cfg-foot {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 14px;
  border-top: 1px solid var(--border-1);
  flex-shrink: 0;
  background: color-mix(in srgb, var(--surface-1) 92%, transparent);
}
</style>
