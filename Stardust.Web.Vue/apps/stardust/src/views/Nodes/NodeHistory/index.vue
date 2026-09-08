<script setup lang="ts">
/**
 * Nodes/NodeHistory/Index — 节点历史列表（自定义迁移页）
 *
 * MVC 原 _List_Search 自定义搜索字段：
 *   - 节点（nodeId）：_SelectNode 下拉（这里用本列表 NodeID/NodeName 动态去重）
 *   - 操作（action）：下拉，选项来自 NodeHistory.FindAllAction()
 *   - 状态（success）：下拉，1=成功 / 0=失败
 *   - 时间：日期范围（dtStart/dtEnd）
 *
 * WebApi 端 NodeHistoryController 带 [Menu(60, false)]，菜单树不会注册该页路由，
 * 故建最小自定义视图兜底，复用标准 CRUD 列表并还原上述搜索字段。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const api = usePageApi('Nodes', 'NodeHistory');

interface Row {
  id?: number;
  nodeId?: number;
  nodeName?: string;
  action?: string;
  success?: boolean;
  remark?: string;
  traceId?: string;
  createTime?: string;
  createIP?: string;
  [k: string]: unknown;
}

// 参照 MVC NodeHistoryController.ListFields.RemoveField("ID", ...) 基准：
// MVC 列表不展示编号(ID)列，故首列从 nodeName 起，与 MVC 呈现一致。
const COLUMNS: { name: string; label: string; width?: number }[] = [
  { name: 'nodeName', label: '节点', width: 150 },
  { name: 'action', label: '操作', width: 120 },
  { name: 'success', label: '成功', width: 90 },
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
const nodeOptions = ref<{ value: string; label: string }[]>([]);

const filters = reactive({
  nodeId: '',
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
  return !!filters.nodeId || !!filters.action || filters.success !== '' || filters.timeRange != null;
});

async function fetchOptions() {
  try {
    // 从已有历史记录中提取「操作」与「节点」去重，与 MVC FindAllAction()/_SelectNode 语义一致
    const res: any = await api.getList<Row>({ pageIndex: 0, pageSize: 1000, fields: 'nodeId,nodeName,action' });
    const data = res?.data ?? [];
    const actionSet = new Set<string>();
    const nodeMap = new Map<number, string>();
    data.forEach((r: Row) => {
      if (r.action) actionSet.add(r.action);
      if (r.nodeId != null) {
        const name = r.nodeName || String(r.nodeId);
        if (!nodeMap.has(r.nodeId)) nodeMap.set(r.nodeId, name);
      }
    });
    actionOptions.value = [
      { value: '', label: '全部' },
      ...Array.from(actionSet)
        .sort((a, b) => b.localeCompare(a))
        .map((a) => ({ value: a, label: a })),
    ];
    nodeOptions.value = [
      { value: '', label: '全部' },
      ...Array.from(nodeMap.entries())
        .sort((a, b) => a[0] - b[0])
        .map(([id, name]) => ({ value: String(id), label: name })),
    ];
  } catch (e) {
    console.error('[NodeHistory] fetchOptions failed', e);
    actionOptions.value = [{ value: '', label: '全部' }];
    nodeOptions.value = [{ value: '', label: '全部' }];
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (filters.nodeId) params.nodeId = filters.nodeId;
    if (filters.action) params.action = filters.action;
    if (filters.success !== '') params.success = filters.success;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[NodeHistory] GetList failed', e);
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
  filters.nodeId = '';
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
  await fetchOptions();
  await fetchList();
});
</script>

<template>
  <div class="list-page node-history-page">
    <div class="lp-header">
      <h2 class="lph-title">
        节点历史
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
      <div class="lp-card-header node-history-search">
        <div class="nh-search-grid">
          <div class="nh-search-field">
            <label class="nh-search-label">节点</label>
            <el-select
              v-model="filters.nodeId"
              clearable
              placeholder="请选择"
              class="nh-search-input"
            >
              <el-option
                v-for="opt in nodeOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="nh-search-field">
            <label class="nh-search-label">操作</label>
            <el-select
              v-model="filters.action"
              clearable
              placeholder="请选择"
              class="nh-search-input"
            >
              <el-option
                v-for="opt in actionOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="nh-search-field">
            <label class="nh-search-label">状态</label>
            <el-select
              v-model="filters.success"
              clearable
              placeholder="请选择"
              class="nh-search-input"
            >
              <el-option
                v-for="opt in successOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="nh-search-field nh-search-field--wide">
            <label class="nh-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="nh-search-input nh-search-datepicker"
            />
          </div>

          <div class="nh-search-actions">
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
              <el-link
                v-if="c.name === 'nodeName' && row.nodeId != null"
                type="primary"
                :underline="false"
                :href="`#/Nodes/Node?Id=${row.nodeId}`"
                target="_blank"
              >
                {{ row.nodeName || row.nodeId }}
              </el-link>
              <span v-else>{{ cellText(row, c.name) }}</span>
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
.node-history-page {
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

.node-history-search {
  .nh-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .nh-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .nh-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .nh-search-input {
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

  .nh-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .nh-search-actions {
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
  .node-history-search .nh-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .node-history-search .nh-search-grid {
    grid-template-columns: 1fr;
  }

  .node-history-search .nh-search-field--wide {
    grid-column: span 1;
  }
}
</style>
