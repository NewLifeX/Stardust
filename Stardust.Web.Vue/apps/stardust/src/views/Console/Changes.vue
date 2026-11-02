<script setup lang="ts">
/**
 * 跨应用「变更」时间线页（/changes，停点 20）。
 * 数据由 changes.logic.fetchChanges 编排：6 个接口并行，按项目 id 集合过滤三张历史表后合并倒序。
 * 取数随 stardustContext（项目/环境）失效重取（useContextReload）；「全部项目」时不带 projectId，
 * 映射自然包含全部，过滤即全放行。点行由 ChangesList 发 select，这里跳 /app/<tab>/<appId>。
 */
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import PageHead from '@/layouts/StardustLayout2/PageHead.vue';
import ChangesList from '@/layouts/StardustLayout2/ChangesList.vue';
import { fetchChanges, type ChangeItem } from '@/layouts/StardustLayout2/changes.logic';
import { stardustContext } from '@/stores/stardustContext';
import { useContextReload } from '@/layouts/StardustLayout2/useContextReload';

const router = useRouter();

const items = ref<ChangeItem[]>([]);
const loading = ref(false);
const errorText = ref('');

async function load(isStale: () => boolean): Promise<void> {
  loading.value = true;
  errorText.value = '';
  try {
    const r = await fetchChanges(request);
    if (isStale()) return;
    items.value = r.items;
    errorText.value = r.error;
  } catch (e) {
    if (isStale()) return;
    errorText.value = e instanceof Error ? e.message : '变更加载失败';
  } finally {
    if (!isStale()) loading.value = false;
  }
}

function onSelect(item: ChangeItem): void {
  void router.push(item.path);
}

// 上下文（项目、环境）就绪后首轮取数，之后切换项目或环境自动重取
const { reload } = useContextReload(load, stardustContext);
</script>

<template>
  <div data-spec="page-changes">
    <PageHead
      eyebrow="变更 · 跨应用时间线"
      title="变更"
      sub="把部署历史、流水线运行、配置变更按时间汇成一条线：发完版本成没成、出事前谁改了什么，点行直达对应应用。"
    >
      <template #actions>
        <Btn
          size="sm"
          :disabled="loading"
          data-spec="changes-refresh"
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

    <ChangesList
      :items="items"
      :loading="loading"
      :error="errorText"
      @select="onSelect"
    />
  </div>
</template>
