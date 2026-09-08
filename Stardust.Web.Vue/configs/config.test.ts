import type { EnvConfig } from '@newlifex/cube-vue/core/configure/types';

export const config: EnvConfig = {
  request: {
    baseUrl: import.meta.env.VITE_APP_BASE_API || '/api',
    timeout: 30000,
  },
};
