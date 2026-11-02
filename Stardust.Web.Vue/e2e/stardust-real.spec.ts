import { test, expect, type Page } from './fixtures';
import type { Request } from '@playwright/test';
import { BACKEND_URL, isApiPath } from './config';

/**
 * 星尘前端**真实数据**端到端用例（无桩）。
 *
 * 前提：前端直连后端（Vite 代理移除），后端 http://localhost:6681 支持带凭证的跨域。
 *
 * ⚠️ 就绪判据（2026-10-14 修正，同类错误已犯 7 次）：
 *   真实后端单请求 2-3s，页面 20 余路并发下膨胀到 15-35s。因此
 *   **「主内容区有文字」不能作为就绪判据**（加载态与空态文案本身就满足它），
 *   且**监听器必须在 page.goto 之前挂载**——否则请求先于监听器发出，inflight 恒为 0，
 *   等待会立刻「已稳定」返回，测试在数据到达前就判通过（曾导致平台管理四个 Tab 全 0 行）。
 */

const SHOT_DIR = 'e2e/screenshots/design-real';

/**
 * 业务请求追踪器。**必须在 page.goto 之前创建。**
 *
 * 先等「至少观测到一个业务请求」，再等 inflight 归零并稳定 3 个采样点（3s），
 * 避免空转早退。
 */
function trackApi(page: Page) {
  let inflight = 0;
  let seen = 0;
  const up = (r: Request) => {
    if (isApiPath(new URL(r.url()).pathname)) {
      inflight += 1;
      seen += 1;
    }
  };
  const down = (r: Request) => {
    if (isApiPath(new URL(r.url()).pathname)) inflight = Math.max(0, inflight - 1);
  };
  page.on('request', up);
  page.on('response', down);
  page.on('requestfailed', down);
  const off = () => {
    page.off('request', up);
    page.off('response', down);
    page.off('requestfailed', down);
  };
  return {
    get seen() {
      return seen;
    },
    get inflight() {
      return inflight;
    },
    async settled(maxMs = 180_000): Promise<void> {
      const t0 = Date.now();
      let quiet = 0;
      try {
        while (Date.now() - t0 < maxMs) {
          await page.waitForTimeout(1_000);
          if (seen === 0) {
            quiet = 0;
            continue;
          }
          if (inflight <= 0) {
            quiet += 1;
            if (quiet >= 3) return;
          } else {
            quiet = 0;
          }
        }
      } finally {
        off();
      }
    },
    off,
  };
}

/** 等待主内容区出现实际渲染内容（非加载占位）。 */
async function waitContent(page: Page, timeout = 30_000): Promise<void> {
  await page.waitForFunction(
    () => {
      const c = document.querySelector('main.cosmic-content');
      if (!c) return false;
      if (c.querySelector('.autopage-loading')) return false;
      return (c.innerText ?? '').trim().length > 30;
    },
    { timeout },
  );
}

