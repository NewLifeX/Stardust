// F14/F14-1 路由接线单测：/trace/:traceId? 契约页必须可达；旧 /Trace/:id 只能命名跳转让位。
// 关键回归（P1）：必选段 /Trace/:id 在 vue-router 打分上压过可选段 /trace/:traceId?，
// 若旧记录 redirect 回「路径 /trace/<id>」会再次命中自身 → 无限递归 RangeError 全部深链白屏。
// 所以本文件用「consoleRoutes + legacyTraceRedirect 全量组合」真实解析，锁死最终 matched 记录。
import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { createRouter, createMemoryHistory } from 'vue-router';
import { consoleRoutes, legacyTraceRedirect } from './routes.ts';

/** 复刻 main.ts externalRoutes 的 trace 相关组合（consoleRoutes 在前 + 旧路由让位声明） */
const routes = [...consoleRoutes, legacyTraceRedirect] as never;

function buildRouter() {
  return createRouter({ history: createMemoryHistory(), routes });
}

describe('F14 /trace 路由契约', () => {
  it('/trace/a,b（逗号多值）最终命中 ConsoleTrace 且 params.traceId 原样保留，不落在 /Trace/:id', async () => {
    const router = buildRouter();
    await router.push('/trace/a,b');
    expect(router.currentRoute.value.name).toBe('ConsoleTrace');
    expect(router.currentRoute.value.params.traceId).toBe('a,b');
    expect(router.currentRoute.value.path).toBe('/trace/a,b');
  });

  it('/trace/<单id>（旧形态会被 /Trace/:id 打分截走）经命名跳转最终命中 ConsoleTrace', async () => {
    const router = buildRouter();
    await router.push('/trace/405');
    expect(router.currentRoute.value.name).toBe('ConsoleTrace');
    expect(router.currentRoute.value.params.traceId).toBe('405');
  });

  it('/trace 无参命中 ConsoleTrace（可选段空参数）', async () => {
    const router = buildRouter();
    await router.push('/trace');
    expect(router.currentRoute.value.name).toBe('ConsoleTrace');
    expect(router.currentRoute.value.params.traceId).toBe('');
  });

  it('/Trace/<旧形态>?layout=detail → 命名跳转一跳直达 ConsoleTrace，query 保留，不无限递归', async () => {
    const router = buildRouter();
    await router.push('/Trace/369299?layout=detail');
    expect(router.currentRoute.value.name).toBe('ConsoleTrace');
    expect(router.currentRoute.value.params.traceId).toBe('369299');
    expect(router.currentRoute.value.query.layout).toBe('detail');
    // 旧记录必须有 redirect 且是命名跳转（防再次改回路径重定向引入递归）
    expect(legacyTraceRedirect.redirect).toBeTypeOf('function');
  });

  it('/trace?id=xxx（旧 MVC 链形态，无 path 参数）命中 ConsoleTrace，query.id 兜底由组件侧消费', async () => {
    const router = buildRouter();
    await router.push('/trace?id=old-link');
    expect(router.currentRoute.value.name).toBe('ConsoleTrace');
    expect(router.currentRoute.value.query.id).toBe('old-link');
  });

  it('F14-1 文本守卫：main.ts 不再 import 旧页组件、/Trace/:id 只能来自 routes.ts 的 legacyTraceRedirect', () => {
    const main = readFileSync(resolve(fileURLToPath(import.meta.url), '../../../main.ts'), 'utf8');
    // 旧页组件 Trace/index.vue 不得再被 import 进 main.ts
    expect(main).not.toContain('views/Trace/index.vue');
    // main.ts 不允许内联声明 /Trace/:id 组件路由，只能并入 routes.ts 的让位声明
    expect(main).not.toMatch(/path: '\/Trace\/:id'/);
    expect(main).toContain('legacyTraceRedirect');
  });

  it('F14-1：旧 MVC 链形态 /trace?id=xxx 由 TraceDetail 的 query.id 兜底（组件层已实现，此处锁约定）', () => {
    const page = readFileSync(resolve(fileURLToPath(import.meta.url), '../../../../apps/stardust/src/views/Console/TraceDetail.vue'), 'utf8');
    expect(page).toContain('query?.id');
  });
});
