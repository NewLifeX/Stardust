import type { EnvConfig } from '@newlifex/cube-vue/core/configure/types';

export const config: EnvConfig = {
  base: {
    title: '星枢 · 无界星图',
    footer: '版权所有 © 2026',
  },
  ui: {
    theme: {
      primaryColor: '#1890ff',
    },
  },
  auth: {
    oauthUrl: '/login',
    getUserInfoAxiosConfig: {
      url: '/Admin/User/Info',
      method: 'GET',
    },
  },
  router: {
    history: 'history',
  },
};
