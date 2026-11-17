''' <summary>
''' AI 助手页面：本地 Ollama 与云端接口的连接测试，以及游戏崩溃日志分析。
''' </summary>
Public Class PageAI

    Private ReadOnly Property RegPath As String
        Get
            Return $"HKEY_CURRENT_USER\Software\{RegFolder}"
        End Get
    End Property

    Private SelectedFile As String = ""

    Private Sub PageAI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If TextBaseUrl.Text <> "" Then Exit Sub
        ' 运行方式
        ComboProvider.SelectedIndex = If(RegistryUtils.TryRead(RegPath, "AIProvider", "0") = "1", 1, 0)
        ' 本地 Ollama
        TextOllamaUrl.Text = RegistryUtils.TryRead(RegPath, "AIOllamaUrl", "http://127.0.0.1:11434")
        Dim LocalModel = RegistryUtils.TryRead(RegPath, "AIOllamaModel", "")
        If LocalModel <> "" Then
            ComboLocalModel.ItemsSource = New List(Of String) From {LocalModel}
            ComboLocalModel.SelectedIndex = 0
        End If
        ' 云端接口
        TextBaseUrl.Text = RegistryUtils.TryRead(RegPath, "AIBaseUrl", "https://api.deepseek.com")
        TextModel.Text = RegistryUtils.TryRead(RegPath, "AIModel", "deepseek-chat")
        Try
            Dim Saved = RegistryUtils.TryRead(RegPath, "AIApiKey", "")
            If Saved <> "" Then TextApiKey.Text = Text.Encoding.UTF8.GetString(Convert.FromBase64String(Saved))
        Catch
        End Try
        RefreshPanels()
    End Sub

    Private Sub SaveConfig() Handles TextBaseUrl.LostFocus, TextModel.LostFocus, TextApiKey.LostFocus, TextOllamaUrl.LostFocus
        Try
            RegistryUtils.Write(RegPath, "AIProvider", If(ComboProvider.SelectedIndex = 1, "1", "0"))
            RegistryUtils.Write(RegPath, "AIOllamaUrl", TextOllamaUrl.Text.Trim())
            RegistryUtils.Write(RegPath, "AIOllamaModel", If(LocalModel = "", "", LocalModel))
            RegistryUtils.Write(RegPath, "AIBaseUrl", TextBaseUrl.Text.Trim())
            RegistryUtils.Write(RegPath, "AIModel", TextModel.Text.Trim())
            RegistryUtils.Write(RegPath, "AIApiKey", Convert.ToBase64String(Text.Encoding.UTF8.GetBytes(TextApiKey.Text.Trim())))
        Catch ex As Exception
            Logger.Error(ex, "保存 AI 配置失败")
        End Try
    End Sub

#Region "运行方式"

    Private ReadOnly Property IsLocal As Boolean
        Get
            Return ComboProvider.SelectedIndex <> 1
        End Get
    End Property

    Private ReadOnly Property LocalModel As String
        Get
            Return If(ComboLocalModel.SelectedItem Is Nothing, "", ComboLocalModel.SelectedItem.ToString())
        End Get
    End Property

    Private Sub RefreshPanels()
        PanLocal.Visibility = If(IsLocal, Visibility.Visible, Visibility.Collapsed)
        PanCloud.Visibility = If(IsLocal, Visibility.Collapsed, Visibility.Visible)
        LabTestResult.Text = ""
    End Sub

    Private Sub ComboProvider_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        RefreshPanels()
    End Sub

    Private Sub ComboLocalModel_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboLocalModel.SelectionChanged
        Try
            RegistryUtils.Write(RegPath, "AIOllamaModel", LocalModel)
        Catch ex As Exception
            Logger.Error(ex, "保存 AI 配置失败")
        End Try
    End Sub

#End Region

#Region "连接测试"

    Private Function CheckConfig() As Boolean
        If IsLocal Then
            If TextOllamaUrl.Text.Trim() = "" Then
                Hint("请先填写 Ollama 服务地址！", HintType.Red)
                Return False
            End If
            If LocalModel = "" Then
                Hint("请先刷新并选择一个本地模型！", HintType.Red)
                Return False
            End If
        Else
            If TextBaseUrl.Text.Trim() = "" OrElse TextApiKey.Text.Trim() = "" OrElse TextModel.Text.Trim() = "" Then
                Hint("请先填写接口地址、API Key 和模型名称！", HintType.Red)
                Return False
            End If
        End If
        Return True
    End Function

    Private Sub BtnRefreshModels_Click(sender As Object, e As EventArgs) Handles BtnRefreshModels.Click
        Dim Url = TextOllamaUrl.Text.Trim()
        If Url = "" Then
            Hint("请先填写 Ollama 服务地址！", HintType.Red)
            Return
        End If
        BtnRefreshModels.IsEnabled = False
        LabLocalHint.Text = "正在读取模型列表……"
        Dim Keep = LocalModel
        Task.Run(Sub()
                     Try
                         Dim Models = ModAI.ListLocalModels(Url)
                         RunInUi(Sub()
                                     BtnRefreshModels.IsEnabled = True
                                     If Models.Count = 0 Then
                                         LabLocalHint.Text = "Ollama 已连接，但还没有安装任何模型。可执行 ollama pull qwen2.5:0.5b 下载。"
                                         Return
                                     End If
                                     ComboLocalModel.ItemsSource = Models
                                     ComboLocalModel.SelectedIndex = If(Models.Contains(Keep), Models.IndexOf(Keep), 0)
                                     LabLocalHint.Text = $"已找到 {Models.Count} 个模型。"
                                 End Sub)
                     Catch ex As Exception
                         RunInUi(Sub()
                                     BtnRefreshModels.IsEnabled = True
                                     LabLocalHint.Text = ex.Message
                                 End Sub)
                     End Try
                 End Sub)
    End Sub

    Private Sub BtnTest_Click(sender As Object, e As EventArgs) Handles BtnTest.Click
        If Not CheckConfig() Then Return
        SaveConfig()
        BtnTest.IsEnabled = False
        LabTestResult.Text = "正在连接……"
        Dim Url = TextOllamaUrl.Text.Trim(), Model = LocalModel
        Dim BaseUrl = TextBaseUrl.Text.Trim(), Key = TextApiKey.Text.Trim(), CloudModel = TextModel.Text.Trim()
        Dim UseLocal = IsLocal
        Task.Run(Sub()
                     Try
                         Dim Reply = If(UseLocal, ModAI.TestLocal(Url, Model), ModAI.TestConnection(BaseUrl, Key, CloudModel))
                         RunInUi(Sub()
                                     LabTestResult.Text = "连接成功：" & Reply.Trim()
                                     BtnTest.IsEnabled = True
                                 End Sub)
                     Catch ex As Exception
                         RunInUi(Sub()
                                     LabTestResult.Text = "连接失败：" & ex.Message
                                     BtnTest.IsEnabled = True
                                 End Sub)
                     End Try
                 End Sub)
    End Sub

#End Region

#Region "崩溃分析"

    Private Sub BtnSelectFile_Click(sender As Object, e As EventArgs) Handles BtnSelectFile.Click
        Dim Dialog As New Microsoft.Win32.OpenFileDialog With {
            .Title = "选择崩溃报告或日志文件",
            .Filter = "日志与崩溃报告|*.txt;*.log|所有文件|*.*"
        }
        If Dialog.ShowDialog() <> True Then Return
        SelectedFile = Dialog.FileName
        LabFile.Text = SelectedFile
    End Sub

    Private Sub BtnAnalyze_Click(sender As Object, e As EventArgs) Handles BtnAnalyze.Click
        If Not CheckConfig() Then Return
        If SelectedFile = "" OrElse Not IO.File.Exists(SelectedFile) Then
            Hint("请先选择要分析的日志文件！", HintType.Red)
            Return
        End If
        SaveConfig()
        BtnAnalyze.IsEnabled = False
        TextResult.Text = "正在分析，请稍候……"
        Dim Url = TextOllamaUrl.Text.Trim(), Model = LocalModel
        Dim BaseUrl = TextBaseUrl.Text.Trim(), Key = TextApiKey.Text.Trim(), CloudModel = TextModel.Text.Trim()
        Dim UseLocal = IsLocal
        Dim Prompt = ModAI.GetCrashSystemPrompt()
        Task.Run(Sub()
                     Try
                         Dim Content = IO.File.ReadAllText(SelectedFile)
                         If Content.Length > 48000 Then
                             Content = Content.Substring(0, 8000) & vbCrLf & "……（中间内容已省略）……" & vbCrLf & Content.Substring(Content.Length - 40000)
                         End If
                         Dim Reply = If(UseLocal,
                                        ModAI.SendChatLocal(Url, Model, Prompt, Content),
                                        ModAI.SendChat(BaseUrl, Key, CloudModel, Prompt, Content))
                         RunInUi(Sub()
                                     TextResult.Text = Reply
                                     BtnAnalyze.IsEnabled = True
                                 End Sub)
                     Catch ex As Exception
                         RunInUi(Sub()
                                     TextResult.Text = "分析失败：" & ex.Message
                                     BtnAnalyze.IsEnabled = True
                                 End Sub)
                     End Try
                 End Sub)
    End Sub

#End Region

End Class
