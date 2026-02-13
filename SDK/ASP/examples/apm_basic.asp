<%@ Language="VBScript" %>
<!--#include file="../src/StardustTracer.asp"-->
<%
' ==================================================
' APM 监控示例
' 演示页面级别的链路追踪和数据上报
'
' 使用方法：
' 1. 将 StardustTracer.asp 部署到 IIS 站点
' 2. 修改下方的服务器地址、应用标识和密钥
' 3. 通过浏览器访问本页面
' ==================================================

' 初始化追踪器
Dim tracer
Set tracer = New StardustTracer
tracer.Init "http://star.example.com:6600", "MyASPApp", "MySecret"

' 使用缓存的 Token（存在 Application 中）
Application.Lock
Dim cachedToken
cachedToken = Application("StardustToken")
If Len(cachedToken) = 0 Then
    tracer.Login
    Application("StardustToken") = tracer.Token
End If
Application.UnLock

' 创建页面级追踪片段
Dim span
Set span = tracer.NewSpan(Request.ServerVariables("REQUEST_METHOD") & " " & Request.ServerVariables("URL"))
span.Tag = Request.ServerVariables("QUERY_STRING")

' ======= 业务逻辑开始 =======

Response.Write "<h1>ASP 页面</h1>"
Response.Write "<p>当前时间: " & Now() & "</p>"

' 模拟数据库查询
Dim dbSpan
Set dbSpan = tracer.NewSpan("SELECT * FROM Users")
dbSpan.Tag = "查询用户列表"
' ... 执行数据库操作 ...
dbSpan.Finish

' ======= 业务逻辑结束 =======

' 完成页面追踪
span.Finish

' 上报数据
tracer.Flush

Set tracer = Nothing
%>
