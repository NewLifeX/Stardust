import { describe, expect, it } from 'vitest';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';
import {
  MAX_TAIL_KEEP,
  applyTailSnapshot,
  canLoadEarlier,
  earlierOffset,
  fetchDeployLogFiles,
  fetchDeployLogTail,
  formatFileSize,
  parseLogTailPayload,
  prependTailPage,
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

describe('applyTailSnapshot（自动轮询增量合并）', () => {
  const tail = (content: string, offset: number, fileSize: number, truncated = false) => ({
    content,
    offset,
    fileSize,
    truncated,
  });

  it('首读（上次 fileSize=0）→ 整体替换', () => {
    const r = applyTailSnapshot('', 0, tail('line1\n', 0, 6));
    expect(r.content).toBe('line1\n');
    expect(r.appended).toBe(false);
  });

  it('文件增长且 offset 追上上次 fileSize → 追加新增', () => {
    const r = applyTailSnapshot('A', 1, tail('B', 1, 2));
    expect(r.content).toBe('AB');
    expect(r.appended).toBe(true);
  });

  it('Agent 端增量读也标 truncated=true，不影响追加', () => {
    // Agent 的 truncated 语义是「非全文件」：offset>0 的增量读恒为 true
    const r = applyTailSnapshot('A', 1, tail('B', 1, 2, true));
    expect(r.content).toBe('AB');
    expect(r.appended).toBe(true);
  });

  it('无新增时小文件回读全量 → 替换（不重复拼接）', () => {
    const r = applyTailSnapshot('AB', 2, tail('AB', 0, 2));
    expect(r.content).toBe('AB');
    expect(r.appended).toBe(false);
  });

  it('无新增时大文件回读尾部 → 替换', () => {
    const r = applyTailSnapshot('AAAABBBBCC', 10, tail('BBCC', 6, 10));
    expect(r.content).toBe('BBCC');
    expect(r.appended).toBe(false);
  });

  it('文件回卷变小（rotate/截断）→ 替换', () => {
    const r = applyTailSnapshot('x'.repeat(100), 100, tail('new', 0, 3));
    expect(r.content).toBe('new');
    expect(r.appended).toBe(false);
  });

  it('追加超过上限后从头截断到 MAX_TAIL_KEEP', () => {
    const big = 'a'.repeat(MAX_TAIL_KEEP);
    const r = applyTailSnapshot(big, big.length, tail('b'.repeat(10), big.length, big.length + 10));
    expect(r.content.length).toBe(MAX_TAIL_KEEP);
    expect(r.content.endsWith('b'.repeat(10))).toBe(true);
  });
});

describe('formatFileSize', () => {
  it('字节与边界', () => {
    expect(formatFileSize(0)).toBe('0 B');
    expect(formatFileSize(999)).toBe('999 B');
    expect(formatFileSize(1024)).toBe('1.0 KB');
    expect(formatFileSize(1536)).toBe('1.5 KB');
    expect(formatFileSize(10 * 1024)).toBe('10 KB');
    expect(formatFileSize(1048576)).toBe('1.0 MB');
    expect(formatFileSize(1572864)).toBe('1.5 MB');
    expect(formatFileSize(5 * 1024 * 1024 * 1024)).toBe('5.0 GB');
  });
});

describe('向上翻页（加载更早）', () => {
  const tail = (content: string, offset: number) => ({ content, offset, fileSize: 0, truncated: true });

  it('earlierOffset：窗口起点向前一页，最小为 1（Agent offset=0 语义是读尾部）', () => {
    expect(earlierOffset(300000)).toBe(300000 - 256 * 1024);
    expect(earlierOffset(100)).toBe(1);
    expect(earlierOffset(0)).toBe(1);
  });

  it('canLoadEarlier：windowStart>1 且未超累计上限', () => {
    expect(canLoadEarlier(262144, 1000)).toBe(true);
    expect(canLoadEarlier(1, 1000)).toBe(false);
    expect(canLoadEarlier(0, 1000)).toBe(false);
    expect(canLoadEarlier(262144, 2 * 1024 * 1024)).toBe(false);
  });

  it('prependTailPage：精确裁齐后前置拼接', () => {
    // 当前窗口 [2,…)='CD'；本次读 [0,2)='AB' → 拼成 'ABCD'，新起点 0
    const r = prependTailPage('CD', 2, tail('AB', 0));
    expect(r.content).toBe('ABCD');
    expect(r.start).toBe(0);
  });

  it('prependTailPage：与当前窗口重叠时裁掉多读部分', () => {
    // 当前窗口 [2,…)='CD'；本次读 [0,3)='ABC'（多读了 byte2='C'）→ 裁到 'AB' 再拼
    const r = prependTailPage('CD', 2, tail('ABC', 0));
    expect(r.content).toBe('ABCD');
  });

  it('prependTailPage：无交集时原样前置（防御）', () => {
    const r = prependTailPage('CD', 100, tail('AB', 1));
    expect(r.content).toBe('ABCD');
    expect(r.start).toBe(1);
  });
});
