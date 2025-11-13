using Stardust.Data;

namespace StarGateway;

class InitService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        //await Task.Yield();

        Task.Run(() =>
        {
            // 配置
            var set = NewLife.Setting.Current;
            if (set.IsNew)
            {
                set.DataPath = "../Data";
                set.Save();
            }

            // 初始化数据库
            var n = App.Meta.Count;
            //AppStat.Meta.Session.Dal.Db.ShowSQL = false;
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}