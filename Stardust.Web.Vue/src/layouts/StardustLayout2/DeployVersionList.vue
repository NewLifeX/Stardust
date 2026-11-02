<script setup lang="ts">
/**
 * DeployVersionList：发布页「版本列表」，对应 ShipPanel 原模板的第 2 块（2026-10-28 拆分）。
 * 纯 UI：只收 props、只发 apply(v)；二次确认（openGuard）与真正下发（applyVersion）在容器里。
 * 默认只显示前 8 个版本，超过时底部可展开；展开状态是纯展示状态，留在本组件内。
 */
import { computed, ref } from 'vue';
import Btn from './Btn.vue';
import Card from './Card.vue';
import Tag from './Tag.vue';
import { H3, NOTICE_CLASS, TD, TH } from './deployView.styles.ts';
import type { Notice, VersionOption } from './ship.logic.ts';

/** 折叠时显示的版本数 */
const COLLAPSED_LIMIT = 8;

const props = defineProps<{
  versions: VersionOption[];
  /** 版本加载失败：底部写失败而不是「还没有版本」 */
  versionsFailed: boolean;
  /** 应用此版本前要让用户看到的后果，没有则不显示提示条 */
  notice: Notice | null;
  /** 正在进行的操作 key，形如 `ver:<版本Id>`；非空时所有按钮禁用 */
  busyKey: string;
}>();

const emit = defineEmits<{ apply: [v: VersionOption] }>();

const showAll = ref(false);
const shown = computed(() => (showAll.value ? props.versions : props.versions.slice(0, COLLAPSED_LIMIT)));

function buttonTitle(v: VersionOption): string {
  if (v.current) return '已经是当前生效版本';
  if (v.disabled) return v.reason;
  return '把该版本设为当前生效版本';
}

function buttonText(v: VersionOption): string {
  if (props.busyKey === `ver:${v.id}`) return '切换中…';
  return v.current ? '当前版本' : '应用此版本';
}
</script>

<template>
  <Card
    padding="none"
    data-spec="ship-versions"
  >
    <div class="px-[18px] pb-2 pt-4">
      <h3 :class="H3">
        版本列表
        <span class="text-[11.5px] font-normal text-[var(--el-text-color-secondary)]">共 {{ versions.length }} 个，应用后即成为当前生效版本</span>
      </h3>
      <div
        v-if="notice"
        class="rounded-[8px] border px-3 py-2 text-[12px] leading-[1.6]"
        :class="NOTICE_CLASS[notice.tone]"
        data-spec="ship-version-notice"
      >
        {{ notice.text }}
      </div>
    </div>
    <table class="w-full border-collapse">
      <thead>
        <tr>
          <th :class="TH">
            版本
          </th>
          <th :class="TH">
            状态
          </th>
          <th :class="[TH, 'text-right']">
            操作
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="v in shown"
          :key="v.id"
          data-spec="ship-version-row"
          class="group"
        >
          <td :class="[TD, 'font-mono tabular-nums']">
            <b class="font-medium text-[var(--el-text-color-primary)]">{{ v.version || `#${v.id}` }}</b>
          </td>
          <td :class="TD">
            <Tag
              v-if="v.current"
              tone="ok"
            >
              当前生效
            </Tag>
            <Tag v-else-if="v.disabled">
              不可用 · {{ v.reason }}
            </Tag>
            <Tag
              v-else
              tone="brand"
            >
              可应用
            </Tag>
          </td>
          <td :class="[TD, 'text-right']">
            <Btn
              size="sm"
              :variant="v.current || v.disabled ? 'default' : 'primary'"
              :disabled="v.current || v.disabled || !!busyKey"
              :title="buttonTitle(v)"
              data-spec="ship-apply"
              @click="emit('apply', v)"
            >
              {{ buttonText(v) }}
            </Btn>
          </td>
        </tr>
        <tr v-if="versionsFailed">
          <td
            colspan="3"
            class="p-5 text-[12.5px] text-[var(--el-color-danger)]"
            data-spec="ship-versions-failed"
          >
            版本加载失败，请点上方「刷新」重试
          </td>
        </tr>
        <tr v-else-if="!versions.length">
          <td
            colspan="3"
            class="p-5 text-[12.5px] text-[var(--el-text-color-secondary)]"
            data-spec="ship-versions-empty"
          >
            该部署集还没有版本
          </td>
        </tr>
      </tbody>
    </table>
    <div
      v-if="versions.length > COLLAPSED_LIMIT"
      class="border-t border-[var(--el-border-color-light)] px-[18px] py-2"
    >
      <Btn
        size="sm"
        variant="ghost"
        data-spec="ship-versions-toggle"
        @click="showAll = !showAll"
      >
        {{ showAll ? '收起' : `显示全部 ${versions.length} 个版本` }}
      </Btn>
    </div>
  </Card>
</template>
