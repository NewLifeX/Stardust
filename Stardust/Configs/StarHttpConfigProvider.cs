using NewLife.Configuration;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Serialization;
using Stardust.Models;
using Stardust.Services;

namespace Stardust.Configs
{
    internal class StarHttpConfigProvider : HttpConfigProvider
    {
        public ConfigInfo ConfigInfo { get; set; }

        private Boolean _useWorkerId;

        protected override IDictionary<String, Object> GetAll()
        {
            var rs = base.GetAll();

            if (rs != null && rs.Count > 0)
            {
                var inf = Info;
                ConfigInfo = JsonHelper.Convert<ConfigInfo>(inf);

                var dic = new Dictionary<String, Object>(inf);
                dic.Remove("configs");
                XTrace.WriteLine("从配置中心加载：{0}", dic.ToJson());
            }

            // 接收配置中心颁发的WorkerId
            if (rs != null && rs.TryGetValue("NewLife.WorkerId", out var wid))
            {
                if (Snowflake.GlobalWorkerId <= 0) _useWorkerId = true;

                var id = wid.ToInt();
                if (id > 0 && _useWorkerId)
                {
                    XTrace.WriteLine("配置中心为当前应用实例分配全局WorkerId={0}，保障雪花Id的唯一性", id);
                    Snowflake.GlobalWorkerId = id;
                }
            }

            return rs;
        }

        public void Attach(ICommandClient client) => client.RegisterCommand("config/publish", DoPublish);

        //private void Client_Received(Object sender, CommandEventArgs e)
        //{
        //    var cmd = e.Model?.Command;
        //    if (cmd.IsNullOrEmpty()) return;
        //    if (!cmd.StartsWithIgnoreCase("config/")) return;

        //    var rs = new CommandReplyModel { Id = e.Model.Id };
        //    try
        //    {
        //        var result = cmd.ToLower() switch
        //        {
        //            "config/publish" => DoPublish(e.Model.Argument),
        //            _ => null,
        //        };
        //        rs.Status = CommandStatus.已完成;
        //        rs.Data = result;
        //    }
        //    catch (Exception ex)
        //    {
        //        rs.Status = CommandStatus.错误;
        //        rs.Data = ex.Message;
        //    }

        //    if (rs.Data != null) e.Reply = rs;
        //}

        private String DoPublish(String argument)
        {
            //todo 临时采用反射办法。后面直接调用DoRefresh
            //var timer = this.GetValue("_timer") as TimerX;
            //if (timer != null) timer.SetNext(-1);
            this.Invoke("DoRefresh", new Object[] { null });

            return "刷新配置成功";
        }
    }
}