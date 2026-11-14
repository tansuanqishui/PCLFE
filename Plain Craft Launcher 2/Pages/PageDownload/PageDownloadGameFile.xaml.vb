''' <summary>
''' 下载栏页面：列出全部原版游戏版本，点击后下载该版本的 JSON 文件或客户端 JAR。
''' </summary>
Public Class PageDownloadGameFile

    ''' <summary>
    ''' 要下载的文件类型。
    ''' </summary>
    Public Enum GameFileType
        Json = 0
        Jar = 1
    End Enum

    ''' <summary>
    ''' 当前页面要下载的文件类型，由左侧栏切换时设置。
    ''' </summary>
    Public Mode As GameFileType = GameFileType.Json

    Private ReadOnly Property FileTypeName As String
        Get
            Return If(Mode = GameFileType.Json, "JSON", "JAR")
        End Get
    End Property

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(LoadVersion, PanLoad, PanAllBack, Nothing, DlClientListLoader, AddressOf LoadVersionFinished)
    End Sub

#Region "版本列表"

    Private Sub LoadVersionFinished(Loader As LoaderTask(Of String, DlClientListResult))
        Try
            ShowVersionList(CType(Loader.Output.Value("versions").DeepClone(), JArray))
        Catch ex As Exception
            Logger.Error(ex, "可视化游戏文件版本列表出错")
            Hint("版本列表处理失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    Private Sub ShowVersionList(Versions As JArray)
        '分类
        Dim Dict As New Dictionary(Of String, List(Of JObject)) From {
            {"正式版", New List(Of JObject)}, {"预览版", New List(Of JObject)}, {"远古版", New List(Of JObject)}, {"愚人节版", New List(Of JObject)}}
        For Each Version As JObject In Versions
            Dict(GetVersionType(Version)).Add(Version)
        Next
        For Each Pair In Dict.ToList
            Dict(Pair.Key) = Pair.Value.OrderByDescending(Function(j) j("releaseTime").Value(Of Date)).ToList
        Next
        '输出
        PanVersions.Children.Clear()
        For Each Pair In Dict
            If Not Pair.Value.Any() Then Continue For
            Dim NewCard As New MyCard With {.Title = Pair.Key, .Margin = New Thickness(0, 0, 0, 15)}
            Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0)}
            NewCard.Children.Add(NewStack)
            NewCard.SwapControl = NewStack
            NewCard.SetContentBuilder(Pair.Value, Function(Version) VersionListItem(Version))
            NewCard.IsSwapped = True
            PanVersions.Children.Add(NewCard)
        Next
    End Sub

    ''' <summary>
    ''' 按原版游戏页面的规则分类版本，但不修改原始 Json。
    ''' </summary>
    Private Function GetVersionType(Version As JObject) As String
        Select Case Version("type").ToString
            Case "release"
                Return "正式版"
            Case "snapshot"
                Dim Id As String = Version("id").ToString
                'Mojang 误分类
                If Id.StartsWithF("1.") AndAlso
                    Not Id.ToLower.Contains("combat") AndAlso
                    Not Id.ToLower.Contains("rc") AndAlso
                    Not Id.ToLower.Contains("experimental") AndAlso
                    Not Id.ToLower.Contains("pre") Then
                    Return "正式版"
                End If
                '4/1 自动视作愚人节版
                Dim ReleaseDate = Version("releaseTime").Value(Of Date).ToUniversalTime().AddHours(2)
                If ReleaseDate.Month = 4 AndAlso ReleaseDate.Day = 1 Then Return "愚人节版"
                Return "预览版"
            Case "special"
                Return "愚人节版"
            Case Else
                Return "远古版"
        End Select
    End Function

    Private Function VersionListItem(Entry As JObject) As UIElement
        Dim Logo As String
        Select Case GetVersionType(Entry)
            Case "正式版"
                Logo = PathImage & "Blocks/Grass.png"
            Case "预览版"
                Logo = PathImage & "Blocks/CommandBlock.png"
            Case "愚人节版"
                Logo = PathImage & "Blocks/GoldBlock.png"
            Case Else
                Logo = PathImage & "Blocks/CobbleStone.png"
        End Select
        Return New MyVirtualizingElement(Of MyListItem)(
            Function()
                Dim NewItem As New MyListItem With {.Logo = Logo, .SnapsToDevicePixels = True, .Title = Entry("id").ToString, .Height = 42, .Type = MyListItem.CheckType.Clickable, .Tag = Entry}
                NewItem.Info = Entry("releaseTime").Value(Of Date).ToString("yyyy'/'MM'/'dd HH':'mm")
                AddHandler NewItem.Click, AddressOf VersionClick
                Return NewItem
            End Function) With {.Height = 42}
    End Function

