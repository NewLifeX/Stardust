<script setup lang="ts">
/**
 * 跨应用配置中心页（/configs，轻量只读版）。
 * 数据由 configCenter.logic.fetchAllConfigs 编排：拉全部 AppConfig + 应用列表建 appId→名映射。
 * 点行打开 ConfigDetailDrawer（取 ConfigData + ConfigHistory）；不做就地编辑，
 * 「前往编辑」跳到对应应用的配置页签 /app/config/<appId>。
 * 取数随 stardustContext（项目/环境）失效重取（useContextReload）。
 */
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import ConfigCenterList from '@/layouts/StardustLayout2/ConfigCenterList.vue';
import ConfigDetailDrawer from '@/layouts/StardustLayout2/ConfigDetailDrawer.vue';
import {
  fetchAllConfigs,
  fetchConfigDetail,
} from '@/layouts/StardustLayout2/configCenter.logic';
import type { ConfigCenterRow, ConfigDetail } from '@/layouts/StardustLayout2/configCenter.logic';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

const router = useRouter();

const rows = ref<ConfigCenterRow[]>([]);
const loading = ref(false);
const errorText = ref('');

const drawerOpen = ref(false);
const detail = ref<ConfigDetail | null>(null);
const detailLoading = ref(false);
const detailError = ref('');

async function load(isStale: () => boolean): Promise<void> {
  loading.value = true;
  errorText.value = '';
  try {
    const r = await fetchAllConfigs(request, stardustContext.projectId);
    if (isStale()) return;
    rows.value = r.rows;
    errorText.value = r.error;
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : '配置中心加载失败';
  } finally {
    if (!isStale()) loading.value = false;
  }
}

async function onOpen(configId: number): Promise<void> {
  const row = rows.value.find((r) => r.configId === configId);
  if (!row) return;
  drawerOpen.value = true;
  detail.value = null;
  detailError.value = '';
  detailLoading.value = true;
  try {
    const d = await fetchConfigDetail(request, configId, row.appId, row.appName);
    detail.value = d;
    detailError.value = d ? '' : '配置详情加载失败';
  } finally {
    detailLoading.value = false;
  }
}

function onGotoAppConfig(appId: number): void {
  drawerOpen.value = false;
  void router.push(`/app/config/${appId}`);
}

const { reload } = useContextReload(load, stardustContext);
</script>

<template>
  <div data-spec="page-configs">
    <PageHead
      eyebrow="配置 · 跨应用"
      title="配置中心"
      sub="汇总所有应用的配置项：搜索、按应用过滤，点行看完整键值、待发布差异与版本历史。就地编辑请到对应应用的配置页签。"
    >
      <template #actions>
        <Btn
          size="sm"
          :disabled="loading"
          data-spec="configs-refresh"
          @click="reload"
        >
          <template #icon>
            <KIcon
              name="Refresh"
              :size="13"
              :class="loading ? 'animate-spin' : ''"
            />
          </template>
          {{ loading ? '刷新中…' : '刷新' }}
        </Btn>
      </template>
    </PageHead>

    <ConfigCenterList
      :rows="rows"
      :loading="loading"
      :error="errorText"
      @open="onOpen"
      @retry="reload"
    />

    <ConfigDetailDrawer
      :open="drawerOpen"
      :detail="detail"
      :loading="detailLoading"
      :error="detailError"
      @close="drawerOpen = false"
      @goto-app-config="onGotoAppConfig"
    />
  </div>
</template>
