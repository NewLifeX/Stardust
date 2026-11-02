import BuildNodes from './BuildNodes.vue';
import type { BuildNodeRow } from './buildNodes.logic.ts';

const ROWS: BuildNodeRow[] = [
  {
    id: 21, deployId: 7, nodeId: 11, nodeName: 'node-build-a', nodeIp: '10.0.0.11',
    enable: true, sourcePath: '/src/MindHub.Api', pullCode: true, buildProject: true,
    packageOutput: true, uploadPackage: true, outputPath: './publish',
    updateTime: '2026-10-30T09:30:00', remark: '',
  },
  {
    id: 22, deployId: 7, nodeId: 12, nodeName: 'node-build-b', nodeIp: '10.0.0.12',
    enable: true, sourcePath: '/src/Stardust', pullCode: false, buildProject: true,
    packageOutput: true, uploadPackage: true, outputPath: '',
    updateTime: '2026-10-29T18:12:00', remark: 'x64',
  },
  {
    id: 23, deployId: 7, nodeId: 13, nodeName: '', nodeIp: '',
    enable: false, sourcePath: '', pullCode: false, buildProject: false,
    packageOutput: false, uploadPackage: false, outputPath: '',
    updateTime: '', remark: '',
  },
];

const BASE = {
  rows: ROWS,
  failed: false,
  busyKey: '',
};

export const stories = [
  // 正常：三行编译节点（全流程开、不拉代码、已停用且无名字）
  { id: 'BuildNodes/Default', component: BuildNodes, props: { ...BASE } },

  // 处理中：在 node-build-a（id=21）上执行「编译上传」，该按钮写「处理中…」，所有按钮禁用
  { id: 'BuildNodes/Busy', component: BuildNodes, props: { ...BASE, busyKey: '21:Build-Upload' } },

  // 空态：该部署集还没有配置编译节点
  { id: 'BuildNodes/Empty', component: BuildNodes, props: { ...BASE, rows: [] } },

  // 失败态：编译节点加载失败
  { id: 'BuildNodes/Failed', component: BuildNodes, props: { ...BASE, rows: [], failed: true } },
];
