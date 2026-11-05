import { test, expect } from './fixtures';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { BACKEND_URL, BACKEND_USER, BACKEND_PASS } from './config';

const e2eDir = path.dirname(fileURLToPath(import.meta.url));
const SHOT_DIR = path.resolve(e2eDir, '../../.playwright-mcp');
const REPORT = path.resolve(e2eDir, '../../.temp/acceptance_app_feature_smoke_report.json');

type Row = { id: string; pass: boolean; notes: string; shot?: string };
const results: Row[] = [];

function record(id: string, pass: boolean, notes: string, shot?: string) {
  results.push({ id, pass, notes, shot });
}

async function apiLogin(): Promise<string> {
  const r = await fetch(`${BACKEND_URL}/Auth/Login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username: BACKEND_USER, password: BACKEND_PASS }),
  });
  const body = await r.json();
  expect(r.status, 'login http').toBe(200);
  expect(body.code, 'login code').toBe(0);
  return body.data.access_token ?? body.data.accessToken;
}

async function apiGet(token: string, p: string) {
  const r = await fetch(`${BACKEND_URL}${p}`, { headers: { Authorization: `Bearer ${token}` } });
  return r.json();
}

async function shot(page: import('@playwright/test').Page, name: string) {
  fs.mkdirSync(SHOT_DIR, { recursive: true });
  const file = path.join(SHOT_DIR, name);
  await page.screenshot({ path: file });
  return name;
}

async function confirmGuard(page: import('@playwright/test').Page) {
  const g = page.locator('[data-spec="guard-modal"]');
  await expect(g).toBeVisible();
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(g).toBeHidden({ timeout: 15_000 });
}

test.describe.configure({ mode: 'serial' });

test('应用功能冒烟（WebApi 契约 + 关键页路由）', async ({ page }) => {
  test.setTimeout(300_000);
  const token = await apiLogin();
  record('ENV', true, 'WebApi 登录 OK');

  const snap = await apiGet(token, '/api/Registry/AppOnline/Snapshot?appId=4');
  const row = snap.data?.[0];
  record(
    'F04-API',
    snap.code === 0 && row?.memory === 189,
    `Snapshot memory=${row?.memory} online=${row?.online}`,
  );

  const detailOk = await apiGet(token, '/api/Deployment/AppPipelineRun/Detail?id=10');
  const detailBad = await apiGet(token, '/api/Deployment/AppPipelineRun/Detail?id=999999');
  record(
    'F16-API',
    detailOk.code === 0 && String(detailBad.message || '').includes('Run record does not exist'),
    `Detail ok=${detailOk.code} miss=${detailBad.message}`,
  );

  const logErr = await apiGet(
    token,
    '/api/Registry/AppClientLog?appId=4&kind=Error&dtStart=2026-11-03&dtEnd=2026-11-03%2023:59:59&PageSize=5',
  );
  record('F17-API', logErr.code === 0, `AppClientLog kind=Error code=${logErr.code}`);

  // F01
  await page.goto('/app/tracer/4', { waitUntil: 'networkidle' });
  const tracerTab = page.locator('[data-spec="tracer-tab"]');
  await expect(tracerTab).toBeVisible();
  const tracerText = await tracerTab.innerText();
  const hasStarServer = tracerText.includes('StarServer-Dev');
  const cb0 = page.locator('tbody input[type="checkbox"]').first();
  if (await cb0.count()) await cb0.check({ force: true });
  const fixBtn = page.locator('[data-spec="tracer-fix"]');
  if (await fixBtn.isEnabled()) {
    await fixBtn.click();
    await confirmGuard(page);
    await page.locator('.el-message').filter({ hasText: /修正/ }).first().waitFor({ timeout: 15_000 }).catch(() => {});
  }
  const s01 = await shot(page, 'shot-lead-rerun-F01-tracer.png');
  record('F01', hasStarServer && /\d{2,}/.test(tracerText), `StarServer-Dev=${hasStarServer}`, s01);

  // F02
  await page.goto('/app/daystat/4', { waitUntil: 'networkidle' });
  const dayBody = await page.locator('body').innerText();
  await page.getByText('最近24h分钟').first().click({ timeout: 5000 }).catch(() => {});
  await page.waitForTimeout(800);
  const s02 = await shot(page, 'shot-lead-rerun-F02-daystat.png');
  record('F02', /调用|错误|耗时/.test(dayBody), '日统计/图表区', s02);

  // F04
  await page.goto('/app/instances/4', { waitUntil: 'networkidle' });
  const inst = await page.locator('body').innerText();
  const s04 = await shot(page, 'shot-lead-rerun-F04-instances.png');
  record(
    'F04',
    inst.includes('189 MB') && !inst.includes('待接入'),
    `189 MB=${inst.includes('189 MB')} 非待接入=${!inst.includes('待接入')}`,
    s04,
  );

  // F03
  const icb = page.locator('tbody input[type="checkbox"]').first();
  if (await icb.count()) await icb.check({ force: true });
  const cmdInput = page.locator('input').filter({ has: page.locator('xpath=..') }).first();
  await page.getByRole('textbox').first().fill('ver').catch(() => {});
  await page.getByRole('button', { name: '执行命令' }).click({ timeout: 8000 }).catch(() => {});
  await page.waitForTimeout(1200);
  await page.getByRole('textbox').first().fill('app/restart').catch(() => {});
  await page.getByRole('button', { name: '执行命令' }).click({ timeout: 8000 }).catch(() => {});
  const guard3 = await page.locator('[data-spec="guard-modal"]').isVisible().catch(() => false);
  if (guard3) await confirmGuard(page);
  await page.waitForTimeout(800);
  const body3 = await page.locator('body').innerText();
  const s03 = await shot(page, 'shot-lead-rerun-F03-execute.png');
  record('F03', guard3, `危险命令 Guard=${guard3} 汇总含拒连=${/6600|拒绝|失败|汇总/.test(body3)}`, s03);

  // F05
  await page.goto('/app/versions/4', { waitUntil: 'networkidle' });
  const hasUpload = (await page.getByRole('button', { name: '上传新版本' }).count()) > 0;
  const s05 = await shot(page, 'shot-lead-rerun-F05-versions.png');
  record('F05', hasUpload, `上传入口=${hasUpload}`, s05);

  // F06-F08
  await page.goto('/app/nodes/4', { waitUntil: 'networkidle' });
  const nodes = await page.locator('body').innerText();
  const s06 = await shot(page, 'shot-lead-rerun-F06-F08-nodes.png');
  record('F06', /编辑发布集|同步应用/.test(nodes), '发布集条', s06);
  record('F07', /部署节点/.test(nodes), '部署节点', s06);
  await page.getByText('编译节点').first().click({ timeout: 5000 }).catch(() => {});
  await page.waitForTimeout(500);
  record('F08', /编译节点/.test(await page.locator('body').innerText()), '编译节点');

  // F09
  await page.goto('/app/pipeline/4', { waitUntil: 'networkidle' });
  const s09 = await shot(page, 'shot-lead-rerun-F09-pipeline.png');
  record('F09', /运行|回滚|流水线/.test(await page.locator('body').innerText()), '流水线页', s09);

  // F10-F11-F15 configs
  await page.goto('/app/config/4', { waitUntil: 'networkidle' });
  record('F10', /配置|发布/.test(await page.locator('body').innerText()), '配置页签');
  await page.goto('/configs', { waitUntil: 'networkidle' });
  const configs = await page.locator('body').innerText();
  const keyNonZero = /Common[\s\S]{0,80}Key 数[\s\S]{0,20}[1-9]/.test(configs) || /MCP[\s\S]{0,80}Key 数[\s\S]{0,20}[1-9]/.test(configs);
  const s10 = await shot(page, 'shot-lead-rerun-F10-configs-keycount.png');
  record('F10-keys', keyNonZero, `Key 数非 0=${keyNonZero}`, s10);
  record('F11', /应用规则/.test(configs), '应用规则');
  // F15 应用依赖已于 2026-11-04 按用户决策移除，不再断言

  // F12
  await page.goto('/apps', { waitUntil: 'networkidle' });
  const s12 = await shot(page, 'shot-lead-rerun-F12-apps.png');
  record('F12', (await page.locator('body').innerText()).includes('vf2.f12.test'), 'vf2.f12.test 存在', s12);

  // F13
  await page.goto('/mesh?tab=registry', { waitUntil: 'networkidle' });
  const hasStarWebApi = (await page.locator('body').innerText()).includes('StarWebApi');
  if (hasStarWebApi) await page.getByText('StarWebApi').first().click({ timeout: 8000 }).catch(() => {});
  await page.waitForTimeout(1000);
  const s13 = await shot(page, 'shot-lead-rerun-F13-mesh.png');
  record('F13', hasStarWebApi, 'StarWebApi 服务详情入口', s13);

  // F14
  await page.goto('/trace/369299', { waitUntil: 'networkidle' });
  const s14a = await shot(page, 'shot-lead-rerun-F14-single.png');
  record('F14', /链路详情|TraceId/.test(await page.locator('body').innerText()), '单链路', s14a);
  await page.locator('[data-spec="trace-search"] input').fill('369299,405');
  await page.locator('[data-spec="trace-search-btn"]').click();
  await page.waitForTimeout(3500);
  const units = await page.locator('[data-spec="trace-unit"]').count();
  const s14b = await shot(page, 'shot-lead-rerun-F14-multi.png');
  record('F14-multi', units >= 1, `trace-unit 数=${units}`, s14b);

  // F17 UI
  const logReqs: string[] = [];
  page.on('request', (req) => {
    const u = req.url();
    if (u.includes('AppClientLog') && u.includes('kind=')) logReqs.push(u);
  });
  await page.goto('/app/logs/4', { waitUntil: 'networkidle' });
  await page.getByText('Error', { exact: true }).first().click({ timeout: 8000 }).catch(() => {});
  await page.waitForTimeout(2000);
  const s17 = await shot(page, 'shot-lead-rerun-F17-log.png');
  record('F17', logReqs.some((u) => u.includes('kind=Error')), `kind=Error 请求=${logReqs.length}`, s17);

  fs.mkdirSync(path.dirname(REPORT), { recursive: true });
  fs.writeFileSync(REPORT, JSON.stringify(results, null, 2), 'utf8');

  const failed = results.filter((r) => !r.pass);
  expect(failed, `复验失败项: ${failed.map((f) => `${f.id}(${f.notes})`).join('; ')}`).toEqual([]);
});
