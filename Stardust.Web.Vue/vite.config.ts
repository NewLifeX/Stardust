import { fileURLToPath, URL } from 'node:url';
import { defineConfig, loadEnv } from 'vite';
import vue from '@vitejs/plugin-vue';
import vueJsx from '@vitejs/plugin-vue-jsx';
import cubeFront from '@newlifex/cube-vue/core/plugin';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

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
      // 前端 API 走同源相对路径；除 Vite 开发资源外，其余请求全部代理到后端（默认 http://localhost:6681）。
      // cube-vue 请求层对服务类接口（/Auth /Sso /Mfa /OAuth 与 /Cube 下的服务动作）会去掉 /api 前缀直连，
      // 因此这些前缀也必须代理到后端，否则菜单(/Cube/MenuTree)、登录(/Auth/Login) 等会落到 Vite 返回空白页。
      proxy: {
        '/api': {
          target: env.VITE_PROXY_TARGET || 'http://localhost:6681',
          changeOrigin: true,
        },
        '/Auth': {
          target: env.VITE_PROXY_TARGET || 'http://localhost:6681',
          changeOrigin: true,
        },
        '/Sso': {
          target: env.VITE_PROXY_TARGET || 'http://localhost:6681',
          changeOrigin: true,
        },
        '/Mfa': {
          target: env.VITE_PROXY_TARGET || 'http://localhost:6681',
          changeOrigin: true,
        },
        '/OAuth': {
          target: env.VITE_PROXY_TARGET || 'http://localhost:6681',
          changeOrigin: true,
        },
        '/Cube': {
          target: env.VITE_PROXY_TARGET || 'http://localhost:6681',
          changeOrigin: true,
        },
      },
    },
    build: {
      outDir: 'dist',
      sourcemap: true,
      target: 'es2022',
    },
  };
});
