<script setup lang="ts">
/**
 * Platform/McpToken/Index — MCP 令牌列表（自定义迁移页）
 *
 * MVC 源 McpTokenController.cs + _Form_Body.cshtml 还原：
 *  - [Menu(30, true)] 可见页，路由由 MenuTree 注册，直接访问 #/Platform/McpToken
 *  - 控制器 ListFields.RemoveField("Token","LastIP","TraceId") + RemoveCreateField/RemoveRemarkField
 *    → 列表列：编号 / 名称 / 启用 / 过期时间 / 最后调用时间 / 调用次数 等（由后端 GetPage 元数据提供）
 *  - 自定义操作列 Reset（DataAction="action"）：
 *      Url = /Platform/McpToken/Reset?id={Id}  → 重置 Token 字符串，旧 Token 立即失效
 *  - 资源授权（_Form_Body.cshtml）：编辑/新建时表单勾选项目/节点/应用授权，后端 OnInsert/OnUpdate 解析保存（列表页无此列）
 *  - 基类操作列（EntityController 默认）：启用/禁用（切换 Enable） + 删除（Delete）
 *  - 搜索（Search 重写）：Q（关键字） / token / enable / dtStart / dtEnd（以控制器 Search() 为准）
 *
 * 前端经 usePageApi + request，自定义 Action 写相对无前缀路径 /Platform/McpToken/Reset
 * （request 会自动拼 baseURL，不要写 /api/）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Platform', 'McpToken');

/** 后端下发的自定义操作列（自建按钮，不进入自动表格列） */
const SKIP_FIELDS = new Set(['Reset']);

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
  Enable?: boolean;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

// 搜索栏（以控制器 Search() 支持参数为准）
const enableOptions = [
  { value: '', label: '全部' },
  { value: 'true', label: '启用' },
  { value: 'false', label: '禁用' },
];
const filters = reactive({
  token: '',
  enable: '',
  keyword: '',
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
    console.error('[Platform/McpToken] GetPage failed', e);
  }
}

function normalizeRow(row: Row): Row {
  // 后端 GetList 返回 camelCase 字段（如 id/name/enable/expireTime），
  // 而后端 ListFields 元数据使用 PascalCase（如 Id/Name/Enable/ExpireTime）。
  // 为保持模板与字段元数据一致，额外映射一份 PascalCase 字段，避免单元格取不到值。
  const clone = { ...row } as Row;
  Object.keys(row).forEach((k) => {
    const pascal = k.charAt(0).toUpperCase() + k.slice(1);
    if (!(pascal in clone)) (clone as any)[pascal] = row[k as keyof Row];
  });
  return clone;
}

async function fetchList() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      pageIndex: page.value - 1,
      pageSize: pageSize.value,
    };
    if (filters.token) params.token = filters.token;
    if (filters.enable !== '') params.enable = filters.enable === 'true';
    if (filters.keyword.trim()) params.Q = filters.keyword.trim();
    if (filters.timeRange?.[0]) params.dtStart = filters.timeRange[0];
    if (filters.timeRange?.[1]) params.dtEnd = filters.timeRange[1];

    const res: any = await api.getList<Row>(params);
    rows.value = (res?.data ?? []).map(normalizeRow);
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Platform/McpToken] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (typeof v === 'boolean') return v ? '是' : '否';
  // 后端返回日期为字符串（ISO 或 yyyy-MM-dd HH:mm:ss），截断到秒，与 MVC 默认呈现一致
  if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v)) {
    return v.length > 19 ? v.slice(0, 19) : v;
  }
  return String(v);
}

// 自定义 Action Reset：GET /Platform/McpToken/Reset?id=<行Id> → JsonRefresh
const resetting = ref(false);
async function onReset(row: Row) {
  if (row.Id == null) {
    ElMessage.warning('缺少令牌编号');
    return;
  }
  resetting.value = true;
  try {
    const res: any = await request({
      url: `/Platform/McpToken/Reset`,
      method: 'get',
      params: { id: row.Id },
    });
    if (res && (res.code === 0 || res.code === 200 || res.success)) {
      ElMessage.success(res.message || 'Token已重置！');
      await fetchList();
    } else if (res?.message) {
      ElMessage.warning(res.message);
    } else {
      ElMessage.success('Token已重置！');
      await fetchList();
    }
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '重置Token失败');
  } finally {
    resetting.value = false;
  }
}

// 基类操作列：启用/禁用（切换 Enable 字段） + 删除（Delete）
const acting = ref(false);

