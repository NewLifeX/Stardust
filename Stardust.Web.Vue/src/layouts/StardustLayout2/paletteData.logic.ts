// 命令面板的数据来源（纯逻辑，无 Vue 依赖，request 由调用方注入，便于单测）。
// 复用应用页与节点页已有的取数方式与模型函数，不新增接口：
//  - /Registry/App        应用列表（toAggRows 派生实例数与在线状态）
//  - /Registry/AppOnline  在线实例（应用实例数与节点在线集合共用这一份）
//  - /Nodes/Node          节点列表（mapNodes 归一字段与状态）
// 没有数据时返回空数组，不造演示数据；失败的部分记在 failed，由调用方决定怎么提示。
import { toAggRows, type AppListItem, type Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  buildOnlineIndex,
  mapNodes,
  type AppOnlineRow,
  type NodeRow,
} from '../../../apps/stardust/src/views/Stardust/nodes.model.ts';
import { buildVerbAction, type PaletteApp, type PaletteNode, type VerbAction } from './palette.items.ts';
import type { Verb } from './hotkeys.logic.ts';
import type { ToastTone } from './toast.logic.ts';

export type PaletteDataPart = 'apps' | 'nodes';

export interface PaletteData {
  apps: PaletteApp[];
  nodes: PaletteNode[];
  /** 取数失败的部分；在线实例请求失败时两部分都算失败（避免与节点页的在线判断不一致） */
  failed: PaletteDataPart[];
}

function rowsOf<T>(res: { data?: unknown } | undefined): T[] {
  const data = res?.data;
  return Array.isArray(data) ? (data as T[]) : [];
}

/** 应用行 → 面板条目：在线 = 已启用且有在线实例（toAggRows 的 status 不为 offline） */
export function buildPaletteApps(rows: AppListItem[], online: AppOnlineRow[]): PaletteApp[] {
  const counts = new Map<string, number>();
  for (const r of online) if (r.appName) counts.set(r.appName, (counts.get(r.appName) ?? 0) + 1);

  const result: PaletteApp[] = [];
  for (const r of toAggRows(rows, counts)) {
    if (!r.name || r.id === undefined) continue;
    result.push({
      id: r.id,
      name: r.name,
      category: r.category,
      version: r.version,
      instances: r.instances,
      online: r.status !== 'offline',
    });
  }
  return result;
}

/** 节点行 → 面板条目：在线 = 状态不是 offline（异常也算在线，可以连终端排查） */
export function buildPaletteNodes(rows: NodeRow[], online: AppOnlineRow[], now: number): PaletteNode[] {
  const { onlineNames, counts } = buildOnlineIndex(online);
  return mapNodes(rows, onlineNames, counts, now)
    .filter((v) => v.name !== '-')
    .map((v) => ({
      id: v.id,
      name: v.name,
      ip: v.ip === '-' ? undefined : v.ip,
      online: v.status !== 'offline',
    }));
}

/** 并行拉取三个接口；在线实例请求失败时应用与节点都算失败，其余请求互不影响 */
export async function fetchPaletteData(request: Req, now: number = Date.now()): Promise<PaletteData> {
  const [appRes, onlineRes, nodeRes] = await Promise.allSettled([
    request({ url: '/Registry/App', method: 'GET', params: { PageSize: 200 } }),
    request({ url: '/Registry/AppOnline', method: 'GET', params: { PageSize: 500 } }),
    request({ url: '/Nodes/Node', method: 'GET', params: { PageSize: 200 } }),
  ]);

  const onlineOk = onlineRes.status === 'fulfilled';
  const online = onlineRes.status === 'fulfilled' ? rowsOf<AppOnlineRow>(onlineRes.value) : [];

  const appsOk = appRes.status === 'fulfilled' && onlineOk;
  const nodesOk = nodeRes.status === 'fulfilled' && onlineOk;

  const failed: PaletteDataPart[] = [];
  if (!appsOk) failed.push('apps');
  if (!nodesOk) failed.push('nodes');

  return {
    apps: appRes.status === 'fulfilled' && onlineOk ? buildPaletteApps(rowsOf<AppListItem>(appRes.value), online) : [],
    nodes: nodeRes.status === 'fulfilled' && onlineOk ? buildPaletteNodes(rowsOf<NodeRow>(nodeRes.value), online, now) : [],
    failed,
  };
}

/** 动词键的结果：打开面板、启动向导，或一条提示（失败与空结果都要说明，按键不静默） */
export type VerbOutcome = VerbAction | { type: 'toast'; message: string; tone: ToastTone };

export interface VerbDeps {
  go: (path: string) => void;
  /** 打开某节点的终端；不传则 N 键提示「待接入」 */
  openTerm?: (id: PaletteNode['id']) => void;
  /** 启动接入向导；不传则 A 键提示「待接入」 */
  startWizard?: () => void;
}

/** P/C/L/N/A 的处理：先看取数是否失败，再看有没有可选项，都没有才交给 buildVerbAction */
export function resolveVerb(verb: Verb, data: PaletteData, deps: VerbDeps): VerbOutcome {
  if (verb === 'A') {
    return deps.startWizard ? { type: 'wizard' } : { type: 'toast', message: '接入向导待接入', tone: 'warn' };
  }

  const isNode = verb === 'N';
  const part: PaletteDataPart = isNode ? 'nodes' : 'apps';
  const label = isNode ? '节点' : '应用';

  if (data.failed.includes(part)) return { type: 'toast', message: `${label}列表加载失败`, tone: 'err' };
  if (isNode && !deps.openTerm) return { type: 'toast', message: '终端节点选择待接入', tone: 'warn' };

  const action = buildVerbAction(verb, {
    go: deps.go,
    apps: data.apps,
    nodes: data.nodes,
    openTerm: deps.openTerm,
    startWizard: deps.startWizard,
  });
  return action ?? { type: 'toast', message: `暂无在线${label}`, tone: 'warn' };
}
