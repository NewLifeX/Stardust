using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Extensions.Services;
using NewLife.Remoting.Models;
using NewLife.Remoting.Services;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.Models;
using XCode;
using XCode.Configuration;

namespace Stardust.Server.Services;

/// <summary>节点服务。处理 StarAgent 节点的注册登录、心跳保活、在线状态管理和命令下发</summary>
public class NodeService : DefaultDeviceService<Node, NodeOnline>
{
    private readonly ITokenService _tokenService;
    private readonly IPasswordProvider _passwordProvider;
    private readonly StarServerSetting _setting;
    private readonly NodeSessionManager _sessionManager;
    private readonly ICacheProvider _cacheProvider;
    private readonly ITracer _tracer;
    private readonly DnsService _dnsService;

    /// <summary>实例化节点服务</summary>
    /// <param name="tokenService">令牌服务</param>
    /// <param name="passwordProvider">密码提供者</param>
    /// <param name="setting">服务端设置</param>
    /// <param name="sessionManager">节点会话管理器</param>
    /// <param name="cacheProvider">缓存提供者</param>
    /// <param name="tracer">跟踪器</param>
    /// <param name="dnsService">DDNS 服务</param>
    /// <param name="serviceProvider">服务提供者</param>
    public NodeService(ITokenService tokenService, IPasswordProvider passwordProvider, StarServerSetting setting, NodeSessionManager sessionManager, ICacheProvider cacheProvider, ITracer tracer, DnsService dnsService, IServiceProvider serviceProvider) : base(sessionManager, passwordProvider, cacheProvider, serviceProvider)
    {
        _tokenService = tokenService;
        _passwordProvider = passwordProvider;
        _setting = setting;
        _sessionManager = sessionManager;
        _cacheProvider = cacheProvider;
        _tracer = tracer;
        _dnsService = dnsService;

        Name = "Node";
    }

    #region 登录注销
    /// <summary>验证设备合法性</summary>
    public override Boolean Authorize(DeviceContext context, ILoginRequest request)
    {
        if (context.Device is not Node node) return false;

        using var span = _tracer?.NewSpan($"{Name}Authorize", new { request.Code, request.ClientId });

        var inf = request as LoginInfo;
        var ip = context.UserHost;
        node = CheckNode(node, inf.Node, inf.ProductCode, ip, _setting.NodeCodeLevel);
        if (node == null)
        {
            WriteHistory(node, "节点鉴权", false, "硬件信息变动过大", ip);
            return false;
        }

        // 没有密码时无需验证
        if (node.Secret.IsNullOrEmpty()) return true;
        if (node.Secret.EqualIgnoreCase(request.Secret)) return true;

        if (_setting.SaltTime > 0 && _passwordProvider is SaltPasswordProvider saltProvider)
        {
            // 使用盐值偏差时间，允许客户端时间与服务端时间有一定偏差
            saltProvider.SaltTime = _setting.SaltTime;
        }
        if (request.Secret.IsNullOrEmpty() || !_passwordProvider.Verify(node.Secret, request.Secret))
        {
            WriteHistory(context, "节点鉴权", false, "密钥校验失败");
            return false;
        }

        return true;
    }

    /// <summary>自动注册</summary>
    /// <param name="context"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public override IDeviceModel Register(DeviceContext context, ILoginRequest request)
    {
        using var span = _tracer?.NewSpan($"{Name}Register", new { request.Code, request.ClientId });

        var inf = request as LoginInfo;
        var node = context.Device as Node ?? QueryDevice(request.Code) as Node;
        var ip = context.UserHost;

        try
        {
            // 校验唯一编码，防止客户端拷贝配置
            if (node == null)
            {
                node = AutoRegister(null, inf, ip, _setting);
            }
            else
            {
                //// 登录密码未设置或者未提交，则执行动态注册
                //if (node == null || node.Secret.IsNullOrEmpty() || secret.IsNullOrEmpty())
                //    node = AutoRegister(node, inf, ip, setting);
                //else if (node.Secret.MD5() != secret)
                //    node = AutoRegister(node, inf, ip, setting);
                //else if (setting.NodeCodeLevel > 0)
                //    node = AutoRegister(node, inf, ip, setting);
                node = AutoRegister(node, inf, ip, _setting);
            }

            node?.WriteHistory("动态注册", true, inf.ToJson(false, false, false), ip);
        }
        catch (Exception ex)
        {
            span?.SetError(ex, null);

            node?.WriteHistory("动态注册", false, inf.ToJson(false, false, false), ip);

            throw;
        }

        return node;
    }

    /// <summary>鉴权后的登录处理。修改设备信息、创建在线记录和写日志</summary>
    /// <param name="context">上下文</param>
    /// <param name="request">登录请求</param>
    public override void OnLogin(DeviceContext context, ILoginRequest request)
    {
        using var span = _tracer?.NewSpan($"{Name}OnLogin", new { request.Code, request.ClientId });

        var node = context.Device as Node;
        var inf = request as LoginInfo;
        var ip = context.UserHost;
        if (!inf.ProductCode.IsNullOrEmpty()) node.ProductCode = inf.ProductCode;

        // 设置默认项目
        if (node.ProjectId == 0 || node.ProjectName == "默认")
        {
            var project = GalaxyProject.FindByName(inf.Project);
            if (project != null)
                node.ProjectId = project.Id;
        }

        node.UpdateIP = ip;
        node.FixNameByRule();

        // 记录旧IP，用于DDNS检测（Login会更新LastLoginIP）
        var oldIp = node.LastLoginIP;

        node.Login(inf.Node, ip);

        var online = context.Online = GetOnline(context) ?? CreateOnline(context);
        if (online is NodeOnline olt && inf.Node != null) olt.Fill(inf.Node);

        //// 设置令牌
        //var tokenModel = tokenService.IssueToken(node.Code, inf.ClientId);

        //// 在线记录
        //var olt = GetOrAddOnline(node, tokenModel.AccessToken, ip);
        //olt.Save(inf.Node, null, tokenModel.AccessToken, ip);

        // 登录历史
        WriteHistory(context, "节点鉴权", true, $"[{node.Name}/{node.Code}]鉴权成功 " + inf.ToJson(false, false, false));

        // 检查节点上线恢复
        NodeOnlineService.CheckOnline(node);

        // DDNS检测。节点上线时检测IP变化并更新DNS记录
        _ = _dnsService.CheckNodeIPChange(node, ip, oldIp);
    }

