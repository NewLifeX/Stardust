<script setup lang="ts">
/**
 * Nodes/Node/Detail — 节点详情（自定义迁移页）
 *
 * 路由：#/Nodes/Node/Detail/{id}
 *  - usePageApi('Nodes','Node').getDetail(id) 读取单条节点记录
 *  - el-descriptions 展示节点字段
 *  - 额外列出该节点部署的应用（AppDeployNode?nodeId），实现「节点列出部署的应用」
 */
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi';
import request from '@newlifex/cube-vue/core/utils/request';

const route = useRoute();
const api = usePageApi('Nodes', 'Node');

const nodeId = computed<number>(() => Number(route.params.id) || 0);

interface NodeDetail {
  Id?: number;
  Name?: string;
  ProjectId?: number;
  Category?: string;
  Product?: string;
  Version?: string;
  IP?: string;
  Enable?: boolean;
  [k: string]: unknown;
}

interface DeployApp {
  Id?: number;
  AppId?: number;
  AppName?: string;
  NodeId?: number;
  Enable?: boolean;
  [k: string]: unknown;
}

const loading = ref(false);
const detail = ref<NodeDetail | null>(null);
const notFound = ref(false);

const appLoading = ref(false);
const deployApps = ref<DeployApp[]>([]);

function formatVal(v: unknown): string {
  if (v == null || v === '') return '-';
  if (typeof v === 'boolean') return v ? '启用' : '禁用';
  return String(v);
}

async function fetchDetail() {
  const id = nodeId.value;
  if (id <= 0) {
    notFound.value = true;
    return;
  }
  loading.value = true;
  try {
    const res: any = await api.getDetail<NodeDetail>(id);
    detail.value = res?.data ?? res ?? null;
    notFound.value = !detail.value;
  } catch (e) {
    console.error('[Node/Detail] getDetail failed', e);
    detail.value = null;
    notFound.value = true;
  } finally {
    loading.value = false;
  }
}

async function fetchDeployApps() {
  const id = nodeId.value;
  if (id <= 0) return;
  appLoading.value = true;
  try {
    const res: any = await request({
      url: '/Deployment/AppDeployNode',
      params: { nodeId: id, pageSize: 200 },
    });
    const list: DeployApp[] = res?.data ?? [];
    deployApps.value = Array.isArray(list) ? list : [];
  } catch (e) {
    console.error('[Node/Detail] fetch deploy apps failed', e);
    deployApps.value = [];
  } finally {
    appLoading.value = false;
  }
}

onMounted(async () => {
  await fetchDetail();
  await fetchDeployApps();
});

watch(
  () => nodeId.value,
  async () => {
    await fetchDetail();
    await fetchDeployApps();
  },
);
</script>

<template>
  <div class="node-detail-page">
    <el-card
      v-loading="loading"
      shadow="never"
    >
      <template v-if="notFound">
        <el-empty description="节点不存在" />
      </template>
      <template v-else-if="detail">
        <el-descriptions
          title="节点详情"
          :column="2"
          border
        >
          <el-descriptions-item label="编号">
            {{ formatVal(detail.Id) }}
          </el-descriptions-item>
          <el-descriptions-item label="名称">
            {{ formatVal(detail.Name) }}
          </el-descriptions-item>
          <el-descriptions-item label="分类">
            {{ formatVal(detail.Category) }}
          </el-descriptions-item>
          <el-descriptions-item label="产品">
            {{ formatVal(detail.Product) }}
          </el-descriptions-item>
          <el-descriptions-item label="版本">
            {{ formatVal(detail.Version) }}
          </el-descriptions-item>
          <el-descriptions-item label="IP">
            {{ formatVal(detail.IP) }}
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

    <el-card
      shadow="never"
      style="margin-top: 16px"
    >
      <template #header>
        <span>部署应用（节点关联）</span>
      </template>
      <el-table
        v-loading="appLoading"
        :data="deployApps"
        stripe
        border
        empty-text="该节点暂无部署应用"
      >
        <el-table-column
          prop="AppName"
          label="应用名称"
          min-width="180"
          show-overflow-tooltip
        />
        <el-table-column
          prop="AppId"
          label="应用ID"
          width="120"
        />
        <el-table-column
          label="状态"
          width="100"
          align="center"
        >
          <template #default="{ row }">
            <el-tag
              size="small"
              :type="row.Enable !== false ? 'success' : 'info'"
            >
              {{ row.Enable !== false ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column
          label="操作"
          width="120"
          align="center"
        >
          <template #default="{ row }">
            <el-button
              v-if="row.AppId"
              type="primary"
              link
              @click="$router.push(`/Registry/App/Detail/${row.AppId}`)"
            >
              查看应用
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<style lang="scss" scoped>
.node-detail-page {
  width: 100%;
}
</style>
