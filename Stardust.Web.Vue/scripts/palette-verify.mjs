/**
 * 命令面板 / 全局热键 浏览器验收脚本（一次性验收工具）
 * ---------------------------------------------------------------------------
 * 目的：M2 CommandPalette + useGlobalHotkeys 的截图门禁验收。
 * 视口固定 1440×900（满足「窗口宽度 1280 以上」）。
 * 登录：直连后端 /Auth/Login（admin/admin）+ 注入 localStorage['token']，同 e2e/auth.setup.ts。
 *
 * 用法（必须在 Stardust.Web.Vue 目录下执行，依赖其 node_modules）：
 *   node scripts/palette-verify.mjs
 *
 * 产出：截图落在 <repo>/.forgeself/verify/palette/，结构化结果落在同目录 result.json。
 */
import { chromium } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..');
const OUT = resolve(REPO_ROOT, '.forgeself', 'verify', 'palette');
mkdirSync(OUT, { recursive: true });

const FRONTEND = process.env.PLAYWRIGHT_BASE_URL || 'http://localhost:5188';
const BACKEND = process.env.BACKEND_URL || 'http://localhost:6681';
const USER = process.env.BACKEND_USER || 'admin';
const PASS = process.env.BACKEND_PASS || 'admin';

const results = [];
function check(name, ok, detail) {
  results.push({ name, ok: !!ok, detail: String(detail) });
  console.log(`${ok ? 'PASS' : 'FAIL'} | ${name} | ${detail}`);
}
const shot = async (page, name) => {
  const p = resolve(OUT, name);
  await page.screenshot({ path: p });
  console.log(`SHOT | ${name}`);
  return p;
};

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  locale: 'zh-CN',
  deviceScaleFactor: 1,
});
const page = await context.newPage();
page.on('console', (m) => {
  if (m.type() === 'error') console.log('[console.error]', m.text().slice(0, 300));
});
page.on('pageerror', (e) => console.log('[pageerror]', String(e.message).slice(0, 300)));

