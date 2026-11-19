import { test, expect, type Page } from '@playwright/test';

const GALLERY = process.env.CT_BASE_URL || 'http://127.0.0.1:5190/';

async function openStory(page: Page, id: string) {
  await page.goto(`${GALLERY}?story=${encodeURIComponent(id)}`);
  await page.waitForSelector('.gal-preview');
  await page.waitForTimeout(300);
}

// CARD-PLT-01：PlatformTab「项目管理」区块（首次生成 CT 基线——基线需跑两遍确认稳定）
test('Data：项目管理区块 12 列，两行数据，title 逐字，环境行隐藏（后端无字段）', async ({ page }) => {
  await openStory(page, 'PlatformTab/Data');
  const block = page.locator('[data-spec="platform-projects"]');
  await expect(block).toBeVisible();
  await expect(block.locator('h3')).toContainText('项目管理');
  await expect(block.locator('h3')).toContainText('2 个项目');
  await expect(page.locator('[data-spec="platform-project-row"]')).toHaveCount(2);
  // 默认排序 Sort 降序：staging(Sort=9) 在 prod(Sort=2) 前
  await expect(page.locator('[data-spec="platform-project-row"]').first()).toContainText('staging');
  // 节点数/应用数列 title 逐字（盘点 §4 旁证）
  const staging = page.locator('[data-spec="platform-project-row"]').first();
  await expect(staging.locator('span[title="staging（节点）"]')).toContainText('2');
  await expect(staging.locator('span[title="staging（应用）"]')).toContainText('4');
  await expect(page.locator('[data-spec="platform-project-row"]').nth(1).locator('span[title="prod（节点）"]')).toContainText('5');
  // 环境行：后端 GalaxyProject 无 Environment 字段 → 值非空才显示（定稿裁定 1，story 数据未给 Environment）
  await expect(page.locator('[data-spec="platform-project-environment"]')).toHaveCount(0);
  // 操作列
  await expect(staging.locator('[data-spec="platform-project-edit-row"]')).toContainText('编辑');
  await expect(staging.locator('[data-spec="platform-project-remove"]')).toContainText('删除');
});

test('ProjectsEmpty：空库引导态（还没有项目 + 创建项目按钮）', async ({ page }) => {
  await openStory(page, 'PlatformTab/ProjectsEmpty');
  await expect(page.locator('[data-spec="platform-project-empty"]')).toContainText('还没有项目');
  await expect(page.locator('[data-spec="platform-project-empty-create"]')).toContainText('创建项目');
  await expect(page.locator('[data-spec="platform-project-row"]')).toHaveCount(0);
});

test('Empty（整页空）：项目管理区块仍渲染空态引导', async ({ page }) => {
  await openStory(page, 'PlatformTab/Empty');
  await expect(page.locator('[data-spec="platform-project-empty"]')).toContainText('还没有项目');
});

test('筛选：关键字命中过滤 + 重置恢复 + 筛选无命中空态文案', async ({ page }) => {
  await openStory(page, 'PlatformTab/Data');
  await page.fill('[data-spec="platform-project-filter-key"]', 'prod');
  await expect(page.locator('[data-spec="platform-project-row"]')).toHaveCount(1);
  await expect(page.locator('[data-spec="platform-project-filter-reset"]')).toBeEnabled();
  // 无命中文案（逐字：没有匹配「{关键字}」的项目）
  await page.fill('[data-spec="platform-project-filter-key"]', '不存在的项目');
  await expect(page.locator('[data-spec="platform-project-empty-filtered"]')).toContainText('没有匹配「不存在的项目」的项目');
  // 重置恢复全量
  await page.click('[data-spec="platform-project-filter-reset"]');
  await expect(page.locator('[data-spec="platform-project-row"]')).toHaveCount(2);
  await expect(page.locator('[data-spec="platform-project-filter-reset"]')).toBeDisabled();
});

test('新建抽屉交互：打开新建项目、名称必填内联、编辑回显', async ({ page }) => {
  await openStory(page, 'PlatformTab/Data');
  // 页内还有「添加项目成员」Drawer（关闭态 overlay 根仍在 DOM），必须用 .open 限定打开中的抽屉
  await page.click('[data-spec="platform-project-add"]');
  await expect(page.locator('[data-spec="drawer"].open')).toBeVisible();
  await expect(page.locator('[data-spec="drawer"].open h2')).toContainText('新建项目');
  // 空名提交 → 内联校验（旧壳逐字「请输入项目名称」），抽屉不关
  await page.click('[data-spec="platform-project-form-save"]');
  await expect(page.locator('[data-spec="platform-project-form-name-error"]')).toContainText('请输入项目名称');
  await expect(page.locator('[data-spec="drawer"].open')).toBeVisible();
  // 关闭新建抽屉再点编辑（modal 遮罩会挡住下层行点击）
  await page.click('[data-spec="platform-project-form-cancel"]');
  await expect(page.locator('[data-spec="drawer"].open')).toHaveCount(0);
  // 编辑回显：点行名称开编辑抽屉，标题「编辑项目」，名称回填
  await page.locator('[data-spec="platform-project-row"]').first().locator('[data-spec="platform-project-edit"]').click();
  await expect(page.locator('[data-spec="drawer"].open h2')).toContainText('编辑项目');
  await expect(page.locator('[data-spec="platform-project-form-name"]')).toHaveValue('staging');
});

