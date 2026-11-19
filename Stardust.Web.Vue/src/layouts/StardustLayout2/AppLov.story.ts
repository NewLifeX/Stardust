import AppLov from './AppLov.vue';
import type { AppOption } from './ship.logic.ts';

const fakeOptions = async (key: string): Promise<AppOption[]> => {
  const all: AppOption[] = [
    { id: 3, name: 'StarServer', displayName: '服务端' },
    { id: 5, name: 'StarWeb', displayName: 'Web 端' },
    { id: 9, name: 'Stardust', displayName: '' },
    // 名字不含 star：验证关键字过滤真的按子串剔除（'star' 命中 3 条而非全部 4 条）
    { id: 12, name: 'Atlas', displayName: '看板' },
  ];
  const kw = key.trim().toLowerCase();
  return kw ? all.filter((o) => o.name.toLowerCase().includes(kw) || o.displayName.includes(key.trim())) : [];
};

// 选中回调透传：gallery 挂载走 h(component, props)，onSelect 函数 prop 接住 emit，
// 写到 window 供 CT 断言「选中回调带 id」（gallery 无 emit 渲染区）
function recordSelect(item: { value: string; id: number }): void {
  (window as unknown as { __lovLast?: { value: string; id: number } }).__lovLast = item;
}

export const stories = [
  // 测试状态：真实替身数据（三条应用），输入关键字出候选
  { id: 'AppLov/Default', component: AppLov, props: { fetchOptions: fakeOptions, onSelect: recordSelect } },

  // 测试状态：无候选（关键字不命中时不弹层）
  { id: 'AppLov/NoHit', component: AppLov, props: { fetchOptions: async () => [] as AppOption[], onSelect: recordSelect } },
];
