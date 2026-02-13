<%
' ==================================================
' 星尘监控 ASP (Classic ASP/VBScript) SDK
' Stardust APM Monitoring SDK for Classic ASP
'
' 适用于 IIS + Classic ASP 环境
' 提供星尘 APM 监控的接入能力，包括：
' - 登录认证
' - 心跳保活
' - 链路追踪
' - 数据上报
'
' 版本: 1.0.0
' 项目: https://github.com/NewLifeX/Stardust
' ==================================================

Class StardustSpan
    Public Id
    Public ParentId
    Public TraceId
    Public StartTime
    Public EndTime
    Public Tag
    Public Error
    Public SpanName

    Private m_tracer

    Public Sub Init(name, tracerObj, parentId)
        Id = GenerateId(16)
        ParentId = parentId
        TraceId = GenerateId(32)
        SpanName = name
        StartTime = GetUnixMilliseconds()
        EndTime = 0
        Tag = ""
        Error = ""
        Set m_tracer = tracerObj
    End Sub

    Public Sub SetError(errMsg)
        Error = CStr(errMsg)
    End Sub

    Public Sub Finish()
        EndTime = GetUnixMilliseconds()
        m_tracer.FinishSpan SpanName, Me
    End Sub

    Public Function ToJson()
        Dim json
        json = "{"
        json = json & """Id"":""" & JsEncode(Id) & ""","
        json = json & """ParentId"":""" & JsEncode(ParentId) & ""","
        json = json & """TraceId"":""" & JsEncode(TraceId) & ""","
        json = json & """StartTime"":" & CStr(StartTime) & ","
        json = json & """EndTime"":" & CStr(EndTime) & ","
        json = json & """Tag"":""" & JsEncode(Tag) & ""","
        json = json & """Error"":""" & JsEncode(Error) & """"
        json = json & "}"
        ToJson = json
    End Function
End Class

Class StardustSpanBuilder
    Public Name
    Public StartTime
    Public EndTime
    Public Total
    Public Errors
    Public Cost
    Public MaxCost
    Public MinCost

    Private m_samples
    Private m_errorSamples
    Private m_maxSamples
    Private m_maxErrors
    Private m_sampleCount
    Private m_errorSampleCount

    Public Sub Init(spanName, maxSamples, maxErrors)
        Name = spanName
        StartTime = GetUnixMilliseconds()
        EndTime = 0
        Total = 0
        Errors = 0
        Cost = 0
        MaxCost = 0
        MinCost = 0
        m_maxSamples = maxSamples
        m_maxErrors = maxErrors
        m_sampleCount = 0
        m_errorSampleCount = 0
        Set m_samples = CreateObject("Scripting.Dictionary")
        Set m_errorSamples = CreateObject("Scripting.Dictionary")
    End Sub

    Public Sub AddSpan(span)
        Dim elapsed
        elapsed = CLng(span.EndTime - span.StartTime)

        Total = Total + 1
        Cost = Cost + elapsed
        If MaxCost = 0 Or elapsed > MaxCost Then MaxCost = elapsed
        If MinCost = 0 Or elapsed < MinCost Then MinCost = elapsed

        If Len(span.Error) > 0 Then
            Errors = Errors + 1
            If m_errorSampleCount < m_maxErrors Then
                m_errorSamples.Add CStr(m_errorSampleCount), span
                m_errorSampleCount = m_errorSampleCount + 1
            End If
        Else
            If m_sampleCount < m_maxSamples Then
                m_samples.Add CStr(m_sampleCount), span
                m_sampleCount = m_sampleCount + 1
            End If
        End If
        EndTime = GetUnixMilliseconds()
    End Sub

    Public Function ToJson()
        Dim json, i, keys
        json = "{"
        json = json & """Name"":""" & JsEncode(Name) & ""","
        json = json & """StartTime"":" & CStr(StartTime) & ","
        json = json & """EndTime"":" & CStr(EndTime) & ","
        json = json & """Total"":" & CStr(Total) & ","
        json = json & """Errors"":" & CStr(Errors) & ","
        json = json & """Cost"":" & CStr(Cost) & ","
        json = json & """MaxCost"":" & CStr(MaxCost) & ","
        json = json & """MinCost"":" & CStr(MinCost) & ","

        ' Samples
        json = json & """Samples"":["
        keys = m_samples.Keys
        For i = 0 To m_samples.Count - 1
            If i > 0 Then json = json & ","
            json = json & m_samples(keys(i)).ToJson()
        Next
        json = json & "],"

        ' ErrorSamples
        json = json & """ErrorSamples"":["
        keys = m_errorSamples.Keys
        For i = 0 To m_errorSamples.Count - 1
            If i > 0 Then json = json & ","
            json = json & m_errorSamples(keys(i)).ToJson()
        Next
        json = json & "]"

        json = json & "}"
        ToJson = json
    End Function
End Class

Class StardustTracer
    Private m_server
    Private m_appId
    Private m_appName
    Private m_secret
    Private m_clientId
    Private m_token
    Private m_maxSamples
    Private m_maxErrors
    Private m_maxTagLength
    Private m_builders

    Public Property Get Token()
        Token = m_token
    End Property

    Public Sub Init(server, appId, secret)
        m_server = server
        m_appId = appId
        m_appName = appId
        m_secret = secret
        m_clientId = GetServerIP() & "@" & CStr(GetCurrentProcessId())
        m_token = ""
        m_maxSamples = 1
        m_maxErrors = 10
        m_maxTagLength = 1024
        Set m_builders = CreateObject("Scripting.Dictionary")
    End Sub

    ' 登录获取令牌
    Public Function Login()
        Dim url, payload, data
        url = m_server & "/App/Login"
        payload = "{"
        payload = payload & """AppId"":""" & JsEncode(m_appId) & ""","
        payload = payload & """Secret"":""" & JsEncode(m_secret) & ""","
        payload = payload & """ClientId"":""" & JsEncode(m_clientId) & ""","
        payload = payload & """AppName"":""" & JsEncode(m_appName) & """"
        payload = payload & "}"

        Set data = PostJson(url, payload)
        If Not data Is Nothing Then
            m_token = GetJsonValue(data, "Token")
            Dim code
            code = GetJsonValue(data, "Code")
            If Len(code) > 0 Then m_appId = code
        End If
        Login = (Len(m_token) > 0)
    End Function

    ' 心跳保活
    Public Sub Ping()
        Dim url, payload, data
        url = m_server & "/App/Ping?Token=" & UrlEncode(m_token)
        payload = "{"
        payload = payload & """Id"":0,"
        payload = payload & """Name"":""" & JsEncode(m_appName) & ""","
        payload = payload & """Time"":" & CStr(GetUnixMilliseconds())
        payload = payload & "}"

        Set data = PostJson(url, payload)
        If Not data Is Nothing Then
            Dim newToken
            newToken = GetJsonValue(data, "Token")
            If Len(newToken) > 0 Then m_token = newToken
        End If
    End Sub

    ' 创建追踪片段
    Public Function NewSpan(name)
        Dim span
        Set span = New StardustSpan
        span.Init name, Me, ""
        Set NewSpan = span
    End Function

    ' 完成片段（内部调用）
    Public Sub FinishSpan(name, span)
        ' 排除自身
        If name = "/Trace/Report" Or name = "/Trace/ReportRaw" Then Exit Sub

        ' 截断 Tag
        If Len(span.Tag) > m_maxTagLength Then
            span.Tag = Left(span.Tag, m_maxTagLength)
        End If

        If Not m_builders.Exists(name) Then
            Dim builder
            Set builder = New StardustSpanBuilder
            builder.Init name, m_maxSamples, m_maxErrors
            m_builders.Add name, builder
        End If
        m_builders(name).AddSpan span
    End Sub

    ' 上报数据
    Public Sub Flush()
        If m_builders.Count = 0 Then Exit Sub

        Dim buildersJson, keys, i, first
        buildersJson = "["
        keys = m_builders.Keys
        first = True
        For i = 0 To m_builders.Count - 1
            If m_builders(keys(i)).Total > 0 Then
                If Not first Then buildersJson = buildersJson & ","
                first = False
                buildersJson = buildersJson & m_builders(keys(i)).ToJson()
            End If
        Next
        buildersJson = buildersJson & "]"

        m_builders.RemoveAll

        Dim payload
        payload = "{"
        payload = payload & """AppId"":""" & JsEncode(m_appId) & ""","
        payload = payload & """AppName"":""" & JsEncode(m_appName) & ""","
        payload = payload & """ClientId"":""" & JsEncode(m_clientId) & ""","
        payload = payload & """Builders"":" & buildersJson
        payload = payload & "}"

        Dim url
        url = m_server & "/Trace/Report?Token=" & UrlEncode(m_token)
        PostJson url, payload
    End Sub

    ' ========== HTTP 工具 ==========

    Private Function PostJson(url, body)
        On Error Resume Next
        Dim http
        Set http = CreateObject("MSXML2.ServerXMLHTTP.6.0")
        http.Open "POST", url, False
        http.setRequestHeader "Content-Type", "application/json; charset=utf-8"
        http.setTimeouts 5000, 5000, 10000, 10000
        http.Send body

        Set PostJson = Nothing
        If Err.Number = 0 And http.Status = 200 Then
            Dim responseText
            responseText = http.responseText

            ' 简单解析 JSON（提取 data 部分）
            Dim codePos
            codePos = InStr(responseText, """code"":0")
            If codePos > 0 Then
                Set PostJson = ParseSimpleJson(responseText)
            End If
        End If

        Set http = Nothing
        On Error GoTo 0
    End Function

    ' 简易 JSON 值提取
    Private Function ParseSimpleJson(jsonStr)
        Set ParseSimpleJson = CreateObject("Scripting.Dictionary")
        ParseSimpleJson.Add "raw", jsonStr

        ' 提取 Token
        Dim tokenVal
        tokenVal = ExtractJsonString(jsonStr, "Token")
        If Len(tokenVal) > 0 Then ParseSimpleJson.Add "Token", tokenVal

        ' 提取 Code
        Dim codeVal
        codeVal = ExtractJsonString(jsonStr, "Code")
        If Len(codeVal) > 0 Then ParseSimpleJson.Add "Code", codeVal
    End Function

    Private Function GetJsonValue(dict, key)
        GetJsonValue = ""
        If dict.Exists(key) Then GetJsonValue = dict(key)
    End Function
End Class

' ========== 工具函数 ==========

Function GenerateId(length)
    Dim chars, result, i
    chars = "0123456789abcdef"
    result = ""
    Randomize
    For i = 1 To length
        result = result & Mid(chars, Int(Rnd * 16) + 1, 1)
    Next
    GenerateId = result
End Function

Function GetUnixMilliseconds()
    ' 计算当前时间距 1970-01-01 的秒数，再乘以 1000 得到毫秒
    Dim seconds
    seconds = DateDiff("s", "1970-01-01 00:00:00", Now())
    GetUnixMilliseconds = CDbl(seconds) * 1000
End Function

Function JsEncode(str)
    Dim result
    result = CStr(str)
    result = Replace(result, "\", "\\")
    result = Replace(result, """", "\""")
    result = Replace(result, vbCr, "\r")
    result = Replace(result, vbLf, "\n")
    result = Replace(result, vbTab, "\t")
    JsEncode = result
End Function

Function ExtractJsonString(jsonStr, key)
    Dim searchKey, pos1, pos2
    ExtractJsonString = ""
    searchKey = """" & key & """:"""
    pos1 = InStr(jsonStr, searchKey)
    If pos1 > 0 Then
        pos1 = pos1 + Len(searchKey)
        pos2 = InStr(pos1, jsonStr, """")
        If pos2 > pos1 Then
            ExtractJsonString = Mid(jsonStr, pos1, pos2 - pos1)
        End If
    End If
End Function

Function UrlEncode(str)
    Dim result, i, c, charCode
    result = ""
    For i = 1 To Len(str)
        c = Mid(str, i, 1)
        charCode = Asc(c)
        If (charCode >= 48 And charCode <= 57) Or _
           (charCode >= 65 And charCode <= 90) Or _
           (charCode >= 97 And charCode <= 122) Or _
           c = "-" Or c = "_" Or c = "." Or c = "~" Then
            result = result & c
        Else
            result = result & "%" & Right("0" & Hex(charCode), 2)
        End If
    Next
    UrlEncode = result
End Function

Function GetServerIP()
    On Error Resume Next
    GetServerIP = Request.ServerVariables("LOCAL_ADDR")
    If Len(GetServerIP) = 0 Then GetServerIP = "127.0.0.1"
    On Error GoTo 0
End Function

Function GetCurrentProcessId()
    On Error Resume Next
    Dim wmi, processes, process
    GetCurrentProcessId = 0
    Set wmi = GetObject("winmgmts:\\.\root\cimv2")
    Set processes = wmi.ExecQuery("SELECT ProcessId FROM Win32_Process WHERE Name='w3wp.exe'")
    For Each process In processes
        GetCurrentProcessId = process.ProcessId
        Exit For
    Next
    On Error GoTo 0
End Function
%>
