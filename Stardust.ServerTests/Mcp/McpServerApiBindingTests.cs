using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using Xunit;
using XCode;

namespace ServerTest.Mcp;

/// <summary>
/// F-PLT-1 回归：POST /api/Platform/McpServer 曾全部丢值 500「SQL logic error near ','」。
/// 根因（build#7 echo 双槽结构定谳）：API 版 NewLife.Cube(6.16.2026.1030) 模型绑定把 JSON 请求值
/// 写入实体属性，而 XCode 持久化读取的值存储（Extends）为空——属性通道与持久化通道脱节，
/// SQL 渲染全默认值（「',,0'」语法错）。修复（build#8 终态）：WebEntityController.Insert 对绑定
/// 实体本体逐业务字段 SetItem 双通道补值（属性值→值存储同步）→ OnInsert（业务钩子）→ 二次同步
/// （钩子生成字段如 Token）→ Insert（内部 Valid 在钩子后，与部署版顺序一致）。
/// 本测试反射驱动控制器 Insert 全链路（Valid→Biz→SQLite 落库），覆盖三条契约：
/// 正常实体直插 / 双通道并存属性值胜出 / 空实体业务 Valid 拒绝。
/// </summary>
public class McpServerApiBindingTests
{
    private static Type ApiControllerType =>
        Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpServerController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi McpServerController not found");

    private static Object InjectContext(Object controller)
    {
        typeof(ControllerBase).GetProperty(nameof(ControllerBase.ControllerContext))!
            .SetValue(controller, new ControllerContext { HttpContext = new DefaultHttpContext() });
        return controller;
    }

    /// <summary>反射调用基类 Insert(T)（避免测试工程与 StarWebApi 的 NewLife.Cube 类型冲突），等待并返回 ApiResponse。</summary>
    private static async Task<Object> InvokeInsertAsync(Object controller, McpServer entity)
    {
        var method = ApiControllerType.GetMethod("Insert", new[] { typeof(McpServer) });
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(controller, new Object[] { entity })!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static Int32 GetCode(Object response)
    {
        var prop = response.GetType().GetProperty("Code") ?? response.GetType().GetProperty("code");
        Assert.NotNull(prop);
        return Convert.ToInt32(prop!.GetValue(response));
    }

    private static Object GetData(Object response)
    {
        var prop = response.GetType().GetProperty("Data") ?? response.GetType().GetProperty("data");
        Assert.NotNull(prop);
        return prop!.GetValue(response);
    }

    /// <summary>直击 DB 的 SQL 行查询（XCode Dal.Session，绕过实体集合缓存与物化偏置，取落库真行）</summary>
    private static IDictionary<String, Object> QueryRow(String sql)
    {
        var dal = XCode.DataAccessLayer.DAL.Create("Stardust");
        var ds = dal.Session.Query(sql);
        if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0) return null;
        var tbl = ds.Tables[0];
        var row = tbl.Rows[0];
        var dict = new Dictionary<String, Object>();
        foreach (DataColumn col in tbl.Columns) dict[col.ColumnName] = row[col];
        return dict;
    }

