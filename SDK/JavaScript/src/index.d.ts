/**
 * Stardust JavaScript SDK 类型声明。
 */

export interface TokenProviderContext {
    appId: string;
    appName: string;
    server: string;
    version: string;
    clientId: string;
}

export interface TokenResult {
    token: string;
    /** 过期时间：相对秒数、绝对秒数或绝对毫秒均可。 */
    expire?: number;
}

export type TokenProvider = (
    context: TokenProviderContext
) => string | TokenResult | Promise<string | TokenResult>;

export interface TracerOptions {
    /** 星尘服务器地址，例如 http://star.example.com:6600 */
    server: string;
    /** 应用ID */
    appId: string;
    /** 应用显示名，默认同 appId */
    appName?: string;
    /** 应用密钥；内部网络可选，公网部署建议改用 tokenProvider */
    secret?: string;
    /** 应用版本，默认 1.1.0 */
    version?: string;
    /** 客户端标识，默认 localIP@pid */
    clientId?: string;
    /** 自定义令牌提供者 */
    tokenProvider?: TokenProvider;
    /** 上报周期（秒），默认 60 */
    period?: number;
    /** 心跳周期（秒），默认 30 */
    heartbeatPeriod?: number;
    /** 每个埋点名称的最大正常样本数，默认 1 */
    maxSamples?: number;
    /** 每个埋点名称的最大错误样本数，默认 10 */
    maxErrors?: number;
    /** 标签最大长度，默认 1024 */
    maxTagLength?: number;
    /** 请求标签最大长度，默认 1024 */
    requestTagLength?: number;
    /** 服务端判定的超时阈值（毫秒），默认 5000 */
    timeout?: number;
    /** HTTP 请求超时（毫秒），默认 10000 */
    httpTimeout?: number;
    /** 是否采集并上报应用性能信息，默认 true */
    enableMeter?: boolean;
    /** 进程退出时自动刷新，默认 true */
    autoFlushOnExit?: boolean;
    /** 是否自动安装 Node.js 埋点（HTTP/数据库/Redis），默认 true */
    autoInstrument?: boolean;
    /** 输出调试日志，默认 false */
    debug?: boolean;
    /** 排除的埋点名称（子串匹配） */
    excludes?: string[];
}

export interface BrowserTracerOptions {
    /** 星尘服务器地址 */
    server: string;
    /** 应用ID */
    appId: string;
    /** 应用显示名，默认同 appId */
    appName?: string;
    /** 应用密钥；公网部署建议改用 tokenProvider，避免前端泄露 */
    secret?: string;
    /** 应用版本，默认 1.1.0 */
    version?: string;
    /** 客户端标识，默认持久化的 browser@<32位十六进制> */
    clientId?: string;
    /** 自定义令牌提供者 */
    tokenProvider?: TokenProvider;
    /** 上报周期（秒），默认 60 */
    period?: number;
    /** 心跳周期（秒），默认 30 */
    heartbeatPeriod?: number;
    /** 每个埋点名称的最大正常样本数，默认 1 */
    maxSamples?: number;
    /** 每个埋点名称的最大错误样本数，默认 10 */
    maxErrors?: number;
    /** 标签最大长度，默认 1024 */
    maxTagLength?: number;
    /** 请求标签最大长度，默认 1024 */
    requestTagLength?: number;
    /** 服务端判定的超时阈值（毫秒），默认 5000 */
    timeout?: number;
    /** 是否采集并上报性能信息，默认 true */
    enableMeter?: boolean;
    /** 是否自动安装浏览器埋点，默认 true */
    autoInstrument?: boolean;
    /** 后端注入的 W3C traceparent，用于把 page.load 关联到服务端链路 */
    initialTraceparent?: string;
    /** traceparent 传播目标，默认仅同源；支持 '*'、origin/主机名或正则 */
    tracePropagationTargets?: string | RegExp | Array<string | RegExp>;
    /** 输出调试日志，默认 false */
    debug?: boolean;
    /** 排除的埋点名称（子串匹配） */
    excludes?: string[];
}

/** 创建 Span 的上下文选项。 */
export interface SpanOptions {
    /** 父 Span ID */
    parentId?: string;
    /** 显式指定 TraceId，缺省继承父 Span 或自动生成 */
    traceId?: string;
}

/** 解析后的 W3C traceparent。 */
export interface TraceparentContext {
    version: string;
    traceId: string;
    spanId: string;
    flags: number;
    sampled: boolean;
}

/** 单个追踪跨度。 */
export class Span {
    constructor(name: string, tracer: StardustTracer | StardustBrowserTracer, parentId?: string | SpanOptions);

