import AppConfigTable from './AppConfigTable.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 按 URL 返回 canned 响应的桩 request；throwOn 时全部请求抛错（用于失败态） */
function stubReq(throwOn = false): Req {
  return async (opt) => {
    if (throwOn) throw new Error('stub 500');
    const url = String(opt.url ?? '');
    if (url === '/Configs/AppConfig') {
      return {
        data: [{ Id: 9, AppId: 32, Name: 'demo', Version: 1, NextVersion: 2, Enable: true }],
      };
    }
    if (url === '/Configs/ConfigData') {
      return {
        data: [
          { Id: 1, Key: 'App.Trace.Enable', Scope: '', Value: 'true', NewValue: '', Version: 1, NewVersion: 1 },
          { Id: 2, Key: 'Log.Level', Scope: '', Value: 'Info', NewValue: 'Warn', Version: 1, NewVersion: 2 },
          { Id: 3, Key: 'Timeout.Http', Scope: '', Value: '', NewValue: '3000', Version: 0, NewVersion: 2 },
          { Id: 4, Key: 'Old.Key', Scope: '', Value: '1', NewVersion: 2, NewStatus: '[[Deleted]]' },
        ],
      };
    }
    if (url === '/Configs/ConfigHistory') {
      return {
        data: [
          { Id: 21, Action: 'Publish', Success: true, Remark: '{"Version":1,"NextVersion":2}', CreateTime: '2026-10-28T09:30:00' },
          { Id: 20, Action: 'Publish', Success: true, Remark: '{"NextVersion":1}', CreateTime: '2026-10-20T09:30:00' },
        ],
      };
    }
    if (url === '/Registry/AppOnline') return { data: [{ Id: 101 }, { Id: 102 }, { Id: 103 }] };
    return { data: [] };
  };
}

function stubReqClean(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (url === '/Configs/AppConfig') {
      return { data: [{ Id: 9, AppId: 32, Name: 'demo', Version: 1, NextVersion: 1, Enable: true }] };
    }
    if (url === '/Configs/ConfigData') {
      return { data: [{ Id: 1, Key: 'App.Trace.Enable', Value: 'true', NewValue: '', Version: 1, NewVersion: 1 }] };
    }
    if (url === '/Configs/ConfigHistory') {
      return { data: [{ Id: 20, Action: 'Publish', Success: true, Remark: '{"NextVersion":1}', CreateTime: '2026-10-20T09:30:00' }] };
    }
    if (url === '/Registry/AppOnline') return { data: [{ Id: 101 }] };
    return { data: [] };
  };
}

function stubReqEmpty(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (url === '/Configs/AppConfig') {
      return { data: [{ Id: 9, AppId: 32, Name: 'demo', Version: 0, NextVersion: 0, Enable: true }] };
    }
    return { data: [] };
  };
}

export const stories = [
  // 有草稿：已修改/新增/待删除三行待发布，待发布浮条显示「2 项待发布」，右栏两条历史、当前 v1
  { id: 'AppConfigTable/Pending', component: AppConfigTable, props: { appId: 32, deps: { request: stubReq() } } },

  // 干净态：没有草稿（Version=NextVersion），不显示待发布浮条，行都是已发布
  { id: 'AppConfigTable/Clean', component: AppConfigTable, props: { appId: 32, deps: { request: stubReqClean() } } },

  // 空态：配置存在但没有配置项、没有历史
  { id: 'AppConfigTable/Empty', component: AppConfigTable, props: { appId: 32, deps: { request: stubReqEmpty() } } },

  // 失败态：请求抛错，显示加载失败而不是空表
  { id: 'AppConfigTable/Failed', component: AppConfigTable, props: { appId: 32, deps: { request: stubReq(true) } } },
];
