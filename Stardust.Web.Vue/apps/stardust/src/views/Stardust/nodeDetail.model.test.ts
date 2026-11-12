/**
 * 节点详情 · 指令行映射单测（task-70：camelCase 错位修复，红先行）
 *
 * 真实返回字段（2026-11-09 走查实锤，GET /Nodes/NodeCommand?nodeId=1）：
 *   { id, nodeID, command, status, result, createTime, createIP, traceId, ... }
 * 缺陷背景：NodeDetail.vue 旧映射 `r.Name ?? r.name` 无此字段 → 下拉 option 全空
 * → Send UI 链不可达。与 REG-01（AppServiceMeta camelCase 错位）同根同族。
 */
import { describe, expect, it } from 'vitest'
import { mapCommandRows } from './nodeDetail.model'

describe('mapCommandRows', () => {
  it('camelCase 真实行 → name 取 command，id 保留', () => {
    const rows = [
      { id: 13, nodeID: 1, command: 'echo walkthrough-nc6-20261109', status: 1, result: 'ok', createIP: '127.0.0.1' },
      { id: 12, nodeID: 1, command: 'ping', status: 1, result: 'ok', createIP: '127.0.0.1' },
    ]
    const out = mapCommandRows(rows)
    expect(out).toHaveLength(2)
    expect(out[0]).toEqual({ id: 13, name: 'echo walkthrough-nc6-20261109' })
    expect(out[1]).toEqual({ id: 12, name: 'ping' })
  })

  it('PascalCase 行（兼容旧序列化）→ 兜底取 Command', () => {
    const rows = [{ Id: 3, Command: 'sysinfo' }]
    expect(mapCommandRows(rows)).toEqual([{ id: 3, name: 'sysinfo' }])
  })

  it('command 缺失/空的行剔除，不让空名进下拉（防同族全空再现）', () => {
    const rows = [
      { id: 1, command: 'echo a' },
      { id: 2 },
      { id: 3, command: '' },
      { id: 4, Command: undefined },
    ]
    expect(mapCommandRows(rows)).toEqual([{ id: 1, name: 'echo a' }])
  })
})
