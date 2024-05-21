using NewLife.Agent;
using NewLife.Agent.Command;

namespace StarAgent.CommandHandler
{
    public class UseStarServer : BaseCommandHandler
    {
        public UseStarServer(ServiceBase service) : base(service)
        {
            Cmd = "-UseStarServer";
            Description = "使用星尘";
            ShortcutKey = 's';
        }

        public override void Process(String[] args)
        {
            var set = ((MyService)Service).StarSetting;
            if (!String.IsNullOrEmpty(set.Server)) Console.WriteLine("服务端：{0}", set.Server);

            Console.WriteLine("请输入新的服务端：");

            var addr = Console.ReadLine();
            if (String.IsNullOrEmpty(addr)) addr = "http://127.0.0.1:6600";

            set.Server = addr;
            set.Save();

            Service.WriteLog("服务端修改为：{0}", addr);
        }
    }
}
