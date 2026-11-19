import ConfigHistoryDiffDrawer from './ConfigHistoryDiffDrawer.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** Diff 接口桩：返回 canned data；fail=true 时抛错（失败态） */
function stubDiff(payload: unknown, fail = false): Req {
  return async () => {
    if (fail) throw new Error('stub 500');
    return payload as Awaited<ReturnType<Req>>;
  };
}

const history = { id: 21, action: 'Publish', success: true, version: 2, time: '2026-11-09 18:00' };

export const stories = [
  // 典型差异：Enable/NextVersion/Version 三键变更（变更归类，warning 色）
  {
    id: 'ConfigHistoryDiffDrawer/Diff',
    component: ConfigHistoryDiffDrawer,
    props: {
      open: true,
      history,
      deps: {
        request: stubDiff({
          data: {
            id: 21,
            configId: 10,
            newJson: '{"Id":10,"Name":"demo","Version":2,"NextVersion":3,"Enable":false}',
            oldJson: '{"Id":10,"Name":"demo","Version":1,"NextVersion":2,"Enable":true}',
            changedKeys: ['Enable', 'NextVersion', 'Version'],
          },
        }),
      },
    },
  },

  // 首次发布：oldJson=null，全量字段（added 归类 + 「首次发布」提示条）
  {
    id: 'ConfigHistoryDiffDrawer/DiffFirst',
    component: ConfigHistoryDiffDrawer,
    props: {
      open: true,
      history: { ...history, id: 20, version: 1, time: '2026-10-20 09:30' },
      deps: {
        request: stubDiff({
          data: {
            id: 20,
            configId: 10,
            newJson: '{"Id":10,"Name":"demo","Version":1,"Enable":true}',
            oldJson: null,
            changedKeys: ['Enable', 'Version'],
          },
        }),
      },
    },
  },

  // 无差异：changedKeys 空数组 → 「两个版本快照无差异」空态
  {
    id: 'ConfigHistoryDiffDrawer/DiffClean',
    component: ConfigHistoryDiffDrawer,
    props: {
      open: true,
      history,
      deps: { request: stubDiff({ data: { id: 21, configId: 10, newJson: '{"Version":1}', oldJson: '{"Version":1}', changedKeys: [] } }) },
    },
  },

  // 失败态：请求抛错 → 「对比加载失败」
  {
    id: 'ConfigHistoryDiffDrawer/DiffFail',
    component: ConfigHistoryDiffDrawer,
    props: { open: true, history, deps: { request: stubDiff(null, true) } },
  },
];
