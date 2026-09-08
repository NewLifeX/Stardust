// 节点抽屉 9 Tab 纯逻辑单测（node --experimental-strip-types 运行）
import {
  overviewInfo,
  rings,
  historyRows,
  commandRows,
  serviceRows,
  appOnlineRows,
  type NodeRow,
  type AppOnlineRow,
} from './nodes.model.ts'

let pass = 0
let fail = 0
function ok(cond: boolean, msg: string) {
  if (cond) {
    pass++
    console.log('  ✓', msg)
  } else {
    fail++
    console.error('  ✗', msg)
  }
}

console.log('overviewInfo:')
const kv = overviewInfo({
  name: 'node-a',
  iP: '10.0.0.1',
  projectName: 'prod',
  oS: 'Linux',
  architecture: 'x64',
  version: 'v2.1',
  lastActive: new Date(Date.now() - 30_000).toISOString(),
})
ok(kv.length === 6, '六项键值')
ok(kv[0].k === '节点名称' && kv[0].v === 'node-a', '节点名称')
ok(kv[1].k === 'IP 地址' && kv[1].v === '10.0.0.1', 'IP 地址优先 iP')
ok(kv[2].k === '集群' && kv[2].v === 'prod', '集群取 projectName')
ok(kv[3].k === '系统' && kv[3].v === 'Linux x64', '系统拼接 oS+架构')
ok(kv[4].k === '运行时长' && /^(\d+s|\d+m|\d+h \d+m|\d+d)$/.test(kv[4].v), '运行时长有值')
ok(kv[5].k === 'Agent 版本' && kv[5].v === 'v2.1', 'Agent 版本')
const fb = overviewInfo({})
ok(fb[0].v === '-', '无名称 → -')
ok(fb[1].v === '-', '无 IP → -')
ok(fb[2].v === '默认集群', '无集群 → 默认集群')
ok(fb[3].v === '-', '无系统 → -')
ok(fb[4].v === '-', '无运行时长 → -')
ok(fb[5].v === '-', '无版本 → -')
ok(overviewInfo({ address: '10.0.0.2' })[1].v === '10.0.0.2', 'IP 回退 address')
ok(overviewInfo({ oS: 'Linux' })[3].v === 'Linux', '仅有 oS')

console.log('rings:')
const r1 = rings({ cpu: 45 })
ok(r1.length === 3, '三环')
ok(r1[0].label === 'CPU' && r1[0].value === 45 && r1[0].color === 'var(--el-color-success)', 'CPU 取值 + success 色')
ok(r1[1].label === '内存' && r1[1].value === null, '内存无数据 → null')
ok(r1[2].label === '磁盘' && r1[2].value === null, '磁盘无数据 → null')
ok(rings({})[0].value === null, '无 CPU → null')
ok(rings({ cpu: '45' as unknown as number })[0].value === null, 'CPU 非 number → null')

console.log('historyRows:')
const h1 = historyRows({ lastLogin: '2026-08-13T10:23:00', lastActive: '2026-08-13T09:05:00' })
ok(h1.length === 2, '两条事件')
ok(h1[0].status === 'login' && h1[0].text === '节点最近登录' && h1[0].time === '2026-08-13 10:23', '登录更晚 → 在前')
ok(h1[1].status === 'active' && h1[1].text === '节点心跳上报' && h1[1].time === '2026-08-13 09:05', '心跳更早 → 在后')
const h2 = historyRows({ lastLogin: '2026-08-13T08:00:00', lastActive: '2026-08-13T11:11:00' })
ok(h2[0].status === 'active', '心跳更晚 → 在前（新→旧排序）')
const h3 = historyRows({})
ok(h3[0].status === 'login' && h3[0].time === '-', '非法时间 → 显示 -')
ok(h3[1].status === 'active' && h3[1].time === '-', '非法时间 → 显示 -')

console.log('commandRows:')
const cmds = commandRows()
ok(cmds.length === 3, '三行指令')
ok(cmds[0].role === 'restart' && cmds[0].name === '重启节点服务', '重启行')
ok(cmds[1].role === 'update' && cmds[1].name === '更新节点 Agent', '更新行')
ok(cmds[2].role === 'collect' && cmds[2].name === '采集节点指标', '采集行')

console.log('serviceRows:')
const svcJson = JSON.stringify([
  { name: 'user-service', role: 'provider', port: '8080', instances: 2 },
  { serviceName: 'svc-b', type: 'client', count: 3 },
  { name: 'order-client', role: 'consumer', address: '10.0.0.1:9090' },
])
const svcs = serviceRows(svcJson)
ok(svcs.length === 3, '解析 3 行')
ok(svcs[0].role === 'provider' && svcs[0].name === 'user-service', '提供在前')
ok(svcs[0].meta === '端口 8080 2 实例', 'meta 拼端口+实例')
ok(svcs[1].role === 'consumer' && svcs[1].name === 'svc-b', 'type=client → 消费组')
ok(svcs[1].meta === '3 实例', 'meta 仅实例')
ok(svcs[2].role === 'consumer' && svcs[2].name === 'order-client', '消费在后')
ok(svcs[2].meta === '端口 10.0.0.1:9090', 'meta 仅端口')
ok(serviceRows('not-json').length === 0, '非法 JSON → 空数组')
ok(serviceRows(undefined).length === 0, '非字符串 → 空数组')
ok(serviceRows(JSON.stringify([{ role: 'provider' }])).length === 0, '无名称行剔除')

console.log('appOnlineRows:')
const online: AppOnlineRow[] = [
  { appName: 'order', nodeName: 'n1' },
  { appName: 'pay', nodeName: 'n2' },
  { appName: 'user', nodeName: 'n1' },
]
const on1 = appOnlineRows(online, 'n1')
ok(on1.length === 2 && on1.every((r) => r.nodeName === 'n1'), '按 nodeName 过滤')
ok(appOnlineRows(online, 'nope').length === 0, '无匹配 → 空')
ok(appOnlineRows(online, '').length === 0, '空 nodeName → 空')
ok(appOnlineRows([], 'n1').length === 0, '空输入 → 空')
ok(appOnlineRows(undefined as unknown as AppOnlineRow[], 'n1').length === 0, 'undefined 输入容错')

console.log(`\n结果：${pass} 通过 / ${fail} 失败`)
if (fail > 0) process.exit(1)
