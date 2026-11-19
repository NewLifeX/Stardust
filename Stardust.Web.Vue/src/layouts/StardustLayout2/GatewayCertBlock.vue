<script setup lang="ts">
/**
 * GatewayCertBlock：治理页「网关」页签——网关证书区块（CARD-GW-04，形态 1 区块卡，多区块末位）。
 * 纯 UI：状态（rows/loading/error/filter/expand）由容器持有；只发 create/edit/remove/filterChange/retry/expand。
 * 7 列（与盘点 §4 对齐，无 Id 编号列）：域名 / 启用 / PFX文件 / 证书文件 / 私钥文件 / PEM文件 / 操作。
 * 有效期（NotAfter）不进列（定稿裁定 4）——收「有效期」行内展开（懒拉 Detail，容器持有展开态），
 * 列表无值时不造数。文件列=服务端路径（WebApi 无上传端点，降级纯路径输入已回填卡片）。
 * PfxPassword 无列（硬条款①，控制器 ListFields.RemoveField 同形）；列表行不输出任何敏感值（硬条款④）。
 * 三态：加载「证书加载中…」/ 失败「证书加载失败：{原因}」+重试 / 空「还没有网关证书」+「添加证书」引导（J-GW-01 C16）。
 * 筛选条：enable 开关（勾选=只看启用）、时间区间（注记「按有效期至（NotAfter）过滤」——不复刻「创建时间」误导）、
 * Q 关键字；**无域名搜索控件**（后端 domain 读了不过滤，复刻即假搜索——定稿裁量 2）；projectId 永不传（C11）。
 */
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { H3, SELECT, TD, TH } from './deployView.styles.ts';
import type { CertRow, GatewayCertForm, GatewayCertFilter } from './gatewayCert.logic.ts';

withDefaults(
  defineProps<{
    rows: CertRow[];
    loading?: boolean;
    error?: string;
    /** 行内展开（有效期）：展开行 id / 详情加载中 / 失败文案 / 详情归一（证书信息组） */
    expandedId?: number | null;
    expandedLoading?: boolean;
    expandedError?: string;
    expandedInfo?: GatewayCertForm | null;
  }>(),
  { loading: false, error: '', expandedId: null, expandedLoading: false, expandedError: '', expandedInfo: null },
);

const emit = defineEmits<{
  /** 工具条「添加证书」/空态引导「添加证书」 */
  create: [];
  /** 行操作：编辑（容器拉 Detail 回填抽屉） */
  edit: [row: CertRow];
  /** 行操作：删除（容器 GuardModal 确认后请求） */
  remove: [row: CertRow];
  /** 筛选变化（部分补，容器持有 filter 并重取） */
  filterChange: [patch: Partial<GatewayCertFilter>];
  /** 失败态重试（容器整页签重取） */
  retry: [];
  /** 行内展开/收起「有效期」（容器懒拉 Detail 承载，定稿裁定 4） */
  expand: [row: CertRow];
}>();

function onEnable(e: Event): void {
  emit('filterChange', { enable: (e.target as HTMLInputElement).checked });
}
function onQ(e: Event): void {
  emit('filterChange', { q: (e.target as HTMLInputElement).value.trim() });
}
function onDtStart(e: Event): void {
  emit('filterChange', { dtStart: (e.target as HTMLInputElement).value });
}
function onDtEnd(e: Event): void {
  emit('filterChange', { dtEnd: (e.target as HTMLInputElement).value });
}
</script>

