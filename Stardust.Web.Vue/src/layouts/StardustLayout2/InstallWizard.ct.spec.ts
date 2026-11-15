import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

// mock 下载清单 / 探测 / 等待节点：CT 环境 request 是 noop 桩（不发网络），
// 必须走批准的 window.__ctRequestImpl 注入点（ct/mocks/request.ts 约束：新用例可用、注册-清理对称）。
// addInitScript 保证组件 watch(immediate) 首次 fetch 前注入点已就位。
async function mockApi(page: Page, opts?: { failAgent?: boolean; probeOffline?: boolean }) {
  await page.addInitScript((cfg) => {
    (window as { __ctRequestImpl?: unknown }).__ctRequestImpl = async (o?: { url?: string }) => {
      const url = o?.url || '';
      if (url === '/Agent') {
        if (cfg.failAgent) throw new Error('mock: /Agent failed');
        return {
          code: 0,
          message: null,
          data: {
            server: 'http://127.0.0.1:6681',
            agent: {
              zip: [{ name: 'staragent90.zip', framework: 'net9.0', os: 'Windows/Linux', url: 'http://127.0.0.1:6681/star/staragent90.zip' }],
              targz: [{ name: 'staragent90.tar.gz', framework: 'net9.0', os: 'Linux', url: 'http://127.0.0.1:6681/star/staragent90.tar.gz' }],
              scripts: [{ name: 'star.sh (net9.0)', description: '自动安装net9.0版', url: 'http://127.0.0.1:6681/star/star.sh' }],
            },
            packages: [{ name: 'star.zip', description: '星尘整体安装包', url: 'http://127.0.0.1:6681/star/star.zip' }],
            dotnet: [{ name: 'net9.sh', description: '.NET 9.0 运行时安装脚本', url: 'http://127.0.0.1:6681/dotnet/net9.sh' }],
          },
        };
      }
      if (url === '/Agent/Probe') {
        return {
          code: 0,
          message: null,
          data: {
            online: !cfg.probeOffline,
            data: cfg.probeOffline
              ? null
              : {
                  Name: 'local-star',
                  Title: '本机星尘代理',
                  FileVersion: '3.7.2026.1101',
                  Compile: '2026-11-10 08:30:00',
                  OS: 'Microsoft Windows 11  专业版',
                  MachineName: 'DESKTOP-STARDUST',
                  LocalIP: '10.188.82.141',
                  Server: 'http://127.0.0.1:6600',
                },
          },
        };
      }
      if (url === '/Nodes/Node') return { code: 0, message: null, data: [] };
      return { code: 0, message: null, data: null };
    };
  }, { failAgent: !!opts?.failAgent, probeOffline: !!opts?.probeOffline });
}

// 注入点清理对称（ct/mocks/request.ts 约束③）
test.afterEach(async ({ page }) => {
  await page.evaluate(() => {
    delete (window as { __ctRequestImpl?: unknown }).__ctRequestImpl;
  }).catch(() => {});
});

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(500);
}

// 视觉截图：Drawer 是 fixed 定位，截整个视口
test('InstallWizard 打开态：安装命令-下载资源五组-探测在线-等待上线空态', async ({ page }) => {
  await mockApi(page);
  await openStory(page, 'InstallWizard/Basic');
  await expect(page.locator('[data-spec="agent-dl-table-zip"]')).toBeVisible();
  await expect(page).toHaveScreenshot();
});

// 语义断言
test('InstallWizard 显示四块结构与五组资源表', async ({ page }) => {
  await mockApi(page);
  await openStory(page, 'InstallWizard/Basic');
  const wizard = page.locator('[data-spec="drawer"]');
  await expect(wizard).toBeVisible();
  await expect(wizard).toContainText('接入向导');
  await expect(wizard).toContainText('复制安装命令');
  await expect(wizard).toContainText('等待节点上线');
  await expect(wizard).toContainText('下载资源');
  await expect(wizard).toContainText('本机探测');
  await expect(page.locator('[data-spec="agent-dl-table-zip"]')).toBeVisible();
  await expect(page.locator('[data-spec="agent-dl-table-targz"]')).toBeVisible();
  await expect(page.locator('[data-spec="agent-dl-table-scripts"]')).toBeVisible();
  await expect(page.locator('[data-spec="agent-dl-table-packages"]')).toBeVisible();
  await expect(page.locator('[data-spec="agent-dl-table-dotnet"]')).toBeVisible();
});

// R3 基址消费：安装命令消费 GET /Agent?format=json 的 targz url
test('InstallWizard 安装命令基址消费清单 url', async ({ page }) => {
  await mockApi(page);
  await openStory(page, 'InstallWizard/Basic');
  const pre = page.locator('[data-spec="drawer"] pre');
  await expect(pre).toContainText('wget http://127.0.0.1:6681/star/staragent90.tar.gz');
});

// R2 探测卡：online → 8 行字段
test('InstallWizard 本机探测在线显示字段表', async ({ page }) => {
  await mockApi(page);
  await openStory(page, 'InstallWizard/Basic');
  const probeTable = page.locator('[data-spec="agent-probe-table"]');
  await expect(probeTable).toBeVisible();
  await expect(probeTable).toContainText('local-star');
  await expect(probeTable).toContainText('http://127.0.0.1:6600');
});

// 探测离线态
test('InstallWizard 本机探测离线显示未检测到', async ({ page }) => {
  await mockApi(page, { probeOffline: true });
  await openStory(page, 'InstallWizard/Basic');
  await expect(page.locator('[data-spec="agent-probe-offline"]')).toContainText('未检测到本机 StarAgent');
});

// R1 三态：清单失败 → failed + 重试
test('InstallWizard 下载资源失败显示重试', async ({ page }) => {
  await mockApi(page, { failAgent: true });
  await openStory(page, 'InstallWizard/Basic');
  await expect(page.locator('[data-spec="agent-dl-failed"]')).toBeVisible();
  await expect(page.locator('[data-spec="agent-dl-retry"]')).toBeVisible();
});

test('InstallWizard 有复制按钮', async ({ page }) => {
  await mockApi(page);
  await openStory(page, 'InstallWizard/Basic');
  await expect(page.locator('[data-spec="wizard-copy"]')).toBeVisible();
});

test('InstallWizard 关闭态不可见', async ({ page }) => {
  await mockApi(page);
  await openStory(page, 'InstallWizard/Closed');
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});

test('InstallWizard 点 X 关闭', async ({ page }) => {
  await mockApi(page);
  await openStory(page, 'InstallWizard/Basic');
  await page.locator('[data-spec="drawer-close"]').click();
  await expect(page.locator('[data-spec="drawer"]')).toBeHidden();
});
