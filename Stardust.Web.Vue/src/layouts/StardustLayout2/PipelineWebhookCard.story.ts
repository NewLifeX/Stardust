import PipelineWebhookCard from './PipelineWebhookCard.vue';
import type { PipelineRow } from './pipelineConfig.logic.ts';
import type { WebhookFieldDoc } from './pipelineWebhook.logic.ts';

const ROWS: PipelineRow[] = [
  { id: 7, name: 'build-main', deployId: 10, branch: 'release/*', buildNodeId: 3, projectId: 88, deployNodeIds: '1,2', autoDeploy: true, enable: true, updateTime: '' },
  { id: 8, name: 'hotfix', deployId: 10, branch: 'main', buildNodeId: 3, projectId: 88, deployNodeIds: '', autoDeploy: false, enable: false, updateTime: '' },
];

const FIELDS: WebhookFieldDoc[] = [
  { name: 'token', displayName: 'Token', description: '流水线鉴权 token，拼接到 webhook URL 的 ?token= 参数' },
  { name: 'secret', displayName: 'Secret', description: '可选，签名密钥。支持 HMAC-SHA256 十六进制/Base64 摘要，以及 GitLab/Gitee 明文令牌头' },
];

export const stories = [
  // 测试状态：GetFields 就绪（说明来自接口）+ 结果语义速查五类
  { id: 'PipelineWebhookCard/Default', component: PipelineWebhookCard, props: { rows: ROWS, fields: FIELDS, fieldsLoading: false, copyingId: 0 } },

  // 测试状态：GetFields 失败 → 降级静态说明（含降级注记）
  { id: 'PipelineWebhookCard/Fallback', component: PipelineWebhookCard, props: { rows: ROWS, fields: null, fieldsLoading: false, copyingId: 0 } },

  // 测试状态：复制进行中（旋转禁用）
  { id: 'PipelineWebhookCard/Copying', component: PipelineWebhookCard, props: { rows: ROWS, fields: FIELDS, fieldsLoading: false, copyingId: 7 } },

  // 测试状态：无流水线
  { id: 'PipelineWebhookCard/Empty', component: PipelineWebhookCard, props: { rows: [], fields: FIELDS, fieldsLoading: false, copyingId: 0 } },
];
