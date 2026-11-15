<script setup lang="ts">
/**
 * AlarmsList：告警中心列表版（当前告警 + 历史告警）。纯 UI：
 * 只收 alarms.logic 归一好的 rows，级别/关键字过滤与展开为内部状态；
 * 组/时间/状态过滤为受控 props（值由页面持有并驱动服务端重取，CARD-MON-02/03），变更经 update:* 上抛
 * （状态=已恢复时页面按 recordStatus=2 服务端重取，CARD-MON-03 定稿 3）。
 * 不做告警规则配置、不做处置流程（认领/静默，后端无接口，C05 占位不动）；
 * 记录行（非 resolved）提供行操作「编辑/删除」（CARD-MON-03，经事件上抛由页面执行），
 * 工具条「导出 CSV」导出当前筛选结果（≤200 条已加载行，前端同步生成，C17）。
 *
 * 三段状态：loading（spinner）、error（可见错误 + 重试按钮，不静默空态）、空态「没有告警」。
 * 级别 Dot：critical 红（err）/ warning 黄（warn）/ info 灰（default）。
 * 状态 Tag：active=未处理（warn）/ recovered=已恢复（ok，CARD-MON-03）/ resolved=已处理（default 灰）。
 * 当前告警在前（toAlarmRows 已排好），历史在后。
 * 行内「告警历史」链接（记录行且有组）→ 上抛 history 事件，由页面设置组过滤并滚动。
 * 行操作「编辑/删除」仅记录行（row.status !== 'resolved'，AlarmHistory 只读）。
 * 样式只用 --el-* + Tailwind，无硬编码色值。
 */
import { computed, ref } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Dot from './Dot.vue';
import KIcon from './KIcon.vue';
import Tag from './Tag.vue';
import {
  filterAlarms,
  type AlarmGroupOption,
  type AlarmRow,
  type AlarmSeverity,
} from './alarms.logic.ts';
import { buildAlarmCsv } from './alarmRecord.logic.ts';

const props = withDefaults(
  defineProps<{
    rows: AlarmRow[];
    loading?: boolean;
    error?: string;
    /** 告警组下拉选项（alarms-filter-group 数据源，由页面拉取传入） */
    groups?: AlarmGroupOption[];
    /** 告警组列表加载失败提示（非致命，下拉旁可见，刷新可重试） */
    groupError?: string;
    /** 服务端组过滤（受控；null=全部组；组 Id 字符串透传，走查缺陷②全链 string） */
    groupId?: string | null;
    /** 时间范围（受控，YYYY-MM-DD；空=不过滤，不造默认时间窗） */
    dtStart?: string;
    dtEnd?: string;
    /** 状态过滤（受控；''=全部状态；'recovered' 时页面按 recordStatus=2 服务端重取，CARD-MON-03） */
    statusFilter?: string;
  }>(),
  {
    loading: false,
    error: '',
    groups: () => [],
    groupError: '',
    groupId: null,
    dtStart: '',
    dtEnd: '',
    statusFilter: '',
  },
);

const emit = defineEmits<{
  retry: [];
  'update:groupId': [value: string | null];
  'update:dtStart': [value: string];
  'update:dtEnd': [value: string];
  /** 状态过滤上抛（'recovered' 时页面按服务端重取，CARD-MON-03） */
  'update:statusFilter': [value: string];
  /** 行操作「编辑」：仅记录行渲染，页面打开编辑抽屉（CARD-MON-03） */
  edit: [row: AlarmRow];
  /** 行操作「删除」：仅记录行渲染，页面弹 Guard 确认（CARD-MON-03） */
  remove: [row: AlarmRow];
  /** 行内「告警历史」：按该组过滤并滚动到列表（CARD-MON-02；组 Id 字符串透传，走查缺陷②） */
  history: [groupId: string];
}>();

// ---------- 受控过滤上抛 ----------

function onGroupChange(e: Event): void {
  const v = (e.target as HTMLSelectElement).value;
  emit('update:groupId', v === '' ? null : v);
}
function onStartChange(e: Event): void {
  emit('update:dtStart', (e.target as HTMLInputElement).value);
}
function onEndChange(e: Event): void {
  emit('update:dtEnd', (e.target as HTMLInputElement).value);
}
function onStatusChange(e: Event): void {
  emit('update:statusFilter', (e.target as HTMLSelectElement).value);
}
/** 行操作「编辑/删除」上抛（仅记录行渲染；AlarmHistory 只读不在行内提供） */
function onEdit(row: AlarmRow): void {
  emit('edit', row);
}
function onRemove(row: AlarmRow): void {
  emit('remove', row);
}
/** 行内「告警历史」：仅对有组的记录行提供（groupId 空串哨兵=无组） */
function goHistory(groupId: string): void {
  if (groupId) emit('history', groupId);
}

// ---------- 过滤状态 ----------

const levelFilter = ref('');
const appQuery = ref('');

const visible = computed<AlarmRow[]>(() =>
  filterAlarms(props.rows, levelFilter.value, props.statusFilter ?? '', appQuery.value),
);

/** 统计条：只数当前告警（active）里的 critical / warning 条数 */
const activeCritical = computed(
  () => props.rows.filter((r) => r.status === 'active' && r.level === 'critical').length,
);
const activeWarning = computed(
  () => props.rows.filter((r) => r.status === 'active' && r.level === 'warning').length,
);

