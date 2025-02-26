using System.Xml.Linq;
using NewLife;
using NewLife.Caching;
using NewLife.IP;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Remoting.Models;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Nodes;
using Stardust.Data.Platform;
using Stardust.Models;
using XCode;
using XCode.Configuration;

namespace Stardust.Server.Services;

public class NodeService
{
    private readonly TokenService _tokenService;
    private readonly IPasswordProvider _passwordProvider;
    private readonly NodeSessionManager _sessionManager;
    private readonly ICacheProvider _cacheProvider;
    private readonly ITracer _tracer;

    public NodeService(TokenService tokenService, IPasswordProvider passwordProvider, NodeSessionManager sessionManager, ICacheProvider cacheProvider, ITracer tracer)
    {
        _tokenService = tokenService;
        _passwordProvider = passwordProvider;
        _sessionManager = sessionManager;
        _cacheProvider = cacheProvider;
        _tracer = tracer;
    }

    #region 注册&登录
    public Boolean Auth(Node node, String secret, LoginInfo inf, String ip, StarServerSetting setting)
    {
        if (node == null) return false;

        node = CheckNode(node, inf.Node, inf.ProductCode, ip, setting.NodeCodeLevel);
        if (node == null)
        {
            WriteHistory(node, "节点鉴权", false, "硬件信息变动过大", ip);
            return false;
        }

        if (node.Secret.IsNullOrEmpty()) return true;
        if (node.Secret == secret) return true;
        //return !secret.IsNullOrEmpty() && !secret.IsNullOrEmpty() && (node.Secret == secret || node.Secret.MD5() == secret);
        if (secret.IsNullOrEmpty() || !_passwordProvider.Verify(node.Secret, secret))
        {
            WriteHistory(node, "节点鉴权", false, "密钥校验失败", ip);
            return false;
        }

        return true;
    }

    public Node Register(LoginInfo inf, String ip, StarServerSetting setting)
    {
        var code = inf.Code;
        var secret = inf.Secret;

        var node = Node.FindByCode(code, true);

        // 校验唯一编码，防止客户端拷贝配置
        if (node == null)
        {
            node = AutoRegister(null, inf, ip, setting);
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
            node = AutoRegister(node, inf, ip, setting);
        }

        return node;
    }

    public TokenModel Login(Node node, LoginInfo inf, String ip, StarServerSetting setting)
    {
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
        node.Login(inf.Node, ip);

        // 设置令牌
        var tokenModel = _tokenService.IssueToken(node.Code, setting.TokenSecret, setting.TokenExpire, inf.ClientId);

        // 在线记录
        var olt = GetOrAddOnline(node, tokenModel.AccessToken, ip);
        olt.Save(inf.Node, null, tokenModel.AccessToken, ip);

        // 登录历史
        node.WriteHistory("节点鉴权", true, $"[{node.Name}/{node.Code}]鉴权成功 " + inf.ToJson(false, false, false), ip);

        return tokenModel;
    }

