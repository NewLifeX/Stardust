import AppQuoteDrawer from './AppQuoteDrawer.vue';
import type { AppQuoteForm } from './configCenter.logic.ts';

const APPS = [
  { id: 7, label: 'OrderService' },
  { id: 9, label: 'StarWeb' },
  { id: 11, label: 'MindHub' },
];

const editing: AppQuoteForm = { appId: 7, targetAppId: 9, kind: 1, enable: true };

interface QuoteWindow {
  __appQuoteEvents?: string[];
}
const w = window as unknown as QuoteWindow;
w.__appQuoteEvents = [];

export const stories = [
  // F15 新增依赖：空表单（两个应用下拉为 0）
  {
    id: 'AppQuoteDrawer/Create',
    component: AppQuoteDrawer,
    props: {
      open: true,
      apps: APPS,
      initial: null,
      title: '新增应用依赖',
      sub: '声明「依赖方 → 被依赖方」的引用关系',
      busy: false,
      onSubmit: (f: AppQuoteForm) => {
        w.__appQuoteEvents!.push(`submit:${f.appId}>${f.targetAppId}@${f.kind}:${f.enable}`);
      },
    },
  },

  // F15 编辑依赖：初值回填
  {
    id: 'AppQuoteDrawer/Edit',
    component: AppQuoteDrawer,
    props: {
      open: true,
      apps: APPS,
      initial: editing,
      title: '编辑应用依赖',
      sub: '调整依赖关系后立即生效',
      busy: false,
      onSubmit: (f: AppQuoteForm) => {
        w.__appQuoteEvents!.push(`submit:${f.appId}>${f.targetAppId}@${f.kind}:${f.enable}`);
      },
    },
  },

  // F15 保存中（AGENTS.md §11）
  {
    id: 'AppQuoteDrawer/Busy',
    component: AppQuoteDrawer,
    props: { open: true, apps: APPS, initial: editing, title: '编辑应用依赖', sub: '', busy: true },
  },
];
