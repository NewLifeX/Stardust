using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeployAgent.Commands;

internal class DeployCommand : ICommand
{
    public void Process(String[] args)
    {
        //!! 待实现：deploy 子命令暂未开发完成，避免误导用户以为功能可用
        // TODO 未来从星尘平台拉取部署任务并执行本地发布，作为 stardeploy 的部署入口
        Console.WriteLine("deploy 命令尚未实现，当前仅支持 pack 命令。");
    }
}

class DeployParameter
{
    public String AppId { get; set; }
}