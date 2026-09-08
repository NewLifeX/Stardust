<script setup lang="ts">
// 星尘控制台 应用管理：抽屉六页签（概览/实例/配置/发布/监控/服务）；fetch 编排与聚合在 appsModel，脚本仅 UI 状态；样式仅 --el-* 令牌
import { ref, computed, onMounted, onBeforeUnmount } from 'vue';
import { ElMessage, ElMessageBox } from 'element-plus';
import { request } from '@newlifex/cube-vue/core/utils/request';
import GlassCard from '@/layouts/StardustLayout/components/GlassCard.vue';
import StatusPill from '@/layouts/StardustLayout/components/StatusPill.vue';
import KpiStat from '@/layouts/StardustLayout/components/KpiStat.vue';
import * as m from './appsModel';
const list = ref<m.AppAggRow[]>([]), filter = ref<'all' | m.AppStatus>('all'), keyword = ref('');
const current = ref<m.AppAggRow | null>(null), drawerVisible = ref(false), tab = ref('overview'), configMode = ref<'table' | 'text'>('table');
const onlineRows = ref<m.OnlineRow[]>([]), detail = ref<m.AppDetail & { history: m.HistoryRow[] }>({ config: [], runs: [], stats: [], services: [], history: [] });
let timer = 0;
const stats = computed(() => m.aggregateKpis(list.value)), filtered = computed(() => m.filterByKeyword(m.filterByStatus(list.value, filter.value), keyword.value));
const instances = computed(() => m.instancesRows(onlineRows.value, current.value?.name ?? '')), services = computed(() => m.servicesGroups(detail.value.services));
const configKvs = computed(() => m.configKv(detail.value.config)), configTxt = computed(() => m.configText(detail.value.config));
const histories = computed(() => m.historyRows(detail.value.history));
const steps = computed(() => m.pipelineSteps(detail.value.runs[0] ?? {})), monitor = computed(() => m.monitorSummary(detail.value.stats));
async function showDetail(row: m.AppAggRow) {
  current.value = row; drawerVisible.value = true; tab.value = 'overview';
  try {
    const d = await m.fetchAppDetail(request, row.name, row.id);
    detail.value = { ...d, history: await m.fetchConfigHistory(request, d.config[0]?.id) };
  } catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '加载详情失败'); }
}
async function doDiff(h: m.HistoryRow) {
  if (!h.id) return;
  try { const k = (await m.configDiff(request, h.id)).join('、'); await ElMessageBox.alert(k || '无差异', '配置差异', { confirmButtonText: '知道了' }); }
  catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '对比失败'); }
}
async function doRollback(h: m.HistoryRow) {
  if (!h.id) return;
  try { await ElMessageBox.confirm(`确认回滚到「${h.action || '发布'} ${h.time || '-'}」版本？`, '确认回滚', { type: 'warning' }); } catch { return; }
  try { ElMessage.success((await m.configRollback(request, h.id)) || '已回滚到此版本'); if (current.value) showDetail(current.value); }
  catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '回滚失败'); }
}
async function load() {
  try {
    const r = await m.fetchAppList(request);
    onlineRows.value = r.online; list.value = r.rows;
  } catch (e: unknown) { ElMessage.error(e instanceof Error ? e.message : '加载应用失败'); }
}
onMounted(() => {
  load();
  timer = window.setInterval(() => {
    if (drawerVisible.value && current.value) m.fetchMinuteStats(request, current.value.name, current.value.id).then((s) => (detail.value = { ...detail.value, stats: s })).catch(() => undefined);
  }, 15000);
});
onBeforeUnmount(() => window.clearInterval(timer));
</script>

