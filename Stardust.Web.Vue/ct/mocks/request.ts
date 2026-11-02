// CT 用 request 桩：组件（如 CubeTable / LovSelect）会 import request，但 CT 场景不依赖真实后端，
// 用 noop 替代即可，避免拉入 axios/universal-cookie 等重依赖链。
const noop = async () => ({ data: null, code: 0, msg: '' });

// 真实 request 模块同时导出 default 与具名 `request`（Cube request.ts: `export { request, client }`）。
// 组件里 `import { request } from '@newlifex/cube-vue/core/utils/request'` 依赖这个具名导出，
// 缺了它在 gallery 里 request 为 undefined，组件 setup 一调用就抛错、整页白屏。
export const request = noop;

// stores/stardustContext.ts 在模块加载时调用 cubeAxios.interceptors.request.use；
// NowPage.story 经 NowPage.vue 引入该模块，缺了这个具名导出 gallery 会在 ESM 链接阶段白屏。
export const cubeAxios = { interceptors: { request: { use(): void {} } } };

export default { get: noop, post: noop, put: noop, delete: noop, request: noop };
export const get = noop;
export const post = noop;
export const put = noop;
export const del = noop;
