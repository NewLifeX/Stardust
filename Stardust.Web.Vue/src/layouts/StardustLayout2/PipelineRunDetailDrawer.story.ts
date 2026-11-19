import PipelineRunDetailDrawer from './PipelineRunDetailDrawer.vue';
import type { RunDetail } from './ship.logic.ts';

function detail(patch: Partial<RunDetail>): RunDetail {
  return {
    id: 42,
    pipelineId: 7,
    pipelineName: 'build-main',
    status: 'Deploying',
    statusLabel: '部署中',
    tone: 'brand',
    triggerSource: 'webhook',
    branch: 'release/*',
    commitId: 'a1b2c3d',
    commitMessage: 'fix: 上传重试',
    commitAuthor: 'zhang',
    commitTime: '2026-11-07 09:12',
    buildNodeId: 3,
    appVersionId: 12,
    versionLabel: '20261107-090101',
    buildStartedTime: '2026-11-07 09:13',
    buildFinishedTime: '2026-11-07 09:19',
    deployStartedTime: '2026-11-07 09:20',
    deployFinishedTime: '—',
    traceId: 'a1b2c3d4e5f60718',
    traceUrl: '',
    remark: '',
    createTime: '2026-11-07 09:12',
    updateTime: '2026-11-07 09:20',
    steps: [],
    active: true,
    ...patch,
  };
}

const READY = detail({});

export const stories = [
  // 测试状态：进行中（轮询中徽标 + 全字段就绪）
  { id: 'PipelineRunDetailDrawer/Active', component: PipelineRunDetailDrawer, props: { open: true, state: 'ready', detail: READY } },

  // 测试状态：终态成功（无轮询徽标；有 TraceUrl → 追踪链接；耗时=部署完成-编译开始=8 分 480 秒）
  {
    id: 'PipelineRunDetailDrawer/Done',
    component: PipelineRunDetailDrawer,
    props: {
      open: true,
      state: 'ready',
      detail: detail({
        status: 'Success', statusLabel: '成功', tone: 'ok', active: false,
        deployFinishedTime: '2026-11-07 09:21', traceUrl: '/trace/a1b2c3d4e5f60718',
      }),
    },
  },

  // 测试状态：失败运行，备注含错误信息
  {
    id: 'PipelineRunDetailDrawer/Failed',
    component: PipelineRunDetailDrawer,
    props: {
      open: true,
      state: 'ready',
      detail: detail({ status: 'Failed', statusLabel: '失败', tone: 'err', active: false, remark: '编译失败：MSB3073 退出码 1' }),
    },
  },

  // 测试状态：初载 / 不存在 / 请求失败
  { id: 'PipelineRunDetailDrawer/Loading', component: PipelineRunDetailDrawer, props: { open: true, state: 'loading', detail: null } },
  { id: 'PipelineRunDetailDrawer/Missing', component: PipelineRunDetailDrawer, props: { open: true, state: 'missing', detail: null } },
  { id: 'PipelineRunDetailDrawer/Error', component: PipelineRunDetailDrawer, props: { open: true, state: 'failed', detail: null } },
];
