// 告警组纯逻辑单测（CARD-MON-01）。
// 覆盖：toAlarmGroupRow 归一兜底、filterGroupRows 客户端过滤、validateAlarmGroupForm 必填、
// groupBody 形状（主键不入 body）、saveAlarmGroup PUT/POST 路由、deleteAlarmGroup 路由、
// executeAlarmTest 成功/失败三态（OK / 执行失败！+原因）、fetchProjectOptions 归一与失败。
import { describe, it, expect } from 'vitest';
import {
  deleteAlarmGroup,
  executeAlarmTest,
  emptyAlarmGroupForm,
  fetchAlarmGroups,
  fetchProjectOptions,
  filterGroupRows,
  groupBody,
  saveAlarmGroup,
  toAlarmGroupForm,
  toAlarmGroupRow,
  validateAlarmGroupForm,
  DISABLED_GROUP_TEST_TITLE,
  GROUP_EMPTY_TEXT,
  GROUP_EMPTY_CREATE_TEXT,
  GROUP_FILTER_EMPTY_TEXT,
  type AlarmGroupForm,
  type AlarmGroupRow,
} from './alarmGroup.logic';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/** 桩请求：按 url 返回预设 data 或抛错 */
function stubReq(map: Record<string, unknown> = {}, throwOn = false): Req {
  return async (opt) => {
    if (throwOn) throw new Error('stub 500');
    const url = String((opt as { url?: string }).url ?? '');
    if (url in map) return map[url] as never;
    return { data: [] };
  };
}

function captureReq(): { req: Req; calls: Array<{ url: string; method: string; params?: unknown; data?: unknown }> } {
  const calls: Array<{ url: string; method: string; params?: unknown; data?: unknown }> = [];
  const req: Req = async (opt) => {
    calls.push({ url: String(opt.url ?? ''), method: String(opt.method ?? ''), params: opt.params, data: (opt as ReqOptions & { data?: unknown }).data });
    return { data: [] };
  };
  return { req, calls };
}

describe('toAlarmGroupRow · 单行归一（防御性兜底）', () => {
  it('字段齐全：id/projectId/projectName/name/enable/webHook/content/创建更新五元组', () => {
    const row = toAlarmGroupRow({
      Id: 3, ProjectId: 1, ProjectName: '默认项目', Name: '值班告警', Enable: true,
      WebHook: 'https://oapi.dingtalk.com/robot/send?access_token=x', Content: '模板内容',
      CreateUser: 'admin', CreateTime: '2026-11-07 10:00:00', CreateIP: '127.0.0.1',
      UpdateUser: 'admin', UpdateTime: '2026-11-07 11:30:00', UpdateIP: '192.168.1.2',
    });
    expect(row.id).toBe(3);
    expect(row.projectId).toBe(1);
    expect(row.projectName).toBe('默认项目');
    expect(row.name).toBe('值班告警');
    expect(row.enable).toBe(true);
    expect(row.webHook).toContain('dingtalk');
    expect(row.content).toBe('模板内容');
    expect(row.createUser).toBe('admin');
    expect(row.createTime).toBe('2026-11-07 10:00');
    expect(row.createIp).toBe('127.0.0.1');
    expect(row.updateTime).toBe('2026-11-07 11:30');
    expect(row.updateIp).toBe('192.168.1.2');
  });
  it('缺 Name → 兜底「未命名告警组」；Enable 数字 0 → false；时间缺失 → —', () => {
    const row = toAlarmGroupRow({ Id: 5, Enable: 0 });
    expect(row.name).toBe('未命名告警组');
    expect(row.enable).toBe(false);
    expect(row.createTime).toBe('—');
    expect(row.updateTime).toBe('—');
  });
  it('Enable 缺失/字符串布尔归一', () => {
    expect(toAlarmGroupRow({ Id: 1, Enable: 'true' }).enable).toBe(true);
    expect(toAlarmGroupRow({ Id: 1 }).enable).toBe(false);
  });
});

describe('toAlarmGroupForm / emptyAlarmGroupForm · 表单初值', () => {
  it('编辑=行回填；兜底名清空为空串（避免把「未命名告警组」写回）', () => {
    const row: AlarmGroupRow = { ...toAlarmGroupRow({ Id: 2, Name: 'g1' }), name: 'g1' };
    const form = toAlarmGroupForm(row);
    expect(form.name).toBe('g1');
    expect(form.projectId).toBe(0);
  });
  it('空表单默认启用', () => {
    expect(emptyAlarmGroupForm()).toEqual({ name: '', projectId: 0, enable: true, webHook: '', content: '' });
  });
});

describe('filterGroupRows · 客户端过滤', () => {
  const rows: AlarmGroupRow[] = [
    toAlarmGroupRow({ Id: 1, Name: '值班告警', ProjectName: '默认项目', Enable: true }),
    toAlarmGroupRow({ Id: 2, Name: '节点巡检', ProjectName: 'MCP 测试', Enable: false }),
    toAlarmGroupRow({ Id: 3, Name: '发布通知', Enable: true }),
  ];
  it('q 匹配名称（不区分大小写）', () => {
    expect(filterGroupRows(rows, '值班', '')).toHaveLength(1);
    expect(filterGroupRows(rows, 'MCP', '')).toHaveLength(1);
  });
  it('q 匹配项目名', () => {
    expect(filterGroupRows(rows, '默认', '')).toHaveLength(1);
    expect(filterGroupRows(rows, '', '')).toHaveLength(3);
  });
  it('enable 过滤与 q 叠加', () => {
    expect(filterGroupRows(rows, '', 'true')).toHaveLength(2);
    expect(filterGroupRows(rows, '', 'false')).toHaveLength(1);
    expect(filterGroupRows(rows, '节点', 'false')).toHaveLength(1);
    expect(filterGroupRows(rows, '节点', 'true')).toHaveLength(0);
  });
});

