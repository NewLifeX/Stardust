import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

test('Data：12 列表头与行内容（CARD-MW-01 BL-5 全列）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const ths = page.locator('[data-spec="mw-mysql-data"] thead th');
  await expect(ths).toHaveCount(12);
  await expect(ths.nth(0)).toContainText('编号');
  await expect(ths.nth(1)).toContainText('MySQL 节点');
  await expect(ths.nth(2)).toContainText('名称');
  await expect(ths.nth(3)).toContainText('启动时长');
  await expect(ths.nth(4)).toContainText('连接数');
  await expect(ths.nth(5)).toContainText('QPS');
  await expect(ths.nth(6)).toContainText('TPS');
  await expect(ths.nth(7)).toContainText('流量');
  await expect(ths.nth(8)).toContainText('InnoDB 缓冲池');
  await expect(ths.nth(9)).toContainText('命中率');
  await expect(ths.nth(10)).toContainText('慢查询');
  await expect(ths.nth(11)).toContainText('线程');
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(2);
  const first = page.locator('[data-spec="mw-mysql-data-row"]').first();
  // 节点列=候选映射；格式化列逐字（uptime/bytes/bufferPool/hitRate 由 logic 单测定格式，此处验链路）
  await expect(first).toContainText('主库节点');
  await expect(first).toContainText('主库@10.0.0.8');
  await expect(first).toContainText('2 天 3 小时');
  await expect(first).toContainText('12 / 100');
  await expect(first).toContainText('1.5 MB / 15 MB');
  await expect(first).toContainText('128 MB · 65536 页');
  await expect(first).toContainText('98.53%');
  // 只读卡（C02）：无新增/编辑/删除操作按钮
  await expect(page.locator('[data-spec="mw-mysql-data-create"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-mysql-data-edit"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-mysql-data-remove"]')).toHaveCount(0);
});

test('Data：服务端分页（total=46/pageSize=20 → 3 页）prev/next 状态与翻页', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await expect(page.locator('[data-spec="mw-mysql-data-page"]')).toContainText('第 1 / 3 页');
  const prev = page.locator('[data-spec="mw-mysql-data-prev"]');
  const next = page.locator('[data-spec="mw-mysql-data-next"]');
  await expect(prev).toBeDisabled();
  await expect(next).toBeEnabled();
  await next.click();
  await expect(page.locator('[data-spec="mw-mysql-data-page"]')).toContainText('第 2 / 3 页');
  await expect(prev).toBeEnabled();
  await expect(next).toBeEnabled();
});

test('Empty：本地库常态空态「暂无 MySQL 采样数据」（B1 一等公民）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/Empty');
  await expect(page.locator('[data-spec="mw-mysql-data-empty"]')).toContainText('暂无 MySQL 采样数据');
  await expect(page.locator('[data-spec="mw-mysql-data-empty"]')).toContainText('采集后');
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(0);
});

test('Loading：加载中态「MySQL 数据加载中…」（§11 异步反馈）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/Loading');
  await expect(page.locator('[data-spec="mw-mysql-data-loading"]')).toContainText('MySQL 数据加载中…');
  await expect(page.locator('[data-spec="mw-mysql-data-filter"]')).toHaveCount(0);
});

test('Failed：失败态展示原因 + 重试按钮存在（重试后桩仍失败保持失败态）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/Failed');
  await expect(page.locator('[data-spec="mw-mysql-data-failed"]')).toContainText('MySQL 数据加载失败');
  await expect(page.locator('[data-spec="mw-mysql-data-failed"]')).toContainText('stub 500');
  await page.locator('[data-spec="mw-mysql-data-retry"]').click();
  await expect(page.locator('[data-spec="mw-mysql-data-failed"]')).toBeVisible();
});

test('NodeFailed：候选失败不拖垮列表 + 降级提示 + 节点列回退 #id', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/NodeFailed');
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="middleware-node-error"]')).toContainText('节点候选加载失败');
  await expect(page.locator('[data-spec="middleware-node-error"]')).toContainText('节点服务离线');
  await expect(page.locator('[data-spec="mw-mysql-data-row"]').first()).toContainText('#3');
  await expect(page.locator('[data-spec="mw-mysql-data-row"]').first()).not.toContainText('主库节点');
});

