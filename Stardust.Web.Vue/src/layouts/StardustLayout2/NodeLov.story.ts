import NodeLov from './NodeLov.vue';
import type { NodeOption } from './nodesPage.logic.ts';

// 替身联想数据：'web' 命中两条（web-1/web-2），'db' 命中一条，Atlas 不含任何常用关键字 → 过滤断言
const fakeOptions = async (key: string): Promise<NodeOption[]> => {
  const all: NodeOption[] = [
    { id: 1, value: 'web-1' },
    { id: 2, value: 'web-2' },
    { id: 3, value: 'db-1' },
    // 名字不含 web/db：验证关键字过滤真的按子串剔除（'web' 命中 2 条而非全部 4 条）
    { id: 9, value: 'atlas-node' },
  ];
  const kw = key.trim().toLowerCase();
  return kw ? all.filter((o) => o.value.toLowerCase().includes(kw)) : [];
};

// 选中回调透传：gallery 挂载走 h(component, props)，onSelect 函数 prop 接住 emit，
// 写到 window 供 CT 断言「选中回调带 id」（gallery 无 emit 渲染区）
function recordSelect(item: { value: string; id: number }): void {
  (window as unknown as { __lovLast?: { value: string; id: number } }).__lovLast = item;
}

export const stories = [
  // 测试状态：真实替身数据（四条节点），输入关键字出候选
  { id: 'NodeLov/Default', component: NodeLov, props: { fetchOptions: fakeOptions, onSelect: recordSelect } },

  // 测试状态：无候选（关键字不命中时不弹层）
  { id: 'NodeLov/NoHit', component: NodeLov, props: { fetchOptions: async () => [] as NodeOption[], onSelect: recordSelect } },
];
