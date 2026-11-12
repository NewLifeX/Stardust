/**
 * 手动刷新按钮的状态机（AGENTS.md §11）：
 *  - 进行中 refreshing 为 true，按钮据此旋转、禁用；
 *  - 重复点击被忽略；
 *  - 请求再快也至少转 REFRESH_MIN_SPIN_MS；
 *  - 失败（含 load 抛错）必须通过 notify 给出可见提示，成功只停止旋转。
 * 时钟与 sleep 可注入，便于单测。
 */
import { ref } from 'vue';
import { refreshFeedback, refreshRemainMs } from './ship.logic.ts';
import type { RefreshOutcome } from './ship.logic.ts';

export interface ManualRefreshOptions {
  /** 实际取数；失败请写进 outcome，不要吞掉也不必抛出，抛出也会被兜住 */
  load: () => Promise<void>;
  /** load 结束后读取各区块的结果 */
  outcome: () => RefreshOutcome;
  notify: (ok: boolean, text: string) => void;
  now?: () => number;
  sleep?: (ms: number) => Promise<void>;
  /** 看门狗时限：load 挂起（后端无响应窗）超过该毫秒数就强复位+提示，防旋转永挂（CARD-MON-08） */
  watchdogMs?: number;
  /** 看门狗计时器调度注入（单测用）；返回取消函数。默认真实 setTimeout */
  watchdogSchedule?: (fire: () => void, ms: number) => () => void;
}

export function useManualRefresh(opt: ManualRefreshOptions) {
  const now = opt.now ?? (() => Date.now());
  const sleep = opt.sleep ?? ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));
  const watchdogMs = opt.watchdogMs ?? 30_000;
  const scheduleWatchdog =
    opt.watchdogSchedule ??
    ((fire: () => void, ms: number) => {
      const id = setTimeout(fire, ms);
      return () => clearTimeout(id);
    });
  const refreshing = ref(false);

  async function refresh(): Promise<void> {
    if (refreshing.value) return;
    refreshing.value = true;
    const started = now();
    let thrown = '';
    let watchdogFired = false;
    // 看门狗与 load 并发赛跑：load 先回则到点时按钮已复位、静默退出；
    // 看门狗先到则强复位+超时提示，迟回的 load 回包被静默丢弃（CARD-MON-08 防旋转永挂）。
    const cancelWatchdog = scheduleWatchdog(() => {
      if (!refreshing.value) return;
      watchdogFired = true;
      refreshing.value = false;
      opt.notify(false, `刷新超时（>${watchdogMs}ms），请稍后重试`);
    }, watchdogMs);
    try {
      await opt.load();
    } catch (e) {
      thrown = e instanceof Error ? e.message : String(e);
    }
    cancelWatchdog();
    if (watchdogFired) return; // 看门狗已收口，本轮迟回结果静默丢弃，不重复提示
    const rest = refreshRemainMs(now() - started);
    if (rest > 0) await sleep(rest);
    refreshing.value = false;
    if (thrown) {
      opt.notify(false, `刷新失败：${thrown}`);
      return;
    }
    const fb = refreshFeedback(opt.outcome());
    if (fb) opt.notify(fb.ok, fb.text);
  }

  return { refreshing, refresh };
}
