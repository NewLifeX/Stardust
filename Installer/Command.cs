using System;
using System.Collections.Generic;

namespace Installer;

/// <summary>命令选项</summary>
internal class Command
{
    #region 属性
    /// <summary>操作名</summary>
    public String Name { get; set; }

    /// <summary>参数集合</summary>
    public IList<String> Arguments { get; set; } = new List<String>();
    #endregion

    #region 方法
    /// <summary>分析输入参数数组，得到命令选项列表</summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static IList<Command> Parse(String[] args)
    {
        var commands = new List<Command>();
        if (args == null || args.Length == 0) return commands;

        Command command = null;
        for (var i = 0; i < args.Length; i++)
        {
            // 命令以-开头，其它是参数
            var arg = args[i];
            if (arg[0] == '-')
                commands.Add(command = new Command { Name = arg });
            else
            {
                // 如果第一个参数不是-开头，则添加空名称命令
                if (command == null)
                    commands.Add(command = new Command { Name = "" });

                command.Arguments.Add(arg);
            }
        }

        return commands;
    }
    #endregion
}