#End Region

#Region "下载"

    Private Sub VersionClick(sender As Object, e As EventArgs)
        DownloadVersionFile(CType(CType(sender, MyListItem).Tag, JObject))
    End Sub

    Private Sub DownloadVersionFile(Version As JObject)
        Try
            Dim Id As String = Version("id").ToString
            Dim TaskName As String = $"Minecraft {Id} {FileTypeName} 下载"
            '选择保存位置
            Dim SaveFolder As String = Dialogs.SelectFolder("选择保存位置", False).FirstOrDefault
            If SaveFolder Is Nothing Then Return
            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar.ToList()
                If OngoingLoader.Name <> TaskName Then Continue For
                Hint($"该版本的 {FileTypeName} 正在下载中！", HintType.Red)
                Return
            Next
            '组装加载器
            Dim Loaders As New List(Of LoaderBase)
            If Mode = GameFileType.Json Then
                Loaders.Add(New LoaderDownload("下载游戏 JSON", New List(Of NetFile) From {
                    New NetFile(DlSourceLauncherOrMetaGet(Version("url").ToString), IO.Path.Combine(SaveFolder, Id & ".json"), New FileChecker With {.IsJson = True})
                }))
            Else
                Dim TempFolder As String = PathTemp & "PCLFEGameJar\" & Id & "\"
                Dim TempJson As String = TempFolder & Id & ".json"
                Dim SavePath As String = IO.Path.Combine(SaveFolder, Id & ".jar")
                Loaders.Add(New LoaderDownload("下载版本 JSON 文件", New List(Of NetFile) From {
                    New NetFile(DlSourceLauncherOrMetaGet(Version("url").ToString), TempJson, New FileChecker With {.IsJson = True})
                }) With {.ProgressWeight = 2})
                Loaders.Add(New LoaderTask(Of String, List(Of NetFile))("分析游戏 JAR 下载地址",
                    Sub(Task)
                        Dim Json As JObject = FileUtils.ReadAsJson(TempJson)
                        Dim Downloads = Json("downloads")
                        Dim Client = If(Downloads Is Nothing, Nothing, Downloads("client"))
                        If Client Is Nothing OrElse Client("url") Is Nothing Then Throw New Exception("该版本没有可下载的客户端 JAR 文件")
                        Dim Checker As New FileChecker With {
                            .MinSize = 1024,
                            .ActualSize = If(Client("size") Is Nothing, -1, Client("size").Value(Of Long)()),
                            .Hash = If(Client("sha1") Is Nothing, Nothing, Client("sha1").ToString())
                        }
                        Task.Output = New List(Of NetFile) From {New NetFile(DlSourceLauncherOrMetaGet(Client("url").ToString), SavePath, Checker)}
                    End Sub) With {.ProgressWeight = 0.5, .Show = False})
                Loaders.Add(New LoaderDownload("下载游戏 JAR", New List(Of NetFile)) With {.ProgressWeight = 8})
            End If
            '启动
            Dim Loader As New LoaderCombo(Of String)(TaskName, Loaders) With {
                .OnStateChanged = Sub(L)
                                        Select Case L.State
                                            Case LoadState.Finished
                                                Hint($"已保存到 {SaveFolder}", HintType.Green)
                                            Case LoadState.Failed
                                                Hint($"下载失败：{L.Error.GetDisplay(False)}", HintType.Red)
                                            Case LoadState.Canceled
                                                Hint("下载已取消！", HintType.Blue)
                                        End Select
                                    End Sub}
            Loader.Start(Id)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Logger.Error(ex, "开始下载游戏文件失败")
            Hint("开始下载失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

#End Region

End Class
