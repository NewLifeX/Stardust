import { describe, expect, it } from 'vitest';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  fetchDeployLogFiles,
  fetchDeployLogTail,
  parseLogTailPayload,
} from './deployLog.logic.ts';

type Cfg = { url: string; method?: string; params?: Record<string, unknown> };
function stubReq(responder: (cfg: Cfg) => unknown = () => ({ code: 0, data: null })): { req: Req; calls: Cfg[] } {
  const calls: Cfg[] = [];
  const req = (async (cfg: Cfg) => {
    calls.push(cfg);
    return responder(cfg);
  }) as Req;
  return { req, calls };
}

describe('parseLogTailPayload', () => {
  it('认 PascalCase 与 camelCase', () => {
    const v = parseLogTailPayload({ Content: 'line1\n', Offset: 10, FileSize: 999, Truncated: true });
    expect(v.content).toBe('line1\n');
    expect(v.offset).toBe(10);
    expect(v.fileSize).toBe(999);
    expect(v.truncated).toBe(true);
  });

  it('空对象给默认值', () => {
    const v = parseLogTailPayload({});
    expect(v.content).toBe('');
    expect(v.offset).toBe(0);
    expect(v.truncated).toBe(false);
  });
});

describe('fetchDeployLogFiles', () => {
  it('缺少 deployNodeId 时不发请求', async () => {
    const { req, calls } = stubReq();
    const r = await fetchDeployLogFiles(req, 0);
    expect(r.ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('成功时解析 files 列表（大小写兼容）', async () => {
    const { req, calls } = stubReq(() => ({
      code: 0,
      data: { files: [{ Name: 'a.log', Size: 12, LastWriteUtc: '2026-01-01' }] },
    }));
    const r = await fetchDeployLogFiles(req, 5);
    expect(r.ok).toBe(true);
    expect(r.files).toEqual([{ name: 'a.log', size: 12, lastWriteUtc: '2026-01-01' }]);
    expect(calls[0]?.url).toBe('/Deployment/AppDeployNode/LogFiles');
    expect(calls[0]?.params).toEqual({ id: 5 });
  });

  it('业务失败透传 message', async () => {
    const { req } = stubReq(() => ({ code: 500, message: 'Log directory is not configured' }));
    const r = await fetchDeployLogFiles(req, 1);
    expect(r.ok).toBe(false);
    expect(r.message).toContain('Log directory');
  });
});

describe('fetchDeployLogTail', () => {
  it('未选文件时不请求', async () => {
    const { req, calls } = stubReq();
    const r = await fetchDeployLogTail(req, 3, '  ');
    expect(r.ok).toBe(false);
    expect(calls).toHaveLength(0);
  });

  it('成功时 parse tail', async () => {
    const { req, calls } = stubReq(() => ({
      code: 0,
      data: { content: 'hello', offset: 0, fileSize: 5, truncated: false },
    }));
    const r = await fetchDeployLogTail(req, 3, 'app.log', 0, 65536);
    expect(r.ok).toBe(true);
    expect(r.tail?.content).toBe('hello');
    expect(calls[0]?.params).toMatchObject({ id: 3, file: 'app.log', offset: 0, maxBytes: 65536 });
  });
});
