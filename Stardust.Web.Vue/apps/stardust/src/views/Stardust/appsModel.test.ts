// 应用页纯逻辑单测（node --experimental-strip-types 运行）
import {
  deriveAppStatus,
  aggregateKpis,
  filterByStatus,
  filterByKeyword,
  historyRows,
  toAggRows,
  instancesRows,
  configKv,
  configText,
  pipelineSteps,
  servicesGroups,
  type AppListItem,
} from './appsModel.ts'

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

console.log('deriveAppStatus:')
ok(deriveAppStatus({ enable: true }, true) === 'online', 'enable+在线 → online')
ok(deriveAppStatus({ enable: false }, true) === 'offline', 'enable=false → offline')
ok(deriveAppStatus({ enable: true }, false) === 'offline', 'enable=true 但不在线 → offline')

console.log('toAggRows + aggregateKpis:')
const apps: AppListItem[] = [
  { name: 'order-service', enable: true },
  { name: 'payment-gateway', enable: true },
  { name: 'user-center', enable: false },
]
const counts = new Map<string, number>([
  ['order-service', 6],
  ['payment-gateway', 3],
])
const rows = toAggRows(apps, counts)
ok(rows[0].status === 'online', 'order-service 在线')
ok(rows[0].instances === 6, 'order-service 实例数=6')
ok(rows[2].status === 'offline', 'user-center 离线 (enable=false)')
ok(rows[2].instances === 0, 'user-center 实例数=0')
const kpi = aggregateKpis(rows)
ok(kpi.total === 3, 'total=3')
ok(kpi.online === 2, 'online=2')
ok(kpi.offline === 1, 'offline=1')
ok(kpi.warn === 0, 'warn=0')

console.log('filterByStatus:')
ok(filterByStatus(rows, 'all').length === 3, 'all → 3 条')
ok(filterByStatus(rows, 'online').length === 2, 'online → 2 条')
ok(filterByStatus(rows, 'offline').length === 1, 'offline → 1 条')
ok(filterByStatus(rows, 'warn').length === 0, 'warn → 0 条')

console.log('filterByKeyword:')
ok(filterByKeyword(rows, 'order').length === 1, 'order → 1 条')
ok(filterByKeyword(rows, '').length === 3, '空关键字 → 3 条')

console.log('instancesRows:')
const onlineRows = [
  { appName: 'order-service', client: 'c1', version: '1.0' },
  { name: 'pay-gateway', client: 'c2' },
  { appName: 'order-service', client: 'c3' },
]
ok(instancesRows(onlineRows, 'order-service').length === 2, 'appName 匹配 2 行')
ok(instancesRows(onlineRows, 'pay-gateway').length === 1, 'name 回退匹配')
ok(instancesRows(onlineRows, 'nope').length === 0, '无匹配 → 0')
ok(instancesRows([], 'order-service').length === 0, '空输入 → 0')

console.log('historyRows:')
const hist = historyRows([
  { action: 'Insert', time: '2026-08-13T08:00:00' },
  { action: 'Update', time: '2026-08-13T09:00:00' },
  { action: 'Delete', time: '2026-08-13T10:00:00' },
  { action: 'Read', time: '2026-08-13T11:00:00' },
  { action: 'View', time: '2026-08-13T07:00:00' },
  { action: 'Publish', time: '2026-08-13T12:00:00' },
])
ok(hist.length === 3, '剔除 Insert/Update/Delete 3 行')
ok(hist.map((r) => r.action).join(',') === 'Publish,Read,View', '按 time 倒序')
ok(historyRows([]).length === 0, '空输入 → 空数组')

console.log('configKv:')
const cfg = configKv([
  { key: 'enable', value: true },
  { field: 'port', content: '6681' },
  { name: 'timeout', value: 30 },
  { key: '  ', value: 'blank' },
  { key: 'empty', value: null },
])
ok(cfg.length === 4, '空 key 行剔除')
ok(cfg[0].key === 'enable' && cfg[0].value === 'true', 'key/value 直取')
ok(cfg[1].key === 'port' && cfg[1].value === '6681', 'field/content 回退')
ok(cfg[2].key === 'timeout' && cfg[2].value === '30', '数字转字符串')
ok(cfg[3].key === 'empty' && cfg[3].value === '', 'null 值 → 空串')

console.log('configText:')
ok(configText([]) === '', '空 → 空串')
ok(configText([{ key: 'a', value: 1 }, { key: 'b', value: 'x' }]) === 'a=1\nb=x', 'key=value 每行一条')
ok(configText([{ key: 'c', content: 'v' }]) === 'c=v', 'content 回退')

console.log('pipelineSteps:')
const order = pipelineSteps({ status: 'Success' })
ok(order.map((s) => s.label).join(',') === '构建,上传,分发,部署', '四步顺序')
ok(order.every((s) => s.state === 'done'), 'Success → 全 done')
const pending = pipelineSteps({ status: 'Pending' })
ok(pending[0].state === 'run' && pending.slice(1).every((s) => s.state === 'wait'), 'Pending → 构建 run 其余 wait')
const up = pipelineSteps({ status: 'UploadSucceeded' })
ok(up[0].state === 'done' && up[1].state === 'run' && up[2].state === 'wait' && up[3].state === 'wait', 'UploadSucceeded → 上传 run')
const dep = pipelineSteps({ status: 'Deploying' })
ok(dep[0].state === 'done' && dep[1].state === 'done' && dep[2].state === 'run' && dep[3].state === 'wait', 'Deploying → 分发 run')
const fail1 = pipelineSteps({ status: 'Failed', buildStartedTime: '2026-08-13T08:00:00' })
ok(fail1[0].state === 'done' && fail1[1].state === 'run', 'Failed 仅构建开始 → 上传 run')
const fail2 = pipelineSteps({ status: 'Failed', buildStartedTime: '2026-08-13T08:00:00', buildFinishedTime: '2026-08-13T08:05:00' })
ok(fail2[0].state === 'done' && fail2[1].state === 'done' && fail2[2].state === 'run', 'Failed 构建完成 → 分发 run')
const fail3 = pipelineSteps({ status: 'Failed' })
ok(fail3[0].state === 'run' && fail3[1].state === 'wait', 'Failed 无时间 → 构建 run')
const unk = pipelineSteps({ status: 'Whatever' })
ok(unk[0].state === 'run' && unk[1].state === 'wait', '未知状态 → 构建 run')

console.log('servicesGroups:')
const srv = servicesGroups([
  { serviceName: 'svc-a' },
  { serviceName: 'svc-b', direction: 'consume' },
  { serviceName: 'svc-c', scope: 'CONSUMER' },
  { serviceName: 'svc-d', direction: '消费' },
  { serviceName: 'svc-e', scope: 'internal' },
])
ok(srv.provide.length === 2, '默认提供 2 行')
ok(srv.consume.length === 3, '消费 3 行')
ok(srv.consume.every((r) => ['svc-b', 'svc-c', 'svc-d'].includes(r.serviceName ?? '')), 'consume/consumer/消费 均识别')
ok(servicesGroups([]).provide.length === 0 && servicesGroups([]).consume.length === 0, '空输入 → 双空')

console.log(`\n结果：${pass} 通过 / ${fail} 失败`)
if (fail > 0) process.exit(1)
