<script setup lang="ts">
/**
 * 节点详情（设计稿 src/page-nodes.jsx · NodeDetail）
 *
 * 结构：面包屑 + 标题行（名称 / 状态 / 区域标签 / Shell·重启 Agent·摘除）
 *       → 6 Tab：概览 / 应用 / 性能 / 运行时 / 命令 / 历史
 * 概览：三资源环形仪表 + 运行应用表 + 最近操作 + 健康检查表
 *
 * 逻辑全部抽 nodeDetail.model.ts 纯函数；数据全部来自真实接口（fail-soft）。
 * 样式纪律：颜色一律 --el-* 令牌，禁 emoji，图标用 1.5px 线性内联 SVG。
 */
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { request } from '@newlifex/cube-vue/core/utils/request'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import RingGauge from '@/layouts/StardustLayout/components/RingGauge.vue'
import EChart from '@/components/EChart.vue'
import {
  buildResourceGauges,
  buildHealthChecks,
  buildRuntimeSpecs,
  buildNodeTimeline,
  nodeStatus,
  resourceWarning,
  relTime,
  absTime,
  type NodeRow,
  type DeployAppRow,
  type CommandRow,
} from './nodeDetail.model'

const route = useRoute()
const router = useRouter()

const loading = ref(true)
const node = ref<NodeRow | null>(null)
const deployApps = ref<DeployAppRow[]>([])
const commands = ref<CommandRow[]>([])
const tab = ref('overview')
const selectedCmd = ref('')
const running = ref(false)
const cmdOutput = ref('')

const tabs: { key: string; label: string }[] = [
  { key: 'overview', label: '概览' },
  { key: 'apps', label: '应用' },
  { key: 'perf', label: '性能' },
  { key: 'runtime', label: '运行时' },
  { key: 'cmd', label: '命令' },
  { key: 'history', label: '历史' },
]

const nodeId = computed(() => Number(route.params.id) || 0)
const now = ref(Date.now())

const status = computed(() => (node.value ? nodeStatus(node.value, now.value) : { key: 'offline' as const, text: '未知' }))
const pillStatus = computed(() => {
  const k = status.value.key
  return k === 'online' ? 'online' : k === 'warn' ? 'warning' : 'offline'
})
const gauges = computed(() => (node.value ? buildResourceGauges(node.value) : []))
const checks = computed(() => (node.value ? buildHealthChecks(node.value, now.value) : []))
const specs = computed(() => (node.value ? buildRuntimeSpecs(node.value) : []))
const timeline = computed(() => (node.value ? buildNodeTimeline(node.value, now.value) : []))
const warning = computed(() => (node.value ? resourceWarning(node.value) : ''))

const title = computed(() => String(node.value?.Name ?? node.value?.name ?? '节点详情'))
const ip = computed(() => String(node.value?.IP ?? node.value?.iP ?? node.value?.ip ?? '—'))
const zone = computed(() => String(node.value?.zone ?? node.value?.Zone ?? ''))
const subText = computed(() => {
  const parts = [ip.value]
  if (node.value?.runtime) parts.push(String(node.value.runtime))
  if (node.value?.Version ?? node.value?.ver) parts.push('Agent ' + String(node.value.Version ?? node.value.ver))
  return parts.join(' · ')
})

function statusColor(k: 'online' | 'warn' | 'offline'): string {
  if (k === 'online') return 'var(--el-color-success)'
  if (k === 'warn') return 'var(--el-color-warning)'
  return 'var(--el-color-danger)'
}

/** 资源迷你趋势：真实数据无时序时留空（不编造曲线） */
function sparkPath(points: number[]): string {
  if (points.length < 2) return ''
  const max = Math.max(...points, 1)
  const min = Math.min(...points, 0)
  const span = max - min || 1
  return points
    .map((p, i) => {
      const x = (i / (points.length - 1)) * 200
      const y = 40 - ((p - min) / span) * 34 - 3
      return (i === 0 ? 'M ' : 'L ') + x.toFixed(1) + ' ' + y.toFixed(1)
    })
    .join(' ')
}

/** 性能双序列图（真实数据源缺失时为待接入态） */
const perfSeries = computed<number[][]>(() => [[], []])

