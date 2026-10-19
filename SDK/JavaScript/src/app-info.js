/**
 * Node.js 应用性能信息采集。
 * 输出字段与星尘服务端 AppInfo 契约保持一致。
 */

'use strict';

const os = require('os');
const { PerformanceObserver } = require('perf_hooks');

let _gcCount = 0;
let _gcObserverStarted = false;

/**
 * 启动 GC 计数器。GC 次数用于计算采样周期内的增量。
 */
function startGcObserver() {
    if (_gcObserverStarted) return;
    _gcObserverStarted = true;

    try {
        if (typeof PerformanceObserver !== 'function') return;
        const observer = new PerformanceObserver((list) => {
            try {
                _gcCount += list.getEntries().length;
            } catch (err) {
                // 忽略采集异常
            }
        });
        observer.observe({ entryTypes: ['gc'] });
        if (typeof observer.unref === 'function') observer.unref();
    } catch (err) {
        // 部分运行时不允许 GC 观测，忽略即可
    }
}

/**
 * 获取本机非回环 IPv4 地址。
 * @returns {String} IP 地址
 */
function getLocalIP() {
    try {
        const interfaces = os.networkInterfaces();
        const names = Object.keys(interfaces);
        for (const name of names) {
            const list = interfaces[name] || [];
            for (const item of list) {
                if (item && item.family === 'IPv4' && !item.internal) return item.address;
            }
        }
    } catch (err) {
        // 忽略网卡枚举异常
    }
    return '127.0.0.1';
}

/**
 * 采集 AppInfo 性能信息。
 */
class AppInfoCollector {
    /**
     * @param {Object} [options] { appName, version, ip }
     */
    constructor(options) {
        const opts = options || {};
        this.appName = opts.appName ? String(opts.appName) : '';
        this.version = opts.version ? String(opts.version) : '1.1.0';
        this.ip = opts.ip ? String(opts.ip) : getLocalIP();

        let uptime = 0;
        try {
            uptime = process.uptime() * 1000;
        } catch (err) {
            uptime = 0;
        }
        this.startTime = new Date(Date.now() - uptime).toISOString();

        this._lastCpuMs = this._readCpuMs();
        this._lastWall = Date.now();
        this._lastGc = _gcCount;

        startGcObserver();
    }

    /**
     * 采集一次性能数据。
     * @returns {Object} AppInfo
     */
    collect() {
        const now = Date.now();
        const cpuMs = this._readCpuMs();
        const wallDelta = now - this._lastWall;
        const cpuDelta = cpuMs - this._lastCpuMs;
        const cpuUsage = wallDelta > 0 ? cpuDelta / wallDelta : 0;

        this._lastCpuMs = cpuMs;
        this._lastWall = now;

        let memory = {};
        try {
            memory = process.memoryUsage();
        } catch (err) {
            memory = {};
        }

        let handles = 0;
        try {
            if (typeof process._getActiveHandles === 'function') handles = process._getActiveHandles().length;
        } catch (err) {
            handles = 0;
        }

        const gcCount = _gcCount - this._lastGc;
        this._lastGc = _gcCount;

        let userName = '';
        try {
            userName = os.userInfo().username;
        } catch (err) {
            userName = '';
        }

        let machineName = '';
        try {
            machineName = os.hostname();
        } catch (err) {
            machineName = '';
        }

        let commandLine = '';
        try {
            commandLine = process.argv.join(' ');
        } catch (err) {
            commandLine = '';
        }

        return {
            Id: process.pid,
            Name: this.appName,
            Version: this.version,
            CommandLine: commandLine,
            UserName: userName,
            MachineName: machineName,
            IP: this.ip,
            StartTime: this.startTime,
            ProcessorTime: Math.round(cpuMs),
            CpuUsage: Math.max(0, cpuUsage),
            WorkingSet: memory.rss || 0,
            HeapSize: memory.heapTotal || 0,
            Threads: 0,
            Handles: handles,
            Connections: 0,
            GCCount: gcCount > 0 ? gcCount : 0,
            Listens: '',
            Time: now,
        };
    }

    /**
     * 读取进程累计 CPU 时间（毫秒）。
     * @returns {Number} CPU 毫秒
     * @private
     */
    _readCpuMs() {
        try {
            const usage = process.cpuUsage();
            return (usage.user + usage.system) / 1000;
        } catch (err) {
            return 0;
        }
    }
}

module.exports = {
    AppInfoCollector,
    getLocalIP,
    startGcObserver,
};