test('Filter：筛选条结构（节点下拉/时间区间/关键字/时间注记）与 @change 即查不炸', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const filter = page.locator('[data-spec="mw-mysql-data-filter"]');
  await expect(filter).toBeVisible();
  await expect(page.locator('[data-spec="mw-mysql-data-filter-node"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-mysql-data-filter-dtstart"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-mysql-data-filter-dtend"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-mysql-data-filter-q"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-mysql-data-filter-time-note"]')).toContainText('按采样时间过滤');
  // C01：候选下拉含「全部节点」默认项 + 两个候选
  const nodeSelect = page.locator('[data-spec="mw-mysql-data-filter-node"]');
  await expect(nodeSelect.locator('option')).toHaveCount(3);
  // C03：选节点触发重查（桩恒同数据）→ 行仍在，页面不炸
  await nodeSelect.selectOption('3');
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(2);
  // 关键字 @change 触发重查
  await page.locator('[data-spec="mw-mysql-data-filter-q"]').fill('主库');
  await page.locator('[data-spec="mw-mysql-data-filter-q"]').blur();
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(2);
});

// ==================== CARD-MW-02 · MySQL 节点 ====================

test('Node：12 列表头与行内容 + 审计注记（CARD-MW-02 全列）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const ths = page.locator('[data-spec="mw-mysql-node"] thead th');
  await expect(ths).toHaveCount(12);
  await expect(ths.nth(0)).toContainText('编号');
  await expect(ths.nth(1)).toContainText('项目');
  await expect(ths.nth(2)).toContainText('名称');
  await expect(ths.nth(3)).toContainText('分类');
  await expect(ths.nth(4)).toContainText('地址');
  await expect(ths.nth(5)).toContainText('端口');
  await expect(ths.nth(6)).toContainText('用户名');
  await expect(ths.nth(7)).toContainText('数据库');
  await expect(ths.nth(8)).toContainText('版本');
  await expect(ths.nth(9)).toContainText('启用');
  await expect(ths.nth(10)).toContainText('更新时间');
  await expect(ths.nth(11)).toContainText('操作');
  await expect(page.locator('[data-spec="mw-mysql-node-audit-note"]')).toContainText('审计日志待平台接入');
  await expect(page.locator('[data-spec="mw-mysql-node-row"]')).toHaveCount(2);
  const first = page.locator('[data-spec="mw-mysql-node-row"]').first();
  await expect(first).toContainText('主库');
  await expect(first).toContainText('银河项目');
  await expect(first).toContainText('10.0.0.8');
  await expect(first).toContainText('3306');
  await expect(first).toContainText('启用');
  await expect(first).toContainText('2026-11-09 10:30');
  // 差异如实：卡内不出现密码（任何路径不渲染）
  await expect(page.locator('[data-spec="mw-mysql-node"]')).not.toContainText('密码');
  // 空值回退：项目 '—'、停用态
  const second = page.locator('[data-spec="mw-mysql-node-row"]').nth(1);
  await expect(second).toContainText('—');
  await expect(second).toContainText('停用');
  // RemoveCreateField：无新增按钮
  await expect(page.locator('[data-spec="mw-mysql-node-create"]')).toHaveCount(0);
});

test('Node：空态「暂无 MySQL 节点」（B1 一等公民）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/Empty');
  await expect(page.locator('[data-spec="mw-mysql-node-empty"]')).toContainText('暂无 MySQL 节点');
  await expect(page.locator('[data-spec="mw-mysql-node-row"]')).toHaveCount(0);
});

test('Node：失败态独立展示 + 重试 + 不拖垮数据卡（NodeListFailed）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/NodeListFailed');
  await expect(page.locator('[data-spec="mw-mysql-node-failed"]')).toContainText('MySQL 节点加载失败');
  await expect(page.locator('[data-spec="mw-mysql-node-failed"]')).toContainText('节点查询失败');
  await page.locator('[data-spec="mw-mysql-node-retry"]').click();
  await expect(page.locator('[data-spec="mw-mysql-node-failed"]')).toBeVisible();
  // 数据卡正常（独立区块互不拖垮）
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(2);
  // 候选正常 → 无 MW-01 降级提示
  await expect(page.locator('[data-spec="middleware-node-error"]')).toHaveCount(0);
});

test('Node：筛选条结构（地址精确/端口/项目/启用三态/时间区间/关键字）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await expect(page.locator('[data-spec="mw-mysql-node-filter-server"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-mysql-node-filter-server-note"]')).toContainText('精确匹配');
  await expect(page.locator('[data-spec="mw-mysql-node-filter-port"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-mysql-node-filter-project"]')).toBeVisible();
  const enable = page.locator('[data-spec="mw-mysql-node-filter-enable"]');
  await expect(enable.locator('option')).toHaveCount(3);
  await expect(page.locator('[data-spec="mw-mysql-node-filter-time-note"]')).toContainText('按更新时间过滤');
  await expect(page.locator('[data-spec="mw-mysql-node-filter-q"]')).toBeVisible();
  // 改筛选触发重查不炸（桩恒同数据）
  await page.locator('[data-spec="mw-mysql-node-filter-q"]').fill('主库');
  await page.locator('[data-spec="mw-mysql-node-filter-q"]').blur();
  await expect(page.locator('[data-spec="mw-mysql-node-row"]')).toHaveCount(2);
});

