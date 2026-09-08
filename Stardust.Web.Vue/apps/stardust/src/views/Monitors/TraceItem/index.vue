<script setup lang="ts">
/**
 * Monitors/TraceItem/Index — 埋点（跟踪项）列表（自定义迁移页）
 *
 * 后端 TraceItemController [Menu(65, false)] 隐藏页，菜单不注册路由，
 * 靠框架 DefaultEntity 的 catch-all + hasMatchingCustomView 自定义视图兜底，可访问 #/Monitors/TraceItem。
 * 对应 MVC 源 TraceItemController.cs / _List_Data.cshtml：
 *  - 自建 el-table 还原后端列表字段（标准列来自 GetPage 元数据，过滤掉后端自定义 url 跳转字段）
 *  - 自定义链接列（AutoPage 不渲染 Url 链接列，跳转按 MVC 源 #/... 自建）：
 *      每日监控 → #/Monitors/TraceDayStat?appId={AppId}&itemId={Id}（紧贴"操作名"列之后）
 *      审计日志 → #/Admin/Log?category=跟踪项&linkId={Id}（末列）
 *  - 批量工具栏（选中后可见）：Fix / FixDisplay 修正显示名 Action
 *      GET /Monitors/TraceItem/Fix
 *      GET /Monitors/TraceItem/FixDisplay
 *      成功 ElMessage + 刷新
 *
 * 列表用 usePageApi('Monitors','TraceItem').getList，分页 pageIndex: page-1，总条数 res.page.totalCount。
 */
import { ref, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Monitors', 'TraceItem');

/** 后端下发的自定义 url 跳转字段（自建链接列，不进入自动表格列） */
const SKIP_FIELDS = new Set(['Monitor', 'Log']);

interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  itemType?: string;
  primaryKey?: boolean;
  lovCode?: string;
  multiple?: boolean;
}
interface PageMeta {
  list: BackendField[];
  [k: string]: unknown;
}
interface Row {
  Id?: number;
  AppId?: number;
  Kind?: string;
  Name?: string;
  DisplayName?: string;
  Enable?: boolean;
  Rules?: string;
  Cloned?: boolean;
  Timeout?: number;
  Days?: number;
  Total?: number;
  Errors?: number;
  Cost?: number;
  AlarmThreshold?: number;
  AlarmErrorRate?: number;
  MaxRingRate?: number;
  MinRingRate?: number;
  AlarmGroup?: string;
  TraceId?: string;
  CreateTime?: string;
  UpdateUser?: string;
  UpdateTime?: string;
  Remark?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

const selectedIds = ref<number[]>([]);

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    itemType: f.itemType,
    primaryKey: f.primaryKey,
    lovCode: f.lovCode,
    multiple: f.multiple,
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
    console.error('[Monitors/TraceItem] GetPage failed', e);
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Monitors/TraceItem] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'CreateTime' || key === 'UpdateTime') {
    return String(v).replace('T', ' ').slice(0, 19);
  }
  return String(v);
}

// 自建链接列
function dayMonitorUrl(row: Row): string {
  return `#/Monitors/TraceDayStat?appId=${row.AppId}&itemId=${row.Id}`;
}
function auditLogUrl(row: Row): string {
  return `#/Admin/Log?category=${encodeURIComponent('跟踪项')}&linkId=${row.Id}`;
}

// 批量选择
function onSelectionChange(selection: Row[]) {
  selectedIds.value = selection.map((r) => r.Id as number).filter((id) => id != null);
}

// 批量修正 Action
const acting = ref(false);
async function onFix(action: 'Fix' | 'FixDisplay') {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选跟踪项');
    return;
  }
  acting.value = true;
  try {
    const res: any = await request({
      url: `/Monitors/TraceItem/${action}`,
      method: 'get',
      params: { keys: selectedIds.value.join(',') },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || (action === 'Fix' ? '修正完成！' : '修正显示名完成！'));
      await fetchList();
    } else {
      ElMessage.error(res?.message || '操作失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  } finally {
    acting.value = false;
  }
}

function handleRefresh() {
  fetchList();
}
function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  console.log('[Monitors/TraceItem] custom view mounted');
  await fetchMeta();
  await fetchList();
});
</script>

<template>
  <div class="list-page trace-item-page">
    <div class="lp-header">
      <h2 class="lph-title">
        跟踪项（埋点）
      </h2>
      <div class="lp-toolbar">
        <el-button
          :loading="acting"
          :disabled="selectedIds.length === 0"
          @click="onFix('Fix')"
        >
          <el-icon><MagicStick /></el-icon>修正
        </el-button>
        <el-button
          :loading="acting"
          :disabled="selectedIds.length === 0"
          @click="onFix('FixDisplay')"
        >
          <el-icon><Edit /></el-icon>修正显示名
        </el-button>
        <el-button
          :loading="loading"
          @click="handleRefresh"
        >
          刷新
        </el-button>
      </div>
    </div>

    <div class="lp-card">
      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="ti-table"
          stripe
          border
          empty-text="暂无数据"
          @selection-change="onSelectionChange"
        >
          <el-table-column
            type="selection"
            width="48"
          />

          <!-- 标准字段列；每日监控为 MVC 自定义链接列，紧贴"操作名"(Name) 之后（与 MVC ListFields 顺序一致） -->
          <template
            v-for="f in listFields"
            :key="f.name"
          >
            <el-table-column
              :prop="f.name"
              :label="f.displayName || f.name"
              show-overflow-tooltip
            >
              <template #default="{ row }">
                <span>{{ cellText(row, f.name) }}</span>
              </template>
            </el-table-column>

            <!-- 自定义链接列：每日监控（MVC 位于"操作名"之后） -->
            <el-table-column
              v-if="f.name === 'Name'"
              label="每日监控"
              width="110"
            >
              <template #default="{ row }">
                <el-link
                  type="primary"
                  :href="dayMonitorUrl(row)"
                  target="_self"
                >
                  每日监控
                </el-link>
              </template>
            </el-table-column>
          </template>

          <!-- 自定义链接列：审计日志（MVC 末列） -->
          <el-table-column
            label="审计日志"
            width="110"
            fixed="right"
            align="center"
          >
            <template #default="{ row }">
              <el-link
                type="info"
                :href="auditLogUrl(row)"
                target="_self"
              >
                审计日志
              </el-link>
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
.trace-item-page {
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
      align-items: center;
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

.ti-table {
  width: 100%;
}
</style>
