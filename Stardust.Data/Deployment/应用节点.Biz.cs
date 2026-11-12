using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using Stardust.Data.Nodes;
using Stardust.Models;
using XCode;
using XCode.Membership;

namespace Stardust.Data.Deployment;

/// <summary>部署节点。应用和节点服务器的依赖关系</summary>
public partial class AppDeployNode : Entity<AppDeployNode>
{
    #region 对象操作
    static AppDeployNode()
    {
        // 累加字段，生成 Update xx Set Count=Count+1234 Where xxx
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(nameof(AppId));

        // 过滤器 UserInterceptor、TimeInterceptor、IPInterceptor
        Meta.Interceptors.Add<UserInterceptor>();
        Meta.Interceptors.Add<TimeInterceptor>();
        Meta.Interceptors.Add<IPInterceptor>();
        Meta.Interceptors.Add<TraceInterceptor>();
    }

    /// <summary>验证并修补数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        if (DeployId <= 0) throw new ArgumentNullException(nameof(DeployId));
        if (NodeId <= 0) throw new ArgumentNullException(nameof(NodeId));

        var len = _.ProcessName.Length;
        if (len > 0 && !ProcessName.IsNullOrEmpty() && ProcessName.Length > len) ProcessName = ProcessName[..len];

        len = _.IP.Length;
        if (len > 0 && !IP.IsNullOrEmpty() && IP.Length > len)
        {
            // 取前三个
            var ss = IP.Split(',');
            IP = ss.Take(3).Join(",");
            if (IP.Length > len) IP = IP[..len];
        }

        len = _.Listens.Length;
        if (len > 0 && !Listens.IsNullOrEmpty() && Listens.Length > len) Listens = Listens[..len];

