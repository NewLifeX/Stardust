import AppLog from './AppLog.vue';
import type { LogRow } from './appLog.logic.ts';

// 固定骨架：今日四条日志，覆盖 Info / Warn / Error / Debug 四个级别
const LOGS: LogRow[] = [
  { id: 1, appId: 32, clientId: 'ip@1', time: '2026-10-29 10:00:01.100', level: 'info', name: 'Startup', message: '应用启动完成，监听 8080' },
  { id: 2, appId: 32, clientId: 'ip@1', time: '2026-10-29 10:00:02.200', level: 'warn', name: 'Cache', message: '缓存命中率低于 60%' },
  { id: 3, appId: 32, clientId: 'ip@2', time: '2026-10-29 10:00:03.300', level: 'error', name: 'Order', message: '下单超时：db.query timeout' },
  { id: 4, appId: 32, clientId: 'ip@2', time: '2026-10-29 10:00:04.400', level: 'debug', name: 'Rpc', message: 'rpc call detail: payload size 256' },
];

export const stories = [
  // 测试状态：五条级别 chip、四行日志（Info/Warn/Error/Debug 颜色依次不同）、右端暂停实时按钮
  { id: 'AppLog/WithLogs', component: AppLog, props: { appId: 32, deps: { fetchLogs: async () => LOGS, intervalMs: 0 } } },

  // 测试状态：该应用今天没有日志
  { id: 'AppLog/Empty', component: AppLog, props: { appId: 32, deps: { fetchLogs: async () => [], intervalMs: 0 } } },
];
