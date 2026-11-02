<script lang="ts">
/**
 * 全局搜索开关（模块级共享，供 TopBar 搜索框点击触发）。
 * 普通 script 块（非 setup）以支持 export；setup 内通过同名绑定使用。
 */
import { reactive } from 'vue';

export const globalSearchState = reactive<{ open: boolean }>({ open: false });
</script>

<script setup lang="ts">
/**
 * 星尘控制台 · 全局搜索（GlobalSearch）
 *
 * 依据《Stardust_交互设计重构方案.md》§18：搜索是第二导航，不是普通输入框。
 *  - 快捷键 Ctrl/Cmd+K 打开，Esc 关闭，↑↓ 选择，Enter 跳转；
 *  - 覆盖 应用 / 节点 / 服务 / 配置 四类资源，结果分组展示；
 *  - 输入防抖 300ms，并行请求；带当前 Project Context 注入。
 *
 * 样式纪律：颜色一律 --el-* 令牌，无硬编码色值。
 */
import { ref, watch, onMounted, onBeforeUnmount, nextTick } from 'vue';
import { useRouter } from 'vue-router';
import { request } from '@newlifex/cube-vue/core/utils/request';
import { stardustContext } from '@/stores/stardustContext';

type ResultKind = '应用' | '节点' | '服务' | '配置' | '发布';

interface SearchGroup {
  kind: ResultKind;
  icon: string;
  items: { key: string; title: string; desc: string; to: string }[];
}

const open = ref(false);
const keyword = ref('');
const groups = ref<SearchGroup[]>([]);
const loading = ref(false);
const searched = ref(false);
const activeIndex = ref(-1);
let timer = 0;

const router = useRouter();

watch(
  () => globalSearchState.open,
  (v) => {
    if (v) openSearch();
    else if (!v && open.value) close();
  },
);

/** 扁平化结果（用于 ↑↓ 高亮与 Enter 跳转） */
const flatItems = ref<{ group: SearchGroup; item: SearchGroup['items'][number] }[]>([]);

function rebuildFlat() {
  const flat: { group: SearchGroup; item: SearchGroup['items'][number] }[] = [];
  for (const g of groups.value) for (const it of g.items) flat.push({ group: g, item: it });
  flatItems.value = flat;
  activeIndex.value = flat.length > 0 ? 0 : -1;
}

function reset() {
  keyword.value = '';
  groups.value = [];
  searched.value = false;
  loading.value = false;
  activeIndex.value = -1;
  flatItems.value = [];
}

