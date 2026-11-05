import type { EnvConfig } from '@newlifex/cube-vue/core/configure/types';

export const config: EnvConfig = {
  request: {
    // 留空 = 与后端同源（推荐形态）：Stardust.Web.Vue 的部署载体是 StarWebApi 内嵌 DLL（前后端必然同域）。
    // 不再使用 ${BUILD_REQUEST_BASE_URL} 占位符——它以字面量进入产物，需要部署载体（如魔方容器 enterpoint.sh）
    // 在启动时 sed 替换；DLL 内嵌部署没有该入口。将来如需跨域网关部署，再恢复占位符并补替换步骤。
    baseUrl: '',
    timeout: 30000,
  },
};
