// CT 用 request 桩：组件（如 CubeTable / LovSelect）会 import request，但 CT 场景不依赖真实后端，
// 用 noop 替代即可，避免拉入 axios/universal-cookie 等重依赖链。
const noop = async () => ({ data: null, code: 0, msg: '' });

/** 失败路径注入点（2026-11-09 lead 批准，三条约束）：
 *  ① 未注册 window.__ctRequestImpl 时走默认 noop，行为与历史桩一致（零变化）；
 *  ② 只允许新写/改写用例使用，存量基线用例不回溯改；
 *  ③ 用例内注册-清理对称（收尾必须删除该键），防跨用例污染。
 *  注册形态：await page.evaluate(() => { (window as any).__ctRequestImpl = async () => { throw ... }; }) */
export const request = async (options?: unknown) => {
  const impl = (globalThis as { __ctRequestImpl?: (o?: unknown) => Promise<unknown> }).__ctRequestImpl;
  if (impl) return impl(options);
  return noop();
};

// 真实 request 模块同时导出 default 与具名 `request`（Cube request.ts: `export { request, client }`）。
// 组件里 `import { request } from '@newlifex/cube-vue/core/utils/request'` 依赖这个具名导出，
// 缺了它在 gallery 里 request 为 undefined，组件 setup 一调用就抛错、整页白屏。

// stores/stardustContext.ts 在模块加载时调用 cubeAxios.interceptors.request.use；
// NowPage.story 经 NowPage.vue 引入该模块，缺了这个具名导出 gallery 会在 ESM 链接阶段白屏。
// 2026-11-10：CARD-MON-08 在 stardustContext.ts 模块加载期新增 cubeAxios.defaults.timeout 赋值，
// 桩补 defaults（默认无 timeout 字段，赋值即生效无副作用）——静态导入链缺一个面就全仓 story 白屏。
export const cubeAxios = { defaults: {}, interceptors: { request: { use(): void {} } } };

export default { get: noop, post: noop, put: noop, delete: noop, request: noop };
export const get = noop;
export const post = noop;
export const put = noop;
export const del = noop;
