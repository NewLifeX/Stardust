<script setup lang="ts">
/**
 * Registry/AppHistory/Index — 应用历史列表（自定义迁移页）
 *
 * MVC 原 _List_Search 自定义搜索字段：
 *   - 操作（action）：下拉，选项来自 AppHistory.FindAllAction()
 *   - 状态（success）：下拉，1=成功 / 0=失败
 *   - 时间：日期范围（dtStart/dtEnd）
 *
 * 注：AppHistory 的 Search 还支持 appId/client/nodeId 参数，但 MVC _List_Search 仅
 * 暴露「操作 / 状态 / 时间」三个字段（其余通过链接带参进入），故最小视图与之保持一致。
 *
 * WebApi 端 AppHistoryController 带 [Menu(0, false)]，菜单树不会注册该页路由，
 * 故建最小自定义视图兜底。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'AppHistory');

interface Row {
  [k: string]: unknown;
}

interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  itemType?: string;
  primaryKey?: boolean;
  [k: string]: unknown;
}
interface PageMeta {
  list: BackendField[];
  [k: string]: unknown;
}

/** 后端下发的列表字段（WebApi RemoveField("Id","Version")，动态取以保证列集合/顺序与 MVC 一致）。
 *  备注：后端 GetPage 数据行含 Map 扩展属性 AppName/NodeName（与 AppOnline 同范式），
 *  但字段元数据 list 不含其 Url 配置，故需自建链接列，并剔除外键数字列避免冗余呈现。 */
const listFields = ref<FieldMeta[]>([]);

/** 后端自定义 url 跳转字段 / 外键数字列 / 需单独定位的字段（自建列接管，不进入自动表格列）。
 *  - AppName/Client/Action/TraceId：带 Url 的链接列，自建
 *  - AppId/NodeId：外键数字列，MVC 中分别被 AppName/NodeName 取代隐藏
 *  - NodeName：Map 扩展属性，MVC 以纯文本“节点”列呈现，自建“节点”列接管
 *  - Success：单独显式渲染以对齐 MVC 列序（操作之后、追踪之前） */
const SKIP_FIELDS = new Set(['AppId', 'NodeId', 'NodeName', 'AppName', 'Client', 'Action', 'TraceId', 'Success']);

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    itemType: f.itemType,
    primaryKey: f.primaryKey,
  };
}

async function fetchMeta() {
  try {
    const res: any = await api.getPage();
    const meta = (res?.data ?? res) as PageMeta;
    if (meta?.list?.length) {
      listFields.value = meta.list
        .filter((f) => !SKIP_FIELDS.has(f.name))
        .map(toFieldMeta);
    }
  } catch (e) {
    console.error('[AppHistory] GetPage failed', e);
  }
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);

const actionOptions = ref<{ value: string; label: string }[]>([]);

const filters = reactive({
  action: '',
  success: '',
  timeRange: null as [string, string] | null,
});

const successOptions = [
  { value: '', label: '全部' },
  { value: 'true', label: '成功' },
  { value: 'false', label: '失败' },
];

const hasActiveFilter = computed(() => {
  return !!filters.action || filters.success !== '' || filters.timeRange != null;
});

async function fetchActions() {
  try {
    // 从已有历史记录中提取类别（action）去重，与 MVC FindAllAction() 语义一致
    const res: any = await api.getList<Row>({ pageIndex: 0, pageSize: 1000, fields: 'action' });
    const data = res?.data ?? [];
    const set = new Set<string>();
    data.forEach((r: Row) => {
      const a = r.action;
      if (a) set.add(a);
    });
    const list = Array.from(set)
      .sort((a, b) => b.localeCompare(a))
      .map((a) => ({ value: a, label: a }));
    actionOptions.value = [{ value: '', label: '全部' }, ...list];
  } catch (e) {
    console.error('[AppHistory] fetchActions failed', e);
    actionOptions.value = [{ value: '', label: '全部' }];
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (filters.action) params.action = filters.action;
    if (filters.success !== '') params.success = filters.success;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[AppHistory] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'Success') return v ? '是' : '否';
  if (key === 'CreateTime') return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
}

// 自建链接列（对齐 MVC 后端 Url 配置；WebApi 端 TraceUrl 仅取字段无 Url，需自建）
function appUrl(row: Row): string {
  return `#/Registry/App?Id=${row.AppId}`;
}
function clientUrl(row: Row): string {
  return `#/Registry/AppHistory?appId=${row.AppId}&client=${row.Client}`;
}
function actionUrl(row: Row): string {
  return `#/Registry/AppHistory?appId=${row.AppId}&action=${row.Action}`;
}
function traceUrl(row: Row): string {
  return `#/Monitors/Trace/Detail?Id=${row.TraceId ?? ''}`;
}

function handleSearch() {
  page.value = 1;
  fetchList();
}

function handleReset() {
  filters.action = '';
  filters.success = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  await fetchMeta();
  await fetchActions();
  await fetchList();
});
</script>

