<script setup lang="ts">
/**
 * AlarmsRules：告警中心「跟踪规则」视图区块（CARD-MON-13，来源旅程 J-MON-15 / 走查 TraceRule.md）。
 * 区块卡形态对齐 AlarmsGroups（CARD-MON-01）：语义说明 + 工具条（搜索框+新增）+ 表格 + 行操作（编辑/删除）。
 * 自取数：fetchTraceRules（真实接口 GET /Monitors/TraceRule，无演示数据）。
 * 假筛选硬条款（TraceRuleController.cs:45-53 只消费 dtStart/dtEnd/Q）：不提供 isWhite/isRegex/enable
 * 过滤控件，搜索框为纯客户端过滤（filterTraceRuleRows），不造「服务端筛选生效」假象。
 * 新增/编辑抽屉内联（Drawer 包装，基准 PipelineConfigDrawer v2 / AlarmGroupDrawer）；
 * 删除走 GuardModal 两段式（标题含规则串、正文写后果、按钮「取消/确认执行」）。
 * §11：所有带请求的按钮 loading + 禁用；结果 toast；失败错误条 + 重试（不静默空态）。
 * data-spec 前缀 trace-rule-（已查重，与 alarm- / alarms- 族既有前缀无冲突）。
 */
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { request } from '@newlifex/cube-vue/core/utils/request';
import Btn from '@/layouts/StardustLayout2/Btn.vue';
import Card from '@/layouts/StardustLayout2/Card.vue';
import Drawer from '@/layouts/StardustLayout2/Drawer/Drawer.vue';
import GuardModal from '@/layouts/StardustLayout2/GuardModal/GuardModal.vue';
import KIcon from '@/layouts/StardustLayout2/KIcon.vue';
import Tag from '@/layouts/StardustLayout2/Tag.vue';
import { SELECT, TD, TH } from '@/layouts/StardustLayout2/deployView.styles.ts';
import { toast } from '@/layouts/StardustLayout2/useToast.ts';
import type { Req } from '../Stardust/appsModel';
import {
  deleteTraceRule,
  emptyTraceRuleForm,
  fetchTraceRules,
  filterTraceRuleRows,
  listLabel,
  modeLabel,
  saveTraceRule,
  toTraceRuleForm,
  validateTraceRuleForm,
  TRACE_RULE_EMPTY_CREATE_TEXT,
  TRACE_RULE_EMPTY_TEXT,
  TRACE_RULE_FILTER_EMPTY_TEXT,
  type TraceRuleForm,
  type TraceRuleRow,
} from './traceRule.logic';

const props = defineProps<{
  deps?: { request?: Req };
}>();

function req(): Req {
  return props.deps?.request ?? (request as Req);
}

// ---------- 取数 ----------

const rows = ref<TraceRuleRow[]>([]);
const loading = ref(true);
const errorText = ref('');

async function load(): Promise<void> {
  loading.value = true;
  errorText.value = '';
  const r = await fetchTraceRules(req());
  rows.value = r.rows;
  errorText.value = r.error;
  loading.value = false;
}

// ---------- 过滤（纯客户端，假筛选硬条款） ----------

const query = ref('');
const visible = computed<TraceRuleRow[]>(() => filterTraceRuleRows(rows.value, query.value));

// ---------- 行内展开（备注全文 + 创建/更新 IP） ----------

const openId = ref<number | null>(null);
function toggle(id: number): void {
  openId.value = openId.value === id ? null : id;
}

// ---------- 新增 / 编辑 ----------

const drawerOpen = ref(false);
const drawerId = ref<number | null>(null);
const drawerInitial = ref<TraceRuleForm | null>(null);
const saveBusy = ref(false);

function drawerTitle(): string {
  return drawerId.value ? '编辑跟踪规则' : '新增跟踪规则';
}

function openCreate(): void {
  drawerId.value = null;
  drawerInitial.value = null;
  drawerOpen.value = true;
}

function openEdit(row: TraceRuleRow): void {
  drawerId.value = row.id;
  drawerInitial.value = toTraceRuleForm(row);
  drawerOpen.value = true;
}

