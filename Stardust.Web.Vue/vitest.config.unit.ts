/**
 * 最小化 Vitest 配置（仅用于单元 / 组件测试）
 *
 * 为什么不用项目根 vite.config.ts？
 *   根配置加载了 cubeFront() / AutoImport / Components / vueDevTools 等重型插件，
 *   在 CI / 沙箱环境下会导致 Vite 开发服务器初始化卡死（worker RPC timeout）。
 *   本配置仅保留 @vitejs/plugin-vue + jsdom，足以驱动纯逻辑单测与组件渲染测试，
 *   且不会拉起全量插件扫描。
 *
 * 与 Cube web 侧（Cube/NewLife.Cube.Vue/web/vitest.config.unit.ts）的差异：
 *   1. alias 用本项目的 `@` → ./src（Cube 侧是 `@newlifex/cube-vue` → 自身根）。
 *   2. 同时保留 `@newlifex/cube-vue` → Cube web 真实源码目录（与 vite.config.ts 一致），
 *      这样测试里 import Cube 组件/工具时解析到同一份源码，不走 dist 产物。
 *   3. 额外排除 e2e/ 与 *.ct.spec.ts（Playwright 组件测试）避免 runner 串扰。
 */
import { fileURLToPath, URL } from 'node:url';
import { defineConfig } from 'vitest/config';
import vue from '@vitejs/plugin-vue';

/**
 * 桩插件：为 cubeFront() 框架插件提供的虚拟模块 `virtual:@newlifex/cube-vue-config`
 * 提供最小实现。该虚拟模块在运行时由框架插件生成（来自 configs/config.*.ts），
 * 但这里只做单元测试，getConfig() 在 configData={} 时会回退到 defaultConfig，
 * 足以驱动组件渲染，且不会触发任何网络请求。
 */
function cubeVirtualConfigStub() {
  const id = 'virtual:@newlifex/cube-vue-config';
  return {
    name: 'cube-virtual-config-stub',
    resolveId(source: string) {
      return source === id ? source : null;
    },
    load(loadId: string) {
      if (loadId === id) {
        return `export const configData = {}; export const currentEnv = 'development'; export default { configData, currentEnv };`;
      }
      return null;
    },
  };
}

export default defineConfig({
  plugins: [cubeVirtualConfigStub(), vue()],
  resolve: {
    // 显式声明扩展名，确保 .vue 单文件组件能被解析（最小化配置下
    // @vitejs/plugin-vue 的扩展名自动注入可能不生效）
    extensions: ['.mjs', '.js', '.mts', '.ts', '.jsx', '.tsx', '.json', '.vue'],
    alias: {
      // 本项目源码别名（与 web/vite.config.ts 一致）
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      // Cube web 框架真实源码目录（与 vite.config.ts 一致），避免测试与运行时解析到不同产物
      '@newlifex/cube-vue': fileURLToPath(new URL('../Cube/NewLife.Cube.Vue/web', import.meta.url)),
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    include: [
      'src/**/*.{spec,test}.ts',
      'apps/**/*.{spec,test}.ts',
    ],
    // 重要：排除 Playwright 组件测试（*.ct.spec.ts）。
    // 因 include 的 `*.{spec,test}.ts` 会误收 `*.ct.spec.ts`（以 .spec.ts 结尾），
    // 不显式排除会导致 Vitest 尝试加载 @playwright/test 的 test，与 Playwright runner 串扰。
    exclude: [
      '**/node_modules/**',
      '**/dist/**',
      'e2e/**',
      '**/*.ct.spec.ts',
      '**/.{idea,git,cache}/**',
    ],
    testTimeout: 20000,
    hookTimeout: 20000,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'html', 'lcov'],
      reportsDirectory: './coverage',
      // 覆盖率门槛（对齐 Cube web 侧：statements 80 / branches 75 / functions 80 / lines 80）
      //
      // 门槛按「已建立测试的模块」统计（include），不是全仓库统计。
      // 原因：测试基建是新建的，全仓库还有大量未纳入测试的历史页面
      // （appsModel.ts / nodes.model.ts 等），若用全量口径，
      // 新建的 test:coverage 一跑就红，门槛失去「防止覆盖率倒退」的意义。
      //
      // include 只列**当前已有测试覆盖**的模块，且必须精确到文件：
      // 用目录通配（src/layouts/StardustLayout2/**）会把同目录下尚无测试的
      // avatar.logic / useToast / routes 等一并拉进来，把覆盖率拉低并误判为「倒退」。
      //
      // 新增模块并补上测试后，把对应文件追加到下面的 include 即可纳入门槛。
      include: [
        // —— src/layouts/StardustLayout2（纯逻辑 + store）——
        'src/layouts/StardustLayout2/hotkeys.logic.ts',
        'src/layouts/StardustLayout2/dock.logic.ts',
        'src/layouts/StardustLayout2/palette.logic.ts',
        'src/layouts/StardustLayout2/palette.items.ts',
        'src/layouts/StardustLayout2/terminal.logic.ts',
        'src/layouts/StardustLayout2/toast.logic.ts',
        'src/layouts/StardustLayout2/ui.store.ts',
        'src/layouts/StardustLayout2/useGlobalHotkeys.ts',
        'src/layouts/StardustLayout2/CommandPalette/usePaletteState.ts',
        'src/layouts/StardustLayout2/ship.logic.ts',
        'src/layouts/StardustLayout2/projectMember.logic.ts',
        'src/layouts/StardustLayout2/appTracer.logic.ts',
        'src/layouts/StardustLayout2/daystat.logic.ts',
        'src/layouts/StardustLayout2/deploy.logic.ts',
        'src/layouts/StardustLayout2/appConfig.logic.ts',
        'src/layouts/StardustLayout2/configCenter.logic.ts',
        'src/layouts/StardustLayout2/apps.logic.ts',
        'src/layouts/StardustLayout2/mesh.logic.ts',
        'src/layouts/StardustLayout2/appInstances.logic.ts',
        'src/stores/contextParams.logic.ts',
        'apps/stardust/src/views/Stardust/appsModel.ts',
        'apps/stardust/src/views/Stardust/nodes.model.ts',
      ],
      thresholds: {
        // 门槛 = 当前真实基线（纳入 appConfig/configCenter/apps/mesh/appInstances.logic 后 2026-11-03 实测：
        // statements 80.31 / branches 67.65 / functions 87.15 / lines 82.15），
        // 略留余量但卡住「覆盖率倒退」。
        statements: 80,
        branches: 67,
        functions: 87,
        lines: 82,
      },
      exclude: [
        'node_modules/',
        '**/*.d.ts',
        '**/main.ts',
        '**/*.{spec,test}.ts',
        '**/*.ct.spec.ts',
        '**/*.story.ts',
        'e2e/**',
        'ct/**',
        'dist/',
      ],
    },
  },
});
