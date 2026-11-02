import { expect, type Page } from './fixtures';

/**
 * 进入一个 cube-vue 叶子页（列表 / 对象 / 自定义视图均可）。
 * 路由为 history 模式：/<path>，path 如 Platform/GalaxyProject 或 Admin/Index/Main。
 * 菜单路由在登录后异步注册，beforeEach 守卫会重导航，故需等待主区域渲染出内容。
 */
export async function gotoLeafPage(page: Page, path: string): Promise<void> {
  await page.goto(`/${path.replace(/^\/+/, '')}`, { waitUntil: 'domcontentloaded' });
  // 等待列表页 / 对象页 / 自定义视图（主区域有渲染内容）出现
  await page.waitForFunction(
    () => {
      const c = document.querySelector('main.cosmic-content');
      if (!c) return false;
      // AutoPage 分流中的「加载中」占位不算渲染完成
      if (c.querySelector('.autopage-loading')) return false;
      if (c.querySelector('.list-page')) return true;
      if (c.querySelector('.object-page .form-content')) return true;
      // 自定义视图：主区域内有实际渲染内容（且非加载占位）
      return c.children.length > 0 && (c.innerText ?? '').trim().length > 0;
    },
    { timeout: 25_000 },
  );
}

/** 断言列表页已渲染：标题 + 表格可见。 */
export async function assertListRendered(page: Page): Promise<void> {
  await expect(page.locator('main.cosmic-content .list-page .lph-title').first()).toBeVisible({ timeout: 25_000 });
  await expect(page.locator('main.cosmic-content .el-table').first()).toBeVisible({ timeout: 25_000 });
  // 等首屏数据/骨架稳定
  await page.waitForTimeout(300);
}

/**
 * 断言叶子页已渲染（兼容「列表页」「单对象/配置页」「自定义视图」）。
 * 列表页：.list-page 可见；对象页：.object-page .form-content 可见；
 * 自定义视图（如操作面板）：主区域内有渲染内容。
 */
export async function assertPageRendered(page: Page): Promise<'list' | 'object' | 'custom'> {
  const isList = await page.locator('main.cosmic-content .list-page').first().isVisible().catch(() => false);
  if (isList) return 'list';
  const isObject = await page.locator('main.cosmic-content .object-page .form-content').first().isVisible().catch(() => false);
  if (isObject) return 'object';
  const hasCustom = await page
    .$eval('main.cosmic-content', (el) => el.children.length > 0 && (el.innerText ?? '').trim().length > 0)
    .catch(() => false);
  if (hasCustom) return 'custom';
  throw new Error('页面既未渲染列表也未渲染对象表单或自定义视图');
}

/** 读取列表首屏数据行数（>=0）。对象页返回 0。 */
export async function getRowCount(page: Page): Promise<number> {
  return page.locator('main.cosmic-content .el-table__body-wrapper tbody tr').count();
}

/**
 * 收集页面关键网络/控制台错误（过滤已知可忽略项）。
 * 返回 { errors: string[], consoleErrors: string[] }
 */
export async function collectErrors(page: Page): Promise<{ errors: string[]; consoleErrors: string[] }> {
  const errors: string[] = [];
  const consoleErrors: string[] = [];
  page.on('response', (res) => {
    const s = res.status();
    if (s >= 400 && !res.url().includes('/Cube/Avatar/')) {
      errors.push(`${s} ${res.url()}`);
    }
  });
  page.on('console', (msg) => {
    // 过滤已知无害噪音：头像 404 的 "Failed to load resource" 通用报错（无 URL，靠上方 response 捕获）；
    // 真正的接口/资源错误会以带 URL 的 response 形式进入 errors。
    if (msg.type() === 'error' && !msg.text().includes('/Cube/Avatar/') && !msg.text().includes('Failed to load resource')) {
      consoleErrors.push(msg.text());
    }
  });
  return { errors, consoleErrors };
}
