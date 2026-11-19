import { expect, test, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(400);
}

test('Mixed：五节点行渲染，名称+mono IP，chip 计数正确', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await expect(page.locator('[data-spec="nodes-row"]')).toHaveCount(5);
  await expect(page.locator('[data-spec="nodes-row"]').nth(0)).toContainText('node-a');
  await expect(page.locator('[data-spec="nodes-row"]').nth(0)).toContainText('10.0.0.1');
  // chip 计数：全部5 / 正常2 / 高负载2 / 离线1
  const chips = page.locator('[data-spec="nodes-filter-chip"]');
  await expect(chips.nth(0)).toContainText('全部 5');
  await expect(chips.nth(1)).toContainText('正常 2');
  await expect(chips.nth(2)).toContainText('高负载 2');
  await expect(chips.nth(3)).toContainText('离线 1');
  // 离线行 CPU/内存列显示 —
  await expect(page.locator('[data-spec="nodes-row"][data-tone="offline"]')).toContainText('node-c');
});

test('过滤：chip 切离线只留 1 行，搜索框按名称过滤', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-filter-chip"][data-key="offline"]').click();
  await expect(page.locator('[data-spec="nodes-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="nodes-row"]')).toContainText('node-c');

  // 切回全部，再按 IP 搜索
  await page.locator('[data-spec="nodes-filter-chip"][data-key="all"]').click();
  await page.locator('[data-spec="nodes-search"]').fill('10.0.0.2');
  await expect(page.locator('[data-spec="nodes-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="nodes-row"]')).toContainText('node-b');
});

test('勾选：勾一行出现批量卡「已选 1 台」+五动作可点，取消后消失（C05/C06）', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await expect(page.locator('[data-spec="nodes-batch"]')).toHaveCount(0);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await expect(page.locator('[data-spec="nodes-batch"]')).toBeVisible();
  await expect(page.locator('[data-spec="nodes-batch"]')).toContainText('已选 1 台');
  // 五动作按钮可点（前端无权限位数据源，不做权限禁用——旅程 S3 如实）
  for (const key of ['checkupgrade', 'synctime', 'restart', 'reboot', 'execute']) {
    await expect(page.locator(`[data-spec="node-online-batch-${key}"]`)).toBeEnabled();
  }
  // 命令/参数输入框在位
  await expect(page.locator('[data-spec="node-online-batch-command"]')).toBeVisible();
  await expect(page.locator('[data-spec="node-online-batch-argument"]')).toBeVisible();
  // 旧三禁用按钮（升级 Agent/安装 .NET/下发指令）已由五动作替代
  await expect(page.locator('[data-spec="nodes-batch-upgrade"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="nodes-batch-dotnet"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="nodes-batch-cmd"]')).toHaveCount(0);
  // 取消清空
  await page.locator('[data-spec="nodes-batch-clear"]').click();
  await expect(page.locator('[data-spec="nodes-batch"]')).toHaveCount(0);
});

test('点行打开抽屉：概览 tab 显示 KV 与 CPU 空态，切到应用 tab 显示应用列表', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await expect(page.locator('[data-spec="drawer"]')).toBeVisible();
  await expect(page.locator('[data-spec="drawer"]')).toContainText('node-a');
  // 概览：CPU 空态 + KV 行（IP/机房/Agent 版本等）
  await expect(page.locator('[data-spec="nodes-drawer-cpu-empty"]')).toContainText('CPU 走势待接入');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('Agent 版本');
  await expect(page.locator('[data-spec="drawer"]')).toContainText('10.0.0.1');

  // 切到「应用进程」tab：node-a 有 2 个应用
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="apps"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-app"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="nodes-drawer-app"]').nth(0)).toContainText('order-api');
});

test('终端按钮点击不发 toast（走 requestTermNode 通道）；抽屉升级仍 disabled、安装 .NET 已解禁可开面板（CARD-NODE-08）', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  // 表格行内终端按钮：现在走 ui.store.requestTermNode，不再 toast
  await page.locator('[data-spec="nodes-row-terminal"]').nth(0).click();
  await expect(page.locator('.el-notification, .sd-toast')).toHaveCount(0);

  // 打开抽屉：升级 Agent 仍 disabled（后端无批量接口），「安装 .NET」已解禁（CARD-NODE-08 接线）
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await expect(page.locator('[data-spec="nodes-drawer-upgrade"]')).toBeDisabled();
  await page.locator('[data-spec="nodes-drawer-dotnet"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-dotnet-panel"]')).toBeVisible();
  await expect(page.locator('[data-spec="nodes-drawer-terminal"]')).toBeVisible();
});

