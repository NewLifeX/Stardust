<script setup lang="ts">
/**
 * TraceTree.vue — 调用链树通用组件（props: traceId）
 *
 * 复刻 MVC TraceController.Search 的深度搜索算法（纯前端 JS）：
 *  - 拉 GET /Monitors/SampleData?traceId=&pageSize=10000 拿到 SampleData 列表
 *  - 按 ParentId/SpanId 构建有序调用链树：
 *      顶级 parent（其 span 不存在）→ 子级按 StartTime 降序入栈 → 深度优先弹出
 *  - 渲染为可折叠 el-tree（默认展开全部），节点显示：操作名 / 耗时 / 开始时间 / 应用@节点
 *  - 空数据 → el-empty
 *
 * 数据字段（见 Stardust.Data/Monitors/采样数据.cs）：
 *  Id / DataId / ItemId / Success / StartTime / EndTime / Cost / Value /
 *  ClientId / TraceId / SpanId / ParentId / Tag / Error / CreateIP / CreateTime
 *  以及 TraceItem（嵌套，可能为空）：Kind / Name / DisplayName
 */
import { ref, onMounted, watch, computed } from 'vue';
import request from '@newlifex/cube-vue/core/utils/request';

const props = defineProps<{ traceId?: string }>();

interface SampleRow {
  id?: number | string;
  dataId?: number | string;
  itemId?: number | string;
  success?: boolean;
  startTime?: number;
  endTime?: number;
  cost?: number;
  spanId?: string | null;
  parentId?: string | null;
  appName?: string | null;
  appId?: number | string;
  name?: string | null;
  tag?: string | null;
  error?: string | null;
  traceItem?: { kind?: string; name?: string; displayName?: string } | null;
  [k: string]: unknown;
}

interface TreeRow {
  key: string;
  id: number | string;
  label: string;
  name: string;
  cost: number;
  startTime: number;
  success: boolean;
  appName: string;
  nodeName: string;
  hasError: boolean;
  raw: SampleRow;
  children: TreeRow[];
}

const loading = ref(false);
const error = ref('');
const rows = ref<SampleRow[]>([]);
const treeData = ref<TreeRow[]>([]);
const defaultExpandedKeys = ref<string[]>([]);

