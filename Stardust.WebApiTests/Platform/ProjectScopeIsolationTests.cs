using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Stardust.Data;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.WebApiTests.Fixtures;
using XCode;
using XCode.Membership;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Platform;

/// <summary>项目数据隔离集成测试。覆盖控制器数据权限的列表、详情、写入与缓存查找防护</summary>
[Collection(WebApiCollection.Name)]
public class ProjectScopeIsolationTests
{
    private static readonly Lazy<Seed> _seed = new(() => new Seed());

    private readonly WebApiTestFactory _factory;

    public ProjectScopeIsolationTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;

        // 启动主机，确保成员体系表已建立
        using var client = factory.CreateClient();

        _ = _seed.Value;
    }

    private sealed class Seed
    {
        public readonly GalaxyProject ProjectA;
        public readonly GalaxyProject ProjectB;
        public readonly Node NodeA;
        public readonly Node NodeB;
        public readonly App AppA;
        public readonly App AppB;
        public readonly AppService AppServiceA;
        public readonly AppHistory AppHistoryB;
        public readonly NodeData NodeDataA;
        public readonly NodeCommand NodeCommandB;
        public readonly XCode.Membership.User UserA;
        public readonly XCode.Membership.User UserEmpty;

        public Seed()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];

            ProjectA = new GalaxyProject { Name = "ScopeA_" + suffix, Enable = true };
            ProjectA.Insert();
            ProjectB = new GalaxyProject { Name = "ScopeB_" + suffix, Enable = true };
            ProjectB.Insert();

            NodeA = new Node { Name = "ScopeNodeA_" + suffix, ProjectId = ProjectA.Id, Enable = true };
            NodeA.Insert();
            NodeB = new Node { Name = "ScopeNodeB_" + suffix, ProjectId = ProjectB.Id, Enable = true };
            NodeB.Insert();

            AppA = new App { Name = "ScopeAppA_" + suffix, DisplayName = "ScopeAppA_" + suffix, ProjectId = ProjectA.Id, Enable = true };
            AppA.Insert();
            AppB = new App { Name = "ScopeAppB_" + suffix, DisplayName = "ScopeAppB_" + suffix, ProjectId = ProjectB.Id, Enable = true };
            AppB.Insert();

            AppServiceA = new AppService { AppId = AppA.Id, ServiceId = 1, Client = "scope", Enable = true };
            AppServiceA.Insert();
            AppHistoryB = new AppHistory { AppId = AppB.Id, Client = "scope", Action = "Test", Creator = "test" };
            AppHistoryB.Insert();

            NodeDataA = new NodeData { NodeID = NodeA.ID, Name = NodeA.Name, Creator = "test", CreateTime = DateTime.Now };
            NodeDataA.Insert();
            NodeCommandB = new NodeCommand { NodeID = NodeB.ID, Command = "scope-test", CreateUser = "test", CreateTime = DateTime.Now };
            NodeCommandB.Insert();

            UserA = new XCode.Membership.User { Name = "scopeA_" + suffix, Password = "test", Enable = true };
            UserA.Insert();
            new ProjectUser { ProjectId = ProjectA.Id, UserId = UserA.Id, Enable = true }.Insert();

            UserEmpty = new XCode.Membership.User { Name = "scopeEmpty_" + suffix, Password = "test", Enable = true };
            UserEmpty.Insert();
        }
    }

    private static void Use(Int32 userId) => ProjectScopeContext.Current = ProjectScope.Resolve(userId);

    private static void Clear() => ProjectScopeContext.Current = null;

    [Fact(DisplayName = "实体方法保持原样，Web 层缓存查找统一补项目可见性校验")]
    public void CachedLookup_UsesProjectGuard()
    {
        Use(_seed.Value.UserA.Id);
        try
        {
            // 数据层实体方法不做项目过滤，服务端数据面与后台任务依赖该语义
            Assert.NotNull(Node.FindByID(_seed.Value.NodeB.ID));
            Assert.Contains(Node.FindAll(new WhereExpression()), e => e.ID == _seed.Value.NodeB.ID);

            // Web 层用 FilterVisible 补缓存查找的可见性校验
            Assert.Null(ProjectScopeRules.FilterVisible(Node.FindByID(_seed.Value.NodeB.ID)));
            Assert.NotNull(ProjectScopeRules.FilterVisible(Node.FindByID(_seed.Value.NodeA.ID)));
        }
        finally
        {
            Clear();
        }
    }

    [Fact(DisplayName = "项目范围查询条件构建器过滤列表并校验单条")]
    public void WhereBuilder_AppliesProjectScope()
    {
        var scope = ProjectScope.Resolve(_seed.Value.UserA.Id);
        Assert.False(scope.IsAdmin);

        var builder = new ProjectScopeWhereBuilder { Factory = Node.Meta.Factory, Scope = scope };
        var exp = builder.GetExpression();
        Assert.False(exp.IsEmpty);
        Assert.True(ProjectScopeRules.CanAccess(_seed.Value.NodeA, scope));
        Assert.False(ProjectScopeRules.CanAccess(_seed.Value.NodeB, scope));

        var admin = new ProjectScopeWhereBuilder { Factory = Node.Meta.Factory, Scope = new ProjectScope { IsAdmin = true } };
        Assert.True(admin.GetExpression().IsEmpty);
    }

    [Fact(DisplayName = "普通用户不能写入其它项目的数据")]
    public void NonAdmin_CannotWriteForeignProject()
    {
        Use(_seed.Value.UserA.Id);
        try
        {
            var scope = ProjectScopeContext.Current;

            var node = new Node { Name = "ScopeForbidden_" + Guid.NewGuid().ToString("N")[..8], ProjectId = _seed.Value.ProjectB.Id, Enable = true };
            Assert.Throws<InvalidOperationException>(() => ProjectScopeRules.Validate(node, DataMethod.Insert, scope));

            // 给他人应用的配置不允许挂到自己项目下
            var cfg = new Data.Configs.AppConfig { Name = "ScopeForbiddenCfg_" + Guid.NewGuid().ToString("N")[..8], ProjectId = _seed.Value.ProjectA.Id, AppId = _seed.Value.AppB.Id, Enable = true };
            Assert.Throws<InvalidOperationException>(() => ProjectScopeRules.Validate(cfg, DataMethod.Insert, scope));

            // 向他人的节点下发命令
            var cmd = new NodeCommand { NodeID = _seed.Value.NodeB.ID, Command = "scope-forbidden" };
            Assert.Throws<InvalidOperationException>(() => ProjectScopeRules.Validate(cmd, DataMethod.Insert, scope));
        }
        finally
        {
            Clear();
        }
    }

    [Fact(DisplayName = "系统管理员不受项目范围限制")]
    public async Task Admin_SeesAll()
    {
        using var client = await _factory.CreateAuthenticatedClient();

        var admin = XCode.Membership.User.FindByName(WebApiTestFactory.DefaultUserName);
        Assert.NotNull(admin);

        var scope = ProjectScope.Resolve(admin.Id);
        Assert.True(scope.IsAdmin);

        ProjectScopeContext.Current = scope;
        try
        {
            Assert.NotNull(ProjectScopeRules.FilterVisible(Node.FindByID(_seed.Value.NodeB.ID)));
            Assert.True(new ProjectScopeWhereBuilder { Factory = Node.Meta.Factory, Scope = scope }.GetExpression().IsEmpty);
        }
        finally
        {
            Clear();
        }
    }

    [Fact(DisplayName = "非管理员通过 HTTP 只能看到自己项目")]
    public async Task NonAdmin_HttpOnlySeesOwnProject()
    {
        var (client, user) = await CreateScopedClientAsync(_seed.Value.ProjectA.Id);
        using var _client = client;

        var resp = await client.GetAsync("/api/Platform/GalaxyProject?PageSize=500");
        var body = await resp.Content.ReadAsStringAsync();
        Assert.True(IsOk(resp, body), $"HTTP {(Int32)resp.StatusCode} {body}");

        using var doc = JsonDocument.Parse(body);
        var names = doc.RootElement.GetProperty("data").EnumerateArray().Select(GetName).ToArray();
        Assert.Contains(_seed.Value.ProjectA.Name, names);
        Assert.DoesNotContain(_seed.Value.ProjectB.Name, names);
    }

    [Fact(DisplayName = "非管理员通过 HTTP 打开他人项目详情被拒绝")]
    public async Task NonAdmin_HttpDetailRejectsForeignProject()
    {
        var (client, user) = await CreateScopedClientAsync(_seed.Value.ProjectA.Id);
        using var _client = client;

        var scope = ProjectScope.Resolve(user.Id);
        Assert.Contains(_seed.Value.ProjectA.Id, scope.ProjectIds);

        var own = await client.GetAsync($"/api/Platform/GalaxyProject/{_seed.Value.ProjectA.Id}");
        var ownBody = await own.Content.ReadAsStringAsync();
        Assert.True(IsOk(own, ownBody), $"HTTP {(Int32)own.StatusCode} {ownBody}");

        var foreign = await client.GetAsync($"/api/Platform/GalaxyProject/{_seed.Value.ProjectB.Id}");
        var foreignBody = await foreign.Content.ReadAsStringAsync();
        Assert.False(IsOk(foreign, foreignBody), $"跨项目详情不应成功：HTTP {(Int32)foreign.StatusCode} {foreignBody}");
    }

    [Fact(DisplayName = "非管理员通过 HTTP 不能修改或删除他人项目")]
    public async Task NonAdmin_HttpWriteRejectsForeignProject()
    {
        var (client, user) = await CreateScopedClientAsync(_seed.Value.ProjectA.Id);
        using var _client = client;

        using var content = new StringContent("{\"name\":\"scope-hacked\"}", Encoding.UTF8, "application/json");
        var update = await client.PutAsync($"/api/Platform/GalaxyProject/{_seed.Value.ProjectB.Id}", content);
        var updateBody = await update.Content.ReadAsStringAsync();
        Assert.False(IsOk(update, updateBody), $"跨项目修改不应成功：HTTP {(Int32)update.StatusCode} {updateBody}");

        var delete = await client.DeleteAsync($"/api/Platform/GalaxyProject/{_seed.Value.ProjectB.Id}");
        var deleteBody = await delete.Content.ReadAsStringAsync();
        Assert.False(IsOk(delete, deleteBody), $"跨项目删除不应成功：HTTP {(Int32)delete.StatusCode} {deleteBody}");
    }

    [Fact(DisplayName = "非管理员通过 HTTP 不能给他人项目新增数据")]
    public async Task NonAdmin_HttpInsertRejectsForeignProject()
    {
        var (client, user) = await CreateScopedClientAsync(_seed.Value.ProjectA.Id);
        using var _client = client;

        var json = $"{{\"name\":\"ScopeForbiddenNode_{Guid.NewGuid().ToString("N")[..8]}\",\"projectId\":{_seed.Value.ProjectB.Id},\"enable\":true}}";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("/api/Nodes/Node", content);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.False(IsOk(resp, body), $"跨项目新增不应成功：HTTP {(Int32)resp.StatusCode} {body}");
    }

    private async Task<(HttpClient Client, XCode.Membership.User User)> CreateScopedClientAsync(Int32 projectId)
    {
        // 先完成一次管理员登录，等待菜单与系统角色授权落库
        using var adminClient = await _factory.CreateAuthenticatedClient();

        var suffix = Guid.NewGuid().ToString("N")[..8];

        // 复制系统角色的菜单权限，保证业务接口可访问；项目隔离由控制器数据权限负责
        var role = new Role { Name = "ScopeRole_" + suffix, Enable = true, IsSystem = false };
        role.Insert();
        foreach (var menu in Menu.FindAll())
        {
            role.Set(menu.ID, PermissionFlags.All);
        }
        role.Save();

        var password = "scope_test";
        var user = new XCode.Membership.User
        {
            Name = "scopeb_" + suffix,
            DisplayName = "scopeb",
            Password = ManageProvider.Provider.PasswordProvider.Hash(password),
            RoleId = role.Id,
            RoleIds = role.Id + "",
            Enable = true,
        };
        user.Insert();
        new ProjectUser { ProjectId = projectId, UserId = user.Id, Enable = true }.Insert();

        var token = await _factory.LoginAsync(user.Name, password);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, user);
    }

    private static Boolean IsOk(HttpResponseMessage resp, String body)
    {
        if (!resp.IsSuccessStatusCode) return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("code", out var code) && code.GetInt32() == 0;
        }
        catch
        {
            return false;
        }
    }

    private static String GetName(JsonElement item)
    {
        if (item.TryGetProperty("name", out var name)) return name.GetString();
        if (item.TryGetProperty("Name", out name)) return name.GetString();

        return null;
    }
}