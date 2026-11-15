// 流水线 Webhook 说明卡纯逻辑（CARD-DEP-04，来源旅程 J-DEP-10；GetFields 消费 + 结果语义速查，2026-11-07）。
// GetFields 为 WebApi 独有（GET /Deployment/PipelineWebhook/GetFields，AllowAnonymous，返回 token/secret 字段说明）；
// 接口失败降级为静态说明（不阻塞卡片）。Secret 值本身不做查看/设置（实现规范 §6.5 + 裁定 #6）。
// 结果语义 reason 字符串逐字来自盘点 §3 F01（accepted/ignored/skipped/error/missing token）。

type Rec = Record<string, unknown>;

function pick(r: Rec, ...keys: string[]): unknown {
  for (const k of keys) {
    const v = r[k];
    if (v !== undefined && v !== null) return v;
  }
  return undefined;
}
function str(v: unknown): string {
  return v === undefined || v === null ? '' : String(v);
}
function listOf(res: { data?: unknown } | undefined): Rec[] {
  const d = res?.data;
  return Array.isArray(d) ? (d as Rec[]) : [];
}

export interface WebhookFieldDoc {
  name: string;
  displayName: string;
  description: string;
}

/** GetFields 失败时的静态降级（与 Api 实现同语义，PipelineWebhookController.cs:57-61） */
export const WEBHOOK_FIELD_FALLBACK: WebhookFieldDoc[] = [
  { name: 'token', displayName: 'Token', description: '流水线鉴权 token，拼接到 webhook URL 的 ?token= 参数' },
  { name: 'secret', displayName: 'Secret', description: '可选，签名密钥。支持 HMAC-SHA256 十六进制/Base64 摘要，以及 GitLab/Gitee 明文令牌头' },
];

export function toWebhookFields(rows: Rec[]): WebhookFieldDoc[] {
  return rows.map((r) => ({
    name: str(pick(r, 'Name', 'name')),
    displayName: str(pick(r, 'DisplayName', 'displayName')) || str(pick(r, 'Name', 'name')),
    description: str(pick(r, 'Description', 'description')),
  }));
}

/** GET {VITE_APP_BASE_API}/Deployment/PipelineWebhook/GetFields：token/secret 字段说明。
 *  走查缺陷2（2026-11-07）：该控制器路由 [area]/[controller]/[action] 无 /api 前缀（Webhook 端点供 Git 平台裸调），
 *  统一 request 拼 /api 会 404，且非 2xx 会弹全局错误提示——故用原生 fetch 直连后端 host：
 *  a) URL 无 /api 前缀（base 取 VITE_APP_BASE_API，与其它接口同源同 host）；b) 失败静默返回 null，卡片降级静态说明。
 *  base 与 fetch 均可注入（单测）；无 base（如纯单测环境）直接降级。 */
export async function fetchWebhookFields(opts: { base?: string; fetchImpl?: typeof fetch } = {}): Promise<WebhookFieldDoc[] | null> {
  const base = String(opts.base ?? import.meta.env?.VITE_APP_BASE_API ?? '').replace(/\/+$/, '');
  if (!base) return null;
  const doFetch = opts.fetchImpl ?? ((...a: Parameters<typeof fetch>) => globalThis.fetch(...a));
  try {
    const res = await doFetch(`${base}/Deployment/PipelineWebhook/GetFields`, { method: 'GET' });
    if (!res.ok) return null;
    const json = (await res.json()) as { data?: unknown } | undefined;
    const rows = toWebhookFields(listOf(json));
    return rows.length ? rows : null;
  } catch {
    return null;
  }
}

/** 结果语义速查五类（reason 字符串逐字来自盘点 §3 F01，旅程 C06/C07/C10 契约） */
export interface WebhookResultGroup {
  result: string;
  tone: 'ok' | 'warn' | 'err' | 'default';
  label: string;
  detail: string;
  reasons: string[];
}

export const WEBHOOK_RESULT_GROUPS: WebhookResultGroup[] = [
  { result: 'accepted', tone: 'ok', label: '已接受', detail: '创建 Pending 运行并后台执行，响应含 runId 与 status', reasons: [] },
  {
    result: 'ignored',
    tone: 'warn',
    label: '已忽略',
    detail: '推送没有产生运行',
    reasons: ['pipeline disabled', 'no branch', 'branch mismatch'],
  },
  { result: 'skipped', tone: 'warn', label: '已跳过', detail: '同流水线+提交在防抖窗口（5 分钟）内重复，属正常去重', reasons: ['duplicate'] },
  {
    result: 'error',
    tone: 'err',
    label: '错误',
    detail: '推送被拒绝',
    reasons: ['invalid token', 'missing signature', 'invalid signature', 'build node unavailable', 'deploy not found'],
  },
  { result: 'missing token', tone: 'err', label: '缺 token', detail: '地址少了 token 参数', reasons: [] },
];
