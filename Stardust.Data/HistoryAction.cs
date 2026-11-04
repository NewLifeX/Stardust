using System;
using System.Collections.Generic;
using NewLife;

namespace Stardust.Data;

/// <summary>历史操作名。数据库保存英文规范键，展示时按当前界面语言翻译</summary>
/// <remarks>
/// 应用历史与节点历史由管理端、服务端、代理和客户端分别写入，写入进程的界面语言不可控。
/// 若在写入时翻译，同一操作会因写入进程语言不同而在库里出现多种文本。
/// 因此操作名统一保存英文规范键，由管理端展示时经 <see cref="Localize"/> 翻译；
/// 旧数据中的中文操作名先经 <see cref="Normalize"/> 归一化，再参与翻译与筛选。
/// </remarks>
public static class HistoryAction
{
    #region 应用历史
    /// <summary>应用鉴权</summary>
    public const String ApplicationAuthentication = "Application authentication";

    /// <summary>应用注册</summary>
    public const String ApplicationRegistered = "Application registered";

    /// <summary>应用重注册</summary>
    public const String ApplicationReRegistered = "Application re-registered";

    /// <summary>关联上线</summary>
    public const String AssociatedOnline = "Associated online";

    /// <summary>超时下线</summary>
    public const String TimedOutOffline = "Timed out offline";

    /// <summary>注册服务</summary>
    public const String RegisterService = "RegisterService";

    /// <summary>注销服务</summary>
    public const String UnregisterService = "UnregisterService";

    /// <summary>发现服务</summary>
    public const String ResolveService = "ResolveService";

    /// <summary>OAuth 授权</summary>
    public const String Authorize = "Authorize";

    /// <summary>刷新令牌</summary>
    public const String RefreshToken = "RefreshToken";

    /// <summary>获取部署信息</summary>
    public const String GetAll = "GetAll";

    /// <summary>上传部署信息</summary>
    public const String Upload = "Upload";

    /// <summary>上传编译文件</summary>
    public const String UploadBuildFile = "UploadBuildFile";

    /// <summary>应用启动</summary>
    public const String AppStart = "AppStart";

    /// <summary>WebSocket 连接</summary>
    public const String WebSocketConnected = "WebSocket connected";

    /// <summary>WebSocket 断开</summary>
    public const String WebSocketDisconnected = "WebSocket disconnected";

    /// <summary>WebSocket 发送</summary>
    public const String WebSocketSend = "WebSocket send";
    #endregion

    #region 节点历史
    /// <summary>节点鉴权</summary>
    public const String NodeAuthentication = "Node authentication";

    /// <summary>动态注册</summary>
    public const String DynamicRegistration = "Dynamic registration";

    /// <summary>登录校验</summary>
    public const String LoginValidation = "Login validation";

    /// <summary>匹配已有节点</summary>
    public const String MatchedExistingNode = "Matched existing node";

    /// <summary>自动更新</summary>
    public const String AutoUpdate = "Auto update";

    /// <summary>推送 dotNet</summary>
    public const String PushDotNet = "Push dotNet";

    /// <summary>修改域名</summary>
    public const String DomainChanged = "Domain changed";

    /// <summary>应用添加节点</summary>
    public const String AppAddNode = "AppAddNode";

    /// <summary>代理自愈</summary>
    public const String Rescue = "Rescue";

    /// <summary>代理升级</summary>
    public const String Upgrade = "Upgrade";

    /// <summary>同步时间</summary>
    public const String SyncTime = "SyncTime";

    /// <summary>执行 Bash</summary>
    public const String RunBash = "RunBash";

    /// <summary>执行命令</summary>
    public const String RunCmd = "RunCmd";

    /// <summary>删除文件</summary>
    public const String Delete = "Delete";

    /// <summary>本地探测</summary>
    public const String LocalProbe = "LocalProbe";

    /// <summary>服务控制</summary>
    public const String ServiceController = "ServiceController";
    #endregion

    #region 属性
    /// <summary>变更类操作。看板“最近变更”只展示这些动作，过滤心跳、WebSocket 与鉴权日志</summary>
    private static readonly String[] _changeActions =
    [
        ApplicationRegistered, ApplicationReRegistered, AssociatedOnline, TimedOutOffline, AppStart,
        RegisterService, UnregisterService, AutoUpdate, Upgrade, Rescue, DomainChanged,
    ];

