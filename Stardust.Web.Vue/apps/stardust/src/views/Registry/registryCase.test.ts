import { describe, expect, it } from 'vitest';
import { cellText, columnLabel, fillTemplate, pickField } from './registryCase';

/**
 * CARD-FIX-REG-01 错位复现用例（B 附则扩列）。
 *
 * 后端序列化为小驼峰，迁移页按 PascalCase 键名读值 → 两域七处全部落空：
 * - 服务域：fillTemplate（三链接列 href）/ cellText（五列行数据）/ 列头模板串
 * - 应用域：① selection.map(r => r.Id) 恒 []；② AppSearch 读 e.Name/e.Id 恒空；
 *           ③ fetchCategories 读 r.Category 恒空；④ detail 六标签读 PascalCase 恒「-」
 * 用例按「现状行为 → 期望行为」组织：先以直取骨架复现错位（红），回退实现落地后转绿。
 */

describe('pickField — camelCase 行数据 × PascalCase 键名错位兜底', () => {
  it('直取命中：行数据含同形键时优先直取', () => {
    expect(pickField({ Id: 5, id: 6 }, 'Id')).toBe(5);
    expect(pickField({ id: 6 }, 'id')).toBe(6);
  });

  it('未命中回退首字母小写键（两域错位的唯一修点）', () => {
    expect(pickField({ id: 11 }, 'Id')).toBe(11); // 应用域① selection / ④ detail.Id
    expect(pickField({ serviceName: 'StarAgent' }, 'ServiceName')).toBe('StarAgent'); // 服务域 cellText
    expect(pickField({ displayName: '星尘应用' }, 'DisplayName')).toBe('星尘应用'); // 应用域② AppSearch
    expect(pickField({ projectId: 7 }, 'ProjectId')).toBe(7); // 应用域④ detail.ProjectId
    expect(pickField({ nodeUrl: '/x' }, 'NodeUrl')).toBe('/x');
  });

  it('未命中且无回退键 → undefined；null/undefined 行安全', () => {
    expect(pickField({ id: 1 }, 'Name')).toBeUndefined();
    expect(pickField(null, 'Id')).toBeUndefined();
    expect(pickField(undefined, 'Id')).toBeUndefined();
  });

  it('假值照取：只在键缺失时回退，不做 truthy 过滤', () => {
    expect(pickField({ enable: false }, 'Enable')).toBe(false);
    expect(pickField({ name: '' }, 'Name')).toBe('');
    expect(pickField({ id: 0 }, 'Id')).toBe(0);
  });
});

describe('cellText — 服务域五列行数据（错位时渲染空串）', () => {
  it('PascalCase 键在 camelCase 行上取到实值', () => {
    expect(cellText({ serviceName: 'StarAgent' }, 'ServiceName')).toBe('StarAgent');
    expect(cellText({ client: 'cc' }, 'Client')).toBe('cc');
  });

  it('取值为 null/undefined → 空串', () => {
    expect(cellText({ serviceName: null }, 'ServiceName')).toBe('');
    expect(cellText({}, 'ServiceName')).toBe('');
    expect(cellText(null, 'ServiceName')).toBe('');
  });
});

describe('fillTemplate — 服务域三链接列 href（错位时占位渲染空串）', () => {
  it('{ServiceName} 占位取到实值', () => {
    expect(fillTemplate('/Registry/Service?name={ServiceName}', { id: 1, serviceName: 'StarAgent' }))
      .toBe('/Registry/Service?name=StarAgent');
  });

  it('多占位同时替换（client 列双参模板）', () => {
    expect(fillTemplate('/Registry/AppService?appId={AppId}&client={Client}', { appId: 9, client: 'cc' }))
      .toBe('/Registry/AppService?appId=9&client=cc');
  });

  it('节点列模板 + 未命中占位渲染空串（与旧行为一致）', () => {
    expect(fillTemplate('/Nodes/Node?Id={NodeId}', { nodeId: 33 })).toBe('/Nodes/Node?Id=33');
    expect(fillTemplate('/Nodes/Node?Id={NodeId}', {})).toBe('/Nodes/Node?Id=');
  });
});

describe('columnLabel — 列头模板串兜底（服务域列头字面 {ServiceName} 缺陷）', () => {
  it('displayName 是模板串（含 {}）时回退 header', () => {
    expect(columnLabel('{ServiceName}', '服务', 'ServiceName')).toBe('服务');
    expect(columnLabel('{NodeName}', '节点', 'NodeName')).toBe('节点');
  });

  it('header 缺省（空/undefined）时回退字段名', () => {
    expect(columnLabel('{ServiceName}', '', 'ServiceName')).toBe('ServiceName');
    expect(columnLabel('{ServiceName}', undefined, 'ServiceName')).toBe('ServiceName');
  });

  it('正常 displayName 原样用（header 存在也不覆盖）', () => {
    expect(columnLabel('客户端', '客户端', 'Client')).toBe('客户端');
  });

  it('displayName 为空时回退 header，再回退字段名', () => {
    expect(columnLabel('', '服务', 'ServiceName')).toBe('服务');
    expect(columnLabel(undefined, undefined, 'Client')).toBe('Client');
  });
});

