using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stardust.Data.Platform;
using Xunit;
using XCode;

namespace ServerTest.Mcp;

/// <summary>
/// DEF-MW-SRV-01 回归：PUT /api/Platform/McpServer/{id} 更新链路双态炸（j-deploy-b 2026-11-11 CRUD 走查实锤）。
/// 生产机制（源码+走查双证定谳）：请求体可重读 → 基类 GetSubmittedFieldsAsync 返回提交键集 → CopySubmitted
/// 用属性反射把提交值写到库中实体（仅属性通道）；但 XCode 属性 setter 不写 Extends 值存储 →
/// ① 全字段改名 PUT：属性通道=新值（Valid 过）→ OnUpdate→entity.Update() 从 Extends 读值 → UPDATE 落旧值
/// = code=0 静默空更新（现象 A）；② 部分提交（UI toggle 仅 {id,enable}，mcpToken.logic.ts 契约）：
/// 未提交字段属性通道保持默认空 → Valid 读 Name 属性=空 → ArgumentNullException →
/// code=500「保存失败！ 名称不能为空！(Parameter 'Name')」（现象 B）。
/// 修复方向：WebEntityController.Update 镜像 Insert（F-PLT-1）双通道补值——提交字段属性+SetItem 双写
/// 并显式置脏，未提交字段属性通道对齐 Extends 库值。
/// 本测试反射驱动控制器 Update 全链路（提交拷贝→Valid→Biz→SQLite 落库），注入可重读 JSON 请求体
/// （与生产等态：CanSeek=true 且位置 0，GetSubmittedFieldsAsync 正常返回提交键集），断言一律走
/// 直击 DB 的 SQL 通道（与 F-PLT-1 先例同源，零缓存零物化偏置）。
/// </summary>
public class McpServerApiUpdateTests
{
    private static Type ApiControllerType =>
        Type.GetType("Stardust.Web.Areas.Platform.Controllers.McpServerController, StarWebApi")
        ?? throw new InvalidOperationException("StarWebApi McpServerController not found");

    private static Object InjectContext(Object controller, String json = null)
    {
        var http = new DefaultHttpContext();
        if (json != null)
        {
            // 生产等态：请求体可重读（6681 实态 fields!=null 的前提；CanSeek=true 且位置 0）
            var bytes = Encoding.UTF8.GetBytes(json);
            http.Request.Body = new MemoryStream(bytes);
            http.Request.ContentType = "application/json";
        }
        typeof(ControllerBase).GetProperty(nameof(ControllerBase.ControllerContext))!
            .SetValue(controller, new ControllerContext { HttpContext = http });
        return controller;
    }

    /// <summary>反射调用 Update(String, T) 全链路（避免测试工程与 StarWebApi 的 NewLife.Cube 类型冲突）。</summary>
    private static async Task<Object> InvokeUpdateAsync(Object controller, String id, McpServer model)
    {
        var method = ApiControllerType.GetMethod("Update", new[] { typeof(String), typeof(McpServer) });
        Assert.NotNull(method);
        var task = (Task)method!.Invoke(controller, new Object[] { id, model })!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }

    private static Int32 GetCode(Object response)
    {
        var prop = response.GetType().GetProperty("Code") ?? response.GetType().GetProperty("code");
        Assert.NotNull(prop);
        return Convert.ToInt32(prop!.GetValue(response));
    }

