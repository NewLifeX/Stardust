import AppInstances from './AppInstances.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import type { InstanceRow } from './appInstances.logic.ts';

const THREE = {
  data: [
    { Id: 11, NodeId: 7, NodeName: 'node-a', IP: '10.0.0.1', Client: '10.0.0.1@1234', Version: '1.2.0', LongLink: true },
    { Id: 12, NodeId: 8, NodeName: 'node-b', IP: '10.0.0.2', Client: '10.0.0.2@5678', Version: '1.2.0', LongLink: true },
    { Id: 13, NodeId: 9, NodeName: 'node-c', IP: '10.0.0.3', Client: '10.0.0.3@9012', Version: '1.1.0', LongLink: false },
  ],
};

function stubReq(throwOn = false): Req {
  return async () => {
    if (throwOn) throw new Error('stub 500');
    return THREE;
  };
}

function stubReqEmpty(): Req {
  return async () => ({ data: [] });
}

function recorderReq(): Req {
  return async () => THREE;
}

export const stories = [
  // 三个在线实例：节点名+mono IP，CPU/内存列待接入 Tag，操作列 hover 浮出重启/终端
  { id: 'AppInstances/ThreeRows', component: AppInstances, props: { appId: 32, deps: { request: stubReq() } } },

  // 空态：整行「暂无在线实例」
  { id: 'AppInstances/Empty', component: AppInstances, props: { appId: 32, deps: { request: stubReqEmpty() } } },

  // 失败态：请求抛错，显示加载失败而不是空表
  { id: 'AppInstances/Failed', component: AppInstances, props: { appId: 32, deps: { request: stubReq(true) } } },

  // 事件录制：行点击发 select、重启/终端按钮发对应事件，进 window.__instEvents（CT 断言用）
  // gallery 只消费 props，事件监听器走 Vue 的 onXxx 约定（onSelect/onRestart/onTerminal）
  {
    id: 'AppInstances/EmitRecorder',
    component: AppInstances,
    props: {
      appId: 32,
      deps: { request: recorderReq() },
      onSelect: (row: InstanceRow) => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents?.push(['select', row.id]),
      onRestart: (row: InstanceRow) => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents?.push(['restart', row.id]),
      onTerminal: (row: InstanceRow) => (window as unknown as { __instEvents?: Array<[string, number]> }).__instEvents?.push(['terminal', row.id]),
    },
  },
];