async function runSearch() {
  const k = keyword.value.trim();
  if (!k) {
    groups.value = [];
    searched.value = false;
    rebuildFlat();
    return;
  }
  loading.value = true;
  searched.value = true;
  try {
    // 上下文参数（projectId/environment）已由全局请求拦截器自动注入，此处不再手动拼接
    const safeGet = (url: string, params: Record<string, unknown>) =>
      request({ url, method: 'GET', params }).catch(() => ({ data: [] }) as { data: Record<string, unknown>[] });
    const [ar, nr, sr, cr, pr] = await Promise.all([
      safeGet('/Registry/App', { key: k, PageSize: 20 }),
      safeGet('/Nodes/Node', { key: k, PageSize: 20 }),
      safeGet('/Registry/AppService', { key: k, PageSize: 20 }),
      safeGet('/Configs/AppConfig', { key: k, PageSize: 20 }),
      safeGet('/Deployment/AppPipelineRun', { Q: k, PageSize: 20 }),
    ]);
    const apps = (ar?.data ?? []) as Record<string, unknown>[];
    const nodes = (nr?.data ?? []) as Record<string, unknown>[];
    const svcs = (sr?.data ?? []) as Record<string, unknown>[];
    const cfgs = (cr?.data ?? []) as Record<string, unknown>[];
    const runs = (pr?.data ?? []) as Record<string, unknown>[];

    const groupsBuilt = ([
      {
        kind: '应用',
        icon: 'M12 3l8 4.5v9L12 21l-8-4.5v-9z M12 12l8-4.5M12 12v9M12 12L4 7.5',
        items: apps.map((a) => ({
          key: String(a.id ?? a.name ?? ''),
          title: String(a.displayName || a.name || '-'),
          desc: [String(a.name ?? ''), a.category ? `分类：${a.category}` : ''].filter(Boolean).join(' · ') || '应用',
          to: a.id != null ? `/Registry/App/${a.id}` : '/stardust/apps',
        })),
      },
      {
        kind: '节点',
        icon: 'M3 4h18v7H3z M3 14h18v6H3z M7 7.5h.01M7 17h.01',
        items: nodes.map((n) => ({
          key: String(n.id ?? n.name ?? ''),
          title: String(n.name || n.code || '-'),
          desc: String(n.iP || n.ip || '') || '节点',
          to: n.id != null ? `/Nodes/Node/${n.id}` : '/stardust/nodes',
        })),
      },
      {
        kind: '服务',
        icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10',
        items: svcs.map((s) => ({
          key: String(s.id ?? s.serviceName ?? ''),
          title: String(s.serviceName || '-'),
          desc: [s.appName ? `提供方：${s.appName}` : '', s.healthy === false ? '异常' : '正常'].filter(Boolean).join(' · ') || '服务',
          to: '/Registry/AppService',
        })),
      },
      {
        kind: '配置',
        icon: 'M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z',
        items: cfgs.map((c) => ({
          key: String(c.id ?? c.name ?? ''),
          title: String(c.name || '-'),
          desc: [c.displayName ? String(c.displayName) : '', c.appName ? `应用：${c.appName}` : ''].filter(Boolean).join(' · ') || '配置',
          to: '/Configs/AppConfig',
        })),
      },
      {
        kind: '发布',
        icon: 'M15.59 14.37a6 6 0 01-5.84 7.18 6 6 0 01-5.84-7.18 6 6 0 0112 0zm0 0H3.75m0 0l-.08-.83a6 6 0 0111.89-3.18M3.75 14.37l-.08.83M20 2v6m0 0l3-3m-3 3l-3-3',
        items: runs.map((r) => ({
          key: String(r.Id ?? r.id ?? ''),
          title: String(r.Name || r.AppName || r.name || `发布 #${r.Id ?? ''}`),
          desc: [
            r.BuildStartedTime ? new Date(String(r.BuildStartedTime)).toLocaleString('zh-CN') : '',
            r.Status != null ? `状态：${r.Status}` : '',
          ].filter(Boolean).join(' · ') || '流水线运行',
          to: r.Id != null ? `/Deployment/AppPipelineRun/${r.Id}` : '/Deployment/AppPipelineRun',
        })),
      },
    ] as SearchGroup[]).filter((g) => g.items.length > 0);
    groups.value = groupsBuilt;
    rebuildFlat();
  } catch (e) {
    console.warn('全局搜索失败', e);
    groups.value = [];
    rebuildFlat();
  } finally {
    loading.value = false;
  }
}

function openSearch() {
  open.value = true;
  globalSearchState.open = true;
  searched.value = false;
  void nextTick(() => focusInput());
}

function close() {
  open.value = false;
  globalSearchState.open = false;
  reset();
}

function focusInput() {
  document.getElementById('stardust-global-search-input')?.focus();
}

function onKeydown(e: KeyboardEvent) {
  if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
    e.preventDefault();
    if (open.value) close();
    else openSearch();
    return;
  }
  if (!open.value) return;
  if (e.key === 'Escape') {
    e.preventDefault();
    close();
  } else if (e.key === 'ArrowDown') {
    e.preventDefault();
    if (flatItems.value.length) activeIndex.value = (activeIndex.value + 1) % flatItems.value.length;
  } else if (e.key === 'ArrowUp') {
    e.preventDefault();
    if (flatItems.value.length) activeIndex.value = (activeIndex.value - 1 + flatItems.value.length) % flatItems.value.length;
  } else if (e.key === 'Enter') {
    e.preventDefault();
    const hit = flatItems.value[activeIndex.value];
    if (hit) go(hit.item.to);
  }
}

function go(to: string) {
  close();
  void router.push(to);
}

/** 组内偏移 → 全局扁平索引（供 ↑↓ 高亮联动） */
function flatIndexOf(g: SearchGroup, i: number): number {
  let idx = 0;
  for (const grp of groups.value) {
    if (grp === g) return idx + i;
    idx += grp.items.length;
  }
  return -1;
}

watch(keyword, () => {
  window.clearTimeout(timer);
  timer = window.setTimeout(() => void runSearch(), 300);
});

onMounted(() => window.addEventListener('keydown', onKeydown));
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
  window.clearTimeout(timer);
});
</script>