    /// <summary>经控制器 Insert 造一行（走 F-PLT-1 修复链，返回库侧自增 Id）。</summary>
    private static async Task<Int32> SeedRowAsync(String name, Boolean enable)
    {
        var controller = InjectContext(Activator.CreateInstance(ApiControllerType)!);
        var insertMethod = ApiControllerType.GetMethod("Insert", new[] { typeof(McpServer) });
        var task = (Task)insertMethod!.Invoke(controller, new Object[] { new McpServer
        {
            Name = name,
            Url = "http://127.0.0.1:9104",
            Enable = enable,
            ProjectId = 0,
            Remark = "DEF-MW-SRV-01 seed",
        } })!;
        await task;
        var rs = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var codeProp = rs.GetType().GetProperty("Code")!;
        Assert.Equal(0, Convert.ToInt32(codeProp.GetValue(rs)));
        var row = QueryRow($"Select Id From McpServer Where Name='{name}'");
        Assert.NotNull(row);
        return Convert.ToInt32(row!["Id"]);
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

    [Fact(DisplayName = "DEF-MW-SRV-01 回归：改名 PUT 必须真实落库，禁止静默空更新")]
    public async Task Update_Rename_Should_Persist_NewName()
    {
        var seedName = "mw01a-seed-" + Guid.NewGuid().ToString("N")[..8];
        var newName = "mw01a-renamed-" + Guid.NewGuid().ToString("N")[..8];
        var id = await SeedRowAsync(seedName, true);
        try
        {
            // 前端全字段提交契约（McpServerForm 编辑 PUT）：Id+五业务字段，属性通道承载提交值
            var model = new McpServer
            {
                Id = id,
                Name = newName,
                Url = "http://127.0.0.1:9104",
                Enable = true,
                ProjectId = 0,
                Remark = "DEF-MW-SRV-01 rename",
            };
            var body = $"{{\"Id\":{id},\"Name\":\"{newName}\",\"Url\":\"http://127.0.0.1:9104\"," +
                       $"\"Enable\":true,\"ProjectId\":0,\"Remark\":\"DEF-MW-SRV-01 rename\"}}";
            var controller = InjectContext(Activator.CreateInstance(ApiControllerType)!, body);
            var response = await InvokeUpdateAsync(controller, id.ToString(), model);
            var code1 = GetCode(response);
            Assert.True(code1 == 0, $"改名 PUT 应成功，实际 code={code1}，message={response.GetType().GetProperty("Message")?.GetValue(response)}");

            var row = QueryRow($"Select Name, Enable From McpServer Where Id={id}");
            Assert.NotNull(row);
            Assert.Equal(newName, Convert.ToString(row!["Name"]));   // 库值必须=新名（修复前=旧名静默空更新）
        }
        finally
        {
            XCode.DataAccessLayer.DAL.Create("Stardust").Session.Execute($"Delete From McpServer Where Id={id} Or Name='{seedName}' Or Name='{newName}'");
        }
    }

    [Fact(DisplayName = "DEF-MW-SRV-01 回归：启停 PUT（UI 部分提交 {id,enable}）必须真实落库且不清空未提交字段")]
    public async Task Update_ToggleEnable_Should_Persist()
    {
        var seedName = "mw01b-seed-" + Guid.NewGuid().ToString("N")[..8];
        var id = await SeedRowAsync(seedName, true);
        try
        {
            // UI toggle 契约（mcpToken.logic.ts：PUT 启停仅提交 {id,enable}）：部分绑定形态，
            // 未提交字段属性通道保持默认空——生产实态下 Valid 读 Name 属性=空 → 500「名称不能为空」
            var model = new McpServer { Id = id, Enable = false };
            var body = $"{{\"Id\":{id},\"Enable\":false}}";
            var controller = InjectContext(Activator.CreateInstance(ApiControllerType)!, body);
            var response = await InvokeUpdateAsync(controller, id.ToString(), model);
            var code2 = GetCode(response);
            Assert.True(code2 == 0, $"启停 PUT 应成功，实际 code={code2}，message={response.GetType().GetProperty("Message")?.GetValue(response)}");

            var row = QueryRow($"Select Enable, Name From McpServer Where Id={id}");
            Assert.NotNull(row);
            Assert.Equal(0, Convert.ToInt32(row!["Enable"]));        // 库值必须=停用（修复前=仍启用）
            Assert.Equal(seedName, Convert.ToString(row["Name"]));   // 未提交字段不得被清空或改名
        }
        finally
        {
            XCode.DataAccessLayer.DAL.Create("Stardust").Session.Execute($"Delete From McpServer Where Id={id} Or Name='{seedName}'");
        }
    }
}
