import type { EnvConfig } from '@newlifex/cube-vue/core/configure/types';

export const config: EnvConfig = {
  request: {
    // 留空时走同源相对路径，由 Vite 代理转发到后端（默认 http://localhost:6680），避免跨域。
    baseUrl: import.meta.env.VITE_APP_BASE_API || 'http://localhost:6681',
    timeout: 30000,
  },
};
