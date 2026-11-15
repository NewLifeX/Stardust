/**
 * 迁移页字段取值纯函数（CARD-FIX-REG-01 单点）。
 *
 * 背景：后端 Cube API 把匿名对象/实体序列化为小驼峰（id/name/displayName/serviceName/...），
 * 而 AppService / App 迁移页沿用 MVC 面的 PascalCase 键名读值（row.Id / {ServiceName} 模板），
 * 导致行数据全部落空。本文件提供唯一的「camelCase 回退」实现，两页共用，禁止各自复制逻辑。
 *
 * 规则（按卡面裁定，不扩面）：
 * - pickField：先直取 `row[key]`；未命中再试首字母小写键。不做全局大小写不敏感映射。
 * - fillTemplate：`{Name}` 占位替换，占位键走 pickField（未命中渲染空串，与旧行为一致）。
 * - cellText：取值转字符串，null/undefined 渲染空串（与旧行为一致）。
 * - columnLabel：列头兜底——displayName 是模板串（含 `{` 或 `}`）或为空时回退 header，再回退字段名。
 */

/** 直取优先、未命中回退首字母小写键的行取值。 */
export function pickField(row: unknown, key: string): unknown {
 if (row == null) return undefined;
  const record = row as Record<string, unknown>;
  if (key in record) return record[key];

  const lower = key.length > 0 ? key.charAt(0).toLowerCase() + key.slice(1) : key;
  return record[lower];
}

/** 单元格文本：取值（含 camelCase 回退）后转字符串，null/undefined 渲染空串。 */
export function cellText(row: unknown, key: string): string {
  const v = pickField(row, key);
  return v == null ? '' : String(v);
}

/** 链接列模板替换：`{Name}` 占位按 pickField 取值，未命中渲染空串。 */
export function fillTemplate(tpl: string, row: unknown): string {
  return tpl.replace(/\{([^}]+)\}/g, (_m, key: string) => cellText(row, key));
}

/** 列头兜底：displayName 是模板串或为空时回退 header，再回退字段名。 */
export function columnLabel(displayName: string | undefined, header: string | undefined, fallbackName: string): string {
  if (displayName && !displayName.includes('{') && !displayName.includes('}')) return displayName;
  if (header && !header.includes('{') && !header.includes('}')) return header;
  return fallbackName;
}
