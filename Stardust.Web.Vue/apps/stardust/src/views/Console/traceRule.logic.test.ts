// 跟踪规则纯逻辑单测（CARD-MON-13，来源旅程 J-MON-15）。
// 覆盖：toTraceRuleRow 归一兜底、emptyTraceRuleForm/toTraceRuleForm 表单初值、validateTraceRuleForm
// 必填与优先级整数校验、ruleBody 形状（主键不入 body）、fetchTraceRules 取数与「假筛选硬条款」锁定
// （只允许 PageSize，不得携带 isWhite/isRegex/enable——控制器 Search :45-53 未消费三参数）、
// saveTraceRule PUT/POST 路由、deleteTraceRule 路由、filterTraceRuleRows 客户端过滤、文案常量。
import { describe, it, expect } from 'vitest';
import {
  deleteTraceRule,
  emptyTraceRuleForm,
  fetchTraceRules,
  filterTraceRuleRows,
  listLabel,
  modeLabel,
  ruleBody,
  saveTraceRule,
  toTraceRuleForm,
  toTraceRuleRow,
  validateTraceRuleForm,
  TRACE_RULE_EMPTY_TEXT,
  TRACE_RULE_EMPTY_CREATE_TEXT,
  TRACE_RULE_FILTER_EMPTY_TEXT,
  type TraceRuleRow,
} from './traceRule.logic';
import type { Req, ReqOptions } from '../../../src/views/Stardust/appsModel.ts';

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

describe('toTraceRuleRow · 单行归一（防御性兜底）', () => {
  it('字段齐全：id/rule/enable/isWhite/isRegex/priority/remark/创建更新五元组', () => {
    const row = toTraceRuleRow({
      Id: 3, Rule: '^/Admin/[A-Z]\\w+', Enable: true, IsWhite: true, IsRegex: true, Priority: 100,
      Remark: '后台白名单',
      CreateUser: 'admin', CreateTime: '2026-11-09 10:00:00', CreateIP: '127.0.0.1',
      UpdateUser: 'admin', UpdateTime: '2026-11-09 11:30:00', UpdateIP: '192.168.1.2',
    });
    expect(row.id).toBe(3);
    expect(row.rule).toBe('^/Admin/[A-Z]\\w+');
    expect(row.enable).toBe(true);
    expect(row.isWhite).toBe(true);
    expect(row.isRegex).toBe(true);
    expect(row.priority).toBe(100);
    expect(row.remark).toBe('后台白名单');
    expect(row.createUser).toBe('admin');
    expect(row.createTime).toBe('2026-11-09 10:00');
    expect(row.createIp).toBe('127.0.0.1');
    expect(row.updateUser).toBe('admin');
    expect(row.updateTime).toBe('2026-11-09 11:30');
    expect(row.updateIp).toBe('192.168.1.2');
  });
  it('缺 Rule → 空串；布尔数字 0/1 归一；时间缺失 → —；Priority 缺失 → 0', () => {
    const row = toTraceRuleRow({ Id: 5, Enable: 1, IsWhite: 0, IsRegex: 1 });
    expect(row.rule).toBe('');
    expect(row.enable).toBe(true);
    expect(row.isWhite).toBe(false);
    expect(row.isRegex).toBe(true);
    expect(row.priority).toBe(0);
    expect(row.createTime).toBe('—');
    expect(row.updateTime).toBe('—');
  });
  it('字符串布尔归一', () => {
    expect(toTraceRuleRow({ Id: 1, Enable: 'true', IsWhite: '1' }).isWhite).toBe(true);
    expect(toTraceRuleRow({ Id: 1 }).enable).toBe(false);
  });
});

describe('emptyTraceRuleForm / toTraceRuleForm · 表单初值', () => {
  it('空表单默认：启用 + 白名单（主要场景）+ 模糊匹配 + 优先级 0', () => {
    expect(emptyTraceRuleForm()).toEqual({ rule: '', enable: true, isWhite: true, isRegex: false, priority: 0, remark: '' });
  });
  it('编辑=行回填（全部可写字段）', () => {
    const row = toTraceRuleRow({ Id: 2, Rule: '/Sso/*', Enable: true, IsWhite: true, IsRegex: false, Priority: 7, Remark: '单点登录' });
    const form = toTraceRuleForm(row);
    expect(form.rule).toBe('/Sso/*');
    expect(form.enable).toBe(true);
    expect(form.isWhite).toBe(true);
    expect(form.isRegex).toBe(false);
    expect(form.priority).toBe(7);
    expect(form.remark).toBe('单点登录');
  });
});

describe('filterTraceRuleRows · 客户端过滤（配置级小表，不造服务端过滤假象）', () => {
  const rows: TraceRuleRow[] = [
    toTraceRuleRow({ Id: 1, Rule: '^/Admin/[A-Z]\\w+', IsWhite: true, Remark: '后台' }),
    toTraceRuleRow({ Id: 2, Rule: '/api/Node/*', IsWhite: false, Remark: '节点黑名单' }),
    toTraceRuleRow({ Id: 3, Rule: '^/Cube/\\w+', IsWhite: true, Remark: 'Cube' }),
  ];
  it('q 匹配规则串（不区分大小写）', () => {
    expect(filterTraceRuleRows(rows, '/api/')).toHaveLength(1);
    expect(filterTraceRuleRows(rows, 'CUBE')).toHaveLength(1);
  });
  it('q 匹配备注', () => {
    expect(filterTraceRuleRows(rows, '黑名单')).toHaveLength(1);
  });
  it('空 q = 全量', () => {
    expect(filterTraceRuleRows(rows, '')).toHaveLength(3);
  });
  it('无匹配 = 空数组（由组件渲染过滤空态）', () => {
    expect(filterTraceRuleRows(rows, 'zzz')).toHaveLength(0);
  });
});