test('Node：深链 Id 单条直达（initialNodeFilter id=5 → 备库一行）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/NodeDeepLink');
  await expect(page.locator('[data-spec="mw-mysql-node-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="mw-mysql-node-row"]').first()).toContainText('备库');
});

test('Node：编辑抽屉回填 + 名称必填 + 保存全量 PUT（S2/S3）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await page.evaluate(() => { (window as unknown as { __mwNodeCalls?: string[] }).__mwNodeCalls = []; });
  await page.locator('[data-spec="mw-mysql-node-edit"]').first().click();
  const drawer = page.locator('[data-spec="mw-mysql-node-drawer"]');
  await expect(drawer).toBeVisible();
  const name = page.locator('[data-spec="mw-mysql-node-form-name"]');
  await expect(name).toHaveValue('主库');
  // 密码掩码回显（安全：type=password）
  const pwd = page.locator('[data-spec="mw-mysql-node-form-password"]');
  await expect(pwd).toHaveValue('seed-pwd');
  await expect(pwd).toHaveAttribute('type', 'password');
  // 名称清空 → 必填拦截，不发 PUT
  await name.fill('');
  await page.locator('[data-spec="mw-mysql-node-save"]').click();
  await expect(page.locator('[data-spec="mw-mysql-node-form-name-error"]')).toContainText('名称不能为空');
  let calls = await page.evaluate(() => (window as unknown as { __mwNodeCalls?: string[] }).__mwNodeCalls ?? []);
  expect(calls).toHaveLength(0);
  // 改名保存 → PUT /MySql/MySqlNode/3 + toast + 抽屉关闭
  await name.fill('主库-A');
  await page.locator('[data-spec="mw-mysql-node-save"]').click();
  await expect(drawer).toBeHidden();
  calls = await page.evaluate(() => (window as unknown as { __mwNodeCalls?: string[] }).__mwNodeCalls ?? []);
  expect(calls).toContain('PUT /MySql/MySqlNode/3');
  await expect(page.locator('.sd-toast')).toContainText('保存成功');
});

test('Node：刷新 §3.5 转圈禁用 + toast 后端 message + 完成恢复（S4/C05）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await page.evaluate(() => { (window as unknown as { __mwNodeCalls?: string[] }).__mwNodeCalls = []; });
  const btn = page.locator('[data-spec="mw-mysql-node-refresh"]').first();
  await btn.click();
  await expect(btn).toBeDisabled();
  await expect(btn).toContainText('刷新中…');
  await expect(page.locator('.sd-toast')).toContainText('刷新[主库]成功！');
  await expect(btn).toBeEnabled();
  await expect(btn).not.toContainText('刷新中…');
  const calls = await page.evaluate(() => (window as unknown as { __mwNodeCalls?: string[] }).__mwNodeCalls ?? []);
  expect(calls).toContain('POST /MySql/MySqlNode/Refresh');
});

test('Node：监控下钻 → 数据卡切到该节点（旅程 -1 替代路径）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await page.locator('[data-spec="mw-mysql-node-monitor"]').first().click();
  await expect(page.locator('.sd-toast')).toContainText('已切到节点「主库」的采样监控');
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(2);
});

// ==================== MW-03 · Redis 数据（CARD-MW-03） ====================

test('Redis Data：八列表头与行内容（CARD-MW-03 BL-5 全列，无 Id/命令外字段）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const ths = page.locator('[data-spec="mw-redis-data"] thead th');
  await expect(ths).toHaveCount(8);
  await expect(ths.nth(0)).toContainText('Redis 节点');
  await expect(ths.nth(1)).toContainText('名称');
  await expect(ths.nth(2)).toContainText('速度');
  await expect(ths.nth(3)).toContainText('连接数');
  await expect(ths.nth(4)).toContainText('已用内存');
  await expect(ths.nth(5)).toContainText('Keys 数');
  await expect(ths.nth(6)).toContainText('命令数');
  await expect(ths.nth(7)).toContainText('创建时间');
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(3);
  const first = page.locator('[data-spec="mw-redis-data-row"]').first();
  await expect(first).toContainText('主缓存');
  await expect(first).toContainText('cache@10.0.0.9');
  await expect(first).toContainText('1200');
  await expect(first).toContainText('8');
  await expect(first).toContainText('512 MB');
  await expect(first).toContainText('9000');
  // 只读卡（C05）：无新增/编辑/删除操作按钮
  await expect(page.locator('[data-spec="mw-redis-data-create"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-redis-data-edit"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-redis-data-remove"]')).toHaveCount(0);
});

