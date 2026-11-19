import StarServerSettingsForm from './StarServerSettingsForm.vue';
import type { Req } from '../../../apps/stardust/src/views/Stardust/appsModel.ts';

/**
 * 示例平台设置（GET /Platform/StarServer 桩响应体）。
 * 键名 PascalCase 与后端 Config<TConfig>.Current 序列化一致；未列出的字段组件按默认值回退。
 */
const RAW: Record<string, unknown> = {
  Port: 6680, AutoRegister: true, AppAutoRegister: true, ReRegister: 1,
  WhiteIP: '10.0.0.0/8,192.168.0.0/16', AllowControlNodesWhenEmpty: false,
  NodeCodeFormula: '{code}-{seq}', NodeCodeLevel: 3,
  TokenExpire: 0, SessionTimeout: 7200, SaltTime: 300,
  MonitorFlowPeriod: 5, MonitorBatchPeriod: 60, MonitorSavePeriod: 300, MonitorPatrolPeriod: 60,
  AlarmPeriod: 300, IgnoreErrorItems: 'test.*,*.ping', IgnoreErrorKeywords: 'TaskCanceled',
  DataRetention: 30, DataRetention2: 90, DataRetention3: 365, ClearMode: 1,
  WebUrl: 'http://127.0.0.1:6600', TraceAnonymous: false, TraceShareExpire: 1440, HomeSummaryMode: 2,
  EnableInstallPage: true, AgentDownloadBaseUrl: '', AgentServerUrl: '', AnonymousReport: false,
  DotNetSyncPeriod: 0, DotNetSyncUrl: '',
  UploadPath: '', MaxUploadSize: 0, FileCache: '', FileCacheWhiteIP: '',
  FileStorageProvide: true, FileStorageFetch: true, ValidateAttachment: false,
  PublicAttachmentCategories: '', AttachmentTokenExpire: 0,
  UplinkServer: '', FixedCity: false,
  EnableMcp: true, RateLimitPaths: '/api/mcp', RateLimitTimes: 60, CorsOrigins: '',
  TokenSecret: 's3cret-token-value', TraceShareSecret: 'share-key-value',
  McpActionSet: 'node/app/config/deploy/gateway/monitor/system', SensitiveKeywords: 'password,secret',
};

interface CallRec { method?: string; url?: string; data?: Record<string, unknown> }

/** 成功桩：GET 返回示例设置、PUT 成功；所有调用记录到 window.__starServerCalls 供 CT 断言 */
function okStub(): Req {
  const calls: CallRec[] = [];
  return (async (o: unknown) => {
    const rec = o as CallRec;
    calls.push(rec);
    (globalThis as { __starServerCalls?: CallRec[] }).__starServerCalls = calls;
    if (rec?.method === 'PUT') return { data: true, status: 200 };
    return { data: { ...RAW }, status: 200 };
  }) as unknown as Req;
}

/** 保存失败桩：GET 正常，PUT 抛错（验证失败提示不静默） */
function saveFailStub(): Req {
  const calls: CallRec[] = [];
  return (async (o: unknown) => {
    const rec = o as CallRec;
    calls.push(rec);
    (globalThis as { __starServerCalls?: CallRec[] }).__starServerCalls = calls;
    if (rec?.method === 'PUT') throw new Error('平台设置保存失败：权限不足');
    return { data: { ...RAW }, status: 200 };
  }) as unknown as Req;
}

export const stories = [
  // 已加载：完整表单（分组渲染 + 字段回显 + 密钥 mask + 只读文本）
  { id: 'StarServerSettingsForm/Loaded', component: StarServerSettingsForm, props: { req: okStub(), open: true } },
  // 加载失败：错误态不静默
  {
    id: 'StarServerSettingsForm/Error',
    component: StarServerSettingsForm,
    props: { req: (async () => { throw new Error('网络中断'); }) as unknown as Req, open: true },
  },
  // 保存失败：PUT 抛错，错误条可见且不关闭
  { id: 'StarServerSettingsForm/SaveFail', component: StarServerSettingsForm, props: { req: saveFailStub(), open: true } },
];
