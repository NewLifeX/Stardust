using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewLife;
using NewLife.Log;
using NewLife.Remoting;
using NewLife.Security;
using NewLife.Serialization;
using Stardust.Data.Nodes;
using Stardust.Models;
using Stardust.Server.Common;
using Stardust.Server.Services;
using XCode;

namespace Stardust.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ApiFilter]
    public class NodeController : BaseController
    {
        #region 登录
        [HttpPost(nameof(Login))]
        public LoginResponse Login(LoginInfo inf)
        {
            var code = inf.Code;
            var secret = inf.Secret;

            var node = Node.FindByCode(code, true);
            var di = inf.Node;
            _nodeForHistory = node;

            // 校验唯一编码，防止客户端拷贝配置
            if (node != null) node = CheckNode(node, di);

            var autoReg = false;
            if (node == null)
            {
                node = AutoRegister(null, inf, out autoReg);
            }
            else
            {
                if (!node.Enable) throw new ApiException(99, "禁止登录");

                // 登录密码未设置或者未提交，则执行动态注册
                if (node.Secret.IsNullOrEmpty() || secret.IsNullOrEmpty())
                    node = AutoRegister(node, inf, out autoReg);
                else if (node.Secret.MD5() != secret)
                    node = AutoRegister(node, inf, out autoReg);
            }

            _nodeForHistory = node ?? throw new ApiException(12, "节点鉴权失败");

            var msg = "";
            var success = false;
            try
            {
                node.Login(di, UserHost);

                // 设置令牌，可能已经进行用户登录
                CreateToken(node.Code);

                if (Session != null) Session["Node"] = node;

                // 在线记录
                var olt = GetOnline(code, node) ?? CreateOnline(code, node);
                olt.Save(di, null, Token);

                msg = $"[{node.Name}/{node.Code}]鉴权成功 ";

                success = true;
            }
            catch (Exception ex)
            {
                msg = ex.GetTrue().Message + " ";
                throw;
            }
            finally
            {
                // 登录历史
                WriteHistory("节点鉴权", success, node, msg + di.ToJson(false, false, false));

                XTrace.WriteLine("登录{0} {1}", success ? "成功" : "失败", msg);
            }

            var rs = new LoginResponse
            {
                Name = node.Name,
                Token = Token,
            };

            // 动态注册，下发节点证书
            if (autoReg)
            {
                rs.Code = node.Code;
                rs.Secret = node.Secret;
            }

            return rs;
        }

        /// <summary>注销</summary>
        /// <param name="reason">注销原因</param>
        /// <returns></returns>
        [HttpGet(nameof(Logout))]
        [HttpPost(nameof(Logout))]
        public LoginResponse Logout(String reason)
        {
            var node = Session["Node"] as Node;
            if (node != null)
            {
                var olt = GetOnline(node.Code, node);
                if (olt != null)
                {
                    var msg = $"{reason} [{node}]]登录于{olt.CreateTime}，最后活跃于{olt.UpdateTime}";
                    WriteHistory(node, "节点下线", true, msg);
                    olt.Delete();

                    // 计算在线时长
                    if (olt.CreateTime.Year > 2000)
                    {
                        node.OnlineTime += (Int32)(DateTime.Now - olt.CreateTime).TotalSeconds;
                        node.SaveAsync();
                    }

                    NodeOnlineService.CheckOffline(node);
                }
            }

            // 销毁会话，更新令牌
            Session["Node"] = null;
            CreateToken(null);

            return new LoginResponse
            {
                Name = node?.Name,
                Token = Token,
            };
        }

        /// <summary>
        /// 校验节点密钥
        /// </summary>
        /// <param name="node"></param>
        /// <param name="ps"></param>
        /// <returns></returns>
        private Node CheckNode(Node node, NodeInfo di)
        {
            // 校验唯一编码，防止客户端拷贝配置
            var uuid = di.UUID;
            var guid = di.MachineGuid;
            var diskid = di.DiskID;
            if (!uuid.IsNullOrEmpty() && uuid != node.Uuid)
            {
                WriteHistory("登录校验", false, node, $"唯一标识不符！{uuid}!={node.Uuid}");
                return null;
            }
            if (!guid.IsNullOrEmpty() && guid != node.MachineGuid)
            {
                WriteHistory("登录校验", false, node, $"机器标识不符！{guid}!={node.MachineGuid}");
                return null;
            }
            if (!diskid.IsNullOrEmpty() && diskid != node.DiskID)
            {
                WriteHistory("登录校验", false, node, $"磁盘序列号不符！{diskid}!={node.DiskID}");
                return null;
            }

            // 机器名
            if (di.MachineName != node.MachineName)
            {
                WriteHistory("登录校验", false, node, $"机器名不符！{di.MachineName}!={node.MachineName}");
            }

            // 网卡地址
            if (di.Macs != node.MACs)
            {
                var dims = di.Macs?.Split(",") ?? new String[0];
                var nodems = node.MACs?.Split(",") ?? new String[0];
                // 任意网卡匹配则通过
                if (!nodems.Any(e => dims.Contains(e)))
                {
                    WriteHistory("登录校验", false, node, $"网卡地址不符！{di.Macs}!={node.MACs}");
                }
            }

            return node;
        }

        private Node AutoRegister(Node node, LoginInfo inf, out Boolean autoReg)
        {
            var set = Setting.Current;
            if (!set.AutoRegister) throw new ApiException(12, "禁止自动注册");

            var di = inf.Node;
            if (node == null)
            {
                // 该硬件的所有节点信息
                var list = Node.Search(di.UUID, di.MachineGuid, di.Macs);

                // 当前节点信息，取较老者
                list = list.OrderBy(e => e.ID).ToList();

                // 找到节点
                if (node == null) node = list.FirstOrDefault();
            }

            var ip = UserHost;
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
            node.Code = BuildCode(di);
            if (node.Code.IsNullOrEmpty()) node.Code = Rand.NextString(8);

            node.Secret = Rand.NextString(16);
            node.UpdateIP = ip;
            node.UpdateTime = DateTime.Now;

            node.Save();
            autoReg = true;

            WriteHistory("动态注册", true, node, inf.ToJson(false, false, false));

            return node;
        }

        private String BuildCode(NodeInfo di)
        {
            var set = Setting.Current;
            //var uid = $"{di.UUID}@{di.MachineGuid}@{di.Macs}";
            var ss = set.NodeCodeFormula.Split(new[] { '(', ')' });
            if (ss.Length >= 2)
            {
                var uid = ss[1];
                foreach (var pi in di.GetType().GetProperties())
                {
                    uid = uid.Replace($"{{{pi.Name}}}", pi.GetValue(di) + "");
                }
                if (!uid.IsNullOrEmpty())
                {
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
        [TokenFilter]
        //[HttpGet(nameof(Ping))]
        [HttpPost(nameof(Ping))]
        public PingResponse Ping(PingInfo inf)
        {
            var rs = new PingResponse
            {
                Time = inf.Time,
                ServerTime = DateTime.UtcNow,
            };

            if (Session["Node"] is Node node)
            {
                var code = node.Code;
                node.FixArea();
                node.SaveAsync();

                rs.Period = node.Period;

                var olt = GetOnline(code, node) ?? CreateOnline(code, node);
                olt.Name = node.Name;
                olt.Category = node.Category;
                olt.Save(null, inf, Token);

                // 拉取命令
                rs.Commands = AcquireCommands(node.ID);
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

            var rs = cmds.Select(e => new CommandModel
            {
                Id = e.ID,
                Command = e.Command,
                Argument = e.Argument,
                Expire = e.Expire,
            }).ToArray();

            foreach (var item in cmds)
            {
                item.Finished = true;
                item.UpdateTime = DateTime.Now;
            }
            cmds.Update(false);

            return rs;
        }

        //[TokenFilter]
        [HttpGet(nameof(Ping))]
        public PingResponse Ping()
        {
            return new PingResponse
            {
                Time = 0,
                ServerTime = DateTime.Now,
            };
        }

        /// <summary></summary>
        /// <param name="code"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        protected virtual NodeOnline GetOnline(String code, Node node)
        {
            var olt = Session["Online"] as NodeOnline;
            if (olt != null) return olt;

            var ip = UserHost;
            var port = HttpContext.Connection.RemotePort;
            var sid = $"{node.ID}@{ip}:{port}";
            return NodeOnline.FindBySessionID(sid);
        }

        /// <summary>检查在线</summary>
        /// <returns></returns>
        protected virtual NodeOnline CreateOnline(String code, Node node)
        {
            var ip = UserHost;
            var port = HttpContext.Connection.RemotePort;
            var sid = $"{node.ID}@{ip}:{port}";
            var olt = NodeOnline.GetOrAdd(sid);
            olt.NodeID = node.ID;
            olt.Name = node.Name;
            olt.Category = node.Category;
            olt.ProvinceID = node.ProvinceID;
            olt.CityID = node.CityID;

            olt.Version = node.Version;
            olt.CompileTime = node.CompileTime;
            olt.Memory = node.Memory;
            olt.MACs = node.MACs;
            //olt.COMs = node.COMs;
            olt.Token = Token;
            olt.CreateIP = ip;

            olt.Creator = Environment.MachineName;

            Session["Online"] = olt;

            return olt;
        }
        #endregion

        #region 历史
        /// <summary>上报数据，针对命令</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost(nameof(Report))]
        public async Task<Object> Report(Int32 id)
        {
            var node = Session["Node"] as Node;
            if (node == null) throw new ApiException(402, "节点未登录");

            var cmd = NodeCommand.FindByID(id);
            if (cmd != null && cmd.NodeID == node.ID)
            {
                var ms = Request.Body;
                if (Request.ContentLength > 0)
                {
                    var rs = "";
                    switch (cmd.Command)
                    {
                        case "截屏":
                            rs = await SaveFileAsync(cmd, ms, "png");
                            break;
                        case "抓日志":
                            rs = await SaveFileAsync(cmd, ms, "log");
                            break;
                        default:
                            rs = await SaveFileAsync(cmd, ms, "bin");
                            break;
                    }

                    if (!rs.IsNullOrEmpty())
                    {
                        cmd.Result = rs;
                        cmd.Save();

                        WriteHistory(node, cmd.Command, true, rs);
                    }
                }
            }

            return null;
        }

        private async Task<String> SaveFileAsync(NodeCommand cmd, Stream ms, String ext)
        {
            var file = $"../{cmd.Command}/{DateTime.Today:yyyyMMdd}/{cmd.NodeID}_{cmd.ID}.{ext}";
            file.EnsureDirectory(true);

            using var fs = file.AsFile().OpenWrite();
            await ms.CopyToAsync(fs);
            await ms.FlushAsync();

            return file;
        }

        protected virtual NodeHistory WriteHistory(String action, Boolean success, Node node, String remark = null)
        {
            var ip = UserHost;
            return NodeHistory.Create(node, action, success, remark, Environment.MachineName, ip);
        }
        #endregion

        #region 升级
        /// <summary>升级检查</summary>
        /// <param name="channel">更新通道</param>
        /// <returns></returns>
        [TokenFilter]
        [HttpGet(nameof(Upgrade))]
        public UpgradeInfo Upgrade(String channel)
        {
            var node = Session["Node"] as Node;
            if (node == null) throw new ApiException(402, "节点未登录");

            // 默认Release通道
            if (!Enum.TryParse<NodeChannels>(channel, true, out var ch)) ch = NodeChannels.Release;
            if (ch < NodeChannels.Release) ch = NodeChannels.Release;

            // 找到所有产品版本
            var list = NodeVersion.GetValids(ch);

            // 应用过滤规则，使用最新的一个版本
            var pv = list.Where(e => e.Match(node)).OrderByDescending(e => e.Version).FirstOrDefault();
            if (pv == null) return null;
            //if (pv == null) throw new ApiException(509, "没有升级规则");

            WriteHistory("自动更新", true, node, $"channel={ch} => [{pv.ID}] {pv.Version} {pv.Source} {pv.Executor}");

            return new UpgradeInfo
            {
                Version = pv.Version,
                Source = pv.Source,
                Executor = pv.Executor,
                Force = pv.Force,
                Description = pv.Description,
            };
        }
        #endregion
    }
}