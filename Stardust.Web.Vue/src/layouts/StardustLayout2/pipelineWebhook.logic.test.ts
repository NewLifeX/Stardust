// 流水线 Webhook 说明卡纯逻辑单测（CARD-DEP-04）：GetFields 解析/失败降级、结果语义速查 reason 逐字。
// 走查缺陷2（2026-11-07）：GetFields 无 /api 前缀，fetchWebhookFields 改为 base+fetch 注入直连。
import { describe, it, expect } from 'vitest';
import { fetchWebhookFields, toWebhookFields, WEBHOOK_FIELD_FALLBACK, WEBHOOK_RESULT_GROUPS } from './pipelineWebhook.logic.ts';

type FetchStub = (url: string, init?: { method?: string }) => Promise<{ ok: boolean; json: () => Promise<unknown> }>;

function fetchOk(data: unknown, seen: { url: string; init?: { method?: string } }[] = []): FetchStub {
  return async (url, init) => {
    seen.push({ url, init });
    return { ok: true, json: async () => data };
  };
}

describe('pipelineWebhook · GetFields（直连，无 /api 前缀）', () => {
  it('base+fetch 注入：拼 {base}/Deployment/PipelineWebhook/GetFields（无 /api），解析 ApiResponse 壳 data 内层', async () => {
    const seen: { url: string; init?: { method?: string } }[] = [];
    const fetchImpl = fetchOk({ code: 0, data: [
      { Name: 'token', DisplayName: 'Token', Description: '流水线鉴权 token，拼接到 webhook URL 的 ?token= 参数' },
      { name: 'secret', displayName: 'Secret', description: '可选，签名密钥。支持 HMAC-SHA256 十六进制/Base64 摘要，以及 GitLab/Gitee 明文令牌头' },
    ] }, seen);
    const fields = await fetchWebhookFields({ base: 'http://localhost:6681/', fetchImpl: fetchImpl as unknown as typeof fetch });
    expect(fields?.map((f) => f.name)).toEqual(['token', 'secret']);
    expect(seen[0].url).toBe('http://localhost:6681/Deployment/PipelineWebhook/GetFields');
    expect(seen[0].init?.method).toBe('GET');
  });

  it('非 2xx / 空 data / fetch 抛错 / 无 base → null 降级（不抛异常不弹全局提示）', async () => {
    const notOk = (async () => ({ ok: false, json: async () => ({}) })) as unknown as typeof fetch;
    expect(await fetchWebhookFields({ base: 'http://x', fetchImpl: notOk })).toBeNull();
    expect(await fetchWebhookFields({ base: 'http://x', fetchImpl: fetchOk({ code: 0, data: [] }) as unknown as typeof fetch })).toBeNull();
    const boom = (async () => {
      throw new Error('net');
    }) as unknown as typeof fetch;
    expect(await fetchWebhookFields({ base: 'http://x', fetchImpl: boom })).toBeNull();
    // 无 base（单测环境无 VITE_APP_BASE_API 兜底）→ 直接降级
    expect(await fetchWebhookFields({ fetchImpl: fetchOk({}) as unknown as typeof fetch })).toBeNull();
  });

  it('toWebhookFields：DisplayName 缺省回退 Name；小写字段也认', () => {
    const rows = toWebhookFields([{ name: 'token', description: 'd1' }, { Name: 's', DisplayName: 'Secret' }]);
    expect(rows).toEqual([
      { name: 'token', displayName: 'token', description: 'd1' },
      { name: 's', displayName: 'Secret', description: '' },
    ]);
  });

  it('降级静态说明与 Api GetFields 同语义（token/secret 两条）', () => {
    expect(WEBHOOK_FIELD_FALLBACK.map((f) => f.name)).toEqual(['token', 'secret']);
    expect(WEBHOOK_FIELD_FALLBACK[1].description).toContain('HMAC-SHA256');
  });
});

describe('pipelineWebhook · 结果语义速查（reason 逐字，盘点 §3 F01）', () => {
  it('五类：accepted/ignored/skipped/error/missing token', () => {
    expect(WEBHOOK_RESULT_GROUPS.map((g) => g.result)).toEqual(['accepted', 'ignored', 'skipped', 'error', 'missing token']);
  });

  it('reason 字符串逐字对齐盘点与旅程 C05/C07/C10', () => {
    const byResult = new Map(WEBHOOK_RESULT_GROUPS.map((g) => [g.result, g]));
    expect(byResult.get('ignored')?.reasons).toEqual(['pipeline disabled', 'no branch', 'branch mismatch']);
    expect(byResult.get('skipped')?.reasons).toEqual(['duplicate']);
    expect(byResult.get('error')?.reasons).toEqual(['invalid token', 'missing signature', 'invalid signature', 'build node unavailable', 'deploy not found']);
    expect(byResult.get('accepted')?.reasons).toEqual([]);
  });

  it('色调：accepted=ok、error/missing token=err', () => {
    const tone = (r: string) => WEBHOOK_RESULT_GROUPS.find((g) => g.result === r)?.tone;
    expect(tone('accepted')).toBe('ok');
    expect(tone('error')).toBe('err');
    expect(tone('missing token')).toBe('err');
  });
});