    /// <summary>注销</summary>
    /// <param name="reason">注销原因</param>
    /// <param name="ip">IP地址</param>
    /// <returns></returns>
    public override IOnlineModel? Logout(DeviceContext context, String? reason, String source)
    {
        using var span = _tracer?.NewSpan($"{Name}Logout", new { context.Code, context.ClientId, reason, source });

        //var node = context.Device as Node;
        //var ip = context.UserHost;
        //var online = GetOnline(node, ip);
        //if (online == null) return null;

        //var msg = $"{reason} [{node}]]登录于{online.CreateTime}，最后活跃于{online.UpdateTime}";
        //node.WriteHistory("节点下线", true, msg, ip);
        //online.Delete();

        //RemoveOnline(node);

        //// 计算在线时长
        //if (online.CreateTime.Year > 2000)
        //{
        //    node.OnlineTime += (Int32)(DateTime.Now - online.CreateTime).TotalSeconds;
        //    node.Update();
        //}

        //NodeOnlineService.CheckOffline(node, "注销");

        var online = base.Logout(context, reason, source);
        if (online is NodeOnline online2 && context.Device is Node node)
        {
            // 计算在线时长
            if (online2.CreateTime.Year > 2000)
            {
                node.OnlineTime += (Int32)(DateTime.Now - online2.CreateTime).TotalSeconds;
                node.Update();
            }

            NodeOnlineService.CheckOffline(node, "注销");
        }

        return online;
    }

    /// <summary>校验节点信息，如果大量不一致则认为是新节点</summary>
    /// <param name="node"></param>
    /// <param name="di"></param>
    /// <param name="productCode"></param>
    /// <param name="ip"></param>
    /// <param name="minLevel"></param>
    /// <returns></returns>
    private static Node CheckNode(Node node, NodeInfo di, String productCode, String ip, Int32 minLevel)
    {
        // 校验唯一编码，防止客户端拷贝配置
        var uuid = di.UUID;
        var guid = di.MachineGuid;
        var serial = di.SerialNumber;
        var board = di.Board;
        var diskid = di.DiskID;

        // 自动截断，避免超长导致对比时不一致
        uuid = TrimLength(uuid, Node._.Uuid);
        guid = TrimLength(guid, Node._.MachineGuid);
        serial = TrimLength(serial, Node._.SerialNumber);
        board = TrimLength(board, Node._.Board);
        diskid = TrimLength(diskid, Node._.DiskID);

        var level = 5;
        if (!uuid.IsNullOrEmpty() && uuid != node.Uuid)
        {
            node.WriteHistory("登录校验", false, $"唯一标识不符！（新!=旧）{uuid}!={node.Uuid}", ip);
            level--;
        }
        if (!guid.IsNullOrEmpty() && guid != node.MachineGuid)
        {
            node.WriteHistory("登录校验", false, $"机器标识不符！（新!=旧）{guid}!={node.MachineGuid}", ip);
            level--;
        }
        if (!serial.IsNullOrEmpty() && serial != node.SerialNumber)
        {
            node.WriteHistory("登录校验", false, $"计算机序列号不符！（新!=旧）{serial}!={node.SerialNumber}", ip);
            level--;
        }
        if (!board.IsNullOrEmpty() && board != node.Board)
        {
            node.WriteHistory("登录校验", false, $"主板不符！（新!=旧）{board}!={node.Board}", ip);
            //flag = false;
        }
        //if (!diskid.IsNullOrEmpty() && diskid != node.DiskID)
        //{
        //    node.WriteHistory("登录校验", false, $"磁盘序列号不符！（新!=旧）{diskid}!={node.DiskID}", ip);
        //    level--;
        //}
        if (!node.ProductCode.IsNullOrEmpty() && !productCode.IsNullOrEmpty() && !node.ProductCode.EqualIgnoreCase(productCode))
        {
            node.WriteHistory("登录校验", false, $"产品编码不符！（新!=旧）{productCode}!={node.ProductCode}", ip);
            //level--;
        }

        // 机器名
        if (di.MachineName != node.MachineName)
        {
            node.WriteHistory("登录校验", false, $"机器名不符！（新!=旧）{di.MachineName}!={node.MachineName}", ip);
        }

        // 网卡地址
        if (di.Macs != node.MACs)
        {
            var dims = di.Macs?.Split(",") ?? [];
            var nodems = node.MACs?.Split(",") ?? [];
            // 任意匹配则通过
            if (!nodems.Any(e => dims.Contains(e)))
            {
                node.WriteHistory("登录校验", false, $"网卡地址不符！（新!=旧）{di.Macs}!={node.MACs}", ip);
                level--;
            }
        }

        // 磁盘。可能有TF卡和U盘
        if (diskid != node.DiskID)
        {
            var dims = diskid?.Split(",") ?? [];
            var nodems = node.DiskID?.Split(",") ?? [];
            // 任意匹配则通过
            if (!nodems.Any(e => dims.Contains(e)))
            {
                node.WriteHistory("登录校验", false, $"磁盘序列号不符！（新!=旧）{diskid}!={node.DiskID}", ip);
                level--;
            }
        }

        if (level < minLevel) return null;

        return node;
    }

    private static String TrimLength(String value, FieldItem field)
    {
        if (value.IsNullOrEmpty()) return value;
        if (field.Length <= 0 || value.Length < field.Length) return value;

        return value[..field.Length];
    }