// 启用/禁用：MVC EntityController.Enable 等价切换 Enable 后保存
async function onToggleEnable(row: Row) {
  if (row.Id == null) {
    ElMessage.warning('缺少令牌编号');
    return;
  }
  acting.value = true;
  try {
    await api.update({ Id: row.Id, Enable: !row.Enable });
    ElMessage.success(row.Enable ? '已禁用' : '已启用');
    await fetchList();
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '操作失败');
  } finally {
    acting.value = false;
  }
}

async function onDelete(row: Row) {
  if (row.Id == null) {
    ElMessage.warning('缺少令牌编号');
    return;
  }
  try {
    await ElMessageBox.confirm(`确认删除令牌「${row.Name || row.Id}」？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确认',
      cancelButtonText: '取消',
    });
  } catch {
    return;
  }
  acting.value = true;
  try {
    await api.remove(row.Id);
    ElMessage.success('已删除');
    await fetchList();
  } catch (e: any) {
    ElMessage.error(e?.response?.data?.message || e?.message || '删除失败');
  } finally {
    acting.value = false;
  }
}

function handleSearch() {
  page.value = 1;
  fetchList();
}
function handleReset() {
  filters.token = '';
  filters.enable = '';
  filters.keyword = '';
  filters.timeRange = null;
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

const hasActiveFilter = computed(
  () => !!filters.token || filters.enable !== '' || !!filters.keyword.trim() || filters.timeRange != null,
);

onMounted(async () => {
  console.log('[Platform/McpToken] custom view mounted');
  await fetchMeta();
  await fetchList();
});
</script>

<template>
  <div class="list-page mcp-token-page">
    <div class="lp-header">
      <h2 class="lph-title">
        MCP 令牌
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
      <div class="lp-card-header mt-search">
        <div class="mt-search-grid">
          <div class="mt-search-field">
            <label class="mt-search-label">关键字</label>
            <el-input
              v-model="filters.keyword"
              clearable
              placeholder="名称/编号"
              class="mt-search-input"
            />
          </div>

          <div class="mt-search-field">
            <label class="mt-search-label">Token</label>
            <el-input
              v-model="filters.token"
              clearable
              placeholder="Token 关键字"
              class="mt-search-input"
            />
          </div>

          <div class="mt-search-field">
            <label class="mt-search-label">启用</label>
            <el-select
              v-model="filters.enable"
              clearable
              placeholder="全部"
              class="mt-search-input"
            >
              <el-option
                v-for="opt in enableOptions"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
              />
            </el-select>
          </div>

          <div class="mt-search-field mt-search-field--wide">
            <label class="mt-search-label">时间</label>
            <el-date-picker
              v-model="filters.timeRange"
              type="daterange"
              range-separator="~"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              value-format="YYYY-MM-DD"
              class="mt-search-input mt-search-datepicker"
            />
          </div>

          <div class="mt-search-actions">
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
            min-width="90"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, f.name) }}</span>
            </template>
          </el-table-column>

          <!-- 自定义操作列：重置Token（GET /Platform/McpToken/Reset?id=） -->
          <el-table-column
            label="重置Token"
            width="110"
            align="center"
            fixed="right"
          >
            <template #default="{ row }">
              <el-button
                v-if="row.Id != null"
                type="warning"
                link
                :loading="resetting"
                @click="onReset(row)"
              >
                重置Token
              </el-button>
              <span v-else>-</span>
            </template>
          </el-table-column>

          <!-- 基类操作列：启用/禁用 + 删除（对齐 MVC EntityController 默认操作列） -->
          <el-table-column
            label="操作"
            width="200"
            align="center"
            fixed="right"
          >
            <template #default="{ row }">
              <el-button
                v-if="row.Id != null"
                type="primary"
                link
                :loading="acting"
                @click="onToggleEnable(row)"
              >
                {{ row.Enable ? '禁用' : '启用' }}
              </el-button>
              <el-button
                v-if="row.Id != null"
                type="danger"
                link
                :loading="acting"
                @click="onDelete(row)"
              >
                删除
              </el-button>
              <span v-else>-</span>
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
.mcp-token-page {
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

.mt-search {
  .mt-search-grid {
    display: grid;
    grid-template-columns: repeat(4, 1fr);
    gap: 12px 16px;
    align-items: end;
  }

  .mt-search-field {
    display: flex;
    flex-direction: column;
    gap: 6px;
    min-width: 0;

    &--wide {
      grid-column: span 2;
    }
  }

  .mt-search-label {
    font-size: 12px;
    font-weight: 500;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .mt-search-input {
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

  .mt-search-datepicker {
    :deep(.el-range-editor) {
      width: 100%;
    }
  }

  .mt-search-actions {
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
  .mt-search .mt-search-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 560px) {
  .mt-search .mt-search-grid {
    grid-template-columns: 1fr;
  }

  .mt-search .mt-search-field--wide {
    grid-column: span 1;
  }
}
</style>
