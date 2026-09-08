/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<Record<string, unknown>, Record<string, unknown>, unknown>
  export default component
}

// Cube 框架内部依赖 vue-json-editor（其自身 node_modules 含 .d.vue.ts 但未随 package.json
// "exports" 暴露）。本项目不直接使用该包，仅兜底声明使 Cube 源码在 vue-tsc 下可解析。
declare module 'vue-json-editor' {
  import type { DefineComponent } from 'vue'
  const editor: DefineComponent<Record<string, unknown>, Record<string, unknown>, unknown>
  export default editor
}

// @newlifex/cube-vue 的类型由 tsconfig paths 指向 Cube 真实源码获得（见 tsconfig.json），
// 业务可拿框架真实类型。框架内部历史遗留的 unused 变量（TS6133）在 tsconfig 关闭
// noUnusedLocals/Parameters 忽略，由 eslint 兜底。

// 以下为 cubeFront 插件在构建期生成的 virtual 模块声明（非框架可解析导出，仍需在此兜底）。
// Stardust 当前未使用 virtual 模块，但保留以防插件或未来用法；Window 增强无副作用。
declare module 'virtual:@newlifex/cube-vue-app' {
  import type { DefineComponent } from 'vue';
  const App: DefineComponent;
  export { App };
}
declare module 'virtual:@newlifex/cube-vue-routes' {
  import type { RouteRecordRaw } from 'vue-router';
  const routes: RouteRecordRaw[];
  export default routes;
}
declare module 'virtual:@newlifex/cube-vue-micro-apps' {
  const appConfigs: any[];
  export default appConfigs;
}
declare module 'virtual:@newlifex/cube-vue-config' {
  const configData: Record<string, string>;
  const currentEnv: string;
  const config: { configData: Record<string, string>; currentEnv: string };
  export { configData, currentEnv };
  export default config;
}
declare module 'virtual:@newlifex/cube-vue-sections' {
  const modules: Record<string, () => Promise<{ default: unknown }>>;
  export default modules;
}
interface Window {
  router: import('vue-router').Router;
  store: import('pinia').Pinia;
  getDvaApp?: () => { _store: { getState: () => any } };
  getCurrentOrganizationId?: () => string | undefined;
}
