<template>
  <div class="demo-board">
    <header class="board-head">
      <div>
        <h2 class="board-title">
          星图数据看板
        </h2>
        <p class="board-sub">
          Demo 区域 · 实时概览
        </p>
      </div>
      <el-button
        type="primary"
        :icon="Refresh"
        :loading="loading"
        @click="reload"
      >
        刷新
      </el-button>
    </header>

    <section class="stat-grid">
      <div
        v-for="s in stats"
        :key="s.key"
        class="stat-card glass"
      >
        <span
          class="stat-ic"
          :style="{ color: s.color }"
        >{{ s.icon }}</span>
        <div class="stat-body">
          <div class="stat-num">
            {{ s.value }}
          </div>
          <div class="stat-label">
            {{ s.label }}
          </div>
        </div>
      </div>
    </section>

    <section class="board-table glass">
      <div class="table-head">
        <span>最近记录</span>
        <el-tag
          size="small"
          effect="plain"
        >
          {{ list.length }} 条
        </el-tag>
      </div>
      <el-table
        v-loading="loading"
        :data="list"
        stripe
        height="320"
      >
        <el-table-column
          prop="id"
          label="ID"
          width="80"
        />
        <el-table-column
          prop="name"
          label="名称"
          min-width="140"
        />
        <el-table-column
          prop="code"
          label="编码"
          min-width="120"
        />
        <el-table-column
          prop="enable"
          label="启用"
          width="90"
        >
          <template #default="{ row }">
            <el-tag
              :type="row.enable ? 'success' : 'info'"
              size="small"
            >
              {{ row.enable ? '是' : '否' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column
          label="操作"
          width="140"
        >
          <template #default="{ row }">
            <el-button
              link
              type="primary"
              @click="openDetail(row)"
            >
              详情
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { usePageApi } from '@newlifex/cube-vue/core/composables/useCubeApi'

interface Row {
  id: number
  name: string
  code: string
  enable: boolean
}

const api = usePageApi('Demo', 'Demo')
const loading = ref(false)
const list = ref<Row[]>([])

const stats = computed(() => [
  { key: 'total', icon: '🌟', color: 'var(--cosmic-star)', label: '总记录', value: list.value.length },
  { key: 'on', icon: '✅', color: 'var(--cosmic-star-ok)', label: '已启用', value: list.value.filter(r => r.enable).length },
  { key: 'off', icon: '⭕', color: 'var(--cosmic-star-idle)', label: '未启用', value: list.value.filter(r => !r.enable).length },
  { key: 'area', icon: '🌌', color: 'var(--cosmic-star-warn)', label: '区域', value: 'Demo' },
])

async function reload() {
  loading.value = true
  try {
    const res = await api.getList({ pageIndex: 0, pageSize: 20 })
    list.value = (res.data || []) as Row[]
  } finally {
    loading.value = false
  }
}

function openDetail(row: Row) {
  window.open(`/Demo/Demo/Detail?id=${row.id}`, '_blank')
}

onMounted(reload)
</script>

<style scoped>
.demo-board { display: flex; flex-direction: column; gap: 16px; }

.board-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.board-title {
  margin: 0;
  font-size: 18px;
  color: var(--cosmic-text);
}
.board-sub {
  margin: 4px 0 0;
  font-size: 12px;
  color: var(--cosmic-text-faint);
}

.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
}
.stat-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  border-radius: var(--cosmic-radius);
}
.stat-ic { font-size: 26px; }
.stat-num { font-size: 22px; font-weight: 600; color: var(--cosmic-text); }
.stat-label { font-size: 12px; color: var(--cosmic-text-faint); }

.board-table {
  padding: 14px;
  border-radius: var(--cosmic-radius);
}
.table-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
  font-size: 14px;
  color: var(--cosmic-text-dim);
}
</style>
