<script setup lang="ts">
/**
 * 调用矩阵（设计稿 src/page-workspace.jsx · TopologyMatrix）
 * 行 = 调用方，列 = 被调方；色块深浅对应 QPS；有边才着色并显示数值，无边显示「·」。
 * 颜色只用 --el-* 令牌（color-mix 按比例调透明度），禁止硬编码。
 */
import type { WsMatrix } from './overview.model'

defineProps<{ matrix: WsMatrix }>()

function cellStyle(ratio: number): Record<string, string> {
  const pct = Math.round(14 + ratio * 50)
  return { background: 'color-mix(in oklab, var(--el-color-primary) ' + pct + '%, transparent)' }
}
</script>

<template>
  <div
    class="sd-mx"
    data-spec="ws-matrix"
  >
    <table
      v-if="matrix.rows.length > 0"
      class="sd-mx-table"
    >
      <thead>
        <tr>
          <th class="sd-mx-rowhead">
            调用方 ↓ 被调方
          </th>
          <th
            v-for="c in matrix.cols"
            :key="c.key"
            class="sd-mx-head"
            data-spec="ws-mx-head"
            :title="c.key"
          >
            {{ c.label }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="r in matrix.rows"
          :key="r.key"
          data-spec="ws-mx-row"
        >
          <th
            class="sd-mx-rowhead"
            data-spec="ws-mx-rowhead"
            :title="r.key"
          >
            {{ r.label }}
          </th>
          <td
            v-for="(c, i) in r.cells"
            :key="i"
            class="sd-mx-cell"
            :class="{ hit: c.hit }"
            data-spec="ws-mx-cell"
            :style="c.hit ? cellStyle(c.ratio) : undefined"
          >
            {{ c.hit ? c.value.toLocaleString() : '·' }}
          </td>
        </tr>
      </tbody>
    </table>
    <p
      v-else
      class="sd-mx-empty"
      data-spec="ws-mx-empty"
    >
      调用关系待接入
    </p>
  </div>
</template>

<style scoped>
.sd-mx {
  overflow-x: auto;
}
.sd-mx-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.sd-mx-table th,
.sd-mx-table td {
  padding: 7px 10px;
  text-align: center;
  white-space: nowrap;
}
.sd-mx-rowhead {
  text-align: left;
  font-weight: 500;
  color: var(--el-text-color-regular);
  font-size: 11.5px;
}
.sd-mx-head {
  font-weight: 500;
  color: var(--el-text-color-secondary);
  font-size: 11.5px;
}
.sd-mx-cell {
  font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-placeholder);
  border-radius: 4px;
}
.sd-mx-cell.hit {
  color: var(--el-text-color-primary);
}
.sd-mx-empty {
  padding: 24px 12px;
  text-align: center;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
