// StardustLayout2 顶栏分段导航的纯逻辑（无 Vue 依赖，可直接用 node --experimental-strip-types 测试）。
// 数据来源：设计交接包 console/kit-shell.jsx 的 PAGES。路由不加前缀，沿用原型语义名。
import type { KIconName } from './icons';

export type NavKey = 'now' | 'apps' | 'nodes' | 'changes' | 'mesh';

export interface NavItem {
  key: NavKey;
  label: string;
  icon: KIconName;
  path: string;
}

export const NAV_ITEMS: readonly NavItem[] = [
  { key: 'now', label: '此刻', icon: 'Sparkles', path: '/now' },
  { key: 'apps', label: '应用', icon: 'Boxes', path: '/apps' },
  { key: 'nodes', label: '节点', icon: 'Server', path: '/nodes' },
  { key: 'changes', label: '变更', icon: 'Refresh', path: '/changes' },
  { key: 'mesh', label: '治理', icon: 'Shield', path: '/mesh' },
];

/**
 * 由当前路径解析高亮的导航项。
 * 应用工作台 /app/:name/:tab? 归入「应用」（原型：p.key === 'apps' && route.page === 'app'）。
 * 不属于五项之一的路径（如旧 /stardust/*）返回 null，导航不高亮。
 */
export function resolveActiveNav(path: string): NavKey | null {
  const first = path.split('?')[0].split('#')[0].split('/').filter(Boolean)[0] ?? '';
  if (first === 'app') return 'apps';
  const hit = NAV_ITEMS.find((n) => n.key === first);
  return hit ? hit.key : null;
}