try {
  // ---------- 登录 ----------
  await page.goto(`${FRONTEND}/login`, { waitUntil: 'domcontentloaded' });
  const resp = await context.request.post(`${BACKEND}/Auth/Login`, {
    data: { username: USER, password: PASS },
    headers: { 'Content-Type': 'application/json' },
    timeout: 60_000,
  });
  const body = await resp.json();
  const token = body?.data?.accessToken ?? body?.data?.access_token ?? '';
  check('登录：后端 /Auth/Login 返回 code=0 且拿到 token', resp.status() === 200 && body?.code === 0 && !!token,
    `status=${resp.status()} code=${body?.code} token=${token ? 'yes' : 'no'}`);
  await page.evaluate((t) => localStorage.setItem('token', t), token);

  // ---------- 进入 /now ----------
  await page.goto(`${FRONTEND}/now`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-spec="console-shell"]', { timeout: 45_000 });
  await page.waitForTimeout(1200);
  check('路由：/now 未被重定向到登录页', !page.url().includes('/login'), page.url());

  const htmlState = await page.evaluate(() => ({
    cls: document.documentElement.className,
    dataTheme: document.documentElement.getAttribute('data-theme'),
  }));
  console.log('HTML STATE', JSON.stringify(htmlState));
  await shot(page, '00-now-dark.png');

  // ======================================================================
  // 检查 1：点「搜索 / 执行…」按钮 → 面板打开，且只有「页面」一组 5 项
  // ======================================================================
  await page.click('[data-spec="search-trigger"]');
  await page.waitForSelector('[data-spec="command-palette"]', { timeout: 10_000 });
  await page.waitForSelector('[data-spec="palette-input"]', { timeout: 10_000 });
  await page.waitForTimeout(600); // 等入场动画结束

  const groups = await page.$$eval('[data-spec="command-palette"] [role="listbox"] > div', (els) =>
    els.map((g) => ({
      title: (g.querySelector('div')?.textContent ?? '').trim(),
      items: Array.from(g.querySelectorAll('[role="option"]')).map((o) => ({
        text: (o.querySelector('span')?.textContent ?? '').trim(),
        hint: (o.querySelector('small')?.textContent ?? '').trim(),
        selected: o.getAttribute('aria-selected'),
      })),
    })),
  );
  const titles = groups.map((g) => g.title);
  const items = groups.flatMap((g) => g.items);
  console.log('PALETTE GROUPS', JSON.stringify(groups, null, 1));
  check('检查1a：面板只有「页面」一组', titles.length === 1 && titles[0] === '页面', `groups=${JSON.stringify(titles)}`);
  check('检查1b：「页面」组 5 项且文案为 此刻/应用/星图/发布/治理',
    items.length === 5 && ['此刻', '应用', '星图', '发布', '治理'].every((t, i) => items[i].text === `跳转 · ${t}`),
    items.map((i) => i.text).join(' | '));
  check('检查1c：每项带 hint（首页/列表/节点/远程发布/低频配置）',
    ['首页', '列表', '节点', '远程发布', '低频配置'].every((h, i) => items[i]?.hint === h),
    items.map((i) => i.hint).join(' | '));
  await shot(page, '01-palette-open-dark.png');

  // ======================================================================
  // 检查 2：暗色主题遮罩（深色+模糊）与面板上浮入场动画
  // ======================================================================
  const darkStyle = await page.evaluate(() => {
    const mask = document.querySelector('[data-spec="command-palette"]');
    const panel = document.querySelector('[data-spec="command-palette"] [role="dialog"]');
    const sel = document.querySelector('[data-spec="command-palette"] [role="option"][aria-selected="true"]');
    const ms = getComputedStyle(mask);
    const ps = getComputedStyle(panel);
    const ss = sel ? getComputedStyle(sel) : null;

    // 扫描样式表：找 dark: 变体与 [.dark_&]: 变体的实际选择器
    const darkVariantSamples = [];
    const arbitraryDarkSamples = [];
    let scanned = 0;
    for (const sheet of Array.from(document.styleSheets)) {
      let rules;
      try { rules = sheet.cssRules; } catch { continue; }
      scanned++;
      for (const r of Array.from(rules)) {
        const selText = r.selectorText || '';
        if (!selText) continue;
        if (selText.includes('\\:') && selText.includes('dark')) darkVariantSamples.push(selText);
        if (selText.includes('.dark') && selText.includes('color-mix') && /72%/.test(r.cssText)) {
          arbitraryDarkSamples.push(r.cssText.slice(0, 240));
        }
      }
    }
    return {
      maskBg: ms.backgroundColor,
      maskBackdrop: ms.backdropFilter || ms.webkitBackdropFilter,
      maskAnimName: ms.animationName,
      maskAnimDur: ms.animationDuration,
      maskAnimTiming: ms.animationTimingFunction,
      panelAnimName: ps.animationName,
      panelAnimDur: ps.animationDuration,
      panelAnimTiming: ps.animationTimingFunction,
      panelBg: ps.backgroundColor,
      panelBorder: ps.borderTopColor,
      panelBorderWidth: ps.borderTopWidth,
      selectedBg: ss ? ss.backgroundColor : null,
      htmlClass: document.documentElement.className,
      dataTheme: document.documentElement.getAttribute('data-theme'),
      stylesheetsScanned: scanned,
      darkVariantSample: darkVariantSamples.slice(0, 4),
      darkVariantCount: darkVariantSamples.length,
      arbitraryDarkSample: arbitraryDarkSamples.slice(0, 2),
    };
  });
  console.log('DARK STYLE', JSON.stringify(darkStyle, null, 1));
  check('检查2a：遮罩带背景模糊（backdrop-filter: blur）', /blur\(/.test(darkStyle.maskBackdrop || ''), `backdrop=${darkStyle.maskBackdrop}`);
  // Chromium 会把 color-mix(in oklab, black 72%, transparent) 算成 oklab(0 0 0 / 0.72)
  const alphaOf = (c) => {
    const m = /rgba?\([^)]*?[\s,/]\s*([0-9.]+)\s*\)$/.exec(c ?? '');
    if (m) return parseFloat(m[1]);
    const o = /oklab\([^)]*?\/\s*([0-9.]+)\s*\)$/.exec(c ?? '');
    return o ? parseFloat(o[1]) : NaN;
  };
  check('检查2b：暗色遮罩为深色半透明（黑色占比 72% 的 color-mix）',
    Math.abs(alphaOf(darkStyle.maskBg) - 0.72) < 0.02,
    `bg=${darkStyle.maskBg} alpha=${alphaOf(darkStyle.maskBg)}`);
  check('检查2c：遮罩入场动画 x-fade 已生成', darkStyle.maskAnimName.includes('x-fade'), `animation=${darkStyle.maskAnimName} ${darkStyle.maskAnimDur}`);
  check('检查2d：面板上浮入场动画 x-pop 已生成', darkStyle.panelAnimName.includes('x-pop'), `animation=${darkStyle.panelAnimName} ${darkStyle.panelAnimDur}`);
  check('检查2e：html 带 .dark class 且 data-theme=dark', darkStyle.htmlClass.includes('dark') && darkStyle.dataTheme === 'dark',
    `class="${darkStyle.htmlClass}" data-theme=${darkStyle.dataTheme}`);
  check('检查2f：Tailwind 已生成 [.dark_&]: 变体规则', darkStyle.arbitraryDarkSample.length > 0,
    darkStyle.arbitraryDarkSample[0] || '未找到 color-mix 72% 的 .dark 规则');
  console.log(`INFO | dark: 变体规则数=${darkStyle.darkVariantCount}，样本=${JSON.stringify(darkStyle.darkVariantSample)}`);

  // ======================================================================
  // 检查 3：过滤 / 上下键 / 回车 / Esc
  // ======================================================================
  // 3a 上下键移动选中
  const selIdx = async () => {
    const all = await page.$$eval('[data-spec="command-palette"] [role="option"]', (els) =>
      els.map((o) => o.getAttribute('aria-selected')));
    return all.indexOf('true');
  };
  const i0 = await selIdx();
  await page.keyboard.press('ArrowDown');
  await page.waitForTimeout(150);
  const i1 = await selIdx();
  await page.keyboard.press('ArrowDown');
  await page.waitForTimeout(150);
  const i2 = await selIdx();
  await page.keyboard.press('ArrowUp');
  await page.waitForTimeout(150);
  const i3 = await selIdx();
  await page.keyboard.press('ArrowUp');
  await page.waitForTimeout(150);
  const i4 = await selIdx();
  check('检查3a：↓ 移动选中项', i0 === 0 && i1 === 1 && i2 === 2, `0→${i1}→${i2}`);
  check('检查3b：↑ 回退且在首项钳制为 0', i3 === 1 && i4 === 0, `→${i3}→${i4}`);
  await shot(page, '02-palette-arrow-selection.png');

  // 3b 输入过滤
  await page.fill('[data-spec="palette-input"]', '星');
  await page.waitForTimeout(250);
  const filtered = await page.$$eval('[data-spec="command-palette"] [role="option"]', (els) =>
    els.map((o) => (o.querySelector('span')?.textContent ?? '').trim()));
  const filteredGroups = await page.$$eval('[data-spec="command-palette"] [role="listbox"] > div', (els) => els.length);
  check('检查3c：输入「星」只过滤出 1 条且分组仍为「页面」',
    filtered.length === 1 && filtered[0] === '跳转 · 星图' && filteredGroups === 1,
    `items=${JSON.stringify(filtered)} groups=${filteredGroups}`);
  await shot(page, '03-palette-filter.png');

  // 3c 无结果
  await page.fill('[data-spec="palette-input"]', 'zzz不存在的词');
  await page.waitForTimeout(250);
  const emptyText = await page.textContent('[data-spec="command-palette"] [role="listbox"]').catch(() => '');
  check('检查3d：无匹配时显示「没有结果」', (emptyText ?? '').includes('没有结果'), (emptyText ?? '').trim().slice(0, 40));
  await shot(page, '04-palette-empty.png');

  // 3d 回车执行（清空后选到第 3 项「星图」→ /nodes）
  await page.fill('[data-spec="palette-input"]', '');
  await page.waitForTimeout(200);
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.waitForTimeout(150);
  const preEnterSel = await selIdx();
  await page.keyboard.press('Enter');
  await page.waitForTimeout(1200);
  const urlAfterEnter = page.url();
  const paletteGone = (await page.$('[data-spec="command-palette"]')) === null;
  check('检查3e：回车跳转对应页面（选中第 3 项「星图」→ /nodes）',
    preEnterSel === 2 && urlAfterEnter.includes('/nodes') && paletteGone,
    `sel=${preEnterSel} url=${urlAfterEnter} paletteClosed=${paletteGone}`);
  await shot(page, '05-after-enter-nodes.png');

  // 3e Esc 关闭
  await page.goto(`${FRONTEND}/now`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-spec="console-shell"]', { timeout: 30_000 });
  await page.waitForTimeout(800);
  await page.keyboard.press('Control+k');
  await page.waitForSelector('[data-spec="command-palette"]', { timeout: 10_000 });
  await page.waitForTimeout(300);
  await page.keyboard.press('Escape');
  await page.waitForTimeout(400);
  const closedByEsc = (await page.$('[data-spec="command-palette"]')) === null;
  check('检查3f：Ctrl+K 打开、Esc 关闭', closedByEsc, `closed=${closedByEsc}`);

  // ======================================================================
  // 检查 4：面板打开时再按 Ctrl+K，焦点不外泄（地址栏不被聚焦）
  // ======================================================================
  await page.click('[data-spec="search-trigger"]');
  await page.waitForSelector('[data-spec="palette-input"]', { timeout: 10_000 });
  await page.waitForTimeout(300);
  await page.evaluate(() => {
    window.__kd = [];
    window.addEventListener('keydown', (e) => {
      window.__kd.push({
        key: e.key,
        ctrl: e.ctrlKey,
        meta: e.metaKey,
        defaultPrevented: e.defaultPrevented,
        active: document.activeElement?.getAttribute?.('data-spec') || document.activeElement?.tagName || null,
      });
    });
  });
  await page.keyboard.press('Control+k');
  await page.waitForTimeout(400);
  const kd = await page.evaluate(() => ({
    events: window.__kd,
    stillOpen: !!document.querySelector('[data-spec="command-palette"]'),
    active: document.activeElement?.getAttribute?.('data-spec') || document.activeElement?.tagName || null,
  }));
  console.log('KEYDOWN LOG', JSON.stringify(kd, null, 1));
  const ctrlK = (kd.events || []).find((e) => e.key.toLowerCase() === 'k' && (e.ctrl || e.meta));
  check('检查4a：面板打开时 Ctrl+K 被 preventDefault（浏览器不会聚焦地址栏）',
    !!ctrlK && ctrlK.defaultPrevented === true, JSON.stringify(ctrlK || null));
  check('检查4b：按 Ctrl+K 后焦点仍在面板输入框内', kd.active === 'palette-input', `activeElement=${kd.active}`);
  check('检查4c：面板未被关闭（保持打开）', kd.stillOpen === true, `open=${kd.stillOpen}`);
  await shot(page, '06-palette-ctrlk-refocus.png');

  // ======================================================================
  // 检查 5：浅色主题
  // ======================================================================
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  await page.click('[data-spec="theme-toggle"]');
  await page.waitForTimeout(500);
  const lightShell = await page.evaluate(() => ({
    cls: document.documentElement.className,
    dataTheme: document.documentElement.getAttribute('data-theme'),
  }));
  check('检查5a：切到浅色（去掉 .dark class，data-theme=light）',
    !lightShell.cls.includes('dark') && lightShell.dataTheme === 'light', JSON.stringify(lightShell));

  await page.click('[data-spec="search-trigger"]');
  await page.waitForSelector('[data-spec="command-palette"]', { timeout: 10_000 });
  await page.waitForTimeout(600);
  const lightStyle = await page.evaluate(() => {
    const mask = document.querySelector('[data-spec="command-palette"]');
    const panel = document.querySelector('[data-spec="command-palette"] [role="dialog"]');
    const sel = document.querySelector('[data-spec="command-palette"] [role="option"][aria-selected="true"]');
    const ms = getComputedStyle(mask);
    const ps = getComputedStyle(panel);
    const ss = sel ? getComputedStyle(sel) : null;
    return {
      maskBg: ms.backgroundColor,
      maskBackdrop: ms.backdropFilter || ms.webkitBackdropFilter,
      maskAnimName: ms.animationName,
      panelAnimName: ps.animationName,
      panelBg: ps.backgroundColor,
      panelBorder: ps.borderTopColor,
      panelBorderWidth: ps.borderTopWidth,
      selectedBg: ss ? ss.backgroundColor : null,
      selectedColor: ss ? ss.color : null,
      bodyBg: getComputedStyle(document.body).backgroundColor,
    };
  });
  console.log('LIGHT STYLE', JSON.stringify(lightStyle, null, 1));
  check('检查5b：浅色遮罩仍是半透明且带模糊',
    /blur\(/.test(lightStyle.maskBackdrop || '') && Math.abs(alphaOf(lightStyle.maskBg) - 0.48) < 0.02,
    `bg=${lightStyle.maskBg} alpha=${alphaOf(lightStyle.maskBg)} backdrop=${lightStyle.maskBackdrop}`);
  check('检查5c：浅色面板有可见边框', lightStyle.panelBorderWidth !== '0px', `${lightStyle.panelBorderWidth} ${lightStyle.panelBorder}`);
  check('检查5d：浅色下选中项有背景', !!lightStyle.selectedBg && lightStyle.selectedBg !== 'rgba(0, 0, 0, 0)',
    `selected=${lightStyle.selectedBg}`);
  await shot(page, '07-palette-light.png');

  // 浅色整页
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  await shot(page, '08-now-light.png');
} catch (e) {
  console.log('SCRIPT ERROR', e?.stack || e);
  results.push({ name: '脚本执行', ok: false, detail: String(e?.message ?? e) });
  try { await shot(page, '99-error.png'); } catch { /* ignore */ }
} finally {
  writeFileSync(resolve(OUT, 'result.json'), JSON.stringify(results, null, 2), 'utf8');
  const failed = results.filter((r) => !r.ok);
  console.log(`\n===== 汇总：${results.length - failed.length}/${results.length} PASS，失败 ${failed.length} =====`);
  for (const f of failed) console.log(`FAIL | ${f.name} | ${f.detail}`);
  console.log(`输出目录：${OUT}`);
  await browser.close();
}
