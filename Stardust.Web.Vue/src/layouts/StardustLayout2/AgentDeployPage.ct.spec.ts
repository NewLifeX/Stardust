import { expect, test, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(400);
}

// story stub 把请求挂 window.__agentCalls，这里读取/清空（NodesPage.batchCalls 同法）
async function clearCalls(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { __agentCalls?: unknown[] }).__agentCalls = [];
  });
}

async function calls(page: Page): Promise<Array<{ url: string; body: unknown }>> {
  return page.evaluate(
    () => (window as unknown as { __agentCalls?: Array<{ url: string; body: unknown }> }).__agentCalls ?? [],
  );
}

// 四项必填 + hosts 双地址（C06：测试连接只发第一个；C07：确认框「2 台」）
async function fillForm(page: Page): Promise<void> {
  await page.locator('[data-spec="agent-deploy-hosts"]').fill('10.0.0.12, 10.0.0.13');
  await page.locator('[data-spec="agent-deploy-username"]').fill('root');
  await page.locator('[data-spec="agent-deploy-password"]').fill('p@ss');
  await page.locator('[data-spec="agent-deploy-serverurl"]').fill('http://10.0.0.2:6600');
}

test('首屏：九字段在位 + Index 预填标 + firstHost 提示 + 执行位置默认 server', async ({ page }) => {
  await openStory(page, 'AgentDeployPage/Default');
  for (const s of ['hosts', 'port', 'ostype', 'username', 'password', 'serverurl', 'downloadurl', 'dotnet', 'executeon']) {
    await expect(page.locator(`[data-spec="agent-deploy-${s}"]`)).toBeVisible();
  }
  // C02：Index 预填进 serverUrl/downloadUrl
  await expect(page.locator('[data-spec="agent-deploy-serverurl"]')).toHaveValue('http://10.0.0.2:6600');
  await expect(page.locator('[data-spec="agent-deploy-downloadurl"]')).toHaveValue('http://x.newlifex.com/star/');
  await expect(page.locator('[data-spec="agent-deploy-form-card"]')).toContainText('(已从服务端预填)');
  // C06：仅测第一个地址的常驻提示
  await expect(page.locator('[data-spec="agent-deploy-firsthost-hint"]')).toContainText('测试连接仅测第一个地址');
  await expect(page.locator('[data-spec="agent-deploy-executeon-server"]')).toBeChecked();
  await expect(page.locator('[data-spec="agent-deploy-web-hint"]')).toHaveCount(0);
});

test('C05 必填校验拦截：空表单点开始部署→错误条四行+不发 Deploy 请求+不弹确认框', async ({ page }) => {
  await openStory(page, 'AgentDeployPage/Default');
  await clearCalls(page);
  // Index 预填了 serverUrl——四项全空才触发「服务器地址不能为空」，先清掉
  await page.locator('[data-spec="agent-deploy-serverurl"]').fill('');
  await page.locator('[data-spec="agent-deploy-start"]').click();
  const errs = page.locator('[data-spec="agent-deploy-errors"]');
  await expect(errs).toBeVisible();
  await expect(errs).toContainText('目标主机不能为空');
  await expect(errs).toContainText('用户名不能为空');
  await expect(errs).toContainText('密码不能为空');
  await expect(errs).toContainText('服务器地址不能为空');
  // 旧壳无拦截差异：本壳必须零请求
  const list = await calls(page);
  expect(list.filter((c) => c.url.includes('Deploy'))).toHaveLength(0);
  await expect(page.locator('[data-spec="agent-deploy-confirm"]')).toHaveCount(0);
});

test('C06 测试连接失败分支（TestFail）：红条原文透出 + 仅发第一个地址 + 按钮恢复', async ({ page }) => {
  await openStory(page, 'AgentDeployPage/TestFail');
  await clearCalls(page);
  await fillForm(page);
  await page.locator('[data-spec="agent-deploy-test"]').click();
  const res = page.locator('[data-spec="agent-deploy-test-result"]');
  await expect(res).toBeVisible();
  await expect(res).toContainText('SSH 连接失败: Connection refused');
  const list = await calls(page);
  const tc = list.find((c) => c.url.includes('TestConnection'));
  expect(tc).toBeTruthy();
  // C06：hosts 两个地址，query 只带第一个
  expect(String((tc!.body as Record<string, unknown>).host)).toBe('10.0.0.12');
  // 请求结束按钮恢复可点
  await expect(page.locator('[data-spec="agent-deploy-test"]')).toBeEnabled();
});

