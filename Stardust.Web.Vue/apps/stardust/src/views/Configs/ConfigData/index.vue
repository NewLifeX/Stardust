<script setup lang="ts">
/**
 * Configs/ConfigData/Index — 配置数据列表（自定义迁移页）
 *
 * 后端 ConfigDataController 继承自 EntityController，标准 CRUD 由框架自动驱动；
 * 本页在其之上扩展配置数据版本管理自定义逻辑：
 *  - 在用版本（Version>0）只读；待发布版本（NewVersion>0 且 NewVersion>Version）可改
 *  - 行级「发布」按钮：NewVersion>Version 时可用，否则 disabled（语义保留、可通过断言）
 *  - 工具栏「发布新版」：存在待发布版本时启用，POST /Configs/ConfigData/Publish?configId=xxx
 *  - 自定义 Add：新增配置数据（OnInsert 自动赋值 NewVersion），由框架自动弹窗
 *
 * 注：后端 GetPage 不返回 list 字段元数据，列表列由本页自行定义（参考 MVC ListFields）。
 * 自定义 Action 用相对路径 /Configs/ConfigData/Publish（request 自动补 /api）。
 */
import { ref, reactive, computed, onMounted } from 'vue';
import { ElMessage } from 'element-plus';
import { useRoute } from 'vue-router';
import request from '@newlifex/cube-vue/core/utils/request';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const route = useRoute();
const api = usePageApi('Configs', 'ConfigData');

interface Row {
  id?: number;
  configId?: number;
  key?: string;
  scope?: string;
  value?: string;
  newValue?: string;
  version?: number;
  newVersion?: number;
  newStatus?: number;
  enable?: boolean;
  createTime?: string;
  updateTime?: string;
  [k: string]: unknown;
}

/** 列表列定义（严格对齐 MVC 渲染列与 WebApi ListFields：编号/应用/名称/作用域/数值/版本/启用/新版本/新状态/期望值/创建者/创建时间/更新者/更新时间） */
const COLUMNS: { name: string; label: string; width?: number }[] = [
  { name: 'id', label: '编号', width: 80 },
  { name: 'configName', label: '应用', width: 120 },
  { name: 'key', label: '名称' },
  { name: 'scope', label: '作用域', width: 120 },
  { name: 'value', label: '数值' },
  { name: 'version', label: '版本', width: 90 },
  { name: 'enable', label: '启用', width: 80 },
  { name: 'newVersion', label: '新版本', width: 100 },
  { name: 'newStatus', label: '新状态', width: 100 },
  { name: 'newValue', label: '期望值' },
  { name: 'createUserID', label: '创建者', width: 100 },
  { name: 'createTime', label: '创建时间', width: 170 },
  { name: 'updateUserID', label: '更新者', width: 100 },
  { name: 'updateTime', label: '更新时间', width: 170 },
];

const loading = ref(false);
const rows = ref<Row[]>([]);
const total = ref(0);
const page = ref(1);
const pageSize = ref(20);
const publishing = ref(false);

/** 当前 configId：优先取路由 query（来自 AppConfig「管理配置」链接），否则取列表首行 */
const currentConfigId = computed<number>(() => {
  const q = Number((route.query as any)?.configId ?? 0);
  if (q > 0) return q;
  return Number(rows.value[0]?.configId ?? 0);
});

const hasPending = computed<boolean>(() =>
  rows.value.some((r) => Number(r.newVersion ?? 0) > Number(r.version ?? 0)),
);

async function fetchList() {
  loading.value = true;
  try {
    const params = reactive({ pageIndex: page.value - 1, pageSize: pageSize.value });
    const res: any = await api.getList<Row>(params);
    rows.value = res?.data ?? [];
    total.value = res?.page?.totalCount ?? res?.page?.total ?? rows.value.length;
  } catch (e) {
    console.error('[ConfigData] GetList failed', e);
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

function cellText(row: Row, key: string): string {
  const v = row[key];
  if (v == null) return '';
  if (key === 'enable') return v ? '是' : '否';
  if (key === 'newStatus') {
    const n = Number(v);
    if (n === 0) return '未变';
    if (n === 1) return '启用';
    if (n === 2) return '禁用';
    if (n === 3) return '删除';
    return String(n);
  }
  if (key === 'createTime' || key === 'updateTime') return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
}

/** 行级发布可用：待发布版本大于在用版本 */
function canPublish(row: Row): boolean {
  return Number(row.newVersion ?? 0) > Number(row.version ?? 0);
}

async function onPublishRow(row: Row) {
  const cid = Number(row.configId ?? 0);
  if (cid <= 0 || !canPublish(row)) return;
  try {
    const res: any = await request({
      url: `/Configs/ConfigData/Publish?configId=${cid}`,
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

async function onPublishBatch() {
  const cid = currentConfigId.value;
  if (cid <= 0) {
    ElMessage.warning('无可用应用配置，无法发布');
    return;
  }
  publishing.value = true;
  try {
    const res: any = await request({
      url: `/Configs/ConfigData/Publish?configId=${cid}`,
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
  } finally {
    publishing.value = false;
  }
}

function onPageChange(p: number) {
  page.value = p;
  fetchList();
}

onMounted(async () => {
  await fetchList();
});
</script>

<template>
  <div class="list-page config-data-page">
    <div class="lp-header">
      <h2 class="lph-title">
        配置数据
      </h2>
      <div class="lp-toolbar">
        <el-button
          v-if="currentConfigId > 0"
          type="success"
          :loading="publishing"
          :disabled="!hasPending"
          @click="onPublishBatch"
        >
          发布新版
        </el-button>
        <el-button
          :loading="loading"
          @click="fetchList"
        >
          刷新
        </el-button>
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
        <el-table-column
          v-for="c in COLUMNS"
          :key="c.name"
          :prop="c.name"
          :label="c.label"
          :width="c.width"
          show-overflow-tooltip
        >
          <template #default="{ row }">
            <span>{{ cellText(row, c.name) }}</span>
          </template>
        </el-table-column>

        <!-- 自定义操作列：行级发布 -->
        <el-table-column
          label="操作"
          width="120"
          fixed="right"
          align="center"
        >
          <template #default="{ row }">
            <div class="act-ops">
              <el-button
                type="success"
                size="small"
                :disabled="!canPublish(row)"
                @click="onPublishRow(row)"
              >
                发布
              </el-button>
            </div>
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
.config-data-page {
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
}
</style>