    id: string;
    parentId: string;
    traceId: string;
    name: string;
    startTime: number;
    endTime: number;
    tag: string;
    error: string;

    setTag(tag: string): this;
    setError(err: Error | string): this;
    finish(): this;
    toJSON(): SpanJson;
}

export interface SpanJson {
    Id: string;
    ParentId: string;
    TraceId: string;
    StartTime: number;
    EndTime: number;
    Tag: string;
    Error: string;
}

export interface SpanBuilderJson {
    Name: string;
    StartTime: number;
    EndTime: number;
    Total: number;
    Errors: number;
    Cost: number;
    MaxCost: number;
    MinCost: number;
    Samples: SpanJson[];
    ErrorSamples: SpanJson[];
}

/** 同名 Span 统计构建器。 */
export class SpanBuilder {
    constructor(name: string, maxSamples?: number, maxErrors?: number);

    name: string;
    startTime: number;
    endTime: number;
    total: number;
    errors: number;
    cost: number;
    maxCost: number;
    minCost: number;
    samples: Span[];
    errorSamples: Span[];

    addSpan(span: Span): this;
    toJSON(): SpanBuilderJson;
}

/** Node.js 星尘追踪器。 */
export class StardustTracer {
    constructor(server: string, appId: string, secret?: string);
    constructor(options: TracerOptions);

    server: string;
    appId: string;
    appName: string;
    secret: string;
    version: string;
    clientId: string;
    period: number;
    heartbeatPeriod: number;
    maxSamples: number;
    maxErrors: number;
    maxTagLength: number;
    requestTagLength: number;
    timeout: number;
    enableMeter: boolean;
    excludes: string[];
    debug: boolean;
    autoInstrument: boolean;

    start(): Promise<this>;
    stop(): Promise<void>;
    flush(): Promise<void>;
    newSpan(name: string, parentId?: string | SpanOptions): Span;
    withSpan<T>(name: string, fn: (span: Span) => T): T;
}

/** 浏览器星尘追踪器。 */
export class StardustBrowserTracer {
    constructor(server: string, appId: string, secret?: string);
    constructor(options: BrowserTracerOptions);

    server: string;
    appId: string;
    appName: string;
    secret: string;
    version: string;
    clientId: string;
    period: number;
    heartbeatPeriod: number;
    maxSamples: number;
    maxErrors: number;
    maxTagLength: number;
    requestTagLength: number;
    timeout: number;
    enableMeter: boolean;
    autoInstrument: boolean;
    tracePropagationTargets: string | RegExp | Array<string | RegExp> | null;
    excludes: string[];
    debug: boolean;

    start(): Promise<this>;
    stop(): Promise<void>;
    flush(): Promise<void>;
    newSpan(name: string, parentId?: string | SpanOptions): Span;
    withSpan<T>(name: string, fn: (span: Span) => T): T;
}

/** 创建 Node.js 追踪器。 */
export function createTracer(server: string, appId: string, secret?: string): StardustTracer;
export function createTracer(options: TracerOptions): StardustTracer;

/** 创建并启动 Node.js 追踪器（一行接入）。 */
export function init(server: string, appId: string, secret?: string): StardustTracer;
export function init(options: TracerOptions): StardustTracer;

/** 安装浏览器自动埋点。 */
export function installBrowserInterceptors(tracer: StardustBrowserTracer): void;

/** 仅安装 fetch 埋点。 */
export function installFetchInterceptor(tracer: StardustBrowserTracer): void;

/** 仅安装 XMLHttpRequest 埋点。 */
export function installXHRInterceptor(tracer: StardustBrowserTracer): void;

/** 手动安装 Node.js 自动埋点（HTTP/数据库/Redis）。autoInstrument 为 true 时无需调用。 */
export function installAutoInstrumentation(tracer: StardustTracer): StardustTracer;

/** 解析 W3C traceparent 头，非法时返回 null。 */
export function parseTraceparent(value: string | string[] | null | undefined): TraceparentContext | null;

/** 格式化 W3C traceparent 头（SDK 出站固定写 01）。 */
export function formatTraceparent(traceId: string, spanId: string, sampled?: boolean): string;

/** 生成 32 位小写十六进制 TraceId。 */
export function generateTraceId(): string;

/** 生成 16 位小写十六进制 SpanId。 */
export function generateSpanId(): string;

/** 获取当前活动 Span（Node.js AsyncLocalStorage 上下文）。 */
export function getCurrentSpan(): Span | null;

/** 在指定 Span 上下文中执行函数（Node.js）。 */
export function runWithSpan<T>(span: Span | null, fn: () => T): T;

export as namespace Stardust;
