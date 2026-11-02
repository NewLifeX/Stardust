<script setup lang="ts">
/**
 * AlarmsList：告警中心列表版（当前告警 + 历史告警）。纯 UI：
 * 只收 alarms.logic 归一好的 rows，过滤/展开为内部状态，点刷新由薄壳取数后重传。
 * 不做告警规则配置、不做处置操作（后端没有对应接口与状态机），只展示。
 *
 * 三段状态：loading（spinner）、error（可见错误 + 重试按钮，不静默空态）、空态「没有告警」。
 * 级别 Dot：critical 红（err）/ warning 黄（warn）/ info 灰（default）。
 * 状态 Tag：active=未处理（warn 语气），resolved=已处理（default 灰）。
 * 当前告警在前（toAlarmRows 已排好），历史在后。
 * 样式只用 --el-* + Tailwind，无硬编码色值。
 */
import { computed, ref } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Dot from './Dot.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import { filterAlarms, type AlarmRow, type AlarmSeverity } from './alarms.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: AlarmRow[];
    loading?: boolean;
    error?: string;
  }>(),
  { loading: false, error: '' },
);

const emit = defineEmits<{ retry: [] }>();

// ---------- 过滤状态 ----------

const levelFilter = ref('');
const statusFilter = ref('');
const appQuery = ref('');

const visible = computed<AlarmRow[]>(() =>
  filterAlarms(props.rows, levelFilter.value, statusFilter.value, appQuery.value),
);

/** 统计条：只数当前告警（active）里的 critical / warning 条数 */
const activeCritical = computed(
  () => props.rows.filter((r) => r.status === 'active' && r.level === 'critical').length,
);
const activeWarning = computed(
  () => props.rows.filter((r) => r.status === 'active' && r.level === 'warning').length,
);

// ---------- 展开行 ----------

const openId = ref<number | null>(null);
function toggle(id: number): void {
  openId.value = openId.value === id ? null : id;
}

const LEVEL_DOT_TONE: Record<AlarmSeverity, 'err' | 'warn' | 'default'> = {
  critical: 'err',
  warning: 'warn',
  info: 'default',
};
const LEVEL_LABEL: Record<AlarmSeverity, string> = {
  critical: 'Critical',
  warning: 'Warning',
  info: 'Info',
};
</script>