test('Empty：空态文案，表格「没有匹配的节点」', async ({ page }) => {
  await openStory(page, 'NodesPage/Empty');
  await expect(page.locator('[data-spec="nodes-sky-empty"]')).toContainText('还没有节点');
  await expect(page.locator('[data-spec="nodes-empty"]')).toContainText('没有匹配的节点');
});

test('Failed：可见错误条 + 重试按钮', async ({ page }) => {
  await openStory(page, 'NodesPage/Failed');
  await expect(page.locator('[data-spec="nodes-error"]')).toContainText('节点加载失败');
  await expect(page.locator('[data-spec="nodes-retry"]')).toBeVisible();
});

test('命令页签：node-a 2 行，9 列表头，trace 链契约页，无 traceId 显示 —', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  // 懒加载：切到命令页签才拉取
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="command"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-cmd-table"]')).toBeVisible();
  await expect(page.locator('[data-spec="nodes-drawer-cmd-row"]')).toHaveCount(2);
  // 首行：命令 mono 高亮 + 参数 + 追踪链 /trace/{traceId}（C06 契约页先例）
  const first = page.locator('[data-spec="nodes-drawer-cmd-row"]').nth(0);
  await expect(first).toContainText('deploy/start');
  await expect(first).toContainText('{"name":"order-api"}');
  await expect(first.locator('[data-spec="nodes-drawer-cmd-trace"]')).toHaveAttribute('href', '/trace/trc-abc-123');
  // 次行无 traceId → 「—」
  await expect(page.locator('[data-spec="nodes-drawer-cmd-row"]').nth(1)).toContainText('check/upgrade');
  await expect(page.locator('[data-spec="nodes-drawer-cmd-row"]').nth(1)).toContainText('已是最新版本');
  await expect(page.locator('[data-spec="nodes-drawer-cmd-row"]').nth(1).locator('[data-spec="nodes-drawer-cmd-trace"]')).toHaveCount(0);
  // 页签按钮带计数？不带（命令页签无计数徽标，与设计稿一致）
  await expect(page.locator('[data-spec="nodes-drawer-tab"][data-key="command"]')).toContainText('命令');
});

test('命令页签空态：node-d 无命令显示「暂无命令记录」', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-row"]').nth(3).click();
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="command"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-cmd-empty"]')).toContainText('暂无命令记录');
  await expect(page.locator('[data-spec="nodes-drawer-cmd-table"]')).toHaveCount(0);
});

test('命令页签失败态：可见错误 + 重试按钮（重试仍失败保持错误可见）', async ({ page }) => {
  await openStory(page, 'NodesPage/CmdFailed');
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="command"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-cmd-error"]')).toContainText('命令记录加载失败：cmd stub 500');
  await page.locator('[data-spec="nodes-drawer-cmd-retry"]').click();
  // 重试请求仍失败 → 错误条不消失（ASYNC 反馈规范：失败可见，不静默）
  await expect(page.locator('[data-spec="nodes-drawer-cmd-error"]')).toContainText('cmd stub 500');
});

// ---------- 历史页签（CARD-NODE-09，2026-11-11） ----------
// success 过滤参数捕获同款 window.__batchCalls（story stubReq 写入）

