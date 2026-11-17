''' <summary>
''' 云端 AI 助手：OpenAI 兼容接口的连接测试与崩溃日志分析。
''' </summary>
Public Module ModAI

    ''' <summary>
    ''' 组合聊天补全接口地址，兼容填写根地址、/v1 地址或完整地址。
    ''' </summary>
    Private Function GetChatUrl(BaseUrl As String) As String
        Dim Url = BaseUrl.Trim().TrimEnd("/"c)
        If Url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase) Then Return Url
        If Url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) Then Return Url & "/chat/completions"
        Return Url & "/v1/chat/completions"
    End Function

    ''' <summary>
    ''' 发送一次对话请求并返回助手回复文本。失败时抛出带可读信息的异常。
    ''' </summary>
    Public Function SendChat(BaseUrl As String, ApiKey As String, Model As String, SystemPrompt As String, UserPrompt As String) As String
        Dim Body As New JObject From {
            {"model", Model},
            {"stream", False},
            {"messages", New JArray From {
                New JObject From {{"role", "system"}, {"content", SystemPrompt}},
                New JObject From {{"role", "user"}, {"content", UserPrompt}}
            }}
        }
        Dim Req = CType(Net.WebRequest.Create(GetChatUrl(BaseUrl)), Net.HttpWebRequest)
        Req.Method = "POST"
        Req.ContentType = "application/json; charset=utf-8"
        Req.Headers.Add("Authorization", "Bearer " & ApiKey)
        Req.Timeout = 90000
        Req.ReadWriteTimeout = 90000
        Req.UserAgent = "PCLFE/" & VersionBaseName
        Dim Payload = Text.Encoding.UTF8.GetBytes(Body.ToString(Newtonsoft.Json.Formatting.None))
        Using Stream = Req.GetRequestStream()
            Stream.Write(Payload, 0, Payload.Length)
        End Using
        Try
            Using Resp = CType(Req.GetResponse(), Net.HttpWebResponse)
                Using Reader As New IO.StreamReader(Resp.GetResponseStream(), Text.Encoding.UTF8)
                    Dim Json = JObject.Parse(Reader.ReadToEnd())
                    Dim Choices = Json("choices")
                    If Choices IsNot Nothing AndAlso Choices.Any() Then
                        Dim Content = Choices(0)("message")("content")
                        If Content IsNot Nothing Then Return Content.ToString()
                    End If
                    Throw New Exception("接口返回内容无法识别")
                End Using
            End Using
        Catch ex As Net.WebException
            Dim Detail = ""
            If ex.Response IsNot Nothing Then
                Try
                    Using Reader As New IO.StreamReader(ex.Response.GetResponseStream(), Text.Encoding.UTF8)
                        Dim Raw = Reader.ReadToEnd()
                        Try
                            Dim ErrJson = JObject.Parse(Raw)
                            Detail = ErrJson("error")("message")?.ToString()
                        Catch
                        End Try
                        If String.IsNullOrEmpty(Detail) AndAlso Raw.Length < 300 Then Detail = Raw
                    End Using
                Catch
                End Try
            End If
            Throw New Exception(If(String.IsNullOrEmpty(Detail), ex.Message, Detail))
        End Try
    End Function

    ''' <summary>
    ''' 测试连接：发送一条极短消息，返回模型回复。
    ''' </summary>
    Public Function TestConnection(BaseUrl As String, ApiKey As String, Model As String) As String
        Return SendChat(BaseUrl, ApiKey, Model, "你是一个助手。", "请只回复四个字：连接成功")
    End Function

    ''' <summary>
    ''' 崩溃分析使用的系统提示词。
    ''' </summary>
    Public Function GetCrashSystemPrompt() As String
        Return "你是 Minecraft Java 版崩溃分析专家。用户会提供崩溃报告或日志。" &
               "请用中文回答，依次给出：1. 最可能的崩溃原因（一句话）；2. 关键证据（引用日志中的具体行）；3. 可操作的解决步骤（按优先级排序）。" &
               "如果日志信息不足，请明确说明还需要什么信息。不要编造日志中不存在的内容。"
    End Function

#Region "本地 Ollama"

    ''' <summary>
    ''' 补全 Ollama 接口地址，兼容填写根地址、带 /api 的地址或带 /v1 的地址。
    ''' </summary>
    Private Function GetOllamaUrl(BaseUrl As String, Path As String) As String
        Dim Url = BaseUrl.Trim().TrimEnd("/"c)
        If Url.EndsWith("/api", StringComparison.OrdinalIgnoreCase) Then Url = Url.Substring(0, Url.Length - 4)
        If Url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) Then Url = Url.Substring(0, Url.Length - 3)
        Return Url & Path
    End Function

    ''' <summary>
    ''' 读取本地 Ollama 已安装的模型名称列表。服务未启动时抛出带提示的异常。
    ''' </summary>
    Public Function ListLocalModels(BaseUrl As String) As List(Of String)
        Dim Req = CType(Net.WebRequest.Create(GetOllamaUrl(BaseUrl, "/api/tags")), Net.HttpWebRequest)
        Req.Method = "GET"
        Req.Timeout = 10000
        Req.ReadWriteTimeout = 10000
        Req.UserAgent = "PCLFE/" & VersionBaseName
        Try
            Using Resp = CType(Req.GetResponse(), Net.HttpWebResponse)
                Using Reader As New IO.StreamReader(Resp.GetResponseStream(), Text.Encoding.UTF8)
                    Dim Models = JObject.Parse(Reader.ReadToEnd())("models")
                    If Models Is Nothing Then Return New List(Of String)
                    Return Models.Select(Function(m) If(m("name") Is Nothing, "", m("name").ToString())).
                                   Where(Function(n) Not String.IsNullOrEmpty(n)).ToList()
                End Using
            End Using
        Catch ex As Net.WebException
            Throw New Exception("无法连接本地 Ollama，请确认服务已启动（" & BaseUrl & "）。", ex)
        End Try
    End Function

    ''' <summary>
    ''' 向本地 Ollama 发送一次对话请求并返回助手回复文本。
    ''' </summary>
    Public Function SendChatLocal(BaseUrl As String, Model As String, SystemPrompt As String, UserPrompt As String) As String
        Dim Body As New JObject From {
            {"model", Model},
            {"stream", False},
            {"options", New JObject From {{"temperature", 0.2}}},
            {"messages", New JArray From {
                New JObject From {{"role", "system"}, {"content", SystemPrompt}},
                New JObject From {{"role", "user"}, {"content", UserPrompt}}
            }}
        }
        Dim Req = CType(Net.WebRequest.Create(GetOllamaUrl(BaseUrl, "/api/chat")), Net.HttpWebRequest)
        Req.Method = "POST"
        Req.ContentType = "application/json; charset=utf-8"
        Req.Timeout = 180000
        Req.ReadWriteTimeout = 180000
        Req.UserAgent = "PCLFE/" & VersionBaseName
        Dim Payload = Text.Encoding.UTF8.GetBytes(Body.ToString(Newtonsoft.Json.Formatting.None))
        Using Stream = Req.GetRequestStream()
            Stream.Write(Payload, 0, Payload.Length)
        End Using
        Try
            Using Resp = CType(Req.GetResponse(), Net.HttpWebResponse)
                Using Reader As New IO.StreamReader(Resp.GetResponseStream(), Text.Encoding.UTF8)
                    Dim Json = JObject.Parse(Reader.ReadToEnd())
                    Dim Content = If(Json("message") Is Nothing, Nothing, Json("message")("content"))
                    If Content Is Nothing Then Throw New Exception("接口返回内容无法识别")
                    Return Content.ToString()
                End Using
            End Using
        Catch ex As Net.WebException
            Dim Detail = ""
            If ex.Response IsNot Nothing Then
                Try
                    Using Reader As New IO.StreamReader(ex.Response.GetResponseStream(), Text.Encoding.UTF8)
                        Dim ErrJson = JObject.Parse(Reader.ReadToEnd())
                        Detail = ErrJson("error")?.ToString()
                    End Using
                Catch
                End Try
            End If
            If ex.Status = Net.WebExceptionStatus.ConnectFailure Then Detail = "无法连接本地 Ollama，请确认服务已启动（" & BaseUrl & "）。"
            Throw New Exception(If(String.IsNullOrEmpty(Detail), ex.Message, Detail))
        End Try
    End Function

    ''' <summary>
    ''' 测试本地模型连接：发送一条极短消息，返回模型回复。
    ''' </summary>
    Public Function TestLocal(BaseUrl As String, Model As String) As String
        Return SendChatLocal(BaseUrl, Model, "你是一个助手。", "请只回复四个字：连接成功")
    End Function

#End Region

End Module