describe('应用域四处代表性取值（接线由页面完成，纯函数层锁行为）', () => {
  const appRow = { id: 11, name: 'reg01-走查应用', displayName: '走查应用', category: '测试', enable: true, projectId: 1 };

  it('① selection：r.Id → 11（SetAlarm 参数包）', () => {
    expect([appRow].map((r) => pickField(r, 'Id') as number).filter((id) => id != null)).toEqual([11]);
  });

  it('② AppSearch：e.Name/e.DisplayName/e.Id 全命中', () => {
    expect(pickField(appRow, 'Name')).toBe('reg01-走查应用');
    expect(pickField(appRow, 'DisplayName')).toBe('走查应用');
    expect(pickField(appRow, 'Id')).toBe(11);
  });

  it('③ fetchCategories：r.Category → 类别值', () => {
    expect(pickField(appRow, 'Category')).toBe('测试');
  });

  it('④ detail 六标签键全命中（Id/Name/DisplayName/Category/Enable/ProjectId）', () => {
    expect(pickField(appRow, 'Id')).toBe(11);
    expect(pickField(appRow, 'Name')).toBe('reg01-走查应用');
    expect(pickField(appRow, 'DisplayName')).toBe('走查应用');
    expect(pickField(appRow, 'Category')).toBe('测试');
    expect(pickField(appRow, 'Enable')).toBe(true);
    expect(pickField(appRow, 'ProjectId')).toBe(1);
  });
});

/**
 * DEF-1 族第二成员：/Registry/Service 旧壳迁移页（REG-09 走查 2026-11-10，j-config wave4 排程令）。
 *
 * 缺陷实证（修复前走查帧 reg09b-09，如实为红）：①13 个数据列单元格 0 非空（本页 cellText
 * Pascal 直读 camelCase 行数据）；②Providers/Consumers 两链接列 href 仅裸路径（row.Id 未命中
 * → toUrl 省参，缺 ?serviceId={Id}）；③链接计数文本同落空。修法=接线共享 registryCase
 * （pickField/cellText/columnLabel），与应用域/服务域第一成员同根同源；本 describe 锚定
 * Service 页接线语义（纯函数层），页面绑定由走查复验帧+CT 回归锁（无 mount 先例，同 5.4-⑤）。
 */
describe('服务域 /Registry/Service 页（DEF-1 族第二成员：行值 13 单元 + links href + selection）', () => {
  const serviceRow = {
    id: 12,
    name: 'reg09-c17-check',
    displayName: 'C17 复验',
    category: '走查',
    enable: true,
    singleton: true,
    useScope: false,
    address: 'http://{node}{service}',
    healthCheck: true,
    projectName: '默认项目',
    providers: 1,
    consumers: 2,
  };

  it('13 数据列代表键全命中（本页 cellText 接线后应取到实值）', () => {
    expect(cellText(serviceRow, 'Id')).toBe('12');
    expect(cellText(serviceRow, 'Name')).toBe('reg09-c17-check');
    expect(cellText(serviceRow, 'DisplayName')).toBe('C17 复验');
    expect(cellText(serviceRow, 'Category')).toBe('走查');
    expect(cellText(serviceRow, 'Enable')).toBe('true');
    expect(cellText(serviceRow, 'Singleton')).toBe('true');
    expect(cellText(serviceRow, 'ProjectName')).toBe('默认项目');
    expect(cellText(serviceRow, 'Address')).toBe('http://{node}{service}');
  });

  it('Providers/Consumers 链接列：计数文本 + href 的 serviceId 参数实值（C12-links 修复语义）', () => {
    expect(cellText(serviceRow, 'Providers')).toBe('1');
    expect(cellText(serviceRow, 'Consumers')).toBe('2');
    // 页面 providersUrl/consumersUrl 以 pickField(row,'Id') 取 serviceId（toUrl 省参修复点）
    expect(pickField(serviceRow, 'Id')).toBe(12);
  });

  it('selection：r.Id → 12（本页批量选择同 defect-B ① 修法）', () => {
    expect([serviceRow].map((r) => pickField(r, 'Id') as number).filter((id) => id != null)).toEqual([12]);
  });
});