function fmtTime(ms?: number): string {
  if (!ms || ms <= 0) return '-';
  const d = new Date(ms);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

/**
 * 复刻 MVC Search 深度搜索算法（纯 JS）：
 *  1. 反向排序（StartTime 降序、SpanId 降序）
 *  2. 找顶级 parent（其对应 span 不存在的 ParentId），其子级按时间降序入栈
 *  3. 依次弹出 parent，深度搜索其子级（按时间降序入栈），形成有序结果 rs
 *  4. 残留异常数据追加到末尾
 * 返回有序的扁平列表（保留了父先于子的访问顺序）。
 */
function depthSearchOrder(list: SampleRow[]): SampleRow[] {
  if (list.length === 0) return [];
  const work = [...list];
  // 反向顺序（startTime 降序，spanId 降序）
  work.sort((a, b) => {
    const sa = a.startTime ?? 0;
    const sb = b.startTime ?? 0;
    if (sb !== sa) return sb - sa;
    return String(b.spanId ?? '').localeCompare(String(a.spanId ?? ''));
  });

  const spanSet = new Set(work.map((e) => String(e.spanId ?? '')));
  const stack: SampleRow[] = [];

  // 所有 parentId（含空），首次出现的 parentId 优先
  const pids = work.map((e) => String(e.parentId ?? '')).filter((v, i, arr) => arr.indexOf(v) === i);
  // 顶级 parentId：其对应 span 不存在（含空字符串）
  for (const pid of pids) {
    if (!spanSet.has(pid)) {
      for (const elm of work.filter((e) => String(e.parentId ?? '') === pid)) {
        stack.push(elm);
      }
    }
  }
  // 从 work 移除已进入栈的
  const inStack = new Set(stack);
  for (let i = work.length - 1; i >= 0; i--) {
    if (inStack.has(work[i])) work.splice(i, 1);
  }

  const rs: SampleRow[] = [];
  if (stack.length === 0) return list; // 理论上至少有顶级

  while (stack.length > 0) {
    const parent = stack.pop()!;
    rs.push(parent);
    const pid = String(parent.spanId ?? '');
    // 当前 span 的下级，按时间降序入栈
    const children = work.filter((e) => String(e.parentId ?? '') === pid);
    for (const c of children) {
      stack.push(c);
      const idx = work.indexOf(c);
      if (idx >= 0) work.splice(idx, 1);
    }
  }
  // 残留异常数据
  rs.push(...work);
  return rs;
}

/** 将有序扁平列表（父先于子）转换为树 */
function buildTree(list: SampleRow[]): TreeRow[] {
  const ordered = depthSearchOrder(list);
  const map = new Map<string, TreeRow>();
  const roots: TreeRow[] = [];

  const makeRow = (item: SampleRow): TreeRow => {
    const nodeName = item.traceItem?.displayName || item.name || item.appName || String(item.id);
    const label = `${item.name || item.appName || '未知操作'} · ${item.cost ?? 0}ms · ${fmtTime(item.startTime)}`;
    return {
      key: String(item.id),
      id: item.id as number | string,
      label,
      name: item.name || '',
      cost: item.cost ?? 0,
      startTime: item.startTime ?? 0,
      success: item.success !== false,
      appName: item.appName || '',
      nodeName: nodeName || '',
      hasError: !!(item.error && item.error.length > 0),
      raw: item,
      children: [],
    };
  };

  for (const item of ordered) {
    const row = makeRow(item);
    map.set(String(item.id), row);
  }

  for (const item of ordered) {
    const row = map.get(String(item.id))!;
    const parentId = String(item.parentId ?? '');
    // 找父级：父级可能是某个 row 的 spanId
    let parentRow: TreeRow | undefined;
    if (parentId) {
      for (const r of map.values()) {
        if (String(r.raw.spanId ?? '') === parentId) {
          parentRow = r;
          break;
        }
      }
    }
    if (parentRow) parentRow.children.push(row);
    else roots.push(row);
  }

  // 收集展开 keys（默认全展开）
  const keys: string[] = [];
  const walk = (arr: TreeRow[]) => {
    for (const r of arr) {
      keys.push(r.key);
      if (r.children.length) walk(r.children);
    }
  };
  walk(roots);
  defaultExpandedKeys.value = keys;
  return roots;
}

async function fetchData() {
  const traceId = props.traceId?.trim();
  if (!traceId) {
    rows.value = [];
    treeData.value = [];
    return;
  }
  loading.value = true;
  error.value = '';
  try {
    const res: any = await request({
      url: '/Monitors/SampleData',
      params: { traceId, pageSize: 10000 },
    });
    const list: SampleRow[] = res?.data ?? res ?? [];
    rows.value = Array.isArray(list) ? list : [];
    treeData.value = buildTree(rows.value);
  } catch (e: any) {
    console.error('[TraceTree] fetch failed', e);
    error.value = e?.message || '加载失败';
    rows.value = [];
    treeData.value = [];
  } finally {
    loading.value = false;
  }
}

const isEmpty = computed(() => !loading.value && treeData.value.length === 0);

onMounted(fetchData);
watch(
  () => props.traceId,
  () => fetchData(),
);
</script>

<template>
  <div class="trace-tree-wrap">
    <el-empty
      v-if="isEmpty"
      description="暂无调用链数据（TraceId 无采样）"
    />

    <el-alert
      v-else-if="error"
      type="error"
      :title="error"
      :closable="false"
      show-icon
    />

    <div
      v-else
      v-loading="loading"
      class="tt-body"
    >
      <el-tree
        v-if="treeData.length"
        :data="treeData"
        :default-expanded-keys="defaultExpandedKeys"
        node-key="key"
        :expand-on-click-node="false"
        class="tt-tree"
      >
        <template #default="{ data }">
          <span class="tt-node">
            <el-icon
              v-if="data.hasError"
              class="tt-err-icon"
              color="var(--el-color-danger)"
            >
              <WarningFilled />
            </el-icon>
            <span class="tt-name">{{ data.nodeName || data.name || '未知操作' }}</span>
            <el-tag
              size="small"
              :type="data.success ? 'success' : 'danger'"
              class="tt-cost"
            >
              {{ data.cost }}ms
            </el-tag>
            <span class="tt-meta">{{ fmtTime(data.startTime) }}</span>
            <span
              v-if="data.appName"
              class="tt-app"
            >{{ data.appName }}</span>
          </span>
        </template>
      </el-tree>
    </div>
  </div>
</template>

<style lang="scss" scoped>
.trace-tree-wrap {
  width: 100%;

  .tt-body {
    min-height: 120px;
    padding: 8px 0;
  }

  .tt-tree {
    width: 100%;
    background: transparent;
  }

  .tt-node {
    display: inline-flex;
    align-items: center;
    gap: 8px;

    .tt-err-icon {
      font-size: 14px;
    }

    .tt-name {
      font-size: 13px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    .tt-cost {
      font-variant-numeric: tabular-nums;
    }

    .tt-meta {
      font-size: 12px;
      color: var(--el-text-color-secondary);
      font-variant-numeric: tabular-nums;
    }

    .tt-app {
      font-size: 12px;
      color: var(--el-text-color-secondary);
      &::before {
        content: '· ';
      }
    }
  }
}
</style>