const perfOption = computed<Record<string, unknown>>(() => {
  const cs = getComputedStyle(document.documentElement)
  const brand = cs.getPropertyValue('--sd-color-brand').trim() || '#7C6BF5'
  const cyan = cs.getPropertyValue('--el-color-info').trim() || '#5BB8FF'
  const split = cs.getPropertyValue('--sd-border-lighter').trim() || 'rgba(255,255,255,0.08)'
  const txt = cs.getPropertyValue('--sd-text-secondary').trim() || 'oklch(0.58 0.014 240)'
  return {
    color: [brand, cyan],
    tooltip: { trigger: 'axis' },
    legend: { data: ['CPU', '内存'], textStyle: { color: txt, fontSize: 11 }, right: 8, top: 0 },
    grid: { left: 44, right: 16, top: 34, bottom: 24 },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: perfSeries.value[0].map((_, i) => String(i + 1)),
      axisLine: { lineStyle: { color: split } },
      axisLabel: { color: txt, fontSize: 10 },
    },
    yAxis: {
      type: 'value',
      max: 100,
      axisLine: { show: false },
      splitLine: { lineStyle: { color: split } },
      axisLabel: { color: txt, fontSize: 10, formatter: '{value}%' },
    },
    series: [
      { name: 'CPU', type: 'line', smooth: true, showSymbol: false, data: perfSeries.value[0], lineStyle: { color: brand, width: 2 } },
      { name: '内存', type: 'line', smooth: true, showSymbol: false, data: perfSeries.value[1], lineStyle: { color: cyan, width: 2 } },
    ],
  }
})

async function reload(): Promise<void> {
  loading.value = true
  now.value = Date.now()
  try {
    const safe = (url: string, params: Record<string, unknown>) => request({ url, method: 'GET', params }).catch(() => null)
    const [dr, cr, cmr] = await Promise.all([
      safe('/Nodes/Node', { PageSize: 200 }),
      safe('/Deployment/AppDeployNode', { nodeId: nodeId.value, PageSize: 200 }),
      safe('/Nodes/NodeCommand', { nodeId: nodeId.value, PageSize: 200 }),
    ])
    const list = (dr?.data ?? []) as NodeRow[]
    node.value = list.find((n) => Number(n.Id ?? n.id) === nodeId.value) ?? null
    deployApps.value = ((cr?.data ?? []) as Record<string, unknown>[]).map((r) => ({
      id: Number(r.Id ?? r.id ?? 0),
      appId: Number(r.AppId ?? r.appId ?? 0),
      appName: String(r.AppName ?? r.appName ?? '—'),
      enable: r.Enable !== false,
    }))
    commands.value = ((cmr?.data ?? []) as Record<string, unknown>[]).map((r) => ({
      id: Number(r.Id ?? r.id ?? 0),
      name: String(r.Name ?? r.name ?? ''),
      role: String(r.Role ?? r.role ?? ''),
      status: Number(r.Status ?? r.status ?? 0),
      result: String(r.Result ?? r.result ?? ''),
    }))
  } catch (e: unknown) {
    ElMessage.error(e instanceof Error ? e.message : '加载节点详情失败')
  } finally {
    loading.value = false
  }
}

async function runCommand(): Promise<void> {
  if (!selectedCmd.value) {
    ElMessage.warning('请先选择指令')
    return
  }
  running.value = true
  try {
    await request({ url: '/Nodes/NodeCommand/Send', method: 'POST', data: { nodeId: nodeId.value, command: selectedCmd.value } })
    cmdOutput.value = '指令已下发：' + selectedCmd.value
  } catch (e: unknown) {
    cmdOutput.value = '指令下发失败：' + (e instanceof Error ? e.message : String(e))
  } finally {
    running.value = false
  }
}

