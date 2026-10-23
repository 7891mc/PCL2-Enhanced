Public Class PageDownloadCart
    Inherits MyPageRight
    Implements IRefreshable

    ''' <summary>
    ''' 购物车条目（跨页面共享）。
    ''' </summary>
    Public Shared CartItems As New List(Of ResourceVersion)

    ''' <summary>
    ''' 将资源加入购物车（供资源详情页等调用）。
    ''' </summary>
    Public Shared Sub AddToCart(Entry As ResourceVersion)
        If Entry Is Nothing Then Return
        For Each Item In CartItems
            If Item.Id = Entry.Id Then
                Hint("该资源已在购物车中", HintType.Green)
                Return
            End If
        Next
        CartItems.Add(Entry)
        Hint($"已加入购物车（共 {CartItems.Count} 项）", HintType.Green)
    End Sub

    Private Sub PageDownloadCart_Loaded() Handles Me.Loaded
        RefreshUI()
    End Sub

    '强制刷新
    Private Sub RefreshSelf() Implements IRefreshable.Refresh
        RefreshUI()
    End Sub

    ''' <summary>
    ''' 刷新购物车 UI。
    ''' </summary>
    Public Sub RefreshUI()
        If PanList Is Nothing Then Return
        PanList.Children.Clear()
        For Each Entry In CartItems
            Dim Card As New MyCard With {.Margin = New Thickness(0, 0, 0, 10), .Title = " "}
            Dim Row As New Grid
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
            Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Auto)})
            Dim PanInfo As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 12, 0)}
            Dim LabName As New TextBlock With {.Text = Entry.Display, .FontSize = 15, .UseLayoutRounding = True, .SnapsToDevicePixels = True, .Foreground = FindResource("ColorBrush1"), .TextTrimming = TextTrimming.CharacterEllipsis}
            Dim LabMeta As New TextBlock With {.Text = $"版本 {Entry.Version}" & If(Entry.Dependencies.Count > 0, $"  ·  {Entry.Dependencies.Count} 项前置", ""), .FontSize = 12, .Opacity = 0.6, .Margin = New Thickness(0, 4, 0, 0)}
            PanInfo.Children.Add(LabName)
            PanInfo.Children.Add(LabMeta)
            Row.Children.Add(PanInfo)
            Dim BtnRemove As New MyButton With {.Text = "移除", .MinWidth = 64, .Height = 28, .Margin = New Thickness(0, 0, 8, 0), .VerticalAlignment = VerticalAlignment.Center, .Tag = Entry}
            AddHandler BtnRemove.Click, AddressOf Remove_Click
            Row.Children.Add(BtnRemove)
            Card.Children.Add(Row)
            PanList.Children.Add(Card)
        Next
        '空状态切换
        If CartItems.Count = 0 Then
            PanBottom.Visibility = Visibility.Collapsed
            PanEmpty.Opacity = 1
            PanList.Opacity = 0
        Else
            PanBottom.Visibility = Visibility.Visible
            PanEmpty.Opacity = 0
            PanList.Opacity = 1
            BtnInstall.Text = $"开始安装（{CartItems.Count} 项）"
        End If
    End Sub

    Private Sub Remove_Click(sender As MyButton, e As EventArgs)
        Dim Entry = TryCast(sender.Tag, ResourceVersion)
        If Entry IsNot Nothing Then CartItems.Remove(Entry)
        RefreshUI()
    End Sub

    Private Sub BtnClear_Click() Handles BtnClear.Click
        ClearCart()
    End Sub

    ''' <summary>
    ''' 清空购物车（带确认，供导航栏按钮调用）。
    ''' </summary>
    Public Sub ClearCart()
        If CartItems.Count = 0 Then Return
        If MyMsgBox($"确定要清空购物车中的 {CartItems.Count} 项资源吗？", "清空购物车", "清空", "取消") = 1 Then
            CartItems.Clear()
            RefreshUI()
            Hint("购物车已清空", HintType.Green)
        End If
    End Sub

    Private Sub BtnInstall_Click() Handles BtnInstall.Click
        If CartItems.Count = 0 Then Return
        Dim Instance = McInstanceSelected
        If Instance Is Nothing Then
            Hint("未找到目标版本，请先在「下载」页面选择要安装的版本", HintType.Red)
            Return
        End If
        Dim ModsFolder = Instance.PathIndie & "mods\"
        DirectoryUtils.Create(ModsFolder)
        Dim VanillaVersion = Instance.Version.VanillaName
        Dim TargetLoader = Instance.Version.ModLoaders
        Dim FileList As New List(Of NetFile)
        Dim FileCopyList As New Dictionary(Of String, String)
        Dim DownloadedNames As New List(Of String)
        For Each Entry In CartItems.ToList
            '跳过目标文件夹中已存在的同名文件
            If FileUtils.Exists(ModsFolder & Entry.FileName) Then Continue For
            Dim TempAddress = PathTemp & "mods\" & Entry.FileName
            If FileCopyList.ContainsKey(TempAddress) Then Continue For
            FileList.Add(Entry.ToNetFile(TempAddress, ResourceVersion.DownloadReason.Standalone, VanillaVersion, TargetLoader))
            FileCopyList(TempAddress) = ModsFolder & Entry.FileName
            DownloadedNames.Add(Entry.Display)
        Next
        If FileList.Count = 0 Then
            Hint("购物车中的资源已全部安装，无需重复下载", HintType.Green)
            Return
        End If
        Dim InstallLoaders As New List(Of LoaderBase)
        InstallLoaders.Add(New LoaderDownload($"下载购物车资源（{FileList.Count} 项）", FileList) With {.ProgressWeight = 10})
        InstallLoaders.Add(New LoaderTask(Of List(Of NetFile), Integer)("移动到 Mod 文件夹",
        Sub()
            Try
                For Each Entry As KeyValuePair(Of String, String) In FileCopyList
                    If FileUtils.Exists(Entry.Key) Then
                        If DirectoryUtils.Exists(PathUtils.RemoveLastPart(Entry.Value)) Then
                            FileUtils.Move(Entry.Key, Entry.Value)
                        Else
                            Logger.Warn($"购物车目标文件夹不存在：{Entry.Value}")
                        End If
                    Else
                        Logger.Warn($"购物车下载文件不存在：{Entry.Key}")
                    End If
                Next
            Catch ex As Exception
                Logger.Warn(ex, "移动购物车资源失败")
            End Try
        End Sub))
        '完成：从购物车移除已安装项并刷新模组列表
        InstallLoaders.Add(New LoaderTask(Of List(Of NetFile), Integer)("完成",
        Sub()
            For Each DownloadedName In DownloadedNames
                For Each Item In CartItems.ToList
                    If Item.Display = DownloadedName Then CartItems.Remove(Item)
                Next
            Next
            RunInUi(Sub()
                RefreshUI()
                Hint("购物车资源安装完成！", HintType.Green)
            End Sub)
            If FrmInstanceMod IsNot Nothing Then
                RunInUi(Sub() FrmInstanceMod.ReloadModList(True))
            End If
        End Sub))
        Dim MainLoader As New LoaderCombo(Of Object)("安装购物车资源", InstallLoaders)
        LoaderTaskbarAdd(MainLoader)
    End Sub

End Class