        base.Valid(isNew);
    }
    #endregion

    #region 扩展属性
    /// <summary>节点</summary>
    [XmlIgnore, ScriptIgnore, IgnoreDataMember]
    public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeId));

    /// <summary>节点</summary>
    [Map(__.NodeId)]
    public String NodeName => Node?.Name;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static AppDeployNode FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.Id == id);
    }

    /// <summary>根据应用查找</summary>
    /// <param name="appId">应用</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployNode> FindAllByAppId(Int32 appId)
    {
        if (appId <= 0) return new List<AppDeployNode>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == appId);

        return FindAll(_.DeployId == appId);
    }

    /// <summary>根据节点查找</summary>
    /// <param name="nodeId">节点</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployNode> FindAllByNodeId(Int32 nodeId)
    {
        //// 实体缓存
        //if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeId == nodeId);

        return FindAll(_.NodeId == nodeId);
    }

    /// <summary>根据应用部署集查找</summary>
    /// <param name="deployId">应用部署集</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployNode> FindAllByDeployId(Int32 deployId)
    {
        if (deployId <= 0) return new List<AppDeployNode>();

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.DeployId == deployId);

        return FindAll(_.DeployId == deployId);
    }
    #endregion

    #region 高级查询
    /// <summary>高级查询</summary>
    /// <param name="appId">应用。原始应用</param>
    /// <param name="nodeId">节点。节点服务器</param>
    /// <param name="enable"></param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns>实体列表</returns>
    public static IList<AppDeployNode> Search(Int32 appId, Int32 nodeId, Boolean? enable, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (appId >= 0) exp &= _.DeployId == appId;
        if (nodeId >= 0) exp &= _.NodeId == nodeId;
        if (enable != null) exp &= _.Enable == enable;
        if (!key.IsNullOrEmpty()) exp &= _.CreateIP.Contains(key);

        return FindAll(exp, page);
    }

    /// <summary>高级查询</summary>
    /// <param name="setIds">部署集 Id 数组。AppDeployNode.DeployId 指向部署集而非应用；按应用查询请先经 AppDeploy.FindAllByAppId 换取部署集 Id</param>
    /// <param name="nodeId">节点。节点服务器</param>
    /// <param name="enable"></param>
    /// <param name="key">关键字</param>
    /// <param name="page">分页参数信息。可携带统计和数据权限扩展查询等信息</param>
    /// <returns></returns>
    public static IList<AppDeployNode> Search(Int32[] setIds, Int32 nodeId, Boolean? enable, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (setIds != null && setIds.Length > 0) exp &= _.DeployId.In(setIds);
        if (nodeId >= 0) exp &= _.NodeId == nodeId;
        if (enable != null) exp &= _.Enable == enable;
        if (!key.IsNullOrEmpty()) exp &= _.CreateIP.Contains(key);

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>修正旧版用户名数据</summary>
    /// <returns></returns>
    public Int32 FixOldUserName()
    {
        // 兼容旧数据
        var user = UserName;
        if (user.IsNullOrEmpty()) return 0;

        // Windows 用户名
        if (user != "$" && user != "$$" && user[^1] == '$')
        {
            ProcessUser = user;
            UserName = null;
            return Update();
        }

        if (user == Deploy?.UserName)
        {
            // 如果和应用的用户名相同，则不设置用户名
            ProcessUser = user;
            UserName = null;
            return Update();
        }

        return 0;
    }

    #region 写入拦截（D3：实体层落库后回修所属部署集节点计数）
    /// <summary>插入后回修所属部署集的节点计数</summary>
    public override Int32 Insert()
    {
        var rs = base.Insert();

        Deploy?.Fix();

        return rs;
    }

    /// <summary>更新后回修所属部署集的节点计数</summary>
    public override Int32 Update()
    {
        var rs = base.Update();

        Deploy?.Fix();

        return rs;
    }

    /// <summary>删除后回修所属部署集的节点计数</summary>
    public override Int32 Delete()
    {
        var rs = base.Delete();

        Deploy?.Fix();

        return rs;
    }
    #endregion

    /// <summary>
    /// 转应用服务信息
    /// </summary>
    /// <returns></returns>
    public ServiceInfo ToService(AppDeploy app)
    {
        app ??= Deploy;
        if (app == null) return null;

        var inf = new ServiceInfo
        {
            Name = DeployName,
            FileName = FileName,
            Arguments = Arguments,
            Environments = Environments,
            WorkingDirectory = WorkingDirectory,
            UserName = UserName,

            Enable = app.Enable && Enable,
            //AutoStart = app.AutoStart,
            AutoStop = app.AutoStop,
            ReloadOnChange = app.ReloadOnChange,
            MaxMemory = MaxMemory,
            Priority = Priority,
            Mode = Mode,
            AllowMultiple = app.AllowMultiple,
            HealthCheck = HealthCheck,
        };

        if (inf.Name.IsNullOrEmpty()) inf.Name = app.Name;
        if (inf.FileName.IsNullOrEmpty()) inf.FileName = app.FileName;
        if (inf.Arguments.IsNullOrEmpty()) inf.Arguments = app.Arguments;
        if (inf.Environments.IsNullOrEmpty()) inf.Environments = app.Environments;
        if (inf.WorkingDirectory.IsNullOrEmpty()) inf.WorkingDirectory = app.WorkingDirectory;
        if (inf.UserName.IsNullOrEmpty()) inf.UserName = app.UserName;
        if (inf.MaxMemory <= 0) inf.MaxMemory = app.MaxMemory;
        if (inf.Priority == 0) inf.Priority = app.Priority;
        if (inf.Mode <= DeployMode.Default) inf.Mode = app.Mode;
        if (inf.HealthCheck.IsNullOrEmpty()) inf.HealthCheck = app.HealthCheck;

        return inf;
    }

    /// <summary>合并后的工作目录（节点覆盖部署集）</summary>
    public String GetEffectiveWorkingDirectory()
    {
        var app = Deploy;
        if (!WorkingDirectory.IsNullOrEmpty()) return WorkingDirectory;
        return app?.WorkingDirectory;
    }

    /// <summary>合并后的日志目录（节点覆盖部署集）</summary>
    public String GetEffectiveLogDirectory()
    {
        var app = Deploy;
        if (!LogDirectory.IsNullOrEmpty()) return LogDirectory;
        return app?.LogDirectory;
    }

    /// <summary>合并后的日志文件匹配（节点覆盖部署集）</summary>
    public String GetEffectiveLogFilePattern()
    {
        var app = Deploy;
        if (!LogFilePattern.IsNullOrEmpty()) return LogFilePattern;
        if (!app?.LogFilePattern.IsNullOrEmpty() == true) return app.LogFilePattern;
        return "*.log";
    }

    public void Fill(AppInfo inf)
    {
        ProcessId = inf.Id;
        ProcessName = inf.Name;
        ProcessUser = inf.UserName;
        // 应用程序版本：客户端上报的应用自身文件版本（Deploy/Ping 代上报链路当前不带版本，内嵌 SDK 链路有值）
        if (!inf.Version.IsNullOrEmpty()) AppVersion = inf.Version;
        StartTime = inf.StartTime;
        Listens = inf.Listens;
    }

    public void Fill(AppOnline online)
    {
        IP = online.IP;
        ProcessId = online.ProcessId;
        ProcessName = online.ProcessName;
        ProcessUser = online.UserName;
        StartTime = online.StartTime;
        Listens = online.Listens;
        // 应用程序版本：登录时登记的应用自身文件版本（RegistryService.OnRegister 写入 AppOnline.Version）。
        // 2026-01-15 2d70fa91 曾注释掉「Version = online.Version」——版本字段改义为部署版本后不再回填客户端版本；
        // 应用自身版本改由 AppVersion 承载，与部署版本（Version，install 下发时回填）语义分开。
        if (!online.Version.IsNullOrEmpty()) AppVersion = online.Version;
        Compile = online.Compile;
        LastActive = online.UpdateTime;
    }
    #endregion
}