// 底部控制台接线（组合函数）：终端节点选择与各节点缓冲区。
// 无生命周期、不读 ui.store；节点列表由调用方以函数传入，接节点接口时只改调用处。
// 打开 dock 并切到终端页签由调用方负责（openDock('term')），这里只管“选哪台节点”。
// 对照原型 kit-app.jsx 的 termNode / termBuf / runTerm / openTerm。

import { computed, ref, shallowRef } from 'vue';
import { bufferOf, clearBuffer, pickTermNode, submitCommand } from './dock.logic.ts';
import type { DockNode, NodeId, TermBuffers } from './dock.logic.ts';

export interface ConsoleDockDeps {
  /** 当前可见的节点列表（含离线节点，在线过滤由 pickTermNode 处理） */
  nodes: () => readonly DockNode[];
}

export function useConsoleDock(deps: ConsoleDockDeps) {
  // 只存“用户选了哪台”；生效节点每次现算，节点下线或列表刷新后不会残留离线选中项
  const picked = ref<NodeId | null>(null);
  const buffers = shallowRef<TermBuffers>({});

  const termNode = computed<NodeId | null>(() => pickTermNode(deps.nodes(), picked.value));
  const lines = computed(() => bufferOf(buffers.value, termNode.value));

  function pickNode(id: NodeId): void {
    picked.value = id;
  }

  /** 对应原型 openTerm：选中目标节点，返回实际生效的节点 id（目标离线时退到第一台在线节点，没有则 null） */
  function openTerm(id: NodeId | null): NodeId | null {
    picked.value = id;
    return termNode.value;
  }

  /** 没有生效节点时不提交，返回 false */
  function submit(raw: string): boolean {
    const id = termNode.value;
    if (id === null) return false;
    const r = submitCommand(buffers.value, id, raw);
    buffers.value = r.buffers;
    return r.accepted;
  }

  /** 只清当前节点的缓冲区；日志页签没有可清内容，由 ConsoleDock 隐藏“清空”按钮 */
  function clear(): void {
    const id = termNode.value;
    if (id === null) return;
    buffers.value = clearBuffer(buffers.value, id);
  }

  return { termNode, lines, pickNode, openTerm, submit, clear };
}
