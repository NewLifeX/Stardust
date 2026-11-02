// 底部控制台·终端（纯逻辑）。
// 真实 PTY 暂无接口（计划 P-06），这里只保留与后端无关的前端策略：高危命令拦截。
// 原型里 df / uptime / free 的演示输出不带进生产。

const DANGEROUS = /rm\s+-rf|mkfs|shutdown|reboot/;

/** 原型正则：/rm\s+-rf|mkfs|shutdown|reboot/ */
export function isDangerousCommand(cmd: string): boolean {
  return DANGEROUS.test(cmd);
}

/** 去首尾空白；空串返回 null，表示不执行 */
export function normalizeCommand(raw: string): string | null {
  const c = raw.trim();
  return c ? c : null;
}

/** 偏离原型文案：原型写“已写入审计日志”，但前端没有审计接口，如实说明 */
export const BLOCKED_MESSAGE = '✖ 高危命令已被安全策略拦截（审计写入待后端接入）';

/** 终端未接入真实通道时的提示 */
export const PENDING_MESSAGE = '终端通道待接入：当前仅提供高危命令拦截，命令不会被发送到节点';