test('Redis 曲线区：未选节点=引导文案；筛选条含时间注记与关键字（C04）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await expect(page.locator('[data-spec="mw-redis-data-chart-empty"]')).toContainText('选择节点后查看性能曲线');
  await expect(page.locator('[data-spec="mw-redis-data-chart-canvas"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-redis-data-filter-time-note"]')).toContainText('按采样时间过滤');
  await expect(page.locator('[data-spec="mw-redis-data-filter-q"]')).toBeVisible();
  // 裁定 2 统一名全 tab 两处（数据卡+队列卡）——数据卡断言用容器限定
  await expect(page.locator('[data-spec="mw-redis-data-filter"] [data-spec="mw-redis-node-lov"] option')).toHaveCount(2);
});

test('Redis 选节点：六序列曲线渲染 + 45 行本地分页切片（3 页，尾页 5 行）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await page.locator('[data-spec="mw-redis-data-filter"] [data-spec="mw-redis-node-lov"]').selectOption('7');
  // 曲线（C03）：canvas 渲染出现，空态文案消失
  await expect(page.locator('[data-spec="mw-redis-data-chart-canvas"] canvas')).toBeVisible();
  await expect(page.locator('[data-spec="mw-redis-data-chart-empty"]')).toHaveCount(0);
  // 本地分页：45 行 → 3 页；第 1 页切片 20 行
  await expect(page.locator('[data-spec="mw-redis-data-page"]')).toContainText('第 1 / 3 页');
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(20);
  const next = page.locator('[data-spec="mw-redis-data-next"]');
  const prev = page.locator('[data-spec="mw-redis-data-prev"]');
  await expect(prev).toBeDisabled();
  await next.click();
  await expect(page.locator('[data-spec="mw-redis-data-page"]')).toContainText('第 2 / 3 页');
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(20);
  await next.click();
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(5);
  await expect(next).toBeDisabled();
  await prev.click();
  await expect(page.locator('[data-spec="mw-redis-data-page"]')).toContainText('第 2 / 3 页');
});

test('Redis Empty/Failed/Loading：三态文案与降级（B1 一等公民 + §11 反馈）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/Empty');
  await expect(page.locator('[data-spec="mw-redis-data-empty"]')).toContainText('暂无 Redis 采样数据');
  await expect(page.locator('[data-spec="mw-redis-data-empty"]')).toContainText('采集后');
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(0);

  await openStory(page, 'MiddlewareTab/Failed');
  await expect(page.locator('[data-spec="mw-redis-data-failed"]')).toContainText('Redis 数据加载失败');
  await expect(page.locator('[data-spec="mw-redis-data-failed"]')).toContainText('Redis stub 500');
  await expect(page.locator('[data-spec="mw-redis-data-retry"]')).toBeVisible();

  await openStory(page, 'MiddlewareTab/Loading');
  await expect(page.locator('[data-spec="mw-redis-data-loading"]')).toContainText('Redis 数据加载中…');
  await expect(page.locator('[data-spec="mw-redis-data-filter"]')).toHaveCount(0);
});

test('Redis 候选失败降级：提示独立可见，数据行照常（无候选时节点列回退 #id）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/NodeFailed');
  await expect(page.locator('[data-spec="middleware-redis-node-error"]')).toContainText('Redis 节点候选加载失败');
  await expect(page.locator('[data-spec="middleware-redis-node-error"]')).toContainText('Redis 候选服务离线');
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(3);
  await expect(page.locator('[data-spec="mw-redis-data-row"]').first()).toContainText('#7');
});

// ==================== MW-04 · Redis 消息队列（CARD-MW-04） ====================

