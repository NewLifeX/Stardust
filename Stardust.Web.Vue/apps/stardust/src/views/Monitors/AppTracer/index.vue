<script setup lang="ts">
/**
 * Monitors/AppTracer/Index — 应用跟踪器列表（自定义迁移页）
 *
 * 后端 AppTracerController [Menu(90)] 可见页，路由由菜单自动注册，直接访问 #/Monitors/AppTracer。
 * 对应 MVC 源 AppTracerController.cs / _List_Data.cshtml：
 *  - 自建 el-table 还原后端列表字段（标准列来自 GetPage 元数据，过滤掉后端自定义 url 跳转字段）
 *  - 自定义链接列（AutoPage 不渲染 Url 链接列，跳转按 MVC 源 #/... 自建）：
 *      每日监控  → #/Monitors/AppDaystat?monitorId={ID}
 *      埋点      → #/Monitors/TraceItem?appId={ID}        （原 ItemCount 字段）
 *      项目      → #/Platform/GalaxyProject?projectId={ProjectId} （原 ProjectName 字段）
 *      审计日志  → #/Admin/Log?category=应用跟踪器&linkId={ID}
 *  - 批量工具栏（选中后可见）：Fix / FixOldData 修正 Action
 *      GET /Monitors/AppTracer/Fix        （相对无前缀路径，request 自动拼 baseURL）
 *      GET /Monitors/AppTracer/FixOldData
 *      成功 ElMessage + 刷新
 *
 * 列表用 usePageApi('Monitors','AppTracer').getList，分页 pageIndex: page-1，总条数 res.page.totalCount。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Monitors', 'AppTracer');

/** 后端下发的自定义 url 跳转字段（自建链接列，不进入自动表格列） */
const SKIP_FIELDS = new Set(['ProjectName', 'DayMonitor', 'Log']);

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
  ID?: number;
  ProjectId?: number;
  ProjectName?: string;
  Name?: string;
  DisplayName?: string;
  Category?: string;
  ItemCount?: number;
  Days?: number;
  Total?: number;
  Enable?: boolean;
  Mode?: number;
  Period?: number;
  MaxSamples?: number;
  MaxErrors?: number;
  EnableMeter?: boolean;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

const selectedIds = ref<number[]>([]);

// 搜索条件（对照 MVC _List_Search.cshtml：分类 / 启用 / 日期范围）
const search = reactive({
  category: '' as string,
  enable: '' as string,
  dtStart: '' as string,
  dtEnd: '' as string,
});
const categoryOptions = ref<{ label: string; value: string }[]>([]);

async function fetchCategories() {
  // WebApi 未提供 GetCategoryList 接口（MVC 原版有），改为从已加载列表数据本地去重生成，
  // 与 Deployment/AppDeploy 的做法一致，避免无效 404 请求。
  const set = new Set<string>();
  for (const r of rows.value) {
    const v = (r as any)?.category ?? (r as any)?.Category;
    if (v != null && v !== '') set.add(String(v));
  }
  categoryOptions.value = Array.from(set).map((v) => ({ label: v, value: v }));
}

function onSearch() {
  page.value = 1;
  fetchList();
}
function onReset() {
  search.category = '';
  search.enable = '';
  search.dtStart = '';
  search.dtEnd = '';
  page.value = 1;
  fetchList();
}

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
    console.error('[Monitors/AppTracer] GetPage failed', e);
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
      category: search.category || undefined,
      enable: search.enable === '' ? undefined : search.enable === 'true',
      dtStart: search.dtStart || undefined,
      dtEnd: search.dtEnd || undefined,
    };
    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Monitors/AppTracer] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  return v == null ? '' : String(v);
}