<template>
  <div class="flex h-full flex-col gap-5">
    <!-- 页头 -->
    <div class="flex items-center justify-between">
      <div class="flex flex-col gap-1">
        <h1 class="text-xl font-semibold text-[var(--el-text-color-primary)]">
          应用管理
        </h1>
        <p class="text-xs text-[var(--el-text-color-regular)]">
          应用是配置 / 监控 / 发布 / 服务 的归属主体 — 点击应用查看其收敛的全部能力
        </p>
      </div>
      <el-button
        type="primary"
        size="small"
      >
        新建应用
      </el-button>
    </div>

    <!-- KPI（盒装卡片，对照效果图） -->
    <div class="grid grid-cols-4 gap-4">
      <KpiStat
        label="应用总数"
        :value="stats.total"
        accent="primary"
      />
      <KpiStat
        label="在线"
        :value="stats.online"
        accent="success"
      />
      <KpiStat
        label="告警"
        :value="stats.warn"
        accent="warning"
      />
      <KpiStat
        label="离线"
        :value="stats.offline"
        accent="danger"
      />
    </div>

    <!-- 过滤 + 搜索 -->
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-2">
        <button
          v-for="c in m.statusChips"
          :key="c.key"
          class="rounded-[18px] px-3.5 py-[7px] text-[13px] transition"
          data-spec="chip"
          :class="filter === c.key ? 'bg-[var(--el-color-primary)] text-white' : 'text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]'"
          @click="filter = c.key"
        >
          {{ c.label }} {{ m.chipCount(stats, c.key) }}
        </button>
      </div>
      <el-input
        v-model="keyword"
        placeholder="搜索应用名称"
        clearable
        size="small"
        class="w-56"
      />
    </div>

    <!-- 列表行 -->
    <div class="flex flex-col gap-3">
      <GlassCard
        v-for="row in filtered"
        :key="row.id"
        data-spec="arow"
        class="flex cursor-pointer items-center gap-4 px-[18px] py-3.5 transition hover:translate-x-0.5 hover:bg-[var(--el-bg-color-overlay)]/60"
        @click="showDetail(row)"
      >
        <div
          data-spec="arow-icon"
          class="flex h-[42px] w-[42px] shrink-0 items-center justify-center rounded-xl bg-[var(--el-color-primary)]/15 text-lg text-[var(--el-color-primary)]"
        >
          {{ (row.name || 'A').charAt(0) }}
        </div>
        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-2">
            <span class="truncate text-[15px] font-semibold text-[var(--el-text-color-primary)]">{{ row.name || '-' }}</span>
            <span class="text-[10px] text-[var(--el-text-color-placeholder)]">{{ row.category || '' }}</span>
          </div>
          <div class="mt-0.5 truncate text-xs text-[var(--el-text-color-placeholder)]">
            {{ row.displayName || '暂无描述' }}
          </div>
        </div>
        <div class="flex items-center gap-[18px] text-xs">
          <div class="text-center">
            <div class="text-[10px] text-[var(--el-text-color-placeholder)]">
              实例
            </div>
            <div class="text-sm font-medium tabular-nums text-[var(--el-text-color-primary)]">
              {{ row.instances }}
            </div>
          </div>
          <div class="text-center">
            <div class="text-[10px] text-[var(--el-text-color-placeholder)]">
              服务
            </div>
            <div class="text-sm font-medium tabular-nums text-[var(--el-text-color-primary)]">
              {{ row.services }}
            </div>
          </div>
          <div class="text-center">
            <div class="text-[10px] text-[var(--el-text-color-placeholder)]">
              版本
            </div>
            <div class="text-sm font-medium tabular-nums text-[var(--el-text-color-primary)]">
              {{ row.version || '-' }}
            </div>
          </div>
        </div>
        <StatusPill :status="m.statusLabel[row.status]" />
      </GlassCard>
    </div>

    <!-- 应用详情抽屉：概览 / 实例 / 配置 / 发布 / 监控 / 服务 -->
    <el-drawer
      v-model="drawerVisible"
      :title="current?.name || '应用详情'"
      size="560px"
      data-spec="drawer"
      class="stardust-drawer"
      destroy-on-close
    >
      <template #header>
        <div class="flex min-w-0 flex-1 items-center justify-between gap-3">
          <div class="flex min-w-0 flex-col">
            <span class="truncate text-lg font-bold text-[var(--el-text-color-primary)]">
              {{ current?.name || '应用详情' }}
            </span>
            <span
              v-if="current?.displayName"
              class="truncate text-xs text-[var(--el-text-color-secondary)]"
            >
              {{ current.displayName }}
            </span>
          </div>
          <button
            class="sd-close"
            aria-label="关闭"
            @click="drawerVisible = false"
          >
            ×
          </button>
        </div>
      </template>

      <div class="flex flex-col">
        <!-- 抽屉头部三态 banner -->
        <div
          v-if="current"
          class="sd-banner"
          :style="m.bannerFor(current.status)"
        >
          <div class="flex items-center gap-2.5">
            <span class="sd-banner-dot" />
            <span class="sd-banner-text">{{ m.bannerFor(current.status).text }}</span>
          </div>
          <span class="sd-banner-version">{{ current.version || '-' }}</span>
        </div>

        <!-- 自定义页签 -->
        <div class="sd-tabs">
          <button
            v-for="t in m.drawerTabs"
            :key="t.key"
            class="sd-tab"
            :class="{ 'sd-tab-active': tab === t.key }"
            @click="tab = t.key"
          >
            {{ t.label }}
          </button>
        </div>

        <!-- 概览 -->
        <div
          v-if="tab === 'overview'"
          class="flex flex-col gap-4"
        >
          <div class="sd-overview-card">
            <div class="sd-overview-title">
              应用注册信息
            </div>
            <div class="sd-overview-grid">
              <div class="sd-overview-item">
                <span class="sd-overview-label">应用ID</span>
                <span class="sd-overview-value">{{ current?.id ?? '-' }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">当前版本</span>
                <span class="sd-overview-value font-mono">{{ current?.version || '-' }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">实例数</span>
                <span class="sd-overview-value">{{ current?.instances ?? 0 }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">语言框架</span>
                <span class="sd-overview-value">{{ [String(current?.language ?? ''), String(current?.framework ?? '')].filter(Boolean).join(' ') || '-' }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">负责人</span>
                <span class="sd-overview-value">{{ String(current?.createUser ?? '-') }}</span>
              </div>
              <div class="sd-overview-item">
                <span class="sd-overview-label">注册服务</span>
                <div class="flex items-center gap-2">
                  <span class="sd-mini-pill sd-mini-pill-success">{{ services.provide.length }} 提供</span>
                  <span class="sd-mini-pill sd-mini-pill-primary">{{ services.consume.length }} 消费</span>
                </div>
              </div>
            </div>
          </div>

          <div class="sd-tip">
            <span class="sd-tip-icon">ℹ</span>
            <span>概览仅汇总应用注册信息，实例 / 配置 / 发布 / 监控 / 服务明细请切换上方页签查看。</span>
          </div>
        </div>

        <!-- 实例 -->
        <div
          v-if="tab === 'instances'"
          class="flex flex-col gap-2"
        >
          <div
            v-for="(r, i) in instances"
            :key="i"
            class="sd-svc"
          >
            <div class="flex items-center justify-between gap-2">
              <div class="flex min-w-0 items-center gap-2">
                <span class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ r.client || r.processName || '-' }}</span>
                <span
                  class="sd-role"
                  :style="m.pillStyleFor(r.enable === false ? '离线' : '运行')"
                >{{ r.enable === false ? '离线' : '运行' }}</span>
              </div>
              <span class="shrink-0 text-xs text-[var(--el-text-color-placeholder)]">{{ r.ip || '-' }}</span>
            </div>
            <div class="mt-1 flex items-center justify-between gap-2 text-[11px] text-[var(--el-text-color-placeholder)]">
              <span class="truncate">{{ r.nodeName || r.machineName || '-' }}</span>
              <span class="shrink-0">{{ r.version || '-' }}</span>
            </div>
          </div>
          <el-empty
            v-if="instances.length === 0"
            description="暂无在线实例"
            :image-size="60"
          />
        </div>

        <!-- 配置：表格 / 文本双模式 + 版本历史卡 -->
        <div
          v-if="tab === 'config'"
          class="flex flex-col"
        >
          <div class="flex items-center justify-between">
            <div class="sd-seg">
              <button
                class="sd-seg-btn"
                :class="{ 'sd-seg-active': configMode === 'table' }"
                @click="configMode = 'table'"
              >
                表格
              </button>
              <button
                class="sd-seg-btn"
                :class="{ 'sd-seg-active': configMode === 'text' }"
                @click="configMode = 'text'"
              >
                文本
              </button>
            </div>
            <span class="text-xs text-[var(--el-text-color-placeholder)]">{{ configKvs.length }} 项</span>
          </div>
          <div
            v-if="configMode === 'table'"
            class="mt-2 flex flex-col"
          >
            <div
              v-for="kv in configKvs"
              :key="kv.key"
              class="flex justify-between border-b border-[var(--el-border-color-lighter)] py-[9px] text-[13px]"
            >
              <span class="text-[var(--el-text-color-regular)]">{{ kv.key }}</span>
              <span class="sd-kv-value">{{ kv.value }}</span>
            </div>
            <div
              v-if="!configKvs.length"
              class="py-2 text-xs text-[var(--el-text-color-placeholder)]"
            >
              暂无配置
            </div>
          </div>
          <pre
            v-else
            class="sd-config-text"
          >{{ configTxt || '暂无配置' }}</pre>
          <div class="sd-hist-card">
            <div class="text-xs font-semibold text-[var(--el-text-color-regular)]">
              版本历史
            </div>
            <div class="mt-[10px] flex flex-col gap-2">
              <template v-if="histories.length">
                <div
                  v-for="h in histories"
                  :key="h.id"
                  class="flex items-center justify-between gap-2 text-xs"
                >
                  <div class="flex min-w-0 flex-col gap-0.5">
                    <span class="truncate text-[var(--el-text-color-primary)]">{{ h.action || '发布' }}</span>
                    <span class="text-[var(--el-text-color-placeholder)]">{{ h.time || '-' }}</span>
                  </div>
                  <span class="flex shrink-0 items-center gap-3 text-[var(--el-color-primary)]">
                    <button
                      class="cursor-pointer"
                      @click="doDiff(h)"
                    >对比上一版 ⟲</button>
                    <button
                      class="cursor-pointer"
                      @click="doRollback(h)"
                    >回滚到此版本 ⤺</button>
                  </span>
                </div>
              </template>
              <div
                v-else
                class="text-xs text-[var(--el-text-color-placeholder)]"
              >
                暂无发布历史
              </div>
            </div>
          </div>
        </div>

        <!-- 发布：流水线 pstep + 箭头 + 图例 -->
        <div
          v-if="tab === 'publish'"
          class="flex flex-col"
        >
          <div
            class="flex flex-wrap items-start gap-2"
            data-spec="psteps"
          >
            <template
              v-for="(s, i) in steps"
              :key="s.key"
            >
              <div class="flex flex-col items-center gap-1.5">
                <div
                  data-spec="pstep"
                  class="sd-step"
                  :class="s.state === 'done' ? 'sd-step-done' : s.state === 'run' ? 'sd-step-run' : 'sd-step-wait'"
                >
                  {{ s.state === 'done' ? '✓' : s.state === 'run' ? '●' : '○' }}
                </div>
                <span class="text-[11px] text-[var(--el-text-color-secondary)]">{{ s.label }}</span>
              </div>
              <span
                v-if="i < steps.length - 1"
                class="sd-step-arrow"
              >→</span>
            </template>
          </div>
          <div class="mt-3 text-xs text-[var(--el-text-color-secondary)]">
            ✓ 完成 ● 进行中 ○ 等待
          </div>
          <div class="mt-4 flex flex-col gap-2">
            <div
              v-for="(r, i) in detail.runs.slice(0, 5)"
              :key="i"
              class="sd-svc flex items-center gap-2"
            >
              <span
                class="sd-role"
                :style="m.pillStyleFor(r.status === 'Success' ? '运行' : '离线')"
              >{{ r.status || '-' }}</span>
              <span class="min-w-0 flex-1 truncate text-[13px] font-semibold text-[var(--el-text-color-primary)]">
                {{ r.pipelineName || '流水线' }} v{{ r.appVersionId ?? '-' }}
              </span>
              <span class="shrink-0 text-[11px] text-[var(--el-text-color-placeholder)]">
                {{ String(r.deployFinishedTime || r.buildFinishedTime || '').slice(0, 16) }}
              </span>
            </div>
            <div
              v-if="!detail.runs.length"
              class="text-xs text-[var(--el-text-color-placeholder)]"
            >
              暂无发布记录
            </div>
          </div>
        </div>

        <!-- 监控：时间行 + 4 KPI + 双折线 svg（请求/延迟） -->
        <div
          v-if="tab === 'monitor'"
          class="flex flex-col"
        >
          <div class="flex items-center justify-between text-xs">
            <span class="text-[var(--el-text-color-regular)]">近 1 小时</span>
            <span class="flex items-center gap-1 text-[var(--el-text-color-placeholder)]">
              <i class="sd-dot bg-[var(--el-color-success)] animate-pulse" />自动刷新 15s
            </span>
          </div>
          <div class="mt-2 grid grid-cols-4 gap-2">
            <KpiStat
              label="QPS"
              :value="Math.round(monitor.qps * 10) / 10"
              accent="success"
            />
            <KpiStat
              label="P99 (ms)"
              :value="monitor.p99"
              accent="warning"
            />
            <KpiStat
              label="错误率 %"
              :value="Math.round(monitor.errorRate * 10000) / 100"
              accent="warning"
            />
            <KpiStat
              label="在线率 %"
              :value="Math.round(monitor.onlineRate * 10000) / 100"
              accent="success"
            />
          </div>
          <svg
            viewBox="0 0 320 96"
            preserveAspectRatio="none"
            class="mt-2 h-24 w-full rounded-lg bg-[var(--el-fill-color-light)]/50"
          >
            <polyline
              fill="none"
              stroke="var(--el-color-success)"
              stroke-width="2"
              stroke-linejoin="round"
              :points="monitor.series.map((s, i) => `${monitor.series.length > 1 ? (i * 300) / (monitor.series.length - 1) : 150},${90 - (s.qps / Math.max(...monitor.series.map((x) => x.qps), 1)) * 80}`).join(' ')"
            />
            <polyline
              fill="none"
              stroke="var(--el-color-primary)"
              stroke-width="2"
              stroke-linejoin="round"
              :points="monitor.series.map((s, i) => `${monitor.series.length > 1 ? (i * 300) / (monitor.series.length - 1) : 150},${90 - (s.latency / Math.max(...monitor.series.map((x) => x.latency), 1)) * 80}`).join(' ')"
            />
          </svg>
          <div class="mt-2 flex items-center gap-4 text-xs text-[var(--el-text-color-regular)]">
            <span class="flex items-center gap-1"><i class="sd-line bg-[var(--el-color-success)]" />请求</span>
            <span class="flex items-center gap-1"><i class="sd-line bg-[var(--el-color-primary)]" />延迟</span>
          </div>
        </div>

        <!-- 服务：提供 / 消费 -->
        <div
          v-if="tab === 'service'"
          class="flex flex-col gap-4"
        >
          <div>
            <div class="mb-2 text-xs font-semibold text-[var(--el-text-color-regular)]">
              提供（{{ services.provide.length }}）
            </div>
            <div class="flex flex-col gap-2">
              <div
                v-for="(r, i) in services.provide"
                :key="i"
                class="sd-svc"
              >
                <div class="flex items-center justify-between gap-2">
                  <div class="flex min-w-0 items-center gap-2">
                    <span class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ r.serviceName || '-' }}</span>
                    <span
                      class="sd-role"
                      :style="m.pillStyleFor('提供')"
                    >提供</span>
                  </div>
                  <span
                    class="shrink-0 text-xs"
                    :class="r.healthy ? 'text-[var(--el-color-success)]' : 'text-[var(--el-color-danger)]'"
                  >{{ r.healthy ? '正常' : '异常' }}</span>
                </div>
                <div class="mt-1 flex items-center justify-between gap-2 text-[11px] text-[var(--el-text-color-placeholder)]">
                  <span class="truncate">{{ r.address || '-' }}</span>
                  <span class="shrink-0">v{{ r.version || '-' }} · 权重 {{ r.weight ?? '-' }}</span>
                </div>
              </div>
              <el-empty
                v-if="services.provide.length === 0"
                description="暂无提供服务"
                :image-size="60"
              />
            </div>
          </div>
          <div>
            <div class="mb-2 text-xs font-semibold text-[var(--el-text-color-regular)]">
              消费（{{ services.consume.length }}）
            </div>
            <div class="flex flex-col gap-2">
              <div
                v-for="(r, i) in services.consume"
                :key="i"
                class="sd-svc"
              >
                <div class="flex items-center justify-between gap-2">
                  <div class="flex min-w-0 items-center gap-2">
                    <span class="truncate text-[13px] font-medium text-[var(--el-text-color-primary)]">{{ r.serviceName || '-' }}</span>
                    <span
                      class="sd-role"
                      :style="m.pillStyleFor('消费')"
                    >消费</span>
                  </div>
                  <span class="shrink-0 text-xs text-[var(--el-text-color-placeholder)]">{{ r.client || '-' }}</span>
                </div>
                <div class="mt-1 flex items-center justify-between gap-2 text-[11px] text-[var(--el-text-color-placeholder)]">
                  <span class="truncate">{{ r.address || '-' }}</span>
                  <span class="shrink-0">Ping {{ r.pingCount ?? 0 }}</span>
                </div>
              </div>
              <el-empty
                v-if="services.consume.length === 0"
                description="暂无消费服务"
                :image-size="60"
              />
            </div>
          </div>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<style scoped>
