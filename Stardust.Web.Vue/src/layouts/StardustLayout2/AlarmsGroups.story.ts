import AlarmsGroups from './AlarmsGroups.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/**
 * AlarmsGroups story（CARD-MON-01）。请求桩按 url 分流：
 *  - GET  /Monitors/AlarmGroup        → 组列表（PascalCase 行，同时验证双取映射）
 *  - DELETE /Monitors/AlarmGroup/{id} → 从内存数组删除（有状态：CT 验证「确认后行消失」）
 *  - GET  /Platform/GalaxyProject     → 项目下拉选项（新增/编辑抽屉打开时拉取）
 * 每次测试 page.goto 重新加载 gallery 页，模块级数组随之重置，用例间无共享状态。
 */
const GROUPS: Array<Record<string, unknown>> = [
  {
    Id: 1, ProjectId: 3, ProjectName: 'IoC平台', Name: '值班告警', Enable: true,
    WebHook: 'https://oapi.dingtalk.com/robot/send?access_token=atk',
    Content: '{{name}} 于 {{time}} 报警，请值班处理',
    CreateUser: 'admin', CreateTime: '2026-11-06 09:00:00', CreateIP: '127.0.0.1',
    UpdateUser: 'admin', UpdateTime: '2026-11-07 10:30:00', UpdateIP: '192.168.1.2',
  },
  {
    Id: 2, ProjectId: 0, ProjectName: '', Name: '停用组', Enable: false,
    WebHook: '', Content: '',
    CreateUser: 'admin', CreateTime: '2026-11-06 09:10:00', CreateIP: '127.0.0.1',
    UpdateUser: 'admin', UpdateTime: '2026-11-06 18:00:00', UpdateIP: '127.0.0.1',
  },
];

const PROJECTS = [
  { Id: 3, Name: 'IoC平台' },
  { Id: 5, Name: '星尘平台' },
];

function liveReq(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (url.includes('/Platform/GalaxyProject')) return { data: PROJECTS };
    if (url.includes('/Monitors/AlarmGroup')) {
      if (String(opt.method ?? '').toUpperCase() === 'DELETE') {
        const id = Number(url.split('/').pop());
        const i = GROUPS.findIndex((g) => g.Id === id);
        if (i >= 0) GROUPS.splice(i, 1);
        return { data: true };
      }
      return { data: GROUPS };
    }
    return { data: [] };
  };
}

function emptyReq(): Req {
  return async (opt) => {
    const url = String(opt.url ?? '');
    if (url.includes('/Platform/GalaxyProject')) return { data: PROJECTS };
    return { data: [] };
  };
}

function failReq(): Req {
  return async () => {
    throw new Error('stub 500');
  };
}

export const stories = [
  { id: 'AlarmsGroups/Data', component: AlarmsGroups, props: { deps: { request: liveReq() } } },
  { id: 'AlarmsGroups/Empty', component: AlarmsGroups, props: { deps: { request: emptyReq() } } },
  { id: 'AlarmsGroups/Failed', component: AlarmsGroups, props: { deps: { request: failReq() } } },
  {
    // 点击录制：点行「告警历史」把组 Id 追加进 window.__alarmsGroupHistory（上抛事件断言）
    id: 'AlarmsGroups/Recorder',
    component: AlarmsGroups,
    props: {
      deps: { request: liveReq() },
      onHistory: (row: { id: number }) => {
        const w = window as unknown as { __alarmsGroupHistory?: number[] };
        (w.__alarmsGroupHistory ??= []).push(row.id);
      },
    },
  },
];
