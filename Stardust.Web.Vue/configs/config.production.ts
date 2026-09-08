import type { EnvConfig } from '@newlifex/cube-vue/core/configure/types';

export const config: EnvConfig = {
  request: {
    baseUrl: '${BUILD_REQUEST_BASE_URL}',
    timeout: 30000,
  },
};
