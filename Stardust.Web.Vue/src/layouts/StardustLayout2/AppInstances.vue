<script setup lang="ts">
/**
 * AppInstances：应用工作台「实例」页签（原型 README §4.3 第 197-200 行）。
 * 纯 UI + 自带三态取数：props 只收 appId；deps.request 是测试缝（默认接 cube-vue request）。
 * 数据源：GET /Registry/AppOnline?appId=（AppOnlineController.Search 支持 appId）。
 *
 * 现实约束（不造数字）：
 *  - AppOnline 主表没有 CPU/内存字段；AppMeter 是时序采样（最近 24 小时上千行），没有
 *    「每实例最新快照」接口，所以 CPU/内存两列标「待接入」；
 *  - 重启 / 终端按钮只发事件（restart/terminal），不实现实际调用（Execute 需要 keys 列表
 *    与 StarAgent 命令通道，不在本页签范围）。
 */
import { ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import { fetchInstances } from './appInstances.logic.ts';
import type { InstanceRow } from './appInstances.logic.ts';
import { toast } from './useToast.ts';
import { useManualRefresh } from './useManualRefresh.ts';
import Btn from './Btn.vue';
import Card from './Card.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';

const props = defineProps<{
  appId: number;
  /** 测试缝：注入 request；生产不传 */
  deps?: { request?: Req };
}>();

const emit = defineEmits<{
  /** 行点击：查看实例详情（节点抽屉由上层接） */
  select: [row: InstanceRow];
  /** 重启：只发事件，实际调用不在本页签 */
  restart: [row: InstanceRow];
  /** 终端：只发事件，实际调用不在本页签 */
  terminal: [row: InstanceRow];
  /** 打开应用历史（client）或性能（clientId） */
  drill: [payload: { tab: 'appHistory' | 'meter'; client: string }];
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

const loading = ref(true);
const loadFailed = ref(false);
const rows = ref<InstanceRow[]>([]);

async function load(): Promise<void> {
  loadFailed.value = false;
  const list = await fetchInstances(req(), props.appId);
  if (list === null) {
    loadFailed.value = true;
    return;
  }
  rows.value = list;
}

const { refreshing, refresh } = useManualRefresh({
  load: async () => {
    loading.value = true;
    try {
      await load();
    } finally {
      loading.value = false;
    }
  },
  outcome: () => ({
    loadError: loadFailed.value ? '实例加载失败' : '',
    versionsFailed: false,
    nodesFailed: false,
    historyFailed: false,
  }),
  notify: (ok, text) => toast(text, ok ? 'ok' : 'err'),
});

watch(
  () => props.appId,
  async () => {
    loading.value = true;
    try {
      await load();
    } finally {
      loading.value = false;
    }
  },
  { immediate: true },
);
</script>

<template>
  <Card data-spec="app-instances">
    <div class="flex flex-wrap items-center gap-x-3 gap-y-2">
      <h3 :class="H3">
        实例
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">
          {{ loading || loadFailed ? '—' : `${rows.length} 个在线` }}
        </span>
        <Tag tone="default">
          实时监控待接入（AppMeter 为时序采样，无快照接口）
        </Tag>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        :disabled="refreshing"
        :aria-busy="refreshing"
        data-spec="app-instances-refresh"
        @click="refresh"
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

    <div
      v-if="loading"
      class="py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
      data-spec="app-instances-loading"
    >
      实例加载中…
    </div>
    <div
      v-else-if="loadFailed"
      class="py-10 text-center text-[12.5px] text-[var(--el-color-danger)]"
      data-spec="app-instances-failed"
    >
      实例加载失败，请点「刷新」重试
    </div>

    <table
      v-else
      class="mt-3 w-full border-collapse"
    >
      <thead>
        <tr>
          <th :class="TH">
            节点
          </th>
          <th :class="TH">
            CPU
          </th>
          <th :class="TH">
            内存
          </th>
          <th :class="TH">
            版本
          </th>
          <th :class="[TH, 'w-[220px]']">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="app-instances-row"
          class="group cursor-pointer hover:bg-[var(--el-bg-color-overlay)]"
          @click="emit('select', r)"
        >
          <td :class="TD">
            <div class="flex items-center gap-2">
              <span
                class="inline-block h-[7px] w-[7px] rounded-full bg-[var(--el-color-success)]"
                data-spec="app-instances-dot"
              />
              <span class="text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.node || '—' }}</span>
              <span class="font-mono text-[12px] text-[var(--el-text-color-secondary)]">{{ r.ip }}</span>
            </div>
          </td>
          <td :class="TD">
            <Tag tone="default">
              待接入
            </Tag>
          </td>
          <td :class="TD">
            <Tag tone="default">
              待接入
            </Tag>
          </td>
          <td :class="[TD, 'font-mono text-[12px] text-[var(--el-text-color-primary)]']">
            {{ r.version || '—' }}
          </td>
          <td :class="TD">
            <div
              class="flex items-center gap-1"
              @click.stop
            >
              <Btn
                size="sm"
                variant="ghost"
                data-spec="app-instances-history"
                @click="emit('drill', { tab: 'appHistory', client: r.client })"
              >
                历史
              </Btn>
              <Btn
                size="sm"
                variant="ghost"
                data-spec="app-instances-meter"
                @click="emit('drill', { tab: 'meter', client: r.client })"
              >
                性能
              </Btn>
              <span class="inline-flex items-center gap-1 opacity-0 transition-opacity duration-[120ms] group-hover:opacity-100">
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="app-instances-restart"
                  @click="emit('restart', r)"
                >
                  重启
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="app-instances-terminal"
                  @click="emit('terminal', r)"
                >
                  终端
                </Btn>
              </span>
            </div>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="5"
            class="p-6 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
            data-spec="app-instances-empty"
          >
            暂无在线实例
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
