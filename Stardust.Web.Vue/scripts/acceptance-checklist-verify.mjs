/**
 * 可选本地脚本：前端 + WebApi 探针（与 e2e/acceptance-app-feature-smoke.spec.ts 重叠，CI 请跑 Playwright）。
 * 用法: cd Stardust.Web.Vue && node scripts/acceptance-checklist-verify.mjs
 */
import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '../..');
const SHOT_DIR = path.join(ROOT, '.playwright-mcp');
const REPORT = path.join(ROOT, '.temp', 'acceptance_checklist_verify_report.json');

const FE = 'http://localhost:5188';
const BE = 'http://127.0.0.1:6681';
const USER = 'admin';
const PASS = 'Admin@1234';

fs.mkdirSync(SHOT_DIR, { recursive: true });

/** @type {{ id: string, pass: boolean, notes: string, shot?: string }[]} */
const results = [];

function record(id, pass, notes, shot) {
  results.push({ id, pass, notes, shot });
  const mark = pass ? 'PASS' : 'FAIL';
  console.log(`[${mark}] ${id}: ${notes}`);
}

async function apiLogin() {
  const r = await fetch(`${BE}/Auth/Login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: USER, password: PASS }),
  });
  const text = await r.text();
  if (!text) throw new Error(`Login empty HTTP ${r.status}`);
  const body = JSON.parse(text);
  if (body.code !== 0) throw new Error(`Login failed: ${body.message}`);
  return body.data.access_token ?? body.data.accessToken;
}

async function apiGet(token, path) {
  const r = await fetch(`${BE}${path}`, { headers: { Authorization: `Bearer ${token}` } });
  const text = await r.text();
  try {
    return { status: r.status, body: JSON.parse(text) };
  } catch {
    return { status: r.status, body: text.slice(0, 400) };
  }
}

async function injectAuth(page, token) {
  await page.goto(`${FE}/login`, { waitUntil: 'domcontentloaded' });
  await page.evaluate(([t]) => {
    localStorage.setItem('token', t);
  }, [token]);
  await page.goto(`${FE}/now`, { waitUntil: 'networkidle', timeout: 60_000 });
  if (page.url().includes('/login')) throw new Error('注入 token 后仍被踢回登录');
}

async function shot(page, name) {
  const file = path.join(SHOT_DIR, name);
  await page.screenshot({ path: file, fullPage: false });
  return name;
}

async function waitNoErrorToast(page) {
  const err = page.locator('.el-message--error');
  await err.first().waitFor({ state: 'hidden', timeout: 500 }).catch(() => {});
  return (await err.count()) === 0;
}

async function confirmGuard(page) {
  const g = page.locator('[data-spec="guard-modal"]');
  await g.waitFor({ state: 'visible', timeout: 10_000 });
  await page.locator('[data-spec="guard-confirm"]').click();
  await g.waitFor({ state: 'hidden', timeout: 15_000 });
}

async function main() {
  console.log('=== Lead checklist re-verify ===');
  let token;
  try {
    token = await apiLogin();
    record('ENV', true, `6681 登录 OK；5188 待 UI 探针`);
  } catch (e) {
    record('ENV', false, String(e.message ?? e));
    fs.writeFileSync(REPORT, JSON.stringify(results, null, 2), 'utf8');
    process.exit(1);
  }

  // WebApi 契约探针（Snapshot / Detail / AppClientLog 等）
  const snap = await apiGet(token, '/api/Registry/AppOnline/Snapshot?appId=4');
  const snapRow = snap.body?.data?.[0];
  record(
    'F04-API',
    snap.status === 200 && snap.body?.code === 0 && snapRow?.memory === 189,
    `Snapshot appId=4 memory=${snapRow?.memory} online=${snapRow?.online}`,
  );

  const detailOk = await apiGet(token, '/api/Deployment/AppPipelineRun/Detail?id=10');
  const detailBad = await apiGet(token, '/api/Deployment/AppPipelineRun/Detail?id=999999');
  record(
    'F16-API',
    detailOk.body?.code === 0 && String(detailBad.body?.message || '').includes('Run record does not exist'),
    `Detail id=10 code=${detailOk.body?.code}; missing=${detailBad.body?.message}`,
  );

  const logErr = await apiGet(
    token,
    `/api/Registry/AppClientLog?appId=4&kind=Error&dtStart=2026-11-03&dtEnd=2026-11-03 23:59:59&PageSize=5`,
  );
  record('F17-API', logErr.status === 200 && logErr.body?.code === 0, `AppClientLog kind=Error code=${logErr.body?.code}`);

  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();
  const consoleErrs = [];
  page.on('console', (m) => {
    if (m.type() === 'error' && !m.text().includes('/Cube/Avatar/')) consoleErrs.push(m.text());
  });

  try {
    await injectAuth(page, token);

    // F01
    await page.goto(`${FE}/app/tracer/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const tracerText = await page.locator('[data-spec="tracer-tab"]').innerText();
    const hasStarServer = tracerText.includes('StarServer-Dev');
    const itemCount = /\d{2,}/.test(tracerText);
    await page.locator('tbody tr').first().locator('input[type="checkbox"]').check({ force: true }).catch(async () => {
      await page.locator('tbody tr').first().click();
    });
    const fixBtn = page.locator('[data-spec="tracer-fix"]');
    const fixEnabled = await fixBtn.isEnabled().catch(() => false);
    if (fixEnabled) {
      await fixBtn.click();
      await confirmGuard(page);
      await page.locator('.el-message').filter({ hasText: /修正/ }).first().waitFor({ timeout: 15_000 }).catch(() => {});
    }
    const s01 = await shot(page, 'shot-lead-rerun-F01-tracer.png');
    record('F01', hasStarServer && itemCount, `StarServer-Dev=${hasStarServer} 批量修正=${fixEnabled}`, s01);

    // F02
    await page.goto(`${FE}/app/daystat/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const dayText = await page.locator('body').innerText();
    const hasCards = /调用|错误|耗时/.test(dayText);
    await page.getByText('最近24h分钟', { exact: false }).first().click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(800);
    const s02 = await shot(page, 'shot-lead-rerun-F02-daystat.png');
    record('F02', hasCards, `汇总/图表文案可见=${hasCards}`, s02);

    // F04 UI
    await page.goto(`${FE}/app/instances/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const instText = await page.locator('body').innerText();
    const memOk = instText.includes('189 MB') || instText.includes('189MB');
    const cpuOk = instText.includes('0.1') || instText.includes('6%');
    const notPending = !instText.includes('待接入');
    const s04 = await shot(page, 'shot-lead-rerun-F04-instances.png');
    record('F04', memOk && notPending, `内存MB=${memOk} 非待接入=${notPending} CPU≈=${cpuOk}`, s04);

    // F03
    const cb = page.locator('tbody input[type="checkbox"]').first();
    if (await cb.count()) await cb.check({ force: true });
    await page.getByPlaceholder(/命令|command/i).first().fill('ver').catch(() => {});
    await page.getByRole('button', { name: /执行命令/ }).first().click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(1500);
    await page.getByPlaceholder(/命令|command/i).first().fill('app/restart').catch(() => {});
    await page.getByRole('button', { name: /执行命令/ }).first().click({ timeout: 5000 }).catch(() => {});
    const guardVisible = await page.locator('[data-spec="guard-modal"]').isVisible().catch(() => false);
    if (guardVisible) await confirmGuard(page);
    await page.waitForTimeout(1000);
    const s03 = await shot(page, 'shot-lead-rerun-F03-execute.png');
    const body3 = await page.locator('body').innerText();
    record('F03', guardVisible && /6600|拒绝|汇总|失败/.test(body3), `危险命令 Guard=${guardVisible}`, s03);

    // F05 - only check page + upload dialog exists (skip file to avoid noise)
    await page.goto(`${FE}/app/versions/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const uploadBtn = page.getByRole('button', { name: /上传新版本/ });
    const hasUpload = (await uploadBtn.count()) > 0;
    const s05 = await shot(page, 'shot-lead-rerun-F05-versions.png');
    record('F05', hasUpload, `上传入口=${hasUpload}`, s05);

    // F06-F08 nodes
    await page.goto(`${FE}/app/nodes/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const nodesText = await page.locator('body').innerText();
    const hasDeploy = /部署节点|编辑发布集|同步应用/.test(nodesText);
    const s06 = await shot(page, 'shot-lead-rerun-F06-F08-nodes.png');
    record('F06', /编辑发布集|同步应用/.test(nodesText), '部署顶栏可见', s06);
    record('F07', /部署节点|批量/.test(nodesText), '部署节点区块', s06);
    await page.getByText('编译节点', { exact: false }).first().click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(500);
    record('F08', /编译节点|编译上传/.test(await page.locator('body').innerText()), '编译节点区块');

    // F09
    await page.goto(`${FE}/app/pipeline/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const pipeText = await page.locator('body').innerText();
    const s09 = await shot(page, 'shot-lead-rerun-F09-pipeline.png');
    record('F09', /运行|回滚|流水线/.test(pipeText), '流水线页加载', s09);

    // F10
    await page.goto(`${FE}/app/config/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const cfgText = await page.locator('body').innerText();
    record('F10', /配置|发布|差异/.test(cfgText), '配置页签');
    await page.goto(`${FE}/configs`, { waitUntil: 'networkidle', timeout: 60_000 });
    const configsText = await page.locator('body').innerText();
    const keyCountOk = !configsText.match(/Key 数[\s\n]*0/g)?.length || /Key 数[\s\n]*[1-9]/.test(configsText);
    const s10 = await shot(page, 'shot-lead-rerun-F10-configs-keycount.png');
    record('F10-keys', keyCountOk, `Key 数非全 0=${keyCountOk}`, s10);

    // F11
    record('F11', /应用规则/.test(configsText), '应用规则区块');

    // F15 deps on configs
    record('F15', /应用依赖/.test(configsText), '应用依赖区块');
    await page.goto(`${FE}/app/config/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    const depsPanel = await page.locator('body').innerText();
    record('F15-panel', /上下游依赖/.test(depsPanel), '工作台上下游面板');

    // F12
    await page.goto(`${FE}/apps`, { waitUntil: 'networkidle', timeout: 60_000 });
    const appsText = await page.locator('body').innerText();
    const hasVf2 = appsText.includes('vf2.f12.test');
    const s12 = await shot(page, 'shot-lead-rerun-F12-apps.png');
    record('F12', hasVf2, `vf2.f12.test 仍在=${hasVf2}`, s12);

    // F13
    await page.goto(`${FE}/mesh?tab=registry`, { waitUntil: 'networkidle', timeout: 60_000 });
    const meshText = await page.locator('body').innerText();
    const hasSvc = meshText.includes('StarWebApi');
    if (hasSvc) {
      await page.getByText('StarWebApi', { exact: false }).first().click({ timeout: 8000 }).catch(() => {});
      await page.waitForTimeout(1200);
    }
    const s13 = await shot(page, 'shot-lead-rerun-F13-mesh.png');
    record('F13', hasSvc, `StarWebApi 可点=${hasSvc}`, s13);

    // F14
    await page.goto(`${FE}/trace/369299`, { waitUntil: 'networkidle', timeout: 60_000 });
    const traceText = await page.locator('body').innerText();
    const s14a = await shot(page, 'shot-lead-rerun-F14-single.png');
    record('F14', /链路|TraceId|瀑布/.test(traceText), '单链路页', s14a);
    await page.locator('[data-spec="trace-search"] input').fill('369299,405');
    await page.locator('[data-spec="trace-search-btn"]').click();
    await page.waitForTimeout(3000);
    const multiText = await page.locator('body').innerText();
    const s14b = await shot(page, 'shot-lead-rerun-F14-multi.png');
    record('F14-multi', /各自独立|并列|trace-unit/i.test(multiText) || (await page.locator('[data-spec="trace-unit"]').count()) >= 1, '多 TraceId 查询', s14b);

    // F17 UI + network
    const logRequests = [];
    page.on('request', (req) => {
      const u = req.url();
      if (u.includes('AppClientLog') && u.includes('kind=')) logRequests.push(u);
    });
    await page.goto(`${FE}/app/logs/4`, { waitUntil: 'networkidle', timeout: 60_000 });
    await page.getByText('Error', { exact: true }).first().click({ timeout: 8000 }).catch(() => {});
    await page.waitForTimeout(2000);
    const s17 = await shot(page, 'shot-lead-rerun-F17-log.png');
    const kindReq = logRequests.some((u) => u.includes('kind=Error'));
    record('F17', kindReq, `Error chip 触发 kind 请求=${kindReq} (${logRequests.length} 条)`, s17);

    record('CONSOLE', consoleErrs.length === 0, consoleErrs.length ? consoleErrs.slice(0, 3).join(' | ') : '0 console error');
  } finally {
    await browser.close();
  }

  fs.writeFileSync(REPORT, JSON.stringify(results, null, 2), 'utf8');
  const failed = results.filter((r) => !r.pass);
  console.log(`\n=== Done: ${results.length - failed.length}/${results.length} pass ===`);
  if (failed.length) {
    console.log('Failed:', failed.map((f) => f.id).join(', '));
    process.exitCode = 1;
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
