using System;
using System.ComponentModel;
using Stardust.Data;
using Xunit;

namespace ServerTest;

/// <summary>实体操作错误辅助测试</summary>
public class EntityErrorHelperTests
{
    [Fact]
    [DisplayName("识别各数据库的唯一索引冲突")]
    public void IsDuplicateKey_MatchesCommonDatabases()
    {
        Assert.True(EntityErrorHelper.IsDuplicateKey(new Exception("Duplicate entry '281-rps:' for key 'traceitem.IU_TraceItem_AppId_Name'")));
        Assert.True(EntityErrorHelper.IsDuplicateKey(new Exception("UNIQUE constraint failed: TraceItem.AppId, TraceItem.Name")));
        Assert.True(EntityErrorHelper.IsDuplicateKey(new Exception("duplicate key value violates unique constraint \"iu_traceitem_appid_name\"")));
        Assert.True(EntityErrorHelper.IsDuplicateKey(new Exception("Violation of UNIQUE KEY constraint 'IU_AppOnline_Client'.")));
        Assert.True(EntityErrorHelper.IsDuplicateKey(new Exception("ORA-00001: unique constraint (STAR.IU_APPONLINE_CLIENT) violated")));
        Assert.True(EntityErrorHelper.IsDuplicateKey(new Exception("违反唯一约束条件")));
    }

    [Fact]
    [DisplayName("非唯一索引冲突不误判")]
    public void IsDuplicateKey_RejectsOtherErrors()
    {
        Assert.False(EntityErrorHelper.IsDuplicateKey(new Exception("Timeout expired. The timeout period elapsed prior to completion of the operation.")));
        Assert.False(EntityErrorHelper.IsDuplicateKey(new Exception("constraint failed: NOT NULL constraint failed")));
        Assert.False(EntityErrorHelper.IsDuplicateKey(null));
    }
}