// 区块三态由整页三态承载（fetchPlatform 单请求拉 tiles+projects+members+projectInfo，无独立数据源；
// CARD-PLT-01 草案 data-spec 的 platform-project-loading/failed/retry 以整页 platform-loading/failed/retry 落地）
test('Loading：整页三态承载，项目管理区块不渲染', async ({ page }) => {
  await openStory(page, 'PlatformTab/Loading');
  await expect(page.locator('[data-spec="platform-loading"]')).toContainText('平台设置加载中…');
  await expect(page.locator('[data-spec="platform-projects"]')).toHaveCount(0);
});

test('Failed：整页三态承载（失败文案+重试），项目管理区块不渲染', async ({ page }) => {
  await openStory(page, 'PlatformTab/Failed');
  await expect(page.locator('[data-spec="platform-failed"]')).toContainText('平台设置加载失败：Request failed with status 500');
  await expect(page.locator('[data-spec="platform-retry"]')).toBeVisible();
  await expect(page.locator('[data-spec="platform-projects"]')).toHaveCount(0);
});

test('删除确认：GuardModal 逐字文案 + 「删除中…」请求中态 + 成功反馈与事件上抛', async ({ page }) => {
  // 挂起 DELETE 响应，让「删除中…」可断言（gallery 组件 props 注入数据，不发列表请求）
  let release: (() => void) | undefined;
  const gate = new Promise<void>((r) => { release = r; });
  await page.route('**/Platform/GalaxyProject/**', async (route) => {
    if (route.request().method() === 'DELETE') {
      await gate;
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ code: 0, data: {}, message: 'ok' }) });
    } else {
      await route.fallback();
    }
  });
  await openStory(page, 'PlatformTab/Data');
  const first = page.locator('[data-spec="platform-project-row"]').first();
  await first.locator('[data-spec="platform-project-remove"]').click();
  await expect(page.locator('[data-spec="guard-modal"]')).toBeVisible();
  // 删除确认文案逐字（CARD-PLT-01 定稿数据契约）
  await expect(page.locator('[data-spec="guard-title"]')).toContainText('删除项目「staging」？删除后该项目将从顶栏选择器消失，其下节点与应用失去项目归属。此操作不可恢复。');
  await expect(page.locator('[data-spec="guard-confirm"]')).toContainText('确认执行');
  await page.locator('[data-spec="guard-confirm"]').click();
  // 请求中：仅被删行显示「删除中…」+ spinner + 禁用（§3.3/§11）
  await expect(first.locator('[data-spec="platform-project-remove"]')).toContainText('删除中…');
  await expect(first.locator('[data-spec="platform-project-remove"]')).toBeDisabled();
  release?.();
  // 成功反馈（§11）：toast「已删除项目 staging」可见
  await expect(page.locator('.sd-toast')).toContainText('已删除项目 staging');
  // 行消失由父组件重载承载（MeshPage 监听 projectChanged → loadPlatform）——story 无父层，
  // CT 断言到 projectChanged 上抛为止；真实页面的行消失在走查 C 码验证
  const changed = await page.evaluate(() => (window as unknown as { __platformProjectChanged?: number }).__platformProjectChanged);
  expect(changed).toBe(1);
});

// ===== 成员卡查询条（CARD-PLT-06：enable/dt/Q 服务端过滤，projectId 顶栏透传；userId 记差异不做控件） =====
// CT 宿主请求层桩（ct/mocks/request.ts）默认 resolve 空数据且不发网络——
// 查询即「服务端返回空列表」路径：计数 0 + 查询态空态文案 + 返回全部出现。
// 失败路径用 window.__ctRequestImpl 注入（注册-清理对称，防跨用例污染；同 McpTab PLT-04 先例）。
test('成员查询条：条件控件可见，查询生效空态不静默，失败注入可见提示', async ({ page }) => {
  await openStory(page, 'PlatformTab/WithMembers');
  await expect(page.locator('[data-spec="platform-member-query-enable"]')).toBeVisible();
  await expect(page.locator('[data-spec="platform-member-query-dt"]')).toBeVisible();
  await expect(page.locator('[data-spec="platform-member-query-q"]')).toBeVisible();
  await expect(page.locator('[data-spec="platform-member-query-apply"]')).toBeVisible();
  // 查询（桩返回空数据）：计数变 0 + 查询态空态文案（不静默）+ 返回全部出现
  await page.locator('[data-spec="platform-member-query-apply"]').click();
  await expect(page.getByRole('heading', { name: /项目成员 0 位/ })).toBeVisible();
  await expect(page.locator('[data-spec="platform-members-empty"]')).toContainText('当前查询条件下没有成员');
  await expect(page.locator('[data-spec="platform-member-query-reset"]')).toBeVisible();
  // 返回全部：清条件回落 story props 列表（2 行），reset 随查询态退场
  await page.locator('[data-spec="platform-member-query-reset"]').click();
  await expect(page.getByRole('heading', { name: /项目成员 2 位/ })).toBeVisible();
  await expect(page.locator('[data-spec="platform-member-query-reset"]')).toHaveCount(0);
  // 失败路径（override 注入）：注册→查询→错误文案可见→收尾拔除
  await page.evaluate(() => {
    (window as unknown as { __ctRequestImpl: unknown }).__ctRequestImpl = async () => {
      throw new Error('网络中断');
    };
  });
  try {
    await page.locator('[data-spec="platform-member-query-apply"]').click();
    await expect(page.locator('[data-spec="platform-member-query-error"]')).toContainText('成员查询失败：网络中断');
  } finally {
    await page.evaluate(() => {
      delete (window as unknown as { __ctRequestImpl?: unknown }).__ctRequestImpl;
    });
  }
});
