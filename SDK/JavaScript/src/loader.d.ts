/**
 * Stardust Browser Loader 类型声明。
 *
 * 通常通过 <script data-* defer> 使用，不需要手动调用。
 */

export interface LoaderConfig {
    appId?: string;
    appName?: string;
    server?: string;
    configUrl?: string;
    tokenUrl?: string;
    version?: string;
    debug?: boolean;
    autoInstrument?: boolean;
    traceparent?: string;
    src?: string;
    scriptSrc?: string;
}

export interface StardustLoader {
    /** 读取当前 script 标签上的配置。 */
    readConfig(script: HTMLScriptElement | null, doc: Document): LoaderConfig;
    /** 计算 browser.js 地址。 */
    resolveSrc(config: LoaderConfig): string;
    /** 合并后端配置。 */
    mergeConfig(config: LoaderConfig, remote: Record<string, unknown>): LoaderConfig;
    /** 创建并启动追踪器。 */
    start(root: Window, config: LoaderConfig): unknown;
    /** 引导入口。 */
    bootstrap(root: Window, doc: Document): Promise<unknown>;
}

declare const loader: StardustLoader;
export default loader;
