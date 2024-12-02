﻿using System.Net.NetworkInformation;
using System.Reflection;
using NewLife;
using NewLife.Agent;
using NewLife.Agent.Command;
using NewLife.Data;
using NewLife.Log;
using NewLife.Reflection;
using Stardust;

namespace StarAgent.CommandHandler;

public class ShowMachineInfo : BaseCommandHandler
{
    public ShowMachineInfo(ServiceBase service) : base(service)
    {
        Cmd = "-ShowMachineInfo";
        Description = "服务器信息";
        ShortcutKey = 't';
    }

    public override void Process(String[] args)
    {
        var service = (MyService)Service;

        //foreach (var di in StarClient.GetDrives())
        //{
        //    XTrace.WriteLine($"{di.Name}\tIsReady={di.IsReady} DriveType={di.DriveType} DriveFormat={di.DriveFormat} TotalSize={di.TotalSize} TotalFreeSpace={di.TotalFreeSpace}");
        //}

        XTrace.WriteLine("FullPath:{0}", ".".GetFullPath());
        XTrace.WriteLine("BasePath:{0}", ".".GetBasePath());
        XTrace.WriteLine("TempPath:{0}", Path.GetTempPath());

        var mi = MachineInfo.Current ?? MachineInfo.RegisterAsync().Result;
        mi.Refresh();
        var pis = mi.GetType().GetProperties(true);

        // 机器信息
        foreach (var pi in pis)
        {
            var val = mi.GetValue(pi);
            if (pi.Name.EndsWithIgnoreCase("Memory"))
                val = val.ToLong().ToGMK();
            else if (pi.Name.EndsWithIgnoreCase("Rate", "Battery"))
                val = val.ToDouble().ToString("p2");

            XTrace.WriteLine("{0}:\t{1}", pi.Name, val);
        }

        // 机器扩展
        var ext = mi as IExtend;
        foreach (var item in ext.Items)
        {
            XTrace.WriteLine("{0}:\t{1}", item.Key, item.Value);
        }

        var client = service._Client ?? new StarClient();
        var ni = client.GetNodeInfo();
        var pis2 = ni.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var pi in pis2)
        {
            if (pis.Any(e => e.Name == pi.Name)) continue;

            var val = ni.GetValue(pi);
            if (pi.Name.EndsWithIgnoreCase("Memory"))
                val = val.ToLong().ToGMK();
            else if (pi.Name.EndsWithIgnoreCase("Rate", "Battery"))
                val = val.ToDouble().ToString("p2");

            XTrace.WriteLine("{0}:\t{1}", pi.Name, val);
        }

        // 网络信息
        XTrace.WriteLine("NetworkAvailable:{0}", NetworkInterface.GetIsNetworkAvailable());
        foreach (var item in NetworkInterface.GetAllNetworkInterfaces())
        {
            //if (item.OperationalStatus != OperationalStatus.Up) continue;
            if (item.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Unknown or NetworkInterfaceType.Tunnel) continue;

            XTrace.WriteLine("{0} {1} {2}", item.NetworkInterfaceType, item.OperationalStatus, item.Name);
            XTrace.WriteLine("\tDescription:\t{0}", item.Description);
            XTrace.WriteLine("\tMac:\t{0}", item.GetPhysicalAddress().GetAddressBytes().ToHex("-"));
            var ipp = item.GetIPProperties();
            if (ipp != null && ipp.UnicastAddresses.Any(e => e.Address.IsIPv4()))
            {
                XTrace.WriteLine("\tIP:\t{0}", ipp.UnicastAddresses.Where(e => e.Address.IsIPv4()).Select(e => e.Address + "").Distinct().Join(","));
                if (ipp.GatewayAddresses.Any(e => e.Address.IsIPv4()))
                    XTrace.WriteLine("\tGateway:{0}", ipp.GatewayAddresses.Where(e => e.Address.IsIPv4()).Join(",", e => e.Address));
                if (ipp.DnsAddresses.Any(e => e.IsIPv4()))
                    XTrace.WriteLine("\tDns:\t{0}", ipp.DnsAddresses.Where(e => e.IsIPv4()).Join());
            }
        }
    }
}