test('Redis Queue：15 列表头与行内容（CARD-MW-04 BL-7 全列；审计列不做假入口）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const ths = page.locator('[data-spec="mw-redis-queue"] thead th');
  await expect(ths).toHaveCount(15);
  await expect(ths.nth(0)).toContainText('Redis 节点');
  await expect(ths.nth(1)).toContainText('名称');
  await expect(ths.nth(2)).toContainText('分类');
  await expect(ths.nth(3)).toContainText('Db');
  await expect(ths.nth(4)).toContainText('类型');
  await expect(ths.nth(5)).toContainText('消费组');
  await expect(ths.nth(6)).toContainText('消费者');
  await expect(ths.nth(7)).toContainText('总消费');
  await expect(ths.nth(8)).toContainText('消息数');
  await expect(ths.nth(9)).toContainText('最大积压');
  await expect(ths.nth(10)).toContainText('启用');
  await expect(ths.nth(11)).toContainText('最早消费');
  await expect(ths.nth(12)).toContainText('最后活跃');
  await expect(ths.nth(13)).toContainText('追踪');
  await expect(ths.nth(14)).toContainText('更新时间');
  await expect(page.locator('[data-spec="mw-redis-queue-row"]')).toHaveCount(3);
  const first = page.locator('[data-spec="mw-redis-queue-row"]').first();
  await expect(first).toContainText('主缓存');
  await expect(first).toContainText('order-queue');
  await expect(first).toContainText('订单');
  await expect(first).toContainText('PubSub');
  await expect(first).toContainText('order-group');
  await expect(first).toContainText('123456');
  await expect(first).toContainText('320');
  await expect(first).toContainText('5000');
  // 零值兜底行（行2 无 TraceId/无消费组 → '—'、停用）
  const second = page.locator('[data-spec="mw-redis-queue-row"]').nth(1);
  await expect(second).toContainText('—');
  await expect(second).toContainText('否');
  // 裁定 3：审计日志【缺·待接入】——不渲染任何审计按钮（不做假入口）
  await expect(page.locator('[data-spec="mw-redis-queue-audit"]')).toHaveCount(0);
  // 只读浏览（BL-10 后端 CRUD 就绪但前端不暴露入口）：无新增/编辑/删除
  await expect(page.locator('[data-spec="mw-redis-queue-create"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-redis-queue-edit"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-redis-queue-remove"]')).toHaveCount(0);
});

test('Redis Queue 追踪列：有 TraceId=可用；无 TraceId=禁用+原因在视线内（定稿裁定 1）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const enabled = page.locator('[data-spec="mw-redis-queue-trace"]');
  await expect(enabled).toHaveCount(1);
  await expect(enabled).toBeEnabled();
  await expect(enabled).toHaveAttribute('title', '查看链路详情');
  const disabled = page.locator('[data-spec="mw-redis-queue-trace-disabled"]');
  await expect(disabled).toHaveCount(2);
  await expect(disabled.first()).toBeDisabled();
  await expect(disabled.first()).toHaveAttribute('title', '无 TraceId：该队列尚未发起追踪');
  // 铁律：无 TraceId 按钮 disabled——不可点、不发路由（禁路径跳转防白屏）
  await expect(disabled.first()).toHaveAttribute('disabled', '');
});

test('Redis Queue 筛选条与分页：四控件/分类聚合下拉/更新时间注记；total=46 → 3 页', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await expect(page.locator('[data-spec="mw-redis-queue-filter"] [data-spec="mw-redis-node-lov"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-redis-queue-filter"] [data-spec="mw-redis-node-lov"] option')).toHaveCount(2);
  // 分类下拉=前端从当前 3 行聚合去重（订单/告警/日志）+「全部分类」=4
  const cat = page.locator('[data-spec="mw-redis-queue-filter-category"]');
  await expect(cat.locator('option')).toHaveCount(4);
  await expect(cat.locator('option').nth(1)).toHaveText('订单');
  await expect(page.locator('[data-spec="mw-redis-queue-filter-time-note"]')).toContainText('按更新时间过滤');
  await expect(page.locator('[data-spec="mw-redis-queue-filter-q"]')).toBeVisible();
  // 标准服务端分页：total=46/pageSize=20 → 3 页（pageIndex+pageSize 恒发）
  await expect(page.locator('[data-spec="mw-redis-queue-page"]')).toContainText('第 1 / 3 页');
  const prev = page.locator('[data-spec="mw-redis-queue-prev"]');
  const next = page.locator('[data-spec="mw-redis-queue-next"]');
  await expect(prev).toBeDisabled();
  await next.click();
  await expect(page.locator('[data-spec="mw-redis-queue-page"]')).toContainText('第 2 / 3 页');
  await expect(prev).toBeEnabled();
  // 筛选 @change 重查不炸（桩恒同数据）
  await cat.selectOption('订单');
  await expect(page.locator('[data-spec="mw-redis-queue-row"]')).toHaveCount(3);
});

