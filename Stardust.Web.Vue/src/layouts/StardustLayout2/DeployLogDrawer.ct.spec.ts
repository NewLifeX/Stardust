import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// 注入点清理对称（ct/mocks/request.ts 约束③，InstallWizard 范式）
test.afterEach(async ({ page }) => {
  await page.evaluate(() => {
    delete (window as { __ctRequestImpl?: unknown }).__ctRequestImpl;
  }).catch(() => {});
});

// task-90：未配置日志目录 → 配置引导空态，不渲染文件列表，不发请求
test('未配置：渲染配置引导空态（StarAgent 日志目录 + 空=跟随部署集），文件列表不出现', async ({ page }) => {
  await openStory(page, 'DeployLogDrawer/Unconfigured');
  const guide = page.locator('[data-spec="deploy-log-config-empty"]');
  await expect(guide).toBeVisible();
  await expect(guide).toContainText('读取 StarAgent 节点上配置的日志目录');
  await expect(guide).toContainText('空 = 跟随部署集');
  await expect(page.locator('[data-spec="deploy-log-configure"]')).toHaveText('配置日志目录');
  // 未配置时不应出现请求驱动的文件列表视图
  await expect(page.locator('[data-spec="deploy-log-file-select"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="deploy-log-content"]')).toHaveCount(0);
});

// task-90：未配置时不发请求（request 计数器 = 0），点按钮上抛 configure
test('未配置：不发请求；点「配置日志目录」上抛 configure', async ({ page }) => {
  await page.addInitScript(() => {
    (window as { __logReqCalls?: number }).__logReqCalls = 0;
    (window as { __ctRequestImpl?: unknown }).__ctRequestImpl = async () => {
      (window as { __logReqCalls?: number }).__logReqCalls = ((window as { __logReqCalls?: number }).__logReqCalls ?? 0) + 1;
      return { code: 0, message: null, data: null };
    };
  });
  await openStory(page, 'DeployLogDrawer/Unconfigured');
  // 打开即计数 0：前置拦截生效（打开 watch 不会触发 LogFiles 请求）
  expect(await page.evaluate(() => (window as { __logReqCalls?: number }).__logReqCalls)).toBe(0);
  await page.locator('[data-spec="deploy-log-configure"]').click();
  const configured = await page.evaluate(() => (window as unknown as { __logConfigure?: number[] }).__logConfigure);
  expect(configured).toHaveLength(1);
  // 配置引导本身也不发请求
  expect(await page.evaluate(() => (window as { __logReqCalls?: number }).__logReqCalls)).toBe(0);
});

// task-90：生效日志目录空 → 非空（保存节点后重算）→ watch 自动拉取并清错误
test('logDir 空→非空：自动加载文件列表（不发 configure 之外的手动刷新）', async ({ page }) => {
  await page.addInitScript(() => {
    (window as { __ctRequestImpl?: unknown }).__ctRequestImpl = async (o?: { url?: string }) => {
      if (o?.url === '/Deployment/AppDeployNode/LogFiles') {
        return { code: 0, message: null, data: { files: [{ Name: 'app-20261112.log', Size: 120, LastWriteUtc: '2026-11-12 10:00:00' }] } };
      }
      if (o?.url === '/Deployment/AppDeployNode/LogTail') {
        return { code: 0, message: null, data: { Content: '2026-11-12 10:00:00 INFO ready', Offset: 0, FileSize: 120, Truncated: false } };
      }
      return { code: 0, message: null, data: null };
    };
  });
  await openStory(page, 'DeployLogDrawer/Unconfigured');
  await expect(page.locator('[data-spec="deploy-log-config-empty"]')).toBeVisible();
  // 模拟保存节点后 refreshSoon 刷新 → logDrawerNode.logDirectory 有值 → effectiveLogDir 重算
  await page.evaluate(() => {
    const w = window as unknown as { setStoryProps: (patch: Record<string, unknown>) => void };
    w.setStoryProps({ logDir: '/var/log/star' });
  });
  await expect(page.locator('[data-spec="deploy-log-config-empty"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="deploy-log-file-select"]')).toContainText('app-20261112.log');
  await expect(page.locator('[data-spec="deploy-log-content"]')).toContainText('INFO ready');
});

// 已配置：正常文件列表视图（CT 宿主 request 默认 noop 桩 → 「无匹配文件」空列表，不发网络）
test('已配置：文件列表视图渲染（无匹配文件 + 内容占位）', async ({ page }) => {
  await openStory(page, 'DeployLogDrawer/Configured');
  await expect(page.locator('[data-spec="deploy-log-drawer"]')).toBeVisible();
  await expect(page.locator('[data-spec="deploy-log-config-empty"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="deploy-log-file-select"]')).toContainText('无匹配文件');
  await expect(page.locator('[data-spec="deploy-log-refresh"]')).toBeEnabled();
});

// 视觉基线：未配置空态 + 已配置视图（首次生成后复跑验证稳定）
test('未配置空态：视觉基线（首次生成后复跑验证稳定）', async ({ page }) => {
  await openStory(page, 'DeployLogDrawer/Unconfigured');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});

test('已配置视图：视觉基线（首次生成后复跑验证稳定）', async ({ page }) => {
  await openStory(page, 'DeployLogDrawer/Configured');
  await expect(page.locator('.gal-preview')).toHaveScreenshot();
});
