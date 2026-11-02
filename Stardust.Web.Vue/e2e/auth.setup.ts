import { test as setup, expect } from '@playwright/test';
import { STORAGE_STATE, BACKEND_URL, BACKEND_USER, BACKEND_PASS } from './config';

/**
 * 登录态准备（**直连后端** API 登录 + 同源注入）：
 *
 * 2026-10-14：前端已改为直连后端（Vite 代理移除），因此登录请求直接打到 BACKEND_URL，
 * 而不再是 `${前端 origin}/Auth/Login`（那条路径现在只会落到 Vite 并 404）。
 *
 * 1. 复用已有有效登录态（localStorage 有 token 且后端 /api/Cube/Info 非 401）；
 * 2. 否则在 browser context 内用 page.request 调 `${BACKEND_URL}/Auth/Login`，
 *    —— page.request 与 page 共享 Cookie 容器，登录 Set-Cookie（ManageProvider 会话）自动入 context；
 *    —— Cookie 不区分端口（RFC 6265），故 localhost:6681 种下的 Cookie 同样随请求发往 localhost:5188；
 *    —— 再把 access_token / refresh_token 写入 localStorage（键与 core/pages/LoginPage.vue 一致：
 *       setAccessToken -> localStorage['token']，setRefreshToken -> localStorage['refresh_token']）；
 * 3. storageState 一并保存 Cookie + localStorage，业务接口（LS token）与 Cube 服务接口
 *    （/Cube/MenuTree 等靠 Cookie 的 ManageProvider.User 识别）双通道都有登录态。
 *
 * 说明：UI 表单登录受后端 passwordStrength 配置影响（本地空库 5-32 / 远程 6-32 不一致），
 * API 登录不受该客户端校验拦截，且登录交互本身已由 LoginForm 单测覆盖，此处聚焦主流程。
 */
setup('登录态准备（API 登录 + 注入 token）', async ({ page }) => {
  // 先打开前端同源页，后续 evaluate / request 都在此 origin 下
  await page.goto('/login', { waitUntil: 'domcontentloaded' });

  // 已有 token 时验证有效性（业务接口 401 视为失效）
  const existing = await page.evaluate(() => localStorage.getItem('token'));
  if (existing) {
    const probe = await page.request.get(`${BACKEND_URL}/api/Cube/Info`);
    if (probe.status() !== 401) {
      await page.context().storageState({ path: STORAGE_STATE });
      return;
    }
    await page.evaluate(() => localStorage.removeItem('token'));
  }

  // 直连后端登录：Set-Cookie 自动进入 page.request 的 Cookie 容器
  const resp = await page.request.post(`${BACKEND_URL}/Auth/Login`, {
    data: { username: BACKEND_USER, password: BACKEND_PASS },
    headers: { 'Content-Type': 'application/json' },
    timeout: 60_000,
  });
  expect(resp.status(), 'API 登录应返回 200').toBe(200);
  const body = await resp.json();
  expect(body.code, 'API 登录应成功').toBe(0);
  const accessToken: string = body.data?.accessToken ?? body.data?.access_token ?? '';
  const refreshToken: string = body.data?.refreshToken ?? body.data?.refresh_token ?? '';
  expect(accessToken, '应返回 accessToken').toBeTruthy();

  // 与 LoginPage.handleLogin 相同的 localStorage 写入
  await page.evaluate(
    ([t, r]) => {
      localStorage.setItem('token', t as string);
      if (r) localStorage.setItem('refresh_token', r as string);
    },
    [accessToken, refreshToken],
  );

  // 预热（同 context，带 Cookie+Token）：菜单树接口返回 code=0 才算登录态就绪，
  // 否则用例首屏会被 handleUnauthorized 踢回未登录页
  await expect
    .poll(
      async () => {
        try {
          const probe = await page.request.get(`${BACKEND_URL}/Cube/MenuTree?PageSize=50`);
          return (await probe.json())?.code ?? -1;
        } catch {
          return -1;
        }
      },
      { message: 'MenuTree 预热应返回 code=0', timeout: 60_000, intervals: [500, 1_000, 2_000, 4_000] },
    )
    .toBe(0);

  await page.context().storageState({ path: STORAGE_STATE });
});