describe('validateTraceRuleForm · 必填与优先级校验', () => {
  it('Rule 空/纯空格 → 报错文案；有值 → 空串', () => {
    expect(validateTraceRuleForm({ rule: '', enable: true, isWhite: true, isRegex: false, priority: 0, remark: '' })).toBe('规则不能为空');
    expect(validateTraceRuleForm({ rule: '  ', enable: true, isWhite: true, isRegex: false, priority: 0, remark: '' })).toBe('规则不能为空');
    expect(validateTraceRuleForm({ rule: '/Sso/*', enable: true, isWhite: true, isRegex: false, priority: 0, remark: '' })).toBe('');
  });
  it('优先级必须为非负整数', () => {
    expect(validateTraceRuleForm({ rule: 'a', enable: true, isWhite: true, isRegex: false, priority: -1, remark: '' })).toBe('优先级需为不小于 0 的整数');
    expect(validateTraceRuleForm({ rule: 'a', enable: true, isWhite: true, isRegex: false, priority: 1.5, remark: '' })).toBe('优先级需为不小于 0 的整数');
    expect(validateTraceRuleForm({ rule: 'a', enable: true, isWhite: true, isRegex: false, priority: Number.NaN, remark: '' })).toBe('优先级需为不小于 0 的整数');
    expect(validateTraceRuleForm({ rule: 'a', enable: true, isWhite: true, isRegex: false, priority: 100, remark: '' })).toBe('');
  });
});

describe('ruleBody · 请求体形状（主键不入 body）', () => {
  it('只含可写字段：rule/enable/isWhite/isRegex/priority/remark，Rule 去首尾空格', () => {
    const body = ruleBody({ rule: '  /Sso/*  ', enable: true, isWhite: true, isRegex: false, priority: 3, remark: ' r ' });
    expect(body).toEqual({ rule: '/Sso/*', enable: true, isWhite: true, isRegex: false, priority: 3, remark: ' r ' });
    expect(Object.keys(body)).not.toContain('id');
  });
});

describe('fetchTraceRules · 取数', () => {
  it('成功：归一 rows，参数只带 PageSize（假筛选硬条款：无 isWhite/isRegex/enable）', async () => {
    const calls: Array<{ url: string; params?: unknown }> = [];
    const req: Req = async (opt) => {
      calls.push({ url: String(opt.url ?? ''), params: opt.params });
      return {
        data: [{ Id: 1, Rule: '^/Admin/[A-Z]\\w+', Enable: true, IsWhite: true, IsRegex: true, Priority: 100 }],
      } as never;
    };
    const r = await fetchTraceRules(req);
    expect(r.error).toBe('');
    expect(r.rows).toHaveLength(1);
    expect(r.rows[0]?.rule).toBe('^/Admin/[A-Z]\\w+');
    expect(calls[0]?.url).toBe('/Monitors/TraceRule');
    const params = (calls[0]?.params ?? {}) as Record<string, unknown>;
    expect(params.PageSize).toBe(200);
    expect(Object.keys(params)).toEqual(['PageSize']);
  });
  it('失败：rows 空数组 + error 文案', async () => {
    const r = await fetchTraceRules(stubReq({}, true));
    expect(r.rows).toEqual([]);
    expect(r.error).toBe('stub 500');
  });
});

describe('saveTraceRule · 新增 POST / 编辑 PUT（主键在路径）', () => {
  it('无 id → POST /Monitors/TraceRule', async () => {
    const { req, calls } = captureReq();
    await saveTraceRule(req, { rule: '/Sso/*', enable: true, isWhite: true, isRegex: false, priority: 0, remark: '' });
    expect(calls[0]?.url).toBe('/Monitors/TraceRule');
    expect(calls[0]?.method).toBe('POST');
    expect(calls[0]?.data).toEqual({ rule: '/Sso/*', enable: true, isWhite: true, isRegex: false, priority: 0, remark: '' });
  });
  it('有 id → PUT /Monitors/TraceRule/{id}，主键不入 body', async () => {
    const { req, calls } = captureReq();
    await saveTraceRule(req, { rule: '/Sso/*', enable: false, isWhite: false, isRegex: true, priority: 5, remark: 'x' }, 12);
    expect(calls[0]?.url).toBe('/Monitors/TraceRule/12');
    expect(calls[0]?.method).toBe('PUT');
    expect(calls[0]?.data).toEqual({ rule: '/Sso/*', enable: false, isWhite: false, isRegex: true, priority: 5, remark: 'x' });
  });
});

describe('deleteTraceRule · DELETE /Monitors/TraceRule/{id}', () => {
  it('主键在路径', async () => {
    const { req, calls } = captureReq();
    await deleteTraceRule(req, 9);
    expect(calls[0]?.url).toBe('/Monitors/TraceRule/9');
    expect(calls[0]?.method).toBe('DELETE');
  });
});

describe('名单/方式标签文案（实体描述逐字）', () => {
  it('listLabel：白名单/黑名单；modeLabel：模糊/正则', () => {
    expect(listLabel(true)).toBe('白名单');
    expect(listLabel(false)).toBe('黑名单');
    expect(modeLabel(true)).toBe('正则');
    expect(modeLabel(false)).toBe('模糊');
  });
});

describe('文案常量（实现规范 §3.3 空态两模式）', () => {
  it('空态与过滤空态文案', () => {
    expect(TRACE_RULE_EMPTY_TEXT).toBe('还没有跟踪规则');
    expect(TRACE_RULE_EMPTY_CREATE_TEXT).toBe('新建规则');
    expect(TRACE_RULE_FILTER_EMPTY_TEXT).toBe('暂无匹配的跟踪规则');
  });
});
