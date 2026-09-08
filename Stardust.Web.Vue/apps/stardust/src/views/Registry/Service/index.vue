<script setup lang="ts">
/**
 * Registry/Service/Index — 消费服务注册（自定义迁移页）
 *
 * 后端 ServiceController 继承自 EntityController<Service>，[Menu(40)] 可见页，路由由 MenuTree 注册，
 * 直接访问 #/Registry/Service。本页对接后端 GetPage 元数据（list 字段），逐列渲染对齐 MVC 呈现：
 *  - 列集合（顺序、显示名均来自后端 GetPage list 元数据，列名权威=实体 DisplayName）：
 *      Id(编号) → ProjectName(项目) → Name(名称) → DisplayName(显示名) → Category(类别)
 *      → Enable(启用) → Extranet(外网) → Singleton(单例) → UseScope(作用域)
 *      → Address(服务地址模版) → HealthCheck(健康监测)
 *      → Providers({Providers}) → Consumers({Consumers})
 *  - 独立链接列（按后端 url 字段内联渲染，不合并）：
 *      Providers → /Registry/AppService?serviceId={Id}   （MVC 源：AppService 链接列）
 *      Consumers → /Registry/AppConsume?serviceId={Id}   （MVC 源：AppConsume 链接列）
 *  - 搜索控件：名称(name) + 时间范围(dtStart/dtEnd)，对应 MVC ServiceController.Search
 *  - 空态 empty-text="暂无数据"，分页 total 来自 res.page.totalCount
 *
 * 标准列表列来自后端 GetPage 元数据的 list 字段（保留完整顺序，含独立链接列），
 * 通过 usePageApi 对接，分页 pageIndex: page-1。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'Service');

/** 后端下发的字段（list 元数据，含独立链接列） */
interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  itemType?: string;
  primaryKey?: boolean;
  lovCode?: string;
  multiple?: boolean;
  url?: string | null;
  target?: string | null;
}
interface PageMeta {
  list: BackendField[];
  [k: string]: unknown;
}
interface Row {
  Id?: number;
  ProjectId?: number;
  ProjectName?: string;
  Name?: string;
  DisplayName?: string;
  Category?: string;
  Enable?: boolean;
  Extranet?: boolean;
  Singleton?: boolean;
  UseScope?: boolean;
  Address?: string;
  HealthCheck?: boolean;
  Providers?: number;
  Consumers?: number;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

const selectedIds = ref<number[]>([]);

// 搜索栏（对应 MVC ServiceController.Search：name + dtStart/dtEnd）
const filters = reactive({
  name: '',
  timeRange: null as [string, string] | null,
});

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    itemType: f.itemType,
    primaryKey: f.primaryKey,
    lovCode: f.lovCode,
    multiple: f.multiple,
    url: f.url ?? undefined,
    target: f.target ?? undefined,
  };
}

async function fetchMeta() {
  try {
    const res: any = await api.getPage();
    const meta = (res?.data ?? res) as PageMeta;
    if (meta?.list?.length) {
      // 保留后端完整顺序（含独立链接列 Providers/Consumers），由模板按字段逐列渲染对齐 MVC 呈现
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Registry/Service] GetPage failed', e);
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (filters.name) params.name = filters.name;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Registry/Service] GetList failed', e);
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

// 独立链接列：后端 url 模板 /Registry/AppService?serviceId={Id} / /Registry/AppConsume?serviceId={Id}
function providersUrl(row: Row): string {
  return `#/Registry/AppService?serviceId=${row.Id}`;
}
function consumersUrl(row: Row): string {
  return `#/Registry/AppConsume?serviceId=${row.Id}`;
}

// 批量选择
function onSelectionChange(selection: Row[]) {
  selectedIds.value = selection.map((r) => r.Id as number).filter((id) => id != null);
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.name = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

const hasActiveFilter = computed(() => !!filters.name || filters.timeRange != null);

onMounted(async () => {
  console.log('[Registry/Service] custom view mounted');
  await fetchMeta();
  await fetchList();
});
</script>

<template>
  <div class="list-page registry-service-page">
    <div class="lp-header">
      <h2 class="lph-title">
        服务注册
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
      <div class="lp-card-header registry-service-search">
        <div class="rs-search-grid">
          <div class="rs-search-field">
            <label class="rs-search-label">名称</label>
            <el-input
              v-model="filters.name"
              clearable
              placeholder="服务名称"
              class="rs-search-input"
            />
          </div>

          <div class="rs-search-field rs-search-field--wide">
            <label class="rs-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="rs-search-input rs-search-datepicker"
            />
          </div>

          <div class="rs-search-actions">
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
          class="rct-table"
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
            :fixed="f.name === 'Name' ? 'left' : false"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <!-- 独立链接列：Providers / Consumers，按后端 url 模板渲染，不合并 -->
              <template v-if="f.name === 'Providers'">
                <el-link
                  type="primary"
                  :href="providersUrl(row)"
                  target="_self"
                >
                  {{ row.Providers }}
                </el-link>
              </template>
              <template v-else-if="f.name === 'Consumers'">
                <el-link
                  type="primary"
                  :href="consumersUrl(row)"
                  target="_self"
                >
                  {{ row.Consumers }}
                </el-link>
              </template>
              <template v-else>
                <span>{{ cellText(row, f.name) }}</span>
              </template>
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
.registry-service-page {
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

.registry-service-search {
  .rs-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .rs-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .rs-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .rs-search-input {
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

    :deep(.el-input__inner) {
      font-size: 13px;
      color: var(--el-text-color-primary);
    }
  }

  .rs-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .rs-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.rct-table {
  width: 100%;
}

@media (max-width: 900px) {
  .registry-service-search .rs-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .registry-service-search .rs-search-grid {
    grid-template-columns: 1fr;
  }

  .registry-service-search .rs-search-field--wide {
    grid-column: span 1;
  }
}
</style>