test('历史页签：node-a 2 行，是/否，无 traceId 显示 —，头部「在迁移页打开」新窗口链接', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  // 懒加载：切到历史页签才拉取
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="history"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-hist-table"]')).toBeVisible();
  await expect(page.locator('[data-spec="nodes-drawer-hist-row"]')).toHaveCount(2);
  // 首行：登录/是（成功绿）/内容/追踪 trc-his-001（纯文本，与迁移页操作/追踪列呈现一致）
  const first = page.locator('[data-spec="nodes-drawer-hist-row"]').nth(0);
  await expect(first).toContainText('登录');
  await expect(first).toContainText('是');
  await expect(first).toContainText('节点上线');
  await expect(first).toContainText('trc-his-001');
  await expect(first).toContainText('2026-11-10 21:00:05');
  await expect(first).toContainText('10.0.0.1');
  // 次行：升级/否（失败红）+ 追踪空「—」
  const second = page.locator('[data-spec="nodes-drawer-hist-row"]').nth(1);
  await expect(second).toContainText('升级');
  await expect(second).toContainText('否');
  await expect(second).toContainText('下载超时');
  await expect(second).toContainText('—');
  // 头部「在迁移页打开」：新窗口 /Nodes/Node?Id=1（C04 断言零改写两全）
  const link = page.locator('[data-spec="nodes-drawer-open-migration"]');
  await expect(link).toContainText('在迁移页打开');
  await expect(link).toHaveAttribute('href', '/Nodes/Node?Id=1');
  await expect(link).toHaveAttribute('target', '_blank');
});

test('历史页签 success 过滤：请求带 success=false/true 字符串，列表随过滤收放', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="history"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-hist-row"]')).toHaveCount(2);
  // 选「失败」→ success='false' 字符串（后端 ToBoolean 直读）
  await page.locator('[data-spec="nodes-drawer-hist-success"]').selectOption('false');
  let calls = (await batchCalls(page)).filter((c) => c.url === '/Nodes/NodeHistory');
  expect(calls[calls.length - 1].params.success).toBe('false');
  await expect(page.locator('[data-spec="nodes-drawer-hist-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="nodes-drawer-hist-row"]').nth(0)).toContainText('升级');
  // 切「成功」→ success='true'，列表回登录
  await page.locator('[data-spec="nodes-drawer-hist-success"]').selectOption('true');
  calls = (await batchCalls(page)).filter((c) => c.url === '/Nodes/NodeHistory');
  expect(calls[calls.length - 1].params.success).toBe('true');
  await expect(page.locator('[data-spec="nodes-drawer-hist-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="nodes-drawer-hist-row"]').nth(0)).toContainText('登录');
});

test('历史页签空态：node-d 无历史显示「暂无历史」', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-row"]').nth(3).click();
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="history"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-hist-empty"]')).toContainText('暂无历史');
  await expect(page.locator('[data-spec="nodes-drawer-hist-table"]')).toHaveCount(0);
});

// ---------- 批量运维（CARD-NODE-11，2026-11-09） ----------
// 请求捕获在浏览器侧 window.__batchCalls（story stub 写入；CT 不能 Node 侧 import story——
// 会连带转译 .vue 文件炸 playwright 的 esbuild）。
interface CapturedBatch { url: string; params: Record<string, unknown> }
async function batchCalls(page: Page): Promise<CapturedBatch[]> {
  return page.evaluate(() => (window as unknown as { __batchCalls?: CapturedBatch[] }).__batchCalls ?? []);
}
async function batchCaptureReset(page: Page): Promise<void> {
  await page.evaluate(() => { (window as unknown as { __batchCalls?: CapturedBatch[] }).__batchCalls = []; });
}

test('执行命令未输入命令警告「请输入命令」，不发请求（C19）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-online-batch-execute"]').click();
  await expect(page.locator('[data-spec="node-online-batch-error"]')).toContainText('请输入命令');
  expect(await batchCalls(page)).toHaveLength(0);
});

test('检查更新：请求 keys=在线会话 Id（nodeId 1→11），汇总条契约逐字+逐节点展开（C06/C17）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-online-batch-checkupgrade"]').click();
  // 请求：GET /Nodes/NodeOnline/CheckUpgrade?keys=11（在线快照 nodeId=1 取 updateTime 最新 id=11）
  const calls = await batchCalls(page);
  expect(calls).toHaveLength(1);
  expect(calls[0].url).toBe('/Nodes/NodeOnline/CheckUpgrade');
  expect(calls[0].params.keys).toBe('11');
  // 汇总条首行逐字=后端契约
  await expect(page.locator('[data-spec="node-online-result-summary"]')).toHaveText(
    '操作成功！下发1个，响应1个，超时0个',
  );
  // 展开逐节点回复：node-a / 已是最新版本
  await page.locator('[data-spec="node-online-result-toggle"]').click();
  await expect(page.locator('[data-spec="node-online-result-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="node-online-result-row"]')).toContainText('node-a');
  await expect(page.locator('[data-spec="node-online-result-row"]')).toContainText('已是最新版本');
  // 收起
  await page.locator('[data-spec="node-online-result-toggle"]').click();
  await expect(page.locator('[data-spec="node-online-result-row"]')).toHaveCount(0);
});