    /// <summary>注销</summary>
    /// <param name="reason">注销原因</param>
    /// <param name="ip">IP地址</param>
    /// <returns></returns>
    public NodeOnline Logout(Node node, String reason, String ip)
    {
        var online = GetOnline(node, ip);
        if (online == null) return null;

        var msg = $"{reason} [{node}]]登录于{online.CreateTime}，最后活跃于{online.UpdateTime}";
        node.WriteHistory("节点下线", true, msg, ip);
        online.Delete();

        RemoveOnline(node);

        // 计算在线时长
        if (online.CreateTime.Year > 2000)
        {
            node.OnlineTime += (Int32)(DateTime.Now - online.CreateTime).TotalSeconds;
            node.Update();
        }

        NodeOnlineService.CheckOffline(node, "注销");

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
        if (!set.AutoRegister) throw new ApiException(12, "禁止自动注册");

        // 检查白名单
        //var ip = UserHost;
        if (!IsMatchWhiteIP(set.WhiteIP, ip)) throw new ApiException(13, "非法来源，禁止注册");

        using var span = _tracer?.NewSpan(nameof(AutoRegister), new { inf.ProductCode, inf.Node });

        var di = inf.Node;
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

        node.WriteHistory("动态注册", true, inf.ToJson(false, false, false), ip);

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

    #region 心跳
    public NodeOnline Ping(Node node, PingInfo inf, String token, String ip)
    {
        if (node == null) return null;

        if (!inf.IP.IsNullOrEmpty()) node.IP = inf.IP;
        node.UpdateIP = ip;
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

        var online = GetOrAddOnline(node, token, ip);
        online.Name = node.Name;
        online.ProjectId = node.ProjectId;
        online.ProductCode = node.ProductCode;
        online.Category = node.Category;
        online.Version = node.Version;
        online.CompileTime = node.CompileTime;
        online.OSKind = node.OSKind;
        online.ProvinceID = node.ProvinceID;
        online.CityID = node.CityID;
        online.Save(null, inf, token, ip);

        //// 下发部署的应用服务
        //rs.Services = GetServices(node.ID);

        return online;
    }

    private static Int32 _totalCommands;
    private static IList<NodeCommand> _commands;
    private static DateTime _nextTime;

    public CommandModel[] AcquireNodeCommands(Int32 nodeId)
    {
        // 缓存最近1000个未执行命令，用于快速过滤，避免大量节点在线时频繁查询命令表
        if (_nextTime < DateTime.Now || _totalCommands != NodeCommand.Meta.Count)
        {
            _totalCommands = NodeCommand.Meta.Count;
            _commands = NodeCommand.AcquireCommands(-1, 1000);
            _nextTime = DateTime.Now.AddMinutes(1);
        }

        // 是否有本节点
        if (!_commands.Any(e => e.NodeID == nodeId)) return null;

        using var span = _tracer?.NewSpan(nameof(AcquireNodeCommands), new { nodeId });

        var cmds = NodeCommand.AcquireCommands(nodeId, 100);
        if (cmds.Count == 0) return null;

        var rs = new List<CommandModel>();
        foreach (var item in cmds)
        {
            if (item.Times > 10 || item.Expire.Year > 2000 && item.Expire < DateTime.Now)
                item.Status = CommandStatus.取消;
            else
            {
                if (item.Status == CommandStatus.处理中 && item.UpdateTime.AddMinutes(10) < DateTime.Now) continue;

                item.Times++;
                item.Status = CommandStatus.处理中;
                rs.Add(item.ToModel());
            }
            item.UpdateTime = DateTime.Now;
        }
        cmds.Update(false);

        return rs.ToArray();
    }

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
        olt.OSKind = node.OSKind;
        olt.Version = node.Version;
        olt.CompileTime = node.CompileTime;
        olt.Memory = node.Memory;
        olt.MACs = node.MACs;
        //olt.COMs = node.COMs;
        olt.Token = token;
        olt.CreateIP = ip;

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
    #endregion

    #region 历史
    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    public Int32 CommandReply(Node node, CommandReplyModel model, String token)
    {
        var cmd = NodeCommand.FindById((Int32)model.Id);
        if (cmd == null) return 0;

        cmd.Status = model.Status;
        cmd.Result = model.Data;
        cmd.Update();

        // 通知命令发布者，指令已完成
        var topic = $"nodereply:{cmd.Id}";
        var q = _cacheProvider.GetQueue<CommandReplyModel>(topic);
        q.Add(model);

        _cacheProvider.Cache.SetExpire(topic, TimeSpan.FromSeconds(60));

        return 1;
    }
    #endregion

    #region 升级
    /// <summary>升级检查</summary>
    /// <param name="channel">更新通道</param>
    /// <returns></returns>
    public NodeVersion Upgrade(Node node, String channel, String ip)
    {
        // 默认Release通道
        if (!Enum.TryParse<NodeChannels>(channel, true, out var ch)) ch = NodeChannels.Release;
        if (ch < NodeChannels.Release) ch = NodeChannels.Release;

        // 找到所有产品版本
        var list = NodeVersion.GetValids(ch);
        list = list.Where(e => e.ProductCode.IsNullOrEmpty() || e.ProductCode.EqualIgnoreCase(node.ProductCode)).ToList();
        if (list.Count == 0) return null;

        using var span = _tracer?.NewSpan(nameof(Upgrade), new { node.Name, node.Code, node.Runtime, node.Framework, node.Frameworks, ip, vers = list.Count });

        // 应用过滤规则，使用最新的一个版本
        var pv = list.OrderByDescending(e => e.ID).FirstOrDefault(e => e.Version != node.LastVersion && e.Match(node));
        if (pv == null) return null;
        //if (pv == null) throw new ApiException(509, "没有升级规则");

        // 检查是否已经升级过这个版本
        if (node.LastVersion == pv.Version) return null;

        node.WriteHistory("自动更新", true, $"channel={ch} version={node.Version} last={node.LastVersion} => [{pv.ID}] {pv.Version} {pv.Executor}", ip);

        node.Channel = ch;
        node.LastVersion = pv.Version;
        node.Update();

        return pv;
    }

    /// <summary>检查节点是否符合规则，并推送dotNet运行时安装指令</summary>
    /// <param name="node"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    public NodeVersion CheckDotNet(Node node, Uri baseUri, String ip)
    {
        // 找到所有产品版本
        var list = NodeVersion.GetValids(0).Where(e => e.ProductCode.EqualIgnoreCase("dotNet")).ToList();
        if (list.Count == 0) return null;

        using var span = _tracer?.NewSpan(nameof(CheckDotNet), new { node.Name, node.Code, node.Runtime, node.Framework, node.Frameworks, ip, vers = list.Count });

        // 应用过滤规则
        list = list.OrderByDescending(e => e.ID).Where(e => e.Match(node)).ToList();
        //var list2 = new List<NodeVersion>();
        //foreach (var pv in list)
        //{
        //    var rs = pv.MatchResult(node);
        //    if (rs == null)
        //        list2.Add(pv);
        //    else
        //        span?.AppendTag($"[{pv.Version}] {rs}");
        //}
        //list = list2;
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

            return pv;
        }

        return null;
    }
    #endregion

