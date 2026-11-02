<script setup lang="ts">
/**
 * RegistryTab：治理页「注册中心」表格（纯 UI）。
 * 列：服务 / 实例 / 健康 / 负载均衡 / 消费者。
 * 实例=Service.Providers、消费者=Service.Consumers（后端聚合数）；健康条按 Enable 在线/停用二态渲染，
 * 不编造百分比；「负载均衡」在 Service/Gateway 之间无对应路由级字段，固定显示「待接入」。
 */
import Card from './Card.vue';
import Tag from './Tag.vue';
import { H3, TD, TH } from './deployView.styles.ts';
import type { RegistryRow } from './mesh.logic.ts';

withDefaults(
  defineProps<{
    rows: RegistryRow[];
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
    data-spec="registry-tab"
  >
    <div class="flex flex-wrap items-center gap-2 px-[14px] pt-[12px]">
      <h3 :class="H3">
        注册中心
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">{{ rows.length }} 个服务</span>
        <Tag tone="default">
          负载均衡待接入
        </Tag>
      </h3>
    </div>

    <div
      v-if="loading"
      data-spec="registry-loading"
      class="flex items-center gap-[8px] px-[14px] py-[18px] text-[12.5px] text-[var(--el-text-color-secondary)]"
    >
      <span class="inline-block h-[14px] w-[14px] animate-spin rounded-full border-2 border-[var(--el-border-color)] border-t-[var(--el-color-primary)]" />
      服务加载中…
    </div>

    <div
      v-else-if="error"
      data-spec="registry-failed"
      class="px-[14px] py-[14px]"
    >
      <div class="text-[12.5px] text-[var(--el-color-danger)]">
        注册中心加载失败：{{ error }}
      </div>
      <button
        type="button"
        data-spec="registry-retry"
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
            服务
          </th>
          <th :class="[TH, 'text-right']">
            实例
          </th>
          <th :class="TH">
            健康
          </th>
          <th :class="TH">
            负载均衡
          </th>
          <th :class="[TH, 'text-right']">
            消费者
          </th>
        </tr>
      </thead>
      <tbody class="group">
        <tr
          v-for="r in rows"
          :key="r.id"
          data-spec="registry-row"
          class="hover:bg-[var(--el-bg-color-overlay)]"
        >
          <td :class="TD">
            <div class="text-[12.5px] font-medium text-[var(--el-text-color-primary)]">
              {{ r.name || r.serviceName }}
            </div>
            <div class="text-[11.5px] text-[var(--el-text-color-secondary)]">
              <span class="font-mono">{{ r.serviceName }}</span>
              <span v-if="r.category"> · {{ r.category }}</span>
            </div>
          </td>
          <td :class="[TD, 'text-right font-mono tabular-nums text-[12.5px] text-[var(--el-text-color-primary)]']">
            {{ r.instances }}
          </td>
          <td :class="TD">
            <div class="flex items-center gap-[8px]">
              <!-- 真实二态：在线=满条成功色，停用=空条；不编造百分比 -->
              <span
                class="relative inline-block h-[6px] w-[90px] overflow-hidden rounded-full border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)]"
              >
                <span
                  class="absolute inset-y-0 left-0 rounded-full"
                  :class="r.enable
                    ? 'w-full bg-[var(--el-color-success)]'
                    : 'w-0 bg-transparent'"
                />
              </span>
              <Tag :tone="r.enable ? 'ok' : 'default'">
                {{ r.enable ? '在线' : '停用' }}
              </Tag>
            </div>
          </td>
          <td :class="TD">
            <Tag tone="default">
              待接入
            </Tag>
          </td>
          <td :class="[TD, 'text-right font-mono tabular-nums text-[12.5px] text-[var(--el-text-color-secondary)]']">
            {{ r.consumers }}
          </td>
        </tr>
        <tr v-if="!rows.length">
          <td
            colspan="5"
            data-spec="registry-empty"
            class="px-[14px] py-[18px] text-center text-[12.5px] text-[var(--el-text-color-secondary)]"
          >
            还没有注册任何服务
          </td>
        </tr>
      </tbody>
    </table>
  </Card>
</template>
