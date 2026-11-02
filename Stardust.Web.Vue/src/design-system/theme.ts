/**
 * 主题切换（暗色 / 浅色）—— 按设计交接包 colors_and_type.css §Light theme override
 * ---------------------------------------------------------------------------
 * 设计真源：design-handoff/design2/colors_and_type.css
 *   默认暗色；[data-theme='light'] 切换为亮色主题。
 *   配套：tokens.stardust.css 的 [data-theme='light'] 令牌块 + element-theme-override.css
 *        的 html.dark 分支（Element Plus 暗色 --el-* 定义在 html.dark 下）。
 *
 * 关键点：Element Plus 的暗色变量挂在 `html.dark` 选择器上，因此浅色模式必须
 *         同时移除 `.dark` class 与 `data-theme='light'` 属性，两者缺一不可。
 */
import { ref } from 'vue'

export type ThemeName = 'dark' | 'light'

export const THEME_KEY = 'stardust-theme'

/** 当前主题（响应式，供顶栏切换按钮绑定） */
export const theme = ref<ThemeName>('dark')

/** 读取本地主题偏好（容错：读取失败或非法值回落暗色） */
export function loadTheme(): ThemeName {
  try {
    return localStorage.getItem(THEME_KEY) === 'light' ? 'light' : 'dark'
  } catch {
    return 'dark'
  }
}

/** 应用主题：写 data-theme 属性 + 切 .dark class + color-scheme，并持久化 */
export function applyTheme(name: ThemeName): void {
  theme.value = name
  const el = document.documentElement
  el.setAttribute('data-theme', name)
  el.classList.toggle('dark', name === 'dark')
  el.style.colorScheme = name
  try {
    localStorage.setItem(THEME_KEY, name)
  } catch {
    /* 隐私模式等场景忽略持久化失败 */
  }
}

/** 应用启动时初始化（main.ts 调用），返回生效主题 */
export function initTheme(): ThemeName {
  const name = loadTheme()
  applyTheme(name)
  return name
}

/** 切换主题并返回新主题 */
export function toggleTheme(): ThemeName {
  const next: ThemeName = theme.value === 'dark' ? 'light' : 'dark'
  applyTheme(next)
  return next
}