<template>
  <Teleport to="body">
    <Transition name="sd-fade">
      <div
        v-if="open"
        class="fixed inset-0 z-[3000] flex items-start justify-center bg-black/45 pt-[12vh] backdrop-blur-[2px]"
        @click.self="close"
      >
        <div
          class="sd-panel w-[640px] max-w-[92vw] overflow-hidden rounded-2xl border"
          style="
            border-color: var(--el-border-color);
            background: color-mix(in srgb, var(--el-bg-color-overlay) 94%, transparent);
            box-shadow: 0 24px 64px color-mix(in srgb, #000 45%, transparent);
          "
        >
          <!-- 输入行 -->
          <div class="flex items-center gap-3 border-b border-[var(--el-border-color-light)] px-4 py-3.5">
            <svg
              class="h-[18px] w-[18px] shrink-0 text-[var(--el-text-color-secondary)]"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="1.8"
            >
              <circle
                cx="11"
                cy="11"
                r="7"
              />
              <path d="M21 21l-4-4" />
            </svg>
            <input
              id="stardust-global-search-input"
              v-model="keyword"
              type="text"
              placeholder="搜索应用、节点、服务、配置、发布……"
              class="min-w-0 flex-1 bg-transparent text-sm text-[var(--el-text-color-primary)] outline-none placeholder:text-[var(--el-text-color-placeholder)]"
            >
            <span class="shrink-0 rounded border border-[var(--el-border-color)] px-1.5 py-0.5 text-[10px] text-[var(--el-text-color-secondary)]">
              ESC
            </span>
          </div>

          <!-- 结果区 -->
          <div class="max-h-[46vh] overflow-y-auto p-2">
            <div
              v-if="loading"
              class="flex flex-col gap-2 p-3"
            >
              <div
                v-for="i in 3"
                :key="i"
                class="h-9 animate-pulse rounded-lg bg-[var(--el-fill-color)]"
              />
            </div>
            <div
              v-else-if="searched && groups.length === 0"
              class="flex flex-col items-center gap-2 py-10 text-center"
            >
              <span class="text-sm text-[var(--el-text-color-regular)]">未找到与「{{ keyword }}」相关的资源</span>
              <span class="text-xs text-[var(--el-text-color-placeholder)]">可尝试更换关键字，或切换项目后重试</span>
            </div>
            <div
              v-else-if="!searched && !keyword"
              class="flex flex-col gap-3 py-4"
            >
              <div class="px-2 text-xs text-[var(--el-text-color-placeholder)]">
                当前项目：{{ stardustContext.projectName || '未选择' }}
                <template v-if="stardustContext.environment">
                  · 环境：{{ stardustContext.environmentName }}
                </template>
              </div>
              <div class="flex flex-wrap gap-2 px-1">
                <span
                  v-for="g in ['应用', '节点', '服务', '配置', '发布']"
                  :key="g"
                  class="rounded-full border border-[var(--el-border-color)] px-2.5 py-1 text-xs text-[var(--el-text-color-secondary)]"
                >{{ g }}</span>
              </div>
            </div>
            <div
              v-else
              class="flex flex-col gap-3"
            >
              <section
                v-for="g in groups"
                :key="g.kind"
              >
                <h3 class="flex items-center gap-1.5 px-2 pb-1.5 pt-2 text-xs font-semibold text-[var(--el-text-color-secondary)]">
                  <svg
                    class="h-3.5 w-3.5"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    stroke-width="1.7"
                    stroke-linecap="round"
                    stroke-linejoin="round"
                  >
                    <path :d="g.icon" />
                  </svg>
                  {{ g.kind }}
                  <span class="font-normal text-[var(--el-text-color-placeholder)]">{{ g.items.length }}</span>
                </h3>
                <div class="flex flex-col gap-0.5">
                  <button
                    v-for="(it, i) in g.items"
                    :key="`${g.kind}-${it.key}`"
                    type="button"
                    class="flex items-center justify-between gap-3 rounded-lg px-2.5 py-2 text-left transition-colors"
                    :class="activeIndex === flatIndexOf(g, i)
                      ? 'bg-[color-mix(in_srgb,var(--el-color-primary)_18%,transparent)]'
                      : 'hover:bg-[var(--el-fill-color)]'"
                    @mouseenter="activeIndex = flatIndexOf(g, i)"
                    @click="go(it.to)"
                  >
                    <span class="min-w-0 flex-1">
                      <span class="block truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ it.title }}</span>
                      <span class="block truncate text-xs text-[var(--el-text-color-placeholder)]">{{ it.desc }}</span>
                    </span>
                    <svg
                      class="h-3.5 w-3.5 shrink-0 text-[var(--el-text-color-secondary)]"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2"
                    >
                      <path d="M9 6l6 6-6 6" />
                    </svg>
                  </button>
                </div>
              </section>
              <p class="px-2 pb-2 pt-1 text-[10px] text-[var(--el-text-color-placeholder)]">
                更多类型（Trace / 日志 / 告警）待接入
              </p>
            </div>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.sd-fade-enter-active,
.sd-fade-leave-active {
  transition: opacity 0.18s ease;
}
.sd-fade-enter-from,
.sd-fade-leave-to {
  opacity: 0;
}
</style>