test('Redis Lov 枢纽 spec 统一登记（MW-05 裁定 2）：两消费卡同挂 mw-redis-node-lov，旧 per-卡 spec 清零', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  // RedisNodeCard 自身不复刻下拉（走查 §7 差异注记）——全 tab 该 spec 恰两处=数据卡+队列卡
  const lovs = page.locator('[data-spec="mw-redis-node-lov"]');
  await expect(lovs).toHaveCount(2);
  // 同源 fetchRedisNodes（NodeSearch 服务端固定 enable=true）：候选一致（全部节点+1 节点）
  await expect(lovs.first().locator('option')).toHaveCount(2);
  await expect(lovs.nth(1).locator('option')).toHaveCount(2);
  // 防回退双锚：统一登记后旧 per-卡 spec 名不再存在
  await expect(page.locator('[data-spec="mw-redis-data-filter-node"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-redis-queue-filter-node"]')).toHaveCount(0);
});

test('Redis Queue Empty/Failed/Loading：三态文案（B1 一等公民 + §11 反馈）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/Empty');
  await expect(page.locator('[data-spec="mw-redis-queue-empty"]')).toContainText('暂无 Redis 队列数据');
  await expect(page.locator('[data-spec="mw-redis-queue-row"]')).toHaveCount(0);

  await openStory(page, 'MiddlewareTab/Failed');
  await expect(page.locator('[data-spec="mw-redis-queue-failed"]')).toContainText('Redis 队列加载失败');
  await expect(page.locator('[data-spec="mw-redis-queue-failed"]')).toContainText('Redis 队列 stub 500');
  await page.locator('[data-spec="mw-redis-queue-retry"]').click();
  await expect(page.locator('[data-spec="mw-redis-queue-failed"]')).toBeVisible();

  await openStory(page, 'MiddlewareTab/Loading');
  await expect(page.locator('[data-spec="mw-redis-queue-loading"]')).toContainText('Redis 队列加载中…');
  await expect(page.locator('[data-spec="mw-redis-queue-filter"]')).toHaveCount(0);
});

test('Redis Queue 空态隔离帧（定稿裁定 4 · C05 空态三态变体）：仅队列空，其余区块照常', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/RedisQueueEmpty');
  await expect(page.locator('[data-spec="mw-redis-queue-empty"]')).toContainText('暂无 Redis 队列数据');
  await expect(page.locator('[data-spec="mw-redis-queue-empty"]')).toContainText('Redis 节点接入队列消费后，队列状态会在这里展示');
  await expect(page.locator('[data-spec="mw-redis-queue-row"]')).toHaveCount(0);
  await expect(page.locator('[data-spec="mw-redis-queue-pager"]')).toHaveCount(0);
  // 隔离增量：其余四块照常（空态只落在队列区块，不拖垮整页）
  await expect(page.locator('[data-spec="mw-mysql-data-row"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(3);
  await expect(page.locator('[data-spec="mw-redis-node-row"]')).toHaveCount(3);
});

test('Redis Queue 追踪列禁用态隔离帧（定稿裁定 4）：全行无 TraceId=整列禁用+原因在视线内（§11）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/RedisQueueTraceDisabled');
  await expect(page.locator('[data-spec="mw-redis-queue-row"]')).toHaveCount(3);
  // 整列禁用本体：可用按钮 0、禁用按钮 3、原因 title 逐字
  await expect(page.locator('[data-spec="mw-redis-queue-trace"]')).toHaveCount(0);
  const disabled = page.locator('[data-spec="mw-redis-queue-trace-disabled"]');
  await expect(disabled).toHaveCount(3);
  await expect(disabled.first()).toBeDisabled();
  await expect(disabled.nth(1)).toBeDisabled();
  await expect(disabled.nth(2)).toBeDisabled();
  for (let i = 0; i < 3; i++) {
    await expect(disabled.nth(i)).toHaveAttribute('title', '无 TraceId：该队列尚未发起追踪');
  }
});

// ==================== MW-05 · Redis 节点（CARD-MW-05） ====================

