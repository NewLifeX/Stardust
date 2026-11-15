// AppPipeline 列表单元格取值纯逻辑（2026-11-12 线上走查提取）。
// 212 真机 AppPipeline 序列化为 camelCase（id/name/deployName/branch/...），
// 本页 Row 接口与 MVC ListFields 方言为 PascalCase（Name/DeployName/...）。
// 取值大小写都认：优先原文键，缺则首字母小写兜底；两方言同源可见（对齐 ship.logic.ts pick 先例）。

type Rec = Record<string, unknown>;

/** 行内取字段：PascalCase 与 camelCase 都认（原键优先，camel 兜底） */
export function pickField(row: Rec | undefined | null, key: string): unknown {
  if (!row) return undefined;
  const v = row[key];
  if (v !== undefined && v !== null) return v;
  const lk = key.charAt(0).toLowerCase() + key.slice(1);
  return row[lk];
}

/** 单元格文本：Enable 布尔→是/否；UpdateTime ISO→'YYYY-MM-DD HH:mm:ss'；缺值→空串 */
export function cellText(row: Rec | undefined | null, key: string): string {
  const v = pickField(row, key);
  if (v == null) return '';
  if (key === 'Enable' || key === 'AutoDeploy') {
    const b = typeof v === 'boolean' ? v : Number(v) !== 0 || String(v).toLowerCase() === 'true';
    return b ? '是' : '否';
  }
  if (key === 'UpdateTime') return String(v).replace('T', ' ').slice(0, 19);
  return String(v);
}
