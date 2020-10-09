using System;
using System.Collections.Generic;
using System.Text;
using NewLife;
using NewLife.Collections;
using NewLife.Data;

namespace StarGateway.Http
{
    /// <summary>Http请求</summary>
    public class HttpRequest
    {
        #region 属性
        /// <summary>头部数据</summary>
        public Packet Header { get; set; }

        /// <summary>负载数据</summary>
        public Packet Body { get; set; }

        /// <summary>请求方法</summary>
        public String Method { get; set; }

        /// <summary>请求资源</summary>
        public String Uri { get; set; }

        /// <summary>版本</summary>
        public String Version { get; set; }

        /// <summary>内容长度</summary>
        public Int32 ContentLength { get; set; } = -1;

        /// <summary>头部集合</summary>
        public IDictionary<String, String> Headers { get; set; }
        #endregion

        #region 方法
        private static readonly Byte[] NewLine = new[] { (Byte)'\r', (Byte)'\n', (Byte)'\r', (Byte)'\n' };
        /// <summary>从数据包中读取消息</summary>
        /// <param name="pk"></param>
        /// <returns>是否成功</returns>
        public virtual Boolean Read(Packet pk)
        {
            // 读取请求方法
            var p = pk.Slice(0, 16).IndexOf(new[] { (Byte)' ' });
            if (p < 0) return false;

            Method = pk.Slice(0, p).ToStr();

            p = pk.IndexOf(NewLine);
            if (p < 0) return false;

            Header = pk.Slice(0, p);
            Body = pk.Slice(p + 4);

            //var isGet = pk.Count >= 4 && pk[0] == 'G' && pk[1] == 'E' && pk[2] == 'T' && pk[3] == ' ';
            //var isPost = pk.Count >= 5 && pk[0] == 'P' && pk[1] == 'O' && pk[2] == 'S' && pk[3] == 'T' && pk[4] == ' ';
            //if (isGet)
            //    Method = "GET";
            //else if (isPost)
            //    Method = "POST";

            return true;
        }

        /// <summary>解码头部</summary>
        public virtual Boolean DecodeHeaders()
        {
            var pk = Header;
            if (pk == null || pk.Total == 0) return false;

            // 请求方法 GET / HTTP/1.1
            var dic = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
            var ss = pk.ToStr().Split(Environment.NewLine);
            {
                var kv = ss[0].Split(" ");
                if (kv != null && kv.Length >= 3)
                {
                    Method = kv[0].Trim();
                    Uri = kv[1].Trim();
                    Version = kv[2].TrimStart("HTTP/");
                }
            }
            for (var i = 1; i < ss.Length; i++)
            {
                var kv = ss[i].Split(":");
                if (kv != null && kv.Length >= 2)
                {
                    dic[kv[0].Trim()] = kv[1].Trim();
                }
            }
            Headers = dic;

            // 内容长度
            if (dic.TryGetValue("Content-Length", out var str))
                ContentLength = str.ToInt();

            return true;
        }

        /// <summary>编码头部</summary>
        /// <returns></returns>
        public virtual Boolean EncodeHeaders()
        {
            var dic = Headers;
            if (dic == null || dic.Count == 0) return false;

            var sb = Pool.StringBuilder.Get();
            sb.AppendFormat("{0} {1} HTTP/{2}", Method, Uri, Version);
            sb.AppendLine();
            foreach (var item in Headers)
            {
                sb.Append(item.Key);
                sb.Append(":");
                sb.Append(item.Value);
                sb.AppendLine();
            }

            // 获取数据，构建头部，不需要附带两个换行
            var data = sb.Put(true).GetBytes();
            var len = data.Length;
            if (Headers.Count > 0) len -= 2;

            Header = new Packet(data, 0, len);

            return true;
        }

        /// <summary>把消息转为封包</summary>
        /// <returns></returns>
        public virtual Packet ToPacket()
        {
            // 使用子数据区，不改变原来的头部对象
            var pk = Header.Slice(0, -1);
            pk.Next = NewLine;
            //pk.Next = new[] { (Byte)'\r', (Byte)'\n' };

            var pay = Body;
            if (pay != null && pay.Total > 0) pk.Append(pay);

            return pk;
        }
        #endregion
    }
}