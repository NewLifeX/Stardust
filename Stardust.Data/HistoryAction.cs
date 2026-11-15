using System;
using NewLife;

namespace Stardust.Data;

/// <summary>历史操作名。数据库保存英文规范键，展示时按当前界面语言翻译</summary>
/// <remarks>
/// 应用历史与节点历史由管理端、服务端、代理和客户端分别写入，写入进程的界面语言不可控。
/// 若在写入时翻译，同一操作会因写入进程语言不同而在库里出现多种文本。
/// 因此操作名统一保存英文规范键，由管理端展示时经 <see cref="Localize"/> 翻译。
/// 历史表按保留期定期清理，旧版本写入的中文操作名不做归一化兼容，按库中原样展示直至清空。
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

    /// <summary>设备下线</summary>
    public const String DeviceOffline = "Device offline";

    /// <summary>设备事件。设备上报的事件没有名称时使用</summary>
    public const String DeviceEvent = "Device event";

    /// <summary>下线超时</summary>
    public const String OfflineTimeout = "Offline timeout";

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

    /// <summary>标识不一致。客户端上报的唯一标识与登记信息不符</summary>
    public const String IdentityMismatch = "Identity mismatch";

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

    /// <summary>时钟偏差。本机时间与服务端相差过大，鉴权可能失败，需要运维校正 NTP</summary>
    public const String ClockSkew = "Clock skew";

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

    /// <summary>重启节点</summary>
    public const String Restart = "Restart";
    #endregion

    #region 属性
    /// <summary>变更类操作。看板“最近变更”只展示这些动作，过滤心跳、WebSocket 与鉴权日志</summary>
    private static readonly String[] _changeActions =
    [
        ApplicationRegistered, ApplicationReRegistered, AssociatedOnline, DeviceOffline, OfflineTimeout, AppStart,
        RegisterService, UnregisterService, AutoUpdate, Upgrade, Rescue, DomainChanged,
    ];
    #endregion

    #region 方法
    /// <summary>按当前界面语言翻译操作名</summary>
    /// <param name="action">数据库中的操作名，必须是英文规范键；未知操作名原样返回</param>
    /// <returns>当前界面语言的操作名</returns>
    public static String Localize(String action)
    {
        if (action.IsNullOrEmpty()) return action;

        return L.T(action);
    }

    /// <summary>连接类操作。WebSocket 连接/断开数量大，历史列表默认不展示，按操作筛选时可查看</summary>
    public static readonly String[] ConnectionActions = [WebSocketConnected, WebSocketDisconnected, WebSocketSend];

    /// <summary>判断是否变更类操作</summary>
    /// <param name="action">数据库中的操作名</param>
    /// <returns>是否为应用上下线、注册、升级等变更动作</returns>
    public static Boolean IsChange(String action)
    {
        if (action.IsNullOrEmpty()) return false;

        foreach (var item in _changeActions)
        {
            if (item.EqualIgnoreCase(action)) return true;
        }

        return false;
    }
    #endregion
}
