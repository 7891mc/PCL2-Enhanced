Public Class PageInstanceScreenshot
    Inherits MyPageRight
    Implements IRefreshable

    Private Sub PageInstanceScreenshot_Loaded() Handles Me.Loaded
        Refresh()
    End Sub

    ''' <summary>
    ''' 刷新截图中心：汇总所有版本的游戏截图。
    ''' </summary>
    Public Sub Refresh() Implements IRefreshable.Refresh
        Try
            RefreshTargets()
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "刷新截图中心失败")
        End Try
    End Sub

    Private Sub BtnSyncAll_Click(sender As Object, e As EventArgs) Handles BtnSyncAll.Click
        Try
            Dim Target = TryCast(ComboCopyTarget.SelectedItem, MyComboBoxItem)
            Dim TargetInstance As McInstance = If(Target Is Nothing, Nothing, TryCast(Target.Tag, McInstance))
            If TargetInstance Is Nothing Then
                Hint("请先选择要同步到的目标版本！", HintType.Red)
                Return
            End If
            If PageInstanceLeft.Instance IsNot Nothing AndAlso McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
                Hint("当前版本正在被修改，请稍后再同步！", HintType.Red)
                Return
            End If
            '统计可同步的截图
            Dim Count As Integer = 0
            For Each Ins In GetInstances()
                Dim FolderInfo = DirectoryUtils.GetInfo(Ins.PathIndie & "screenshots\")
                If Not FolderInfo.Exists Then Continue For
                For Each F As IO.FileInfo In FolderInfo.EnumerateFiles("*.png").Concat(FolderInfo.EnumerateFiles("*.jpg")).Concat(FolderInfo.EnumerateFiles("*.jpeg"))
                    '跳过目标版本自己的截图
                    If Ins.Name.ToLower() = TargetInstance.Name.ToLower() Then Continue For
                    Dim TargetFolder = TargetInstance.PathIndie & "screenshots\"
                    DirectoryUtils.Create(TargetFolder)
                    Dim TargetFile = TargetFolder & F.Name
                    If FileUtils.Exists(TargetFile) Then Continue For
                    FileUtils.Copy(F.FullName, TargetFile)
                    Count += 1
                Next
            Next
            Hint(If(Count > 0, $"已同步 {Count} 张截图到「{TargetInstance.Name}」！", "没有需要同步的截图。"), If(Count > 0, HintType.Green, HintType.Blue))
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "同步截图失败", LogBehavior.Alert)
        End Try
    End Sub

    Private Sub RefreshTargets()
        ComboCopyTarget.Items.Clear()
        Dim AllInstances = GetInstances()
        For Each Ins In AllInstances
            ComboCopyTarget.Items.Add(New MyComboBoxItem With {.Content = Ins.Name, .Tag = Ins})
        Next
        If AllInstances.Any Then ComboCopyTarget.SelectedIndex = 0
    End Sub

    ''' <summary>
    ''' 获取全部已安装的版本（优先使用实例列表缓存，否则枚举版本文件夹）。
    ''' </summary>
    Private Function GetInstances() As List(Of McInstance)
        Dim Result As New List(Of McInstance)
        Try
            If McInstanceList.Any(Function(p) p.Key <> McInstanceCardType.Error) Then
                For Each Ins In McInstanceList.Values.SelectMany(Function(l) l)
                    If Not Result.Any(Function(r) r.Name = Ins.Name) Then Result.Add(Ins)
                Next
            End If
            If Not Result.Any Then
                Dim VersionsInfo = DirectoryUtils.GetInfo(McFolderSelected & "versions\")
                If VersionsInfo.Exists Then
                    For Each SubDir In VersionsInfo.EnumerateDirectories().OrderBy(Function(d) d.Name)
                        Try
                            Result.Add(New McInstance(SubDir.Name).Load())
                        Catch ex As Exception
                        End Try
                    Next
                End If
            End If
        Catch ex As Exception
            Logger.Warn(ex, "获取版本列表失败")
        End Try
        Return Result
    End Function

    Private Sub RefreshList()
        PanList.Children.Clear()
        Dim AllInstances = GetInstances()
        If Not AllInstances.Any Then
            PanList.Children.Add(New MyHint With {.Text = "未找到任何已安装的版本，请先下载或安装游戏版本。", .Theme = MyHint.Themes.Yellow})
            Return
        End If
        Dim Count As Integer = 0
        For Each Ins In AllInstances
            Dim FolderInfo = DirectoryUtils.GetInfo(Ins.PathIndie & "screenshots\")
            If Not FolderInfo.Exists Then Continue For
            For Each F As IO.FileInfo In FolderInfo.EnumerateFiles("*.png").Concat(FolderInfo.EnumerateFiles("*.jpg")).Concat(FolderInfo.EnumerateFiles("*.jpeg")).OrderByDescending(Function(x) x.LastWriteTime)
                AddRow(F, Ins)
                Count += 1
            Next
        Next
        If Count = 0 Then PanList.Children.Add(New MyHint With {.Text = "各版本还没有任何截图，进入游戏按 F2 即可截图。", .Theme = MyHint.Themes.Blue})
    End Sub

    Private Sub AddRow(F As IO.FileInfo, Ins As McInstance)
        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 10), .Height = 56, .SnapsToDevicePixels = True, .UseLayoutRounding = True}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(98)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        '缩略图
        Dim Img As New Image With {.Width = 96, .Height = 54, .Stretch = Stretch.UniformToFill, .SnapsToDevicePixels = True}
        Try
            Img.Source = New System.Windows.Media.Imaging.BitmapImage(New Uri(F.FullName))
        Catch ex As Exception
        End Try
        Dim ImgBorder As New Border With {.Child = Img, .Margin = New Thickness(0, 0, 10, 0), .SnapsToDevicePixels = True, .UseLayoutRounding = True}
        ImgBorder.Clip = New RectangleGeometry(New Rect(0, 0, 96, 54), 4, 4)
        Grid.SetColumn(ImgBorder, 0)
        Row.Children.Add(ImgBorder)
        '名称与所属版本
        Dim PanText As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim LabName As New TextBlock With {.Text = F.Name, .FontSize = 14, .TextTrimming = TextTrimming.CharacterEllipsis}
        Dim LabInfo As New TextBlock With {.Text = $"所属版本：{Ins.Name}　·　{F.LastWriteTime.ToString("yyyy-MM-dd HH:mm")}", .FontSize = 12, .Foreground = New SolidColorBrush(Color.FromRgb(128, 128, 128)), .Margin = New Thickness(0, 3, 0, 0)}
        PanText.Children.Add(LabName)
        PanText.Children.Add(LabInfo)
        Grid.SetColumn(PanText, 1)
        Row.Children.Add(PanText)
        '按钮
        Dim PanButtons As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim BtnOpen As New MyButton With {.Text = "打开", .MinWidth = 72, .Height = 30, .Margin = New Thickness(0, 0, 8, 0)}
        AddHandler BtnOpen.Click, Sub() OpenExplorer(F.FullName)
        Dim BtnCopy As New MyButton With {.Text = "同步", .MinWidth = 72, .Height = 30, .Margin = New Thickness(0, 0, 8, 0)}
        AddHandler BtnCopy.Click, Sub() CopyOne(F, Ins)
        Dim BtnDelete As New MyButton With {.Text = "删除", .MinWidth = 72, .Height = 30}
        AddHandler BtnDelete.Click, Sub() DeleteOne(F)
        PanButtons.Children.Add(BtnOpen)
        PanButtons.Children.Add(BtnCopy)
        PanButtons.Children.Add(BtnDelete)
        Grid.SetColumn(PanButtons, 2)
        Row.Children.Add(PanButtons)
        PanList.Children.Add(Row)
    End Sub

    Private Sub CopyOne(F As IO.FileInfo, Ins As McInstance)
        Try
            Dim Target = TryCast(ComboCopyTarget.SelectedItem, MyComboBoxItem)
            Dim TargetInstance As McInstance = If(Target Is Nothing, Nothing, TryCast(Target.Tag, McInstance))
            If TargetInstance Is Nothing Then
                Hint("请先选择要同步到的目标版本！", HintType.Red)
                Return
            End If
            If Ins.Name.ToLower() = TargetInstance.Name.ToLower() Then
                Hint("该截图已属于目标版本，无需同步。", HintType.Blue)
                Return
            End If
            Dim TargetFolder = TargetInstance.PathIndie & "screenshots\"
            DirectoryUtils.Create(TargetFolder)
            Dim TargetFile = TargetFolder & F.Name
            If FileUtils.Exists(TargetFile) Then
                Hint("目标版本已存在同名截图，已跳过。", HintType.Blue)
                Return
            End If
            FileUtils.Copy(F.FullName, TargetFile)
            Hint($"已同步到「{TargetInstance.Name}」！", HintType.Green)
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "同步截图失败", LogBehavior.Alert)
        End Try
    End Sub

    Private Sub DeleteOne(F As IO.FileInfo)
        Try
            If MyMsgBox($"确定删除截图「{F.Name}」吗？", "删除截图", "删除", "取消") <> 1 Then Return
            FileUtils.Delete(F.FullName)
            Hint("截图已删除。", HintType.Green)
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "删除截图失败", LogBehavior.Alert)
        End Try
    End Sub
End Class
