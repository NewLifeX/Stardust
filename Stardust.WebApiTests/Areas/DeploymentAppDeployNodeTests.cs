using System.Text.Json;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.WebApiTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Stardust.WebApiTests.Areas;

// Deployment AppDeployNode query and count-fix regressions.
// appId branch: controller resolves the app's deploy sets then array In query; no-set app returns 0 rows; deployId single-value unchanged.
// Deploy-set Nodes field: entity-layer Insert/Update/Delete overrides keep the enabled-node count in sync, covering the Cube DeleteSelect batch path.
[Collection(WebApiCollection.Name)]
public class DeploymentAppDeployNodeTests
{
    private static readonly Lazy<Seed> _seed = new(() => new Seed());

    private readonly ApiClient _api;

    public DeploymentAppDeployNodeTests(WebApiTestFactory factory, ITestOutputHelper output)
    {
        _api = new(factory, output);
        _ = _seed.Value;
    }

    private sealed class Seed
    {
        public readonly Node Node1;
        public readonly Node Node2;
        public readonly App App1;
        public readonly App App2;
        public readonly App App9;
        public readonly AppDeploy Set1;
        public readonly AppDeploy Set2;
        public readonly AppDeploy Set3;

        public Seed()
        {
            Node1 = GetOrAddNode("DepNode1");
            Node2 = GetOrAddNode("DepNode2");
            App1 = GetOrAddApp("DepApp1");
            App2 = GetOrAddApp("DepApp2");
            App9 = GetOrAddApp("DepApp9");

            // seeded Nodes values match the enabled-count rule, so the fix keeps them stable
            Set1 = GetOrAddSet(App1, "DepSet1", 1); // 2 nodes: 1 enabled 1 disabled
            Set2 = GetOrAddSet(App1, "DepSet2", 1); // 1 node: enabled
            Set3 = GetOrAddSet(App2, "DepSet3", 1); // 1 node: enabled

            AddOrSkipNode(Set1, Node1, true);
            AddOrSkipNode(Set1, Node2, false);
            AddOrSkipNode(Set2, Node1, true);
            AddOrSkipNode(Set3, Node2, true);
        }

        private static Node GetOrAddNode(String name)
        {
            var entity = Node.Find(Node._.Name == name);
            if (entity != null) return entity;

            entity = new Node { Name = name, Enable = true };
            entity.Insert();
            return entity;
        }

        private static App GetOrAddApp(String name)
        {
            var entity = App.Find(App._.Name == name);
            if (entity != null) return entity;

            entity = new App { Name = name, DisplayName = name, Enable = true };
            entity.Insert();
            return entity;
        }

        private static AppDeploy GetOrAddSet(App app, String name, Int32 nodes)
        {
            var entity = AppDeploy.Find(AppDeploy._.Name == name);
            if (entity != null) return entity;

            entity = new AppDeploy { Name = name, AppId = app.Id, Enable = true, Nodes = nodes };
            entity.Insert();
            return entity;
        }

        private static void AddOrSkipNode(AppDeploy set, Node node, Boolean enable)
        {
            var exist = AppDeployNode.Find(AppDeployNode._.DeployId == set.Id & AppDeployNode._.NodeId == node.ID);
            if (exist != null) return;

            new AppDeployNode { DeployId = set.Id, NodeId = node.ID, Enable = enable }.Insert();
        }
    }

    // appId branch filters by the app's deploy sets
    [Fact]
    public async Task AppId_FiltersByDeploySets()
    {
        var rows = await QueryAsync($"appId={_seed.Value.App1.Id}");

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Contains(
            r.GetProperty("deployId").GetInt32(),
            new[] { _seed.Value.Set1.Id, _seed.Value.Set2.Id }));
    }

    [Fact]
    public async Task AppId_NoDeploySets_ReturnsEmpty()
    {
        var rows = await QueryAsync($"appId={_seed.Value.App9.Id}");

        Assert.Empty(rows);
    }

    [Fact]
    public async Task AppId_EnableFilter()
    {
        var enabled = await QueryAsync($"appId={_seed.Value.App1.Id}&enable=true");
        Assert.Equal(2, enabled.Count);
        Assert.All(enabled, r => Assert.True(r.GetProperty("enable").GetBoolean()));

        var disabled = await QueryAsync($"appId={_seed.Value.App1.Id}&enable=false");
        Assert.Single(disabled);
        Assert.All(disabled, r => Assert.False(r.GetProperty("enable").GetBoolean()));
    }

    // deployId single-value regression guard
    [Fact]
    public async Task DeployId_SingleSet_Unchanged()
    {
        var rows = await QueryAsync($"deployId={_seed.Value.Set1.Id}");

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(_seed.Value.Set1.Id, r.GetProperty("deployId").GetInt32()));
    }

    // array overload In semantics (enable filter supported)
    [Fact]
    public void ArraySearch_In_MatchesSetIds()
    {
        var setIds = new[] { _seed.Value.Set1.Id, _seed.Value.Set3.Id };

        var list = AppDeployNode.Search(setIds, -1, null, null, null);

        Assert.Equal(3, list.Count);
        Assert.All(list, e => Assert.Contains(e.DeployId, setIds));
    }

    // entity write path fixes deploy count
    [Fact]
    public void Entity_WritePath_FixesDeployCount()
    {
        var app2 = _seed.Value.App2;
        var set = AppDeploy.Find(AppDeploy._.Name == "DepSet4");
        if (set == null)
        {
            set = new AppDeploy { Name = "DepSet4", AppId = app2.Id, Enable = true, Nodes = 0 };
            set.Insert();
        }
        else
        {
            // re-entrant: clear history rows back to zero
            foreach (var item in AppDeployNode.FindAllByDeployId(set.Id)) item.Delete();
            set.Nodes = 0;
            set.Update();
        }

        var node1 = new AppDeployNode { DeployId = set.Id, NodeId = _seed.Value.Node1.ID, Enable = true };
        node1.Insert();
        Assert.Equal(1, Recount(set.Id)); // after Insert count = enabled rows

        var node2 = new AppDeployNode { DeployId = set.Id, NodeId = _seed.Value.Node2.ID, Enable = true };
        node2.Insert();
        Assert.Equal(2, Recount(set.Id));

        node1.Enable = false;
        node1.Update();
        Assert.Equal(1, Recount(set.Id)); // after Update still enabled-count

        // DeleteSelect batch equivalent: entity Delete without controller hooks
        node1.Delete();
        Assert.Equal(1, Recount(set.Id));

        node2.Delete();
        Assert.Equal(0, Recount(set.Id));
    }

    private static Int32 Recount(Int32 deployId) => AppDeploy.FindAll(AppDeploy._.Id == deployId)[0].Nodes;

    private async Task<List<JsonElement>> QueryAsync(String query)
    {
        var api = await _api.GetJsonAsync($"Deployment/AppDeployNode?PageSize=200&{query}");

        Assert.Equal(JsonValueKind.Array, api.Data.ValueKind);

        return api.Data.EnumerateArray().ToList();
    }
}
