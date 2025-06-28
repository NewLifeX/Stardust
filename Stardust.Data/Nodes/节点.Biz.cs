using NewLife;
using NewLife.Data;
using Stardust.Data.Platform;
using Stardust.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using XCode;
using XCode.Cache;
using XCode.Configuration;
using XCode.Membership;

namespace Stardust.Data.Nodes;

/// <summary>节点信息</summary>
public partial class Node : Entity<Node>
{
    #region 对象操作
    static Node()
    {
        var df = Meta.Factory.AdditionalFields;
        df.Add(__.Logins);
        //!!! OnlineTime是新加字段，允许空，导致累加操作失败，暂时关闭累加
        //df.Add(__.OnlineTime);

        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();

        var sc = Meta.SingleCache;
        sc.Expire = 60;
        sc.FindSlaveKeyMethod = e => Find(__.Code, e);
        sc.GetSlaveKeyMethod = e => e.Code;
        //sc.SlaveKeyIgnoreCase = false;
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew"></param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        if (Name.IsNullOrEmpty()) throw new ArgumentNullException(__.Name, _.Name.DisplayName + "不能为空！");

        this.TrimExtraLong(__.Uuid, __.MachineGuid, __.MACs, __.DiskID, __.SerialNumber, __.OS, __.DriveInfo);
        this.TrimExtraLong(__.Framework, __.Frameworks, __.IP);

        // 建议先调用基类方法，基类方法会做一些统一处理
        base.Valid(isNew);

        if (Period == 0) Period = 60;

        if (!Dirtys[__.OSKind])
        {
            // 自动识别版本
            var kind = OSKindHelper.Parse(OS, OSVersion);
            if (kind > 0) OSKind = kind;
        }
    }

    /// <summary>已重载</summary>
    /// <returns></returns>
    public override String ToString() => Code;
    #endregion

    #region 扩展属性
    /// <summary>省份</summary>
    [XmlIgnore, ScriptIgnore]
    public Area Province => Extends.Get(nameof(Province), k => Area.FindByID(ProvinceID));

    /// <summary>省份名</summary>
    [Map(__.ProvinceID)]
    public String ProvinceName => Province + "";

    /// <summary>城市</summary>
    [XmlIgnore, ScriptIgnore]
    public Area City => Extends.Get(nameof(City), k => Area.FindByID(CityID));

    /// <summary>城市名</summary>
    [Map(__.CityID)]
    public String CityName => City?.Path ?? Province?.Path;

    /// <summary>最后地址。IP=>Address</summary>
    [DisplayName("最后地址")]
    public String LastLoginAddress => LastLoginIP.IPToAddress();
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns></returns>
    public static Node FindByID(Int32 id)
    {
        if (id <= 0) return null;

        if (Meta.Count < 1000) return Meta.Cache.Entities.FirstOrDefault(e => e.ID == id);

        // 单对象缓存
        return Meta.SingleCache[id];
    }

    /// <summary>根据名称。登录用户名查找</summary>
    /// <param name="name">名称。登录用户名</param>
    /// <returns></returns>
    public static Node FindByName(String name)
    {
        if (name.IsNullOrEmpty()) return null;

        if (Meta.Count < 1000) return Meta.Cache.Entities.FirstOrDefault(e => e.Name == name);

        return Find(__.Name, name);
    }

    /// <summary>根据Mac</summary>
    /// <param name="mac">Mac</param>
    /// <returns></returns>
    public static Node FindByMac(String mac)
    {
        if (mac.IsNullOrEmpty()) return null;

        return Find(_.MACs.Contains(mac));
    }

    /// <summary>根据名称查找</summary>
    /// <param name="code">名称</param>
    /// <param name="cache">是否走缓存</param>
    /// <returns>实体对象</returns>
    public static Node FindByCode(String code, Boolean cache = true)
    {
        if (code.IsNullOrEmpty()) return null;

        if (!cache) return Find(_.Code == code);

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Code == code);