test('重启系统：GuardModal 确认文案（台数+离线后果），取消不发请求、确认才发（C18）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-online-batch-reboot"]').click();
  const guard = page.locator('[data-spec="node-online-confirm-reboot"]');
  await expect(guard).toBeVisible();
  await expect(guard).toContainText('重启系统');
  await expect(guard).toContainText('1 台');
  await expect(guard).toContainText('短暂离线后自动恢复');
  // GuardModal 用例约定：等 x-pop 320ms 入场动画走完再点（GuardModal.ct.spec.ts openStory 同款）
  await page.waitForTimeout(500);
  // 取消 → 不发请求
  await guard.locator('[data-spec="guard-cancel"]').click();
  await expect(page.locator('[data-spec="node-online-confirm-reboot"]')).toHaveCount(0);
  expect(await batchCalls(page)).toHaveLength(0);
  // 再点 → 确认 → 发请求
  await page.locator('[data-spec="node-online-batch-reboot"]').click();
  await page.waitForTimeout(500);
  await page.locator('[data-spec="node-online-confirm-reboot"]').locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="node-online-result-summary"]')).toContainText('操作成功！');
  const calls = await batchCalls(page);
  expect(calls).toHaveLength(1);
  expect(calls[0].url).toBe('/Nodes/NodeOnline/Reboot');
  expect(calls[0].params.keys).toBe('11');
});

test('执行命令：确认框回显命令与参数，确认后 Execute 请求带 command/argument（C19）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-online-batch-command"]').fill('deploy/start');
  await page.locator('[data-spec="node-online-batch-argument"]').fill('{"name":"order-api"}');
  await page.locator('[data-spec="node-online-batch-execute"]').click();
  const guard = page.locator('[data-spec="node-online-confirm-execute"]');
  await expect(guard).toBeVisible();
  await expect(guard).toContainText('执行命令');
  await expect(guard).toContainText('deploy/start');
  await expect(guard).toContainText('{"name":"order-api"}');
  await page.waitForTimeout(500);
  await guard.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="node-online-result-summary"]')).toContainText('操作成功！');
  const calls = await batchCalls(page);
  expect(calls).toHaveLength(1);
  expect(calls[0].url).toBe('/Nodes/NodeOnline/Execute');
  expect(calls[0].params.keys).toBe('11');
  expect(calls[0].params.command).toBe('deploy/start');
  expect(calls[0].params.argument).toBe('{"name":"order-api"}');
});

test('批量失败：后端 code 400 → 失败条「批量操作失败：权限不足」可见，结束后按钮恢复（§11）', async ({ page }) => {
  await openStory(page, 'NodesPage/BatchFail');
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-online-batch-synctime"]').click();
  await expect(page.locator('[data-spec="node-online-batch-error"]')).toContainText('批量操作失败：权限不足');
  // 动作结束后按钮恢复可点（防重复禁用解除）
  await expect(page.locator('[data-spec="node-online-batch-synctime"]')).toBeEnabled();
});

// ---------- 性能页签（CARD-NODE-07） ----------

test('性能页签：切页签懒加载 24h 曲线，PageSize=1440（镜像后端换算），全空序列不进图（C11c）', async ({ page }) => {
  await openStory(page, 'NodesPage/Mixed');
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await expect(page.locator('[data-spec="drawer"]')).toBeVisible();

  // 性能页签按钮在位（data-key=perf，S1b）
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="perf"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-perf-chart"]')).toBeVisible();
  await expect(page.locator('[data-spec="nodes-drawer-perf-chart"]')).toContainText('近 24 小时性能');

  // 契约断言：懒加载请求 GET /Nodes/NodeData，PageSize=1440（period 未传 → 24*3600/60 默认；纯函数换算在前端主动发起）
  const calls = await page.evaluate(() => (window as unknown as { __batchCalls?: Array<{ url: string; params: Record<string, unknown> }> }).__batchCalls);
  const dataCall = (calls ?? []).filter((c) => c.url === '/Nodes/NodeData');
  expect(dataCall).toHaveLength(1);
  expect(dataCall[0].params.nodeId).toBe(1);
  expect(dataCall[0].params.PageSize).toBe(1440);

  // 「CPU 走势待接入」空态卡仍在概览页签（现状保持，裁定不撤）
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="overview"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-cpu-empty"]')).toContainText('CPU 走势待接入');
});