<template>
  <Card
    padding="none"
    data-spec="gateway-cert"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        网关证书
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ rows.length }} 条</span>
      </h3>
      <span class="flex-1" />
      <Btn
        size="sm"
        variant="default"
        data-spec="gateway-cert-create"
        @click="emit('create')"
      >
        添加证书
      </Btn>
    </div>

    <!-- 筛选条：开关勾选=只看启用；加载/失败态不显示 -->
    <div
      v-if="!loading && !error"
      class="mb-[10px] mt-[8px] flex flex-wrap items-center gap-2 px-[14px]"
      data-spec="gateway-cert-filter"
    >
      <label class="inline-flex cursor-pointer items-center gap-[6px] text-[11.5px] text-[var(--el-text-color-secondary)]">
        <input
          type="checkbox"
          class="h-[13px] w-[13px]"
          aria-label="只看启用证书"
          data-spec="gateway-cert-filter-enable"
          @change="onEnable"
        >
        启用
      </label>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">有效期</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="开始日期"
        data-spec="gateway-cert-filter-dtstart"
        @change="onDtStart"
      >
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">~</span>
      <input
        type="date"
        :class="SELECT"
        aria-label="结束日期"
        data-spec="gateway-cert-filter-dtend"
        @change="onDtEnd"
      >
      <span
        class="text-[11px] text-[var(--el-text-color-secondary)]"
        data-spec="gateway-cert-filter-time-note"
      >按有效期至（NotAfter）过滤</span>
      <span class="text-[11.5px] text-[var(--el-text-color-secondary)]">关键字</span>
      <input
        type="text"
        placeholder="域名 / 备注"
        :class="SELECT"
        aria-label="按关键字筛选"
        data-spec="gateway-cert-filter-q"
        @change="onQ"
      >
    </div>

    <div
      v-if="loading"
      data-spec="gateway-cert-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      证书加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="gateway-cert-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        证书加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="gateway-cert-retry"
        class="mt-[8px] cursor-pointer rounded-[6px] border border-[var(--el-border-color-light)] px-[10px] py-[4px] text-[12px] text-[var(--el-text-color-regular)] hover:bg-[var(--el-bg-color-overlay)]"
        @click="emit('retry')"
      >
        重试
      </button>
    </div>

    <table
      v-else
      class="w-full border-collapse"
    >
      <thead>
        <tr>
          <th :class="TH">
            域名
          </th>
          <th :class="[TH, 'text-right']">
            启用
          </th>
          <th :class="TH">
            PFX文件
          </th>
          <th :class="TH">
            证书文件
          </th>
          <th :class="TH">
            私钥文件
          </th>
          <th :class="TH">
            PEM文件
          </th>
          <th :class="[TH, 'text-right']">
            操作
          </th>
        </tr>
      </thead>
      <tbody class="group">
        <template
          v-for="r in rows"
          :key="r.id"
        >
          <tr
            data-spec="gateway-cert-row"
            class="hover:bg-[var(--el-bg-color-overlay)]"
          >
            <td :class="TD">
              <span class="font-mono text-[12.5px] text-[var(--el-text-color-primary)]">{{ r.domain }}</span>
            </td>
            <td :class="[TD, 'text-right']">
              <Tag :tone="r.enable ? 'ok' : 'default'">
                {{ r.enable ? '启用' : '停用' }}
              </Tag>
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">{{ r.pfxFile || '—' }}</span>
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">{{ r.crtFile || '—' }}</span>
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">{{ r.keyFile || '—' }}</span>
            </td>
            <td :class="TD">
              <span class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">{{ r.pemFile || '—' }}</span>
            </td>
            <td :class="[TD, 'text-right']">
              <span class="inline-flex justify-end gap-1">
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="gateway-cert-expand"
                  :aria-expanded="expandedId === r.id"
                  @click="emit('expand', r)"
                >
                  有效期
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="gateway-cert-edit"
                  @click="emit('edit', r)"
                >
                  编辑
                </Btn>
                <Btn
                  size="sm"
                  variant="ghost"
                  data-spec="gateway-cert-remove"
                  @click="emit('remove', r)"
                >
                  删除
                </Btn>
              </span>
            </td>
          </tr>
          <!-- 行内展开：有效期/证书信息（懒拉 Detail，定稿裁定 4——排查价值由展开区满足） -->
          <tr
            v-if="expandedId === r.id"
            data-spec="gateway-cert-expand-row"
            class="bg-[var(--el-fill-color-lighter)]"
          >
            <td
              colspan="7"
              data-spec="gateway-cert-expand-detail"
              class="px-[24px] py-[10px]"
            >
              <div
                v-if="expandedLoading"
                class="text-[12px] text-[var(--el-text-color-secondary)]"
              >
                证书信息加载中…
              </div>
              <div
                v-else-if="expandedError"
                class="text-[12px] text-[var(--el-color-danger)]"
              >
                {{ expandedError }}
              </div>
              <div
                v-else
                class="flex flex-wrap gap-x-[24px] gap-y-[6px] text-[12px]"
              >
                <span class="text-[var(--el-text-color-secondary)]">有效期至 <span class="font-mono text-[var(--el-text-color-primary)]">{{ expandedInfo?.notAfter || '—' }}</span></span>
                <span class="text-[var(--el-text-color-secondary)]">生效 <span class="font-mono text-[var(--el-text-color-primary)]">{{ expandedInfo?.notBefore || '—' }}</span></span>
                <span class="text-[var(--el-text-color-secondary)]">颁发者 <span class="text-[var(--el-text-color-primary)]">{{ expandedInfo?.issuer || '—' }}</span></span>
                <span class="text-[var(--el-text-color-secondary)]">使用者 <span class="text-[var(--el-text-color-primary)]">{{ expandedInfo?.subject || '—' }}</span></span>
                <span class="text-[var(--el-text-color-secondary)]">指纹 <span class="font-mono text-[var(--el-text-color-primary)]">{{ expandedInfo?.thumbprint || '—' }}</span></span>
                <span class="text-[var(--el-text-color-secondary)]">上次续期 <span class="font-mono text-[var(--el-text-color-primary)]">{{ expandedInfo?.lastRenew || '—' }}</span></span>
              </div>
            </td>
          </tr>
        </template>
        <tr v-if="!rows.length">
          <td
            colspan="7"
            data-spec="gateway-cert-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            <div>还没有网关证书</div>
            <Btn
              size="sm"
              variant="default"
              class="mt-[8px]"
              data-spec="gateway-cert-empty-create"
              @click="emit('create')"
            >
              添加证书
            </Btn>
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
