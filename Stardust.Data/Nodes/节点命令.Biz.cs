using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Remoting.Models;
using Stardust.Models;
using XCode;

namespace Stardust.Data.Nodes;

/// <summary>节点命令</summary>
public partial class NodeCommand : Entity<NodeCommand>
{
    #region 对象操作
    static NodeCommand()
    {
        // 累加字段
        //var df = Meta.Factory.AdditionalFields;
        //df.Add(__.NodeID);

        // 过滤器 UserModule、TimeModule、IPModule
        Meta.Modules.Add<UserModule>();
        Meta.Modules.Add<TimeModule>();
        Meta.Modules.Add<IPModule>();
    }

    /// <summary>验证数据，通过抛出异常的方式提示验证失败。</summary>
    /// <param name="isNew">是否插入</param>
    public override void Valid(Boolean isNew)
    {
        // 如果没有脏数据，则不需要进行任何处理
        if (!HasDirty) return;

        this.TrimExtraLong(__.Result);

        base.Valid(isNew);

        if (TraceId.IsNullOrEmpty()) TraceId = DefaultSpan.Current?.TraceId;
    }
    #endregion

    #region 扩展属性
    /// <summary>节点</summary>
    [XmlIgnore, IgnoreDataMember]
    //[ScriptIgnore]
    public Node Node => Extends.Get(nameof(Node), k => Node.FindByID(NodeID));

    /// <summary>节点</summary>
    [XmlIgnore, IgnoreDataMember]
    //[ScriptIgnore]
    [DisplayName("节点")]
    [Map(__.NodeID, typeof(Node), "ID")]
    public String NodeName => Node?.Name;
    #endregion

    #region 扩展查询
    /// <summary>根据编号查找</summary>
    /// <param name="id">编号</param>
    /// <returns>实体对象</returns>
    public static NodeCommand FindById(Int32 id)
    {
        if (id <= 0) return null;

        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.Id == id);

        // 单对象缓存
        return Meta.SingleCache[id];

        //return Find(_.ID == id);
    }

    /// <summary>根据节点、状态查找</summary>
    /// <param name="nodeId">节点</param>
    /// <param name="status">状态</param>
    /// <returns>实体列表</returns>
    public static IList<NodeCommand> FindAllByNodeIDAndStatus(Int32 nodeId, CommandStatus status)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeID == nodeId && e.Status == status);

        return FindAll(_.NodeID == nodeId & _.Status == status);
    }

    /// <summary>根据节点、命令查找</summary>
    /// <param name="nodeId">节点</param>
    /// <param name="command">命令</param>
    /// <returns>实体列表</returns>
    public static IList<NodeCommand> FindAllByNodeIDAndCommand(Int32 nodeId, String command)
    {
        // 实体缓存
        if (Meta.Session.Count < 1000) return Meta.Cache.FindAll(e => e.NodeID == nodeId && e.Command.EqualIgnoreCase(command));

        return FindAll(_.NodeID == nodeId & _.Command == command);
    }
    #endregion

    #region 高级查询
    /// <summary>高级搜索</summary>
    /// <param name="nodeId"></param>
    /// <param name="command"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="key"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    public static IList<NodeCommand> Search(Int32 nodeId, String command, DateTime start, DateTime end, String key, PageParameter page)
    {
        var exp = new WhereExpression();

        if (nodeId > 0) exp &= _.NodeID == nodeId;
        if (!command.IsNullOrEmpty()) exp &= _.Command == command;

        exp &= _.UpdateTime.Between(start, end);

        if (!key.IsNullOrEmpty()) exp &= _.Command == key;

        return FindAll(exp, page);
    }
    #endregion

    #region 业务操作
    /// <summary>获取有效命令</summary>
    /// <param name="nodeId"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static IList<NodeCommand> AcquireCommands(Int32 nodeId, Int32 count = 100)
    {
        var exp = new WhereExpression();
        if (nodeId > 0) exp &= _.NodeID == nodeId;
        exp &= _.Status <= CommandStatus.处理中;

        return FindAll(exp, _.Id.Asc(), null, 0, count);
    }

    /// <summary>添加节点命令</summary>
    /// <param name="node"></param>
    /// <param name="command"></param>
    /// <returns></returns>
    public static NodeCommand Add(Node node, String command)
    {
        var cmd = new NodeCommand
        {
            NodeID = node.ID,
            Command = command,
            CreateTime = DateTime.Now,
        };

        cmd.Insert();

        return cmd;
    }

    /// <summary>转为模型</summary>
    /// <returns></returns>
    public CommandModel ToModel()
    {
        return new CommandModel
        {
            Id = Id,
            Command = Command,
            Argument = Argument,
            Expire = Expire.Year > 2000 ? Expire.ToUniversalTime() : Expire,
            StartTime = StartTime.Year > 2000 ? StartTime.ToUniversalTime() : StartTime,
            TraceId = TraceId,
        };
    }
    #endregion
}