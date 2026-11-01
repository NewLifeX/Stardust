using System;

namespace Stardust.Data;

/// <summary>实体操作错误辅助</summary>
public static class EntityErrorHelper
{
    /// <summary>判断异常是否为唯一索引冲突</summary>
    /// <remarks>
    /// 高并发写入在线记录、跟踪项等带唯一索引的表时，两个请求可能同时判断记录不存在并尝试插入，
    /// 后插入者会收到数据库的唯一索引冲突。该异常表示并发写入已经成功，通常可以转为读取已有记录。
    /// </remarks>
    /// <param name="ex">异常</param>
    /// <returns>是否唯一索引冲突</returns>
    public static Boolean IsDuplicateKey(Exception ex)
    {
        if (ex == null) return false;

        var msg = ex.Message;
        return msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("UNIQUE KEY constraint", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("ORA-00001", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("唯一", StringComparison.OrdinalIgnoreCase);
    }
}
