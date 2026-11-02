/**
 * 发布页各展示组件共用的 Tailwind 类名（2026-10-28 从 ShipPanel.vue 抽出，值与原文一致）。
 * 只放颜色走 --el-* 的摆放类，不放逻辑。
 */
import type { Notice } from './ship.logic.ts';

/** 区块小标题 */
export const H3 = 'mb-[10px] flex flex-wrap items-center gap-2 text-[13px] font-semibold text-[var(--el-text-color-primary)]';

/** 表头单元格 */
export const TH =
  'border-b border-[var(--el-border-color-light)] px-[14px] py-[9px] text-left text-[11px] font-medium text-[var(--el-text-color-secondary)]';

/** 表体单元格，配合行上的 group 去掉最后一行的下边线 */
export const TD =
  'border-b border-[var(--el-border-color-light)] px-[14px] py-[10px] text-[12.5px] group-last:border-b-0';

/** 提示条：warn 用警告色，info 用次要文字色 */
export const NOTICE_CLASS: Record<Notice['tone'], string> = {
  warn: 'border-[color-mix(in_oklab,var(--el-color-warning)_35%,transparent)] bg-[color-mix(in_oklab,var(--el-color-warning)_10%,transparent)] text-[var(--el-color-warning)]',
  info: 'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)]',
};

/** 原生下拉 */
export const SELECT =
  'h-[28px] cursor-pointer rounded-[8px] border border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] px-[9px] text-[12px] text-[var(--el-text-color-primary)] outline-none focus:border-[var(--el-color-primary)]';