// 自建链接列
function dayMonitorUrl(row: Row): string {
  return `#/Monitors/AppDaystat?monitorId=${row.ID}`;
}
function itemUrl(row: Row): string {
  return `#/Monitors/TraceItem?appId=${row.ID}`;
}
function projectUrl(row: Row): string {
  return `#/Platform/GalaxyProject?projectId=${row.ProjectId}`;
}
function auditLogUrl(row: Row): string {
  return `#/Admin/Log?category=${encodeURIComponent('应用跟踪器')}&linkId=${row.ID}`;
}

// 批量选择
function onSelectionChange(selection: Row[]) {
  selectedIds.value = selection.map((r) => r.ID as number).filter((id) => id != null);
}

// 批量修正 Action
const acting = ref(false);
async function onFix(action: 'Fix' | 'FixOldData') {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请先勾选应用');
    return;
  }
  acting.value = true;
  try {
    const res: any = await request({
      url: `/Monitors/AppTracer/${action}`,
      method: 'get',
      params: { keys: selectedIds.value.join(',') },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || (action === 'Fix' ? '修正完成！' : '修正旧数据完成！'));
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
  console.log('[Monitors/AppTracer] custom view mounted');
  await fetchMeta();
  await fetchList();
  fetchCategories();
});
</script>

<template>
  <div class="list-page app-tracer-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用跟踪器
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
          @click="onFix('FixOldData')"
        >
          <el-icon><RefreshLeft /></el-icon>修正旧数据
        </el-button>
        <el-button
          :loading="loading"
          @click="handleRefresh"
        >
          刷新
        </el-button>
      </div>
    </div>

    <!-- 搜索栏：对照 MVC _List_Search.cshtml（分类 / 启用 / 日期范围） -->
    <div class="lp-search">
      <el-select
        v-model="search.category"
        placeholder="分类"
        clearable
        style="width: 160px"
      >
        <el-option
          v-for="o in categoryOptions"
          :key="o.value"
          :label="o.label"
          :value="o.value"
        />
      </el-select>
      <el-select
        v-model="search.enable"
        placeholder="启用"
        clearable
        style="width: 120px"
      >
        <el-option
          label="启用"
          value="true"
        />
        <el-option
          label="禁用"
          value="false"
        />
      </el-select>
      <el-date-picker
        v-model="search.dtStart"
        type="date"
        placeholder="开始日期"
        value-format="YYYY-MM-DD"
        style="width: 160px"
      />
      <span class="lp-search-sep">~</span>
      <el-date-picker
        v-model="search.dtEnd"
        type="date"
        placeholder="结束日期"
        value-format="YYYY-MM-DD"
        style="width: 160px"
      />
      <el-button
        type="primary"
        @click="onSearch"
      >
        查询
      </el-button>
      <el-button @click="onReset">
        重置
      </el-button>
    </div>

    <div class="lp-card">
      <div class="lp-card-body">
        <el-table
          v-loading="loading"
          :data="rows"
          class="at-table"
          stripe
          border
          empty-text="暂无数据"
          @selection-change="onSelectionChange"
        >
          <el-table-column
            type="selection"
            width="48"
          />

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

          <!-- 自定义链接列：每日监控 -->
          <el-table-column
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

          <!-- 自定义链接列：埋点（原 ItemCount 字段） -->
          <el-table-column
            label="埋点"
            width="110"
          >
            <template #default="{ row }">
              <el-link
                type="primary"
                :href="itemUrl(row)"
                target="_self"
              >
                埋点
              </el-link>
            </template>
          </el-table-column>

          <!-- 自定义链接列：项目（原 ProjectName 字段） -->
          <el-table-column
            label="项目"
            width="160"
          >
            <template #default="{ row }">
              <el-link
                v-if="row.ProjectName"
                type="primary"
                :href="projectUrl(row)"
                target="_self"
              >
                {{ row.ProjectName }}
              </el-link>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 自定义链接列：审计日志 -->
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
.app-tracer-page {
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

  .lp-search {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
    padding: 0 var(--el-main-padding, 24px);
    margin: 12px 0;

    .lp-search-sep {
      color: var(--el-text-color-secondary);
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

.at-table {
  width: 100%;
}
</style>
