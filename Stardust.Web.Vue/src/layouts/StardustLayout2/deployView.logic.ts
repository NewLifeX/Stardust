// 发布页展示用的纯计算（无 Vue 依赖，便于单测）。
// 从 ShipPanel.vue 里移出，行为与原组件一致；各展示组件（DeployVersionBar、DeployNodes 等）共用。

/** 与 Tag.vue 的 tone 保持一致的子集 */
export type VersionTagTone = 'default' | 'ok' | 'warn';

export interface VersionTag {
  tone: VersionTagTone;
  label: string;
}

/** 部署集里这里只关心的两个字段 */
export interface DeployVersionInfo {
  version: string;
  multiVersion: boolean;
}

/** 节点里这里只关心的两个字段 */
export interface NodeVersionInfo {
  enable: boolean;
  version: string;
}

/**
 * 节点版本与部署集版本的比对标签。
 * 没有部署集或节点已停用时不显示；节点没上报版本一律提示（含部署集版本也为空的情况，不会判成一致）；
 * 开了多版本时版本不同属于允许，只用中性色。
 */
export function nodeVersionTag(deploy: DeployVersionInfo | null, node: NodeVersionInfo): VersionTag | null {
  if (!deploy || !node.enable) return null;
  if (node.version && node.version === deploy.version) return { tone: 'ok', label: '与部署集一致' };
  if (!node.version) return { tone: 'warn', label: '未上报版本' };
  return deploy.multiVersion
    ? { tone: 'default', label: '多版本，允许不同' }
    : { tone: 'warn', label: '与部署集不一致' };
}

export interface NodeStats {
  /** 启用的节点数 */
  total: number;
  /** 版本与部署集一致的启用节点数 */
  same: number;
  /** 不一致的启用节点数 */
  diff: number;
}

/** 统计启用节点里版本与部署集一致的个数；部署集版本为空时没有节点算一致 */
export function nodeStats(deployVersion: string, nodes: readonly NodeVersionInfo[]): NodeStats {
  const enabled = nodes.filter((n) => n.enable);
  const same = enabled.filter((n) => !!deployVersion && n.version === deployVersion).length;
  return { total: enabled.length, same, diff: enabled.length - same };
}