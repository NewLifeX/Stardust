import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import vueJsx from '@vitejs/plugin-vue-jsx';
import cubeFront from '@newlifex/cube-vue/core/plugin';

/**
 * 2026-10-14：前端**直连后端**，已移除 Vite 代理。
 *
 * - API 基地址由 .env.development 的 VITE_APP_BASE_API 提供，且**仅承载后端主机、不含 /api**。
 *   实体接口的 /api 前缀由请求层自行拼装（Cube/NewLife.Cube.Vue/web/core/utils/url.ts routeToApiPrefix）；
 *   服务接口（/Auth /Sso /Mfa /OAuth /Cube 服务动作）不带前缀。
 * - 后端已开启**支持凭证的跨域**（预检返回 Access-Control-Allow-Origin 回显来源 +
 *   Access-Control-Allow-Credentials: true，见 Stardust.WebApi/Startup.cs），
 *   故不再需要 /api、/Auth、/Sso、/Mfa、/OAuth、/Cube 六条代理规则。
 */
export default defineConfig(() => {
  return {
    plugins: [
      vue(),
      vueJsx(),
      cubeFront(),
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
        // 别名指向 Cube web 源码目录，解决 workspace 包内自引用（@newlifex/cube-vue/core/...）解析失败问题。
        // 注意：真实路径为 Cube/NewLife.Cube.Vue/web（此前多写了一层 NewLife.Cube，指向 .NET 工程子目录，已失效）。
        '@newlifex/cube-vue': fileURLToPath(new URL('../Cube/NewLife.Cube.Vue/web', import.meta.url)),
      },
    },
    optimizeDeps: {
      // api-core 的登录结果归一化已本地修复（兼容 ApiResponse 包装），
      // 依赖缓存已含修复，无需每次强制重优化（避免启动时的批量删除触发沙箱防护）。
    },
    server: {
      port: 5188,
      host: '0.0.0.0',
      // 无代理：页面直接请求 VITE_APP_BASE_API 指定的后端主机（跨域由后端 CORS 策略放行）。
    },
    build: {
      outDir: 'dist',
      sourcemap: true,
      target: 'es2022',
    },
  };
});