test('C09 web 分支：切 web 出警示条，确认框显示 web 执行位置与台数', async ({ page }) => {
  await openStory(page, 'AgentDeployPage/Default');
  await page.locator('[data-spec="agent-deploy-executeon-web"]').check();
  await expect(page.locator('[data-spec="agent-deploy-web-hint"]')).toContainText('StarWeb 本地执行功能暂未实现');
  await fillForm(page);
  await page.locator('[data-spec="agent-deploy-start"]').click();
  const confirm = page.locator('[data-spec="agent-deploy-confirm"]');
  await expect(confirm).toBeVisible();
  await expect(confirm.locator('[data-spec="guard-detail"]')).toContainText('StarWeb 本地（暂未实现）');
  await expect(confirm.locator('[data-spec="guard-detail"]')).toContainText('2 台目标主机');
  // 取消不发请求
  await confirm.locator('[data-spec="guard-cancel"]').click();
  await expect(page.locator('[data-spec="agent-deploy-confirm"]')).toHaveCount(0);
  const list = await calls(page);
  expect(list.filter((c) => c.url.includes('Deploy'))).toHaveLength(0);
});

test('C08 部署成功流：确认执行→结果表 2 行+汇总+输出弹窗两态+Pascal body', async ({ page }) => {
  await openStory(page, 'AgentDeployPage/Default');
  await clearCalls(page);
  await fillForm(page);
  await page.locator('[data-spec="agent-deploy-start"]').click();
  await page.locator('[data-spec="agent-deploy-confirm"]').locator('[data-spec="guard-confirm"]').click();
  // 汇总 + 双色结果行
  await expect(page.locator('[data-spec="agent-deploy-summary"]')).toContainText('部署完成：成功 1 个，失败 1 个');
  const rows = page.locator('[data-spec="agent-deploy-result-row"]');
  await expect(rows).toHaveCount(2);
  await expect(page.locator('[data-spec="agent-deploy-result-host-10.0.0.12"]')).toBeVisible();
  await expect(page.locator('[data-spec="agent-deploy-result-host-10.0.0.13"]')).toBeVisible();
  // Deploy 请求体 PascalCase 对齐 AgentDeployModel
  const list = await calls(page);
  const dep = list.find((c) => c.url.includes('Deploy'));
  expect(dep).toBeTruthy();
  const body = (dep!.body ?? {}) as Record<string, unknown>;
  expect(body.Hosts).toBe('10.0.0.12, 10.0.0.13');
  expect(body.UserName).toBe('root');
  expect(body.OSType).toBe('Linux');
  expect(body.DotnetVersion).toBe(9);
  // 输出弹窗：成功行有输出，失败行「无输出」
  await page.locator('[data-spec="agent-deploy-output-btn-10.0.0.12"]').click();
  const modal = page.locator('[data-spec="agent-deploy-output-modal"]');
  await expect(modal).toBeVisible();
  await expect(page.locator('[data-spec="agent-deploy-output-text"]')).toContainText('StarAgent 安装完成');
  await modal.locator('.el-dialog__headerbtn').click();
  // el-dialog 关闭默认隐藏 DOM 不销毁（无 destroy-on-close），断隐藏不断计数
  await expect(modal).toBeHidden();
  await page.locator('[data-spec="agent-deploy-output-btn-10.0.0.13"]').click();
  await expect(page.locator('[data-spec="agent-deploy-output-text"]')).toContainText('无输出');
});

test('重置：表单回默认+预填重放+结果区清空', async ({ page }) => {
  await openStory(page, 'AgentDeployPage/Default');
  await fillForm(page);
  await page.locator('[data-spec="agent-deploy-hosts"]').fill('10.9.9.9');
  await page.locator('[data-spec="agent-deploy-start"]').click();
  await page.locator('[data-spec="agent-deploy-confirm"]').locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="agent-deploy-result-row"]')).toHaveCount(2);
  await page.locator('[data-spec="agent-deploy-reset"]').click();
  await expect(page.locator('[data-spec="agent-deploy-hosts"]')).toHaveValue('');
  await expect(page.locator('[data-spec="agent-deploy-username"]')).toHaveValue('');
  await expect(page.locator('[data-spec="agent-deploy-password"]')).toHaveValue('');
  // C02：重置后重新消费 Index 预填
  await expect(page.locator('[data-spec="agent-deploy-serverurl"]')).toHaveValue('http://10.0.0.2:6600');
  await expect(page.locator('[data-spec="agent-deploy-result-card"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="agent-deploy-errors"]')).toHaveCount(0);
});
