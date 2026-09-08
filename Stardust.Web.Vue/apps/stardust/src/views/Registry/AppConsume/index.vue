<script setup lang="ts">
/**
 * Registry/AppConsume/Index — 应用消费列表（自定义迁移页）
 *
 * 后端 AppConsumeController 继承自 EntityController<AppConsume>，[Menu(0, false)] 隐藏菜单页，
 * 路由由 MenuTree 注册，直接访问 #/Registry/AppConsume（通常由外部带 appId / client 跳转进入）。
 * 本页对应 MVC 源 AppConsumeController.cs / _List.cshtml：
 *  - 后端静态构造函数移除 ServiceId，并对以下字段设置独立链接列（独立跳转，不合并）：
 *      ServiceName  DisplayName="{ServiceName}"  Url="/Registry/Service?name={ServiceName}"        Target="_blank"
 *      NodeName     Header="节点"  DisplayName="{NodeName}"  Url="/Nodes/Node?Id={NodeId}"          Target="_blank"
 *      Client       Url="/Registry/AppConsume?appId={AppId}&client={Client}"                       （站内）
 *  - 搜索参数（Search 方法）：appId、client、serviceId、enable、Q（关键字）
 *
 * 列集合完全由后端 GetPage 元数据的 list 字段驱动（保留后端顺序，含独立链接列，不硬编码、不合并），
 * 通过 usePageApi 对接，分页 pageIndex: page-1，总条数 res.page.totalCount。
 * 含 url 的字段自动渲染为链接列（替换 {Field} 占位符），匹配 MVC 呈现。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'AppConsume');

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
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

// 搜索栏（对应 MVC Search：Q 关键字 + enable 启用状态）
const filters = reactive({
  q: '',
  enable: '' as string, // ''=全部, 'true'=启用, 'false'=禁用
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
      // 保留后端完整顺序（含独立链接列），由模板按字段逐列渲染对齐 MVC 呈现
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Registry/AppConsume] GetPage failed', e);
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
    console.error('[Registry/AppConsume] GetList failed', e);
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

/** 将后端 Url 模板中的 {Field} 替换为行数据值，构造跳转链接 */
function buildLink(f: FieldMeta, row: Row): string {
  const tpl = f.url;
  if (!tpl) return '';
  return tpl.replace(/\{(\w+)\}/g, (_, name: string) => {
    const v = row[name];
    return v == null ? '' : String(v);
  });
}

function isLinkField(f: FieldMeta): boolean {
  return !!f.url && f.url.length > 0;
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
  console.log('[Registry/AppConsume] custom view mounted');
  await fetchMeta();
  await fetchList();
});
</script>

<template>
  <div class="list-page registry-appconsume-page">
    <div class="lp-header">
      <h2 class="lph-title">
        消费统计
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
      <div class="lp-card-header registry-appconsume-search">
        <div class="rac-search-grid">
          <div class="rac-search-field">
            <label class="rac-search-label">关键字</label>
            <el-input
              v-model="filters.q"
              clearable
              placeholder="服务名 / 客户端 / 地址"
              class="rac-search-input"
            />
          </div>

          <div class="rac-search-field">
            <label class="rac-search-label">启用</label>
            <el-select
              v-model="filters.enable"
              clearable
              placeholder="全部"
              class="rac-search-input"
            >
              <el-option
                label="全部"
                value=""
              />
              <el-option
                label="启用"
                value="true"
              />
              <el-option
                label="禁用"
                value="false"
              />
            </el-select>
          </div>

          <div class="rac-search-actions">
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
          class="rac-table"
          stripe
          border
          empty-text="暂无数据"
        >
          <el-table-column
            v-for="f in listFields"
            :key="f.name"
            :prop="f.name"
            :label="f.displayName || f.name"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <!-- 独立链接列：按后端 url 模板渲染，不合并 -->
              <template v-if="isLinkField(f)">
                <el-link
                  type="primary"
                  :href="buildLink(f, row)"
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
.registry-appconsume-page {
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

.registry-appconsume-search {
  .rac-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .rac-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;
  }

  .rac-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .rac-search-input {
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

    :deep(.el-input__inner) {
      font-size: 13px;
      color: var(--el-text-color-primary);
    }
  }

  .rac-search-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    padding-bottom: 0;
  }
}

.rac-table {
  width: 100%;
}

@media (max-width: 900px) {
  .registry-appconsume-search .rac-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .registry-appconsume-search .rac-search-grid {
    grid-template-columns: 1fr;
  }
}
</style>
