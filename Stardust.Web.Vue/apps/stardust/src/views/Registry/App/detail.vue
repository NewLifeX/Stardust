<script setup lang="ts">
/**
 * Registry/App/Detail — 应用详情（自定义迁移页）
 *
 * 路由：#/Registry/App/Detail/{id}
 *  - usePageApi('Registry','App').getDetail(id) 读取单条应用记录
 *  - el-descriptions 展示应用字段
 */
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';

const route = useRoute();
const api = usePageApi('Registry', 'App');

const appId = computed<number>(() => Number(route.params.id) || 0);

interface AppDetail {
  Id?: number;
  Name?: string;
  DisplayName?: string;
  ProjectId?: number;
  Category?: string;
  Enable?: boolean;
  [k: string]: unknown;
}

const loading = ref(false);
const detail = ref<AppDetail | null>(null);
const notFound = ref(false);

function formatVal(v: unknown): string {
  if (v == null || v === '') return '-';
  if (typeof v === 'boolean') return v ? '启用' : '禁用';
  return String(v);
}

async function fetchDetail() {
  const id = appId.value;
  if (id <= 0) {
    notFound.value = true;
    return;
  }
  loading.value = true;
  try {
    const res: any = await api.getDetail<AppDetail>(id);
    detail.value = res?.data ?? res ?? null;
    notFound.value = !detail.value;
  } catch (e) {
    console.error('[App/Detail] getDetail failed', e);
    detail.value = null;
    notFound.value = true;
  } finally {
    loading.value = false;
  }
}

onMounted(fetchDetail);
watch(
  () => appId.value,
  () => fetchDetail(),
);
</script>

<template>
  <div class="app-detail-page">
    <el-card
      v-loading="loading"
      shadow="never"
    >
      <template v-if="notFound">
        <el-empty description="应用不存在" />
      </template>
      <template v-else-if="detail">
        <el-descriptions
          title="应用详情"
          :column="2"
          border
        >
          <el-descriptions-item label="编号">
            {{ formatVal(detail.Id) }}
          </el-descriptions-item>
          <el-descriptions-item label="名称">
            {{ formatVal(detail.Name) }}
          </el-descriptions-item>
          <el-descriptions-item label="显示名">
            {{ formatVal(detail.DisplayName) }}
          </el-descriptions-item>
          <el-descriptions-item label="类别">
            {{ formatVal(detail.Category) }}
          </el-descriptions-item>
          <el-descriptions-item label="状态">
            {{ formatVal(detail.Enable) }}
          </el-descriptions-item>
          <el-descriptions-item label="项目ID">
            {{ formatVal(detail.ProjectId) }}
          </el-descriptions-item>
        </el-descriptions>
      </template>
    </el-card>
  </div>
</template>

<style lang="scss" scoped>
.app-detail-page {
  width: 100%;
}
</style>
