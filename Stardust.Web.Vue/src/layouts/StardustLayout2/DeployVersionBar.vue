<script setup lang="ts">
/**
 * DeployVersionBar：发布页「当前生效版本」条，对应 ShipPanel 原模板的第 1 块（2026-10-28 拆分）。
 * 纯 UI：只收 props、只发 refresh/edit/sync；取数与刷新状态机在 useDeployData，节点统计由 deployView.logic 的 nodeStats 算好传入。
 * 刷新/同步按钮遵守 AGENTS.md §11：操作中禁用，刷新中图标旋转、文案变「刷新中…」。
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
  /** 同步应用请求进行中（busyKey === 'deploy-sync'） */
  syncing: boolean;
}>();

const emit = defineEmits<{ refresh: []; edit: []; sync: [] }>();
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
        :disabled="busy || refreshing || syncing"
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
      <Btn
        size="sm"
        :disabled="busy || refreshing || syncing"
        data-spec="ship-deploy-edit"
        title="修改部署集名称、类别、应用包名与仓库"
        @click="emit('edit')"
      >
        编辑发布集
      </Btn>
      <Btn
        size="sm"
        :disabled="busy || refreshing || syncing"
        :aria-busy="syncing"
        data-spec="ship-sync-app"
        title="从应用注册表同步全部部署集（后端 AppDeploy/SyncApp）"
        @click="emit('sync')"
      >
        <template #icon>
          <span :class="syncing ? 'inline-flex animate-spin' : 'inline-flex'">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        {{ syncing ? '同步中…' : '同步应用' }}
      </Btn>
    </div>
  </Card>
</template>
