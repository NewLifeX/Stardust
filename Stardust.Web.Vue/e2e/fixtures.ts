import { createRequire } from 'node:module';
import path from 'node:path';

// 本环境 pnpm 软链不稳定，本地 @playwright/test 时有时无；
// Playwright 用 ESM 加载配置与用例，只认本地 node_modules、不回退全局。
// 这里集中处理：优先本地，本地不可用则回退到已完整安装的全局 @playwright/test
// （与 CLI 同源，不会出现双实例）。所有配置/用例统一从此处导入。
const require = createRequire(import.meta.url);

function loadPlaywright() {
  try {
    return require('@playwright/test');
  } catch {
    const g = 'C:\\Users\\12504\\AppData\\Local\\nvm\\v24.16.0\\node_modules';
    return require(path.join(g, '@playwright/test'));
  }
}

const pw = loadPlaywright();

export const test = pw.test;
export const expect = pw.expect;
export const devices = pw.devices;
export const defineConfig = pw.defineConfig;
export const request = pw.request;
