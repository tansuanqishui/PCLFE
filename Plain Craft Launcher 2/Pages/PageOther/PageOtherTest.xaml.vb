Public Class PageOtherTest

    ''' <summary>
    ''' 开始一个自定义多线程下载任务。
    ''' </summary>
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Try
            Url = If(Url, "").Trim()
            If Not (Url.StartsWithF("http://", True) OrElse Url.StartsWithF("https://", True)) Then
                Hint("下载地址必须是 http 或 https 链接！", HintType.Red)
                Return
            End If
            If String.IsNullOrWhiteSpace(FileName) OrElse FileName = "未知" Then
                FileName = PathUtils.GetLastPart(Url.Split("?"c)(0))
            End If
            If String.IsNullOrWhiteSpace(FileName) Then FileName = "下载文件"
            If String.IsNullOrWhiteSpace(Folder) Then
                Folder = Dialogs.SelectFolder("选择保存位置", False).FirstOrDefault()
                If String.IsNullOrEmpty(Folder) Then Return '用户取消了选择
            End If
            If Not Folder.EndsWith("\") Then Folder &= "\"
            Dim FilePath As String = Folder & FileName
            '使用多线程下载引擎后台下载（任务名包含“自定义下载”）
            Dim Task As New LoaderCombo(Of Integer)("自定义下载 " & FileName & " ", {
                New LoaderDownload("下载文件", New List(Of NetFile) From {New NetFile({Url}, FilePath)}) With {.Block = True}
            }) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Task.Start(1)
            LoaderTaskbarAdd(Task)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            Hint("已加入下载列表：" & FileName, HintType.Green)
            '隐藏主题 跳票红：在百宝箱中下载任意一个文件后解锁（与原正版登录条件一致，一次即可）
            If ThemeUnlock(10, False) Then MyMsgBox("感谢你使用自定义多线程下载！" & vbCrLf & "隐藏主题 跳票红 已解锁！", "提示")
        Catch ex As Exception
            Logger.Error(ex, "开始自定义下载失败")
        End Try
    End Sub

    Private Sub BtnBrowse_Click(sender As Object, e As EventArgs) Handles BtnBrowse.Click
        Dim Folder As String = Dialogs.SelectFolder("选择保存位置", False).FirstOrDefault()
        If Not String.IsNullOrEmpty(Folder) Then TextFolder.Text = Folder
    End Sub

    Private Sub BtnStart_Click(sender As Object, e As EventArgs) Handles BtnStart.Click
        If String.IsNullOrWhiteSpace(TextUrl.Text) Then
            Hint("请输入下载地址！", HintType.Red)
            Return
        End If
        StartCustomDownload(TextUrl.Text, TextFileName.Text.Trim(), TextFolder.Text.Trim())
    End Sub

    Public Shared Sub Jrrp()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub RubbishClear()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Function GetRandomCave() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function

End Class
