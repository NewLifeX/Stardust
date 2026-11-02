<script setup lang="ts">
/**
 * 应用列表（/apps），对照原型 page-apps.jsx 的 PageApps：页头 + 分类 chip + 应用行卡片。
 * 数据复用 appsModel.fetchAppList（应用、在线实例、服务数、分钟统计四个接口并行）。
 * 取数失败与「没有应用」要分开显示；失败时给重试按钮。
 * 「接入应用」向导尚未接线，按钮先禁用并标「待接入」，不做假动作。
 */
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { fetchAppList, type AppAggRow } from '../Stardust/appsModel';
import AppRow from './AppRow.vue';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Card from '@/layouts/StardustLayout2/Card.vue';
import Chip from '@/layouts/StardustLayout2/Chip.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import { ALL_CATEGORY, fetchAppBuildStatus, filterByCategory, listCategories } from '@/layouts/StardustLayout2/apps.logic';
import { appPath, type AppTab } from '@/layouts/StardustLayout2/palette.items';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

const router = useRouter();

const rows = ref<AppAggRow[]>([]);
const state = ref<'loading' | 'ok' | 'failed'>('loading');
const errorText = ref('');
const category = ref(ALL_CATEGORY);

const categories = computed(() => listCategories(rows.value));
const visible = computed(() => filterByCategory(rows.value, category.value));

async function load(isStale: () => boolean): Promise<void> {
  state.value = 'loading';
  errorText.value = '';
  try {
    const list = (await fetchAppList(request)).rows;
    if (isStale()) return;
    // 批量取构建状态（全局聚合，非逐应用 N+1）；失败不阻塞列表
    const buildMap = await fetchAppBuildStatus(request);
    if (isStale()) return;
    rows.value = list.map((r) => ({ ...r, buildStatus: buildMap.get(r.id ?? 0) ?? null }));
    if (!categories.value.includes(category.value)) category.value = ALL_CATEGORY;
    state.value = 'ok';
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : String(e);
    state.value = 'failed';
  }
}

function openApp(id: number | undefined, tab: AppTab): void {
  if (id !== undefined) void router.push(appPath(id, tab));
}

// 上下文（项目、环境）就绪后首轮取数，之后切换项目或环境自动重取
const { reload } = useContextReload(load, stardustContext);
</script>

<template>
  <div data-spec="page-apps">
    <PageHead
      :eyebrow="`应用 · ${rows.length}`"
      title="应用"
      sub="每个应用是一个工作台：性能、链路、日志、配置、实例、发布都在同一处。"
    >
      <template #actions>
        <Btn
          variant="default"
          disabled
          title="接入向导待接入"
          data-spec="apps-wizard"
        >
          <template #icon>
            <KIcon
              name="Plus"
              :size="13"
            />
          </template>
          接入应用（待接入）
        </Btn>
      </template>
    </PageHead>

    <p
      v-if="state === 'loading'"
      data-spec="apps-loading"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      应用列表加载中…
    </p>

    <div
      v-else-if="state === 'failed'"
      data-spec="apps-failed"
      class="flex items-center gap-[10px] text-[13px] text-[var(--el-color-danger)]"
    >
      <span>应用列表加载失败：{{ errorText }}</span>
      <Btn
        size="sm"
        data-spec="apps-retry"
        @click="reload"
      >
        重试
      </Btn>
    </div>

    <p
      v-else-if="rows.length === 0"
      data-spec="apps-empty"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      还没有接入应用。
    </p>

    <template v-else>
      <div class="mb-[14px] flex flex-wrap items-center gap-[8px]">
        <Chip
          v-for="c in categories"
          :key="c"
          :on="c === category"
          data-spec="apps-chip"
          @click="category = c"
        >
          {{ c }}
        </Chip>
        <span class="flex-1" />
        <span class="font-mono text-[11px] text-[var(--el-text-color-secondary)]">
          点击任意行进入工作台 · 快捷键 P / C / L
        </span>
      </div>
      <Card padding="none">
        <AppRow
          v-for="r in visible"
          :key="r.id ?? r.name"
          :row="r"
          @open="(tab) => openApp(r.id, tab)"
        />
      </Card>
    </template>
  </div>
</template>
