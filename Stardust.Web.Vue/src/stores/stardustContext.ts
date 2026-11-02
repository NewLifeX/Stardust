/**
 * 星尘控制台 · 全局上下文（StardustContext）
 *
 * 依据《Stardust_交互设计重构方案.md》§5.3 / §24：
 *   Project 与 Environment 是控制台所有资源查询、跳转、筛选、权限和操作的统一上下文。
 *
 * 设计：
 *  - 单例 reactive store（布局与各页面共享同一状态）；
 *  - localStorage 持久化，刷新后保持上下文；
 *  - setProject / setEnvironment 变更后自动持久化；
 *  - requestParams() 供各页请求注入 projectId / environment（后端按 ProjectId 过滤时生效）。
 *
 * 样式纪律：仅 --el-* 令牌派生，无硬编码色值。
 */
import { reactive, readonly } from 'vue';
import { cubeAxios } from '@newlifex/cube-vue/core/utils/request';
import { mergeContextParams } from './contextParams.logic.ts';

export interface StardustContext {
  /** 当前项目编号（来自 GalaxyProject.Id，后端不可用时为 null） */
  projectId: number | null;
  /** 当前项目名称（来自 GalaxyProject.Name） */
  projectName: string;
  /** 当前环境（开发 / 测试 / 预发布 / 生产） */
  environment: string | null;
  /** 当前环境显示名（同 environment，预留多语言/别名扩展） */
  environmentName: string;
  /** 上下文版本号：setProject / setEnvironment 每次变更递增，页面 watch 后自动刷新（方案 §5.3 ②） */
  revision: number;
  /**
   * 项目上下文是否已完成首次解析（TopBar 拉取 /Platform/GalaxyProject 之后置位）。
   *
   * 页面首轮取数应等到 ready 为 true —— 否则会「先按 null 上下文打一整轮请求，
   * 解析完成后再整轮重打」。实测后端在并发下很慢（顺序 2s → 并发 15-35s），
   * 这种重复请求的代价被显著放大。
   */
  ready: boolean;
}

/** 方案 §5.4 推荐环境：开发 → 测试 → 预发布 → 生产 */
export const ENVIRONMENTS: { key: string; label: string; token: string }[] = [
  { key: 'dev', label: '开发', token: 'var(--el-color-info)' },
  { key: 'test', label: '测试', token: 'var(--el-color-primary)' },
  { key: 'staging', label: '预发布', token: 'var(--el-color-warning)' },
  { key: 'prod', label: '生产', token: 'var(--el-color-danger)' },
];

/** 环境 key → 展示标签（未知 key 原样返回） */
export function environmentLabel(key: string | null | undefined): string {
  if (!key) return '全部环境';
  return ENVIRONMENTS.find((e) => e.key === key)?.label ?? key;
}

/** 环境 key → 语义令牌色（未知回退中性色） */
export function environmentToken(key: string | null | undefined): string {
  if (!key) return 'var(--el-text-color-secondary)';
  return ENVIRONMENTS.find((e) => e.key === key)?.token ?? 'var(--el-text-color-secondary)';
}

const LS_PROJECT_ID = 'stardust-project-id';
const LS_PROJECT_NAME = 'stardust-project-name';
const LS_ENVIRONMENT = 'stardust-environment';

function loadInitial(): StardustContext {
  return {
    projectId: Number(localStorage.getItem(LS_PROJECT_ID)) || null,
    projectName: localStorage.getItem(LS_PROJECT_NAME) || '',
    environment: localStorage.getItem(LS_ENVIRONMENT) || null,
    environmentName: environmentLabel(localStorage.getItem(LS_ENVIRONMENT)),
    revision: 0,
    // 已持久化 projectId（老用户 / 刷新页面）时上下文即刻可用，无需等待 TopBar 解析
    ready: !!localStorage.getItem(LS_PROJECT_ID),
  };
}

/** 全局上下文单例：布局与页面共享 */
export const stardustContext = reactive<StardustContext>(loadInitial());

/** 上下文变更（项目 / 环境）后递增版本号，页面 watch 联动刷新 */
function bumpRevision(): void {
  stardustContext.revision += 1;
}

/**
 * 标记项目上下文已完成首次解析（TopBar 调完 /Platform/GalaxyProject 后调用）。
 *
 * **不递增 revision** —— 就绪本身不是「上下文变更」。页面靠 watch ready 触发首轮取数；
 * 若此处同时递增 revision，页面会收到两次触发、首轮取数被执行两遍（正是要消除的重复）。
 */
export function markStardustContextReady(): void {
  if (stardustContext.ready) return;
  stardustContext.ready = true;
}

/** 切换项目（同时持久化；项目名称为空时清空 id 以免串数据） */
export function setStardustProject(id: number | null, name: string): void {
  stardustContext.projectId = id && name ? id : null;
  stardustContext.projectName = name ?? '';
  if (name) localStorage.setItem(LS_PROJECT_ID, String(id ?? ''));
  else localStorage.removeItem(LS_PROJECT_ID);
  localStorage.setItem(LS_PROJECT_NAME, stardustContext.projectName);
  bumpRevision();
}

/** 切换环境（同时持久化） */
export function setStardustEnvironment(key: string | null): void {
  stardustContext.environment = key;
  stardustContext.environmentName = environmentLabel(key);
  if (key) localStorage.setItem(LS_ENVIRONMENT, key);
  else localStorage.removeItem(LS_ENVIRONMENT);
  bumpRevision();
}

/** 各页请求注入用：返回携带当前项目/环境的查询参数（无上下文时为 null） */
export function contextParams(): { projectId?: number; environment?: string } {
  const p: { projectId?: number; environment?: string } = {};
  if (stardustContext.projectId) p.projectId = stardustContext.projectId;
  if (stardustContext.environment) p.environment = stardustContext.environment;
  return p;
}

/**
 * 方案 §24.2：全局请求拦截器，自动把当前上下文（projectId / environment）合并到所有请求 query。
 * 业务页无需再手动拼 contextParams()；已显式传入的同名参数不被覆盖。
 * 无上下文时 contextParams() 为空对象，不注入任何参数，对登录/用户信息等请求无副作用。
 */
let contextInterceptorRegistered = false;
export function ensureStardustContextInterceptor(): void {
  if (contextInterceptorRegistered) return;
  contextInterceptorRegistered = true;
  cubeAxios.interceptors.request.use((config) => {
    const ctx = contextParams();
    if (ctx.projectId != null || ctx.environment != null) {
      // 显式 query 盖过上下文：令牌表单要按所选项目拉成员，不能被顶栏项目覆盖
      config.params = mergeContextParams(config.params as Record<string, unknown> | undefined, ctx);
    }
    return config;
  });
}

// 模块被任意页面/组件引用时即自动接线，确保全应用请求统一携带上下文
ensureStardustContextInterceptor();

/** 只读引用（供模板绑定；直接使用 stardustContext 亦可） */
export const stardustContextView = readonly(stardustContext);
