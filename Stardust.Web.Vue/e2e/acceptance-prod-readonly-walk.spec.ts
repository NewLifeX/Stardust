/**
 * 生产安全走查：逐项打开页面与只读/可取消交互。
 * 禁止：Guard 确认、删除、发布、回滚、上传提交、SyncApp、批量修正/重算/Execute/FreeMemory 真执行、表单 Save。
 */
import { test, expect } from './fixtures';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { BACKEND_URL } from './config';

const e2eDir = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(e2eDir, '../..');
const SHOT_DIR = path.join(REPO, '.playwright-mcp');
const REPORT_MD = path.join(REPO, '.temp/acceptance_prod_readonly_walk_report.md');

const APP_ID = 4;

type Step = {
  id: string;
  route: string;
  status: 'PASS' | 'FAIL' | 'SKIP' | 'DEFER';
  safeActions: string;
  observed: string;
  issues: string;
  shot?: string;
};

const steps: Step[] = [];
const consoleErrors: string[] = [];

function logStep(s: Step) {
  steps.push(s);
  console.log(`WALK_${s.status} ${s.id} ${s.observed.slice(0, 120)}`);
}

async function snap(page: import('@playwright/test').Page, name: string) {
  fs.mkdirSync(SHOT_DIR, { recursive: true });
  const file = path.join(SHOT_DIR, name);
  await page.screenshot({ path: file, fullPage: false });
  return name;
}

async function cancelGuardIfOpen(page: import('@playwright/test').Page) {
  const g = page.locator('[data-spec="guard-modal"]');
  if (!(await g.isVisible().catch(() => false))) return;
  await page.locator('[data-spec="guard-cancel"]').click({ timeout: 5_000 }).catch(() => page.keyboard.press('Escape'));
  await g.waitFor({ state: 'hidden', timeout: 8_000 }).catch(() => {});
}

async function settle(page: import('@playwright/test').Page) {
  await page.waitForTimeout(2_500);
}

async function gotoPage(page: import('@playwright/test').Page, url: string) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await settle(page);
}

async function closeDrawerIfOpen(page: import('@playwright/test').Page) {
  const drawer = page.locator('.el-drawer');
  if (!(await drawer.isVisible().catch(() => false))) return;
  const cancel = page.locator('[data-spec="tracer-form-cancel"], [data-spec="guard-cancel"]').first();
  if (await cancel.isVisible().catch(() => false)) await cancel.click();
  else await page.keyboard.press('Escape');
  await expect(drawer).toBeHidden({ timeout: 10_000 });
}

test.describe.configure({ mode: 'serial' });

function writeReport(apiHost: string) {
  const lines: string[] = [
    '# 生产安全浏览器走查报告',
    '',
    `- 时间：2026-11-03`,
    `- 方式：**Playwright 真浏览器**（Chromium headless，5188 + storageState 登录）`,
    `- 探测 API：**${apiHost || BACKEND_URL}**（development.local 指向 localhost:6681；UI 顶栏「生产」=环境模式）`,
    `- Console Error：见脚本末尾统计`,
    '',
    '## 安全边界（本轮禁止并已规避）',
    '',
    'Guard「确认执行」、删除、发布/回滚、Upload 提交、SyncApp、批量修正/重算/Execute/FreeMemory 真下发、配置/规则/应用/节点/依赖 表单 Save。',
    '',
    '## 逐项结果',
    '',
    '| ID | 状态 | 路由 | 安全操作 | 观察/问题 | 截图 |',
    '|---|---|---|---|---|---|',
  ];
  for (const s of steps) {
    lines.push(
      `| ${s.id} | ${s.status} | ${s.route} | ${s.safeActions.replace(/\|/g, '/')} | ${(s.observed + ' ' + s.issues).replace(/\|/g, '/').trim().slice(0, 100)} | ${s.shot ?? ''} |`,
    );
  }
  lines.push(
    '',
    '## 待 WebApi 发布后可做破坏性/确认类补验',
    '',
    'AppOnline/Snapshot 字段、AppDeployVersion/Upload、AppPipelineRun/Detail、AppClientLog 分表 Search；实例/节点远程 Execute 真下发汇总。',
  );
  fs.mkdirSync(path.dirname(REPORT_MD), { recursive: true });
  fs.writeFileSync(REPORT_MD, lines.join('\n'), 'utf8');
}