describe('validateAlarmGroupForm · Name 必填（后端 ArgumentNullException 同语义）', () => {
  it('空/纯空格 → 报错文案；有值 → 空串', () => {
    expect(validateAlarmGroupForm({ name: '', projectId: 0, enable: true, webHook: '', content: '' })).toBe('名称不能为空');
    expect(validateAlarmGroupForm({ name: '  ', projectId: 0, enable: true, webHook: '', content: '' })).toBe('名称不能为空');
    expect(validateAlarmGroupForm({ name: 'g', projectId: 0, enable: true, webHook: '', content: '' })).toBe('');
  });
});

describe('groupBody / saveAlarmGroup · CRUD 路由与 body（主键不入 body）', () => {
  const form: AlarmGroupForm = { name: ' 值班告警 ', projectId: 2, enable: false, webHook: ' https://x ', content: 'c' };

  it('body：name/webHook trim、含 projectId/enable/content、不含 id', () => {
    const body = groupBody(form);
    expect(body).toEqual({ name: '值班告警', projectId: 2, enable: false, webHook: 'https://x', content: 'c' });
    expect('id' in body).toBe(false);
  });
  it('编辑 → PUT /Monitors/AlarmGroup/{id}，body 同形', async () => {
    const { req, calls } = captureReq();
    await saveAlarmGroup(req, form, 7);
    expect(calls[0].method).toBe('PUT');
    expect(calls[0].url).toBe('/Monitors/AlarmGroup/7');
    expect(calls[0].data).toEqual(groupBody(form));
  });
  it('新增 → POST /Monitors/AlarmGroup（无 id 后缀）', async () => {
    const { req, calls } = captureReq();
    await saveAlarmGroup(req, form);
    expect(calls[0].method).toBe('POST');
    expect(calls[0].url).toBe('/Monitors/AlarmGroup');
  });
  it('删除 → DELETE /Monitors/AlarmGroup/{id}', async () => {
    const { req, calls } = captureReq();
    await deleteAlarmGroup(req, 9);
    expect(calls[0]).toMatchObject({ method: 'DELETE', url: '/Monitors/AlarmGroup/9' });
  });
});

describe('executeAlarmTest · 测试发送三态（后端契约）', () => {
  it('成功：message 取后端（"OK"）', async () => {
    const req: Req = async () => ({ code: 0, message: 'OK' }) as never;
    const r = await executeAlarmTest(req, 1);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('OK');
  });
  it('成功但 message 空 → 兜底 "OK"', async () => {
    const req: Req = async () => ({ code: 0 }) as never;
    const r = await executeAlarmTest(req, 1);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('OK');
  });
  it('失败：message 取后端原文（"执行失败！"+原因）', async () => {
    const req: Req = async () => {
      throw new Error('执行失败！webhook 无效');
    };
    const r = await executeAlarmTest(req, 1);
    expect(r.ok).toBe(false);
    expect(r.message).toBe('执行失败！webhook 无效');
  });
  it('未启用组同样返回成功（后端 OK 不发送的契约，前端不区分）', async () => {
    const req: Req = async () => ({ code: 0, message: 'OK' }) as never;
    const r = await executeAlarmTest(req, 2);
    expect(r.ok).toBe(true);
    expect(r.message).toBe('OK');
  });
});

describe('fetchAlarmGroups / fetchProjectOptions · 取数（request 注入）', () => {
  it('列表成功：归一行、error 空', async () => {
    const req = stubReq({
      '/Monitors/AlarmGroup': { data: [{ Id: 1, Name: 'g', Enable: true }] },
    });
    const r = await fetchAlarmGroups(req);
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0].name).toBe('g');
  });
  it('列表失败：rows 空且 error 可见（不静默空态）', async () => {
    const r = await fetchAlarmGroups(stubReq({}, true));
    expect(r.rows).toEqual([]);
    expect(r.error).not.toBe('');
  });
  it('项目下拉：Id/Name 归一；Name 缺失兜底 #Id', async () => {
    const req = stubReq({
      '/Platform/GalaxyProject': { data: [{ Id: 4, Name: 'MCP 测试' }, { Id: 5 }] },
    });
    const r = await fetchProjectOptions(req, 'mcp');
    expect(r.error).toBe('');
    expect(r.options).toEqual([
      { id: 4, name: 'MCP 测试' },
      { id: 5, name: '#5' },
    ]);
  });
  it('项目下拉失败：options 空且 error 可见', async () => {
    const r = await fetchProjectOptions(stubReq({}, true));
    expect(r.options).toEqual([]);
    expect(r.error).not.toBe('');
  });
});

describe('逐字文案常量（卡片/旅程断言源）', () => {
  it('停用组 title、空态引导、过滤空态', () => {
    expect(DISABLED_GROUP_TEST_TITLE).toBe('未启用的组不会真实发送（后端直接返回 OK）');
    expect(GROUP_EMPTY_TEXT).toBe('还没有告警组');
    expect(GROUP_EMPTY_CREATE_TEXT).toBe('创建告警组');
    expect(GROUP_FILTER_EMPTY_TEXT).toBe('暂无告警组记录');
  });
});
