import DeployVersionList from './DeployVersionList.vue';
import type { VersionOption } from './ship.logic.ts';

function ver(id: number, patch: Partial<VersionOption> = {}): VersionOption {
  const version = patch.version ?? `v1.0.${id}`;
  return { id, version, disabled: false, reason: '', current: false, label: version, ...patch };
}

const THREE: VersionOption[] = [
  ver(3, { version: 'v1.4.2', current: true }),
  ver(2, { version: 'v1.4.1' }),
  ver(1, { version: 'v1.3.9', disabled: true, reason: '没有文件地址' }),
];

// 12 个版本，用来测「默认只显示前 8 个、可展开」
const TWELVE: VersionOption[] = Array.from({ length: 12 }, (_, i) => ver(12 - i, { version: `v1.5.${12 - i}`, current: i === 0 }));

const BASE = {
  versions: THREE,
  versionsFailed: false,
  notice: null,
  busyKey: '',
  uploading: false,
};

export const stories = [
  // 测试状态：当前、可应用、不可用三种行各一条，没有提示条
  { id: 'DeployVersionList/Default', component: DeployVersionList, props: { ...BASE } },

  // 测试状态：超过 8 个版本，默认只显示 8 行，底部有「显示全部 12 个版本」
  { id: 'DeployVersionList/Many', component: DeployVersionList, props: { ...BASE, versions: TWELVE } },

  // 测试状态：没有版本
  { id: 'DeployVersionList/Empty', component: DeployVersionList, props: { ...BASE, versions: [] } },

  // 测试状态：版本加载失败（不能显示成「没有版本」）
  { id: 'DeployVersionList/Failed', component: DeployVersionList, props: { ...BASE, versions: [], versionsFailed: true } },

  // 测试状态：正在应用 id=2 的版本：该行按钮写「切换中…」，所有按钮禁用
  { id: 'DeployVersionList/Busy', component: DeployVersionList, props: { ...BASE, busyKey: 'ver:2' } },

  // 测试状态：上传中（F05）：上传按钮禁用、图标旋转、文案「上传中…」，文件名在视线内（AGENTS.md §11）
  {
    id: 'DeployVersionList/Uploading',
    component: DeployVersionList,
    props: { ...BASE, uploading: true },
  },

  // 测试状态：提示条两种语气
  {
    id: 'DeployVersionList/NoticeInfo',
    component: DeployVersionList,
    props: { ...BASE, notice: { tone: 'info', text: '部署集未启用：应用后只切换生效版本号，不会下发到任何节点。' } },
  },
  {
    id: 'DeployVersionList/NoticeWarn',
    component: DeployVersionList,
    props: { ...BASE, notice: { tone: 'warn', text: '将向 4 个启用节点下发 install，节点会重启应用。' } },
  },
];
