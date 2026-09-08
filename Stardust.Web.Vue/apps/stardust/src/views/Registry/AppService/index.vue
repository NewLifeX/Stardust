<script setup lang="ts">
/**
 * Registry/AppService/Index — 应用服务 / 消费端（自定义迁移页）
 *
 * 后端 AppServiceController 继承自 EntityController<AppService>，[Menu(0,false)]，路由由 MenuTree 注册，
 * 直接访问 #/Registry/AppService。本页对齐 MVC 源 AppServiceController.cs：
 *  - 标准列表列来自后端 GetPage 元数据的 list 字段（保留完整顺序，含独立链接列，不合并）
 *  - 后端定制的三列独立链接（MVC ListFields 构造）：
 *      ServiceName → /Registry/Service?name={ServiceName}        （_blank）
 *      NodeName    → /Nodes/Node?Id={NodeId}                     （_blank）
 *      Client      → /Registry/AppService?appId={AppId}&client={Client}
 *  - 搜索栏：关键字 Q + 启用状态 enable（对应 MVC 高级搜索 appId/serviceId/client/enable/Q）
 *
 * 列名权威 = 实体 DisplayName（由后端 list.displayName 下发），以 GetPage 元数据为准对齐 MVC 呈现。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'AppService');

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
  AppId?: number;
  ServiceName?: string;
  Client?: string;
  NodeId?: number;
  NodeName?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

// 搜索栏（对齐 MVC 高级搜索）
const filters = reactive({
  q: '',
  enable: '' as string, // '' 全部 / 'true' / 'false'
});

const enableOptions = [
  { value: '', label: '全部' },
  { value: 'true', label: '已启用' },
  { value: 'false', label: '已禁用' },
];

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

// 独立链接列占位符替换（如 /Registry/Service?name={ServiceName}）
function fillTemplate(tpl: string, row: Row): string {
  return tpl.replace(/\{([^}]+)\}/g, (_m, key: string) => {
    const v = row[key];
    return v == null ? '' : String(v);
  });
}

// 列 url 来源优先级：后端字段 url > 内置映射
function fieldUrl(f: FieldMeta, row: Row): string | null {
  if (f.url) return fillTemplate(f.url, row);
  return null;
}

async function fetchMeta() {
  try {
    const res: any = await api.getPage();
    const meta = (res?.data ?? res) as PageMeta;
    if (meta?.list?.length) {
      // 保留后端完整顺序（含独立链接列），由模板按字段逐列渲染对齐 MVC 呈现
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Registry/AppService] GetPage failed', e);
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (filters.q) params.Q = filters.q;
    if (filters.enable !== '') params.enable = filters.enable === 'true';

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Registry/AppService] GetList failed', e);
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

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.q = '';
  filters.enable = '';
  page.value = 1;
  fetchList();
}
function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

const hasActiveFilter = computed(() => !!filters.q || filters.enable !== '');

onMounted(async () => {
  console.log('[Registry/AppService] custom view mounted');
  await fetchMeta();
  await fetchList();
});
</script>

<template>
  <div class="list-page registry-appservice-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用服务 / 消费端
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
      <div class="lp-card-header registry-appservice-search">
        <div class="ras-search-grid">
          <div class="ras-search-field">
            <label class="ras-search-label">关键字</label>
            <el-input
              v-model="filters.q"
              clearable
              placeholder="服务名 / 客户端 / 地址 / 标签"
              class="ras-search-input"
              @keyup.enter="handleSearch"
            />
          </div>

          <div class="ras-search-field">
            <label class="ras-search-label">启用</label>
            <el-select
              v-model="filters.enable"
              clearable
              placeholder="全部"
              class="ras-search-input"
            >
              <el-option
                v-for="opt in enableOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="ras-search-actions">
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
          class="as-table"
          stripe
          border
          empty-text="暂无数据"
        >
          <el-table-column
            v-for="f in listFields"
            :key="f.name"
            :prop="f.name"
            :label="f.displayName || f.name"
            :fixed="f.name === 'Id' ? 'left' : false"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <!-- 独立链接列：按后端字段名渲染，不合并 -->
              <template v-if="fieldUrl(f, row)">
                <el-link
                  type="primary"
                  :href="fieldUrl(f, row) as string"
                  :target="f.target === '_blank' ? '_blank' : '_self'"
                >
                  {{ cellText(row, f.name) }}
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
.registry-appservice-page {
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

.registry-appservice-search {
  .ras-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .ras-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;
  }

  .ras-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .ras-search-input {
    width: 100%;

    :deep(.el-input__wrapper),
    :deep(.el-select__wrapper) {
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
    :deep(.el-select__wrapper.is-focused) {
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

  .ras-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.as-table {
  width: 100%;
}

@media (max-width: 900px) {
  .registry-appservice-search .ras-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .registry-appservice-search .ras-search-grid {
    grid-template-columns: 1fr;
  }
}
</style>
