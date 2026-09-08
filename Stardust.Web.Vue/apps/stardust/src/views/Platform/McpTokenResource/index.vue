<script setup lang="ts">
/**
 * Platform/McpTokenResource/Index — MCP 令牌资源授权列表（自定义迁移页）
 *
 * MVC 源 McpTokenResourceController.cs + 实体 McpTokenResource.cs 还原：
 *  - [Menu(20, true)] 可见页，路由由 MenuTree 注册，直接访问 #/Platform/McpTokenResource
 *  - 控制器 ListFields.RemoveField("Remark","CreateIP") + RemoveCreateField
 *    → 列表列（由后端 GetPage 元数据提供）：
 *        编号 / 令牌(TokenId) / 资源类型 / 资源编号 / 全部资源 / 启用 / 授权时间
 *  - 无自定义 DataAction，故不建自定义操作列；列表列全部来自后端 GetPage 元数据。
 *  - "全部资源"(IsAll)、"启用"(Enable) 为布尔字段，前端以「是/否」呈现。
 *
 * 前端经 usePageApi + request 对接，分页 pageIndex: page-1，总条数 res.page.totalCount。
 */
import { ref, reactive, onMounted } from 'vue';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import type { FieldMeta } from '@newlifex/cube-vue/core/types/field';

const api = usePageApi('Platform', 'McpTokenResource');

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
  TokenId?: number;
  ResourceType?: string;
  ResourceId?: number;
  IsAll?: boolean;
  Enable?: boolean;
  CreateTime?: string;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const listFields = ref<FieldMeta[]>([]);

// 布尔字段（由后端元数据 typeName 推断，统一以「是/否」呈现）
const BOOL_FIELDS = new Set(['IsAll', 'Enable']);

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
        .filter((f) => !BOOL_FIELDS.has(f.name) || true)
        .map(toFieldMeta);
    }
  } catch (e) {
    console.error('[Platform/McpTokenResource] GetPage failed', e);
  }
}

function normalizeRow(row: Row): Row {
  // 后端 GetList 返回 camelCase 字段（如 id/tokenId/isAll/enable/createTime），
  // 而后端 ListFields 元数据使用 PascalCase（如 Id/TokenId/IsAll/Enable/CreateTime）。
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
    const res: any = await api.getList<Row>(params);
    rows.value = (res?.data ?? []).map(normalizeRow);
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[Platform/McpTokenResource] GetList failed', e);
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, f: FieldMeta): string {
  const v = row[f.name];
  if (v == null) return '';
  if (BOOL_FIELDS.has(f.name)) return v ? '是' : '否';
  // 日期字符串截断到秒，与 MVC 默认呈现一致
  if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}/.test(v)) {
    return v.length > 19 ? v.slice(0, 19) : v;
  }
  return String(v);
}

function handleSearch() {
  page.value = 1;
  fetchList();
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  console.log('[Platform/McpTokenResource] custom view mounted');
  await fetchMeta();
  await fetchList();
});
</script>

<template>
  <div class="list-page mcp-token-res-page">
    <div class="lp-header">
      <h2 class="lph-title">
        MCP 令牌资源授权
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
            min-width="110"
            show-overflow-tooltip
          >
            <template #default="{ row }">
              <span>{{ cellText(row, f) }}</span>
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
.mcp-token-res-page {
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

.act-table {
  width: 100%;
}
</style>
