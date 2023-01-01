using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Installer;

internal class Program
{
    static void Main(String[] args)
    {
        if (args != null)
            Console.WriteLine("args: {0}", String.Join(' ', args));

        // 分解命令（可能包括多组），逐个执行
        // 如：installer.exe -install StarAgent -start StarAgent
        foreach (var cmd in Command.Parse(args))
        {
            switch (cmd.Name.ToLower())
            {
                case "-install": Install(cmd.Arguments); break;
                case "-uninstall": Uninstall(cmd.Arguments); break;
                case "-reinstall": Reinstall(cmd.Arguments); break;
                case "-start": Start(cmd.Arguments); break;
                case "-stop": Stop(cmd.Arguments); break;
                case "-restart": Restart(cmd.Arguments); break;
                default:
                    break;
            }
        }

        Console.WriteLine("Hello, World!");
    }

    static void Install(IList<String> args)
    {

    }

    static void Uninstall(IList<String> args)
    {

    }

    static void Reinstall(IList<String> args)
    {

    }

    static void Start(IList<String> args)
    {
        if (args.Count == 0) return;

        Process.Start("net", $"start {args[0]}");
    }

    static void Stop(IList<String> args)
    {
        if (args.Count == 0) return;

        Process.Start("net", $"stop {args[0]}");
    }

    static void Restart(IList<String> args)
    {
        if (args.Count == 0) return;

        Process.Start("net", $"start {args[0]}");
        Process.Start("net", $"stop {args[0]}");
    }
}