<script setup lang="ts">
/**
 * Configs/AppConfig/Index — 应用配置列表（自定义迁移页）
 *
 * 后端 AppConfigController 继承自 EntityController，标准 CRUD 由框架自动驱动；
 * 本页在其之上扩展自定义逻辑：
 *  - 对齐 MVC 表头：管理配置 / 发布 / 历史 / 预览 / 审计日志 各自为独立链接列
 *      · 管理配置 / 历史 / 预览 / 审计日志 为跳转链接
 *      · 发布 为行级 AJAX action（Version < NextVersion 时禁用），POST /Configs/AppConfig/Publish
 *  - 工具栏「同步应用」按钮：POST /Configs/AppConfig/SyncApp
 *
 * 列表列完整沿用后端 GetPage 元数据的 list 字段顺序（含管理配置/发布/历史/预览/审计日志
 * 独立链接列、项目链接列），按字段分派独立列渲染以对齐 MVC；不过滤字段。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import { useRoute } from 'vue-router';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const route = useRoute();
const api = usePageApi('Configs', 'AppConfig');

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
  Name?: string;
  Version?: number;
  NextVersion?: number;
  AppSecret?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);
const syncing = ref(false);

/** 搜索：类别 + 日期范围（还原 MVC _List_Search） */
const categoryOptions = ref<{ value: string; label: string }[]>([]);
const filters = reactive({
  category: '',
  timeRange: null as [string, string] | null,
});
const hasActiveFilter = computed(() => !!filters.category || filters.timeRange != null);

function toFieldMeta(f: BackendField): FieldMeta {
  return {
    name: f.name,
    displayName: f.displayName,
    typeName: f.typeName,
    itemType: f.itemType,
    primaryKey: f.primaryKey,
    lovCode: f.lovCode,
    multiple: f.multiple,
    url: f.url,
    target: f.target,
  } as FieldMeta & { url?: string; target?: string };
}

async function fetchMeta() {
  try {
    const res: any = await api.getPage();
    const meta = (res?.data ?? res) as PageMeta;
    if (meta?.list?.length) {
      listFields.value = meta.list.map(toFieldMeta);
    }
  } catch (e) {
    console.error('[AppConfig] GetPage failed', e);
  }
}

async function fetchCategories() {
  try {
    const res: any = await request({ url: '/Configs/AppConfig/Categories', method: 'get' });
    const dic = (res && (res.data ?? res)) as Record<string, string> | null;
    const list = dic
      ? Object.keys(dic).map((k) => ({ value: k, label: dic[k] }))
      : [];
    categoryOptions.value = [{ value: '', label: '全部' }, ...list];
  } catch (e) {
    console.error('[AppConfig] fetchCategories failed', e);
    categoryOptions.value = [{ value: '', label: '全部' }];
  }
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (filters.category) params.category = filters.category;
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[AppConfig] GetList failed', e);
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}

function handleReset() {
  filters.category = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  return v == null ? '' : String(v);
}

/** 还原 MVC 链接列的 {占位符} 路由（如 项目 → /Platform/GalaxyProject?Id={ProjectId}） */
function fieldUrl(f: FieldMeta & { url?: string; target?: string }, row: Row): string {
  if (!f.url) return '';
  return f.url.replace(/\{(\w+)\}/g, (_, k) => encodeURIComponent(String(row[k] ?? '')));
}

function canPublish(row: Row): boolean {
  const v = Number(row.Version ?? 0);
  const nv = Number(row.NextVersion ?? 0);
  return v < nv;
}

function configDataUrl(row: Row): string {
  return `#/Configs/ConfigData?configId=${row.Id}`;
}
function historyUrl(row: Row): string {
  return `#/Configs/ConfigHistory?configId=${row.Id}`;
}
function previewUrl(row: Row): string {
  return `/config/getall?appId=${encodeURIComponent(row.Name ?? '')}&secret=${encodeURIComponent(row.AppSecret ?? '')}`;
}
function auditLogUrl(row: Row): string {
  return `#/Admin/Log?category=${encodeURIComponent('应用配置')}&linkId=${row.Id}`;
}