    [Fact(DisplayName = "F-PLT-1 回归：双通道并存时属性值胜出，Extends 陈值不得污染落库")]
    public async Task Insert_Should_Let_Property_Values_Win_Over_Extends()
    {
        var name = "fplt1-dual-" + Guid.NewGuid().ToString("N")[..8];
        var entity = new McpServer
        {
            Name = name,
            Url = "http://127.0.0.1:9103",
            Enable = true,
            ToolCount = 2,
            Remark = "F-PLT-1 dual-channel",
        };
        // 预置值存储陈值：SetItem 物化 Extends（fresh 实体值存储为 null，惰性创建——与生产一致：
        // STJ 绑定后属性承载输入值，值存储是默认/陈值）。Pascal 键与属性同槽冲突 + camel 键并存，
        // 修复后属性值必须胜出
        entity.SetItem("Name", "stale-extends-value");
        entity.SetItem("Url", "http://stale.invalid");
        entity.SetItem("name", "stale-camel-value");

        try
        {
            var controller = InjectContext(Activator.CreateInstance(ApiControllerType)!);
            var response = await InvokeInsertAsync(controller, entity);
            Assert.Equal(0, GetCode(response));

            var data = GetData(response);
            Assert.NotNull(data);
            // 双通道注记（进程内定谳）：插入后实体对象属性字段 _Id=0（自增回填仅入 Extends 存储槽，
            // 属性字段不回填——与 j-deploy-b HTTP 回显 camel id:0 + 存储槽真值互证）；且 XCode 集合缓存
            // 会在 Insert 后纳入该内存对象，FindAll(exp) 命中缓存即返回原对象（属性 Id 恒 0）。
            // 故断言一律走直击 DB 的 SQL 通道（与库侧 python 探针同源，零缓存零物化偏置）
            var row = QueryRow($"Select Id, Name, Url, ToolCount From McpServer Where Name='{name}'");
            Assert.NotNull(row);
            var id = Convert.ToInt32(row!["Id"]);
            Assert.True(id > 0, $"插入应成功产生自增 Id（落库行），实际 Id={id}");
            Assert.Equal(name, Convert.ToString(row["Name"]));      // 属性通道胜出（DB 侧验证）
            Assert.Equal("http://127.0.0.1:9103", Convert.ToString(row["Url"]));
            Assert.Equal(2, Convert.ToInt32(row["ToolCount"]));
        }
        finally
        {
            // SQL 直删测试行（确定性清理，隔离库 fplt1-test.db）
            XCode.DataAccessLayer.DAL.Create("Stardust").Session.Execute($"Delete From McpServer Where Name='{name}'");
        }
    }

    [Fact(DisplayName = "F-PLT-1 对照：字段齐全的正常实体 Insert 不受重建逻辑影响")]
    public async Task Insert_Should_PassThrough_ProperEntity()
    {
        var name = "fplt1-ctl-" + Guid.NewGuid().ToString("N")[..8];
        var controller = InjectContext(Activator.CreateInstance(ApiControllerType)!);
        var entity = new McpServer
        {
            Name = name,
            Url = "http://127.0.0.1:9102",
            Enable = true,
            ToolCount = 1,
            Remark = "F-PLT-1 control",
        };

        try
        {
            var response = await InvokeInsertAsync(controller, entity);
            Assert.Equal(0, GetCode(response));
            var data = GetData(response);
            Assert.NotNull(data);
            // 同 test1：断言走直击 DB 的 SQL 通道（集合缓存命中会返回插入原对象，属性 Id 恒 0）
            var row = QueryRow($"Select Id, Name, ToolCount From McpServer Where Name='{name}'");
            Assert.NotNull(row);
            var id = Convert.ToInt32(row!["Id"]);
            Assert.True(id > 0, $"正常实体直插应成功，实际 Id={id}");
            Assert.Equal(name, Convert.ToString(row["Name"]));
            Assert.Equal(1, Convert.ToInt32(row["ToolCount"]));
        }
        finally
        {
            // SQL 直删测试行（确定性清理，隔离库 fplt1-test.db）
            XCode.DataAccessLayer.DAL.Create("Stardust").Session.Execute($"Delete From McpServer Where Name='{name}'");
        }
    }

    [Fact(DisplayName = "F-PLT-1 回归：空实体 Insert 必须被业务 Valid 拒绝，不允许静默成功")]
    public async Task Insert_EmptyEntity_Should_Be_Rejected()
    {
        var controller = InjectContext(Activator.CreateInstance(ApiControllerType)!);
        var entity = new McpServer();
        try
        {
            var response = await InvokeInsertAsync(controller, entity);
            Assert.NotEqual(0, GetCode(response));
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            Assert.True(inner is ArgumentNullException || inner.Message.Contains("Name"),
                $"空实体应被业务 Valid 拒绝，实际异常：{inner.GetType().Name}: {inner.Message}");
        }
    }
}