    private Node AutoRegister(Node node, LoginInfo inf, String ip, StarServerSetting set)
    {
        if (!set.AutoRegister) throw new ApiException(ApiCode.Forbidden, "禁止自动注册");

        // 检查白名单
        //var ip = UserHost;
        if (!IsMatchWhiteIP(set.WhiteIP, ip)) throw new ApiException(ApiCode.Forbidden, "非法来源，禁止注册");

        var di = inf.Node;
        using var span = _tracer?.NewSpan(nameof(AutoRegister), new { inf.ProductCode, di.UUID, di.MachineGuid, di.Macs, di.DiskID });

        var code = BuildCode(di, inf.ProductCode, set);
        if (code.IsNullOrEmpty()) code = Rand.NextString(8);
        span?.AppendTag($"code={code}");

        // 如果节点编码有改变，则倾向于新建节点
        if (node == null || node.Code != code) node = Node.FindByCode(code);

        if (node == null)
        {
            node = QueryByInfo(inf.ProductCode, di, set.NodeCodeLevel).FirstOrDefault();
            if (node != null)
            {
                var msg = $"检测到节点[{inf.Code}/{di.Macs}]与旧节点[{node.Code}]高度相似，选择使用旧节点";
                XTrace.WriteLine(msg);
                span?.AppendTag(msg);
                node.WriteHistory("匹配已有节点", true, msg, ip);
            }
        }

        var name = "";
        if (name.IsNullOrEmpty()) name = di.MachineName;
        if (name.IsNullOrEmpty()) name = di.UserName;

        node ??= new Node
        {
            Enable = true,

            CreateIP = ip,
            CreateTime = DateTime.Now,
        };

        // 如果未打开动态注册，则把节点修改为禁用
        node.Enable = set.AutoRegister;

        if (node.Name.IsNullOrEmpty()) node.Name = name;

        // 优先使用节点散列来生成节点证书，确保节点路由到其它接入网关时保持相同证书代码
        node.Code = code;

        node.Secret = Rand.NextString(16);
        node.UpdateIP = ip;
        node.UpdateTime = DateTime.Now;

        node.Save();
        //autoReg = true;

        // 第一个注册的StarAgent提高采样频率，便于测试和演示
        if (node.ID == 1)
        {
            node.Period = 5;
            node.Update();
        }

        return node;
    }

    public static IList<Node> QueryByInfo(String productCode, NodeInfo di, Int32 level)
    {
        // 该硬件的所有节点信息
        var list = Node.SearchAny(di.UUID, di.MachineGuid, di.Macs, di.SerialNumber, di.DiskID);

        // 当前节点信息，取较老者
        list = list.Where(e => e.ProductCode.IsNullOrEmpty() || e.ProductCode == productCode).OrderBy(e => e.ID).ToList();

        // 找到节点
        //node ??= list.FirstOrDefault();
        // 节点编码辨识度。UUID+Guid+SerialNumber+DiskId+MAC，只要其中几个相同，就认为是同一个节点，默认2
        //var level = set.NodeCodeLevel;
        if (level <= 0) level = 2;

        // 按匹配度排序，匹配度越高，越靠前。注意不同节点的匹配度可能相同。
        var rs = new MySortedList<Int32, Node>();
        foreach (var node in list)
        {
            var n = 0;
            if (!di.UUID.IsNullOrEmpty() && node.Uuid == di.UUID) n++;
            if (!di.MachineGuid.IsNullOrEmpty() && node.MachineGuid == di.MachineGuid) n++;
            //if (!di.Macs.IsNullOrEmpty() && node.MACs == di.Macs) n++;
            if (!di.SerialNumber.IsNullOrEmpty() && node.SerialNumber == di.SerialNumber) n++;
            //if (!di.DiskID.IsNullOrEmpty() && node.DiskID == di.DiskID) n++;

            // 网卡地址
            if (di.Macs == node.MACs)
                n++;
            else
            {
                var dims = di.Macs?.Split(",") ?? [];
                var nodems = node.MACs?.Split(",") ?? [];
                if (dims != null && nodems != null) n += dims.Count(e => nodems.Contains(e));
            }

            // 磁盘。可能有TF卡和U盘
            var diskid = di.DiskID;
            if (diskid == node.DiskID)
                n++;
            else
            {
                var dims = diskid?.Split(",") ?? [];
                var nodems = node.DiskID?.Split(",") ?? [];
                //if (dims != null && nodems != null) n += dims.Count(e => nodems.Contains(e));
                // 在虚拟机云服务器中，磁盘序列化可能大范围一致，因此只计算一个匹配
                if (dims != null && nodems != null && dims.Any(e => nodems.Contains(e))) n++;
            }

            if (n >= level) rs.Add(10 - n, node);
        }

        return rs.Values;
    }

    /// <summary>
    /// 是否匹配白名单，未设置则直接通过
    /// </summary>
    /// <param name="whiteIp"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    private static Boolean IsMatchWhiteIP(String whiteIp, String ip)
    {
        if (ip.IsNullOrEmpty()) return true;
        if (whiteIp.IsNullOrEmpty()) return true;

        var ss = whiteIp.Split(",");
        foreach (var item in ss)
        {
            if (item.IsMatch(ip)) return true;
        }

        return false;
    }

    private String BuildCode(NodeInfo di, String productCode, StarServerSetting set)
    {
        using var span = _tracer?.NewSpan(nameof(BuildCode), new { set.NodeCodeFormula });

        //var set = Setting.Current;
        //var uid = $"{di.UUID}@{di.MachineGuid}@{di.Macs}";
        var ss = (set.NodeCodeFormula + "").Split('(', ')');
        if (ss.Length >= 2)
        {
            var uid = ss[1];
            uid = uid.Replace("{ProductCode}", productCode);
            foreach (var pi in di.GetType().GetProperties())
            {
                uid = uid.Replace($"{{{pi.Name}}}", pi.GetValue(di) + "");
            }
            span?.AppendTag($"uid={uid}");

            var p1 = uid.IndexOf('{');
            if (p1 > 0)
            {
                var p2 = uid.IndexOf('}', p1 + 1);
                if (p2 > 0)
                {
                    // 有些标识符刚好带有大括号，导致误判以为是未解析变量
                    var len = p2 - p1 - 1;
                    if (len >= 2 && len < 10)
                        XTrace.WriteLine("节点编码公式有误，存在未解析变量，uid={0}", uid);
                }
            }
            //if (uid.Contains('{') || uid.Contains('}')) XTrace.WriteLine("节点编码公式有误，存在未解析变量，uid={0}", uid);
            if (!uid.IsNullOrEmpty())
            {
                XTrace.WriteLine("生成节点编码 uid={0} alg={1}", uid, ss[0]);

                // 使用产品类别加密一下，确保不同类别有不同编码
                var buf = uid.GetBytes();
                //code = buf.Crc().GetBytes().ToHex();
                switch (ss[0].ToLower())
                {
                    case "crc": buf = buf.Crc().GetBytes(); break;
                    case "crc16": buf = buf.Crc16().GetBytes(); break;
                    case "md5": buf = buf.MD5(); break;
                    case "md5_16": buf = uid.MD5_16().ToHex(); break;
                    default:
                        break;
                }
                var code = buf.ToHex();
                span?.AppendTag($"code={code}");

                return code;
            }
        }

        return null;
    }
    #endregion

