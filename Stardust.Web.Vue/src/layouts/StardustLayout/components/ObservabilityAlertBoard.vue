<script setup lang="ts">
/**
 * 告警 Tab 看板（设计稿 src/page-observability.jsx · AlertsTab）
 * four-col 四卡（未处理/处理中/今日已恢复/静默中，值 28px/600 + badge）
 *  + 状态 chip 切换 + 8 列表格（级别/规则/对象/当前值/阈值/持续/触发时间/操作）
 *  + 操作按钮随状态变化 + 空态。数据源 /Monitors/AlarmRecord（fail-soft）。
 */
import { computed, onMounted, ref } from 'vue'
import { request } from '@newlifex/cube-vue/core/utils/request'
import EmptyState from '@/layouts/StardustLayout/components/EmptyState.vue'
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue'
import * as p from '../observabilityPanels.model'

type StateKey = 'pending' | 'ack' | 'resolved'

const rows = ref<Record<string, unknown>[]>([])
const state = ref<StateKey>('pending')

const stateChips: { key: StateKey; label: string }[] = [
  { key: 'pending', label: '未处理' },
  { key: 'ack', label: '处理中' },
  { key: 'resolved', label: '已恢复' },
]

async function load() {
  const r = await request({ url: '/Monitors/AlarmRecord', method: 'GET', params: { PageSize: 200 } }).catch(() => null)
  rows.value = (r?.data ?? []) as Record<string, unknown>[]
}
onMounted(() => void load())

const cards = computed(() => p.buildAlertCards(rows.value))
const counts = computed(() => p.alertStateCounts(rows.value))
const filtered = computed(() => p.toAlertRows(rows.value).filter((r) => r.state === state.value))
</script>

<template>
  <div class="oa-wrap">
    <div
      class="oa-cards"
      data-spec="obs-alert-cards"
    >
      <section
        v-for="c in cards"
        :key="c.label"
        class="oa-card"
        data-spec="obs-alert-card"
      >
        <div class="oa-card-top">
          <span class="oa-card-label">{{ c.label }}</span>
          <span class="oa-spacer" />
          <span
            class="oa-badge"
            :class="'is-' + c.tone"
          >
            <span class="oa-dot" />{{ c.badge }}
          </span>
        </div>
        <div
          class="oa-card-value"
          :class="'is-' + c.tone"
        >
          {{ c.value }}
        </div>
      </section>
    </div>

    <div
      class="oa-bar"
      data-spec="obs-alert-bar"
    >
      <button
        v-for="s in stateChips"
        :key="s.key"
        class="oa-chip"
        :class="{ 'is-active': state === s.key }"
        type="button"
        data-spec="obs-alert-chip"
        :data-state="s.key"
        @click="state = s.key"
      >
        {{ s.label }} <span class="oa-cnt">{{ counts[s.key] }}</span>
      </button>
      <span class="oa-spacer" />
      <button
        class="oa-btn"
        type="button"
        data-spec="obs-alert-new-rule"
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
        >
          <path d="M12 5v14M5 12h14" />
        </svg>
        新建规则
      </button>
    </div>

    <section class="oa-panel">
      <table
        class="oa-table"
        data-spec="obs-alert-table"
      >
        <thead>
          <tr>
            <th>级别</th>
            <th>规则</th>
            <th>对象</th>
            <th class="oa-num">
              当前值
            </th>
            <th class="oa-num">
              阈值
            </th>
            <th>持续</th>
            <th>触发时间</th>
            <th class="oa-ops">
              操作
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(r, i) in filtered"
            :key="i"
            data-spec="obs-alert-row"
          >
            <td>
              <StatusPill
                :status="r.level"
                :tone="r.tone"
              />
            </td>
            <td class="oa-mono">
              {{ r.rule }}
            </td>
            <td class="oa-link">
              {{ r.obj }}
            </td>
            <td class="oa-num is-danger">
              {{ r.val }}
            </td>
            <td class="oa-num oa-dim">
              {{ r.thr }}
            </td>
            <td class="oa-dim oa-mono">
              {{ r.dur }}
            </td>
            <td class="oa-dim oa-mono">
              {{ r.at }}
            </td>
            <td>
              <div class="oa-ops-row">
                <template v-if="state === 'pending'">
                  <button
                    class="oa-btn-sm"
                    type="button"
                    data-spec="obs-alert-ack"
                  >
                    确认
                  </button>
                  <button
                    class="oa-btn-sm"
                    type="button"
                    data-spec="obs-alert-silence"
                  >
                    静默
                  </button>
                </template>
                <button
                  v-if="state === 'ack'"
                  class="oa-btn-sm is-primary"
                  type="button"
                  data-spec="obs-alert-recover"
                >
                  恢复
                </button>
                <button
                  class="oa-btn-sm is-ghost"
                  type="button"
                  data-spec="obs-alert-trace"
                >
                  Trace
                </button>
                <button
                  class="oa-btn-sm is-ghost"
                  type="button"
                  data-spec="obs-alert-log"
                >
                  日志
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
      <div
        v-if="!filtered.length"
        class="oa-empty"
        data-spec="obs-alert-empty"
      >
        <EmptyState title="没有此状态的告警" />
      </div>
    </section>
  </div>