test('Redis Node：12 列表头与行内容（CARD-MW-05；Password 任何路径不渲染）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await expect(page.locator('[data-spec="mw-redis-node"]')).toContainText('Redis 节点');
  await expect(page.locator('[data-spec="mw-redis-node"]')).toContainText('共 3 条');
  const ths = page.locator('[data-spec="mw-redis-node"] thead th');
  await expect(ths).toHaveCount(12);
  await expect(ths.nth(0)).toContainText('编号');
  await expect(ths.nth(2)).toContainText('名称');
  await expect(ths.nth(4)).toContainText('地址');
  await expect(ths.nth(9)).toContainText('启用');
  await expect(ths.nth(11)).toContainText('操作');
  await expect(page.locator('[data-spec="mw-redis-node-row"]')).toHaveCount(3);
  const first = page.locator('[data-spec="mw-redis-node-row"]').first();
  await expect(first).toContainText('主缓存');
  await expect(first).toContainText('银河项目');
  await expect(first).toContainText('10.0.0.9:6379');
  await expect(first).toContainText('master');
  // 启用列=徽章文字（启用/停用），非 是/否
  await expect(first).toContainText('启用');
  await expect(page.locator('[data-spec="mw-redis-node-row"]').nth(1)).toContainText('停用');
  // 裁定④：卡头新增按钮 + 行内 4 操作按钮
  await expect(page.locator('[data-spec="mw-redis-node-create"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-redis-node-edit"]')).toHaveCount(3);
  // 密码列不存在（Password 不映射不渲染）；服务器地址列无端口号拆列
  await expect(page.locator('[data-spec="mw-redis-node-password"]')).toHaveCount(0);
});

test('Redis Node 筛选条：分类聚合下拉/地址精确注记/启用三态/Q/时间注记（C01+C02）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const cat = page.locator('[data-spec="mw-redis-node-filter-category"]');
  await expect(cat.locator('option')).toHaveCount(3);
  await expect(cat.locator('option').nth(1)).toHaveText('Cache');
  await expect(cat.locator('option').nth(2)).toHaveText('Queue');
  await expect(page.locator('[data-spec="mw-redis-node-filter-server"]')).toBeVisible();
  await expect(page.locator('[data-spec="mw-redis-node-filter-server-note"]')).toContainText('精确匹配');
  const enable = page.locator('[data-spec="mw-redis-node-filter-enable"]');
  await expect(enable.locator('option')).toHaveCount(3);
  await expect(enable.locator('option').nth(1)).toHaveText('启用');
  await expect(enable.locator('option').nth(2)).toHaveText('停用');
  await expect(page.locator('[data-spec="mw-redis-node-filter-time-note"]')).toContainText('按更新时间过滤');
  await expect(page.locator('[data-spec="mw-redis-node-filter-q"]')).toBeVisible();
  // 筛选 @change 重查不炸（桩恒同数据）
  await enable.selectOption('true');
  await expect(page.locator('[data-spec="mw-redis-node-row"]')).toHaveCount(3);
});

test('Redis Node 编辑抽屉：回填+密码掩码+PUT 保存（C05，裁定④）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await page.locator('[data-spec="mw-redis-node-edit"]').first().click();
  const drawer = page.locator('[data-spec="mw-redis-node-drawer"]');
  await expect(drawer).toContainText('编辑节点');
  await expect(page.locator('[data-spec="mw-redis-node-form-name"]')).toHaveValue('主缓存');
  // Password 回显但掩码（type=password）
  const pwd = page.locator('[data-spec="mw-redis-node-form-password"]');
  await expect(pwd).toHaveValue('seed-redis');
  await expect(pwd).toHaveAttribute('type', 'password');
  await expect(page.locator('[data-spec="mw-redis-node-form-server"]')).toHaveValue('10.0.0.9:6379');
  await page.locator('[data-spec="mw-redis-node-save"]').click();
  await expect(page.locator('.sd-toast')).toContainText('保存成功！');
  // ElDrawer 关闭态 overlay 根仍挂 DOM（SKILL 坑：toHaveCount(0) 恒失败）→ toBeHidden
  await expect(drawer).toBeHidden();
  const calls = await page.evaluate(() => (window as unknown as { __mwRedisNodeCalls?: string[] }).__mwRedisNodeCalls ?? []);
  expect(calls).toContain('PUT /Redis/RedisNode/7');
});

test('Redis Node 新增抽屉：无删除按钮+空名校验+POST 保存（裁定④）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await page.locator('[data-spec="mw-redis-node-create"]').click();
  const drawer = page.locator('[data-spec="mw-redis-node-drawer"]');
  await expect(drawer).toContainText('新增节点');
  // 新增模式无删除（无 Id 可删）
  await expect(page.locator('[data-spec="mw-redis-node-delete"]')).toHaveCount(0);
  // 空名提交 → 内联校验（role=alert）
  await page.locator('[data-spec="mw-redis-node-save"]').click();
  await expect(page.locator('[data-spec="mw-redis-node-form-name-error"]')).toContainText('名称不能为空');
  // 填名保存 → POST + toast
  await page.locator('[data-spec="mw-redis-node-form-name"]').fill('新节点');
  await page.locator('[data-spec="mw-redis-node-form-server"]').fill('127.0.0.1:6380');
  await page.locator('[data-spec="mw-redis-node-save"]').click();
  await expect(page.locator('.sd-toast')).toContainText('新增成功！');
  await expect(drawer).toBeHidden();
  const calls = await page.evaluate(() => (window as unknown as { __mwRedisNodeCalls?: string[] }).__mwRedisNodeCalls ?? []);
  expect(calls).toContain('POST /Redis/RedisNode');
});