<template>
  <Card
    padding="none"
    data-spec="alarms-list"
  >
    <!-- 过滤工具条：级别 + 状态下拉 + 应用搜索 -->
    <div
      class="flex flex-wrap items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]"
    >
      <select
        v-model="levelFilter"
        data-spec="alarms-filter-level"
        class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
      >
        <option value="">
          全部级别
        </option>
        <option value="critical">
          Critical
        </option>
        <option value="warning">
          Warning
        </option>
        <option value="info">
          Info
        </option>
      </select>

      <select
        v-model="statusFilter"
        data-spec="alarms-filter-status"
        class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
      >
        <option value="">
          全部状态
        </option>
        <option value="active">
          未处理
        </option>
        <option value="resolved">
          已处理
        </option>
      </select>

      <div
        class="flex h-[28px] items-center gap-[6px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px]"
      >
        <KIcon
          name="Search"
          :size="13"
          class="text-[var(--el-text-color-secondary)]"
        />
        <input
          v-model="appQuery"
          type="text"
          placeholder="搜索应用 / 节点 / 标题"
          data-spec="alarms-filter-q"
          class="w-[160px] bg-transparent text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)]"
        >
      </div>

      <span class="flex-1" />

      <!-- 统计条：当前 N Critical / M Warning -->
      <span
        v-if="!loading && !error && rows.length > 0"
        data-spec="alarms-stats"
        class="flex items-center gap-[10px] font-mono text-[11px] text-[var(--el-text-color-secondary)]"
      >
        <span class="flex items-center gap-[4px]">
          <Dot tone="err" />
          当前 {{ activeCritical }} Critical
        </span>
        <span class="flex items-center gap-[4px]">
          <Dot tone="warn" />
          当前 {{ activeWarning }} Warning
        </span>
      </span>
    </div>

    <!-- 加载中 -->
    <div
      v-if="loading"
      data-spec="alarms-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      告警加载中…
    </div>

    <!-- 失败：可见错误 + 重试，不当作空态 -->
    <div
      v-else-if="error"
      data-spec="alarms-failed"
      class="flex items-center gap-[10px] px-[14px] py-[14px] text-[12.5px] text-[var(--el-color-danger)]"
    >
      告警加载失败：{{ error }}
      <Btn
        size="sm"
        data-spec="alarms-retry"
        @click="emit('retry')"
      >
        重试
      </Btn>
    </div>

    <!-- 空态：无数据（或过滤后为空统一提示） -->
    <div
      v-else-if="visible.length === 0"
      data-spec="alarms-empty"
      class="px-[14px] py-[22px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <template v-if="rows.length === 0">
        没有告警
      </template>
      <template v-else>
        当前过滤条件下没有匹配的告警
      </template>
    </div>

    <!-- 表格 -->
    <template v-else>
      <table class="w-full border-collapse">
        <thead>
          <tr>
            <th class="w-[60px] border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              级别
            </th>
            <th class="border-b border-[var(--el-border-color-light)] px-[10px] py-[9px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              告警标题
            </th>
            <th class="w-[140px] border-b border-[var(--el-border-color-light)] px-[10px] py-[9px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              对象
            </th>
            <th class="w-[80px] border-b border-[var(--el-border-color-light)] px-[10px] py-[9px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              状态
            </th>
            <th class="w-[110px] border-b border-[var(--el-border-color-light)] px-[10px] py-[9px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              持续时间
            </th>
            <th class="w-[80px] border-b border-[var(--el-border-color-light)] px-[10px] py-[9px] text-right text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              通知次数
            </th>
            <th class="w-[130px] border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-right text-[11px] font-medium text-[var(--el-text-color-secondary)]">
              开始时间
            </th>
          </tr>
        </thead>
        <tbody>
          <template
            v-for="row in visible"
            :key="row.id"
          >
            <tr
              data-spec="alarm-row"
              :data-level="row.level"
              :data-status="row.status"
              :data-open="openId === row.id"
              class="cursor-pointer transition-colors hover:bg-[var(--el-bg-color-overlay)]"
              @click="toggle(row.id)"
            >
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
                <span class="flex items-center gap-[6px]">
                  <Dot :tone="LEVEL_DOT_TONE[row.level]" />
                  <span class="font-mono text-[10.5px] uppercase text-[var(--el-text-color-secondary)]">
                    {{ LEVEL_LABEL[row.level] }}
                  </span>
                </span>
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[10px] py-[10px]">
                <span class="flex items-center gap-[6px]">
                  <b class="truncate text-[12.5px] font-medium text-[var(--el-text-color-primary)]">{{ row.title }}</b>
                  <KIcon
                    :name="openId === row.id ? 'ChevronDown' : 'ChevronRight'"
                    :size="13"
                    class="shrink-0 text-[var(--el-text-color-secondary)]"
                  />
                </span>
              </td>
              <td class="truncate border-b border-[var(--el-border-color-light)] px-[10px] py-[10px] text-[12px] text-[var(--el-text-color-secondary)]">
                {{ row.appName }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[10px] py-[10px]">
                <Tag
                  v-if="row.status === 'active'"
                  tone="warn"
                >
                  未处理
                </Tag>
                <Tag
                  v-else
                  tone="default"
                >
                  已处理
                </Tag>
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[10px] py-[10px] font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">
                {{ row.durationText }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[10px] py-[10px] text-right font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">
                {{ row.times }}
              </td>
              <td class="border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] text-right font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">
                {{ row.startTime }}
              </td>
            </tr>

            <!-- 展开详情：完整 Content + Category + Action + 时间线信息 -->
            <tr
              v-if="openId === row.id"
              data-spec="alarm-detail"
            >
              <td
                colspan="7"
                class="border-b border-[var(--el-border-color-light)] bg-[var(--el-fill-color-light)] px-[18px] py-[12px]"
              >
                <pre
                  v-if="row.content"
                  data-spec="alarm-content"
                  class="m-0 whitespace-pre-wrap rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[10px] font-mono text-[12px] leading-relaxed text-[var(--el-text-color-regular)]"
                >{{ row.content }}</pre>
                <div class="mt-[8px] grid grid-cols-2 gap-x-[16px] gap-y-[4px] text-[11.5px] text-[var(--el-text-color-secondary)] min-[720px]:grid-cols-4">
                  <span>类别：{{ row.category || '—' }}</span>
                  <span>对象：{{ row.action || '—' }}</span>
                  <span>开始：{{ row.startTime }}</span>
                  <span>持续：{{ row.durationText }}</span>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </template>
  </Card>
</template>