async function onSubmit(form: TraceRuleForm): Promise<void> {
  saveBusy.value = true;
  try {
    await saveTraceRule(req(), form, drawerId.value ?? undefined);
    drawerOpen.value = false;
    saveBusy.value = false;
    toast(`跟踪规则「${form.rule.trim()}」已保存`, 'ok');
    await load();
  } catch (e) {
    saveBusy.value = false;
    toast(`保存跟踪规则失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  }
}

// ---------- 删除 ----------

const removeTarget = ref<TraceRuleRow | null>(null);
const removeBusy = ref(false);

function askRemove(row: TraceRuleRow): void {
  removeTarget.value = row;
}

async function confirmRemove(): Promise<void> {
  const row = removeTarget.value;
  if (!row || removeBusy.value) return;
  removeBusy.value = true;
  try {
    await deleteTraceRule(req(), row.id);
    removeBusy.value = false;
    removeTarget.value = null;
    toast(`跟踪规则「${row.rule}」已删除`, 'ok');
    await load();
  } catch (e) {
    removeBusy.value = false;
    removeTarget.value = null;
    toast(`删除「${row.rule}」失败：${e instanceof Error && e.message ? e.message : String(e)}`, 'err');
  }
}

onMounted(() => void load());

// ---------- 抽屉表单（内联） ----------

const inputCls =
  'h-[30px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] text-[12.5px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';
const labelCls = 'flex flex-col gap-[4px]';
const spanCls = 'text-[12px] text-[var(--el-text-color-secondary)]';

const form = reactive<TraceRuleForm>(emptyTraceRuleForm());
const formError = ref('');

// 抽屉打开时回填：新增=空表单（默认启用+白名单），编辑=行值
watch(drawerOpen, (open) => {
  if (!open) return;
  formError.value = '';
  Object.assign(form, emptyTraceRuleForm(), drawerInitial.value ?? {});
});

function onFormSubmit(): void {
  const err = validateTraceRuleForm(form);
  if (err) {
    formError.value = err;
    return;
  }
  formError.value = '';
  void onSubmit({ ...form });
}
</script>

<template>
  <Card
    padding="none"
    data-spec="alarms-rules"
  >
    <!-- 语义说明（实体描述逐字 + C04 匹配语义文档化，不改后端） -->
    <div class="border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-[11.5px] leading-relaxed text-[var(--el-text-color-secondary)]">
      全局黑白名单，白名单放行，黑名单拦截。优先级越大越在前面。匹配顺序：按优先级从高到低逐条匹配，命中黑名单丢弃链路数据、命中白名单放行，均未命中默认放行；规则支持 * 模糊匹配（不区分大小写）或正则（区分大小写）。
    </div>

    <!-- 工具条：搜索（客户端过滤） + 新增 -->
    <div class="flex flex-wrap items-center gap-[8px] border-b border-[var(--el-border-color-light)] px-[14px] py-[10px]">
      <div class="flex h-[28px] items-center gap-[6px] rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px]">
        <KIcon
          name="Search"
          :size="12"
          class="text-[var(--el-text-color-secondary)]"
        />
        <input
          v-model="query"
          type="text"
          data-spec="trace-rule-search"
          placeholder="搜索规则 / 备注"
          class="w-[150px] bg-transparent text-[12px] text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-secondary)]"
        >
      </div>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="primary"
        data-spec="trace-rule-create"
        @click="openCreate"
      >
        新增跟踪规则
      </Btn>
    </div>

    <!-- 三态：loading / failed / 内容 -->
    <div
      v-if="loading"
      data-spec="trace-rule-loading"
      class="flex items-center justify-center gap-[8px] py-10 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-flex animate-spin">
        <span class="h-[14px] w-[14px] rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      </span>
      跟踪规则加载中…
    </div>

    <div
      v-else-if="errorText"
      data-spec="trace-rule-failed"
      class="flex flex-wrap items-center justify-center gap-[10px] py-8 text-[12.5px] text-[var(--el-color-danger)]"
    >
      跟踪规则加载失败：{{ errorText }}
      <Btn
        size="sm"
        data-spec="trace-rule-retry"
        @click="load"
      >
        重试
      </Btn>
    </div>

    <!-- 空态两模式（实现规范 §3.3）：无数据=引导；有数据过滤后无=纯列表空态 -->
    <div
      v-else-if="visible.length === 0 && rows.length === 0"
      data-spec="trace-rule-empty"
      class="flex flex-col items-center gap-[10px] py-10 text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span>{{ TRACE_RULE_EMPTY_TEXT }}</span>
      <Btn
        size="sm"
        variant="primary"
        data-spec="trace-rule-empty-create"
        @click="openCreate"
      >
        {{ TRACE_RULE_EMPTY_CREATE_TEXT }}
      </Btn>
    </div>
    <div
      v-else-if="visible.length === 0"
      data-spec="trace-rule-filter-empty"
      class="py-10 text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      {{ TRACE_RULE_FILTER_EMPTY_TEXT }}
    </div>

    <!-- 表格 -->
    <table
      v-else
      class="w-full border-collapse"
      data-spec="trace-rule-table"
    >
      <thead>
        <tr>
          <th :class="TH">
            规则
          </th>
          <th :class="TH">
            名单
          </th>
          <th :class="TH">
            方式
          </th>
          <th :class="TH">
            启用
          </th>
          <th :class="TH">
            优先级
          </th>
          <th :class="TH">
            更新者
          </th>
          <th :class="TH">
            更新时间
          </th>
          <th :class="TH">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <template
          v-for="row in visible"
          :key="row.id"
        >
          <tr
            :data-spec="'trace-rule-row'"
            class="group cursor-pointer hover:bg-[var(--el-fill-color-light)]"
            @click="toggle(row.id)"
          >
            <td :class="TD">
              <span
                class="block max-w-[260px] truncate font-mono text-[11.5px] text-[var(--el-text-color-primary)]"
                :title="row.rule"
              >{{ row.rule || '—' }}</span>
            </td>
            <td :class="TD">
              <Tag :tone="row.isWhite ? 'ok' : 'err'">
                {{ listLabel(row.isWhite) }}
              </Tag>
            </td>
            <td :class="TD">
              <Tag :tone="row.isRegex ? 'brand' : 'default'">
                {{ modeLabel(row.isRegex) }}
              </Tag>
            </td>
            <td :class="TD">
              <Tag :tone="row.enable ? 'ok' : 'default'">
                {{ row.enable ? '启用' : '停用' }}
              </Tag>
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-regular)]">{{ row.priority }}</span>
            </td>
            <td :class="TD">
              {{ row.updateUser || '—' }}
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] tabular-nums text-[var(--el-text-color-secondary)]">{{ row.updateTime }}</span>
            </td>
            <td
              :class="TD"
              @click.stop
            >
              <div class="flex items-center gap-[4px]">
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="trace-rule-edit"
                  @click="openEdit(row)"
                >
                  编辑
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="trace-rule-remove"
                  @click="askRemove(row)"
                >
                  删除
                </Btn>
              </div>
            </td>
          </tr>
          <!-- 行内展开：备注全文 + 创建/更新 IP（RemoveCreateField 隐藏的创建类信息在此观察） -->
          <tr
            v-if="openId === row.id"
            data-spec="trace-rule-detail"
          >
            <td
              :class="TD"
              colspan="8"
            >
              <pre
                v-if="row.remark"
                data-spec="trace-rule-remark"
                class="m-0 whitespace-pre-wrap rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] p-[10px] font-mono text-[12px] leading-relaxed text-[var(--el-text-color-regular)]"
              >{{ row.remark }}</pre>
              <div class="mt-[8px] grid grid-cols-2 gap-x-[16px] gap-y-[4px] text-[11.5px] text-[var(--el-text-color-secondary)] min-[720px]:grid-cols-4">
                <span>创建者：{{ row.createUser || '—' }}</span>
                <span>创建时间：{{ row.createTime }}</span>
                <span>创建地址：{{ row.createIp || '—' }}</span>
                <span>更新地址：{{ row.updateIp || '—' }}</span>
              </div>
            </td>
          </tr>
        </template>
      </tbody>
    </table>

    <!-- 新增/编辑抽屉（内联，Drawer 包装） -->
    <Drawer
      :open="drawerOpen"
      :title="drawerTitle()"
      :sub="drawerId ? `#${drawerId}` : '全局黑白名单，白名单放行，黑名单拦截'"
      @close="drawerOpen = false"
    >
      <div
        class="flex flex-col gap-[13px]"
        data-spec="trace-rule-form"
      >
        <label :class="labelCls">
          <span :class="spanCls">规则 <span class="text-[var(--el-color-danger)]">*</span></span>
          <input
            v-model="form.rule"
            type="text"
            data-spec="trace-rule-form-rule"
            :class="inputCls"
            placeholder="支持*模糊匹配（不区分大小写），如/cube/*。支持正则（区分大小写）"
            @keydown.enter="onFormSubmit"
          >
        </label>

        <label class="flex items-center gap-[8px]">
          <input
            v-model="form.isWhite"
            type="checkbox"
            data-spec="trace-rule-form-iswhite"
            class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
          >
          <span class="text-[12.5px] text-[var(--el-text-color-primary)]">白名单</span>
          <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">白名单。否则是黑名单</span>
        </label>

        <label class="flex items-center gap-[8px]">
          <input
            v-model="form.isRegex"
            type="checkbox"
            data-spec="trace-rule-form-isregex"
            class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
          >
          <span class="text-[12.5px] text-[var(--el-text-color-primary)]">正则</span>
          <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">是否使用正则表达式，此时区分大小写；不勾选则 * 模糊匹配（不区分大小写）</span>
        </label>

        <label class="flex items-center gap-[8px]">
          <input
            v-model="form.enable"
            type="checkbox"
            data-spec="trace-rule-form-enable"
            class="h-[14px] w-[14px] accent-[var(--el-color-primary)]"
          >
          <span class="text-[12.5px] text-[var(--el-text-color-primary)]">启用</span>
          <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">停用后该规则不参与匹配</span>
        </label>

        <label :class="labelCls">
          <span :class="spanCls">优先级</span>
          <input
            v-model.number="form.priority"
            type="number"
            min="0"
            step="1"
            data-spec="trace-rule-form-priority"
            :class="inputCls"
            class="!w-[140px]"
          >
          <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">优先级。越大越在前面</span>
        </label>

        <label :class="labelCls">
          <span :class="spanCls">备注</span>
          <textarea
            v-model="form.remark"
            data-spec="trace-rule-form-remark"
            class="min-h-[72px] w-full rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[10px] py-[7px] text-[12.5px] leading-relaxed text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]"
            placeholder="规则的用途说明"
          />
        </label>

        <p
          v-if="formError"
          role="alert"
          data-spec="trace-rule-form-error"
          class="m-0 text-[12px] text-[var(--el-color-danger)]"
        >
          {{ formError }}
        </p>
      </div>

      <template #actions>
        <Btn
          variant="ghost"
          data-spec="trace-rule-form-cancel"
          @click="drawerOpen = false"
        >
          取消
        </Btn>
        <Btn
          variant="primary"
          data-spec="trace-rule-form-submit"
          :disabled="saveBusy"
          @click="onFormSubmit"
        >
          {{ saveBusy ? '保存中…' : '保存' }}
        </Btn>
      </template>
    </Drawer>

    <!-- 删除（GuardModal 两段式：短标题 label + 后果 detail，告警组同款） -->
    <GuardModal
      v-if="removeTarget"
      :label="`删除跟踪规则「${removeTarget.rule}」？`"
      detail="删除后该规则立即失效，不再参与链路数据的放行与拦截"
      @cancel="removeTarget = null"
      @confirm="confirmRemove"
    />
  </Card>
</template>