// ---------- 展开行 ----------

const openId = ref<string | null>(null);
function toggle(id: string): void {
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

// ---------- 导出 CSV（CARD-MON-03 C17：前端同步生成，行集=当前筛选结果 ≤200 条已加载行） ----------

/** 组名解析：告警组下拉数据源优先，取不到显 GroupId（定稿 5，不造数；组 Id 字符串同形比较） */
function resolveGroupName(groupId: string): string {
  return props.groups.find((g) => g.id === groupId)?.name ?? groupId;
}
/** 生成 CSV 并触发下载（同步无异步窗口，双击防护由「空结果禁用 + title 原因」承担） */
function onExport(): void {
  if (visible.value.length === 0) return;
  const csv = buildAlarmCsv(visible.value, resolveGroupName);
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = '告警记录导出.csv';
  a.click();
  URL.revokeObjectURL(url);
}
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

      <!-- 状态下拉（CARD-MON-03：受控，变更上抛由页面驱动服务端重取；已恢复=recordStatus 2） -->
      <select
        :value="props.statusFilter ?? ''"
        data-spec="alarms-filter-status"
        aria-label="按状态过滤"
        class="h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        @change="onStatusChange"
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
        <option value="recovered">
          已恢复
        </option>
      </select>

      <!-- 告警组下拉（CARD-MON-02：服务端过滤，选中即带 groupId 重取；「全部组」=不过滤） -->
      <select
        :value="props.groupId ?? ''"
        data-spec="alarms-filter-group"
        aria-label="按告警组过滤"
        :title="props.groupError || undefined"
        class="h-[28px] max-w-[180px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
        @change="onGroupChange"
      >
        <option value="">
          全部组
        </option>
        <option
          v-for="g in props.groups"
          :key="g.id"
          :value="g.id"
        >
          {{ g.name }}
        </option>
      </select>
      <span
        v-if="props.groupError"
        data-spec="alarms-group-error"
        class="text-[11px] text-[var(--el-color-danger)]"
      >{{ props.groupError }}</span>

      <!-- 时间范围（CARD-MON-02：dtStart/dtEnd 直传服务端；不选=全量，无默认时间窗） -->
      <span
        data-spec="alarms-filter-range"
        class="flex h-[28px] items-center gap-[4px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px]"
      >
        <input
          type="date"
          :value="props.dtStart"
          aria-label="开始日期"
          class="w-[112px] cursor-pointer bg-transparent text-[12px] text-[var(--el-text-color-primary)] outline-none"
          @change="onStartChange"
        >
        <span class="text-[11px] text-[var(--el-text-color-placeholder)]">至</span>
        <input
          type="date"
          :value="props.dtEnd"
          aria-label="结束日期"
          class="w-[112px] cursor-pointer bg-transparent text-[12px] text-[var(--el-text-color-primary)] outline-none"
          @change="onEndChange"
        >
      </span>

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

      <!-- 导出 CSV（CARD-MON-03 C17：导出当前筛选结果；空结果禁用，原因在 title 随状态实时变化） -->
      <span
        data-spec="alarms-export-note"
        class="text-[11px] text-[var(--el-text-color-placeholder)]"
      >当前筛选结果（最多最近 200 条）</span>
      <Btn
        size="sm"
        data-spec="alarms-export"
        :disabled="visible.length === 0"
        :title="visible.length === 0 ? '当前筛选结果为空，无可导出数据' : '导出当前筛选结果为 CSV'"
        @click="onExport"
      >
        导出 CSV
      </Btn>
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
                  <button
                    v-if="row.status !== 'resolved' && row.groupId !== ''"
                    type="button"
                    data-spec="alarms-history-link"
                    title="查看该组告警历史"
                    class="shrink-0 cursor-pointer text-[11px] text-[var(--el-color-primary)] hover:underline"
                    @click.stop="goHistory(row.groupId)"
                  >
                    告警历史
                  </button>
                  <!-- 行操作（CARD-MON-03）：仅记录行（active/recovered），AlarmHistory 只读不提供 -->
                  <button
                    v-if="row.status !== 'resolved'"
                    type="button"
                    data-spec="alarm-record-edit"
                    title="编辑该告警记录"
                    class="shrink-0 cursor-pointer text-[11px] text-[var(--el-color-primary)] hover:underline"
                    @click.stop="onEdit(row)"
                  >
                    编辑
                  </button>
                  <button
                    v-if="row.status !== 'resolved'"
                    type="button"
                    data-spec="alarm-record-remove"
                    title="删除该告警记录"
                    class="shrink-0 cursor-pointer text-[11px] text-[var(--el-color-danger)] hover:underline"
                    @click.stop="onRemove(row)"
                  >
                    删除
                  </button>
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
                  v-else-if="row.status === 'recovered'"
                  tone="ok"
                >
                  已恢复
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
                <div class="mt-[8px] grid grid-cols-2 gap-x-[16px] gap-y-[4px] text-[11.5px] text-[var(--el-text-color-secondary)] min-[720px]:grid-cols-5">
                  <span>类别：{{ row.category || '—' }}</span>
                  <span>对象：{{ row.action || '—' }}</span>
                  <span>开始：{{ row.startTime }}</span>
                  <span>持续：{{ row.durationText }}</span>
                  <span data-spec="alarm-error">错误：{{ row.error || '（空）' }}</span>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </template>
  </Card>
</template>
