using NewLife;
using NewLife.Threading;
using Stardust.Data;
using Stardust.Data.Configs;
using Stardust.Data.Deployment;
using Stardust.Data.Monitors;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;

namespace Stardust.Web.Services;

public class FixDataHostedService : IHostedService
{
    private TimerX _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new TimerX(DoWork, null, 5_000, 300_000) { Async = true };

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.TryDispose();

        return Task.CompletedTask;
    }

    private void DoWork(Object state)
    {
        FixNode();
        FixApp();
        FixTracer();
        FixConfig();
        FixDeploy();

        FixProject();
    }

    void FixNode()
    {
        var p = 0;
        while (true)
        {
            var list = Node.FindAll(null, null, null, p, 1000);
            if (list.Count == 0) break;

            // 修正所属项目
            foreach (var entity in list.Where(e => e.ProjectId == 0 && !e.Category.IsNullOrEmpty()))
            {
                var prj = GalaxyProject.FindByName(entity.Category);
                if (prj != null)
                {
                    entity.ProjectId = prj.Id;
                    entity.Update();
                }
            }

            if (list.Count < 1000) break;
            p += list.Count;
        }
    }

    void FixApp()
    {
        var p = 0;
        while (true)
        {
            var list = App.FindAll(null, null, null, p, 1000);
            if (list.Count == 0) break;

            // 修正所属项目
            foreach (var entity in list.Where(e => e.ProjectId == 0 && !e.Category.IsNullOrEmpty()))
            {
                var prj = GalaxyProject.FindByName(entity.Category);
                if (prj != null)
                {
                    entity.ProjectId = prj.Id;
                    entity.Update();
                }
            }

            if (list.Count < 1000) break;
            p += list.Count;
        }
    }

    void FixTracer()
    {
        var p = 0;
        while (true)
        {
            var list = AppTracer.FindAll(null, null, null, p, 1000);
            if (list.Count == 0) break;

            // 修正所属项目
            foreach (var entity in list.Where(e => e.ProjectId == 0 && !e.Category.IsNullOrEmpty()))
            {
                var prj = GalaxyProject.FindByName(entity.Category);
                if (prj != null)
                {
                    entity.ProjectId = prj.Id;
                    entity.Update();
                }
            }

            if (list.Count < 1000) break;
            p += list.Count;
        }
    }

    void FixConfig()
    {
        var p = 0;
        while (true)
        {
            var list = AppConfig.FindAll(null, null, null, p, 1000);
            if (list.Count == 0) break;

            // 修正所属项目
            foreach (var entity in list.Where(e => e.ProjectId == 0 && !e.Category.IsNullOrEmpty()))
            {
                var prj = GalaxyProject.FindByName(entity.Category);
                if (prj != null)
                {
                    entity.ProjectId = prj.Id;
                    entity.Update();
                }
            }

            if (list.Count < 1000) break;
            p += list.Count;
        }
    }

    void FixDeploy()
    {
        var p = 0;
        while (true)
        {
            var list = AppDeploy.FindAll(null, null, null, p, 1000);
            if (list.Count == 0) break;

            // 修正所属项目
            foreach (var entity in list.Where(e => e.ProjectId == 0 && !e.Category.IsNullOrEmpty()))
            {
                var prj = GalaxyProject.FindByName(entity.Category);
                if (prj != null)
                {
                    entity.ProjectId = prj.Id;
                    entity.Update();
                }
            }

            if (list.Count < 1000) break;
            p += list.Count;
        }
    }

    void FixProject()
    {
        var nodes = Node.SearchGroupByProject();
        var apps = App.SearchGroupByProject();
        var list = GalaxyProject.FindAll();
        foreach (var prj in list)
        {
            var nt = nodes.FirstOrDefault(e => e.ProjectId == prj.Id);
            if (nt != null) prj.Nodes = nt["total"].ToInt();

            var at = apps.FirstOrDefault(e => e.ProjectId == prj.Id);
            if (at != null) prj.Apps = at["total"].ToInt();

            prj.Update();
        }
    }
}
