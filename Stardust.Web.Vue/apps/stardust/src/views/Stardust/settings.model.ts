// 设置页纯逻辑（与 UI 解耦）：表单分组配置 + 本地存取
// TODO: 设置页暂无对应 MVC/WebApi 真源（NodeController 不存在 GetSetting/SaveSetting 公开接口），
//       暂用本地存储保证页面可用；待确认后端配置来源（如 Configs/AppConfig 键值时）再对接真实接口。

export interface SettingsState {
  clusterName: string
  nodeName: string
  notifyError: boolean
  notifyThreshold: number
  notifyChannel: string
  autoApprove: boolean
  ipWhitelist: string
  auditLog: boolean
  theme: string
  dashboardLayout: string
  compact: boolean
}

export const defaultSettings: SettingsState = {
  clusterName: '默认集群',
  nodeName: '',
  notifyError: true,
  notifyThreshold: 80,
  notifyChannel: '企业微信',
  autoApprove: false,
  ipWhitelist: '',
  auditLog: true,
  theme: 'dark',
  dashboardLayout: 'grid',
  compact: false,
}

export interface SettingItem {
  type: 'input' | 'switch' | 'slider' | 'select'
  prop: keyof SettingsState
  label: string
  placeholder?: string
  options?: string[]
}

export interface SettingGroup {
  key: string
  title: string
  desc: string
  items: SettingItem[]
}

/** 四卡片分组配置（对照 Doc/preview_settings.png 结构） */
export function settingGroups(): SettingGroup[] {
  return [
    { key: 'basic', title: '基础信息', desc: '集群与节点标识', items: [
      { type: 'input', prop: 'clusterName', label: '集群名称' },
      { type: 'input', prop: 'nodeName', label: '当前节点名称', placeholder: '自动获取' },
    ] },
    { key: 'notify', title: '通知设置', desc: '告警通道与阈值', items: [
      { type: 'switch', prop: 'notifyError', label: '错误告警' },
      { type: 'slider', prop: 'notifyThreshold', label: '告警阈值 (%)' },
      { type: 'select', prop: 'notifyChannel', label: '通知渠道', options: ['企业微信', '钉钉', '邮件', 'Webhook'] },
    ] },
    { key: 'security', title: '安全设置', desc: '审批与审计', items: [
      { type: 'switch', prop: 'autoApprove', label: '自动审批发布' },
      { type: 'input', prop: 'ipWhitelist', label: 'IP 白名单', placeholder: '多个以逗号分隔' },
      { type: 'switch', prop: 'auditLog', label: '操作审计日志' },
    ] },
    { key: 'appearance', title: '外观设置', desc: '主题与布局', items: [
      { type: 'select', prop: 'theme', label: '主题', options: ['dark', 'light'] },
      { type: 'select', prop: 'dashboardLayout', label: '仪表盘布局', options: ['grid', 'list'] },
      { type: 'switch', prop: 'compact', label: '紧凑模式' },
    ] },
  ]
}

export const STORAGE_KEY = 'stardust.settings'

/** 读取本地设置（容错，失败回退默认） */
export function loadSettings(): SettingsState {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    return saved ? { ...defaultSettings, ...JSON.parse(saved) } : { ...defaultSettings }
  } catch {
    return { ...defaultSettings }
  }
}

/** 保存设置到本地 */
export function saveSettings(s: SettingsState): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(s))
}
