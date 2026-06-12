using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Reflection;
using NewLife.Threading;
using NewLife.Web;
using Stardust.Models;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Membership;
using XCode.Shards;

namespace Stardust.Data.Nodes;

public partial class DotNetPackage : Entity<DotNetPackage>
{
    #region 对象操作
    // 控制最大缓存数量，Find/FindAll查询方法在表行数小于该值时走实体缓存
    private static Int32 MaxCacheCount = 1000;

    static DotNetPackage()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(OSKind));

        // 拦截器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add(new UserInterceptor { AllowEmpty = false });
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add(new IPInterceptor { AllowEmpty = false });

        // 实体缓存
        // var ec = Meta.Cache;
        // ec.Expire = 60;
    }

    /// <summary>验证并修补数据，返回验证结果，或者通过抛出异常的方式提示验证失败。</summary>
    /// <param name="method">添删改方法</param>
    public override Boolean Valid(DataMethod method)
    {
        //if (method == DataMethod.Delete) return true;
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return true;

        // 建议先调用基类方法，基类方法会做一些统一处理
        if (!base.Valid(method)) return false;

        // 在新插入数据或者修改了指定字段时进行修正

        // 处理当前已登录用户信息，可以由UserInterceptor拦截器代劳
        /*var user = ManageProvider.User;
        if (user != null)
        {
            if (method == DataMethod.Insert && !Dirtys[nameof(CreateUserID)]) CreateUserID = user.ID;
            if (!Dirtys[nameof(UpdateUserID)]) UpdateUserID = user.ID;
        }*/
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateTime)]) CreateTime = DateTime.Now;
        //if (!Dirtys[nameof(UpdateTime)]) UpdateTime = DateTime.Now;
        //if (method == DataMethod.Insert && !Dirtys[nameof(CreateIP)]) CreateIP = ManageProvider.UserHost;
        //if (!Dirtys[nameof(UpdateIP)]) UpdateIP = ManageProvider.UserHost;

        return true;
    }

    ///// <summary>首次连接数据库时初始化数据，仅用于实体类重载，用户不应该调用该方法</summary>
    //[EditorBrowsable(EditorBrowsableState.Never)]
    //protected override void InitData()
    //{
    //    // InitData一般用于当数据表没有数据时添加一些默认数据，该实体类的任何第一次数据库操作都会触发该方法，默认异步调用
    //    if (Meta.Session.Count > 0) return;

    //    if (XTrace.Debug) XTrace.WriteLine("开始初始化DotNetPackage[dotNet安装包]数据……");

    //    var entity = new DotNetPackage();
    //    entity.Version = "abc";
    //    entity.Kind = "abc";
    //    entity.OSKind = 0;
    //    entity.Architecture = "abc";
    //    entity.FileName = "abc";
    //    entity.Source = "abc";
    //    entity.Size = 0;
    //    entity.FileHash = "abc";
    //    entity.Enable = true;
    //    entity.Force = true;
    //    entity.Channel = 0;
    //    entity.AutoImport = true;
    //    entity.Insert();

    //    if (XTrace.Debug) XTrace.WriteLine("完成初始化DotNetPackage[dotNet安装包]数据！");
    //}

    ///// <summary>已重载。基类先调用Valid(true)验证数据，然后在事务保护内调用OnInsert</summary>
    ///// <returns></returns>
    //public override Int32 Insert()
    //{
    //    return base.Insert();
    //}

    ///// <summary>已重载。在事务保护范围内处理业务，位于Valid之后</summary>
    ///// <returns></returns>
    //protected override Int32 OnDelete()
    //{
    //    return base.OnDelete();
    //}
    #endregion

    #region 扩展属性
    #endregion

    #region 高级查询

    // Select Count(Id) as Id,Version From DotNetPackage Where CreateTime>'2020-01-24 00:00:00' Group By Version Order By Id Desc limit 20
    static readonly FieldCache<DotNetPackage> _VersionCache = new(nameof(Version))
    {
        //Where = _.CreateTime > DateTime.Today.AddDays(-30) & Expression.Empty
    };

    /// <summary>获取版本号列表，字段缓存10分钟，分组统计数据最多的前20种，用于魔方前台下拉选择</summary>
    /// <returns></returns>
    public static IDictionary<String, String> GetVersionList() => _VersionCache.FindAllName();

    /// <summary>按版本号查询所有安装包</summary>
    /// <param name="version">版本号</param>
    public static IList<DotNetPackage> FindAllByVersion(String version) => FindAll(_.Version == version);
    #endregion

    #region 业务操作
    /// <summary>将节点具体的OSKinds映射为通用的OSKind</summary>
    private static OSKind GetOSKind(Stardust.Models.OSKinds os)
    {
        var v = (Int32)os;
        if (v >= 10 && v < 100 || v == 70) return OSKind.Windows;
        if (v >= 100 && v < 200) return OSKind.Linux;
        if (v >= 200 && v < 300) return OSKind.LinuxMusl;
        if (v >= 400 && v < 500) return OSKind.OSX;

        return OSKind.Any;
    }

    /// <summary>将节点架构字符串映射为CpuArch枚举</summary>
    private static CpuArch GetCpuArch(String arch)
    {
        if (arch.IsNullOrEmpty()) return CpuArch.Any;

        if (arch.EqualIgnoreCase("X86", "x86", "X32", "x32")) return CpuArch.X86;
        if (arch.EqualIgnoreCase("X64", "x64", "AMD64", "amd64")) return CpuArch.X64;
        if (arch.EqualIgnoreCase("Arm64", "arm64", "AArch64", "aarch64")) return CpuArch.Arm64;
        if (arch.EqualIgnoreCase("Arm32", "arm32", "Arm", "arm")) return CpuArch.Arm;
        if (arch.EqualIgnoreCase("LoongArch64", "loongarch64", "LA64", "la64")) return CpuArch.LA64;
        if (arch.EqualIgnoreCase("RiscV64", "riscv64")) return CpuArch.RiscV64;
        if (arch.EqualIgnoreCase("Mips64", "mips64")) return CpuArch.Mips64;

        return CpuArch.Any;
    }

    /// <summary>获取有效包列表</summary>
    /// <param name="channel">升级通道</param>
    /// <returns></returns>
    public static IList<DotNetPackage> GetValids(NodeChannels channel)
    {
        var list = Meta.Cache.FindAll(e => e.Enable);
        if (list.Count == 0) return list;

        if (channel >= NodeChannels.Release) list = list.Where(e => e.Channel == channel).ToList();

        // 按照编号降序
        list = list.OrderByDescending(e => e.Id).Take(100).ToList();

        return list;
    }

    /// <summary>匹配节点，找到适合指定节点的dotNet安装包</summary>
    /// <param name="node">节点</param>
    /// <returns>匹配的安装包，若无匹配返回null</returns>
    public static DotNetPackage Match(Node node) => Match(node, null);

    /// <summary>匹配节点，找到适合指定节点的dotNet安装包</summary>
    /// <param name="node">节点</param>
    /// <param name="kind">安装类型。null表示自动判断：Linux→aspnet，Win桌面→desktop，Win服务器→host</param>
    /// <returns>匹配的安装包，若无匹配返回null</returns>
    public static DotNetPackage Match(Node node, String kind)
    {
        // 将节点信息映射为DotNetPackage的枚举
        var os = GetOSKind(node.OSKind);
        var arch = GetCpuArch(node.Architecture);

        // 自动判断安装类型
        if (kind.IsNullOrEmpty())
        {
            if (os == OSKind.Linux || os == OSKind.LinuxMusl)
                kind = "aspnet";
            else if (os == OSKind.Windows)
            {
                // 从详细OS判断桌面版还是服务器版
                var osDetail = node.OSKind;
                var v = (Int32)osDetail;
                // Windows 桌面版 (win10/win11/win7)
                if (osDetail == Stardust.Models.OSKinds.Win10 || osDetail == Stardust.Models.OSKinds.Win11 || osDetail == Stardust.Models.OSKinds.Win7)
                    kind = "desktop";
                // Windows 服务器版
                else if (v == 68 || v == 64 || v == 66 || v == 69 || v == 72 || v == 70)
                    kind = "host";
                else
                    kind = "desktop";
            }
        }

        if (kind.IsNullOrEmpty()) return null;

        var list = GetValids(NodeChannels.Release);
        if (list.Count == 0) return null;

        // 按匹配度排序：精确匹配 OS + Arch + Kind，Version 降序
        var pkg = list
            .Where(e => e.Kind == kind)
            .Where(e => e.OSKind == os || e.OSKind == 0)
            .Where(e => e.Architecture == arch || e.Architecture == 0)
            .OrderByDescending(e => e.Id)
            .FirstOrDefault();

        return pkg;
    }
    #endregion
}