<template>
  <div class="list-page app-history-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用历史
      </h2>
      <div class="lp-toolbar">
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
      </div>
    </div>

    <div class="lp-card">
      <div class="lp-card-header app-history-search">
        <div class="ah-search-grid">
          <div class="ah-search-field">
            <label class="ah-search-label">操作</label>
            <el-select
              v-model="filters.action"
              clearable
              placeholder="请选择"
              class="ah-search-input"
            >
              <el-option
                v-for="opt in actionOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ah-search-field">
            <label class="ah-search-label">状态</label>
            <el-select
              v-model="filters.success"
              clearable
              placeholder="请选择"
              class="ah-search-input"
            >
              <el-option
                v-for="opt in successOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ah-search-field ah-search-field--wide">
            <label class="ah-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="ah-search-input ah-search-datepicker"
            />
          </div>

          <div class="ah-search-actions">
            <el-button
              :class="{ 'is-active': hasActiveFilter }"
              @click="handleReset"
            >
              重置
            </el-button>
            <el-button
              type="primary"
              @click="handleSearch"
            >
              <el-icon><Search /></el-icon>搜索
            </el-button>
          </div>
        </div>
      </div>

      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="act-table"
          stripe
          border
          empty-text="暂无数据"
        >
          <!-- 自定义链接列：应用 → 应用页（对齐 MVC AppName.Url=/Registry/App?Id={AppId}） -->
          <el-table-column
            label="应用"
            width="160"
            fixed="left"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.AppId != null"
                type="primary"
                :href="appUrl(row)"
                target="_self"
              >
                {{ row.AppName }}
              </el-link>
              <span v-else>{{ row.AppName }}</span>
            </template>
          </el-table-column>

          <!-- 自定义链接列：客户端 → 自身带参（对齐 MVC Client.Url=/Registry/AppHistory?appId={AppId}&client={Client}） -->
          <el-table-column
            label="客户端"
            width="150"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Client"
                type="primary"
                :href="clientUrl(row)"
                target="_self"
              >
                {{ row.Client }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 节点列：MVC 中 NodeId 被 Map 扩展属性 NodeName 取代，无 Url，纯文本呈现（DisplayName=节点） -->
          <el-table-column
            label="节点"
            width="160"
          >
            <template #default="{ row }">
              <span>{{ row.NodeName || '-' }}</span>
            </template>
          </el-table-column>

          <!-- 自定义链接列：操作 → 自身带参（对齐 MVC Action.Url=/Registry/AppHistory?appId={AppId}&action={Action}） -->
          <el-table-column
            label="操作"
            width="120"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.Action"
                type="primary"
                :href="actionUrl(row)"
                target="_self"
              >
                {{ row.Action }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 成功列：单独显式渲染以对齐 MVC 列序（操作之后、追踪之前），布尔 是/否 -->
          <el-table-column
            label="成功"
            width="90"
            align="center"
          >
            <template #default="{ row }">
              <span>{{ row.Success ? '是' : '否' }}</span>
            </template>
          </el-table-column>

          <!-- 自定义链接列：追踪 → 追踪详情（对齐 MVC ListFields.TraceUrl()，WebApi 端仅取字段无 Url，需自建） -->
          <el-table-column
            label="追踪"
            width="160"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.TraceId"
                type="info"
                :href="traceUrl(row)"
                target="_blank"
              >
                {{ row.TraceId }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 动态列：Creator / CreateTime / CreateIP / Remark（后端下发，列序与 MVC 一致位于末尾） -->
          <el-table-column
            v-for="f in listFields"
            :key="f.name"
            :prop="f.name"
            :label="f.displayName || f.name"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, f.name) }}</span>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <div class="lp-card-footer">
        <el-pagination
          :current-page="page"
          :page-size="pageSize"
          :total="total"
          layout="total, prev, pager, next, sizes"
          :page-sizes="[10, 20, 50, 100]"
          @current-change="onPageChange"
          @size-change="
            (s: number) => {
              pageSize = s;
              page = 1;
              fetchList();
            }
          "
        />
      </div>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.app-history-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  .lp-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: var(--el-main-padding, 24px) var(--el-main-padding, 24px) 0;

    .lph-title {
      margin: 0;
      font-size: 18px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .lp-toolbar {
      display: flex;
      gap: 8px;
    }
  }

  .lp-card {
    flex: 1;
    margin: var(--el-main-padding, 24px);
    padding: 16px;
    background: var(--el-bg-color-overlay);
    border: 1px solid var(--el-border-color-light);
    border-radius: var(--el-border-radius-base);
    box-shadow: var(--el-box-shadow-light);
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  .lp-card-header {
    padding: 16px 20px;
    border-bottom: 1px solid var(--el-border-color-light);
  }

  .lp-card-body {
    flex: 1;
    min-height: 200px;
    overflow: auto;
  }

  .lp-card-footer {
    padding: 12px 20px;
    border-top: 1px solid var(--el-border-color-light);
  }
}

.app-history-search {
  .ah-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .ah-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .ah-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .ah-search-input {
    width: 100%;

    :deep(.el-input__wrapper),
    :deep(.el-select__wrapper),
    :deep(.el-range-editor) {
      min-height: 36px;
      border-radius: var(--el-border-radius-base);
      background: var(--el-bg-color);
      border: 1px solid var(--el-border-color-light);
      box-shadow: none;
      transition:
        border-color 0.15s ease,
        background 0.15s ease,
        box-shadow 0.15s ease;
    }

    :deep(.el-input__wrapper.is-focus),
    :deep(.el-select__wrapper.is-focused),
    :deep(.el-range-editor.is-active) {
      background: var(--el-fill-color-blank);
      border-color: var(--el-color-primary);
      box-shadow:
        0 0 0 1px var(--el-color-primary) inset,
        0 0 0 3px var(--el-color-primary-light-8);
    }

    :deep(.el-input__inner),
    :deep(.el-select__selected-item) {
      font-size: 13px;
      color: var(--el-text-color-primary);
    }
  }

  .ah-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .ah-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.act-table {
  width: 100%;
}

@media (max-width: 900px) {
  .app-history-search .ah-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .app-history-search .ah-search-grid {
    grid-template-columns: 1fr;
  }

  .app-history-search .ah-search-field--wide {
    grid-column: span 1;
  }
}
</style>
