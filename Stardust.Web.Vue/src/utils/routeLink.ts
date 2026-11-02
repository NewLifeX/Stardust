// src/utils/routeLink.ts
// 统一资源路由工具：星尘前端所有业务页面的「链接生成」与「页面跳转」收口到本文件。
// 背景：早期页面各自手拼 `#/Area/Controller?key=val`（hash 模式遗留），切 history 后全部失效。
// 约定：
//  - 生成链接（表格行 :href、URL 字符串）→ toUrl(path, query)
//  - 执行跳转（window.location.hash、window.open、点击跳转）→ go(path, query) 或 goUrl(url)
// 禁止在业务代码中再出现裸 `#/` 字面量。
import { gotoPage } from '@newlifex/cube-vue/core/utils/router';

/** query 值类型：可转字符串的基本值 */
export type RouteQueryValue = string | number | boolean | null | undefined;

/**
 * 构造 history 路由 URL（无 # 前缀），query 对象自动 URL 编码。
 * @example toUrl('/Configs/ConfigData', { configId: 3 }) → '/Configs/ConfigData?configId=3'
 */
export function toUrl(path: string, query?: Record<string, RouteQueryValue>): string {
  const base = path.startsWith('/') ? path : `/${path}`;
  if (!query) return base;
  const qs = Object.entries(query)
    .filter(([, v]) => v !== null && v !== undefined && v !== '')
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
    .join('&');
  return qs ? `${base}?${qs}` : base;
}

/**
 * 跳转到 history 路由（内部走框架 gotoPage = window.router.push）。
 * @example go('/Registry/App/Detail', { id: 3 })
 */
export function go(path: string, query?: Record<string, RouteQueryValue>): void {
  gotoPage(toUrl(path, query));
}

/**
 * 按完整 URL 跳转（已含 query 或外部地址），等价 window.router.push(url)。
 */
export function goUrl(url: string): void {
  gotoPage(url);
}

/** 在新窗口打开 history 路由 */
export function openInNewTab(path: string, query?: Record<string, RouteQueryValue>): void {
  window.open(toUrl(path, query), '_blank');
}
