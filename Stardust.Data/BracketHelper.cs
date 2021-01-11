using System;
using System.Collections.Generic;
using NewLife;
using Stardust.Data.Models;

namespace Stardust.Data
{
    public static class BracketHelper
    {
        /// <summary>处理内嵌标记</summary>
        /// <param name="str"></param>
        /// <param name="replace"></param>
        /// <returns></returns>
        public static String Build(String str, Func<String, String, String, String> replace)
        {
            if (str.IsNullOrEmpty()) return str;

            var p = 0;
            while (true)
            {
                var p1 = str.IndexOf("${", p);
                if (p1 < 0) break;

                var p2 = str.IndexOf('}', p1 + 2);
                if (p2 < 0) break;

                // 替换
                var item = str.Substring(p1 + 2, p2 - p1 - 2);
                // 拆分 ${ztbi@db:huzhou}
                var ss = item.Split("@", ":");
                var key = ss[0];
                var app = ss.Length > 1 ? ss[1] : "";
                var scope = ss.Length > 2 ? ss[2] : "";
                item = replace(key, app, scope) + "";

                // 重新组合
                var left = str.Substring(0, p1);
                var right = str.Substring(p2 + 1);
                str = left + item + right;

                // 移动游标，加速下一次处理
                p = left.Length + item.Length;
            }

            return str;
        }

        public static String Build2(String str, Func<String, String> replace)
        {
            if (str.IsNullOrEmpty()) return str;

            var p = 0;
            while (true)
            {
                var p1 = str.IndexOf("${", p);
                if (p1 < 0) break;

                var p2 = str.IndexOf('}', p1 + 2);
                if (p2 < 0) break;

                // 替换
                var item = str.Substring(p1 + 2, p2 - p1 - 2);
                item = replace(item) + "";

                // 重新组合
                var left = str.Substring(0, p1);
                var right = str.Substring(p2 + 1);
                str = left + item + right;

                // 移动游标，加速下一次处理
                p = left.Length + item.Length;
            }

            return str;
        }

        public static List<ConfigItem> Parse(String str)
        {
            var list = new List<ConfigItem>();

            if (str.IsNullOrEmpty()) return list;

            var p = 0;
            while (true)
            {
                var p1 = str.IndexOf("${", p);
                if (p1 < 0) break;

                var p2 = str.IndexOf('}', p1 + 2);
                if (p2 < 0) break;

                // 替换
                var item = str.Substring(p1 + 2, p2 - p1 - 2);
                // 拆分 ${ztbi@db:huzhou}
                var ss = item.Split("@", ":");

                var ci = new ConfigItem
                {
                    Key = ss[0],
                    Value = ss.Length > 1 ? ss[1] : "",
                    Scope = ss.Length > 2 ? ss[2] : ""
                };

                list.Add(ci);

                // 重新组合
                var left = str.Substring(0, p1);
                var right = str.Substring(p2 + 1);
                str = left + item + right;

                // 移动游标，加速下一次处理
                p = left.Length + item.Length;
            }

            return list;
        }
    }
}