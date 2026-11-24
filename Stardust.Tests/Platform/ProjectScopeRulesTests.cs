using System;
using System.Linq;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using XCode;
using Xunit;

namespace Stardust.Tests.Platform;

/// <summary>项目范围实体分类防漏测试。新增星尘实体必须显式归入项目/父级/全局三类之一</summary>
public class ProjectScopeRulesTests
{
    [Fact(DisplayName = "所有星尘实体均已归入项目隔离分类")]
    public void AllEntitiesClassified()
    {
        var assembly = typeof(App).Assembly;
        var types = assembly.GetTypes()
            .Where(e => e.IsClass && !e.IsAbstract && typeof(IEntity).IsAssignableFrom(e))
            .Where(e => e.Namespace != null && (e.Namespace == "Stardust.Data" || e.Namespace.StartsWith("Stardust.Data.")))
            .ToArray();

        Assert.NotEmpty(types);

        var unknown = types.Where(e => ProjectScopeRules.GetMode(e) == ProjectScopeMode.Unknown).ToArray();

        Assert.True(unknown.Length == 0, "存在未分类实体，请加入项目隔离分类：" + String.Join(",", unknown.Select(e => e.FullName)));
    }

    [Fact(DisplayName = "代表性实体分类符合预期")]
    public void RepresentativeModes()
    {
        Assert.Equal(ProjectScopeMode.Project, ProjectScopeRules.GetMode(typeof(App)));
        Assert.Equal(ProjectScopeMode.Project, ProjectScopeRules.GetMode(typeof(Node)));
        Assert.Equal(ProjectScopeMode.Project, ProjectScopeRules.GetMode(typeof(ProjectUser)));
        Assert.Equal(ProjectScopeMode.ProjectSelf, ProjectScopeRules.GetMode(typeof(GalaxyProject)));
        Assert.Equal(ProjectScopeMode.AppId, ProjectScopeRules.GetMode(typeof(AppService)));
        Assert.Equal(ProjectScopeMode.NodeId, ProjectScopeRules.GetMode(typeof(NodeData)));
        Assert.Equal(ProjectScopeMode.Parent, ProjectScopeRules.GetMode(typeof(ConfigData)));
        Assert.Equal(ProjectScopeMode.Global, ProjectScopeRules.GetMode(typeof(ProductRelease)));
    }
}
