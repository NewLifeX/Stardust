// 与 playwright.config.ts 中保持一致，供用例复用。
export const STORAGE_STATE = 'e2e/.auth/user.json';

// 前端开发服务器（Vite）
export const FRONTEND_URL = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5188';

// 后端（StarWebApi）—— 前端**直连**该主机，不再经 Vite 代理
export const BACKEND_URL = process.env.BACKEND_URL || 'http://localhost:6681';

// 后端登录账号（本地空库默认 admin/admin）
export const BACKEND_USER = process.env.BACKEND_USER || 'admin';
export const BACKEND_PASS = process.env.BACKEND_PASS || 'admin';

// 前端请求层的「业务接口」路径前缀：
//   /api   —— 实体接口（由请求层拼装）
//   其余  —— Cube 服务接口，不带 /api
export const API_PATH_PREFIXES = ['/api/', '/Auth/', '/Sso/', '/Mfa/', '/OAuth/', '/Cube/'];

/** 判断某次请求是否属于「业务接口」。 */
export function isApiPath(pathname: string): boolean {
  return API_PATH_PREFIXES.some((p) => pathname.startsWith(p));
}

// 菜单项选择器（无界星图自定义菜单，非 Element Plus 的 .el-menu）
export const MENU_ITEM_SELECTOR = '.cosmic-menu .menu-item';
// 一级菜单项：出现在 .menu-children 嵌套之外的菜单项
export const TOP_MENU_SELECTOR = '.cosmic-menu .menu-item';