    private static readonly Dictionary<String, String> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // 英文规范键统一大小写，避免客户端传入小写变体后出现重复分组
        [ApplicationAuthentication] = ApplicationAuthentication,
        [ApplicationRegistered] = ApplicationRegistered,
        [ApplicationReRegistered] = ApplicationReRegistered,
        [AssociatedOnline] = AssociatedOnline,
        [TimedOutOffline] = TimedOutOffline,
        [RegisterService] = RegisterService,
        [UnregisterService] = UnregisterService,
        [ResolveService] = ResolveService,
        [Authorize] = Authorize,
        [RefreshToken] = RefreshToken,
        [GetAll] = GetAll,
        [Upload] = Upload,
        [UploadBuildFile] = UploadBuildFile,
        [AppStart] = AppStart,
        [WebSocketConnected] = WebSocketConnected,
        [WebSocketDisconnected] = WebSocketDisconnected,
        [WebSocketSend] = WebSocketSend,
        [NodeAuthentication] = NodeAuthentication,
        [DynamicRegistration] = DynamicRegistration,
        [LoginValidation] = LoginValidation,
        [MatchedExistingNode] = MatchedExistingNode,
        [AutoUpdate] = AutoUpdate,
        [PushDotNet] = PushDotNet,
        [DomainChanged] = DomainChanged,
        [AppAddNode] = AppAddNode,
        [Rescue] = Rescue,
        [Upgrade] = Upgrade,
        [SyncTime] = SyncTime,
        [RunBash] = RunBash,
        [RunCmd] = RunCmd,
        [Delete] = Delete,
        [LocalProbe] = LocalProbe,
        [ServiceController] = ServiceController,

        // 旧版本在写入时翻译成中文，历史数据需要等价归一化
        ["应用鉴权"] = ApplicationAuthentication,
        ["应用注册"] = ApplicationRegistered,
        ["应用重注册"] = ApplicationReRegistered,
        ["关联上线"] = AssociatedOnline,
        ["超时下线"] = TimedOutOffline,
        ["应用下线"] = TimedOutOffline,
        ["节点鉴权"] = NodeAuthentication,
        ["节点下线"] = TimedOutOffline,
        ["动态注册"] = DynamicRegistration,
        ["登录校验"] = LoginValidation,
        ["匹配已有节点"] = MatchedExistingNode,
        ["自动更新"] = AutoUpdate,
        ["推送dotNet"] = PushDotNet,
        ["修改域名"] = DomainChanged,
        ["WebSocket连接"] = WebSocketConnected,
        ["WebSocket断开"] = WebSocketDisconnected,
        ["WebSocket发送"] = WebSocketSend,
    };
    #endregion

    #region 方法
    /// <summary>把历史记录中的操作名归一化为英文规范键</summary>
    /// <param name="action">数据库中的操作名，可能是英文规范键或旧版本写入的中文文本</param>
    /// <returns>英文规范键；未知操作名原样返回</returns>
    public static String Normalize(String action)
    {
        if (action.IsNullOrEmpty()) return action;

        var key = action.Trim();
        return _aliases.TryGetValue(key, out var value) ? value : key;
    }

    /// <summary>按当前界面语言翻译操作名</summary>
    /// <param name="action">数据库中的操作名</param>
    /// <returns>当前界面语言的操作名；未知操作名原样返回</returns>
    public static String Localize(String action)
    {
        var key = Normalize(action);
        return key.IsNullOrEmpty() ? key : L.T(key);
    }

    /// <summary>判断是否变更类操作</summary>
    /// <param name="action">数据库中的操作名</param>
    /// <returns>是否为应用上下线、注册、升级等变更动作</returns>
    public static Boolean IsChange(String action)
    {
        var key = Normalize(action);
        if (key.IsNullOrEmpty()) return false;

        foreach (var item in _changeActions)
        {
            if (item.EqualIgnoreCase(key)) return true;
        }

        return false;
    }
    #endregion
}