.sd-config-text {
  margin: 0;
  max-height: 220px;
  overflow: auto;
  border-radius: 8px;
  background: var(--el-fill-color-light);
  color: var(--el-color-info);
  padding: 10px 12px;
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-all;
}
.sd-hist-card {
  display: flex;
  flex-direction: column;
  margin-top: 16px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 8px;
  padding: 10px 12px;
}
.sd-seg {
  display: inline-flex;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
}
.sd-seg-btn {
  padding: 6px 14px;
  font-size: 12px;
  line-height: 1.4;
  color: var(--el-text-color-regular);
  background: transparent;
  transition:
    background-color 0.2s,
    color 0.2s;
}
.sd-seg-btn + .sd-seg-btn {
  border-left: 1px solid var(--el-border-color);
}
.sd-seg-active {
  background: color-mix(in srgb, var(--el-color-primary) 20%, transparent);
  color: var(--el-color-white);
}
.sd-kv-value {
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Courier New', monospace;
  color: var(--el-color-info);
}
.sd-step {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 54px;
  height: 54px;
  border: 2px solid;
  border-radius: 9999px;
  font-size: 20px;
  line-height: 1;
}
.sd-step-done {
  border-color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 15%, transparent);
  color: var(--el-color-success);
}
.sd-step-run {
  border-color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 15%, transparent);
  color: var(--el-color-primary);
  box-shadow: 0 0 12px color-mix(in srgb, var(--el-color-primary) 35%, transparent);
}
.sd-step-wait {
  border-color: var(--el-border-color);
  color: var(--el-text-color-secondary);
  opacity: 0.5;
}
.sd-step-arrow {
  margin-top: 18px;
  font-size: 18px;
  line-height: 1;
  color: var(--el-text-color-secondary);
}
.sd-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 9999px;
}
.sd-line {
  display: inline-block;
  width: 18px;
  height: 3px;
  border-radius: 2px;
}
.stardust-drawer :deep(.el-drawer__header) {
  margin-bottom: 0;
  padding: 16px 20px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.stardust-drawer :deep(.el-drawer__body) {
  padding: 20px;
  background: color-mix(in srgb, var(--el-bg-color) 92%, transparent);
}
.stardust-drawer :deep(.el-drawer__close-btn) {
  display: none;
}
.sd-close {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  font-size: 18px;
  line-height: 1;
  color: var(--el-text-color-regular);
  transition:
    background-color 0.2s,
    color 0.2s;
}
.sd-close:hover {
  background: var(--el-fill-color-light);
  color: var(--el-text-color-primary);
}
.sd-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid transparent;
  border-radius: 10px;
  padding: 12px 14px;
}
.sd-banner-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 10px currentColor;
}
.sd-banner-text {
  font-size: 13px;
  font-weight: 600;
}
.sd-banner-version {
  border-radius: 6px;
  padding: 3px 8px;
  background: color-mix(in srgb, var(--el-bg-color) 60%, transparent);
  font-family: ui-monospace, SFMono-Regular, Consolas, 'Courier New', monospace;
  font-size: 12px;
  color: var(--el-text-color-regular);
}
.sd-tabs {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-top: 18px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color-light);
}
.sd-tab {
  position: relative;
  padding: 8px 14px;
  font-size: 13px;
  line-height: 1.4;
  color: var(--el-text-color-regular);
  transition: color 0.2s;
}
.sd-tab:hover {
  color: var(--el-text-color-primary);
}
.sd-tab-active {
  color: var(--el-color-primary);
  font-weight: 600;
}
.sd-tab-active::after {
  content: '';
  position: absolute;
  right: 14px;
  bottom: -1px;
  left: 14px;
  height: 2px;
  border-radius: 2px 2px 0 0;
  background: var(--el-color-primary);
}
.sd-overview-card {
  border: 1px solid var(--el-border-color-light);
  border-radius: 12px;
  padding: 16px;
  background: color-mix(in srgb, var(--el-bg-color) 70%, transparent);
  backdrop-filter: blur(8px);
}
.sd-overview-title {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 14px;
  font-size: 12px;
  font-weight: 600;
  color: var(--el-text-color-regular);
}
.sd-overview-title::before {
  content: '';
  width: 3px;
  height: 12px;
  border-radius: 2px;
  background: var(--el-color-primary);
}
.sd-overview-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px 12px;
}
.sd-overview-item {
  display: flex;
  flex-direction: column;
  gap: 5px;
}
.sd-overview-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
}
.sd-overview-value {
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.sd-mini-pill {
  border-radius: 6px;
  padding: 2px 7px;
  font-size: 11px;
  font-weight: 600;
}
.sd-mini-pill-success {
  color: var(--el-color-success);
  background: color-mix(in srgb, var(--el-color-success) 12%, transparent);
}
.sd-mini-pill-primary {
  color: var(--el-color-primary);
  background: color-mix(in srgb, var(--el-color-primary) 12%, transparent);
}
.sd-tip {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  border: 1px solid color-mix(in srgb, var(--el-color-info) 25%, transparent);
  border-radius: 10px;
  padding: 12px 14px;
  background: color-mix(in srgb, var(--el-color-info) 8%, transparent);
  color: var(--el-text-color-regular);
  font-size: 12px;
  line-height: 1.5;
}
.sd-tip-icon {
  display: inline-flex;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: color-mix(in srgb, var(--el-color-info) 18%, transparent);
  color: var(--el-color-info);
  font-size: 10px;
  font-weight: 700;
}
.sd-svc {
  border: 1px solid var(--el-border-color-light);
  border-radius: 8px;
  padding: 10px 12px;
  background: color-mix(in srgb, var(--el-text-color-primary) 4%, transparent);
}
.sd-role {
  border-radius: 8px;
  padding: 2px 7px;
  font-size: 10px;
  line-height: 1.4;
  font-weight: 500;
  white-space: nowrap;
}
</style>
