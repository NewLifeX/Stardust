using System.Collections.Generic;
using System.ComponentModel;
using NewLife;
using NewLife.Cube;
using NewLife.Reflection;
using NewLife.Web;
using Stardust.Data.Platform;
using XCode;

namespace Stardust.Web.Areas;

/// <summary>项目数据范围实体控制器基类。在 Cube 控制器管道上统一应用项目过滤</summary>
/// <remarks>
/// 列表查询把项目条件写入 <c>PageParameter.State</c>，单条读取在 <c>FindData</c> 之后校验可见性，
/// 写入由 <see cref="Valid"/> 校验并补全项目编号。数据层实体方法不做任何项目过滤。
/// 不走 <c>CreateWhere</c>：Cube 的 <c>WhereBuilder.Eval</c> 对 ITenantScope 实体强制要求租户上下文，
/// 未启用多租户时会把正常单条读取误判为非法访问。
/// </remarks>
/// <typeparam name="T">实体类型</typeparam>
public class ProjectScopedEntityController<T> : EntityController<T> where T : Entity<T>, new()
{
    /// <summary>搜索数据。把项目范围并入分页查询状态</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<T> SearchData(Pager p)
    {
        var scope = ProjectScopeContext.Current;
        if (scope == null || scope.IsAdmin) return base.SearchData(p);

        var inner = base.CreateWhere();
        if (inner != null) inner.Data2 ??= p.Items;

        var builder = new ProjectScopeWhereBuilder { Factory = Factory, Scope = scope, Inner = inner };
        builder.Data2 ??= p.Items;
        p.State = builder;

        if (PageSetting.OrderByKey && p.Sort.IsNullOrEmpty() && p.OrderBy.IsNullOrEmpty())
        {
            var uk = Factory.Unique;
            if (uk != null && uk.Type.IsInt()) p.OrderBy = uk.Desc();
        }

        return Search(p);
    }

    /// <summary>查找单行数据，并校验项目可见性</summary>
    /// <param name="key">主键</param>
    /// <returns>实体对象</returns>
    protected override T FindData(Object key)
    {
        var entity = base.FindData(key);
        if (entity == null) return null;

        var scope = ProjectScopeContext.Current;
        if (scope == null || scope.IsAdmin) return entity;

        if (!ProjectScopeRules.CanAccess(entity, scope)) throw new InvalidOperationException(L.T("Illegal data access [{0}]", key));

        return entity;
    }

    /// <summary>验证实体对象</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="type">操作类型</param>
    /// <param name="post">是否提交数据阶段</param>
    /// <returns>是否通过</returns>
    protected override Boolean Valid(T entity, DataObjectMethodType type, Boolean post)
    {
        if (!base.Valid(entity, type, post)) return false;

        if (post)
        {
            DataMethod? method = type switch
            {
                DataObjectMethodType.Insert => DataMethod.Insert,
                DataObjectMethodType.Update => DataMethod.Update,
                DataObjectMethodType.Delete => DataMethod.Delete,
                _ => null,
            };
            if (method != null) ValidateProjectScope(entity, method.Value);
        }

        return true;
    }

    /// <summary>校验实体是否属于当前项目范围，并在新增时补全项目编号</summary>
    /// <param name="entity">实体对象</param>
    /// <param name="method">数据操作方法</param>
    protected void ValidateProjectScope(T entity, DataMethod method)
    {
        var scope = ProjectScopeContext.Current;
        if (scope == null || scope.IsAdmin) return;

        ProjectScopeRules.Validate(entity, method, scope);
    }
}

/// <summary>项目数据范围只读实体控制器基类</summary>
/// <typeparam name="T">实体类型</typeparam>
public class ProjectScopedReadOnlyEntityController<T> : ReadOnlyEntityController<T> where T : Entity<T>, new()
{
    /// <summary>搜索数据。把项目范围并入分页查询状态</summary>
    /// <param name="p">分页参数</param>
    /// <returns>实体列表</returns>
    protected override IEnumerable<T> SearchData(Pager p)
    {
        var scope = ProjectScopeContext.Current;
        if (scope == null || scope.IsAdmin) return base.SearchData(p);

        var inner = base.CreateWhere();
        if (inner != null) inner.Data2 ??= p.Items;

        var builder = new ProjectScopeWhereBuilder { Factory = Factory, Scope = scope, Inner = inner };
        builder.Data2 ??= p.Items;
        p.State = builder;

        if (PageSetting.OrderByKey && p.Sort.IsNullOrEmpty() && p.OrderBy.IsNullOrEmpty())
        {
            var uk = Factory.Unique;
            if (uk != null && uk.Type.IsInt()) p.OrderBy = uk.Desc();
        }

        return Search(p);
    }

    /// <summary>查找单行数据，并校验项目可见性</summary>
    /// <param name="key">主键</param>
    /// <returns>实体对象</returns>
    protected override T FindData(Object key)
    {
        var entity = base.FindData(key);
        if (entity == null) return null;

        var scope = ProjectScopeContext.Current;
        if (scope == null || scope.IsAdmin) return entity;

        if (!ProjectScopeRules.CanAccess(entity, scope)) throw new InvalidOperationException(L.T("Illegal data access [{0}]", key));

        return entity;
    }
}