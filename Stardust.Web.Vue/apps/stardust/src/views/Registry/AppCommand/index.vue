<script setup lang="ts">
/**
 * Registry/AppCommand/Index — 应用命令 / 发布队列（自定义迁移页）
 *
 * 与 MVC 源（Areas/Registry/Controllers/AppCommandController.cs + 通用 List 模板）做字段/呈现级对齐：
 *  - 后端 ListFields 静态构造：
 *      RemoveField("StartTime","Expire","UpdateUserId") 移除 开始执行时间/过期时间/更新者
 *      RemoveCreateField()                               移除 创建者组扩展列（CreateUserID/CreateTime/CreateIP）
 *      AddListField("Expire", null, "Result")            在 结果 前插回 过期时间
 *      AddListField("CreateUser", "UpdateTime")          在 更新时间 前插回 创建者
 *      Command 配置独立链接 Url：/Registry/AppCommand?appId={AppId}&command={Command}
 *  - 搜索字段（通用 List 模板 + Search 参数）：应用(appId)、命令(command)、时间(dtStart/dtEnd)
 *  - 列表列与独立链接列由后端 GetPage 元数据的 list 字段驱动渲染，天然对齐 MVC 列集合/顺序/呈现。
 *
 * 后端 API：GET /api/Registry/AppCommand（GetPage / GetList）已就绪。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Registry', 'AppCommand');

interface BackendField {
  name: string;
  displayName: string;
  typeName: string;
  primaryKey?: boolean;
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
  AppName?: string;
  Command?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

// 搜索栏
const appOptions = ref<{ value: string; label: string }[]>([]);
const filters = reactive({
  appId: '',
  command: '',
  timeRange: null as [string, string] | null,
});

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    primaryKey: f.primaryKey,
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
    console.error('[Registry/AppCommand] GetPage failed', e);
  }
}

async function fetchAppOptions() {
  try {
    // 从已有命令记录中提取「应用」去重，与 MVC 通用 List 中应用关联下拉语义一致
    const res: any = await api.getList<Row>({ pageIndex: 0, pageSize: 1000, fields: 'appId,appName' });
    const data = res?.data ?? [];
    const map = new Map<number, string>();
    data.forEach((r: Row) => {
      if (r.AppId != null) {
        const name = r.AppName || String(r.AppId);
        if (!map.has(r.AppId)) map.set(r.AppId, name);
      }
    });
    appOptions.value = [
      { value: '', label: '全部' },
      ...Array.from(map.entries())
        .sort((a, b) => a[0] - b[0])
        .map(([id, name]) => ({ value: String(id), label: name })),
    ];
  } catch (e) {
    console.error('[Registry/AppCommand] fetchAppOptions failed', e);
    appOptions.value = [{ value: '', label: '全部' }];
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (filters.appId) params.appId = filters.appId;
    if (filters.command) params.command = filters.command;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Registry/AppCommand] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  // 后端下发的日期字段统一裁剪为 yyyy-MM-dd HH:mm:ss
  if (key === 'Expire' || key === 'StartTime' || key === 'UpdateTime' || key === 'CreateTime') {
    return String(v).replace('T', ' ').slice(0, 19);
  }
  return String(v);
}

const hasActiveFilter = computed(
  () => !!filters.appId || !!filters.command || filters.timeRange != null,
);

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.appId = '';
  filters.command = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}
function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  console.log('[Registry/AppCommand] custom view mounted');
  await fetchMeta();
  await fetchAppOptions();
  await fetchList();
});
</script>

<template>
  <div class="list-page registry-app-command-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用命令
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
      <div class="lp-card-header registry-app-command-search">
        <div class="rac-search-grid">
          <div class="rac-search-field">
            <label class="rac-search-label">应用</label>
            <el-select
              v-model="filters.appId"
              clearable
              placeholder="全部"
              class="rac-search-input"
            >
              <el-option
                v-for="opt in appOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="rac-search-field">
            <label class="rac-search-label">命令</label>
            <el-input
              v-model="filters.command"
              clearable
              placeholder="请输入命令"
              class="rac-search-input"
            />
          </div>

          <div class="rac-search-field rac-search-field--wide">
            <label class="rac-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="rac-search-input rac-search-datepicker"
            />
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
          class="act-table"
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
              <!-- 独立链接列：Command 配置后端 Url，按后端元数据 url 字段渲染（不合并） -->
              <el-link
                v-if="f.url && row[f.name] != null && row[f.name] !== ''"
                type="primary"
                :href="f.url.replace('{AppId}', row.AppId ?? '').replace('{Command}', row.Command ?? '')"
                target="_blank"
              >
                {{ row[f.name] }}
              </el-link>
              <span v-else>{{ cellText(row, f.name) }}</span>
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
.registry-app-command-page {
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

.registry-app-command-search {
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

    &--wide {
      grid-column: span 2;
    }
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

  .rac-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .rac-search-actions {
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
  .registry-app-command-search .rac-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .registry-app-command-search .rac-search-grid {
    grid-template-columns: 1fr;
  }

  .registry-app-command-search .rac-search-field--wide {
    grid-column: span 1;
  }
}
</style>