function onShell(): void {
  ElMessage.info('Shell 终端待接入')
}
function onRestart(): void {
  ElMessage.info('重启 Agent 待接入')
}
async function onRemove(): Promise<void> {
  try {
    await ElMessageBox.confirm('确认从集群摘除节点 ' + title.value + '？', '摘除节点', {
      type: 'warning',
      confirmButtonText: '确认摘除',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  ElMessage.info('摘除节点待接入')
}
function goApps(): void {
  router.push('/stardust/nodes')
}

onMounted(reload)
watch(() => route.params.id, () => void reload())
watch(
  () => route.params.id,
  () => void reload(),
)
</script>

<template>
  <div class="nd">
    <!-- 面包屑 -->
    <nav class="nd-crumb">
      <button
        type="button"
        class="nd-crumb-link"
        @click="goApps"
      >
        节点
      </button>
      <span class="nd-crumb-sep">/</span>
      <span class="nd-crumb-cur">{{ title }}</span>
    </nav>

    <!-- 标题行 -->
    <header class="nd-header">
      <div class="nd-head-l">
        <div
          class="nd-title-row"
          data-spec="nd-title"
        >
          <h1 class="nd-title">
            {{ title }}
          </h1>
          <StatusPill :status="pillStatus">
            {{ status.text }}
          </StatusPill>
          <span
            v-if="zone"
            class="nd-zone"
          >{{ zone }}</span>
        </div>
        <p
          class="nd-sub"
          data-spec="nd-sub"
        >
          {{ subText }}
        </p>
      </div>
      <div class="nd-actions">
        <button
          type="button"
          class="nd-btn"
          data-spec="nd-act-shell"
          @click="onShell"
        >
          <svg
            class="nd-ico"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="m4 17 6-6-6-6" />
            <path d="M12 19h8" />
          </svg>
          Shell
        </button>
        <button
          type="button"
          class="nd-btn"
          data-spec="nd-act-restart"
          @click="onRestart"
        >
          <svg
            class="nd-ico"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="M21 12a9 9 0 1 1-2.64-6.36" />
            <path d="M21 3v6h-6" />
          </svg>
          重启 Agent
        </button>
        <button
          type="button"
          class="nd-btn is-danger"
          data-spec="nd-act-remove"
          @click="onRemove"
        >
          摘除
        </button>
      </div>
    </header>

    <!-- Tab 条 -->
    <div
      class="nd-tabs"
      data-spec="nd-tabs"
    >
      <button
        v-for="t in tabs"
        :key="t.key"
        type="button"
        class="nd-tab"
        :class="{ 'is-active': tab === t.key }"
        :data-spec="'nd-tab-' + t.key"
        @click="tab = t.key"
      >
        {{ t.label }}
      </button>
    </div>

    <div
      v-if="loading && !node"
      class="nd-empty"
      data-spec="nd-empty"
    >
      加载中…
    </div>

    <div
      v-else-if="!node"
      class="nd-empty"
      data-spec="nd-empty"
    >
      节点不存在或已被移除
    </div>

    <!-- 概览 -->
    <template v-else-if="tab === 'overview'">
      <div
        class="nd-three"
        data-spec="nd-gauges"
      >
        <section
          v-for="g in gauges"
          :key="g.key"
          class="nd-panel nd-gauge-panel"
          data-spec="nd-gauge"
        >
          <div class="nd-panel-head">
            <span class="nd-panel-title">{{ g.label }}</span>
            <span class="nd-panel-sub">{{ g.sub }}</span>
          </div>
          <div class="nd-gauge-body">
            <RingGauge
              :value="g.value"
              :label="g.label"
              :data-spec="'nd-ring-' + g.key"
            />
            <div class="nd-gauge-side">
              <div class="nd-dim">
                {{ g.detail }}
              </div>
              <svg
                v-if="sparkPath(g.spark)"
                class="nd-spark"
                viewBox="0 0 200 40"
                preserveAspectRatio="none"
                aria-hidden="true"
              >
                <path
                  :d="sparkPath(g.spark)"
                  fill="none"
                  stroke="var(--el-color-primary)"
                  stroke-width="1.5"
                  vector-effect="non-scaling-stroke"
                />
              </svg>
              <div
                v-else
                class="nd-dim is-faint"
              >
                无时序采样
              </div>
            </div>
          </div>
        </section>
      </div>

      <div
        v-if="warning"
        class="nd-warn"
        data-spec="nd-warn"
      >
        资源预警：{{ warning }}
      </div>

      <div class="nd-two">
        <section class="nd-panel">
          <div class="nd-panel-head">
            <span class="nd-panel-title">运行应用</span>
            <span class="nd-panel-sub">{{ deployApps.length }} 个应用实例</span>
          </div>
          <div class="nd-panel-body is-flush">
            <div
              v-if="deployApps.length === 0"
              class="nd-empty is-inline"
              data-spec="nd-apps-empty"
            >
              该节点暂无部署应用
            </div>
            <table
              v-else
              class="nd-table"
            >
              <thead>
                <tr>
                  <th data-spec="nd-apps-th">
                    应用
                  </th>
                  <th
                    class="num"
                    data-spec="nd-apps-th"
                  >
                    应用 ID
                  </th>
                  <th data-spec="nd-apps-th">
                    状态
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="a in deployApps"
                  :key="a.id"
                  data-spec="nd-apps-row"
                >
                  <td class="link">
                    {{ a.appName }}
                  </td>
                  <td class="num mono">
                    {{ a.appId || '—' }}
                  </td>
                  <td>
                    <span class="nd-dim">{{ a.enable ? '启用' : '禁用' }}</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section class="nd-panel">
          <div class="nd-panel-head">
            <span class="nd-panel-title">最近操作</span>
            <span class="nd-panel-sub">{{ timeline.length }} 条</span>
          </div>
          <div class="nd-panel-body">
            <div
              v-if="timeline.length === 0"
              class="nd-empty is-inline"
              data-spec="nd-timeline-empty"
            >
              暂无记录
            </div>
            <div
              v-for="(it, i) in timeline"
              v-else
              :key="i"
              class="nd-tl"
              data-spec="nd-timeline"
            >
              <span
                class="nd-tl-dot"
                :class="'is-' + it.tone"
              />
              <span class="nd-tl-title">{{ it.title }}</span>
              <span class="nd-tl-time">{{ it.time }}</span>
            </div>
          </div>
        </section>
      </div>

      <div
        class="nd-section-label"
        data-spec="nd-section-label"
      >
        健康检查
      </div>
      <section class="nd-panel">
        <div class="nd-panel-body is-flush">
          <table class="nd-table">
            <thead>
              <tr>
                <th data-spec="nd-check-th">
                  检查项
                </th>
                <th data-spec="nd-check-th">
                  结果
                </th>
                <th data-spec="nd-check-th">
                  值
                </th>
                <th data-spec="nd-check-th">
                  阈值
                </th>
                <th data-spec="nd-check-th">
                  最近
                </th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="c in checks"
                :key="c.name"
                data-spec="nd-check-row"
              >
                <td>{{ c.name }}</td>
                <td>
                  <span
                    class="nd-dot"
                    :style="{
                      background: c.ok === null
                        ? 'var(--el-text-color-placeholder)'
                        : c.ok
                          ? 'var(--el-color-success)'
                          : 'var(--el-color-warning)',
                    }"
                  />{{ c.ok === null ? '无数据' : c.ok ? '通过' : '告警' }}
                </td>
                <td class="mono">
                  {{ c.value }}
                </td>
                <td class="mono nd-dim">
                  {{ c.threshold }}
                </td>
                <td class="nd-dim">
                  {{ c.at }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>

    <!-- 应用 -->
    <section
      v-else-if="tab === 'apps'"
      class="nd-panel"
    >
      <div class="nd-panel-head">
        <span class="nd-panel-title">节点承载的全部实例</span>
        <span class="nd-panel-sub">{{ deployApps.length }} 项</span>
      </div>
      <div class="nd-panel-body is-flush">
        <div
          v-if="deployApps.length === 0"
          class="nd-empty is-inline"
          data-spec="nd-apps-empty"
        >
          该节点暂无部署应用
        </div>
        <table
          v-else
          class="nd-table"
        >
          <thead>
            <tr>
              <th>应用</th>
              <th class="num">
                应用 ID
              </th>
              <th>状态</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="a in deployApps"
              :key="'t-' + a.id"
              data-spec="nd-apps-row"
            >
              <td class="link">
                {{ a.appName }}
              </td>
              <td class="num mono">
                {{ a.appId || '—' }}
              </td>
              <td class="nd-dim">
                {{ a.enable ? '启用' : '禁用' }}
              </td>
              <td>
                <button
                  type="button"
                  class="nd-link-btn"
                  @click="router.push('/stardust/apps')"
                >
                  查看应用
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- 性能 -->
    <section
      v-else-if="tab === 'perf'"
      class="nd-panel"
      data-spec="nd-perf"
    >
      <div class="nd-panel-head">
        <span class="nd-panel-title">CPU · 内存</span>
        <span class="nd-panel-sub">时序采样</span>
      </div>
      <div class="nd-panel-body">
        <div
          v-if="perfSeries[0].length === 0"
          class="nd-empty is-inline"
          data-spec="nd-perf-empty"
        >
          节点时序采样待接入（后端暂无节点级指标序列）
        </div>
        <EChart
          v-else
          :option="perfOption"
          height="240px"
        />
      </div>
    </section>

    <!-- 运行时 -->
    <section
      v-else-if="tab === 'runtime'"
      class="nd-panel"
      data-spec="nd-runtime"
    >
      <div class="nd-panel-head">
        <span class="nd-panel-title">运行时规格</span>
        <span class="nd-panel-sub">来自节点记录</span>
      </div>
      <div class="nd-panel-body">
        <dl class="nd-dl">
          <template
            v-for="s in specs"
            :key="s.k"
          >
            <dt data-spec="nd-spec-k">
              {{ s.k }}
            </dt>
            <dd
              :class="{ mono: s.mono }"
              data-spec="nd-spec-v"
            >
              {{ s.v }}
            </dd>
          </template>
        </dl>
      </div>
    </section>

    <!-- 命令 -->
    <section
      v-else-if="tab === 'cmd'"
      class="nd-panel"
      data-spec="nd-cmd"
    >
      <div class="nd-panel-head">
        <span class="nd-panel-title">远程命令</span>
        <span class="nd-panel-sub">{{ commands.length }} 条可用指令</span>
      </div>
      <div class="nd-panel-body">
        <div class="nd-cmd-bar">
          <select
            v-model="selectedCmd"
            class="nd-select"
            data-spec="nd-cmd-select"
          >
            <option value="">
              选择指令
            </option>
            <option
              v-for="c in commands"
              :key="c.id"
              :value="c.name"
            >
              {{ c.name }}
            </option>
          </select>
          <button
            type="button"
            class="nd-btn is-primary"
            data-spec="nd-cmd-run"
            :disabled="running"
            @click="runCommand"
          >
            执行
          </button>
        </div>
        <pre
          class="nd-term"
          data-spec="nd-cmd-output"
        >{{ cmdOutput || '等待指令下发…' }}</pre>
      </div>
    </section>

    <!-- 历史 -->
    <section
      v-else
      class="nd-panel"
      data-spec="nd-history"
    >
      <div class="nd-panel-head">
        <span class="nd-panel-title">节点历史</span>
        <span class="nd-panel-sub">{{ timeline.length }} 条</span>
      </div>
      <div class="nd-panel-body">
        <div
          v-if="timeline.length === 0"
          class="nd-empty is-inline"
          data-spec="nd-timeline-empty"
        >
          暂无记录
        </div>
        <div
          v-for="(it, i) in timeline"
          v-else
          :key="'h-' + i"
          class="nd-tl"
          data-spec="nd-timeline"
        >
          <span
            class="nd-tl-dot"
            :class="'is-' + it.tone"
          />
          <span class="nd-tl-title">{{ it.title }}</span>
          <span class="nd-tl-time">{{ it.time }}</span>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.nd {
  display: flex;
  flex-direction: column;
  gap: 14px;
  overflow-y: auto;
}
.nd-crumb {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.nd-crumb-link {
  color: var(--el-color-primary);
}
.nd-crumb-sep {
  color: var(--el-text-color-placeholder);
}
.nd-crumb-cur {
  color: var(--el-text-color-regular);
}
.nd-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}
.nd-title-row {
  display: flex;
  align-items: center;
  gap: 12px;
}
.nd-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.nd-zone {
  padding: 2px 8px;
  border-radius: 4px;
  background: color-mix(in srgb, var(--el-color-info) 16%, transparent);
  color: var(--el-color-info);
  font-size: 11.5px;
}
.nd-sub {
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-regular);
}
.nd-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}
.nd-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  height: 32px;
  padding: 0 12px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  font-size: 12.5px;
  color: var(--el-text-color-regular);
  transition: border-color 120ms, color 120ms;
}
.nd-btn:hover:not(:disabled) {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.nd-btn.is-danger:hover {
  border-color: var(--el-color-danger);
  color: var(--el-color-danger);
}
.nd-btn.is-primary {
  background: var(--el-color-primary);
  border-color: var(--el-color-primary);
  color: var(--el-color-white);
}
.nd-btn:disabled {
  opacity: 0.6;
}
.nd-ico {
  width: 13px;
  height: 13px;
}
.nd-tabs {
  display: flex;
  gap: 2px;
  padding: 3px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color-light);
  background: color-mix(in srgb, var(--el-bg-color-overlay) 70%, transparent);
  align-self: flex-start;
}
.nd-tab {
  padding: 6px 14px;
  border-radius: 6px;
  font-size: 12.5px;
  color: var(--el-text-color-regular);
  transition: background-color 120ms cubic-bezier(0.16, 1, 0.3, 1), color 120ms;
}
.nd-tab.is-active {
  background: var(--el-color-primary);
  color: var(--el-color-white);
}
.nd-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  overflow: hidden;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
}
.nd-panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.nd-panel-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}
.nd-panel-sub {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}
.nd-panel-body {
  padding: 16px;
}
.nd-panel-body.is-flush {
  padding: 0;
}
.nd-three {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}
.nd-gauge-panel .nd-gauge-body {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
}
.nd-gauge-side {
  flex: 1;
  min-width: 0;
}
.nd-dim {
  font-size: 11.5px;
  color: var(--el-text-color-secondary);
}
.nd-dim.is-faint {
  color: var(--el-text-color-placeholder);
}
.nd-spark {
  display: block;
  width: 200px;
  height: 40px;
  margin-top: 6px;
}
.nd-warn {
  padding: 10px 16px;
  border-radius: 10px;
  border: 1px solid color-mix(in srgb, var(--el-color-warning) 40%, transparent);
  background: color-mix(in srgb, var(--el-color-warning) 12%, transparent);
  font-size: 12px;
  color: var(--el-color-warning);
}
.nd-two {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 16px;
  align-items: start;
}
.nd-section-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-regular);
}
.nd-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12.5px;
}
.nd-table th {
  padding: 10px 16px;
  text-align: left;
  font-size: 11px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  background: color-mix(in srgb, var(--el-bg-color-overlay) 60%, transparent);
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.nd-table th.num,
.nd-table td.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.nd-table td {
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-regular);
}
.nd-table tbody tr:last-child td {
  border-bottom: none;
}
.nd-table td.link {
  color: var(--el-text-color-primary);
  font-weight: 500;
}
.nd-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 999px;
  margin-right: 6px;
}
.nd-tl {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.nd-tl:last-child {
  border-bottom: none;
}
.nd-tl-dot {
  width: 6px;
  height: 6px;
  border-radius: 999px;
  flex-shrink: 0;
}
.nd-tl-dot.is-brand {
  background: var(--el-color-primary);
}
.nd-tl-dot.is-success {
  background: var(--el-color-success);
}
.nd-tl-dot.is-warning {
  background: var(--el-color-warning);
}
.nd-tl-dot.is-danger {
  background: var(--el-color-danger);
}
.nd-tl-dot.is-info {
  background: var(--el-color-info);
}
.nd-tl-title {
  flex: 1;
  min-width: 0;
  font-size: 12.5px;
  color: var(--el-text-color-primary);
}
.nd-tl-time {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}
.nd-dl {
  display: grid;
  grid-template-columns: 160px 1fr;
  gap: 8px 16px;
  margin: 0;
}
.nd-dl dt {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.nd-dl dd {
  margin: 0;
  font-size: 12.5px;
  color: var(--el-text-color-primary);
}
.nd-cmd-bar {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
}
.nd-select {
  height: 32px;
  padding: 0 10px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12.5px;
}
.nd-term {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: var(--el-fill-color-light);
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-size: 12px;
  color: var(--el-text-color-regular);
  overflow: auto;
  min-height: 96px;
  white-space: pre-wrap;
}
.nd-empty {
  padding: 32px 12px;
  text-align: center;
  font-size: 12.5px;
  color: var(--el-text-color-secondary);
}
.nd-empty.is-inline {
  padding: 24px 12px;
}
.nd-link-btn {
  font-size: 12px;
  color: var(--el-color-primary);
}
</style>
