/**
 * 把 vite 构建产物 dist/ 同步到 Stardust.WebApi/wwwroot（魔方皮肤包 NewLife.Cube.Vue 的 wwwroot 惯例）。
 * 由 `pnpm build:all` 在 vite build 之后调用；wwwroot 已 gitignore，属构建产物不入库。
 * 同步语义：先清空目标目录再整树复制，保证 API 工程嵌入的 wwwroot 与 dist 完全一致（无陈旧哈希资源残留）。
 */
import { cpSync, existsSync, mkdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDir, '..');
const sourceDir = join(projectRoot, 'dist');
const targetDir = resolve(projectRoot, '../Stardust.WebApi/wwwroot');

if (!existsSync(join(sourceDir, 'index.html'))) {
  console.error(`[sync-wwwroot] 未找到构建产物 ${sourceDir}\\index.html，请先执行 pnpm build`);
  process.exit(1);
}

rmSync(targetDir, { recursive: true, force: true });
mkdirSync(targetDir, { recursive: true });
cpSync(sourceDir, targetDir, { recursive: true });

console.log(`[sync-wwwroot] 已同步 dist -> ${targetDir}`);
