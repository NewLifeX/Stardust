import PipelineConfigCard from './PipelineConfigCard.vue';
import type { PipelineRow } from './pipelineConfig.logic.ts';

function row(id: number, patch: Partial<PipelineRow>): PipelineRow {
  return {
    id,
    name: `pipeline-${id}`,
    deployId: 10,
    branch: id % 2 ? 'release/*' : 'main',
    buildNodeId: 3,
    projectId: 88,
    deployNodeIds: '1,2',
    autoDeploy: id % 2 === 0,
    enable: true,
    updateTime: '2026-11-07T10:20:30',
    ...patch,
  };
}

const ROWS: PipelineRow[] = [
  row(7, { name: 'build-main', branch: 'release/*', autoDeploy: true }),
  row(8, { name: 'hotfix', branch: 'main', autoDeploy: false, enable: false }),
];

export const stories = [
  // 测试状态：两条流水线（含禁用行），行操作齐备（复制 WebHook/启动/编辑/删除）
  { id: 'PipelineConfigCard/Default', component: PipelineConfigCard, props: { rows: ROWS, failed: false, busyKey: '' } },

  // 测试状态：部署集存在但无流水线 → 空态「还没有流水线」+「创建流水线」引导（裁定 #9，C21）
  { id: 'PipelineConfigCard/Empty', component: PipelineConfigCard, props: { rows: [], failed: false, busyKey: '' } },

  // 测试状态：加载失败 → 失败行 + 重试
  { id: 'PipelineConfigCard/Failed', component: PipelineConfigCard, props: { rows: [], failed: true, busyKey: '' } },

  // 测试状态：复制进行中（复制按钮旋转禁用）+ 启动进行中（触发按钮旋转禁用）+ 删除进行中（AGENTS.md §11）
  { id: 'PipelineConfigCard/Busy', component: PipelineConfigCard, props: { rows: ROWS, failed: false, busyKey: 'copy:7' } },
  { id: 'PipelineConfigCard/Triggering', component: PipelineConfigCard, props: { rows: ROWS, failed: false, busyKey: 'trigger:7' } },
  { id: 'PipelineConfigCard/Removing', component: PipelineConfigCard, props: { rows: ROWS, failed: false, busyKey: 'del:7' } },
];
