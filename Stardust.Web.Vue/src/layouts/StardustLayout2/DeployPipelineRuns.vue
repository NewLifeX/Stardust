<script setup lang="ts">
/**
 * DeployPipelineRuns：发布页「流水线运行 · 按节点的部署结果」，对应 ShipPanel 原模板的最后一块（2026-10-28 拆分）。
 * 纯 UI：只收 props、只发 select(runId)；取步骤的请求在 useDeployData，stages 与 view 由容器算好传入。
 * 格子、四段进度的样式常量在 deployPipeline.styles.ts；图例与选中行都由传入的数据在本组件内推出。
 */
import { computed } from 'vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import { CELL_BASE, CELL_CLASS, DOT_BASE, DOT_CLASS, STEP_BASE, STEP_CLASS } from './deployPipeline.styles.ts';
import type { PipelineView, StageItem } from './deployPipeline.styles.ts';
import type { NodeCell, ShipRunRow } from './ship.logic.ts';

const props = defineProps<{
  runRows: ShipRunRow[];
  /** 当前选中的运行 Id，没有选中为 null */
  selectedRunId: number | null;
  /** 四段进度；没有选中运行时为空数组，不显示 */
  stages: StageItem[];
  view: PipelineView | null;
}>();

const emit = defineEmits<{ select: [runId: number] }>();

const selectedRow = computed(() => props.runRows.find((r) => r.id === props.selectedRunId));

// 图例：只列出格子里出现过的状态，每种一次，按首次出现的顺序
const legend = computed(() => {
  const seen = new Map<NodeCell, string>();
  for (const it of props.view?.items ?? []) if (!seen.has(it.cell)) seen.set(it.cell, it.label);
  return [...seen.entries()];
});
</script>

<template>
  <Card data-spec="ship-pipeline">
    <h3 :class="H3">
      流水线运行 · 按节点的部署结果
      <Tag
        v-if="selectedRow"
        :tone="selectedRow.tone"
      >
        {{ selectedRow.statusLabel }}
      </Tag>
    </h3>

    <div
      v-if="stages.length"
      class="mb-[18px] mt-[6px] flex"
      data-spec="ship-stages"
    >
      <div
        v-for="(s, i) in stages"
        :key="s.key"
        :class="[STEP_BASE, STEP_CLASS[s.state]]"
      >
        <div :class="[DOT_BASE, DOT_CLASS[s.state]]">
          <KIcon
            v-if="s.state === 'done'"
            name="Check"
            :size="10"
          />
          <template v-else>
            {{ i + 1 }}
          </template>
        </div>
        {{ s.label }}
      </div>
    </div>

    <div class="mb-[10px] text-[11.5px] text-[var(--el-text-color-secondary)]">
      {{ view?.note }}
    </div>

    <div
      v-if="view?.allSkipped"
      class="mb-3 rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-warning)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-warning)_10%,transparent)] px-3 py-2 text-[12px] text-[var(--el-color-warning)]"
      data-spec="ship-allskipped"
    >
      无节点实际部署：这次运行的所有部署步骤都被跳过了（后端此时可能仍把运行标为成功）。
    </div>

    <div
      v-if="view?.items.length"
      class="grid grid-cols-8 gap-[5px] max-[1080px]:grid-cols-6"
    >
      <div
        v-for="(it, i) in view.items"
        :key="it.key"
        data-spec="ship-cell"
        :title="`${it.name}${it.ip ? ' · ' + it.ip : ''} · ${it.label}${it.message ? '：' + it.message : ''}`"
        :class="[CELL_BASE, CELL_CLASS[it.cell]]"
      >
        {{ i + 1 }}
      </div>
    </div>

    <div
      class="mt-[10px] flex flex-wrap items-center gap-x-3 gap-y-1 text-[11.5px] text-[var(--el-text-color-secondary)]"
      data-spec="ship-legend"
    >
      <span
        v-for="[cell, label] in legend"
        :key="cell"
        class="inline-flex items-center gap-[5px]"
      >
        <i
          class="inline-block h-[9px] w-[9px] rounded-[2px] border"
          :class="CELL_CLASS[cell]"
        />{{ label }}
      </span>
      <span class="flex-1" />
      <span
        class="inline-flex items-center gap-1"
        title="后端没有回滚接口；回滚可以直接在版本列表里应用旧版本"
      >
        <KIcon
          name="Rotate"
          :size="11"
        />一键回滚 <Tag>待接入</Tag>
      </span>
    </div>

    <div class="-mx-[18px] -mb-4 mt-4 overflow-hidden rounded-b-[inherit] border-t border-[var(--el-border-color-light)]">
      <table
        class="w-full border-collapse"
        data-spec="ship-runs"
      >
        <thead>
          <tr>
            <th :class="TH">
              版本
            </th>
            <th :class="TH">
              流水线
            </th>
            <th :class="TH">
              来源
            </th>
            <th :class="TH">
              操作人
            </th>
            <th :class="TH">
              时间
            </th>
            <th :class="TH">
              状态
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="r in runRows"
            :key="r.key"
            data-spec="ship-run"
            class="group cursor-pointer transition-colors duration-[120ms] hover:bg-[var(--el-bg-color-overlay)]"
            :class="r.id === selectedRunId ? 'bg-[color-mix(in_oklab,var(--el-color-primary)_10%,transparent)]' : ''"
            @click="emit('select', r.id)"
          >
            <td :class="TD">
              <b class="font-medium text-[var(--el-text-color-primary)]">{{ r.version }}</b>
            </td>
            <td :class="[TD, 'text-[var(--el-text-color-secondary)]']">
              {{ r.pipeline }}
            </td>
            <td :class="[TD, 'text-[var(--el-text-color-secondary)]']">
              {{ r.trigger }}
            </td>
            <td :class="[TD, 'text-[var(--el-text-color-secondary)]']">
              {{ r.author }}
            </td>
            <td :class="[TD, 'font-mono tabular-nums text-[var(--el-text-color-secondary)]']">
              {{ r.time }}
            </td>
            <td :class="TD">
              <Tag :tone="r.tone">
                {{ r.statusLabel }}
              </Tag>
            </td>
          </tr>
          <tr v-if="!runRows.length">
            <td
              colspan="6"
              class="p-5 text-[12.5px] text-[var(--el-text-color-secondary)]"
              data-spec="ship-runs-empty"
            >
              暂无流水线运行记录
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </Card>
</template>
