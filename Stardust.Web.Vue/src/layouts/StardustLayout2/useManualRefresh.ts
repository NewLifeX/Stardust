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
}

export function useManualRefresh(opt: ManualRefreshOptions) {
  const now = opt.now ?? (() => Date.now());
  const sleep = opt.sleep ?? ((ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms)));
  const refreshing = ref(false);

  async function refresh(): Promise<void> {
    if (refreshing.value) return;
    refreshing.value = true;
    const started = now();
    let thrown = '';
    try {
      await opt.load();
    } catch (e) {
      thrown = e instanceof Error ? e.message : String(e);
    }
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
