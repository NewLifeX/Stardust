// galaxyProject.logic 纯函数单测：归一、校验、请求体、筛选排序、删除后上下文回退。
import { describe, expect, it } from 'vitest';
import {
  buildCreatePayload,
  buildUpdatePayload,
  contextAfterDelete,
  emptyProjectForm,
  filterProjectRows,
  fmtTime,
  projectToForm,
  toMs,
  toProjectRows,
  validateProjectName,
} from './galaxyProject.logic.ts';

describe('toProjectRows 归一', () => {
  it('驼峰/帕斯卡双取，TenantName/ManagerName 缺省为空串，Sort 降序', () => {
    const rows = toProjectRows([
      { id: 2, name: 'b', enable: false, isGlobal: true, tenantId: 3, tenantName: '租户B', managerId: 0, nodes: '5', apps: 12, sort: 1, CreateTime: '2026-01-02 03:04:05' },
      { Id: 1, Name: 'a', Enable: 1, IsGlobal: 0, TenantId: 0, ManagerId: 9, ManagerName: '张三', Nodes: 0, Apps: 0, Sort: 10, CreateTime: '2026-01-01 00:00:00' },
    ]);
    expect(rows.map((r) => r.id)).toEqual([1, 2]); // Sort 降序：10 > 1
    const first = rows[0];
    expect(first.name).toBe('a');
    expect(first.enable).toBe(true);
    expect(first.isGlobal).toBe(false);
    expect(first.managerName).toBe('张三');
    expect(first.nodes).toBe(0);
    expect(first.createdText).toBe('2026-01-01 00:00');
    expect(rows[1].tenantName).toBe('租户B');
    expect(rows[1].enable).toBe(false);
    expect(rows[1].nodes).toBe(5);
  });

  it('无名行退化 #Id，坏时间显示 —', () => {
    const rows = toProjectRows([{ id: 7, name: '', CreateTime: 'not-a-date' }]);
    expect(rows[0].name).toBe('#7');
    expect(rows[0].createdText).toBe('—');
    expect(rows[0].createdMs).toBe(0);
  });
});

describe('toMs / fmtTime', () => {
  it('空格日期与 ISO 均可解析；空串/坏值归 0', () => {
    expect(toMs('2026-01-02 03:04:05')).toBe(Date.parse('2026-01-02T03:04:05'));
    expect(toMs('2026-01-02T03:04:05')).toBe(Date.parse('2026-01-02T03:04:05'));
    expect(toMs('')).toBe(0);
    expect(toMs('x')).toBe(0);
    expect(toMs('2026-01-0203:04:05')).toBe(0);
  });
  it('fmtTime 0 → —', () => {
    expect(fmtTime(0)).toBe('—');
  });
});

describe('validateProjectName', () => {
  it('空/纯空白 → 旧壳逐字文案；有名 → 空串', () => {
    expect(validateProjectName('')).toBe('请输入项目名称');
    expect(validateProjectName('   ')).toBe('请输入项目名称');
    expect(validateProjectName(' demo ')).toBe('');
  });
});

describe('请求体构建', () => {
  it('POST 不带 Id；数字字段归一非负整数；字符串 trim', () => {
    const payload = buildCreatePayload({ ...emptyProjectForm(), name: ' demo ', tenantId: -3, sort: 1.9, whiteIPs: ' 10.0.0.1 ' });
    expect(Object.keys(payload)).not.toContain('id');
    expect(payload.name).toBe('demo');
    expect(payload.tenantId).toBe(0);
    expect(payload.sort).toBe(1);
    expect(payload.whiteIPs).toBe('10.0.0.1');
    expect(payload.enable).toBe(true);
    expect(payload.isGlobal).toBe(false);
  });
  it('PUT 与 POST 等价（整包，Id 在路径）', () => {
    const m = { ...emptyProjectForm(), name: 'x' };
    expect(buildUpdatePayload(m)).toEqual(buildCreatePayload(m));
  });
  it('projectToForm：#Id 兜底名回显为空串，其余字段原样', () => {
    const [row] = toProjectRows([{ id: 7, name: 'p', Enable: false, IsGlobal: true, TenantId: 2, Sort: 4, WhiteIPs: 'a', Remark: 'r' }]);
    const form = projectToForm(row);
    expect(form.name).toBe('p');
    expect(form.enable).toBe(false);
    expect(form.isGlobal).toBe(true);
    expect(form.sort).toBe(4);
    expect(form.remark).toBe('r');
    const [anon] = toProjectRows([{ id: 8, name: '' }]);
    expect(projectToForm(anon).name).toBe('');
  });
});

describe('filterProjectRows', () => {
  const ROWS = toProjectRows([
    { id: 1, name: 'alpha', TenantName: '租户A', ManagerName: '张三', Sort: 5, CreateTime: '2026-02-01 10:00:00' },
    { id: 2, name: 'beta', TenantName: '租户B', ManagerName: '李四', Sort: 9, CreateTime: '2026-03-10 10:00:00' },
    { id: 3, name: 'gamma', TenantName: '', ManagerName: '', Sort: 5, CreateTime: '2026-03-11 23:00:00' },
  ]);

  it('关键字命中名称/租户/管理者，大小写不敏感；空条件不过滤', () => {
    expect(filterProjectRows(ROWS, { keyword: 'ALP', dtStart: '', dtEnd: '' }).map((r) => r.id)).toEqual([1]);
    expect(filterProjectRows(ROWS, { keyword: '租户b', dtStart: '', dtEnd: '' }).map((r) => r.id)).toEqual([2]);
    expect(filterProjectRows(ROWS, { keyword: '李四', dtStart: '', dtEnd: '' }).map((r) => r.id)).toEqual([2]);
    expect(filterProjectRows(ROWS, { keyword: '', dtStart: '', dtEnd: '' }).length).toBe(3);
  });

  it('时间区间含首尾当日；无创建时间的行在限定区间时被排除', () => {
    expect(filterProjectRows(ROWS, { keyword: '', dtStart: '2026-03-10', dtEnd: '2026-03-10' }).map((r) => r.id)).toEqual([2]);
    expect(filterProjectRows(ROWS, { keyword: '', dtStart: '2026-02-01', dtEnd: '' }).length).toBe(3);
    const noTime = toProjectRows([{ id: 9, name: 'x', Sort: 1 }]);
    expect(filterProjectRows(noTime, { keyword: '', dtStart: '2026-01-01', dtEnd: '' })).toEqual([]);
  });

  it('默认排序 Sort 降序保持（9 在前）', () => {
    expect(filterProjectRows(ROWS, { keyword: '', dtStart: '', dtEnd: '' })[0].id).toBe(2);
  });
});

describe('contextAfterDelete 删除当前项目回退', () => {
  it('删除当前项目 → 切回全部项目 + 联动 toast（定稿逐字）', () => {
    const r = contextAfterDelete(7, 7, 'prod');
    expect(r.switched).toBe(true);
    expect(r.toast).toBe('已删除当前项目，已切回「全部项目」');
  });
  it('删除其它项目 / 全部项目上下文 → 不切换', () => {
    expect(contextAfterDelete(7, 8, 'prod')).toEqual({ switched: false, toast: '' });
    expect(contextAfterDelete(7, null, '')).toEqual({ switched: false, toast: '' });
  });
});
