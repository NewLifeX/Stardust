<script setup lang="ts">
/**
 * Trace/Detail — 追踪明细（自定义迁移页，根 Area 隐藏页兜底）
 *
 * MVC 源 TraceController.Index（layout=detail）→ 详细表格展示该 traceId 的每条采样。
 * 路由：#/Trace/<traceId>?layout=detail 或 #/Trace/Detail?id=<traceId>
 * 复用 SampleData 列表，纯表格（el-table）：操作名/耗时/开始-结束/节点/状态/Tag。
 *
 * 路由兜底：#/Trace/Detail 为隐藏三级路由，须在 src/main.ts 中 addRoute('/Trace/Detail', ...)。
 */
import { ref, onMounted, watch, computed } from 'vue';
import { useRoute } from 'vue-router';
import request from '@newlifex/cube-vue/core/utils/request';

const props = defineProps<{ traceId?: string }>();
const route = useRoute();

const effectiveTraceId = computed<string>(() => {
  if (props.traceId) return props.traceId.trim();
  const q = (route.query.id as string) || '';
  const p = (route.params.id as string) || '';
  return (q || p || '').trim();
});

interface DetailRow {
  id?: number | string;
  name?: string | null;
  appName?: string | null;
  clientId?: string | null;
  cost?: number;
  startTime?: number;
  endTime?: number;
  success?: boolean;
  tag?: string | null;
  error?: string | null;
  [k: string]: unknown;
}

const loading = ref(false);
const rows = ref<DetailRow[]>([]);

function fmtTime(ms?: number): string {
  if (!ms || ms <= 0) return '-';
  const d = new Date(ms);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

async function fetchData() {
  const tid = effectiveTraceId.value;
  if (!tid) {
    rows.value = [];
    return;
  }
  loading.value = true;
  try {
    const res: any = await request({
      url: '/Monitors/SampleData',
      params: { traceId: tid, pageSize: 10000 },
    });
    const list: DetailRow[] = res?.data ?? res ?? [];
    rows.value = Array.isArray(list) ? list : [];
  } catch (e) {
    console.error('[Trace/Detail] fetch failed', e);
    rows.value = [];
  } finally {
    loading.value = false;
  }
}

onMounted(fetchData);
watch(
  () => effectiveTraceId.value,
  () => fetchData(),
);
</script>

<template>
  <div class="trace-detail-page">
    <el-table
      v-loading="loading"
      :data="rows"
      stripe
      border
      empty-text="暂无追踪明细（TraceId 无采样）"
      class="td-table"
    >
      <el-table-column
        prop="name"
        label="操作名"
        min-width="200"
        show-overflow-tooltip
      />
      <el-table-column
        prop="appName"
        label="应用"
        width="160"
        show-overflow-tooltip
      />
      <el-table-column
        prop="clientId"
        label="实例"
        width="160"
        show-overflow-tooltip
      />
      <el-table-column
        label="耗时"
        width="100"
        align="right"
      >
        <template #default="{ row }">
          {{ (row.cost ?? 0) }}ms
        </template>
      </el-table-column>
      <el-table-column
        label="开始时间"
        width="180"
      >
        <template #default="{ row }">
          {{ fmtTime(row.startTime) }}
        </template>
      </el-table-column>
      <el-table-column
        label="结束时间"
        width="180"
      >
        <template #default="{ row }">
          {{ fmtTime(row.endTime) }}
        </template>
      </el-table-column>
      <el-table-column
        label="状态"
        width="90"
        align="center"
      >
        <template #default="{ row }">
          <el-tag
            size="small"
            :type="row.success !== false ? 'success' : 'danger'"
          >
            {{ row.success !== false ? '正常' : '异常' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column
        prop="tag"
        label="Tag"
        min-width="180"
        show-overflow-tooltip
      />
    </el-table>
  </div>
</template>

<style lang="scss" scoped>
.trace-detail-page {
  width: 100%;

  .td-table {
    width: 100%;
  }
}
</style>
