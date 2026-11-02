<script setup lang="ts">
/**
 * 告警中心薄壳（/alarms）。取数由 alarms.logic.fetchAlarms 编排：
 * 并行拉当前告警（AlarmRecord?status=1）+ 历史告警（AlarmHistory），归一合并。
 * 取数随 stardustContext（项目/环境）失效重取（useContextReload）；
 * AlarmRecord 实体无 ProjectId 字段，projectId 透传但对该接口无过滤效果（已读源码确认）。
 * 本页只展示，不做规则配置与处置操作（后端无对应接口/状态机）。
 */
import { ref } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import AlarmsList from '@/layouts/StardustLayout2/AlarmsList.vue';
import { fetchAlarms, type AlarmRow } from '@/layouts/StardustLayout2/alarms.logic';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

const rows = ref<AlarmRow[]>([]);
const loading = ref(false);
const errorText = ref('');

async function load(isStale: () => boolean): Promise<void> {
  loading.value = true;
  errorText.value = '';
  try {
    const r = await fetchAlarms(request, stardustContext.projectId);
    if (isStale()) return;
    rows.value = r.rows;
    errorText.value = r.error;
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : '告警加载失败';
  } finally {
    if (!isStale()) loading.value = false;
  }
}

// 上下文（项目、环境）就绪后首轮取数，之后切换项目或环境自动重取
const { reload } = useContextReload(load, stardustContext);
</script>

<template>
  <div data-spec="page-alarms">
    <PageHead
      eyebrow="监控 · 告警中心"
      title="告警中心"
      sub="当前告警与历史告警合并视图：按级别、状态、应用过滤，点行展开详情。规则配置与处置操作待后端接口就绪后接入。"
    >
      <template #actions>
        <Btn
          size="sm"
          :disabled="loading"
          data-spec="alarms-refresh"
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

    <AlarmsList
      :rows="rows"
      :loading="loading"
      :error="errorText"
      @retry="reload"
    />
  </div>
</template>
