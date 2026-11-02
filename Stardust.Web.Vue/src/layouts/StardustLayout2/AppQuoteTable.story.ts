import AppQuoteTable from './AppQuoteTable.vue';
import type { AppQuoteRow } from './configCenter.logic.ts';

const rows: AppQuoteRow[] = [
  { id: 2, appId: 7, targetAppId: 9, kind: 1, enable: true, appName: 'OrderService', targetName: 'StarWeb' },
  { id: 3, appId: 7, targetAppId: 11, kind: 0, enable: false, appName: 'OrderService', targetName: 'MindHub' },
];

// 操作 emit 记录到 window（CT 断言用，AppRow/EditEmit 同款模式）
interface QuoteWindow {
  __appQuoteEvents?: string[];
}
const w = window as unknown as QuoteWindow;
w.__appQuoteEvents = [];

export const stories = [
  // 测试状态：两条依赖记录（一启用一停用），编辑/删除 emit 记录到 window
  {
    id: 'AppQuoteTable/Rows',
    component: AppQuoteTable,
    props: {
      rows,
      loading: false,
      error: '',
      removingId: null as number | null,
      busy: false,
      onCreate: () => {
        w.__appQuoteEvents!.push('create');
      },
      onEdit: (r: AppQuoteRow) => {
        w.__appQuoteEvents!.push(`edit:${r.id}`);
      },
      onRemove: (r: AppQuoteRow) => {
        w.__appQuoteEvents!.push(`remove:${r.id}`);
      },
    },
  },

  // 测试状态：空表（引导新增）
  { id: 'AppQuoteTable/Empty', component: AppQuoteTable, props: { rows: [], loading: false, error: '', removingId: null, busy: false } },

  // 测试状态：加载失败 + 重试按钮
  { id: 'AppQuoteTable/Failed', component: AppQuoteTable, props: { rows: [], loading: false, error: '加载失败', removingId: null, busy: false } },

  // 测试状态：删除中（行按钮禁用写「删除中…」，AGENTS.md §11）
  { id: 'AppQuoteTable/Removing', component: AppQuoteTable, props: { rows, loading: false, error: '', removingId: 2, busy: false } },
];
