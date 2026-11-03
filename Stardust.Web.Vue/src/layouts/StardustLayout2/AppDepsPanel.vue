<script setup lang="ts">
/**
 * AppDepsPanel：应用工作台「配置」页签的上下游依赖面板。
 * 读同一接口 GET /Configs/AppQuote：appId=本应用 → 上游（本应用依赖谁）；
 * targetAppId=本应用 → 下游（谁依赖本应用）。只读展示，CRUD 在 /configs 页。
 * 应用名经 fetchAppList 建 id→名映射；取数失败独立三态，不拖垮配置表。
 */
import { ref, watch } from 'vue';
import realRequest from '@newlifex/cube-vue/core/utils/request';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import { fetchAppQuotes } from './configCenter.logic.ts';
import { fetchAppList } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { AppQuoteRow } from './configCenter.logic.ts';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

const props = defineProps<{
  appId: number;
  deps?: { request?: Req };
}>();

const emit = defineEmits<{
  manage: [];
}>();

function req(): Req {
  return props.deps?.request ?? (realRequest as Req);
}

const loading = ref(true);
const error = ref(false);
/** 上游：本应用依赖的行（appId=props.appId） */
const upstream = ref<AppQuoteRow[]>([]);
/** 下游：依赖本应用的行（targetAppId=props.appId） */
const downstream = ref<AppQuoteRow[]>([]);

let token = 0;

async function load(): Promise<void> {
  const mine = ++token;
  loading.value = true;
  error.value = false;
  try {
    const [appRes, up, down] = await Promise.all([
      fetchAppList(req()),
      fetchAppQuotes(req(), { appId: props.appId }),
      fetchAppQuotes(req(), { targetAppId: props.appId }),
    ]);
    if (mine !== token) return;
    if (up === null || down === null) {
      error.value = true;
      upstream.value = [];
      downstream.value = [];
      return;
    }
    const names = new Map<number, string>();
    for (const a of appRes.rows) names.set(Number(a.id), String(a.displayName || a.name || `#${a.id}`));
    const nm = (id: number) => (id > 0 ? names.get(id) ?? `#${id}` : '—');
    upstream.value = up.map((q) => ({ ...q, appName: nm(q.appId), targetName: nm(q.targetAppId) }));
    downstream.value = down.map((q) => ({ ...q, appName: nm(q.appId), targetName: nm(q.targetAppId) }));
  } catch {
    if (mine !== token) return;
    error.value = true;
  } finally {
    if (mine === token) loading.value = false;
  }
}

watch(() => props.appId, () => void load(), { immediate: true });
</script>

<template>
  <Card
    padding="none"
    class="mt-4"
    data-spec="app-deps-panel"
  >
    <div class="flex items-center gap-[10px] border-b border-[var(--el-border-color-light)] px-[18px] py-3">
      <h3 :class="H3">
        上下游依赖
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">
          上游=本应用依赖谁；下游=谁依赖本应用（AppQuote，全局管理在「配置中心」页）
        </span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="ghost"
        data-spec="app-deps-manage"
        @click="emit('manage')"
      >
        去配置中心管理
      </Btn>
    </div>

    <div
      v-if="loading"
      class="py-8 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="app-deps-loading"
    >
      依赖加载中…
    </div>
    <div
      v-else-if="error"
      class="py-8 text-center text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="app-deps-failed"
    >
      依赖加载失败
      <Btn
        size="sm"
        variant="ghost"
        data-spec="app-deps-retry"
        @click="load"
      >
        <template #icon>
          <span class="inline-flex">
            <KIcon
              name="Refresh"
              :size="12"
            />
          </span>
        </template>
        重试
      </Btn>
    </div>

    <div
      v-else
      class="grid grid-cols-2 gap-0 divide-x divide-[var(--el-border-color-light)]"
    >
      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th :class="TH">
              上游（依赖谁）
            </th>
            <th :class="[TH, 'w-[60px]']">
              启用
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="r in upstream"
            :key="`up-${r.id}`"
            data-spec="app-deps-up-row"
          >
            <td :class="[TD, 'font-mono text-[12px]']">
              {{ r.targetName }}
            </td>
            <td :class="TD">
              <Tag :tone="r.enable ? 'ok' : 'default'">
                {{ r.enable ? '启用' : '停用' }}
              </Tag>
            </td>
          </tr>
          <tr v-if="!upstream.length">
            <td
              colspan="2"
              class="p-4 text-center text-[12px] text-[var(--el-text-color-secondary)]"
              data-spec="app-deps-up-empty"
            >
              本应用没有声明上游依赖
            </td>
          </tr>
        </tbody>
      </table>

      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th :class="TH">
              下游（谁依赖我）
            </th>
            <th :class="[TH, 'w-[60px]']">
              启用
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="r in downstream"
            :key="`down-${r.id}`"
            data-spec="app-deps-down-row"
          >
            <td :class="[TD, 'font-mono text-[12px]']">
              {{ r.appName }}
            </td>
            <td :class="TD">
              <Tag :tone="r.enable ? 'ok' : 'default'">
                {{ r.enable ? '启用' : '停用' }}
              </Tag>
            </td>
          </tr>
          <tr v-if="!downstream.length">
            <td
              colspan="2"
              class="p-4 text-center text-[12px] text-[var(--el-text-color-secondary)]"
              data-spec="app-deps-down-empty"
            >
              还没有应用依赖本应用
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </Card>
</template>
