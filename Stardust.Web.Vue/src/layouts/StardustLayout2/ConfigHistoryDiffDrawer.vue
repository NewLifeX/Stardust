<script setup lang="ts">
/**
 * ConfigHistoryDiffDrawer：配置历史「对比上一版」抽屉（J-CFG-05 S2b，2026-11-09 接入）。
 * 数据源：GET /Configs/ConfigHistory/Diff?id=（ConfigHistoryController.cs:51-73）：
 *  - newJson=本条 Remark 快照；oldJson=同配置更早最近一次 Publish 的 Remark（首次发布为 null）；
 *  - changedKeys=两快照顶层键差异数组（按键名排序）。
 * 语义边界：版本快照级对比（AppConfig 字段差异），非 KV 级历史 diff（Remark 只存版本号快照）。
 * 三态兜底：请求失败「加载失败」；oldJson=null「首次发布」提示；changedKeys 空「无差异」。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import {
  buildDiffRows,
  fetchConfigHistoryDiff,
} from './appConfig.logic.ts';
import type { ConfigDiffKind, ConfigDiffRow, ConfigHistoryDiff, ConfigHistoryItem } from './appConfig.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import Drawer from './Drawer/Drawer.vue';
import Tag from './Tag.vue';

const props = defineProps<{
  open: boolean;
  /** 触发对比的历史行（取 id 拉取 Diff；展示版本号供标题） */
  history: ConfigHistoryItem | null;
  /** 测试缝：注入 request；生产不传 */
  deps?: { request?: Req };
}>();

const emit = defineEmits<{ close: [] }>();

const loading = ref(false);
const failed = ref(false);
const diff = ref<ConfigHistoryDiff | null>(null);

const rows = computed<ConfigDiffRow[]>(() => (diff.value ? buildDiffRows(diff.value) : []));
/** 首次发布：后端 oldJson=null（无更早 Publish） */
const firstPublish = computed(() => !!diff.value && diff.value.oldJson === null);

const KIND_TAG: Record<ConfigDiffKind, { text: string; tone: 'ok' | 'warn' | 'err' }> = {
  added: { text: '新增', tone: 'ok' },
  removed: { text: '移除', tone: 'err' },
  changed: { text: '变更', tone: 'warn' },
};

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

watch(
  () => props.open,
  async (open) => {
    if (!open) return;
    const id = props.history?.id ?? 0;
    diff.value = null;
    failed.value = false;
    if (!id) {
      failed.value = true;
      return;
    }
    loading.value = true;
    try {
      const d = await fetchConfigHistoryDiff(req(), id);
      diff.value = d;
      failed.value = !d;
    } finally {
      loading.value = false;
    }
  },
  { immediate: true },
);
</script>

<template>
  <Drawer
    :open="open"
    :title="`版本对比 · v${history?.version ?? 0}`"
    :sub="history ? `${history.time} · 与上一版发布的快照差异` : ''"
    @close="emit('close')"
  >
    <div
      class="flex flex-col gap-[10px]"
      data-spec="config-history-diff"
    >
      <p
        v-if="loading"
        class="text-[12px] text-[var(--el-text-color-secondary)]"
        data-spec="config-history-diff-loading"
      >
        对比加载中…
      </p>

      <p
        v-else-if="failed"
        class="text-[12px] text-[var(--el-color-danger)]"
        data-spec="config-history-diff-fail"
        role="alert"
      >
        对比加载失败，请稍后重试。
      </p>

      <template v-else-if="diff">
        <p
          class="rounded-[8px] border border-[var(--el-border-color-light)] bg-[color-mix(in_oklab,var(--el-fill-color)_60%,transparent)] px-[10px] py-[7px] text-[11.5px] text-[var(--el-text-color-secondary)]"
        >
          与上一版发布快照的顶层字段差异（版本快照级对比；键值级历史差异因快照不含 KV 数据不可做）。
        </p>

        <p
          v-if="firstPublish"
          class="rounded-[8px] border border-[color-mix(in_oklab,var(--el-color-primary)_40%,transparent)] bg-[color-mix(in_oklab,var(--el-color-primary)_8%,transparent)] px-[10px] py-[7px] text-[11.5px] text-[var(--el-text-color-regular)]"
          data-spec="config-history-diff-first"
        >
          这是首次发布，没有更早的发布版本可比，以下为本次快照全量字段。
        </p>

        <p
          v-else-if="!rows.length"
          class="py-3 text-[12px] text-[var(--el-text-color-secondary)]"
          data-spec="config-history-diff-empty"
        >
          两个版本快照无差异。
        </p>

        <div
          v-for="r in rows"
          :key="r.key"
          class="flex items-center gap-2 border-b border-[var(--el-border-color-light)] py-[7px] font-mono text-[11.5px] last:border-b-0"
          data-spec="config-history-diff-row"
        >
          <span class="w-[96px] shrink-0 truncate text-[var(--el-text-color-primary)]">{{ r.key }}</span>
          <Tag :tone="KIND_TAG[r.kind].tone">
            {{ KIND_TAG[r.kind].text }}
          </Tag>
          <span
            class="min-w-0 flex-1 truncate text-[var(--el-text-color-secondary)]"
            :title="r.oldValue"
          >
            {{ r.oldValue }}
          </span>
          <span class="shrink-0 text-[var(--el-text-color-secondary)]">→</span>
          <span
            class="min-w-0 flex-1 truncate"
            :class="r.kind === 'removed' ? 'text-[var(--el-text-color-secondary)]' : 'text-[var(--el-color-warning)]'"
            :title="r.newValue"
          >
            {{ r.newValue }}
          </span>
        </div>
      </template>
    </div>
  </Drawer>
</template>