        // 单对象缓存
        return Meta.SingleCache.GetItemWithSlaveKey(code) as Node;
    }

    /// <summary>根据IP查找节点</summary>
    /// <param name="ips"></param>
    /// <returns></returns>
    public static IList<Node> FindAllByIPs(params String[] ips)
    {
        if (ips == null || ips.Length == 0) return new List<Node>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => !e.IP.IsNullOrEmpty() && ips.Contains(e.IP));

        return FindAll(_.IP.In(ips));
    }

    /// <summary>根据IP查找节点</summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static IList<Node> FindAllByIP(String ip)
    {
        if (ip.IsNullOrEmpty()) return new List<Node>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => ip == e.IP);

        return FindAll(_.IP == ip);
    }

    /// <summary>根据分类查找</summary>
    /// <param name="category">分类</param>
    /// <returns>实体列表</returns>
    public static IList<Node> FindAllByCategory(String category)
    {
        if (category.IsNullOrEmpty()) return new List<Node>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Category.EqualIgnoreCase(category));

        return FindAll(_.Category == category);
    }

    /// <summary>根据产品查找</summary>
    /// <param name="productCode">产品</param>
    /// <returns>实体列表</returns>
    public static IList<Node> FindAllByProductCode(String productCode)
    {
        if (productCode.IsNullOrEmpty()) return new List<Node>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProductCode.EqualIgnoreCase(productCode));

        return FindAll(_.ProductCode == productCode);
    }

    /// <summary>根据版本查找</summary>
    /// <param name="version">版本</param>
    /// <returns>实体列表</returns>
    public static IList<Node> FindAllByVersion(String version)
    {
        if (version.IsNullOrEmpty()) return new List<Node>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Version.EqualIgnoreCase(version));

        return FindAll(_.Version == version);
    }

    /// <summary>根据系统种类查找</summary>
    /// <param name="oSKind">系统种类</param>
    /// <returns>实体列表</returns>
    public static IList<Node> FindAllByOSKind(Stardust.Models.OSKinds oSKind)
    {
        if (oSKind <= 0) return new List<Node>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.OSKind == oSKind);

        return FindAll(_.OSKind == oSKind);
    }

    /// <summary>根据唯一标识、机器标识、网卡查找</summary>
    /// <param name="uuid">唯一标识</param>
    /// <param name="machineGuid">机器标识</param>
    /// <param name="mACs">网卡</param>
    /// <returns>实体列表</returns>
    public static IList<Node> FindAllByUuidAndMachineGuidAndMACs(String uuid, String machineGuid, String mACs)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.Uuid.EqualIgnoreCase(uuid) && e.MachineGuid.EqualIgnoreCase(machineGuid) && e.MACs.EqualIgnoreCase(mACs));

        return FindAll(_.Uuid == uuid & _.MachineGuid == machineGuid & _.MACs == mACs);
    }

    /// <summary>根据最后活跃查找</summary>
    /// <param name="lastActive">最后活跃</param>
    /// <returns>实体列表</returns>
    public static IList<Node> FindAllByLastActive(DateTime lastActive)
    {

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.LastActive == lastActive);

        return FindAll(_.LastActive == lastActive);
    }

    /// <summary>根据项目查找</summary>
    /// <param name="projectId">项目</param>
    /// <returns>实体列表</returns>
    public static IList<Node> FindAllByProjectId(Int32 projectId)
    {
        if (projectId <= 0) return new List<Node>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.ProjectId == projectId);

        return FindAll(_.ProjectId == projectId);
    }
    #endregion

    #region 高级查询
    /// <summary>根据唯一标识搜索，任意一个匹配即可</summary>
    /// <param name="uuid"></param>
    /// <param name="guid"></param>
    /// <param name="macs"></param>
    /// <returns></returns>
    public static IList<Node> SearchAny(String uuid, String guid, String macs, String serial, String diskid)
    {
        var exp = new WhereExpression();
        if (!uuid.IsNullOrEmpty()) exp |= _.Uuid == uuid;
        if (!guid.IsNullOrEmpty()) exp |= _.MachineGuid == guid;
        if (!macs.IsNullOrEmpty()) exp |= _.MACs == macs;
        if (!serial.IsNullOrEmpty()) exp |= _.SerialNumber == serial;
        if (!diskid.IsNullOrEmpty()) exp |= _.DiskID == diskid;

        if (exp.IsEmpty) return new List<Node>();

        return FindAll(exp);
    }

    /// <summary>高级查询</summary>
    /// <param name="provinceId">省份</param>
    /// <param name="cityId">城市</param>
    /// <param name="category">类别</param>
    /// <param name="product">类别</param>
    /// <param name="osKind">系统种类</param>
    /// <param name="version">版本</param>
    /// <param name="enable"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<Node> Search(Int32 projectId, Int32 provinceId, Int32 cityId, String category, String product, OSKinds osKind, String version, String runtime, String framework, String arch, Boolean? enable, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId >= 0) exp &= _.ProjectId == projectId;
        if (provinceId >= 0) exp &= _.ProvinceID == provinceId;
        if (cityId >= 0) exp &= _.CityID == cityId;
        if (!category.IsNullOrEmpty()) exp &= _.Category == category;
        if (!product.IsNullOrEmpty()) exp &= _.ProductCode == product;
        if (osKind > 0)
            exp &= _.OSKind == osKind;
        else if (osKind == 0)
            exp &= _.OSKind == 0 | _.OSKind.IsNull();
        if (!version.IsNullOrEmpty()) exp &= _.Version == version;
        if (!runtime.IsNullOrEmpty()) exp &= _.Runtime.StartsWith(runtime);
        if (!framework.IsNullOrEmpty()) exp &= _.Framework == framework;
        if (!arch.IsNullOrEmpty()) exp &= _.Architecture == arch;
        if (enable != null) exp &= _.Enable == enable.Value;

        //exp &= _.CreateTime.Between(start, end);
        //exp &= _.LastLogin.Between(start, end);
        //exp &= _.UpdateTime.Between(start, end);
        exp &= _.LastActive.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= SearchWhereByKeys(key);

        return FindAll(exp, page);
    }

    /// <summary>根据IP查找节点</summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static IList<Node> SearchByIP(String ip)
    {
        if (ip.IsNullOrEmpty()) return [];

        var ips = ip.Split(',', StringSplitOptions.RemoveEmptyEntries);

        // 模糊匹配IP
        IList<Node> list;
        if (Meta.Session.Count < 1000)
        {
            list = Meta.Cache.FindAll(e => !e.IP.IsNullOrEmpty() && ips.Any(y => e.IP.Contains(y)));
        }
        else
        {
            var exp = new WhereExpression();
            foreach (var item in ips)
            {
                exp |= _.IP.Contains(item);
            }
            list = FindAll(exp);
        }

        // 精确匹配IP
        list = list.Where(e => !e.IP.IsNullOrEmpty() && e.IP.Split(',').Any(y => ips.Contains(y))).ToList();

        return list;
    }

    /// <summary>根据类别搜索</summary>
    /// <param name="category"></param>
    /// <param name="product"></param>
    /// <param name="enable"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<Node> SearchByCategory(String category, String product, Boolean? enable, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (!category.IsNullOrEmpty()) exp &= _.Category == category | _.Category.IsNullOrEmpty();
        if (!product.IsNullOrEmpty()) exp &= _.ProductCode == product;

        if (enable != null) exp &= _.Enable == enable.Value;

        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.Category.Contains(key) | _.MachineName.Contains(key) | _.UserName.Contains(key);

        return FindAll(exp, page);
    }

    public static IList<Node> Search(Int32 projectId, Boolean? global, String category, String product, Boolean? enable, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (projectId > 0 && global != null)
        {
            // 找到全局项目，然后再找到所有节点。如果项目不存在，则也不会有节点
            var prjs = GalaxyProject.FindAllWithCache().Where(e => e.IsGlobal == global.Value).Select(e => e.Id).ToList();
            if (projectId > 0 && !prjs.Contains(projectId)) prjs.Add(projectId);
            if (prjs.Count == 0) return [];

            exp &= _.ProjectId.In(prjs);
        }
        else if (projectId > 0)
            exp &= _.ProjectId == projectId;

        if (!category.IsNullOrEmpty()) exp &= _.Category == category | _.Category.IsNullOrEmpty();
        if (!product.IsNullOrEmpty()) exp &= _.ProductCode == product;

        if (enable != null) exp &= _.Enable == enable.Value;

        if (!key.IsNullOrEmpty()) exp &= _.Code.Contains(key) | _.Name.Contains(key) | _.Category.Contains(key) | _.MachineName.Contains(key) | _.UserName.Contains(key);

        return FindAll(exp, page);
    }

    public static IList<Node> SearchGroup(DateTime start, String selects, FieldItem groupField)
    {
        var exp = new WhereExpression();
        exp &= _.LastActive >= start;
        exp &= _.Enable == true;

        return FindAll(exp.GroupBy(groupField), null, selects);
    }

    public static IList<Node> SearchGroup(DateTime start, String selects, String groupField)
    {
        var exp = new WhereExpression();
        exp &= _.LastActive >= start;
        exp &= _.Enable == true;

        return FindAll(exp + $" Group By {groupField}", null, selects, 0, 0);
    }

    internal static IDictionary<Int32, Int32> SearchGroupByCreateTime(DateTime start, DateTime end)
    {
        var exp = new WhereExpression();
        exp &= _.CreateTime.Between(start, end);
        var list = FindAll(exp.GroupBy(_.ProvinceID), null, _.ID.Count() & _.ProvinceID, 0, 0);
        return list.ToDictionary(e => e.ProvinceID, e => e.ID);
    }

    internal static IDictionary<Int32, Int32> SearchGroupByLastLogin(DateTime start, DateTime end)
    {
        var exp = new WhereExpression();
        exp &= _.LastLogin.Between(start, end);
        var list = FindAll(exp.GroupBy(_.ProvinceID), null, _.ID.Count() & _.ProvinceID, 0, 0);
        return list.ToDictionary(e => e.ProvinceID, e => e.ID);
    }

    internal static IDictionary<Int32, Int32> SearchCountByCreateDate(DateTime date)
    {
        var exp = new WhereExpression();
        exp &= _.CreateTime < date.AddDays(1);
        var list = FindAll(exp.GroupBy(_.ProvinceID), null, _.ID.Count() & _.ProvinceID, 0, 0);
        return list.ToDictionary(e => e.ProvinceID, e => e.ID);
    }

    public static IList<Node> SearchGroupByProject()
    {
        var selects = _.ID.Count("total") & _.ProjectId;
        var exp = new WhereExpression();

        return FindAll(exp.GroupBy(_.ProjectId), null, selects);
    }
    #endregion

    #region 扩展操作
    /// <summary>类别名实体缓存，异步，缓存10分钟</summary>
    static Lazy<FieldCache<Node>> VersionCache = new(() => new FieldCache<Node>(__.Version)
    {
        Where = _.UpdateTime > DateTime.Today.AddDays(-30) & Expression.Empty,
        MaxRows = 50
    });

    /// <summary>获取所有类别名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllVersion() => VersionCache.Value.FindAllName().OrderByDescending(e => e.Key).ToDictionary(e => e.Key, e => e.Value);

    /// <summary>类别名实体缓存，异步，缓存10分钟</summary>
    static Lazy<FieldCache<Node>> CategoryCache = new(() => new FieldCache<Node>(__.Category)
    {
        Where = _.UpdateTime > DateTime.Today.AddDays(-30) & Expression.Empty,
        MaxRows = 50
    });

    /// <summary>获取所有类别名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllCategory() => CategoryCache.Value.FindAllName();

    /// <summary>类别名实体缓存，异步，缓存10分钟</summary>
    static Lazy<FieldCache<Node>> ProductCache = new(() => new FieldCache<Node>(__.ProductCode)
    {
        Where = _.UpdateTime > DateTime.Today.AddDays(-30) & Expression.Empty,
        MaxRows = 50
    });

    /// <summary>获取所有类别名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllProduct() => ProductCache.Value.FindAllName();

    /// <summary>系统种类实体缓存，异步，缓存10分钟</summary>
    static Lazy<FieldCache<Node>> OSKindCache = new(() => new FieldCache<Node>(__.OSKind)
    {
        Where = _.UpdateTime > DateTime.Today.AddDays(-30) & Expression.Empty,
        MaxRows = 50
    });

    /// <summary>获取所有系统种类名称</summary>
    /// <returns></returns>
    public static IDictionary<String, String> FindAllOSKind() => OSKindCache.Value.FindAllName();
    #endregion

    #region 业务
    ///// <summary>根据编码查询或添加</summary>
    ///// <param name="code"></param>
    ///// <returns></returns>
    //public static Node GetOrAdd(String code) => GetOrAdd(code, FindByCode, k => new Node { Code = k, Enable = true });

    /// <summary>登录并保存信息</summary>
    /// <param name="di"></param>
    /// <param name="ip"></param>
    public void Login(NodeInfo di, String ip)
    {
        var node = this;

        node.Fill(di);

        // 如果节点本地IP为空，而来源IP是局域网，则直接取用
        //if (node.IP.IsNullOrEmpty() && ip.StartsWithIgnoreCase("10.", "192.", "172.")) node.IP = ip;
        if (node.IP.IsNullOrEmpty()) node.IP = ip;

        node.Logins++;
        node.LastLogin = DateTime.Now;
        node.LastLoginIP = ip;
        node.LastActive = DateTime.Now;

        if (node.CreateIP.IsNullOrEmpty()) node.CreateIP = ip;
        node.UpdateIP = ip;

        node.FixArea();

        node.Save();
    }

    /// <summary>填充</summary>
    /// <param name="info"></param>
    public void Fill(NodeInfo info)
    {
        var node = this;

        if (!info.OSName.IsNullOrEmpty()) node.OS = info.OSName;
        if (!info.OSVersion.IsNullOrEmpty()) node.OSVersion = info.OSVersion;
        if (!info.Architecture.IsNullOrEmpty()) node.Architecture = info.Architecture;
        if (!info.Version.IsNullOrEmpty()) node.Version = info.Version;
        if (info.Compile.Year > 2000) node.CompileTime = info.Compile;

        if (!info.MachineName.IsNullOrEmpty())
        {
            if (node.Name.IsNullOrEmpty() || node.Name == node.MachineName) node.Name = info.MachineName;
            node.MachineName = info.MachineName;
        }
        if (!info.UserName.IsNullOrEmpty()) node.UserName = info.UserName;
        if (!info.IP.IsNullOrEmpty()) node.IP = info.IP;
        if (!info.Gateway.IsNullOrEmpty()) node.Gateway = info.Gateway;
        if (!info.Dns.IsNullOrEmpty()) node.Dns = info.Dns;
        if (!info.Product.IsNullOrEmpty()) node.Product = info.Product;
        if (!info.Vendor.IsNullOrEmpty()) node.Vendor = info.Vendor;
        if (!info.Processor.IsNullOrEmpty()) node.Processor = info.Processor;
        if (!info.UUID.IsNullOrEmpty()) node.Uuid = info.UUID;
        if (!info.MachineGuid.IsNullOrEmpty()) node.MachineGuid = info.MachineGuid;
        if (!info.SerialNumber.IsNullOrEmpty()) node.SerialNumber = info.SerialNumber;
        if (!info.Board.IsNullOrEmpty()) node.Board = info.Board;
        if (!info.DiskID.IsNullOrEmpty()) node.DiskID = info.DiskID;

        if (info.ProcessorCount > 0) node.Cpu = info.ProcessorCount;
        if (info.Memory > 0) node.Memory = (Int32)Math.Round(info.Memory / 1024d / 1024);
        if (info.TotalSize > 0) node.TotalSize = (Int32)Math.Round(info.TotalSize / 1024d / 1024);
        if (info.DriveSize > 0) node.DriveSize = (Int32)Math.Round(info.DriveSize / 1024d / 1024);
        if (!info.DriveInfo.IsNullOrEmpty())
        {
            node.DriveInfo = info.DriveInfo;
            // 兼容旧版客户端
            if (node.DriveSize == 0)
                node.DriveSize = (Int32)Math.Round(info.DriveInfo.Split(",").Sum(e => e.Substring("/", "G").ToDouble() * 1024));
        }
        if (info.MaxOpenFiles > 0) node.MaxOpenFiles = info.MaxOpenFiles;
        if (!info.Dpi.IsNullOrEmpty()) node.Dpi = info.Dpi;
        if (!info.Resolution.IsNullOrEmpty()) node.Resolution = info.Resolution;
        if (!info.Macs.IsNullOrEmpty()) node.MACs = info.Macs;
        //if (!di.COMs.IsNullOrEmpty()) node.COMs = di.COMs;
        if (!info.InstallPath.IsNullOrEmpty()) node.InstallPath = info.InstallPath;
        if (!info.Runtime.IsNullOrEmpty())
        {
            node.Runtime = info.Runtime;
            var fx = GetVersionByBuild(info.Runtime);
            if (node.Framework.IsNullOrEmpty() || node.Framework == node.Runtime) node.Framework = fx;
            if (node.Frameworks.IsNullOrEmpty() || node.Framework == node.Runtime) node.Frameworks = fx;
        }
        if (!info.Framework.IsNullOrEmpty())
        {
            //node.Framework = di.Framework?.Split(',').LastOrDefault();
            node.Frameworks = info.Framework;
            // 选取最大的版本，而不是最后一个，例如6.0.3字符串大于6.0.13
            Version max = null;
            var fs = info.Framework.Split(',');
            if (fs != null)
            {
                foreach (var f in fs)
                {
                    if (System.Version.TryParse(f, out var v) && (max == null || max < v))
                        max = v;
                }
                node.Framework = max?.ToString();
            }
        }

        if (!node.OS.IsNullOrEmpty()) node.OSKind = OSKindHelper.Parse(node.OS, node.OSVersion);
    }

    /// <summary>
    /// 根据运行时构建号获取主要CLR版本
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    private static String GetVersionByBuild(String version)
    {
        if (!System.Version.TryParse(version, out var ver)) return version;

        // .NET Framework 4.0及以上的主要CLR版本是4.0.30319.x
        if (ver.Major == 4 && ver.Minor == 0 && ver.Build == 30319)
        {
            // 大致范围估计，不保证100%准确
            return ver.Revision switch
            {
                >= 42000 => "4.6.1",
                >= 34209 => "4.6",
                >= 19000 => "4.5.2",
                >= 18408 => "4.5.1",
                >= 17929 => "4.5.0",
                _ => "4.0"
            };
        }

        // 如果不是4.0.30319.x版本格式，直接返回原始版本
        return ver.ToString();
    }

    /// <summary>修正地区</summary>
    public void FixArea()
    {
        // 借助节点所在网关，优先根据节点定位来确定位置
        var location = NodeLocation.Match(IP, MACs, UpdateIP);
        if (location == null)
        {
            var gws = Gateway?.Split('/');
            if (gws != null && gws.Length >= 2)
            {
                location = NodeLocation.Match(gws[0], gws[1], UpdateIP);
            }
        }
        if (location != null)
        {
            var area = location.Area;
            if (area != null)
            {
                ProvinceID = area.GetAllParents().FirstOrDefault()?.ID ?? 0;
                CityID = area.ID;
            }

            Address = location.Address;
            Location = location.Location;

            return;
        }

        var node = this;
        if (node.UpdateIP.IsNullOrEmpty()) return;

        var rs = Area.SearchIP(node.UpdateIP);
        if (rs.Count > 0)
        {
            node.ProvinceID = rs[0].ID;
            node.CityID = 0;
        }
        if (rs.Count > 1) node.CityID = rs[^1].ID;

        Address = node.UpdateIP.IPToAddress()?.TrimStart("中国–");
    }

    /// <summary>
    /// 根据IP地址修正名称和分类
    /// </summary>
    public void FixNameByRule()
    {
        //var ip = IP;
        //if (ip.IsNullOrEmpty()) return;

        var rule = NodeResolver.Instance.Match(IP, UpdateIP);
        if (rule != null)
        {
            if ((Name.IsNullOrEmpty() || Name == MachineName) && !rule.Name.IsNullOrEmpty())
                Name = rule.Name;

            if (!rule.Category.IsNullOrEmpty())
                Category = rule.Category;
        }
    }

    /// <summary>写历史</summary>
    /// <param name="action"></param>
    /// <param name="success"></param>
    /// <param name="remark"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public NodeHistory WriteHistory(String action, Boolean success, String remark, String ip)
    {
        var hi = NodeHistory.Create(this, action, success, remark, Environment.MachineName, ip);
        //hi.SaveAsync();
        hi.Insert();

        return hi;
    }
    #endregion
}