</template>

<style scoped>
.oa-wrap {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.oa-cards {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}
.oa-card {
  padding: 16px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-overlay);
}
.oa-card-top {
  display: flex;
  align-items: center;
  gap: 8px;
}
.oa-card-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.oa-spacer {
  flex: 1;
}
.oa-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 2px 8px;
  border: 1px solid currentColor;
  border-radius: 999px;
  font-size: 10px;
}
.oa-dot {
  width: 5px;
  height: 5px;
  border-radius: 999px;
  background: currentColor;
}
.oa-badge.is-danger {
  color: var(--el-color-danger);
}
.oa-badge.is-info {
  color: var(--el-color-info);
}
.oa-badge.is-success {
  color: var(--el-color-success);
}
.oa-badge.is-plain {
  color: var(--el-text-color-secondary);
}
.oa-card-value {
  margin-top: 4px;
  font-size: 28px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-primary);
}
.oa-card-value.is-danger {
  color: var(--el-color-danger);
}
.oa-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}
.oa-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  height: 28px;
  padding: 0 10px;
  border: 1px solid var(--el-border-color);
  border-radius: 999px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.oa-chip.is-active {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.oa-cnt {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  font-variant-numeric: tabular-nums;
}
.oa-btn {
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
.oa-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.oa-panel {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  background: var(--el-bg-color-overlay);
  overflow: hidden;
}
.oa-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.oa-table th {
  padding: 8px 12px;
  text-align: left;
  font-weight: 500;
  font-size: 11px;
  color: var(--el-text-color-secondary);
  border-bottom: 1px solid var(--el-border-color-lighter);
}
.oa-table td {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  color: var(--el-text-color-regular);
}
.oa-table tbody tr:hover {
  background: var(--el-fill-color-light);
}
.oa-num {
  text-align: right;
  font-variant-numeric: tabular-nums;
}
.oa-ops {
  width: 200px;
}
.oa-mono {
  font-family: 'JetBrains Mono', ui-monospace, monospace;
}
.oa-dim {
  color: var(--el-text-color-secondary);
}
.oa-link {
  color: var(--el-color-primary);
  cursor: pointer;
}
.is-danger {
  color: var(--el-color-danger);
}
.oa-ops-row {
  display: flex;
  align-items: center;
  gap: 4px;
}
.oa-btn-sm {
  height: 24px;
  padding: 0 8px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  background: var(--el-bg-color);
  color: var(--el-text-color-regular);
  font-size: 11px;
  cursor: pointer;
  transition: all 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}
.oa-btn-sm:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.oa-btn-sm.is-primary {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}
.oa-btn-sm.is-ghost {
  border-color: transparent;
  color: var(--el-text-color-secondary);
}
.oa-empty {
  padding: 24px;
}
</style>
