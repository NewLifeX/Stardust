using System;
using XCode;
using XCode.Model;

namespace Stardust.Data.Platform;

/// <summary>项目范围查询条件构建器。把当前用户的项目可见范围接入 Cube 列表查询</summary>
/// <remarks>
/// Cube 的列表查询通过 <c>PageParameter.State</c> 应用数据权限，本类把项目范围规则接到该入口；
/// 单条读取由控制器 <c>FindData</c> 直接调用 <see cref="ProjectScopeRules.CanAccess"/>。
/// 数据层实体方法保持原样。
/// </remarks>
public class ProjectScopeWhereBuilder : WhereBuilder
{
    #region 属性
    /// <summary>项目范围</summary>
    public ProjectScope Scope { get; set; }

    /// <summary>上层数据权限构建器。租户、部门等既有条件继续生效</summary>
    public WhereBuilder Inner { get; set; }
    #endregion

    #region 构造
    /// <summary>实例化</summary>
    public ProjectScopeWhereBuilder() => Expression = nameof(ProjectScopeWhereBuilder);
    #endregion

    #region 表达式
    /// <summary>解析为查询条件</summary>
    /// <param name="value">表达式文本。项目范围不使用文本表达式，此参数被忽略</param>
    /// <returns>查询条件</returns>
    protected override Expression ParseExpression(String value)
    {
        var exp = ProjectScopeRules.BuildQueryFilter(Factory, Scope);

        var inner = Inner?.GetExpression();
        if (inner != null && !inner.IsEmpty) exp = exp == null || exp.IsEmpty ? inner : exp & inner;

        return exp ?? new WhereExpression();
    }
    #endregion
}