test('直连契约：业务接口全部打到后端主机（不落回 Vite）', async ({ page }) => {
  const apiHits: string[] = [];
  const offHost: string[] = [];

  page.on('request', (r) => {
    const u = new URL(r.url());
    if (!isApiPath(u.pathname)) return;
    apiHits.push(r.url());
    if (u.origin !== new URL(BACKEND_URL).origin) offHost.push(r.url());
  });

  await page.goto('/stardust/overview', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(4_000);

  expect(apiHits.length, '应发出业务接口请求').toBeGreaterThan(0);
  expect(offHost, '业务接口必须直连后端主机').toEqual([]);
});

test('直连契约：后端 CORS 放行带凭证的跨域请求', async ({ page }) => {
  const res = await page.request.get(`${BACKEND_URL}/api/Registry/App?PageSize=2`, {
    headers: { Origin: 'http://localhost:5188' },
  });
  expect([200, 401], `业务接口应返回 200/401，实际 ${res.status()}`).toContain(res.status());
  expect(res.headers()['access-control-allow-credentials'], '应允许携带凭证').toBe('true');
});

/**
 * 关键页面在真实数据下的渲染留档。
 *
 * 注意：这些页面的「数据是否真的到达」尚未断言（缺内容标记），
 * 仅断言「有渲染内容 + 无鉴权失败 + 无控制台错误」，textLen 偏小者可能是空态文案。
 */
const PAGES: Array<{ name: string; path: string; ready?: string }> = [
  { name: 'workspace', path: '/stardust/overview', ready: '[data-spec^="ws-"]' },
  { name: 'apps', path: '/stardust/apps' },
  { name: 'nodes', path: '/stardust/nodes' },
  { name: 'release', path: '/stardust/release' },
  { name: 'config', path: '/stardust/config' },
  { name: 'gateway', path: '/stardust/gateway', ready: '[data-spec^="gw-"]' },
  { name: 'observability', path: '/stardust/observability', ready: '[data-spec^="obs-"]' },
];

for (const p of PAGES) {
  test(`真实数据 · ${p.name}`, async ({ page }) => {
    test.setTimeout(300_000);

    const consoleErrors: string[] = [];
    page.on('console', (m) => {
      if (m.type() === 'error' && !m.text().includes('/Cube/Avatar/')) {
        consoleErrors.push(m.text().slice(0, 160));
      }
    });
    const failed: string[] = [];
    page.on('response', (res) => {
      if (res.status() >= 400 && !res.url().includes('/Cube/Avatar/')) {
        failed.push(`${res.status()} ${res.url()}`);
      }
    });

    const api = trackApi(page); // 必须在 goto 之前
    await page.goto(p.path, { waitUntil: 'domcontentloaded' });

    if (p.ready) {
      await page
        .waitForFunction((sel: string) => !!document.querySelector(sel), p.ready, { timeout: 60_000 })
        .catch(() => {});
    }
    await api.settled();
    await waitContent(page);

    const text = (await page.locator('main.cosmic-content').innerText()).replace(/\s+/g, ' ').trim();
    expect(text.length, `${p.name} 主内容不应为空壳`).toBeGreaterThan(30);

    await page.screenshot({ path: `${SHOT_DIR}/${p.name}.png`, fullPage: true });

    const authFail = failed.filter((f) => f.startsWith('401') || f.startsWith('403'));
    expect(authFail, `${p.name} 不应出现 401/403：${authFail.join(' | ')}`).toEqual([]);
    expect(consoleErrors, `${p.name} 控制台错误：${consoleErrors.join(' | ')}`).toEqual([]);

    console.log(
      `[stardust-real] ${p.name} apiSeen=${api.seen} textLen=${text.length} 4xx+5xx=${failed.length}`,
    );
  });
}

/**
 * 平台管理五 Tab：真实数据下必须拿到行数据。
 * 后端已核实：用户 1 / 角色 4 / 项目 2 / Token 0。
 */
const ADMIN_TABS = ['projects', 'users', 'roles', 'tokens', 'settings'] as const;

test('平台管理五 Tab · 真实数据行数', async ({ page }) => {
  test.setTimeout(900_000); // 5 Tab 串行，每 Tab 都要等 TopBar 解析 + 自身取数（后端并发下 15-35s/次）

  const consoleErrors: string[] = [];
  page.on('console', (m) => {
    if (m.type() === 'error' && !m.text().includes('/Cube/Avatar/')) consoleErrors.push(m.text().slice(0, 160));
  });

  const report: Record<string, unknown> = {};

  for (const tab of ADMIN_TABS) {
    const api = trackApi(page); // 必须在 goto 之前
    await page.goto(`/stardust/admin?tab=${tab}`, { waitUntil: 'domcontentloaded' });
    await api.settled(240_000);

    // 数据落地后行/空态才出现；此处给内容渲染留出稳定期
    await page
      .waitForFunction(
        () =>
          !!document.querySelector('[data-spec^="ac-"][data-spec$="-row"], [data-spec^="ac-"][data-spec$="-empty"]'),
        { timeout: 60_000 },
      )
      .catch(() => {});
    await page.waitForTimeout(2_000);

    const info = await page.evaluate(() => {
      const rows = Array.from(document.querySelectorAll('[data-spec^="ac-"][data-spec$="-row"]'));
      const cell = (row: Element, i: number) => {
        const td = row.children[i];
        return td ? (td.textContent ?? '').trim() : '';
      };
      return {
        rows: rows.length,
        col1: rows.map((r) => cell(r, 0)),
        col2: rows.map((r) => cell(r, 1)),
        col3: rows.map((r) => cell(r, 2)),
        empty: document.querySelectorAll('[data-spec^="ac-"][data-spec$="-empty"]').length,
        tabCounts: Array.from(document.querySelectorAll('[data-spec="ac-tab-count"]')).map((e) => (e.textContent ?? '').trim()),
      };
    });
    report[tab] = info;
    console.log(
      `[admin-real] ${tab} apiSeen=${api.seen} rows=${info.rows} empty=${info.empty} counts=${JSON.stringify(info.tabCounts)} c1=${JSON.stringify(info.col1)} c2=${JSON.stringify(info.col2)} c3=${JSON.stringify(info.col3)}`,
    );
    await page.screenshot({ path: `${SHOT_DIR}/admin-real-${tab}.png`, fullPage: true });
  }

  // 用户 / 角色 / 项目三 Tab 的真实数据已核实存在，必须有行；Token 为空是后端事实
  expect((report.users as { rows: number }).rows, '用户 Tab 应有真实数据行').toBeGreaterThan(0);
  expect((report.roles as { rows: number }).rows, '角色 Tab 应有真实数据行').toBeGreaterThan(0);
  expect((report.projects as { rows: number }).rows, '项目 Tab 应有真实数据行').toBeGreaterThan(0);
  expect(consoleErrors, `平台管理控制台错误：${consoleErrors.join(' | ')}`).toEqual([]);
});