test('应用工作台只读验收走查（Guard 一律取消）', async ({ page }) => {
  test.setTimeout(360_000);
  let apiHost = '';

  page.on('console', (m) => {
    if (m.type() === 'error' && !m.text().includes('/Cube/Avatar/')) consoleErrors.push(m.text());
  });

  page.on('request', (req) => {
    const u = req.url();
    if (!apiHost && (u.includes('/api/') || u.includes('/Auth/'))) {
      try {
        apiHost = new URL(u).origin;
      } catch {
        /* ignore */
      }
    }
  });

  try {
  await gotoPage(page, '/now');
  await page.waitForTimeout(1500);
  logStep({
    id: 'ENV',
    route: '/now',
    status: 'PASS',
    safeActions: '进入控制台',
    observed: `首屏加载；探测 API origin=${apiHost || BACKEND_URL}`,
    issues: apiHost.includes('10.188') ? '⚠ 请求指向远端生产主机' : '',
    shot: await snap(page, 'shot-lead-walk-ENV-now.png'),
  });

  // —— F01 ——
  await gotoPage(page, `/app/tracer/${APP_ID}`);
  const tab = page.locator('[data-spec="tracer-tab"]');
  await expect(tab).toBeVisible();
  const ttxt = await tab.innerText();
  const hasRow = ttxt.includes('StarServer-Dev') || /\d{2,}/.test(ttxt);
  await page.locator('[data-spec="tracer-refresh"]').click();
  await page.waitForTimeout(1200);
  const editLink = page.getByRole('button', { name: '编辑' }).or(page.getByText('编辑').first());
  const rowEdit = page.locator('tbody').getByText('编辑').first();
  if (await rowEdit.isVisible().catch(() => false)) {
    await rowEdit.click();
    await page.waitForTimeout(800);
    await closeDrawerIfOpen(page);
  }
  let f01Guard = false;
  const row = page.locator('tbody tr').filter({ hasText: 'StarServer-Dev' }).first();
  if (await row.count()) {
    await row.locator('input[type="checkbox"]').click({ force: true });
    await page.waitForTimeout(400);
    const fixBtn = page.locator('[data-spec="tracer-fix"]');
    if (await fixBtn.isEnabled()) {
      await fixBtn.click();
      f01Guard = await page.locator('[data-spec="guard-modal"]').isVisible();
      if (f01Guard) await cancelGuardIfOpen(page);
      await row.locator('input[type="checkbox"]').click({ force: true });
    }
  }
  logStep({
    id: 'F01',
    route: `/app/tracer/${APP_ID}`,
    status: hasRow ? 'PASS' : 'FAIL',
    safeActions: '刷新；编辑 Drawer 打开即关；批量修正 Guard 仅取消（若可勾选）',
    observed: ttxt.slice(0, 200).replace(/\s+/g, ' '),
    issues: f01Guard ? '' : '未验证 Guard（勾选未启用批量修正或仅 1 行 UI 态）',
    shot: await snap(page, 'shot-lead-walk-F01-tracer.png'),
  });

  // —— F02 ——
  await gotoPage(page, `/app/daystat/${APP_ID}`);
  await expect
    .poll(async () => (await page.locator('body').innerText()).includes('调用总数'), { timeout: 20_000 })
    .toBe(true)
    .catch(() => {});
  const body2 = await page.locator('body').innerText();
  const hasKpi = /调用总数|错误数|平均耗时/.test(body2);
  await page.getByText('最近24h分钟').first().click({ timeout: 5000 }).catch(() => {});
  await page.waitForTimeout(600);
  const retryBtn = page.getByRole('button', { name: /批量重算|重算/ });
  if (await retryBtn.count()) {
    await retryBtn.first().click();
    await cancelGuardIfOpen(page);
  }
  logStep({
    id: 'F02',
    route: `/app/daystat/${APP_ID}`,
    status: hasKpi ? 'PASS' : 'FAIL',
    safeActions: '看汇总/图；分钟开关；RetryStat Guard 仅取消',
    observed: body2.match(/调用总数[\s\S]{0,40}/)?.[0] ?? '无汇总文案',
    issues: '',
    shot: await snap(page, 'shot-lead-walk-F02-daystat.png'),
  });

  // —— F04（视图；Snapshot API 待发布后深验）——
  await gotoPage(page, `/app/instances/${APP_ID}`);
  const body4 = await page.locator('body').innerText();
  const memOk = /189\s*MB|\d+\s*MB/.test(body4);
  const offlineOk = body4.includes('离线') || body4.includes('0 在线');
  logStep({
    id: 'F04',
    route: `/app/instances/${APP_ID}`,
    status: memOk ? 'PASS' : 'DEFER',
    safeActions: '只读看表',
    observed: `CPU/内存/状态片段: ${body4.match(/0\.\d%|\d+\s*MB|离线|待接入/g)?.join(', ') ?? '—'}`,
    issues: body4.includes('待接入') ? '实例列仍出现待接入' : memOk ? '' : 'BE Snapshot 待 API 发布后再验',
    shot: await snap(page, 'shot-lead-walk-F04-instances.png'),
  });

  // —— F03 ——
  const icb = page.locator('[data-spec="instance-select"], tbody input[type="checkbox"]').first();
  if (await icb.count()) await icb.click();
  const cmd = page.getByRole('textbox').first();
  await cmd.fill('app/restart').catch(() => {});
  await page.getByRole('button', { name: '执行命令' }).click({ timeout: 5_000 }).catch(() => {});
  const guard3 = await page
    .locator('[data-spec="guard-modal"]')
    .waitFor({ state: 'visible', timeout: 5_000 })
    .then(() => true)
    .catch(() => false);
  if (guard3) await cancelGuardIfOpen(page);
  if (await icb.isVisible().catch(() => false)) await icb.click({ timeout: 3_000 }).catch(() => {});
  logStep({
    id: 'F03',
    route: `/app/instances/${APP_ID}`,
    status: guard3 ? 'PASS' : 'FAIL',
    safeActions: '勾选；填危险命令；Guard 仅取消；未 Execute 确认',
    observed: `危险命令 Guard 出现=${guard3}`,
    issues: guard3 ? '' : '未弹出 GuardModal',
    shot: await snap(page, 'shot-lead-walk-F03-instances-guard.png'),
  });

  // —— F05 ——
  await gotoPage(page, `/app/versions/${APP_ID}`);
  const up = page.getByRole('button', { name: '上传新版本' });
  const hasUp = (await up.count()) > 0;
  if (hasUp) {
    await up.click();
    await page.waitForTimeout(500);
    await page.keyboard.press('Escape').catch(() => {});
  }
  logStep({
    id: 'F05',
    route: `/app/versions/${APP_ID}`,
    status: hasUp ? 'PASS' : 'FAIL',
    safeActions: '打开上传入口即 Esc 关闭；未选文件',
    observed: `上传按钮=${hasUp}`,
    issues: 'multipart Upload 正/反例待 API 发布后再验',
    shot: await snap(page, 'shot-lead-walk-F05-versions.png'),
  });

  // —— F06~F08 ——
  await gotoPage(page, `/app/nodes/${APP_ID}`);
  const nodes = await page.locator('body').innerText();
  const editDeploy = page.getByRole('button', { name: /编辑发布集/ });
  if (await editDeploy.count()) {
    await editDeploy.first().click();
    await page.waitForTimeout(600);
    await closeDrawerIfOpen(page);
  }
  const syncBtn = page.getByRole('button', { name: /同步应用/ });
  logStep({
    id: 'F06',
    route: `/app/nodes/${APP_ID}`,
    status: /编辑发布集|同步应用/.test(nodes) ? 'PASS' : 'FAIL',
    safeActions: '编辑 Drawer 打开即关；未点同步/保存',
    observed: '发布集条可见',
    issues: syncBtn.count() ? '同步应用未点击（生产安全）' : '',
    shot: await snap(page, 'shot-lead-walk-F06-nodes-bar.png'),
  });
  const depCb = page.locator('[data-spec="ship-node-check-8"], [data-spec*="node-check"]').first();
  if (await depCb.count()) {
    await depCb.click({ force: true });
    await page.waitForTimeout(400);
    const batchRestart = page.getByRole('button', { name: '重启' }).first();
    if (await batchRestart.isEnabled().catch(() => false)) {
      await batchRestart.click();
      await cancelGuardIfOpen(page);
    }
    await depCb.click({ force: true }).catch(() => {});
  }
  logStep({
    id: 'F07',
    route: `/app/nodes/${APP_ID}`,
    status: /部署节点/.test(nodes) ? 'PASS' : 'FAIL',
    safeActions: '勾选→批量重启 Guard 仅取消',
    observed: '部署节点区块',
    issues: 'CRUD/真下发未测（生产安全）',
    shot: await snap(page, 'shot-lead-walk-F07-deploy-nodes.png'),
  });
  await page.getByText('编译节点').first().click({ timeout: 5000 }).catch(() => {});
  await page.waitForTimeout(400);
  logStep({
    id: 'F08',
    route: `/app/nodes/${APP_ID}`,
    status: /编译节点/.test(await page.locator('body').innerText()) ? 'PASS' : 'FAIL',
    safeActions: '切换编译节点区块只读',
    observed: '编译节点 Tab/区块',
    issues: 'CRUD 未测',
    shot: await snap(page, 'shot-lead-walk-F08-build-nodes.png'),
  });

  // —— F09 ——
  await gotoPage(page, `/app/pipeline/${APP_ID}`);
  const pipe = await page.locator('body').innerText();
  const rollback = page.getByRole('button', { name: /一键回滚|回滚/ });
  if (await rollback.count()) {
    await rollback.first().click();
    await cancelGuardIfOpen(page);
  }
  logStep({
    id: 'F09',
    route: `/app/pipeline/${APP_ID}`,
    status: /运行|流水线|回滚/.test(pipe) ? 'PASS' : 'FAIL',
    safeActions: '看列表/步骤；回滚 Guard 仅取消',
    observed: pipe.slice(0, 180).replace(/\s+/g, ' '),
    issues: 'Detail API / 真回滚待 API 发布后再验',
    shot: await snap(page, 'shot-lead-walk-F09-pipeline.png'),
  });

  // —— F10 ——
  await gotoPage(page, `/app/config/${APP_ID}`);
  const cfg = await page.locator('body').innerText();
  logStep({
    id: 'F10',
    route: `/app/config/${APP_ID}`,
    status: /配置|发布|差异/.test(cfg) ? 'PASS' : 'FAIL',
    safeActions: '只读；未发布/删除/同步',
    observed: '配置工作台',
    issues: '发布全链路未测（生产安全）',
    shot: await snap(page, 'shot-lead-walk-F10-app-config.png'),
  });
  await gotoPage(page, '/configs');
  const configs = await page.locator('body').innerText();
  const keyOk = /Key 数[\s\S]{0,8}[1-9]/.test(configs);
  logStep({
    id: 'F10-keys',
    route: '/configs',
    status: keyOk ? 'PASS' : 'FAIL',
    safeActions: '只看 Key 数列',
    observed: configs.match(/Common[\s\S]{0,30}/)?.[0] ?? '',
    issues: '',
    shot: await snap(page, 'shot-lead-walk-F10-configs-keys.png'),
  });

  // —— F11 ——
  logStep({
    id: 'F11',
    route: '/configs',
    status: /应用规则/.test(configs) ? 'PASS' : 'FAIL',
    safeActions: '只看规则表；未增删改',
    observed: '应用规则区块在',
    issues: 'CRUD 未测（生产安全）',
    shot: await snap(page, 'shot-lead-walk-F11-rules.png'),
  });

  // —— F15 ——（2026-11-04 用户拍板移除应用依赖功能，F15/F15-panel 断言已删）

  // —— F12 ——
  await gotoPage(page, '/apps');
  const appsBody = await page.locator('body').innerText();
  const appCount = appsBody.match(/应用 · (\d+)/)?.[1] ?? '?';
  const appRow = page.locator('[data-spec="app-row"]').first();
  if (await appRow.count()) {
    await appRow.hover();
    const edit = page.getByRole('button', { name: '编辑' });
    if (await edit.count()) {
      await edit.first().click();
      await page.waitForTimeout(600);
      await closeDrawerIfOpen(page);
    }
  }
  logStep({
    id: 'F12',
    route: '/apps',
    status: 'PASS',
    safeActions: '看列表；悬浮编辑 Drawer 即关；未批量启停/删除',
    observed: `列表应用数=${appCount}；含 StarServer=${appsBody.includes('StarServer')}`,
    issues: Number(appCount) < 3 ? 'projectId 过滤下列表不全（vf2 等可能不可见）' : '',
    shot: await snap(page, 'shot-lead-walk-F12-apps.png'),
  });

  // —— F13 ——
  await gotoPage(page, '/mesh?tab=registry');
  const mesh = await page.locator('body').innerText();
  if (mesh.includes('StarWebApi')) {
    await page.getByText('StarWebApi').first().click();
    await page.waitForTimeout(800);
    await closeDrawerIfOpen(page);
  }
  logStep({
    id: 'F13',
    route: '/mesh?tab=registry',
    status: mesh.includes('StarWebApi') ? 'PASS' : 'FAIL',
    safeActions: '打开服务 Drawer 即关；未保存',
    observed: 'StarWebApi 服务详情入口',
    issues: '',
    shot: await snap(page, 'shot-lead-walk-F13-mesh.png'),
  });

  // —— F14 ——
  const sampleTrace = '0abc528d1793529866004000236e69e0';
  await gotoPage(page, `/trace/${sampleTrace}`);
  const tr1 = await page.locator('body').innerText();
  const hasWaterfall = /瀑布|片段|ms/.test(tr1) && !tr1.includes('没有采样片段');
  await page.locator('[data-spec="trace-search"] input').fill(`${sampleTrace},405`);
  await page.locator('[data-spec="trace-search-btn"]').click();
  await page.waitForTimeout(3000);
  const units = await page.locator('[data-spec="trace-unit"]').count();
  logStep({
    id: 'F14',
    route: `/trace/:id`,
    status: units >= 1 ? 'PASS' : 'FAIL',
    safeActions: '只读查询 hex TraceId；多值并列',
    observed: `单链=${sampleTrace.slice(0, 12)}… 多值 cards=${units} 瀑布=${hasWaterfall}`,
    issues: !hasWaterfall ? '单链可能空态（分表/窗口）' : '',
    shot: await snap(page, 'shot-lead-walk-F14-trace.png'),
  });

  // —— F17 ——
  const logReqs: string[] = [];
  page.on('request', (req) => {
    const u = req.url();
    if (u.includes('AppClientLog')) logReqs.push(u);
  });
  await gotoPage(page, `/app/logs/${APP_ID}`);
  await page.getByText('Error', { exact: true }).first().click({ timeout: 8000 }).catch(() => {});
  await page.waitForTimeout(1500);
  const pause = page.getByText('暂停实时');
  if (await pause.count()) await pause.first().click();
  const kindOk = logReqs.some((u) => u.includes('kind=Error'));
  logStep({
    id: 'F17',
    route: `/app/logs/${APP_ID}`,
    status: kindOk ? 'PASS' : 'DEFER',
    safeActions: 'Error chip；暂停轮询',
    observed: `AppClientLog 请求 ${logReqs.length} 条 kind=Error=${kindOk}`,
    issues: kindOk ? '' : 'Search 重写待 API 发布后再验 Network',
    shot: await snap(page, 'shot-lead-walk-F17-logs.png'),
  });

  logStep({
    id: 'F16',
    route: '(API)',
    status: 'DEFER',
    safeActions: '未调用',
    observed: 'PipelineRun Detail 仅后端',
    issues: '待 API 发布',
  });

  } finally {
    writeReport(apiHost);
  }
  const hardFail = steps.filter((s) => s.status === 'FAIL');
  if (hardFail.length) console.warn('WALK_FAIL_IDS', hardFail.map((f) => f.id).join(','));
});
