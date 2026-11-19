import TraceItemDrawer from './TraceItemDrawer.vue';
import type { TraceItemEditModel } from './traceItem.logic.ts';

const editInitial: TraceItemEditModel = {
  appId: 32,
  name: 'GET /api/user',
  kind: 'Http',
  displayName: '用户接口',
  enable: true,
  cloned: false,
  timeout: 3,
  rules: 'name=*/api/user*',
};

const createInitial: TraceItemEditModel = {
  appId: 32,
  name: '',
  kind: '',
  displayName: '',
  enable: true,
  cloned: false,
  timeout: 0,
  rules: '',
};

const kinds = [
  { value: 'Http', label: 'Http远程请求' },
  { value: 'Sql', label: 'Sql执行' },
  { value: 'Redis', label: 'Redis缓存' },
];

export const stories = [
  { id: 'TraceItemDrawer/Edit', component: TraceItemDrawer, props: { open: true, title: '编辑跟踪项 · GET /api/user', sub: 'IoC容器 · #32', initial: editInitial, kinds, kindsLoading: false, busy: false, error: '' } },
  { id: 'TraceItemDrawer/Create', component: TraceItemDrawer, props: { open: true, title: '新增跟踪项', sub: 'IoC容器 · #32', initial: createInitial, kinds, kindsLoading: false, busy: false, error: '' } },
  { id: 'TraceItemDrawer/Busy', component: TraceItemDrawer, props: { open: true, title: '编辑跟踪项 · GET /api/user', sub: 'IoC容器 · #32', initial: editInitial, kinds, kindsLoading: true, busy: true, error: '' } },
];
