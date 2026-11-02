// StardustLayout2 的界面开关：命令面板、生产确认（GuardModal）、底部控制台。
// 只放需要同步读写的状态（hotkeys.logic.ts 的 Esc 与 ⌘K 依赖它）。
// Drawer 与 GuardModal 都不走 useModal（输入-1027）；抽屉、接入向导的“是否打开”等接入页面时再定。
// 对照原型 kit-app.jsx：palette 是 {placeholder, items} | null，⌘K 只会打开、没有关闭分支；
// guard 是 {label, fn} | null，确认时先关弹窗再执行回调。
import { computed, reactive, shallowRef } from 'vue';
import type { PaletteGroup } from './palette.logic';

/** 命令面板的一次打开所需内容：⌘K 与 P/C/L/N 动词键只是传入不同的配置 */
export interface PaletteConfig {
  placeholder: string;
  groups: PaletteGroup[];
}

/** 一次生产确认：label 是弹窗标题，fn 在用户点“确认执行”后调用 */
export interface GuardRequest {
  label: string;
  fn: () => void | Promise<void>;
}

/** 底部控制台页签：日志 / 终端 */
export type DockTab = 'log' | 'term';

// 配置里带 run 函数，用 shallowRef，避免被深层代理
const paletteConfig = shallowRef<PaletteConfig | null>(null);
// 同样带函数，用 shallowRef
const guardRequest = shallowRef<GuardRequest | null>(null);
// 默认关闭、日志页签：原型 kit-app.jsx:39 useState({ open: false, tab: 'log' })
const dockState = reactive<{ open: boolean; tab: DockTab }>({ open: false, tab: 'log' });
// 接入向导 Drawer（A 键打开）：不走 useModal，与 Drawer 组件约定一致
const wizardState = shallowRef(false);

/** 当前面板配置，null 表示未打开（只读） */
export const palette = computed(() => paletteConfig.value);
/** 供 hotkeys 的 HotkeyState.palette 使用 */
export const paletteOpen = computed(() => paletteConfig.value !== null);
/** 当前生产确认，null 表示未打开（只读） */
export const guard = computed(() => guardRequest.value);
/** 供 hotkeys 的 HotkeyState.guard 使用 */
export const guardOpen = computed(() => guardRequest.value !== null);
export const dockOpen = computed(() => dockState.open);
export const dockTab = computed(() => dockState.tab);
/** 供 hotkeys 的 HotkeyState.wizard 使用 */
export const wizardOpen = computed(() => wizardState.value);

/** 打开面板；已打开时用新配置替换（原型：动词键会替换当前面板内容） */
export function openPalette(config: PaletteConfig): void {
  paletteConfig.value = config;
}

export function closePalette(): void {
  paletteConfig.value = null;
}

/** 打开生产确认；已打开时用新请求替换（原型 doGuard 就是 setGuard） */
export function openGuard(label: string, fn: GuardRequest['fn']): void {
  guardRequest.value = { label, fn };
}

/** 取消：遮罩、取消按钮、Esc 都走这里，不执行回调 */
export function cancelGuard(): void {
  guardRequest.value = null;
}

/** 确认：先关弹窗再执行回调（原型 kit-app.jsx:381），回调里再开新的确认也不会被误关 */
export function confirmGuard(): void {
  const current = guardRequest.value;
  if (!current) return;
  guardRequest.value = null;
  void current.fn();
}

/** 打开底部控制台；不传 tab 则保持当前页签 */
export function openDock(tab?: DockTab): void {
  if (tab) dockState.tab = tab;
  dockState.open = true;
}

export function closeDock(): void {
  dockState.open = false;
}

/** 反引号：呼出 / 收起底部控制台 */
export function toggleDock(): void {
  dockState.open = !dockState.open;
}

export function setDockTab(tab: DockTab): void {
  dockState.tab = tab;
}

/** 打开接入向导 Drawer */
export function openWizard(): void {
  wizardState.value = true;
}

/** 关闭接入向导 Drawer */
export function closeWizard(): void {
  wizardState.value = false;
}

/**
 * 终端节点请求：页面（节点页/此刻页）请求打开某节点的终端。
 * 页面组件拿不到 index.vue 里 useConsoleDock 的实例，经这个模块级通道把节点 id 发出来，
 * index.vue 监听后选节点并展开终端页签。每次都生成新对象（带 nonce），
 * 同一节点重复点击也能触发 watch。
 */
export interface TermNodeRequest {
  id: number | string;
  nonce: number;
}
const termNodeRequest = shallowRef<TermNodeRequest | null>(null);
/** 只读的终端请求（供 index.vue watch） */
export const termNodeReq = computed(() => termNodeRequest.value);
/** 页面调用：请求打开 id 节点的终端 */
export function requestTermNode(id: number | string): void {
  termNodeRequest.value = { id, nonce: Date.now() };
}

/** 仅供单测在用例之间复位 */
export function resetUiState(): void {
  paletteConfig.value = null;
  guardRequest.value = null;
  dockState.open = false;
  dockState.tab = 'log';
  wizardState.value = false;
}
