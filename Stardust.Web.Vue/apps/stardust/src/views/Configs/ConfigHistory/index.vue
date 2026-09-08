<script setup lang="ts">
/**
 * Configs/ConfigHistory/Index — 配置历史列表（自定义迁移页）
 *
 * MVC 原 _List_Search 自定义了三个搜索字段：
 *   - 类别（action）：下拉，选项来自 ConfigHistory.FindAllActions()
 *   - 状态（success）：下拉，1=成功 / 0=失败
 *   - 时间：日期范围（dtStart/dtEnd）
 *
 * WebApi 端 ConfigHistoryController 为标准 EntityController，但 [Menu(0,false)]
 * 导致前端菜单树不会自动注册该页路由；因此建最小自定义视图兜底，
 * 复用标准 CRUD 列表并还原上述搜索字段。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Configs', 'ConfigHistory');

interface Row {
  id?: number;
  configName?: string;
  action?: string;
  success?: boolean;
  remark?: string;
  traceId?: string;
  createTime?: string;
  createIP?: string;
  [k: string]: unknown;
}

const COLUMNS: { name: string; label: string; width?: number }[] = [
  { name: 'id', label: '编号', width: 80 },
  { name: 'configName', label: '应用', width: 100 },
  { name: 'action', label: '操作', width: 120 },
  { name: 'success', label: '成功', width: 90 },
  { name: 'createUserID', label: '创建者', width: 100 },
  { name: 'remark', label: '内容' },
  { name: 'traceId', label: '追踪', width: 160 },
  { name: 'createTime', label: '创建时间', width: 170 },
  { name: 'createIP', label: '创建地址', width: 150 },
];

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
    // 从已有历史记录中提取类别（action）去重，与 MVC FindAllActions() 语义一致
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
    console.error('[ConfigHistory] fetchActions failed', e);
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
    console.error('[ConfigHistory] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'success') return v ? '是' : '否';
  if (key === 'createTime') return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
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
  await fetchActions();
  await fetchList();
});
</script>

<template>
  <div class="list-page config-history-page">
    <div class="lp-header">
      <h2 class="lph-title">
        配置历史
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
      <!-- 自定义搜索区：还原 MVC _List_Search（类别/状态/时间） -->
      <div class="lp-card-header config-history-search">
        <div class="ch-search-grid">
          <div class="ch-search-field">
            <label class="ch-search-label">类别</label>
            <el-select
              v-model="filters.action"
              clearable
              placeholder="请选择"
              class="ch-search-input"
            >
              <el-option
                v-for="opt in actionOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ch-search-field">
            <label class="ch-search-label">状态</label>
            <el-select
              v-model="filters.success"
              clearable
              placeholder="请选择"
              class="ch-search-input"
            >
              <el-option
                v-for="opt in successOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ch-search-field ch-search-field--wide">
            <label class="ch-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="ch-search-input ch-search-datepicker"
            />
          </div>

          <div class="ch-search-actions">
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
          <el-table-column
            v-for="c in COLUMNS"
            :key="c.name"
            :prop="c.name"
            :label="c.label"
            :width="c.width"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, c.name) }}</span>
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
.config-history-page {
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

.config-history-search {
  .ch-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .ch-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .ch-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .ch-search-input {
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

  .ch-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .ch-search-actions {
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
  .config-history-search .ch-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .config-history-search .ch-search-grid {
    grid-template-columns: 1fr;
  }

  .config-history-search .ch-search-field--wide {
    grid-column: span 1;
  }
}
</style>
