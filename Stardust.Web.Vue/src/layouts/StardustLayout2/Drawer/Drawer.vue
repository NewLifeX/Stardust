<script setup lang="ts">
/**
 * Drawer：右侧抽屉，对照原型 kit-shell.jsx 的 Drawer 与 kit.css 的 .x-scrim / .x-drawer。
 * 用 Element Plus 的 ElDrawer 封装（输入-1027：不走 useModal / ModalContainer）。
 * 纯 UI：open 由父组件持有；X 按钮、点遮罩、Esc 都只发 close，不自己改 open。
 * 插槽：actions（操作按钮行）、tabs（页签）、默认（正文）。
 * 与原型的已知差异（入场动画、焦点陷阱、Esc 层级）记在 TODO2.md。
 */
import { ElDrawer } from 'element-plus';
import KIcon from '../KIcon.vue';

withDefaults(
  defineProps<{
    open: boolean;
    title: string;
    sub?: string;
  }>(),
  { sub: '' },
);

const emit = defineEmits<{ close: [] }>();

// beforeClose 接管关闭：ElDrawer 不会自己把可见状态改成 false，由父组件改 open
const requestClose = () => emit('close');

// 抽屉本体：从 56px 顶栏下沿贴右铺到底，宽 min(560px, 96vw)，左边框 border-2，背景 surface-1
// `!` 用来压过 .el-drawer.rtl 的 top/height 和全局 .el-drawer 的背景、阴影
const DRAWER_CLASS =
  '!top-[56px] !h-auto !w-[min(560px,96vw)] !border-l !border-[var(--el-border-color)] !bg-[var(--el-bg-color)] !shadow-[var(--el-box-shadow)]';

// 遮罩：铺满全屏（含顶栏），8px 模糊；颜色与命令面板一致（亮 48%、暗 72%）
const MASK_CLASS =
  '!bg-[color-mix(in_oklab,var(--el-color-black)_48%,transparent)] backdrop-blur-[8px] [.dark_&]:!bg-[color-mix(in_oklab,var(--el-color-black)_72%,transparent)]';

// 正文区：原型 padding 20px 22px，超出滚动（ElDrawer 的 body 自带 overflow: auto）
const BODY_CLASS = '!px-[22px] !py-5';
</script>

<template>
  <ElDrawer
    data-spec="drawer"
    :model-value="open"
    :title="title"
    :with-header="false"
    destroy-on-close
    size="560px"
    :z-index="40"
    :before-close="requestClose"
    :class="DRAWER_CLASS"
    :modal-class="MASK_CLASS"
    :body-class="BODY_CLASS"
  >
    <div class="mb-[14px] flex items-start gap-[10px]">
      <div class="min-w-0">
        <h2 class="flex items-center gap-2 text-[18px] font-semibold tracking-[-0.01em] text-[var(--el-text-color-primary)]">
          {{ title }}
        </h2>
        <div
          v-if="sub"
          class="mt-[3px] font-mono text-[12px] tabular-nums text-[var(--el-text-color-secondary)]"
        >
          {{ sub }}
        </div>
      </div>
      <span class="flex-1" />
      <button
        type="button"
        data-spec="drawer-close"
        aria-label="关闭"
        class="grid h-[30px] w-[30px] shrink-0 place-items-center rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color)] text-[var(--el-text-color-regular)] transition-colors duration-[120ms] hover:bg-[var(--el-fill-color-light)] hover:text-[var(--el-text-color-primary)]"
        @click="requestClose"
      >
        <KIcon
          name="X"
          :size="14"
        />
      </button>
    </div>

    <div
      v-if="$slots.actions"
      class="mb-3 flex flex-wrap gap-2"
    >
      <slot name="actions" />
    </div>

    <slot name="tabs" />
    <slot />
  </ElDrawer>
</template>