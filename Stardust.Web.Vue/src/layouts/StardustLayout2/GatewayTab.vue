<script setup lang="ts">
/**
 * GatewayTab：治理页「网关」路由表（纯 UI）。
 * 列：路由 / 匹配 / 目标集群 / 健康 / TLS。
 * 路由=Name（副行 Domain+Path）；匹配=Methods（空=ALL）+Path；目标集群=ClusterId 经集群表解析，
 * 查不到显示「待接入」；健康=Enable 真实状态；TLS=按路由域名是否存在启用证书真实判断，
 * 不硬编码「TLS 1.3」。
 */
import Card from './Card.vue';
import Tag from './Tag.vue';
import KIcon from './KIcon.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { GatewayRouteRow } from './mesh.logic.ts';

withDefaults(
  defineProps<{
    rows: GatewayRouteRow[];
    loading?: boolean;
    error?: string;
  }>(),
  { loading: false, error: '' },
);

const emit = defineEmits<{ retry: [] }>();
</script>

<template>
  <Card
    padding="none"
    data-spec="gateway-tab"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        网关路由
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ rows.length }} 条</span>
      </h3>
    </div>

    <div
      v-if="loading"
      data-spec="gateway-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      路由加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="gateway-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        网关路由加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="gateway-retry"
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
            路由
          </th>
          <th :class="TH">
            匹配
          </th>
          <th :class="TH">
            目标集群
          </th>
          <th :class="[TH, 'text-right']">
            健康
          </th>
          <th :class="TH">
            TLS
          </th>
        </tr>
      </thead>
      <tbody class="group">
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="gateway-row"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="TD">
            <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
              {{ r.name }}
            </div>
            <div class="text-[11.5px] text-[var(--el-text-color-secondary)]">
              {{ r.domain || '—' }}
            </div>
          </td>
          <td :class="TD">
            <div class="font-mono text-[12px] text-[var(--el-text-color-primary)]">
              {{ r.methods || 'ALL' }}
            </div>
            <div class="font-mono text-[11.5px] text-[var(--el-text-color-secondary)]">
              {{ r.path }}
            </div>
          </td>
          <td :class="TD">
            <span
              v-if="r.clusterName"
              class="text-[12.5px] text-[var(--el-text-color-primary)]"
            >{{ r.clusterName }}</span>
            <Tag
              v-else
              tone="default"
            >
              待接入
            </Tag>
          </td>
          <td :class="[TD, 'text-right']">
            <Tag :tone="r.enable ? 'ok' : 'default'">
              {{ r.enable ? '启用' : '停用' }}
            </Tag>
          </td>
          <td :class="TD">
            <div
              v-if="r.tls.present"
              class="flex items-center gap-[5px] text-[12px] text-[var(--el-text-color-secondary)]"
            >
              <KIcon
                name="Lock"
                :size="11"
              />
              <span class="font-mono">{{ r.tls.domain }}</span>
              <span
                v-if="r.tls.autoRenew"
                class="text-[11px]"
              >自动续期</span>
            </div>
            <Tag
              v-else
              tone="default"
            >
              待接入
            </Tag>
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="5"
            data-spec="gateway-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            还没有配置网关路由
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
