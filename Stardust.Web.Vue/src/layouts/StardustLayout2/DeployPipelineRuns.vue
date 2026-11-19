<script setup lang="ts">
/**
 * DeployPipelineRuns：发布页「流水线运行 · 按节点的部署结果」，对应 ShipPanel 原模板的最后一块（2026-10-28 拆分；含一键回滚）。
 * 纯 UI：只收 props、只发 select(runId)/rollback；取步骤的请求在 useDeployData，stages 与 view 由容器算好传入。
 * F09 一键回滚：头部按钮，候选版本挑选与 Guard 确认在容器（rollbackCandidates + rollbackNotice）。
 * 格子、四段进度的样式常量在 deployPipeline.styles.ts；图例与选中行都由传入的数据在本组件内推出。
 * CARD-DEP-02（2026-11-07）：运行表上方加筛选条（状态下拉 7 项 + 时间区间 + 重置，逐字对齐旧壳 0-6 中文），
 * 行尾加「重跑/详情」操作（确认与请求在容器）；6 列不动，行尾加操作列。
 */
import { computed } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import { CELL_BASE, CELL_CLASS, DOT_BASE, DOT_CLASS, STEP_BASE, STEP_CLASS } from './deployPipeline.styles.ts';
import type { PipelineView, StageItem } from './deployPipeline.styles.ts';
import { isRunFilterActive, RUN_STATUS_FILTERS } from './ship.logic.ts';
import type { NodeCell, RunFilter, ShipRunRow } from './ship.logic.ts';

const props = defineProps<{
  runRows: ShipRunRow[];
  /** 当前选中的运行 Id，没有选中为 null */
  selectedRunId: number | null;
  /** 四段进度；没有选中运行时为空数组，不显示 */
  stages: StageItem[];
  view: PipelineView | null;
  /** 有可回滚的候选版本才可点；没有候选或 busy 时置灰并在 title 说明 */
  rollbackDisabled: boolean;
  rollbackTitle: string;
  /** 回滚请求进行中 */
  rollingBack: boolean;
  /** 运行筛选（容器持有，静默重取语义与 5s 轮询共存） */
  runFilter: RunFilter;
  /** 正在重跑的行（busyKey 形如 rerun:{id}）；非空时行操作禁用（AGENTS.md §11） */
  rerunKey: string;
}>();

const emit = defineEmits<{
  select: [runId: number];
  rollback: [];
  rerun: [row: ShipRunRow];
  detail: [row: ShipRunRow];
  filterChange: [patch: Partial<RunFilter>];
  filterReset: [];
}>();

const selectedRow = computed(() => props.runRows.find((r) => r.id === props.selectedRunId));

// 图例：只列出格子里出现过的状态，每种一次，按首次出现的顺序
const legend = computed(() => {
  const seen = new Map<NodeCell, string>();
  for (const it of props.view?.items ?? []) if (!seen.has(it.cell)) seen.set(it.cell, it.label);
  return [...seen.entries()];
});

const filterActive = computed(() => isRunFilterActive(props.runFilter));

function onStatusChange(e: Event): void {
  emit('filterChange', { status: Number((e.target as HTMLSelectElement).value) || 0 });
}
function onDateStart(e: Event): void {
  emit('filterChange', { dtStart: (e.target as HTMLInputElement).value });
}
function onDateEnd(e: Event): void {
  emit('filterChange', { dtEnd: (e.target as HTMLInputElement).value });
}
</script>

<template>
  <Card data-spec="ship-pipeline">
    <div class="flex flex-wrap items-center gap-3">
      <h3 :class="H3">
        流水线运行 · 按节点的部署结果
        <Tag
          v-if="selectedRow"
          :tone="selectedRow.tone"
        >
          {{ selectedRow.statusLabel }}
        </Tag>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="default"
        :disabled="rollbackDisabled || rollingBack"
        :title="rollbackTitle"
        data-spec="ship-rollback"
        @click="emit('rollback')"
      >
        <template #icon>
          <span :class="rollingBack ? 'inline-flex animate-spin' : 'inline-flex'">
            <KIcon
              name="Rotate"
              :size="12"
            />
          </span>
        </template>
        {{ rollingBack ? '回滚中…' : '一键回滚' }}
      </Btn>
    </div>

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
    </div>

    <!-- CARD-DEP-02：筛选条（状态 0-6 七项 + 时间区间 + 重置），插在运行表上方 -->
    <div
      class="mb-[10px] flex flex-wrap items-center gap-2"
      data-spec="ship-runs-filter"
    >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">状态</span>
      <select
        :class="SELECT"
        :value="runFilter.status"
        data-spec="ship-runs-filter-status"
        aria-label="按状态筛选"
        @change="onStatusChange"
      >
        <option
          v-for="o in RUN_STATUS_FILTERS"
          :key="o.value"
          :value="o.value"
        >
          {{ o.label }}
        </option>
      </select>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">时间</span>
      <input
        type="date"
        :class="SELECT"
        :value="runFilter.dtStart"
        aria-label="开始日期"
        data-spec="ship-runs-filter-start"
        @change="onDateStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        :value="runFilter.dtEnd"
        aria-label="结束日期"
        data-spec="ship-runs-filter-end"
        @change="onDateEnd"
      >
      <Btn
        size="sm"
        variant="ghost"
        :disabled="!filterActive"
        :title="filterActive ? '清空状态与时间筛选，恢复全量' : '当前没有生效的筛选'"
        data-spec="ship-runs-filter-reset"
        @click="emit('filterReset')"
      >
        重置
      </Btn>
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
            <th :class="[TH, 'text-right']">
              操作
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
            <td :class="[TD, 'text-right']">
              <span
                class="inline-flex justify-end gap-1"
                @click.stop
              >
                <Btn
                  size="sm"
                  variant="ghost"
                  :disabled="!!rerunKey"
                  :title="rerunKey === `rerun:${r.id}` ? '重跑中…' : '基于本次运行重新触发流水线'"
                  data-spec="ship-rerun"
                  @click="emit('rerun', r)"
                >
                  <template #icon>
                    <span :class="rerunKey === `rerun:${r.id}` ? 'inline-flex animate-spin' : 'inline-flex'">
                      <KIcon
                        name="Rotate"
                        :size="12"
                      />
                    </span>
                  </template>
                  {{ rerunKey === `rerun:${r.id}` ? '重跑中…' : '重跑' }}
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="ship-runs-detail"
                  @click="emit('detail', r)"
                >
                  详情
                </Btn>
              </span>
            </td>
          </tr>
          <tr v-if="!runRows.length">
            <td
              colspan="7"
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