test('Redis Node 删除：编辑抽屉内入口 → GuardModal 二次确认 → DELETE（裁定④，§11 点名）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  await page.locator('[data-spec="mw-redis-node-edit"]').first().click();
  await expect(page.locator('[data-spec="mw-redis-node-form-name"]')).toHaveValue('主缓存');
  await page.locator('[data-spec="mw-redis-node-delete"]').click();
  const modal = page.locator('[data-spec="guard-modal"]');
  await expect(modal).toContainText('删除 Redis 节点');
  await expect(page.locator('[data-spec="guard-detail"]')).toContainText('主缓存');
  // 取消 → 不发 DELETE
  await page.locator('[data-spec="guard-cancel"]').click();
  await expect(modal).toHaveCount(0);
  let calls = await page.evaluate(() => (window as unknown as { __mwRedisNodeCalls?: string[] }).__mwRedisNodeCalls ?? []);
  expect(calls).not.toContain('DELETE /Redis/RedisNode/7');
  // 确认 → DELETE + toast + 抽屉关闭
  await page.locator('[data-spec="mw-redis-node-delete"]').click();
  await page.locator('[data-spec="guard-confirm"]').click();
  await expect(page.locator('.sd-toast')).toContainText('删除成功！');
  await expect(page.locator('[data-spec="mw-redis-node-drawer"]')).toBeHidden();
  calls = await page.evaluate(() => (window as unknown as { __mwRedisNodeCalls?: string[] }).__mwRedisNodeCalls ?? []);
  expect(calls).toContain('DELETE /Redis/RedisNode/7');
});

test('Redis Node 双下钻：监控/队列 → 上方两卡切到该节点（裁定③，组件状态不跳路由）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  // 监控下钻：Redis 数据卡切 redisId=7 → 45 行本地切片（3 页）+ 曲线 canvas 出现
  await page.locator('[data-spec="mw-redis-node-monitor"]').first().click();
  await expect(page.locator('.sd-toast')).toContainText('已切到节点「主缓存」的监控数据');
  await expect(page.locator('[data-spec="mw-redis-data-chart-canvas"] canvas')).toBeVisible();
  await expect(page.locator('[data-spec="mw-redis-data-page"]')).toContainText('第 1 / 3 页');
  // 队列下钻：Redis 队列卡切 redisId=7（桩恒 3 行不炸，toast 点名节点）；监控/队列双 toast 并存 → 断最后一条
  await page.locator('[data-spec="mw-redis-node-queue"]').first().click();
  await expect(page.locator('.sd-toast').last()).toContainText('已切到节点「主缓存」的消息队列');
});

test('Redis Node 刷新：GET Refresh + toast 透传 + 保底 300ms（C04，§3.5）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/WithData');
  const btn = page.locator('[data-spec="mw-redis-node-refresh"]').first();
  await btn.click();
  await expect(page.locator('.sd-toast')).toContainText('刷新[主缓存]成功！');
  await expect(btn).toBeEnabled();
  await expect(btn).not.toContainText('刷新中…');
  const calls = await page.evaluate(() => (window as unknown as { __mwRedisNodeCalls?: string[] }).__mwRedisNodeCalls ?? []);
  expect(calls).toContain('GET /Redis/RedisNode/Refresh');
});

test('Redis Node 独立降级：节点卡失败其余照常（B1 一等公民）', async ({ page }) => {
  await openStory(page, 'MiddlewareTab/RedisNodeListFailed');
  await expect(page.locator('[data-spec="mw-redis-node-failed"]')).toContainText('Redis 节点加载失败');
  await expect(page.locator('[data-spec="mw-redis-node-failed"]')).toContainText('Redis 节点查询失败');
  await page.locator('[data-spec="mw-redis-node-retry"]').click();
  await expect(page.locator('[data-spec="mw-redis-node-failed"]')).toBeVisible();
  // 其余四块照常（独立区块互不拖垮）
  await expect(page.locator('[data-spec="mw-redis-data-row"]')).toHaveCount(3);
  await expect(page.locator('[data-spec="mw-redis-queue-row"]')).toHaveCount(3);
});