test('性能页签失败：NodeData 抛错 → 错误条「性能数据加载失败：stub 500」+ 重试按钮在位', async ({ page }) => {
  await openStory(page, 'NodesPage/PerfFail');
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await expect(page.locator('[data-spec="drawer"]')).toBeVisible();
  await page.locator('[data-spec="nodes-drawer-tab"][data-key="perf"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-perf-error"]')).toContainText('性能数据加载失败：stub 500');
  await expect(page.locator('[data-spec="nodes-drawer-perf-retry"]')).toBeVisible();

  // 空态（Empty story）：NodeData 空 → 「暂无性能数据」，不画假曲线
  await openStory(page, 'NodesPage/Empty');
  await expect(page.locator('[data-spec="nodes-row"]')).toHaveCount(0);
});

// ---------- 框架批量管理（CARD-NODE-08，2026-11-10：NodeFrameworkController） ----------
// ver「自动（最新）」=传空（ResolveForPush :334 自动匹配）；keys=Node 主键直连（与 NODE-11 在线会话 Id 不同）；
// 卸载 ver 必填无 kind（:134/:137）；高危先 GuardModal（仅本地栈节点实走）。

test('框架区：勾选后批量卡出现框架安装区，ver 默认「自动（最新）」、kind 默认自动、卸载默认空（C08）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await expect(page.locator('[data-spec="node-framework-batch"]')).toBeVisible();
  // 三下拉默认值：ver=''（自动最新）、kind='auto'（GetKindList 首项）、卸载 ver=''（请选择版本）
  await expect(page.locator('[data-spec="node-framework-batch-ver"]')).toHaveValue('');
  await expect(page.locator('[data-spec="node-framework-batch-kind"]')).toHaveValue('auto');
  await expect(page.locator('[data-spec="node-framework-batch-unver"]')).toHaveValue('');
  // 下拉选项来自 GetVersionList/GetKindList stub（8.0.11 / 9.0.0 / 自动 / 运行时）
  await expect(page.locator('[data-spec="node-framework-batch-ver"] option')).toHaveCount(3);
  await expect(page.locator('[data-spec="node-framework-batch-kind"] option')).toHaveCount(2);
  // 未勾选时批量卡不出现 → 框架区随卡隐藏
  await page.locator('[data-spec="nodes-batch-clear"]').click();
  await expect(page.locator('[data-spec="node-framework-batch"]')).toHaveCount(0);
});

test('安装默认「自动（最新）」：请求 ver 传空、kind=auto、keys=Node 主键（node-a+node-b → "1,2"），汇总条契约逐字（C09b）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="nodes-row"]').nth(1).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-framework-batch-install"]').click();
  const guard = page.locator('[data-spec="node-framework-confirm-install"]');
  await expect(guard).toBeVisible();
  await expect(guard).toContainText('批量安装 .NET 框架');
  await expect(guard).toContainText('2 台');
  await expect(guard).toContainText('自动（最新）');
  await page.waitForTimeout(500);
  await guard.locator('[data-spec="guard-confirm"]').click();
  const calls = await batchCalls(page);
  expect(calls).toHaveLength(1);
  expect(calls[0].url).toBe('/Nodes/NodeFramework/InstallFramework');
  // 裁定断言：ver 显式空串（非缺省）→ 后端 ResolveForPush 自动匹配最新启用包
  expect(calls[0].params.keys).toBe('1,2');
  expect(calls[0].params.ver).toBe('');
  expect(calls[0].params.kind).toBe('auto');
  await expect(page.locator('[data-spec="node-framework-result-summary"]')).toHaveText(
    '操作成功！下发1个，响应1个，超时0个',
  );
});

