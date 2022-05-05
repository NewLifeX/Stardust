using System;
using System.Collections.Generic;
using System.Linq;
using NewLife;
using Stardust.Data.Nodes;

namespace Stardust.Data;

/// <summary>
/// 节点解析器
/// </summary>
public class NodeResolver
{
    #region 静态
    private static NodeResolver _instance;
    private DateTime _expire;

    /// <summary>
    /// 静态实例。定时过期，更新策略
    /// </summary>
    public static NodeResolver Instance
    {
        get
        {
            if (_instance == null || _instance._expire > DateTime.Now)
            {
                var resolver = new NodeResolver
                {
                    _expire = DateTime.Now.AddMinutes(10)
                };

                _instance = resolver;
            }

            return _instance;
        }
    }
    #endregion

    #region 属性

    #endregion

    #region 方法
    /// <summary>
    /// 匹配IP地址所对应的节点信息
    /// </summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    public IEnumerable<NodeRule> Matchs(String ip)
    {
        if (ip.IsNullOrEmpty()) yield break;

        var list = NodeRule.FindAllWithCache().Where(e => e.Enable).ToList();

        // 多IP地址
        var ss = ip.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in ss)
        {
            // 去掉后缀
            var ip2 = item;
            var p = ip2.IndexOfAny(new[] { '@', '#' });
            if (p > 0) ip2 = ip2[..p];

            foreach (var rule in list)
            {
                if (rule.Rule.IsMatch(ip2)) yield return rule;
            }
        }
    }

    /// <summary>
    /// 匹配IP地址所对应的节点信息
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="localIp"></param>
    /// <returns></returns>
    public NodeRule Match(String ip, String localIp)
    {
        if (ip.IsNullOrEmpty() && localIp.IsNullOrEmpty()) return null;

        ip += "," + localIp;

        var list = Matchs(ip).OrderByDescending(e => e.Priority).ToList();

        return list.FirstOrDefault();
    }
    #endregion
}