    #region 下行指令
    /// <summary>向节点发送命令。通知节点更新、安装和启停应用等</summary>
    /// <param name="model"></param>
    /// <param name="token">应用令牌</param>
    /// <returns></returns>
    public async Task<NodeCommand> SendCommand(CommandInModel model, String token, StarServerSetting setting)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定节点");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var node = Node.FindByCode(model.Code);
        if (node == null) throw new ArgumentOutOfRangeException(nameof(model.Code), "无效节点");

        var (_, app) = _tokenService.DecodeToken(token, setting.TokenSecret);
        if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new ApiException(401, "无权操作！");

        if (app.AllowControlNodes != "*" && !node.Code.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
            throw new ApiException(403, $"[{app}]无权操作节点[{node}]！\n安全设计需要，默认禁止所有应用向任意节点发送控制指令。\n可在注册中心应用系统中修改[{app}]的可控节点，添加[{node.Code}]，或者设置为*所有节点。");

        return await SendCommand(node, model, app.Name);
    }

    /// <summary>向节点发送命令。（内部用）</summary>
    /// <param name="node"></param>
    /// <param name="model"></param>
    /// <param name="createUser"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<NodeCommand> SendCommand(Node node, CommandInModel model, String createUser = null)
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

        var commandModel = cmd.ToModel();
        commandModel.TraceId = DefaultSpan.Current + "";

        //var queue = _cacheProvider.GetQueue<String>($"nodecmd:{node.Code}");
        //queue.Add(commandModel.ToJson());
        _sessionManager.PublishAsync(node.Code, commandModel, null, default).ConfigureAwait(false).GetAwaiter().GetResult();

        // 挂起等待。借助redis队列，等待响应
        if (model.Timeout > 0)
        {
            var q = _cacheProvider.GetQueue<CommandReplyModel>($"nodereply:{cmd.Id}");
            var reply = await q.TakeOneAsync(model.Timeout);
            if (reply != null)
            {
                // 埋点
                using var span = _tracer?.NewSpan($"mq:NodeCommandReply", reply);

                if (reply.Status == CommandStatus.错误)
                    throw new Exception($"命令错误！{reply.Data}");
                else if (reply.Status == CommandStatus.取消)
                    throw new Exception($"命令已取消！{reply.Data}");
            }
        }

        return cmd;
    }
    #endregion

    #region 辅助
    public (JwtBuilder, Node, Exception) DecodeToken(String token, String tokenSecret)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));

        // 解码令牌
        var ss = tokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };

        var rs = jwt.TryDecode(token, out var message);
        var node = Node.FindByCode(jwt.Subject);

        Exception ex = null;
        if (!rs || node == null)
        {
            if (node != null)
                ex = new ApiException(403, $"[{node.Name}/{node.Code}]非法访问 {message}");
            else
                ex = new ApiException(403, $"[{jwt.Subject}]非法访问 {message}");
        }

        return (jwt, node, ex);
    }

    private void WriteHistory(Node node, String action, Boolean success, String remark, String ip = null)
    {
        var hi = NodeHistory.Create(node, action, success, remark, Environment.MachineName, ip);
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