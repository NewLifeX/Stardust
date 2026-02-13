<%@ Language="VBScript" %>
<!--#include file="../src/StardustTracer.asp"-->
<%
' ==================================================
' 基础心跳示例
' 演示最简单的登录 + 心跳保活功能
'
' 使用方法：
' 1. 将 StardustTracer.asp 部署到 IIS 站点
' 2. 修改下方的服务器地址、应用标识和密钥
' 3. 通过浏览器访问本页面
' ==================================================

Dim tracer
Set tracer = New StardustTracer
tracer.Init "http://star.example.com:6600", "MyASPApp", "MySecret"

' 登录获取令牌
If tracer.Login() Then
    Response.Write "<p>登录成功，Token: " & Left(tracer.Token, 20) & "...</p>"

    ' 发送心跳
    tracer.Ping
    Response.Write "<p>心跳发送成功</p>"
Else
    Response.Write "<p>登录失败，请检查服务器地址和应用配置</p>"
End If

Set tracer = Nothing
%>
