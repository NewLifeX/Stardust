<script setup lang="ts">
/**
 * 发布页（/ship），对照原型 page-ship.jsx 的 PageShip：页头 + 应用下拉 + ShipPanel。
 * 应用列表复用 appsModel.fetchAppList；已停用的应用不出现在下拉里（原型同样过滤 st === 'off'）。
 * 原型下拉按应用名取值，这里用应用 Id（部署集按 appId 查）。
 */
import { computed, ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { fetchAppList } from '../Stardust/appsModel';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import ShipPanel from './ShipPanel.vue';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

interface AppOption {
  id: number;
  name: string;
}

const apps = ref<AppOption[]>([]);
const appId = ref(0);
const state = ref<'loading' | 'ok' | 'failed'>('loading');

const current = computed(() => apps.value.find((a) => a.id === appId.value));

async function loadApps(isStale: () => boolean): Promise<void> {
  state.value = 'loading';
  try {
    const r = await fetchAppList(request);
    if (isStale()) return;
    apps.value = r.rows
      .filter((a) => a.enable !== false && a.id && a.name)
      .map((a) => ({ id: Number(a.id), name: String(a.name) }));
    if (!apps.value.some((a) => a.id === appId.value)) appId.value = apps.value[0]?.id ?? 0;
    state.value = 'ok';
  } catch {
    if (isStale()) return;
    state.value = 'failed';
  }
}

// 上下文（项目、环境）就绪后首轮取数，之后切换项目或环境自动重取
useContextReload(loadApps, stardustContext);
</script>

<template>
  <div data-spec="page-ship">
    <PageHead
      eyebrow="发布 · 远程自动发布"
      title="发布"
      sub="查看当前生效版本、应用版本、对单个节点安装或重启，并查看部署历史。灰度与分批发布后端暂未提供，先显示「待接入」。"
    >
      <template #actions>
        <select
          v-model.number="appId"
          data-spec="ship-app"
          class="h-[32px] w-[200px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[11px] text-[13px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        >
          <option
            v-for="a in apps"
            :key="a.id"
            :value="a.id"
          >
            {{ a.name }}
          </option>
        </select>
      </template>
    </PageHead>

    <p
      v-if="state === 'loading'"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      应用列表加载中…
    </p>
    <p
      v-else-if="state === 'failed'"
      class="text-[13px] text-[var(--el-color-danger)]"
    >
      应用列表加载失败，请刷新重试。
    </p>
    <p
      v-else-if="!current"
      class="text-[13px] text-[var(--el-text-color-secondary)]"
    >
      暂无可发布的应用。
    </p>
    <ShipPanel
      v-else
      :app-id="current.id"
      :app-name="current.name"
    />
  </div>
</template>