    #region 心跳保活
    public override IOnlineModel OnPing(DeviceContext context, IPingRequest? request)
    {
        if (context.Device is not Node node) return null;

        using var span = _tracer?.NewSpan($"{Name}OnPing", new { context.Code, context.ClientId });

        var inf = request as PingInfo;
        if (!inf.IP.IsNullOrEmpty()) node.IP = inf.IP;
        if (!inf.Gateway.IsNullOrEmpty()) node.Gateway = inf.Gateway;

        node.UpdateIP = context.UserHost;
        node.FixArea();
        node.FixNameByRule();

        // 在心跳中更新客户端所有的框架。因此客户端长期不重启，而中途可能安装了新版NET运行时
        if (!inf.Framework.IsNullOrEmpty())
        {
            //node.Framework = inf.Framework?.Split(',').LastOrDefault();
            node.Frameworks = inf.Framework;
            // 选取最大的版本，而不是最后一个，例如6.0.3字符串大于6.0.13
            Version max = null;
            var fs = inf.Framework.Split(',');
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

        // 每10分钟更新一次节点信息，确保活跃
        if (node.LastActive.AddMinutes(10) < DateTime.Now) node.LastActive = DateTime.Now;
        //node.SaveAsync();
        node.Update();

        //var online = GetOrAddOnline(node, context.Token, context.UserHost);
        var online = base.OnPing(context, request) as NodeOnline;
        //online.Save(null, inf, context.Token, context.UserHost);

        //context.Online = online;

        //// 下发部署的应用服务
        //rs.Services = GetServices(node.ID);

        // DDNS检测。心跳时检测IP变化并更新DNS记录
        _ = _dnsService.CheckNodeIPChange(node, context.UserHost);

        return online;
    }

    private static Int32 _totalCommands;
    private static IList<NodeCommand> _commands = [];
    private static DateTime _nextTime;

    public override CommandModel[]? AcquireCommands(DeviceContext context)
    {
        // 缓存最近1000个未执行命令，用于快速过滤，避免大量节点在线时频繁查询命令表
        if (_nextTime < DateTime.Now || _totalCommands != NodeCommand.Meta.Count)
        {
            _totalCommands = NodeCommand.Meta.Count;
            _commands = NodeCommand.AcquireCommands(-1, 1000);
            _nextTime = DateTime.Now.AddMinutes(1);
        }

        if (context.Device is not Node node) return null;
        var nodeId = node.ID;

        // 是否有本节点
        if (!_commands.Any(e => e.NodeID == nodeId)) return null;

        using var span = _tracer?.NewSpan(nameof(AcquireCommands), new { nodeId });

        var cmds = NodeCommand.AcquireCommands(nodeId, 100);
        if (cmds.Count == 0) return null;

        var rs = new List<CommandModel>();
        foreach (var item in cmds)
        {
            // 命令要提前下发，在客户端本地做延迟处理，这里不应该过滤掉
            //// 命令是否已经开始
            //if (item.StartTime > DateTime.Now) continue;

            // 带有过期时间的命令，加大重试次数
            var maxTimes = item.Expire.Year > 2000 ? 100 : 10;
            if (item.Times > maxTimes || item.Expire.Year > 2000 && item.Expire < DateTime.Now)
                item.Status = CommandStatus.取消;
            else
            {
                // 如果命令正在处理中，则短期内不重复下发。客户端StarAgent具备去重能力，不需要服务端过滤
                //if (item.Status == CommandStatus.处理中 && item.UpdateTime.AddSeconds(30) > DateTime.Now) continue;

                // 即时指令，或者已到开始时间的未来指令，才增加次数
                if (item.StartTime.Year < 2000 || item.StartTime < DateTime.Now)
                    item.Times++;
                item.Status = CommandStatus.处理中;

                var commandModel = BuildCommand(item.Node, item);

                rs.Add(commandModel);
            }
            item.UpdateTime = DateTime.Now;
        }
        cmds.Update(false);

        span?.Value = rs.Count;

        return rs.ToArray();
    }

    /// <summary>获取在线。先查缓存再查库</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public override IOnlineModel? GetOnline(DeviceContext context) => base.GetOnline(context) as NodeOnline;

    public NodeOnline GetOrAddOnline(Node node, String token, String ip)
    {
        var localIp = node?.IP;
        if (localIp.IsNullOrEmpty()) localIp = ip;

        return GetOnline(node, localIp) ?? CreateOnline(node, token, ip);
    }

    /// <summary>获取在线</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public NodeOnline GetOnline(Node node, String ip)
    {
        //var sid = $"{node.ID}@{ip}";
        var sid = node.Code;
        var olt = _cacheProvider.InnerCache.Get<NodeOnline>($"NodeOnline:{sid}");
        if (olt != null)
        {
            //_cacheProvider.InnerCache.SetExpire($"NodeOnline:{sid}", TimeSpan.FromSeconds(120));
            return olt;
        }

        olt = NodeOnline.FindBySessionID(sid);
        if (olt != null) UpdateOnline(node, olt);

        return olt;
    }

    /// <summary>检查在线</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public NodeOnline CreateOnline(Node node, String token, String ip)
    {
        using var span = _tracer?.NewSpan($"{Name}CreateOnline", new { node.Code, ip });

        //var sid = $"{node.ID}@{ip}";
        var sid = node.Code;
        var olt = NodeOnline.GetOrAdd(sid);
        olt.ProjectId = node.ProjectId;
        olt.NodeID = node.ID;
        olt.Name = node.Name;
        olt.ProductCode = node.ProductCode;
        olt.IP = node.IP;
        olt.Category = node.Category;
        olt.ProvinceID = node.ProvinceID;
        olt.CityID = node.CityID;
        olt.Address = node.Address;
        olt.Location = node.Location;
        olt.OSKind = node.OSKind;
        olt.Version = node.Version;
        olt.CompileTime = node.CompileTime;
        olt.Memory = node.Memory;
        olt.MACs = node.MACs;
        //olt.COMs = node.COMs;
        olt.Token = token;
        olt.CreateIP = ip;
        olt.UpdateIP = ip;

        olt.Creator = Environment.MachineName;

        //_cacheProvider.InnerCache.Set($"NodeOnline:{sid}", olt, 120);
        UpdateOnline(node, olt);

        return olt;
    }

    /// <summary>更新在线状态</summary>
    /// <param name="node"></param>
    /// <param name="online"></param>
    public void UpdateOnline(Node node, NodeOnline online)
    {
        var sid = node.Code;
        _cacheProvider.InnerCache.Set($"NodeOnline:{sid}", online, 120);
    }

    /// <summary>删除在线状态</summary>
    /// <param name="node"></param>
    public void RemoveOnline(Node node)
    {
        var sid = node.Code;
        _cacheProvider.InnerCache.Remove($"NodeOnline:{sid}");
    }

    /// <summary>设置设备的长连接上线/下线</summary>
    /// <param name="context">上下文</param>
    /// <param name="online"></param>
    /// <returns></returns>
    public override void SetOnline(DeviceContext context, Boolean online)
    {
        // 优先从缓存/数据库获取最新在线记录，避免 context.Online 持有过期实例
        if ((GetOnline(context) ?? context.Online) is NodeOnline olt)
        {
            // 下线时检查是否有活跃会话，避免旧会话断开时覆盖新会话的状态
            if (!online && context.Device is Node node)
            {
                var session = _sessionManager.Get(node.Code);
                if (session != null && session.Active)
                    return;
            }

            olt.WebSocket = online;
            olt.Update();

            // 更新缓存，确保后续 GetOnline 能拿到最新值
            if (context.Device is Node node2)
                UpdateOnline(node2, olt);
        }
    }

    /// <summary>创建在线</summary>
    /// <param name="context">上下文</param>
    /// <returns></returns>
    public override IOnlineModel? CreateOnline(DeviceContext context)
    {
        if (context.Device is not Node node) return null;

        var online = base.CreateOnline(context) as NodeOnline;
        //var online = NodeOnline.GetOrAdd(GetSessionId(context));
        //online.ProjectId = node.ProjectId;
        //online.NodeID = node.ID;
        //online.Name = node.Name;
        //online.ProductCode = node.ProductCode;
        //online.IP = node.IP;
        //online.Category = node.Category;
        //online.ProvinceID = node.ProvinceID;
        //online.CityID = node.CityID;
        //online.Address = node.Address;
        //online.Location = node.Location;
        //online.OSKind = node.OSKind;
        //online.Version = node.Version;
        //online.CompileTime = node.CompileTime;
        //online.Memory = node.Memory;
        //online.MACs = node.MACs;
        online.Token = context.Token;
        //online.CreateIP = context.UserHost;
        //online.UpdateIP = context.UserHost;
        //online.Creator = Environment.MachineName;

        //context.Online = online;

        //return base.CreateOnline(context);
        return online;
    }
    #endregion

    #region 升级更新
    /// <summary>升级检查。新版优先匹配 ProductRelease + ProductPackage，回退到旧 NodeVersion 逻辑</summary>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    public override IUpgradeInfo? Upgrade(DeviceContext context, String? channel)
    {
        // 默认Release通道
        if (!Enum.TryParse<NodeChannels>(channel, true, out var ch)) ch = NodeChannels.Release;
        if (ch < NodeChannels.Release) ch = NodeChannels.Release;

        var node = context.Device as Node;
        var ip = context.UserHost;

        // ---- 新路径：ProductRelease + ProductPackage 匹配 ----
        var upgradeInfo = TryUpgradeFromRelease(node, ch);
        if (upgradeInfo != null) return upgradeInfo;

        // ---- 回退路径：旧 NodeVersion 逻辑（兼容已有记录） ----
        var list = NodeVersion.GetValids(ch);
        list = list.Where(e => e.ProductCode.IsNullOrEmpty() || e.ProductCode.EqualIgnoreCase(node.ProductCode)).ToList();
        if (list.Count == 0) return null;

        using var span = _tracer?.NewSpan(nameof(Upgrade), new { node.Name, node.Code, node.Runtime, node.Framework, node.Frameworks, ip, vers = list.Count });

        // 应用过滤规则，使用最新的一个版本
        var pv = list.OrderByDescending(e => e.ID).FirstOrDefault(e => e.Version != node.LastVersion && e.Match(node));
        if (pv == null) return null;

        // 检查是否已经升级过这个版本
        if (node.LastVersion == pv.Version) return null;

        node.WriteHistory("自动更新", true, $"channel={ch} version={node.Version} last={node.LastVersion} => [{pv.ID}] {pv.Version} {pv.Executor}", ip);

        node.Channel = ch;
        node.LastVersion = pv.Version;
        node.Update();

        return new UpgradeInfo
        {
            Version = pv.Version,
            Source = pv.Source,
            FileHash = pv.FileHash,
            FileSize = pv.Size,
            Preinstall = pv.Preinstall,
            Executor = pv.Executor,
            Force = pv.Force,
            Description = pv.Description,
        };
    }

    /// <summary>尝试从新的 ProductRelease 表中匹配升级</summary>
    private UpgradeInfo TryUpgradeFromRelease(Node node, NodeChannels channel)
    {
        var releases = ProductRelease.GetValids(channel);
        if (releases.Count == 0) return null;

        var ip = node.UpdateIP;

        foreach (var release in releases)
        {
            // 检查是否已经升级过这个版本
            if (node.LastVersion == release.Version) continue;

            var pkg = release.MatchPackage(node);
            if (pkg == null) continue;

            node.WriteHistory("自动更新", true, $"channel={channel} version={node.Version} last={node.LastVersion} => Release[{release.Id}] {release.Version} Package[{pkg.TargetRuntime}] {pkg.FileName}", ip);

            node.Channel = channel;
            node.LastVersion = release.Version;
            node.Update();

            // 双层取值：Package优先级高于Release，客户端自行处理空Executor
            var executor = !pkg.Executor.IsNullOrEmpty() ? pkg.Executor : release.Executor;
            var preinstall = !pkg.Preinstall.IsNullOrEmpty() ? pkg.Preinstall : release.Preinstall;

            return new UpgradeInfo
            {
                Version = release.Version,
                Source = pkg.Source,
                FileHash = pkg.FileHash,
                FileSize = pkg.Size,
                Preinstall = preinstall,
                Executor = executor,
                Force = release.Force,
                Description = release.Remark,
            };
        }

        return null;
    }

    /// <summary>检查节点是否符合规则，并推送dotNet运行时安装指令。新版优先匹配 DotNetPackage，回退到旧 NodeVersion 逻辑</summary>
    /// <param name="node"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public DotNetPackage CheckDotNet(Node node, Uri baseUri, String ip)
    {
        // ---- 检查 NodeVersion 中是否有启用的 dotNet 策略 ----
        // 如果旧表存在 dotNet 记录但全部被禁用，说明管理员已明确关闭 dotNet 推送，跳过全部路径
        var allNv = NodeVersion.Meta.Cache.FindAll(e => e.ProductCode.EqualIgnoreCase("dotNet")).ToList();
        if (allNv.Count > 0 && allNv.All(e => !e.Enable))
            return null;

        // ---- 新路径：DotNetPackage 匹配 ----
        var pkg = TryDotNetFromPackage(node, baseUri, ip);
        if (pkg != null) return pkg;

        // ---- 回退路径：旧 NodeVersion(ProductCode=dotNet) 逻辑 ----
        var list = NodeVersion.GetValids(0).Where(e => e.ProductCode.EqualIgnoreCase("dotNet")).ToList();
        if (list.Count == 0) return null;

        using var span = _tracer?.NewSpan(nameof(CheckDotNet), new { node.Name, node.Code, node.Runtime, node.Framework, node.Frameworks, ip, vers = list.Count });

        // 应用过滤规则
        list = list.OrderByDescending(e => e.ID).Where(e => e.Match(node)).ToList();
        if (list.Count == 0) return null;

        // 每个版本都要检查，如果已经推送，则推送下一个
        foreach (var pv in list)
        {
            span?.AppendTag($"[{pv.ID}]{pv.Version}");

            // 准备安装框架所需要的参数
            var fmodel = new FrameworkModel { Version = pv.Version, BaseUrl = pv.Source, Force = pv.Force };
            // 如果没有指定源，则使用默认源
            if (fmodel.BaseUrl.IsNullOrEmpty()) fmodel.BaseUrl = new Uri(baseUri, "/files/dotnet/").ToString();
            span?.AppendTag($" source={fmodel.BaseUrl}");

            // 检查是否已经升级过这个版本
            var key = $"nodeNet:{node.Code}-{fmodel.Version}";
            if (_cacheProvider.Cache.Get<String>(key) == pv.Version) return null;
            _cacheProvider.Cache.Set(key, pv.Version, 600);

            var model = new CommandInModel
            {
                Code = node.Code,
                Command = "framework/install",
                Argument = fmodel.ToJson(),
                Expire = 60,
            };
            _ = SendCommand(node, model, $"NodeVersion:{pv.Version}");

            node.WriteHistory("推送dotNet", true, $"version={node.Framework} => [{pv.ID}] {pv.Version} {fmodel.BaseUrl}", ip);

            return null; // 兼容返回值，旧机制无 DotNetPackage 对象可返回
        }

        return null;
    }

    /// <summary>尝试从新的 DotNetPackage 表中匹配 dotNet 安装包并推送</summary>
    private DotNetPackage TryDotNetFromPackage(Node node, Uri baseUri, String ip)
    {
        var pkg = DotNetPackage.Match(node);
        if (pkg == null) return null;

        // 检查节点是否已经安装了该版本
        if (!node.Framework.IsNullOrEmpty())
        {
            System.Version.TryParse(pkg.Version, out var targetVer);
            System.Version.TryParse(node.Framework, out var currentVer);
            if (currentVer != null && targetVer != null && currentVer >= targetVer && !pkg.Force)
                return null;
        }

        // 检查 NodeVersion 中是否存在相同版本被禁用（管理员按版本控制开关）
        var nvVer = $"v{pkg.Version}-{pkg.Kind}";
        var disabledNv = NodeVersion.Meta.Cache.FindAll(e =>
            e.ProductCode.EqualIgnoreCase("dotNet") &&
            !e.Enable &&
            e.Version.EqualIgnoreCase(nvVer)).ToList();
        if (disabledNv.Count > 0)
        {
            node.WriteHistory("跳过dotNet", true, $"NodeVersion[{nvVer}] 已禁用，跳过推送", ip);
            return null;
        }

        // 检查节点操作系统是否兼容目标.NET版本（如Ubuntu18无法安装.NET10）
        if (!IsOSCompatible(node, pkg.Version))
        {
            node.WriteHistory("跳过dotNet", true, $"OS[{node.OS}] 不兼容 .NET {pkg.Version}，已跳过", ip);
            return null;
        }

        // 检查节点的GLIBC版本是否满足要求（Linux节点上报了CLibVersion时启用）
        var minGLibc = GetDotNetMinGLibcVersion(pkg.Version);
        if (minGLibc != null && !CheckGLibc(node, minGLibc))
        {
            node.WriteHistory("跳过dotNet", true, $"GLIBC[{node.CLibVersion}] 不满足 {GetNetMajorVersion(pkg.Version)} 最低要求 {minGLibc}，.NET {pkg.Version} 已跳过", ip);
            return null;
        }

        // 准备安装框架所需要的参数
        // 将 Kind 嵌入 Version，Agent 端 DoInstall 可从中提取安装类型（aspnet/runtime/desktop/host）
        var source = pkg.Source;
        if (!source.IsNullOrEmpty() && !pkg.FileName.IsNullOrEmpty() && source.EndsWith(pkg.FileName))
            source = source.Substring(0, source.Length - pkg.FileName.Length);
        var fmodel = new FrameworkModel
        {
            Version = $"{pkg.Version}-{pkg.Kind}",
            BaseUrl = source,
            Force = pkg.Force,
        };
        // 如果没有指定源，则使用默认源
        if (fmodel.BaseUrl.IsNullOrEmpty()) fmodel.BaseUrl = new Uri(baseUri, "/files/dotnet/").ToString();

        // 检查是否已经推送过这个版本（避免重复推送）
        var key = $"nodeNet:{node.Code}-{pkg.Version}-{pkg.Kind}";
        if (_cacheProvider.Cache.Get<String>(key) == pkg.Version) return null;
        _cacheProvider.Cache.Set(key, pkg.Version, 600);

        var model = new CommandInModel
        {
            Code = node.Code,
            Command = "framework/install",
            Argument = fmodel.ToJson(),
            Expire = 60,
        };
        _ = SendCommand(node, model, $"DotNetPackage:{pkg.Version}");

        node.WriteHistory("推送dotNet", true, $"version={node.Framework} => Package[{pkg.Id}] {pkg.Version}-{pkg.Kind} {pkg.Source}", ip);

        return pkg;
    }

    /// <summary>检查节点操作系统是否兼容目标.NET版本。防止向过旧的操作系统推送不支持的.NET运行时</summary>
    /// <param name="node">节点</param>
    /// <param name="version">目标.NET版本号，如 10.0.9</param>
    /// <returns>兼容返回true，不兼容返回false</returns>
    private static Boolean IsOSCompatible(Node node, String version)
    {
        if (node.OS.IsNullOrEmpty() || version.IsNullOrEmpty()) return true;
        if (!System.Version.TryParse(version.TrimStart('v', 'V'), out var ver)) return true;

        var os = node.OS;
        var major = ver.Major;

        // 提取操作系统名称和版本号
        Double osVer = 0;
        var dist = "";

        // 匹配常见 Linux 发行版
        if (os.StartsWithIgnoreCase("Ubuntu"))
        {
            dist = "Ubuntu";
            // "Ubuntu 18.04.5 LTS" → 18.04
            var part = os.Split(' ').Skip(1).FirstOrDefault();
            Double.TryParse(part, out osVer);
        }
        else if (os.StartsWithIgnoreCase("Debian"))
        {
            dist = "Debian";
            // "Debian GNU/Linux 11 (bullseye)" → 11
            foreach (var s in os.Split(' '))
            {
                if (Double.TryParse(s, out var v)) { osVer = v; break; }
            }
        }
        else if (os.StartsWithIgnoreCase("CentOS") || os.StartsWithIgnoreCase("RHEL") || os.StartsWithIgnoreCase("Red Hat"))
        {
            dist = "RHEL";
            // "CentOS Linux 7 (Core)" → 7
            foreach (var s in os.Split(' '))
            {
                if (Double.TryParse(s, out var v)) { osVer = v; break; }
            }
        }

        // 检查兼容性
        if (osVer > 0)
        {
            if (major >= 10)
            {
                if (dist == "Ubuntu") return osVer >= 22.04;
                if (dist == "Debian") return osVer >= 12;
                if (dist == "RHEL") return osVer >= 9;
            }
            else if (major >= 8)
            {
                if (dist == "Ubuntu") return osVer >= 20.04;
                if (dist == "Debian") return osVer >= 11;
                if (dist == "RHEL") return osVer >= 8;
            }
            else if (major >= 6)
            {
                if (dist == "Ubuntu") return osVer >= 16.04;
                if (dist == "Debian") return osVer >= 10;
                if (dist == "RHEL") return osVer >= 7;
            }
        }

        // 未知操作系统或无法识别版本时，默认兼容（不阻塞推送）
        return true;
    }

    /// <summary>获取指定.NET版本要求的最低GLIBC版本（硬编码，每年随.NET大版本更新一次）</summary>
    /// <param name="version">.NET版本号，如 10.0.9</param>
    /// <returns>最低GLIBC版本号，如 2.17；未知时返回 null</returns>
    /// <remarks>
    /// .NET 版本与 glibc 兼容性历史：
    /// .NET 6/7/8 → glibc 2.17+（CentOS 7 及更新）
    /// .NET 9/10  → glibc 2.27+（CentOS 8/Ubuntu 18.04 及更新）
    /// 首次发行年份：.NET 6=2021, .NET 7=2022, .NET 8=2023, .NET 9=2024, .NET 10=2025
    /// </remarks>
    private static String? GetDotNetMinGLibcVersion(String version)
    {
        if (version.IsNullOrEmpty()) return null;
        if (!System.Version.TryParse(version.TrimStart('v', 'V'), out var ver)) return null;

        var major = ver.Major;

        // .NET 9+ 要求 glibc 2.27+
        if (major >= 9) return "2.27";
        // .NET 6/7/8 支持 glibc 2.17+
        if (major >= 6) return "2.17";

        return null;
    }

    /// <summary>获取.NET版本的主要代号字符串，用于日志显示</summary>
    private static String GetNetMajorVersion(String version)
    {
        if (version.IsNullOrEmpty()) return "";
        if (!System.Version.TryParse(version.TrimStart('v', 'V'), out var ver)) return "";
        return $".NET {ver.Major}";
    }

    /// <summary>检查节点的GLIBC版本是否满足最低要求</summary>
    /// <param name="node">节点</param>
    /// <param name="minVersion">最低GLIBC版本号，如 2.17</param>
    /// <returns>满足返回true，不满足返回false</returns>
    /// <remarks>
    /// 仅当节点为Linux且上报了CLibVersion时启用精确检查。
    /// CLibVersion 格式示例：glibc 2.17、glibc 2.27、musl 1.2.2；可能形如 glibc 2.17;glibcxx 3.4.30
    /// 使用 System.Version 逐段比较 major.minor，忽略后缀 patch 版本。
    /// </remarks>
    private static Boolean CheckGLibc(Node node, String minVersion)
    {
        // 非 Linux 或未上报 CLibVersion 时跳过检查
        if (node.CLibVersion.IsNullOrEmpty()) return true;
        if (!node.OS.StartsWithIgnoreCase("Linux") && !node.OS.StartsWithIgnoreCase("CentOS") &&
            !node.OS.StartsWithIgnoreCase("Ubuntu") && !node.OS.StartsWithIgnoreCase("Debian") &&
            !node.OS.StartsWithIgnoreCase("RHEL") && !node.OS.StartsWithIgnoreCase("Red Hat"))
            return true;

        if (!System.Version.TryParse(minVersion, out var min)) return true;

        // 从 CLibVersion 中提取 glibc 版本号
        // 格式：glibc 2.17 或 glibc 2.17;glibcxx 3.4.30
        var clib = node.CLibVersion;
        var p = clib.IndexOf(';');
        if (p > 0) clib = clib.Substring(0, p);
        clib = clib.Trim();

        // 提取 glibc x.y 或 musl x.y.z
        if (!clib.StartsWithIgnoreCase("glibc ") && !clib.StartsWithIgnoreCase("musl ")) return true;

        var verStr = clib.Substring(clib.IndexOf(' ') + 1).Trim();
        if (verStr.IsNullOrEmpty()) return true;

        if (!System.Version.TryParse(verStr, out var current)) return true;

        // 只比较 major.minor，patch 版本不影响兼容性
        var currentMajorMinor = current.Major * 10000 + current.Minor;
        var minMajorMinor = min.Major * 10000 + min.Minor;

        return currentMajorMinor >= minMajorMinor;
    }
    #endregion

    #region 下行指令
    /// <summary>向节点发送命令。通知节点更新、安装和启停应用等</summary>
    /// <param name="model"></param>
    /// <param name="token">应用令牌</param>
    /// <returns></returns>
    public override Task<CommandReplyModel?> SendCommand(DeviceContext context, CommandInModel model, CancellationToken cancellationToken = default)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定节点");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var node = Node.FindByCode(model.Code);
        if (node == null) throw new ArgumentOutOfRangeException(nameof(model.Code), "无效节点");

        var (jwt, ex) = _tokenService.DecodeToken(context.Token);
        if (ex != null) throw ex;

        var app = App.FindByName(jwt?.Subject);
        if (app == null) throw new ApiException(ApiCode.Unauthorized, "无权操作！");

        if (!app.AllowControlNodes.IsNullOrEmpty())
        {
            if (app.AllowControlNodes != "*" && !node.Code.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
                throw new ApiException(ApiCode.Forbidden, $"[{app}]无权操作节点[{node}]！\n安全设计需要，默认禁止所有应用向任意节点发送控制指令。\n可在注册中心应用系统中修改[{app}]的可控节点，添加[{node.Code}]，或者设置为*所有节点。");
        }
        else if (!_setting.AllowControlNodesWhenEmpty)
            throw new ApiException(ApiCode.Unauthorized, "无权操作！");

        return SendCommand(node, model, app + "", cancellationToken);
    }

    /// <summary>向节点发送命令。（内部用）</summary>
    /// <param name="node"></param>
    /// <param name="model"></param>
    /// <param name="createUser"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<CommandReplyModel> SendCommand(Node node, CommandInModel model, String createUser = null, CancellationToken cancellationToken = default)
    {
        var cmd = new NodeCommand
        {
            NodeID = node.ID,
            Command = model.Command,
            Argument = model.Argument,
            Times = 0,
            Status = CommandStatus.就绪,

            TraceId = DefaultSpan.Current?.TraceId,
            CreateUser = createUser,
        };
        if (model.StartTime > 0) cmd.StartTime = DateTime.Now.AddSeconds(model.StartTime);
        if (model.Expire > 0) cmd.Expire = DateTime.Now.AddSeconds(model.Expire);
        cmd.Insert();

        var commandModel = BuildCommand(node, cmd);
        var code = node.Code;

        // 通过SessionManager发布命令，内置timeout机制等待响应（跨实例广播）
        var reply = await _sessionManager.PublishAsync(code, commandModel, null, model.Timeout, cancellationToken);
        if (reply != null)
        {
            // 埋点
            using var span = _tracer?.NewSpan($"mq:NodeCommandReply", reply);

            if (reply.Status == CommandStatus.错误)
                throw new Exception($"命令错误！{reply.Data}");
            else if (reply.Status == CommandStatus.取消)
                throw new Exception($"命令已取消！{reply.Data}");

            return reply;
        }

        return null;
    }
    #endregion

    #region 事件上报
    public override Int32 CommandReply(DeviceContext context, CommandReplyModel model)
    {
        var cmd = NodeCommand.FindById((Int32)model.Id);
        if (cmd == null) return 0;

        cmd.Status = model.Status;
        cmd.Result = model.Data;
        cmd.Update();

        // 通过会话管理器内置的响应事件总线广播响应（跨实例广播不阻塞）
        _ = _sessionManager.PublishResponseAsync(model, default);

        return 1;
    }

    public override Int32 PostEvents(DeviceContext context, EventModel[] events)
    {
        var node = context.Device as Node;
        var ip = context.UserHost;
        var his = new List<NodeHistory>();
        var dis = new List<AppDeployHistory>();
        foreach (var model in events)
        {
            var success = !model.Type.EqualIgnoreCase("error");
            if (model.Name.EqualIgnoreCase("ServiceController"))
            {
                var appId = 0;
                var p = model.Type.LastIndexOf('-');
                if (p > 0)
                {
                    success = !model.Type[(p + 1)..].EqualIgnoreCase("error");
                    appId = AppDeploy.FindByName(model.Type[..p])?.Id ?? 0;
                }

                //_deployService.WriteHistory(appId, _node?.ID ?? 0, model.Name, success, model.Remark, UserHost);
                var dhi = AppDeployHistory.Create(appId, node?.ID ?? 0, model.Name, success, model.Remark, ip);
                dis.Add(dhi);
            }

            var history = NodeHistory.Create(node, model.Name, success, model.Remark, Environment.MachineName, ip);
            var time = model.Time.ToDateTime().ToLocalTime();
            if (time.Year > 2000) history.CreateTime = time;
            his.Add(history);
        }

        his.Insert();
        dis.Insert();

        return events.Length;
    }
    #endregion

    #region 辅助
    public override IDeviceModel? QueryDevice(String code) => Node.FindByCode(code);

    public override IOnlineModel? QueryOnline(String sessionId) => NodeOnline.FindBySessionId(sessionId, true);

    protected override String GetSessionId(DeviceContext context) => context.Code ?? base.GetSessionId(context);

    private CommandModel BuildCommand(Node node, NodeCommand cmd)
    {
        var model = cmd.ToModel();
        model.TraceId = DefaultSpan.Current + "";

        // 新版本使用UTC时间
        if (node.CompileTime.Year >= 2025)
        {
            if (model.StartTime.Year > 2000)
                model.StartTime = model.StartTime.ToUniversalTime();
            if (model.Expire.Year > 2000)
                model.Expire = model.Expire.ToUniversalTime();
        }

        return model;
    }

    private void WriteHistory(Node node, String action, Boolean success, String remark, String ip = null)
    {
        var hi = NodeHistory.Create(node, action, success, remark, Environment.MachineName, ip);
        hi.Insert();
    }

    public override void WriteHistory(DeviceContext context, String action, Boolean success, String remark)
    {
        var hi = NodeHistory.Create(context.Device as Node, action, success, remark, Environment.MachineName, context.UserHost);
        hi.Insert();
    }
    #endregion
}

public class MySortedList<TKey, TValue>
{
    public List<TKey> Keys { get; private set; } = [];

    public List<TValue> Values { get; private set; } = [];

    public void Add(TKey key, TValue value)
    {
        // 二分法搜索
        var idx = Keys.BinarySearch(key);
        if (idx >= 0)
        {
            // 找到已有元素，在其后面插入
            Keys.Insert(idx + 1, key);
            Values.Insert(idx + 1, value);
        }
        else
        {
            // 补码得到索引。判断是否在最后
            idx = ~idx;
            if (idx >= Keys.Count)
            {
                Keys.Add(key);
                Values.Add(value);
            }
            else
            {
                Keys.Insert(idx, key);
                Values.Insert(idx, value);
            }
        }
    }
}