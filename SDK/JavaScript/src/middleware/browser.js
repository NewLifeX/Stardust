/**
 * 浏览器环境中间件兼容入口。
 * 自动埋点实现位于 ../browser.js。
 */

'use strict';

const browser = require('../browser');

module.exports = {
    installBrowserInterceptors: browser.installBrowserInterceptors,
    installFetchInterceptor: browser.installFetchInterceptor,
    installXHRInterceptor: browser.installXHRInterceptor,
};