async function onPublish(row: Row) {
  try {
    const res: any = await request({
      url: `/Configs/AppConfig/Publish?configId=${row.Id}`,
      method: 'post',
    });
    if (res && res.code === 0) {
      ElMessage.success(res.message || '发布成功');
      await fetchList();
    } else {
      ElMessage.error(res?.message || '发布失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '发布失败');
  }
}

async function onSyncApp() {
  syncing.value = true;
  try {
    const res: any = await request({
      url: `/Configs/AppConfig/SyncApp`,
      method: 'post',
    });
    if (res && res.code === 0) {
      ElMessage.success(res.message || '同步成功');
      await fetchList();
    } else {
      ElMessage.error(res?.message || '同步失败');
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '同步失败');
  } finally {
    syncing.value = false;
  }
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  await fetchMeta();
  await fetchCategories();
  await fetchList();
});
</script>

<template>
  <div class="list-page app-config-page">
    <div class="lp-header">
      <h2 class="lph-title">
        应用配置
      </h2>
      <div class="lp-toolbar">
        <el-button
          type="primary"
          :loading="syncing"
          @click="onSyncApp"
        >
          同步应用
        </el-button>
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
      </div>
    </div>

    <!-- 搜索区：还原 MVC _List_Search（类别 / 日期范围） -->
    <div class="lp-search">
      <div class="lp-search-grid">
        <div class="lp-search-field">
          <label class="lp-search-label">类别</label>
          <el-select
            v-model="filters.category"
            clearable
            placeholder="请选择"
            class="lp-search-input"
          >
            <el-option
              v-for="opt in categoryOptions"
              :key="opt.value"
              :label="opt.label"
              :value="opt.value"
            />
          </el-select>
        </div>
        <div class="lp-search-field lp-search-field--wide">
          <label class="lp-search-label">时间</label>
          <el-date-picker
            v-model="filters.timeRange"
            type="daterange"
            range-separator="~"
            start-placeholder="开始日期"
            end-placeholder="结束日期"
            value-format="YYYY-MM-DD"
            class="lp-search-input lp-search-datepicker"
          />
        </div>
        <div class="lp-search-actions">
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

    <div class="lp-card">
      <el-table
        v-loading="loading"
        :data="rows"
        class="act-table"
        stripe
        border
        empty-text="暂无数据"
      >
        <!-- 按后端 GetPage list 顺序渲染，独立链接列（管理配置/发布/历史/预览/审计日志）还原为独立 el-table-column，列顺序对齐 MVC -->
        <el-table-column
          v-for="f in listFields"
          :key="f.name"
          :prop="f.name"
          :label="f.displayName || f.name"
          show-overflow-tooltip
        >
          <template #default="{ row }">
            <template v-if="f.name === 'ConfigData'">
              <el-link
                type="primary"
                :href="configDataUrl(row)"
                target="_self"
              >
                管理配置
              </el-link>
            </template>
            <template v-else-if="f.name === 'Publish'">
              <el-button
                v-if="canPublish(row)"
                type="success"
                size="small"
                @click="onPublish(row)"
              >
                发布
              </el-button>
            </template>
            <template v-else-if="f.name === 'History'">
              <el-link
                type="primary"
                :href="historyUrl(row)"
                target="_self"
              >
                历史
              </el-link>
            </template>
            <template v-else-if="f.name === 'Preview'">
              <el-link
                type="primary"
                :href="previewUrl(row)"
                target="_blank"
              >
                预览
              </el-link>
            </template>
            <template v-else-if="f.name === 'Log'">
              <el-link
                type="primary"
                :href="auditLogUrl(row)"
                target="_self"
              >
                审计日志
              </el-link>
            </template>
            <template v-else-if="(f as FieldMeta & { url?: string }).url">
              <el-link
                type="primary"
                :href="fieldUrl(f as FieldMeta & { url?: string; target?: string }, row)"
                :target="(f as FieldMeta & { target?: string }).target === '_blank' ? '_blank' : '_self'"
              >
                {{ cellText(row, f.name) }}
              </el-link>
            </template>
            <template v-else-if="f.typeName === 'Boolean'">
              <el-tag
                :type="row[f.name] ? 'success' : 'info'"
                size="small"
              >
                {{ row[f.name] ? '是' : '否' }}
              </el-tag>
            </template>
            <template v-else>
              <span>{{ cellText(row, f.name) }}</span>
            </template>
          </template>
        </el-table-column>
      </el-table>

      <div class="lp-pager">
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
.app-config-page {
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
    overflow: auto;
  }

  .act-table {
    width: 100%;
  }

  .act-ops {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
  }

  .lp-pager {
    margin-top: 16px;
    display: flex;
    justify-content: flex-end;
  }

  .lp-search {
    margin: 12px var(--el-main-padding, 24px) 0;

    .lp-search-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px 16px;
      align-items: end;
    }

    .lp-search-field {
      display: flex;
      flex-direction: column;
      gap: 6px;
      min-width: 0;

      &--wide {
        grid-column: span 2;
      }
    }

    .lp-search-label {
      font-size: 12px;
      font-weight: 500;
      color: var(--el-text-color-secondary);
      white-space: nowrap;
    }

    .lp-search-input {
      width: 100%;

      :deep(.el-input__wrapper),
      :deep(.el-select__wrapper),
      :deep(.el-range-editor) {
        min-height: 36px;
        border-radius: var(--el-border-radius-base);
        background: var(--el-bg-color);
        border: 1px solid var(--el-border-color-light);
        box-shadow: none;
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
    }

    .lp-search-datepicker :deep(.el-range-editor) {
      width: 100%;
    }

    .lp-search-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
    }
  }
}

@media (max-width: 900px) {
  .app-config-page .lp-search .lp-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .app-config-page .lp-search .lp-search-grid {
    grid-template-columns: 1fr;
  }

  .app-config-page .lp-search .lp-search-field--wide {
    grid-column: span 1;
  }
}
</style>
