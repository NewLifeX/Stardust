#!/usr/bin/env node
/**
 * 确保本地后端（StarWebApi，默认 http://localhost:6681）处于运行状态。
 *
 * 行为：
 *   - 后端已在运行 → 直接退出 0（配合 Playwright webServer.reuseExistingServer 使用）
 *   - 未运行        → 从 Bin/WebApi/StarWebApi.exe 以分离进程拉起，并轮询直到就绪
 *
 * 环境变量：
 *   BACKEND_URL  探活地址，默认 http://localhost:6681
 *   BACKEND_EXE  后端可执行文件路径，默认 <repo>/Bin/WebApi/StarWebApi.exe
 */
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..');
const EXE = process.env.BACKEND_EXE || resolve(REPO, 'Bin', 'WebApi', 'StarWebApi.exe');
const BASE = process.env.BACKEND_URL || 'http://localhost:6681';
const READY_PATH = '/api/Registry/App';

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

async function alive() {
  try {
    const r = await fetch(BASE + READY_PATH, { method: 'GET', signal: AbortSignal.timeout(4000) });
    return r.status > 0;
  } catch {
    return false;
  }
}

if (await alive()) {
  console.log(`[ensure-backend] 后端已在运行：${BASE}`);
  process.exit(0);
}

if (!existsSync(EXE)) {
  console.error(`[ensure-backend] 未找到后端可执行文件：${EXE}`);
  process.exit(1);
}

console.log(`[ensure-backend] 正在启动后端：${EXE}`);
const child = spawn(EXE, [], { cwd: dirname(EXE), detached: true, stdio: 'ignore', windowsHide: true });
child.unref();

const deadline = Date.now() + 120000;
while (Date.now() < deadline) {
  await sleep(1500);
  if (await alive()) {
    console.log(`[ensure-backend] 后端就绪：${BASE}`);
    process.exit(0);
  }
}
console.error(`[ensure-backend] 120s 内后端未就绪：${BASE}`);
process.exit(1);
