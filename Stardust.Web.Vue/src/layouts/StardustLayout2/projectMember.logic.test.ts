import { describe, expect, it } from 'vitest';
import type { Req, ReqOptions } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  addMember,
  addMemberBlockReason,
  candidateUsers,
  feedbackRemainMs,
  fetchMemberLinks,
  fetchUserOptions,
  memberSaveBlock,
  memberUserOptions,
  removeMember,
  setMemberEnable,
  type MemberLink,
} from './projectMember.logic.ts';

const MEMBERS: MemberLink[] = [
  { id: 1, projectId: 3, userId: 9, enable: true, userName: '何炳宏' },
  { id: 2, projectId: 3, userId: 15, enable: false, userName: '停用账号' },
  { id: 3, projectId: 4, userId: 9, enable: true, userName: '何炳宏' },
  { id: 4, projectId: 3, userId: 9, enable: true, userName: '重复' },
];

describe('memberUserOptions', () => {
  const users = [
    { id: 9, name: 'admin' },
    { id: 21, name: '外人' },
  ];

  it('未选项目时不给任何用户', () => {
    expect(memberUserOptions(0, users, MEMBERS)).toEqual([]);
  });

  it('只保留该项目的启用成员，名字优先用用户表，停用与重复丢掉', () => {
    expect(memberUserOptions(3, users, MEMBERS)).toEqual([{ id: 9, name: 'admin' }]);
  });

  it('用户表没有名字时用成员行上的 UserName', () => {
    expect(memberUserOptions(4, [], MEMBERS)).toEqual([{ id: 9, name: '何炳宏' }]);
  });
});

describe('candidateUsers / 按钮原因', () => {
  it('已在成员表里的用户不再作为添加候选', () => {
    expect(candidateUsers(
      [{ id: 9, name: 'a' }, { id: 8, name: 'b' }, { id: 0, name: '无效' }],
      [{ userId: 9 }],
    )).toEqual([{ id: 8, name: 'b' }]);
  });

  it('没选项目、没选用户、已是成员，原因各不相同', () => {
    expect(addMemberBlockReason(null, 1, [])).toContain('顶栏');
    expect(addMemberBlockReason(3, 0, [])).toContain('请选择');
    expect(addMemberBlockReason(3, 9, [{ userId: 9 }])).toContain('已是当前项目成员');
    expect(addMemberBlockReason(3, 8, [{ userId: 9 }])).toBe('');
  });
});

describe('memberSaveBlock', () => {
  it('未绑定用户不拦；绑定用户必须是该项目启用成员', () => {
    expect(memberSaveBlock(3, 0, MEMBERS, true)).toBe('');
    expect(memberSaveBlock(0, 9, MEMBERS, true)).toContain('先选择项目');
    expect(memberSaveBlock(3, 9, MEMBERS, false)).toContain('正在核对');
    expect(memberSaveBlock(3, 15, MEMBERS, true)).toContain('不是所选项目的启用成员');
    expect(memberSaveBlock(3, 9, MEMBERS, true)).toBe('');
  });
});

describe('feedbackRemainMs', () => {
  it('不足 300ms 补到 300，已超过则不再等', () => {
    expect(feedbackRemainMs(40)).toBe(260);
    expect(feedbackRemainMs(300)).toBe(0);
    expect(feedbackRemainMs(800)).toBe(0);
    expect(feedbackRemainMs(Number.NaN)).toBe(300);
  });
});

describe('ProjectUser 请求', () => {
  it('fetchMemberLinks 带 projectId，并读 UserName / Enable', async () => {
    let seen: Record<string, unknown> = {};
    const req: Req = async (opt) => {
      seen = opt.params ?? {};
      return {
        data: [
          { Id: 1, ProjectId: 3, UserId: 2, UserName: '何炳宏', Enable: true },
          { Id: 2, ProjectId: 3, UserId: 4, Enable: false },
        ],
      };
    };
    const r = await fetchMemberLinks(req, 3);
    expect(seen.projectId).toBe(3);
    expect(r.error).toBe('');
    expect(r.rows[0]).toMatchObject({ id: 1, userName: '何炳宏', enable: true });
    expect(r.rows[1].enable).toBe(false);
    expect(r.rows[1].userName).toBe('#4');
  });

  it('fetchMemberLinks 失败时给出错误，不抛', async () => {
    const req: Req = async () => { throw new Error('超时'); };
    const r = await fetchMemberLinks(req, 1);
    expect(r.rows).toEqual([]);
    expect(r.error).toBe('超时');
  });

  it('fetchUserOptions 跳过无编号行', async () => {
    const req: Req = async () => ({ data: [{ ID: 2, DisplayName: '何炳宏' }, { Name: '无名' }] });
    const r = await fetchUserOptions(req);
    expect(r.users).toEqual([{ id: 2, name: '何炳宏' }]);
  });

  it('addMember 成功读回 Id；缺项目或用户时不发请求', async () => {
    const calls: string[] = [];
    const req: Req = async (opt) => {
      calls.push(opt.method ?? '');
      return { code: 0, data: { Id: 18 }, message: '' };
    };
    expect((await addMember(req, { projectId: 0, userId: 2, enable: true })).ok).toBe(false);
    expect((await addMember(req, { projectId: 3, userId: 0, enable: true })).ok).toBe(false);
    expect(calls).toEqual([]);
    const ok = await addMember(req, { projectId: 3, userId: 2, enable: true });
    expect(ok).toMatchObject({ ok: true, id: 18 });
    expect(calls).toEqual(['POST']);
  });

  it('addMember 业务失败与抛错都写明用户和项目', async () => {
    const bad: Req = async () => ({ code: 500, message: '唯一索引冲突', data: null });
    const r1 = await addMember(bad, { projectId: 3, userId: 2, enable: true });
    expect(r1.ok).toBe(false);
    expect(r1.message).toContain('唯一索引冲突');

    const boom: Req = async () => { throw new Error('网络断开'); };
    const r2 = await addMember(boom, { projectId: 3, userId: 2, enable: true });
    expect(r2.message).toContain('用户 2');
    expect(r2.message).toContain('项目 3');
    expect(r2.message).toContain('网络断开');
  });

  it('removeMember / setMemberEnable 走 DELETE 查询 id 与 PUT 正文', async () => {
    const calls: string[] = [];
    const req: Req = async (opt) => {
      const extra = opt as ReqOptions & { data?: { id?: number; enable?: boolean } };
      calls.push(`${opt.method} ${opt.url} q=${opt.params?.id ?? ''} body=${extra.data?.id ?? ''}/${String(extra.data?.enable)}`);
      return { code: 0, data: null };
    };
    expect((await removeMember(req, 0)).ok).toBe(false);
    const removed = await removeMember(req, 7, '何炳宏');
    expect(removed.ok).toBe(true);
    expect(removed.message).toContain('何炳宏');
    const stopped = await setMemberEnable(req, 7, false, '何炳宏');
    expect(stopped.message).toContain('停用');
    expect(calls).toEqual([
      'DELETE /Platform/ProjectUser q=7 body=/undefined',
      'PUT /Platform/ProjectUser q= body=7/false',
    ]);
  });
});
