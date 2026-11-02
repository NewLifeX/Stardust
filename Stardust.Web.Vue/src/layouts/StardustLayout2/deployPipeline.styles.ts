/**
 * 发布页「流水线运行 · 按节点的部署结果」专用的类型与 Tailwind 类名
 * （2026-10-28 从 ShipPanel.vue 抽出，样式值与原文一致）。
 * StageItem 与 appsModel 的 PipelineStep 结构相同；布局层不反向依赖 apps/ 下的页面模块，所以在这里独立声明。
 */
import type { NodeCell, NodeResult } from './ship.logic.ts';

export type StageState = 'done' | 'run' | 'wait';

/** 流水线一段（构建、上传、分发、部署） */
export interface StageItem {
  key: string;
  label: string;
  state: StageState;
}

/** 按节点结果区的展示数据：由容器根据运行、步骤、节点算好后传入 */
export interface PipelineView {
  items: NodeResult[];
  /** 有部署步骤且全部被跳过：后端可能仍把运行标成成功，要单独说明 */
  allSkipped: boolean;
  note: string;
}

export const CELL_BASE =
  'grid aspect-[1.5] place-items-center rounded-[4px] border font-mono text-[9.5px] transition-colors duration-[200ms]';

export const CELL_CLASS: Record<NodeCell, string> = {
  none: 'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-placeholder)]',
  old: 'border-[var(--el-border-color-light)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-placeholder)]',
  off: 'border-dashed border-[var(--el-border-color)] bg-transparent text-[var(--el-text-color-placeholder)]',
  wait: 'border-[var(--el-border-color)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)]',
  ing: 'border-transparent bg-[var(--el-color-primary)] text-[var(--el-color-white)]',
  cur: 'border-transparent bg-[color-mix(in_oklab,var(--el-color-success)_85%,transparent)] text-[var(--el-color-black)]',
  ok: 'border-transparent bg-[color-mix(in_oklab,var(--el-color-success)_85%,transparent)] text-[var(--el-color-black)]',
  fail: 'border-transparent bg-[var(--el-color-danger)] text-[var(--el-color-white)]',
  skip: 'border-[var(--el-border-color)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)] opacity-60',
  cancel: 'border-[var(--el-border-color)] bg-[var(--el-bg-color-overlay)] text-[var(--el-text-color-secondary)] opacity-60',
  unrun: 'border-transparent bg-[color-mix(in_oklab,var(--el-color-warning)_55%,transparent)] text-[var(--el-color-black)]',
  noreply: 'border-transparent bg-[color-mix(in_oklab,var(--el-color-warning)_85%,transparent)] text-[var(--el-color-black)]',
};

export const STEP_BASE =
  'relative flex-1 text-center text-[11px] font-medium tracking-[0.02em] before:absolute before:top-[11px] before:left-[calc(-50%+12px)] before:right-[calc(50%+12px)] before:h-px first:before:hidden';

export const STEP_CLASS: Record<StageState, string> = {
  done: 'text-[var(--el-text-color-regular)] before:bg-[color-mix(in_oklab,var(--el-color-success)_50%,transparent)]',
  run: 'text-[var(--el-text-color-primary)] before:bg-[var(--el-border-color-light)]',
  wait: 'text-[var(--el-text-color-placeholder)] before:bg-[var(--el-border-color-light)]',
};

export const DOT_BASE =
  'relative mx-auto mb-[6px] grid h-[22px] w-[22px] place-items-center rounded-full border font-mono text-[10.5px]';

export const DOT_CLASS: Record<StageState, string> = {
  done: 'border-[var(--el-color-success)] bg-[var(--el-color-success)] text-[var(--el-color-black)]',
  run: 'border-[var(--el-color-primary)] bg-[var(--el-bg-color)] text-[var(--el-color-primary-light-3)] shadow-[0_0_12px_color-mix(in_oklab,var(--el-color-primary)_50%,transparent)]',
  wait: 'border-[var(--el-border-color)] bg-[var(--el-bg-color)]',
};
