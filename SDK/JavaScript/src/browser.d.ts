/**
 * Stardust Browser SDK 子路径类型声明。
 * 对应 package.json 的 "./browser" 导出。
 */

export type {
    BrowserTracerOptions,
    SpanOptions,
    TraceparentContext,
    TokenProvider,
    TokenProviderContext,
    TokenResult,
    SpanJson,
} from './index';

export {
    Span,
    StardustBrowserTracer,
} from './index';

import {
    BrowserTracerOptions,
    TraceparentContext,
    StardustBrowserTracer,
} from './index';

/** 创建浏览器追踪器。 */
export declare function createTracer(options: BrowserTracerOptions): StardustBrowserTracer;
export declare function createTracer(server: string, appId: string, secret?: string): StardustBrowserTracer;

/** 创建并启动浏览器追踪器（一行接入）。 */
export declare function init(options: BrowserTracerOptions): StardustBrowserTracer;
export declare function init(server: string, appId: string, secret?: string): StardustBrowserTracer;

/** 手动上报异常。 */
export declare function captureException(err: Error | string, options?: { tag?: string }): import('./index').Span | null;

/** 立即上报当前聚合数据。 */
export declare function flush(): Promise<void>;

/** 安装全部浏览器自动埋点。 */
export declare function installBrowserInterceptors(tracer: StardustBrowserTracer): void;

/** 仅安装 fetch 埋点。 */
export declare function installFetchInterceptor(tracer: StardustBrowserTracer): void;

/** 仅安装 XMLHttpRequest 埋点。 */
export declare function installXHRInterceptor(tracer: StardustBrowserTracer): void;

/** 解析 W3C traceparent 头，非法时返回 null。 */
export declare function parseTraceparent(value: string | string[] | null | undefined): TraceparentContext | null;

/** 格式化 W3C traceparent 头。 */
export declare function formatTraceparent(traceId: string, spanId: string, sampled?: boolean): string;

/** 生成 32 位小写十六进制 TraceId。 */
export declare function generateTraceId(): string;

/** 生成 16 位小写十六进制 SpanId。 */
export declare function generateSpanId(): string;

/** SDK 版本。 */
export declare const version: string;

export default StardustBrowserTracer;
