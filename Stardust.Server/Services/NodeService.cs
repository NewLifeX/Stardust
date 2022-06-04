using System.Reflection;
using NewLife;
using NewLife.Caching;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using NewLife.Web;
using Stardust.Data.Deployment;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Models;
using XCode;

namespace Stardust.Server.Services;

public class NodeService
{
    private static readonly ICache _cache = new MemoryCache();
    private readonly TokenService _tokenService;
    private readonly ICache _queue;

    public NodeService(TokenService tokenService, ICache queue)
    {
        _tokenService = tokenService;
        _queue = queue;
    }

    #region 注册&登录
    public Boolean Auth(Node node, String secret, LoginInfo inf, String ip)
    {
        if (node == null) return false;

        node = CheckNode(node, inf.Node, inf.ProductCode, ip);
        if (node == null) return false;

        if (node.Secret.IsNullOrEmpty()) return true;
        return !secret.IsNullOrEmpty() && !secret.IsNullOrEmpty() && (node.Secret == secret || node.Secret.MD5() == secret);
    }

    public Node Register(LoginInfo inf, String ip, Setting setting)
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
            // 登录密码未设置或者未提交，则执行动态注册
            if (node == null || node.Secret.IsNullOrEmpty() || secret.IsNullOrEmpty())
                node = AutoRegister(node, inf, ip, setting);
            else if (node.Secret.MD5() != secret)
                node = AutoRegister(node, inf, ip, setting);
        }

        return node;
    }

    public TokenModel Login(Node node, LoginInfo inf, String ip, Setting setting)
    {
        if (!inf.ProductCode.IsNullOrEmpty()) node.ProductCode = inf.ProductCode;

        node.UpdateIP = ip;
        node.FixNameByRule();
        node.Login(inf.Node, ip);

        // 设置令牌
        var tokenModel = IssueToken(node.Code, setting);

        // 在线记录
        var olt = GetOnline(node, ip) ?? CreateOnline(node, tokenModel.AccessToken, ip);
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

        var sid = $"{node.ID}@{ip}";
        _cache.Remove($"NodeOnline:{sid}");

        // 计算在线时长
        if (online.CreateTime.Year > 2000)
        {
            node.OnlineTime += (Int32)(DateTime.Now - online.CreateTime).TotalSeconds;
            node.SaveAsync();
        }

        NodeOnlineService.CheckOffline(node, "注销");

        return online;
    }

    /// <summary>
    /// 校验节点密钥
    /// </summary>
    /// <param name="node"></param>
    /// <param name="ps"></param>
    /// <returns></returns>
    private Node CheckNode(Node node, NodeInfo di, String productCode, String ip)
    {
        // 校验唯一编码，防止客户端拷贝配置
        var uuid = di.UUID;
        var guid = di.MachineGuid;
        var diskid = di.DiskID;
        if (!uuid.IsNullOrEmpty() && uuid != node.Uuid)
        {
            node.WriteHistory("登录校验", false, $"唯一标识不符！{uuid}!={node.Uuid}", ip);
            return null;
        }
        if (!guid.IsNullOrEmpty() && guid != node.MachineGuid)
        {
            node.WriteHistory("登录校验", false, $"机器标识不符！{guid}!={node.MachineGuid}", ip);
            return null;
        }
        if (!diskid.IsNullOrEmpty() && diskid != node.DiskID)
        {
            node.WriteHistory("登录校验", false, $"磁盘序列号不符！{diskid}!={node.DiskID}", ip);
            return null;
        }
        if (!node.ProductCode.IsNullOrEmpty() && !productCode.IsNullOrEmpty() && !node.ProductCode.EqualIgnoreCase(productCode))
        {
            node.WriteHistory("登录校验", false, $"产品编码不符！{productCode}!={node.ProductCode}", ip);
            return null;
        }

        // 机器名
        if (di.MachineName != node.MachineName)
        {
            node.WriteHistory("登录校验", false, $"机器名不符！{di.MachineName}!={node.MachineName}", ip);
        }

        // 网卡地址
        if (di.Macs != node.MACs)
        {
            var dims = di.Macs?.Split(",") ?? new String[0];
            var nodems = node.MACs?.Split(",") ?? new String[0];
            // 任意网卡匹配则通过
            if (!nodems.Any(e => dims.Contains(e)))
            {
                node.WriteHistory("登录校验", false, $"网卡地址不符！{di.Macs}!={node.MACs}", ip);
            }
        }

        return node;
    }

    private Node AutoRegister(Node node, LoginInfo inf, String ip, Setting set)
    {
        if (!set.AutoRegister) throw new ApiException(12, "禁止自动注册");

        // 检查白名单
        //var ip = UserHost;
        if (!IsMatchWhiteIP(set.WhiteIP, ip)) throw new ApiException(13, "非法来源，禁止注册");

        var di = inf.Node;
        var code = BuildCode(di, inf.ProductCode, set);
        if (code.IsNullOrEmpty()) code = Rand.NextString(8);

        // 如果节点编码有改变，则倾向于新建节点
        if (node == null || node.Code != code) node = Node.FindByCode(code);

        if (node == null)
        {
            // 该硬件的所有节点信息
            var list = Node.Search(di.UUID, di.MachineGuid, di.Macs);

            // 当前节点信息，取较老者
            list = list.Where(e => e.ProductCode.IsNullOrEmpty() || e.ProductCode == inf.ProductCode).OrderBy(e => e.ID).ToList();

            // 找到节点
            if (node == null) node = list.FirstOrDefault();
        }

        var name = "";
        if (name.IsNullOrEmpty()) name = di.MachineName;
        if (name.IsNullOrEmpty()) name = di.UserName;

        if (node == null) node = new Node
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

        node.WriteHistory("动态注册", true, inf.ToJson(false, false, false), ip);

        return node;
    }

    /// <summary>
    /// 是否匹配白名单，未设置则直接通过
    /// </summary>
    /// <param name="whiteIp"></param>
    /// <param name="ip"></param>
    /// <returns></returns>
    private Boolean IsMatchWhiteIP(String whiteIp, String ip)
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

    private String BuildCode(NodeInfo di, String productCode, Setting set)
    {
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
            if (uid.Contains('{') || uid.Contains('}')) XTrace.WriteLine("节点编码公式有误，存在未解析变量，uid={0}", uid);
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
                return buf.ToHex();
            }
        }

        return null;
    }
    #endregion

    #region 心跳
    public PingResponse Ping(Node node, PingInfo inf, String token, String ip, Setting set)
    {
        var rs = new PingResponse
        {
            Time = inf.Time,
            ServerTime = DateTime.UtcNow,
        };

        if (node != null)
        {
            if (!inf.IP.IsNullOrEmpty()) node.IP = inf.IP;
            node.UpdateIP = ip;
            node.FixArea();
            node.FixNameByRule();
            node.SaveAsync();

            rs.Period = node.Period;

            var olt = GetOnline(node, ip) ?? CreateOnline(node, token, ip);
            olt.Name = node.Name;
            olt.Category = node.Category;
            olt.Version = node.Version;
            olt.CompileTime = node.CompileTime;
            olt.Save(null, inf, token, ip);

            // 令牌有效期检查，10分钟内到期的令牌，颁发新令牌。
            //todo 这里将来由客户端提交刷新令牌，才能颁发新的访问令牌。
            var tm = ValidAndIssueToken(node.Code, token, set);
            if (tm != null)
            {
                rs.Token = tm.AccessToken;

                node.WriteHistory("刷新令牌", true, tm.ToJson(), ip);
            }

            // 拉取命令
            rs.Commands = AcquireCommands(node.ID);

            // 下发部署的应用服务
            rs.Services = GetServices(node.ID);
        }

        return rs;
    }

    private static IList<NodeCommand> _commands;
    private static DateTime _nextTime;

    private static CommandModel[] AcquireCommands(Int32 nodeId)
    {
        // 缓存最近1000个未执行命令，用于快速过滤，避免大量节点在线时频繁查询命令表
        if (_nextTime < DateTime.Now)
        {
            _commands = NodeCommand.AcquireCommands(-1, 1000);
            _nextTime = DateTime.Now.AddMinutes(1);
        }

        // 是否有本节点
        if (!_commands.Any(e => e.NodeID == nodeId)) return null;

        var cmds = NodeCommand.AcquireCommands(nodeId, 100);
        if (cmds == null) return null;

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

    private ServiceInfo[] GetServices(Int32 nodeId)
    {
        var list = AppDeployNode.FindAllByNodeId(nodeId);
        list = list.Where(e => e.Enable).ToList();
        if (list.Count == 0) return null;

        var svcs = new List<ServiceInfo>();
        foreach (var item in list)
        {
            var deploy = item.App;
            if (deploy == null || !deploy.Enable) continue;

            var svc = new ServiceInfo
            {
                Name = deploy.Name,
                FileName = deploy.FileName,
                Arguments = deploy.Arguments,
                WorkingDirectory = deploy.WorkingDirectory,
                AutoStart = deploy.AutoStart,
                //Singleton = true,
            };
            if (!item.Arguments.IsNullOrEmpty()) svc.Arguments = item.Arguments;
            if (!item.WorkingDirectory.IsNullOrEmpty()) svc.WorkingDirectory = item.WorkingDirectory;

            svcs.Add(svc);
        }

        return svcs.ToArray();
    }

    public PingResponse Ping()
    {
        return new PingResponse
        {
            Time = 0,
            ServerTime = DateTime.Now,
        };
    }

    /// <summary></summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public NodeOnline GetOnline(Node node, String ip)
    {
        var sid = $"{node.ID}@{ip}";
        var olt = _cache.Get<NodeOnline>($"NodeOnline:{sid}");
        if (olt != null)
        {
            _cache.SetExpire($"NodeOnline:{sid}", TimeSpan.FromSeconds(600));
            return olt;
        }

        return NodeOnline.FindBySessionID(sid);
    }

    /// <summary>检查在线</summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public NodeOnline CreateOnline(Node node, String token, String ip)
    {
        var sid = $"{node.ID}@{ip}";
        var olt = NodeOnline.GetOrAdd(sid);
        olt.NodeID = node.ID;
        olt.Name = node.Name;
        olt.IP = node.IP;
        olt.Category = node.Category;
        olt.ProvinceID = node.ProvinceID;
        olt.CityID = node.CityID;

        olt.Version = node.Version;
        olt.CompileTime = node.CompileTime;
        olt.Memory = node.Memory;
        olt.MACs = node.MACs;
        //olt.COMs = node.COMs;
        olt.Token = token;
        olt.CreateIP = ip;

        olt.Creator = Environment.MachineName;

        _cache.Set($"NodeOnline:{sid}", olt, 600);

        return olt;
    }
    #endregion

    #region 历史
    /// <summary>设备端响应服务调用</summary>
    /// <param name="model">服务</param>
    /// <returns></returns>
    public Int32 CommandReply(CommandReplyModel model, String token)
    {
        var cmd = NodeCommand.FindById(model.Id);
        if (cmd == null) return 0;

        cmd.Status = model.Status;
        cmd.Result = model.Data;
        cmd.Update();

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

        // 应用过滤规则，使用最新的一个版本
        var pv = list.Where(e => e.Match(node)).OrderByDescending(e => e.Version).FirstOrDefault();
        if (pv == null) return null;
        //if (pv == null) throw new ApiException(509, "没有升级规则");

        node.WriteHistory("自动更新", true, $"channel={ch} => [{pv.ID}] {pv.Version} {pv.Executor}", ip);

        return pv;
    }
    #endregion

    #region 下行指令
    /// <summary>向节点发送命令</summary>
    /// <param name="model"></param>
    /// <param name="token">应用令牌</param>
    /// <returns></returns>
    public Int32 SendCommand(CommandInModel model, String token, Setting setting)
    {
        if (model.Code.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Code), "必须指定节点");
        if (model.Command.IsNullOrEmpty()) throw new ArgumentNullException(nameof(model.Command));

        var node = Node.FindByCode(model.Code);
        if (node == null) throw new ArgumentOutOfRangeException(nameof(model.Code), "无效节点");

        var (_, app) = _tokenService.DecodeToken(token, setting.TokenSecret);
        if (app == null || app.AllowControlNodes.IsNullOrEmpty()) throw new InvalidOperationException("无权操作！");

        if (app.AllowControlNodes != "*" && !node.Code.EqualIgnoreCase(app.AllowControlNodes.Split(",")))
            throw new InvalidOperationException($"[{app}]无权操作节点[{node}]！");

        var cmd = new NodeCommand
        {
            NodeID = node.ID,
            Command = model.Command,
            Argument = model.Argument,
            //Expire = model.Expire,
            Times = 1,
            Status = CommandStatus.处理中,

            CreateUser = app.Name,
        };
        if (model.Expire > 0) cmd.Expire = DateTime.Now.AddSeconds(model.Expire);
        cmd.Insert();

        var queue = _queue.GetQueue<String>($"nodecmd:{node.Code}");
        queue.Add(cmd.ToModel().ToJson());

        return cmd.Id;
    }
    #endregion

    #region 辅助
    public TokenModel IssueToken(String name, Setting set)
    {
        // 颁发令牌
        var ss = set.TokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Issuer = Assembly.GetEntryAssembly().GetName().Name,
            Subject = name,
            Id = Rand.NextString(8),
            Expire = DateTime.Now.AddSeconds(set.TokenExpire),

            Algorithm = ss[0],
            Secret = ss[1],
        };

        return new TokenModel
        {
            AccessToken = jwt.Encode(null),
            TokenType = jwt.Type ?? "JWT",
            ExpireIn = set.TokenExpire,
            RefreshToken = jwt.Encode(null),
        };
    }

    public (Node, Exception) DecodeToken(String token, String tokenSecret)
    {
        if (token.IsNullOrEmpty()) throw new ArgumentNullException(nameof(token));
        //if (token.IsNullOrEmpty()) throw new ApiException(402, $"节点未登录[ip={UserHost}]");

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

        return (node, ex);
    }

    public TokenModel ValidAndIssueToken(String deviceCode, String token, Setting set)
    {
        if (token.IsNullOrEmpty()) return null;
        //var set = Setting.Current;

        // 令牌有效期检查，10分钟内过期者，重新颁发令牌
        var ss = set.TokenSecret.Split(':');
        var jwt = new JwtBuilder
        {
            Algorithm = ss[0],
            Secret = ss[1],
        };
        var rs = jwt.TryDecode(token, out var message);
        return !rs || jwt == null ? null : DateTime.Now.AddMinutes(10) > jwt.Expire ? IssueToken(deviceCode, set) : null;
    }

    private void WriteHistory(Node node, String action, Boolean success, String remark, String ip)
    {
        var hi = NodeHistory.Create(node, action, success, remark, Environment.MachineName, ip);
        hi.SaveAsync();
    }
    #endregion
}