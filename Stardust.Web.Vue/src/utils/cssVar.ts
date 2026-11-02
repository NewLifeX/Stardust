/**
 * 读取 CSS 自定义属性（设计令牌）的运行时值。
 * 用途：ECharts 等 Canvas 渲染无法解析 var()，需在运行时取到已求值的令牌值。
 * 令牌缺失时回退到传入的 fallback，保证图表仍可渲染。
 */
export function cssVar(name: string, fallback = ""): string {
  if (typeof window === "undefined") return fallback;
  const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return v || fallback;
}
