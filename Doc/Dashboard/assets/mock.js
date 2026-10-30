/**
 * 星尘看板设计稿 · 统一假数据与视图工具
 * -----------------------------------------------------------------------------
 * 所有页面共用本文件，保证「系统 → 项目 → 应用 → 节点」各看板中的项目、应用、
 * 节点与指标彼此一致，评审时不会出现数字对不上。
 *
 * 演示三态（地址栏追加）：
 *   ?scope=none       无任何授权项目（整页空态）
 *   ?scope=limited    仅可见 3/12 个项目（受限账号）
 *   无参数            全量数据（管理员）
 */
(function () {
  'use strict';

  const hours = Array.from({ length: 24 }, (_, i) => String(i).padStart(2, '0') + ':00');
  const week = ['10-24', '10-25', '10-26', '10-27', '10-28', '10-29', '10-30'];

  /** 确定性伪随机，保证每次打开曲线一致 */
  function seeded(seed) {
    let s = seed;
    return function () {
      s = (s * 1103515245 + 12345) % 2147483648;
      return s / 2147483648;
    };
  }

  function wave(count, base, amp, seed, floor, ceil) {
    const rnd = seeded(seed);
    return Array.from({ length: count }, (_, i) => {
      const day = Math.sin((i / count) * Math.PI * 2 - 1.2) * amp;
      const noise = (rnd() - 0.5) * amp * 0.55;
      const v = base + day + noise;
      return Number(Math.min(ceil, Math.max(floor, v)).toFixed(2));
    });
  }

  const projects = [
    { key: 'stardust', name: '星尘平台', manager: '陈星', members: 12, apps: 6, appsOnline: 6, nodes: 8, nodesOnline: 8, requests: 2186420, errorRate: 0.21, alerts: 1, health: 98, visible: true },
    { key: 'shop', name: '电商中台', manager: '李海', members: 8, apps: 4, appsOnline: 4, nodes: 8, nodesOnline: 7, requests: 4863500, errorRate: 0.42, alerts: 3, health: 92, visible: true },
    { key: 'logistics', name: '物流调度', manager: '王悦', members: 6, apps: 3, appsOnline: 3, nodes: 5, nodesOnline: 5, requests: 1208900, errorRate: 0.18, alerts: 1, health: 96, visible: true },
    { key: 'iot', name: '物联网平台', manager: '赵启', members: 5, apps: 4, appsOnline: 3, nodes: 12, nodesOnline: 11, requests: 986400, errorRate: 0.66, alerts: 4, health: 87, visible: false },
    { key: 'datahub', name: '数据中台', manager: '周衡', members: 4, apps: 2, appsOnline: 2, nodes: 4, nodesOnline: 4, requests: 642800, errorRate: 0.11, alerts: 0, health: 99, visible: false },
    { key: 'portal', name: '官网门户', manager: '林岚', members: 3, apps: 2, appsOnline: 2, nodes: 3, nodesOnline: 3, requests: 486200, errorRate: 0.07, alerts: 0, health: 99, visible: false },
    { key: 'pay', name: '支付结算', manager: '何川', members: 6, apps: 3, appsOnline: 3, nodes: 6, nodesOnline: 5, requests: 820600, errorRate: 0.31, alerts: 2, health: 94, visible: false },
    { key: 'ticket', name: '客服工单', manager: '苏芮', members: 4, apps: 2, appsOnline: 2, nodes: 3, nodesOnline: 3, requests: 236400, errorRate: 0.09, alerts: 0, health: 99, visible: false },
    { key: 'risk', name: '风控计算', manager: '罗晟', members: 5, apps: 3, appsOnline: 3, nodes: 7, nodesOnline: 6, requests: 512900, errorRate: 0.24, alerts: 1, health: 95, visible: false },
    { key: 'message', name: '消息中心', manager: '方哲', members: 3, apps: 2, appsOnline: 2, nodes: 2, nodesOnline: 2, requests: 318700, errorRate: 0.15, alerts: 0, health: 98, visible: false },
    { key: 'bi', name: '报表分析', manager: '沈岚', members: 4, apps: 3, appsOnline: 2, nodes: 4, nodesOnline: 3, requests: 184300, errorRate: 0.38, alerts: 1, health: 90, visible: false },
    { key: 'open', name: '开放平台', manager: '徐克', members: 5, apps: 3, appsOnline: 3, nodes: 5, nodesOnline: 4, requests: 426800, errorRate: 0.19, alerts: 1, health: 95, visible: false }
  ];

  const apps = [
    { key: 'order', name: '订单服务', code: 'order-api', project: '电商中台', category: '业务服务', status: 'online', instances: 6, totalInstances: 6, qps: 310, errorRate: 0.42, avgCost: 42, p95: 186, services: 23, version: 'v2.8.1', compile: '2026-10-24 09:12', today: 4863500, spark: 'orderQps' },
    { key: 'product', name: '商品服务', code: 'product-api', project: '电商中台', category: '业务服务', status: 'online', instances: 4, totalInstances: 4, qps: 206, errorRate: 0.12, avgCost: 26, p95: 108, services: 15, version: 'v1.9.4', compile: '2026-10-21 15:40', today: 3128800, spark: 'productQps' },
    { key: 'pay', name: '支付网关', code: 'pay-gateway', project: '电商中台', category: '网关', status: 'online', instances: 3, totalInstances: 3, qps: 137, errorRate: 0.58, avgCost: 68, p95: 246, services: 9, version: 'v3.1.0', compile: '2026-10-18 11:02', today: 1420600, spark: 'payQps' },
    { key: 'user', name: '用户中心', code: 'user-center', project: '电商中台', category: '基础服务', status: 'online', instances: 5, totalInstances: 5, qps: 98, errorRate: 0.09, avgCost: 18, p95: 64, services: 12, version: 'v2.2.6', compile: '2026-10-12 08:36', today: 986400, spark: 'userQps' },
    { key: 'starser', name: 'StarServer 服务端', code: 'starserver', project: '星尘平台', category: '平台服务', status: 'online', instances: 4, totalInstances: 4, qps: 168, errorRate: 0.14, avgCost: 22, p95: 88, services: 31, version: 'v3.4.2', compile: '2026-10-27 20:18', today: 1682400, spark: 'starserQps' },
    { key: 'starweb', name: 'StarWeb 控制台', code: 'starweb', project: '星尘平台', category: 'Web 前端', status: 'online', instances: 2, totalInstances: 2, qps: 42, errorRate: 0.05, avgCost: 12, p95: 46, services: 6, version: 'v3.4.0', compile: '2026-10-26 10:05', today: 386200, spark: 'starwebQps' },
    { key: 'dispatch', name: '调度引擎', code: 'dispatch', project: '物流调度', category: '业务服务', status: 'online', instances: 3, totalInstances: 3, qps: 76, errorRate: 0.18, avgCost: 34, p95: 132, services: 11, version: 'v1.6.3', compile: '2026-10-19 14:22', today: 826400, spark: 'dispatchQps' }
  ];

  const nodes = [
    { key: 'nj-app-01', name: 'NJ-App-01', ip: '10.0.2.11', project: '电商中台', location: '南京', os: 'Linux', cpu: 38, memory: 62, disk: 54, status: 'online', load: 1.24, temp: 44, uptime: 96 },
    { key: 'nj-app-02', name: 'NJ-App-02', ip: '10.0.2.12', project: '电商中台', location: '南京', os: 'Linux', cpu: 42, memory: 68, disk: 55, status: 'online', load: 1.82, temp: 47, uptime: 128 },
    { key: 'sh-app-01', name: 'SH-App-01', ip: '10.0.3.21', project: '电商中台', location: '上海', os: 'Linux', cpu: 71, memory: 74, disk: 63, status: 'warn', load: 3.42, temp: 58, uptime: 64 },
    { key: 'sh-app-02', name: 'SH-App-02', ip: '10.0.3.22', project: '电商中台', location: '上海', os: 'Linux', cpu: 33, memory: 51, disk: 47, status: 'online', load: 1.06, temp: 42, uptime: 64 },
    { key: 'gz-app-01', name: 'GZ-App-01', ip: '10.0.4.31', project: '电商中台', location: '广州', os: 'Windows', cpu: 0, memory: 0, disk: 39, status: 'offline', load: 0, temp: 0, uptime: 0 },
    { key: 'bj-app-01', name: 'BJ-App-01', ip: '10.0.5.41', project: '电商中台', location: '北京', os: 'Linux', cpu: 58, memory: 66, disk: 71, status: 'online', load: 2.14, temp: 51, uptime: 210 },
    { key: 'nj-db-01', name: 'NJ-DB-01', ip: '10.0.2.51', project: '电商中台', location: '南京', os: 'Linux', cpu: 62, memory: 81, disk: 72, status: 'online', load: 2.68, temp: 55, uptime: 302 },
    { key: 'nj-cache-01', name: 'NJ-Cache-01', ip: '10.0.2.61', project: '电商中台', location: '南京', os: 'Linux', cpu: 18, memory: 36, disk: 22, status: 'online', load: 0.42, temp: 36, uptime: 302 }
  ];

  const series = {
    systemThroughput: wave(24, 1850, 620, 7, 980, 2780).map((v) => Math.round(v)),
    systemError: wave(24, 0.44, 0.28, 11, 0.08, 1.02),
    appRequest: wave(24, 292, 58, 13, 168, 386).map((v) => Math.round(v)),
    appCost: wave(24, 44, 9, 17, 26, 68),
    appError: wave(24, 0.44, 0.26, 19, 0.06, 0.98),
    nodeCpu: wave(24, 44, 20, 23, 18, 92),
    nodeMemory: wave(24, 66, 7, 29, 54, 78),
    nodeDisk: wave(24, 55, 2.4, 31, 51, 59),
    nodeIops: wave(24, 640, 320, 37, 120, 1480).map((v) => Math.round(v)),
    weekRequest: [2680, 2910, 2780, 3120, 3410, 3860, 3290],
    weekError: [0.32, 0.28, 0.41, 0.36, 0.52, 0.61, 0.47],
    orderQps: wave(18, 310, 42, 41, 210, 398).map((v) => Math.round(v)),
    productQps: wave(18, 206, 34, 43, 130, 286).map((v) => Math.round(v)),
    payQps: wave(18, 137, 26, 47, 82, 198).map((v) => Math.round(v)),
    userQps: wave(18, 98, 18, 53, 56, 142).map((v) => Math.round(v)),
    starserQps: wave(18, 168, 24, 59, 96, 224).map((v) => Math.round(v)),
    starwebQps: wave(18, 42, 9, 61, 18, 68).map((v) => Math.round(v)),
    dispatchQps: wave(18, 76, 14, 67, 38, 118).map((v) => Math.round(v)),
    systemSpark: wave(16, 1900, 320, 71, 1200, 2500).map((v) => Math.round(v)),
    onlineRate: wave(16, 88, 4, 91, 80, 96).map((v) => Number(v.toFixed(1))),
    appSpark: wave(16, 300, 36, 73, 210, 380).map((v) => Math.round(v)),
    nodeSpark: wave(16, 46, 14, 79, 22, 82),
    errorSpark: wave(16, 0.44, 0.18, 83, 0.12, 0.86)
  };

  const endpoints = [
    { path: '/api/order/create', qps: 412, cost: 128, errorRate: 0.32 },
    { path: '/api/order/query', qps: 986, cost: 56, errorRate: 0.06 },
    { path: '/api/order/detail', qps: 320, cost: 61, errorRate: 0.11 },
    { path: '/api/cart/checkout', qps: 140, cost: 96, errorRate: 0.28 },
    { path: '/api/order/cancel', qps: 86, cost: 74, errorRate: 0.18 }
  ];

  const errorCodes = [
    { name: '500 服务异常', value: 182, color: '#FF4545' },
    { name: '502 网关错误', value: 64, color: '#FFC208' },
    { name: '请求超时', value: 41, color: '#8C5CF5' },
    { name: '400 参数错误', value: 96, color: '#6FE3EC' }
  ];

  const dependencies = [
    { name: '用户中心', type: '出站', qps: 286, cost: 2.4, errorRate: 0.02 },
    { name: '商品服务', type: '出站', qps: 412, cost: 8.1, errorRate: 0.05 },
    { name: '支付网关', type: '出站', qps: 137, cost: 46.0, errorRate: 0.42 },
    { name: 'order-db (MySQL)', type: '存储', qps: 1240, cost: 3.2, errorRate: 0.01 },
    { name: 'redis-order', type: '缓存', qps: 2860, cost: 0.8, errorRate: 0 }
  ];

  const deployments = [
    { app: '订单服务', version: 'v2.8.1', nodes: 6, status: 'success', cost: '2 分 18 秒', time: '2 小时前' },
    { app: '支付网关', version: 'v3.1.0', nodes: 3, status: 'success', cost: '1 分 42 秒', time: '5 小时前' },
    { app: '商品服务', version: 'v1.9.4', nodes: 4, status: 'success', cost: '1 分 06 秒', time: '昨天 15:40' },
    { app: '用户中心', version: 'v2.2.6', nodes: 5, status: 'failed', cost: '0 分 36 秒', time: '昨天 09:12' }
  ];

  const alerts = [
    { level: 'bad', app: '支付网关', node: 'SH-App-01', message: '错误率 0.58% 超过阈值 0.5%', time: '3 分钟前', status: '未确认' },
    { level: 'warn', app: '订单服务', node: 'SH-App-01', message: 'CPU 使用率 71% 持续 10 分钟', time: '12 分钟前', status: '已确认' },
    { level: 'warn', app: '订单服务', node: 'NJ-DB-01', message: '磁盘 /data 使用率 72%', time: '48 分钟前', status: '未确认' },
    { level: 'bad', app: '商品服务', node: 'GZ-App-01', message: '节点离线超过 30 分钟', time: '1 小时前', status: '未确认' },
    { level: 'warn', app: '用户中心', node: 'BJ-App-01', message: '接口 P95 246ms 超过阈值 200ms', time: '2 小时前', status: '已恢复' }
  ];

  const events = [
    { level: 'bad', title: '支付网关 错误率 0.58%', meta: '电商中台 · SH-App-01 · 告警', time: '14:18' },
    { level: 'ok', title: '订单服务 部署 v2.8.1 完成', meta: '电商中台 · 6 个节点 · 耗时 2 分 18 秒', time: '12:32' },
    { level: 'warn', title: 'SH-App-01 CPU 使用率 71%', meta: '电商中台 · 持续 10 分钟', time: '12:06' },
    { level: 'ok', title: 'GZ-App-01 已恢复心跳', meta: '电商中台 · 离线 42 分钟', time: '11:44' },
    { level: 'info', title: '配置 order.timeout 已下发', meta: '订单服务 · 版本 v18 · 6 个实例', time: '10:58' },
    { level: 'bad', title: 'GZ-App-01 节点离线', meta: '电商中台 · 10.0.4.31', time: '10:42' },
    { level: 'ok', title: '物流调度 接入新应用 调度引擎', meta: '物流调度 · v1.6.3', time: '09:26' },
    { level: 'warn', title: 'NJ-DB-01 磁盘 /data 72%', meta: '电商中台 · 建议清理归档日志', time: '08:51' },
    { level: 'info', title: '星尘平台 新增节点 NJ-Cache-01', meta: '星尘平台 · 南京 · 10.0.2.61', time: '08:12' },
    { level: 'ok', title: '支付网关 部署 v3.1.0 完成', meta: '电商中台 · 3 个节点', time: '昨天 18:20' },
    { level: 'warn', title: '商品服务 P95 108ms 接近阈值', meta: '电商中台 · 4 个实例', time: '昨天 16:02' },
    { level: 'info', title: '开放平台 配置中心同步完成', meta: '开放平台 · 12 个配置项', time: '昨天 14:35' }
  ];

  const nodeProcesses = [
    { name: 'dotnet StarServer', pid: 2214, cpu: 32.4, memory: '1.24 GB' },
    { name: 'dotnet StarWeb', pid: 2388, cpu: 11.2, memory: '860 MB' },
    { name: 'redis-server', pid: 1180, cpu: 3.1, memory: '420 MB' },
    { name: 'nginx', pid: 902, cpu: 0.8, memory: '68 MB' },
    { name: 'node_exporter', pid: 764, cpu: 0.4, memory: '42 MB' },
    { name: 'systemd-journald', pid: 312, cpu: 0.2, memory: '36 MB' }
  ];

  const nodeOps = [
    { level: 'ok', title: '远程命令 df -h 执行成功', meta: '操作人 陈星 · 输出已归档', time: '3 小时前' },
    { level: 'info', title: 'StarAgent 升级至 1.21.4', meta: '灰度批次 batch-02', time: '2 天前' },
    { level: 'warn', title: 'CPU 峰值 92% 告警', meta: '持续 4 分钟 · 已自动恢复', time: '1 天前' },
    { level: 'ok', title: '重启 StarAgent 服务', meta: '操作人 李海 · 耗时 6 秒', time: '5 天前' }
  ];

  const appChanges = [
    { level: 'ok', title: '部署 v2.8.1 到 6 个节点', meta: '流水线 #248 · 2 分 18 秒 · 全部成功', time: '2 小时前' },
    { level: 'info', title: '配置 order.timeout 由 30s 调整为 45s', meta: '配置中心 v18 · 已下发 6 个实例', time: '昨天 10:58' },
    { level: 'warn', title: '回滚 v2.8.0 → v2.7.9', meta: '原因：支付回调超时 · 操作人 李海', time: '3 天前' },
    { level: 'info', title: '新增实例 NJ-App-02 / SH-App-02', meta: '扩容 2 个实例 · 由自动伸缩触发', time: '5 天前' }
  ];

  const systemTopology = {
    nodes: [
      { name: 'StarWeb', value: 42, x: 120, y: 90, symbolSize: 34 },
      { name: 'StarServer', value: 168, x: 300, y: 60, symbolSize: 46 },
      { name: '订单服务', value: 310, x: 470, y: 120, symbolSize: 54 },
      { name: '商品服务', value: 206, x: 470, y: 30, symbolSize: 42 },
      { name: '支付网关', value: 137, x: 620, y: 88, symbolSize: 34 },
      { name: '用户中心', value: 98, x: 300, y: 170, symbolSize: 30 },
      { name: '消息队列', value: 260, x: 150, y: 180, symbolSize: 30 },
      { name: 'Redis 集群', value: 320, x: 620, y: 180, symbolSize: 32 }
    ],
    links: [
      { source: 'StarWeb', target: 'StarServer', value: 42 },
      { source: 'StarServer', target: '订单服务', value: 168 },
      { source: 'StarServer', target: '商品服务', value: 120 },
      { source: '订单服务', target: '支付网关', value: 137 },
      { source: '订单服务', target: '用户中心', value: 98 },
      { source: '商品服务', target: 'Redis 集群', value: 206 },
      { source: 'StarServer', target: '消息队列', value: 168 },
      { source: '订单服务', target: '消息队列', value: 86 }
    ]
  };

  const projectTopology = {
    nodes: [
      { name: '电商中台', value: 4, x: 90, y: 110, symbolSize: 52, category: 0 },
      { name: '订单服务', value: 310, x: 300, y: 50, symbolSize: 44, category: 1 },
      { name: '商品服务', value: 206, x: 300, y: 110, symbolSize: 40, category: 1 },
      { name: '支付网关', value: 137, x: 300, y: 170, symbolSize: 36, category: 1 },
      { name: '用户中心', value: 98, x: 300, y: 225, symbolSize: 32, category: 1 },
      { name: 'NJ-App-01', value: 38, x: 520, y: 20, symbolSize: 26, category: 2 },
      { name: 'NJ-App-02', value: 42, x: 520, y: 66, symbolSize: 26, category: 2 },
      { name: 'SH-App-01', value: 71, x: 520, y: 112, symbolSize: 26, category: 2 },
      { name: 'SH-App-02', value: 33, x: 520, y: 158, symbolSize: 26, category: 2 },
      { name: 'BJ-App-01', value: 58, x: 520, y: 204, symbolSize: 26, category: 2 },
      { name: 'NJ-DB-01', value: 62, x: 520, y: 248, symbolSize: 26, category: 2 }
    ],
    links: [
      { source: '电商中台', target: '订单服务', value: 4 },
      { source: '电商中台', target: '商品服务', value: 4 },
      { source: '电商中台', target: '支付网关', value: 3 },
      { source: '电商中台', target: '用户中心', value: 5 },
      { source: '订单服务', target: 'NJ-App-01', value: 2 },
      { source: '订单服务', target: 'NJ-App-02', value: 2 },
      { source: '订单服务', target: 'SH-App-01', value: 2 },
      { source: '商品服务', target: 'SH-App-01', value: 1 },
      { source: '商品服务', target: 'SH-App-02', value: 2 },
      { source: '商品服务', target: 'BJ-App-01', value: 1 },
      { source: '支付网关', target: 'BJ-App-01', value: 1 },
      { source: '支付网关', target: 'NJ-DB-01', value: 2 },
      { source: '用户中心', target: 'NJ-DB-01', value: 3 },
      { source: '用户中心', target: 'NJ-App-01', value: 2 }
    ]
  };

  const locations = [
    { name: '南京', nodes: 8, cpu: 48, memory: 62, disk: 58 },
    { name: '上海', nodes: 9, cpu: 56, memory: 68, disk: 61 },
    { name: '广州', nodes: 7, cpu: 42, memory: 55, disk: 49 },
    { name: '北京', nodes: 8, cpu: 51, memory: 63, disk: 66 }
  ];

  const scope = { totalProjects: 12, visibleCount: 3 };

  /* ------------------------------ 工具函数 ------------------------------ */

  const uid = { n: 0 };

  function fmt(n) {
    if (n === null || n === undefined || Number.isNaN(n)) return '—';
    return Number(n).toLocaleString('en-US');
  }

  function compact(n) {
    if (n >= 100000000) return (n / 100000000).toFixed(2).replace(/\.?0+$/, '') + ' 亿';
    if (n >= 10000) return (n / 10000).toFixed(1).replace(/\.0$/, '') + ' 万';
    return fmt(n);
  }

  function spark(values, color) {
    const c = color || 'var(--sd-color-brand)';
    const width = 100;
    const height = 32;
    const min = Math.min.apply(null, values);
    const max = Math.max.apply(null, values);
    const span = max - min || 1;
    const step = width / (values.length - 1 || 1);
    const pts = values.map(function (v, i) {
      const x = i * step;
      const y = height - 3 - ((v - min) / span) * (height - 7);
      return [x, y];
    });
    let line = '';
    pts.forEach(function (p, i) {
      line += (i ? 'L' : 'M') + p[0].toFixed(1) + ' ' + p[1].toFixed(1) + ' ';
    });
    const area = line + 'L ' + width + ' ' + height + ' L 0 ' + height + ' Z';
    const gid = 'sdg' + ++uid.n;
    return '<svg viewBox="0 0 ' + width + ' ' + height + '" preserveAspectRatio="none" width="100%" height="100%">' +
      '<defs><linearGradient id="' + gid + '" x1="0" y1="0" x2="0" y2="1">' +
      '<stop offset="0%" stop-color="' + c + '" stop-opacity=".34"/>' +
      '<stop offset="100%" stop-color="' + c + '" stop-opacity="0"/>' +
      '</linearGradient></defs>' +
      '<path d="' + area + '" fill="url(#' + gid + ')"/>' +
      '<path d="' + line + '" fill="none" stroke="' + c + '" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>' +
      '</svg>';
  }

  function mountSparks() {
    document.querySelectorAll('[data-spark]').forEach(function (el) {
      const key = el.getAttribute('data-spark');
      const data = series[key];
      if (!data) return;
      const color = el.getAttribute('data-spark-color') || undefined;
      el.innerHTML = spark(data, color);
    });
  }

  function currentScope() {
    const s = new URLSearchParams(location.search).get('scope');
    return s === 'limited' || s === 'none' ? s : 'full';
  }

  const scopeLabels = {
    full: '全部 12 个项目',
    limited: '可见 3 / 12 个项目',
    none: '未授权任何项目'
  };

  function applyScope() {
    const state = currentScope();
    document.documentElement.dataset.scope = state;
    document.querySelectorAll('[data-scope-label]').forEach(function (el) {
      el.textContent = scopeLabels[state];
    });
    document.querySelectorAll('[data-scope-projects]').forEach(function (el) {
      el.textContent = state === 'full' ? String(scope.totalProjects) : String(scope.visibleCount);
    });
    document.querySelectorAll('[data-scope-value]').forEach(function (el) {
      const v = state === 'full' ? el.dataset.full : (el.dataset.limited || el.dataset.full);
      el.textContent = v;
    });
    if (state === 'limited') {
      document.querySelectorAll('[data-project]:not([data-visible="true"])').forEach(function (el) {
        el.setAttribute('data-scope', 'hide-limited');
      });
    }
  }

  function cycleScope() {
    const state = currentScope();
    const next = state === 'full' ? 'limited' : state === 'limited' ? 'none' : 'full';
    const base = location.pathname;
    location.href = next === 'full' ? base : base + '?scope=' + next;
  }

  function startClock() {
    const els = document.querySelectorAll('[data-clock]');
    if (!els.length) return;
    function tick() {
      const text = new Date().toLocaleTimeString('zh-CN', { hour12: false });
      els.forEach(function (el) { el.textContent = text; });
    }
    tick();
    setInterval(tick, 1000);
  }

  function wireCommon() {
    applyScope();
    mountSparks();
    startClock();
    document.querySelectorAll('[data-action="toggle-scope"]').forEach(function (el) {
      el.addEventListener('click', cycleScope);
    });
    document.querySelectorAll('[data-action="refresh"]').forEach(function (el) {
      el.addEventListener('click', function () {
        el.classList.add('is-spinning');
        setTimeout(function () { el.classList.remove('is-spinning'); }, 700);
      });
    });
    document.querySelectorAll('.segment').forEach(function (seg) {
      seg.addEventListener('click', function (e) {
        const btn = e.target.closest('button');
        if (!btn || !seg.contains(btn)) return;
        seg.querySelectorAll('button').forEach(function (b) { b.classList.remove('is-active'); });
        btn.classList.add('is-active');
      });
    });
  }

  document.addEventListener('DOMContentLoaded', wireCommon);

  window.SD = {
    hours: hours,
    week: week,
    projects: projects,
    apps: apps,
    nodes: nodes,
    series: series,
    endpoints: endpoints,
    errorCodes: errorCodes,
    dependencies: dependencies,
    deployments: deployments,
    alerts: alerts,
    events: events,
    nodeProcesses: nodeProcesses,
    nodeOps: nodeOps,
    appChanges: appChanges,
    systemTopology: systemTopology,
    projectTopology: projectTopology,
    locations: locations,
    scope: scope,
    util: {
      fmt: fmt,
      compact: compact,
      spark: spark,
      mountSparks: mountSparks,
      currentScope: currentScope,
      cycleScope: cycleScope
    }
  };
})();
