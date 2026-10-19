/**
 * Stardust Browser SDK 子路径类型声明。
 * 对应 package.json 的 "./browser" 导出。
 */

export type {
    BrowserTracerOptions,
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
    StardustBrowserTracer,
} from './index';

/** 创建浏览器追踪器。 */
export declare function createTracer(options: BrowserTracerOptions): StardustBrowserTracer;
export declare function createTracer(server: string, appId: string, secret?: string): StardustBrowserTracer;

/** 安装全部浏览器自动埋点。 */
export declare function installBrowserInterceptors(tracer: StardustBrowserTracer): void;

/** 仅安装 fetch 埋点。 */
export declare function installFetchInterceptor(tracer: StardustBrowserTracer): void;

/** 仅安装 XMLHttpRequest 埋点。 */
export declare function installXHRInterceptor(tracer: StardustBrowserTracer): void;

/** SDK 版本。 */
export declare const version: string;

export default StardustBrowserTracer;
