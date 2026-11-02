<script setup lang="ts">
/**
 * DeployVersionBar：发布页「当前生效版本」条，对应 ShipPanel 原模板的第 1 块（2026-10-28 拆分）。
 * 纯 UI：只收 props、只发 refresh；取数与刷新状态机在 useDeployData，节点统计由 deployView.logic 的 nodeStats 算好传入。
 * 刷新按钮遵守 AGENTS.md §11：刷新中或有操作进行中时禁用，刷新中图标旋转、文案变「刷新中…」。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import type { NodeStats } from './deployView.logic.ts';

defineProps<{
  /** 部署集当前生效版本，空显示「—」 */
  version: string;
  enable: boolean;
  autoPublish: boolean;
  multiVersion: boolean;
  stats: NodeStats;
  /** 节点列表加载失败：统计处显示失败而不是「没有启用的节点」 */
  nodesFailed: boolean;
  refreshing: boolean;
  /** 有节点操作或应用版本在进行：刷新按钮同样禁用 */
  busy: boolean;
}>();

const emit = defineEmits<{ refresh: [] }>();
</script>

<template>
  <Card data-spec="ship-current">
    <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
      <div>
        <div class="text-[11px] text-[var(--el-text-color-secondary)]">
          当前生效版本
        </div>
        <div
          class="font-mono text-[22px] font-semibold tabular-nums text-[var(--el-text-color-primary)]"
          data-spec="ship-current-version"
        >
          {{ version || '—' }}
        </div>
      </div>
      <div class="flex flex-wrap items-center gap-[6px]">
        <Tag :tone="enable ? 'ok' : 'default'">
          {{ enable ? '部署集已启用' : '部署集未启用' }}
        </Tag>
        <Tag :tone="autoPublish ? 'brand' : 'default'">
          自动发布{{ autoPublish ? '：开' : '：关' }}
        </Tag>
        <Tag v-if="multiVersion">
          多版本
        </Tag>
      </div>
      <span class="flex-1" />
      <div
        class="text-[12px] text-[var(--el-text-color-secondary)]"
        data-spec="ship-current-stats"
      >
        <template v-if="nodesFailed">
          节点加载失败
        </template>
        <template v-else-if="!stats.total">
          没有启用的节点
        </template>
        <template v-else>
          {{ stats.total }} 个启用节点：
          <b class="font-medium text-[var(--el-color-success)]">{{ stats.same }}</b> 个版本一致
          <span
            v-if="stats.diff"
            class="ml-3"
          >
            <b
              data-spec="ship-diff-count"
              :class="multiVersion ? 'font-medium' : 'font-medium text-[var(--el-color-warning)]'"
            >{{ stats.diff }}</b> 个不一致
          </span>
        </template>
      </div>
      <Btn
        size="sm"
        :disabled="busy || refreshing"
        :aria-busy="refreshing"
        data-spec="ship-refresh"
        @click="emit('refresh')"
      >
        <template #icon>
          <span :class="refreshing ? 'inline-flex animate-spin' : 'inline-flex'">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        {{ refreshing ? '刷新中…' : '刷新' }}
      </Btn>
    </div>
  </Card>
</template>
