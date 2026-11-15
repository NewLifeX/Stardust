import DeployLogDrawer from './DeployLogDrawer.vue';

/** task-90 配置引导：点「配置日志目录」上抛 configure 事件，录制进 window.__logConfigure（CT 断言用，AlarmRecordDrawer 先例） */
export const stories = [
  // 未配置日志目录：配置引导空态（不发请求），configure 事件录制
  {
    id: 'DeployLogDrawer/Unconfigured',
    component: DeployLogDrawer,
    props: {
      open: true,
      deployNodeId: 5,
      nodeName: 'node-a',
      logDir: '',
      onConfigure: () => {
        const w = window as unknown as { __logConfigure?: number[] };
        (w.__logConfigure ??= []).push(Date.now());
      },
    },
  },

  // 已配置（节点级生效目录）：正常文件列表视图（CT 宿主 request 为 noop 桩，不发网络）
  {
    id: 'DeployLogDrawer/Configured',
    component: DeployLogDrawer,
    props: {
      open: true,
      deployNodeId: 5,
      nodeName: 'node-a',
      logDir: '/var/log/star',
    },
  },

  // 关闭态（不渲染内容，占位回归）
  {
    id: 'DeployLogDrawer/Closed',
    component: DeployLogDrawer,
    props: { open: false, deployNodeId: 0, nodeName: '', logDir: '' },
  },
];
