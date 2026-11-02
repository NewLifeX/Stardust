// 设置页纯逻辑（与 UI 解耦）—— 按设计交接包 PageSettings.jsx 的 SETTINGS_SCHEMA 重建。
// 设计真源：design-handoff/ui_kits/console/PageSettings.jsx（6 个设置域 + 字段 schema）。
//
// 后端现状：暂无 GetSetting/SaveSetting 公开接口（NodeController 不存在对应方法），
//           故沿用本地存储保证页面可用；待后端提供配置源（如 Configs/AppConfig 键值）再对接。

/** 字段控件类型：开关 / 分段单选 / 带后缀输入 */
export type FieldKind = 'toggle' | 'radio' | 'input'

export interface SettingField {
  /** 域内唯一键（持久化用） */
  key: string
  label: string
  kind: FieldKind
  /** kind === 'radio' 时的候选项 */
  options?: string[]
  /** kind === 'input' 时的单位后缀 */
  suffix?: string
  /** 出厂默认值 */
  def: string | number | boolean
}

export interface SettingDomain {
  key: string
  /** 星座节点中文标签 */
  label: string
  /** 星座节点英文提示 */
  hint: string
  /** 星座节点图标（Lucide 线性 path，stroke 1.5） */
  icon: string
  /** 面板标题 */
  title: string
  /** 面板描述 */
  desc: string
  fields: SettingField[]
}

/** 6 个设置域（标签 / 提示 / 图标 / 字段，对齐设计稿） */
export const DOMAINS: SettingDomain[] = [
  {
    key: 'registry',
    label: '注册中心',
    hint: 'Etcd · Gossip',
    icon: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zM16.24 7.76l-2.12 6.36-6.36 2.12 2.12-6.36z',
    title: '注册中心',
    desc: '选择服务注册的后端存储与集群一致性策略',
    fields: [
      { key: 'backend', label: '存储后端', kind: 'radio', options: ['Etcd', 'Consul', '内置 Gossip'], def: 'Etcd' },
      { key: 'heartbeatMs', label: '心跳间隔', kind: 'input', suffix: 'ms', def: 200 },
      { key: 'evictThreshold', label: '摘除阈值', kind: 'input', suffix: '次', def: 3 },
      { key: 'geoDr', label: '异地容灾', kind: 'toggle', def: true },
    ],
  },
  {
    key: 'discovery',
    label: '服务发现',
    hint: '策略 · 权重',
    icon: 'M6 3v12M6 21a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM18 9a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM18 9a9 9 0 0 1-9 9',
    title: '服务发现',
    desc: '客户端 / 服务端发现策略与负载均衡',
    fields: [
      { key: 'strategy', label: '默认策略', kind: 'radio', options: ['weighted-rr', 'least-conn', 'consistent-hash'], def: 'weighted-rr' },
      { key: 'clientCacheS', label: '客户端缓存', kind: 'input', suffix: 's', def: 30 },
      { key: 'grpcKeepalive', label: 'gRPC 长连接', kind: 'toggle', def: true },
    ],
  },
  {
    key: 'release',
    label: '发布策略',
    hint: '灰度 · 蓝绿',
    icon: 'M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09zM12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z',
    title: '发布策略',
    desc: '默认发布方式与观察窗口',
    fields: [
      { key: 'mode', label: '默认方式', kind: 'radio', options: ['滚动', '蓝绿', '金丝雀'], def: '金丝雀' },
      { key: 'firstPct', label: '首次流量比例', kind: 'input', suffix: '%', def: 5 },
      { key: 'watchMin', label: '观察窗口', kind: 'input', suffix: '分钟', def: 10 },
      { key: 'autoRollback', label: '自动回滚（错误率超阈值）', kind: 'toggle', def: true },
    ],
  },
  {
    key: 'alert',
    label: '告警通道',
    hint: '钉钉 · Webhook',
    icon: 'M18 8a6 6 0 0 0-12 0c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 0 1-3.46 0',
    title: '告警通道',
    desc: '事件推送渠道与静默规则',
    fields: [
      { key: 'channel', label: '默认通道', kind: 'radio', options: ['钉钉', 'Webhook', '邮件'], def: '钉钉' },
      { key: 'quietNight', label: '静默夜间告警（00:00–07:00）', kind: 'toggle', def: false },
      { key: 'mergeWindowMin', label: '合并同源告警窗口', kind: 'input', suffix: '分钟', def: 5 },
    ],
  },
  {
    key: 'security',
    label: '密钥 & 证书',
    hint: 'mTLS · RBAC',
    icon: 'M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z',
    title: '密钥 & 证书',
    desc: 'mTLS、密钥轮转与访问控制',
    fields: [
      { key: 'mtls', label: 'mTLS 全链路', kind: 'toggle', def: true },
      { key: 'certRotateDays', label: '证书自动轮转', kind: 'input', suffix: '天', def: 30 },
      { key: 'permModel', label: '默认权限模型', kind: 'radio', options: ['RBAC', 'ABAC'], def: 'RBAC' },
    ],
  },
  {
    key: 'members',
    label: '成员权限',
    hint: '角色 · 团队',
    icon: 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8zM23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
    title: '成员权限',
    desc: '组织成员、角色与团队',
    fields: [
      { key: 'defaultRole', label: '默认新成员角色', kind: 'radio', options: ['查看者', '操作员', '管理员'], def: '查看者' },
      { key: 'dualApproval', label: '需二次审批的操作', kind: 'toggle', def: true },
      { key: 'sso', label: 'SSO 单点登录', kind: 'toggle', def: true },
    ],
  },
]

export function domainByKey(key: string): SettingDomain | undefined {
  return DOMAINS.find((d) => d.key === key)
}

/** 设置值：域 key → 字段 key → 值 */
export type SettingsState = Record<string, Record<string, string | number | boolean>>

export const STORAGE_KEY = 'stardust.settings'

/** 出厂默认（由 schema 的 def 派生） */
export function defaultSettings(): SettingsState {
  const out: SettingsState = {}
  for (const d of DOMAINS) {
    const bag: Record<string, string | number | boolean> = {}
    for (const f of d.fields) bag[f.key] = f.def
    out[d.key] = bag
  }
  return out
}

/** 读取本地设置（容错：缺域/缺字段用默认补齐） */
export function loadSettings(): SettingsState {
  const base = defaultSettings()
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return base
    const saved = JSON.parse(raw) as SettingsState
    for (const d of DOMAINS) {
      const bag = saved?.[d.key]
      if (!bag || typeof bag !== 'object') continue
      for (const f of d.fields) {
        const v = (bag as Record<string, unknown>)[f.key]
        if (v !== undefined && v !== null) base[d.key][f.key] = v as string | number | boolean
      }
    }
    return base
  } catch {
    return base
  }
}

/** 保存设置到本地 */
export function saveSettings(s: SettingsState): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(s))
}

/** 将单个域恢复为出厂默认（原地修改） */
export function resetDomain(s: SettingsState, key: string): void {
  const d = domainByKey(key)
  if (!d) return
  const bag: Record<string, string | number | boolean> = {}
  for (const f of d.fields) bag[f.key] = f.def
  s[key] = bag
}

/** 分段单选项是否用等宽字体（设计稿：以 [a-z0-9-] 开头的标识符用 mono） */
export function isMonoOption(option: string): boolean {
  return /^[a-z0-9-]/i.test(option)
}