test('卸载未选版本：警告「请选择框架版本」（C07 逐字）不弹 guard 不发请求', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-framework-batch-uninstall"]').click();
  await expect(page.locator('[data-spec="node-framework-error"]')).toContainText('请选择框架版本');
  await expect(page.locator('[data-spec="node-framework-confirm-uninstall"]')).toHaveCount(0);
  expect(await batchCalls(page)).toHaveLength(0);
});

test('卸载选定版本：请求带 ver=8.0.11 且无 kind 字段（:134/:137 ver 必填）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-framework-batch-unver"]').selectOption('8.0.11');
  await page.locator('[data-spec="node-framework-batch-uninstall"]').click();
  const guard = page.locator('[data-spec="node-framework-confirm-uninstall"]');
  await expect(guard).toBeVisible();
  await expect(guard).toContainText('批量卸载 .NET 框架');
  await expect(guard).toContainText('8.0.11');
  await page.waitForTimeout(500);
  await guard.locator('[data-spec="guard-confirm"]').click();
  const calls = await batchCalls(page);
  expect(calls).toHaveLength(1);
  expect(calls[0].url).toBe('/Nodes/NodeFramework/UninstallFramework');
  expect(calls[0].params.keys).toBe('1');
  expect(calls[0].params.ver).toBe('8.0.11');
  expect('kind' in calls[0].params).toBe(false);
});

test('框架 Guard 取消：不发请求、guard 消失（高危红线）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-framework-batch-install"]').click();
  const guard = page.locator('[data-spec="node-framework-confirm-install"]');
  await expect(guard).toBeVisible();
  await page.waitForTimeout(500);
  await guard.locator('[data-spec="guard-cancel"]').click();
  await expect(page.locator('[data-spec="node-framework-confirm-install"]')).toHaveCount(0);
  expect(await batchCalls(page)).toHaveLength(0);
});

test('框架安装失败：后端全跳过清单 → 失败条「框架操作失败：没有可推送的节点！」可见，按钮恢复（§11）', async ({ page }) => {
  await openStory(page, 'NodesPage/BatchFail');
  await page.locator('[data-spec="nodes-row"]').nth(0).locator('[data-spec="nodes-row-check"]').check();
  await page.locator('[data-spec="node-framework-batch-install"]').click();
  await page.waitForTimeout(500);
  await page.locator('[data-spec="node-framework-confirm-install"]').locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('[data-spec="node-framework-error"]')).toContainText('框架操作失败');
  await expect(page.locator('[data-spec="node-framework-error"]')).toContainText('没有可推送的节点！');
  await expect(page.locator('[data-spec="node-framework-batch-install"]')).toBeEnabled();
});

test('抽屉单节点框架面板：点「安装 .NET」展开面板，安装请求 keys=该节点主键（node-a → 1）', async ({ page }) => {
  await openStory(page, 'NodesPage/Batch');
  await batchCaptureReset(page);
  await page.locator('[data-spec="nodes-row"]').nth(0).click();
  await expect(page.locator('[data-spec="drawer"]')).toBeVisible();
  // 抽屉按钮已解禁（原 disabled「后端暂未提供批量操作接口」），点开内联面板
  await page.locator('[data-spec="nodes-drawer-dotnet"]').click();
  await expect(page.locator('[data-spec="nodes-drawer-dotnet-panel"]')).toBeVisible();
  await expect(page.locator('[data-spec="nodes-drawer-dotnet-panel"]')).toContainText('node-a');
  await page.locator('[data-spec="node-framework-drawer-install"]').click();
  const guard = page.locator('[data-spec="node-framework-confirm-install"]');
  await expect(guard).toBeVisible();
  await page.waitForTimeout(500);
  await guard.locator('[data-spec="guard-confirm"]').click();
  const calls = await batchCalls(page);
  expect(calls).toHaveLength(1);
  expect(calls[0].url).toBe('/Nodes/NodeFramework/InstallFramework');
  expect(calls[0].params.keys).toBe('1');
  expect(calls[0].params.ver).toBe('');
  // 面板内结果条（双渲染共用 fwResult）
  await expect(page.locator('[data-spec="nodes-drawer-dotnet-panel"] [data-spec="node-framework-result-summary"]')).toContainText('操作成功！');
});
