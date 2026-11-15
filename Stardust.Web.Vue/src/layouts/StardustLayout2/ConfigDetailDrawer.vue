<script setup lang="ts">
/**
 * ConfigDetailDrawer：跨应用配置中心的详情抽屉（只读）。
 * 数据由薄壳取好传入（detail），详情本身不发请求；「预览」弹层取数走注入的
 * fetchPreview（默认接 configCenter.logic.fetchConfigPreview + 全局 request，
 * 与 AppLov.fetchOptions 同一注入模式，story/CT 传替身）。
 * 左：完整 KV（Key / 生效值 / 草稿值）；中：待发布差异（已发布 Value → 草稿 NewValue）；
 * 右/下：版本历史（版本号 / 动作 / 时间，只读日志）。
 *
 * 诚实边界（后端 ConfigHistory.Remark 只存 AppConfig 版本号，不存历史 KV 快照）：
 * 不能选两个历史版本做 KV diff；这里的 diff 只对比当前「已发布 → 草稿」。
 * 不做编辑：底部提示跳转到应用 → 配置页签。样式只用 --el-* + Tailwind。
 */
import { computed, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Drawer from './Drawer/Drawer.vue';
import Tag from './Tag.vue';
import Btn from './Btn.vue';
import { fetchConfigPreview, pendingDiff } from './configCenter.logic.ts';
import type { ConfigDetail, ConfigDiffRow, ConfigPreview } from './configCenter.logic.ts';

const props = withDefaults(
  defineProps<{
    open: boolean;
    detail: ConfigDetail | null;
    loading?: boolean;
    error?: string;
    /** 预览取数注入点：appId → /config/getall 实拉结果；默认接 logic 层 */
    fetchPreview?: (appId: number) => Promise<ConfigPreview | null>;
  }>(),
  { loading: false, error: '' },
);

const emit = defineEmits<{
  close: [];
  gotoAppConfig: [appId: number];
}>();

/** 待发布差异（已发布 Value → 草稿 NewValue），只列有变化的行 */
const diffRows = computed<ConfigDiffRow[]>(() =>
  props.detail ? pendingDiff(props.detail.items) : [],
);

// ---------- 预览弹层（候选 B，C19：内容为 /config/getall 实拉结果，关闭无副作用） ----------

const previewOpen = ref(false);
const previewLoading = ref(false);
const previewError = ref('');
const previewData = ref<ConfigPreview | null>(null);

const fetchPreviewFn: (appId: number) => Promise<ConfigPreview | null> =
  props.fetchPreview ?? ((appId: number) => fetchConfigPreview(request, appId));

async function loadPreview(): Promise<void> {
  if (!props.detail) return;
  previewLoading.value = true;
  previewError.value = '';
  try {
    const p = await fetchPreviewFn(props.detail.appId);
    if (p === null) previewError.value = '预览加载失败';
    else previewData.value = p;
  } catch {
    previewError.value = '预览加载失败';
  } finally {
    previewLoading.value = false;
  }
}

function openPreview(): void {
  previewData.value = null;
  previewError.value = '';
  previewOpen.value = true;
  void loadPreview();
}

// 主抽屉关闭即复位预览态（关闭无副作用）
watch(
  () => props.open,
  (open) => {
    if (!open) {
      previewOpen.value = false;
      previewLoading.value = false;
      previewError.value = '';
      previewData.value = null;
    }
  },
);

/** 生效键值对按 Key 排序展示 */
const previewEntries = computed<[string, string][]>(() =>
  previewData.value ? Object.entries(previewData.value.configs).sort(([a], [b]) => a.localeCompare(b)) : [],
);

const DIFF_TAG: Record<ConfigDiffRow['state'], { text: string; tone: 'ok' | 'warn' | 'err' | 'default' }> = {
  added: { text: '新增', tone: 'ok' },
  removed: { text: '删除', tone: 'err' },
  changed: { text: '修改', tone: 'warn' },
  unchanged: { text: '不变', tone: 'default' },
};

const ACTION_LABEL: Record<string, string> = {
  Publish: '发布',
  Rollback: '回滚',
  Insert: '新增项',
  Update: '修改项',
  Delete: '删除项',
};
</script>

<template>
  <Drawer
    :open="open"
    :title="detail ? detail.appName : '配置详情'"
    :sub="detail ? `配置 #${detail.configId} · ${detail.items.length} 项` : ''"
    @close="emit('close')"
  >
    <template #header-extra>
      <Btn
        v-if="detail"
        size="sm"
        data-spec="config-preview-link"
        @click="openPreview"
      >
        预览
      </Btn>
    </template>

    <!-- 加载中 -->
    <div
      v-if="loading"
      data-spec="config-detail-loading"
      class="flex items-center gap-[8px] py-[24px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      详情加载中…
    </div>

    <!-- 失败 -->
    <div
      v-else-if="error"
      data-spec="config-detail-failed"
      class="py-[24px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      配置详情加载失败：{{ error }}
    </div>

    <!-- 空 -->
    <div
      v-else-if="!detail"
      data-spec="config-detail-empty"
      class="py-[24px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      未选择配置
    </div>

    <div
      v-else
      data-spec="config-detail-body"
    >
      <!-- 待发布摘要条 -->
      <div
        v-if="detail.pendingCount > 0"
        class="mb-3 rounded-[10px] border border-[color-mix(in_oklab,var(--el-color-warning)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-warning)_10%,transparent)] px-[12px] py-[8px] text-[12px] text-[var(--el-color-warning)]"
        data-spec="config-detail-pending-bar"
      >
        {{ detail.pendingCount }} 项待发布草稿（发布即生效，热更新在线实例）
      </div>

      <!-- 待发布差异 -->
      <div v-if="diffRows.length">
        <div class="mb-2 text-[12px] font-semibold text-[var(--el-text-color-primary)]">
          待发布差异
        </div>
        <table class="mb-4 w-full border-collapse">
          <thead>
            <tr>
              <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                Key
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                旧值
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                新值
              </th>
              <th class="w-[64px] border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                变化
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="d in diffRows"
              :key="d.key"
              data-spec="config-diff-row"
              :data-state="d.state"
              class="group"
            >
              <td class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] font-mono text-[11.5px] text-[var(--el-text-color-primary)]">
                {{ d.key }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] font-mono text-[11.5px] text-[var(--el-text-color-secondary)] group-last:border-b-0">
                {{ d.oldVal || '（空）' }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] font-mono text-[11.5px] text-[var(--el-color-warning)] group-last:border-b-0">
                {{ d.newVal || '（空）' }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] group-last:border-b-0">
                <Tag :tone="DIFF_TAG[d.state].tone">
                  {{ DIFF_TAG[d.state].text }}
                </Tag>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 完整 KV -->
      <div class="mb-2 text-[12px] font-semibold text-[var(--el-text-color-primary)]">
        配置项
      </div>
      <table class="mb-4 w-full border-collapse">
        <thead>
          <tr>
            <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              Key
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              生效值
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              草稿值
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="it in detail.items"
            :key="it.id"
            data-spec="config-kv-row"
            :data-draft="it.newValue !== it.value || !!it.newStatus"
          >
            <td class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] font-mono text-[11.5px] text-[var(--el-text-color-primary)]">
              {{ it.key }}
            </td>
            <td class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">
              {{ it.value || '（空）' }}
              <Tag
                v-if="it.newStatus"
                tone="err"
                class="ml-1"
              >
                待删除
              </Tag>
            </td>
            <td
              class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] font-mono text-[11.5px]"
              :class="it.newValue !== it.value ? 'text-[var(--el-color-warning)]' : 'text-[var(--el-text-color-placeholder)]'"
            >
              {{ it.newValue || '（空）' }}
            </td>
          </tr>
          <tr v-if="!detail.items.length">
            <td
              colspan="3"
              class="px-2 py-4 text-[12px] text-[var(--el-text-color-secondary)]"
              data-spec="config-kv-empty"
            >
              还没有配置项
            </td>
          </tr>
        </tbody>
      </table>

      <!-- 版本历史（只读日志） -->
      <div class="mb-2 text-[12px] font-semibold text-[var(--el-text-color-primary)]">
        版本历史
      </div>
      <div
        v-if="detail.history.length"
        class="mb-4"
      >
        <div
          v-for="h in detail.history"
          :key="h.id"
          data-spec="config-history-row"
          class="flex items-center gap-2 border-b border-[var(--el-border-color-light)] py-[6px] last:border-b-0"
        >
          <Tag tone="brand">
            v{{ h.version || '—' }}
          </Tag>
          <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">
            {{ ACTION_LABEL[h.action] || h.action }}
          </span>
          <Tag
            v-if="!h.success"
            tone="err"
          >
            失败
          </Tag>
          <span class="ml-auto font-mono text-[11px] tabular-nums text-[var(--el-text-color-placeholder)]">
            {{ h.time }}
          </span>
        </div>
      </div>
      <div
        v-else
        class="mb-4 py-2 text-[12px] text-[var(--el-text-color-secondary)]"
        data-spec="config-history-empty"
      >
        还没有发布历史
      </div>

      <!-- 底部：就地编辑提示 + 跳转 -->
      <div class="mt-4 flex flex-wrap items-center gap-2 rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[12px] py-[10px]">
        <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">
          就地编辑请到应用 → 配置页签进行
        </span>
        <span class="flex-1" />
        <Btn
          size="sm"
          variant="primary"
          data-spec="config-goto-app"
          @click="emit('gotoAppConfig', detail.appId)"
        >
          前往编辑
        </Btn>
      </div>
    </div>

    <!-- 预览弹层：GET /config/getall?appId={应用名}&secret=***（secret 不展示）实拉结果，只读 -->
    <Drawer
      :open="previewOpen"
      :title="detail ? `预览 · ${detail.appName}` : '预览'"
      :sub="previewData ? `GET /config/getall?appId=${previewData.appName}&secret=***` : '预览加载中…'"
      @close="previewOpen = false"
    >
      <!-- 预览加载中 -->
      <div
        v-if="previewLoading"
        data-spec="config-preview-loading"
        class="flex items-center gap-[8px] py-[24px] text-[12.5px] text-[var(--el-text-color-secondary)]"
      >
        <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
        预览加载中…
      </div>

      <!-- 预览失败 + 重试 -->
      <div
        v-else-if="previewError"
        data-spec="config-preview-failed"
        class="flex items-center gap-[10px] py-[24px]"
      >
        <span class="text-[12.5px] text-[var(--el-color-danger)]">{{ previewError }}</span>
        <Btn
          size="sm"
          data-spec="config-preview-retry"
          @click="loadPreview"
        >
          重试
        </Btn>
      </div>

      <!-- 预览体：元信息 + 生效键值对 -->
      <div
        v-else-if="previewData"
        data-spec="config-preview-body"
      >
        <div
          data-spec="config-preview-meta"
          class="mb-3 flex flex-wrap items-center gap-x-[14px] gap-y-[4px] rounded-[10px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[12px] py-[8px] text-[11.5px] text-[var(--el-text-color-secondary)]"
        >
          <span>生效版本 <span class="font-mono text-[var(--el-text-color-primary)]">v{{ previewData.version }}</span></span>
          <span>作用域 <span class="font-mono text-[var(--el-text-color-primary)]">{{ previewData.scope || '—' }}</span></span>
          <span>下一版本 <span class="font-mono text-[var(--el-text-color-primary)]">v{{ previewData.nextVersion }}</span></span>
          <span>下次发布 <span class="font-mono text-[var(--el-text-color-primary)]">{{ previewData.nextPublish || '—' }}</span></span>
          <span>更新时间 <span class="font-mono text-[var(--el-text-color-primary)]">{{ previewData.updateTime || '—' }}</span></span>
          <span>{{ previewEntries.length }} 个键</span>
        </div>

        <!-- 预览空态：拉到 ConfigInfo 但没有键 -->
        <div
          v-if="previewEntries.length === 0"
          data-spec="config-preview-empty"
          class="py-[24px] text-[12.5px] text-[var(--el-text-color-secondary)]"
        >
          该应用还没有可拉取的配置
        </div>

        <table
          v-else
          class="w-full border-collapse"
        >
          <thead>
            <tr>
              <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                Key
              </th>
              <th class="border-b border-[var(--el-border-color-light)] px-2 py-[6px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
                生效值
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="[key, value] in previewEntries"
              :key="key"
              data-spec="config-preview-row"
              :data-key="key"
            >
              <td class="border-b border-[var(--el-border-color-lighter)] px-2 py-[5px] align-top font-mono text-[12px] text-[var(--el-text-color-primary)]">
                {{ key }}
              </td>
              <td class="border-b border-[var(--el-border-color-lighter)] px-2 py-[5px] align-top font-mono text-[12px] break-all text-[var(--el-text-color-secondary)]">
                {{ value }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </Drawer>
  </Drawer>
</template>
