Public Class PageInstanceBackup
    Inherits MyPageRight
    Implements IRefreshable

    Private Sub PageInstanceBackup_Loaded() Handles Me.Loaded
        Refresh()
    End Sub

    ''' <summary>
    ''' 刷新备份列表。
    ''' </summary>
    Public Sub Refresh() Implements IRefreshable.Refresh
        Try
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "刷新备份列表失败")
        End Try
    End Sub

    Private Function GetBackupFolder() As String
        Return PathTemp & "Backups\"
    End Function

    Private Sub BtnBackup_Click(sender As Object, e As EventArgs) Handles BtnBackup.Click
        If PageInstanceLeft.Instance Is Nothing Then
            Hint("请先选择一个版本！", HintType.Red)
            Return
        End If
        If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
            Hint("该版本正在被修改，请稍后再备份！", HintType.Red)
            Return
        End If
        If MyMsgBox($"确定备份当前版本「{PageInstanceLeft.Instance.Name}」吗？备份将保存版本文件夹中的全部内容。", "备份版本", "备份", "取消") <> 1 Then Return
        Try
            DirectoryUtils.Create(GetBackupFolder())
            Dim SafeName = PageInstanceLeft.Instance.Name.Replace("/", "_").Replace("\", "_").Replace(":", "_").Replace("*", "_").Replace("?", "_").Replace("""", "_").Replace("<", "_").Replace(">", "_").Replace("|", "_")
            Dim FileName = GetBackupFolder() & SafeName & "_" & Now.ToString("yyyyMMdd_HHmmss") & ".zip"
            Logger.Info($"正在备份版本 {PageInstanceLeft.Instance.Name} 到 {FileName}")
            FileUtils.CreateZipFromDirectory(FileName, PageInstanceLeft.Instance.PathVersion)
            Hint("备份完成！", HintType.Green)
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "备份版本失败", LogBehavior.Alert)
        End Try
    End Sub

    Private Sub RefreshList()
        PanList.Children.Clear()
        Dim Folder = GetBackupFolder()
        Dim FolderInfo = DirectoryUtils.GetInfo(Folder)
        If Not FolderInfo.Exists Then
            PanList.Children.Add(New MyHint With {.Text = "还没有任何备份，点击上方「备份当前版本」即可创建第一个备份。", .Theme = MyHint.Themes.Blue})
            Return
        End If
        Dim Files = FolderInfo.EnumerateFiles("*.zip").OrderByDescending(Function(f) f.LastWriteTime).ToList
        If Not Files.Any Then
            PanList.Children.Add(New MyHint With {.Text = "还没有任何备份，点击上方「备份当前版本」即可创建第一个备份。", .Theme = MyHint.Themes.Blue})
            Return
        End If
        For Each F In Files
            AddRow(F)
        Next
    End Sub

    Private Sub AddRow(F As IO.FileInfo)
        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 10), .Height = 52, .SnapsToDevicePixels = True, .UseLayoutRounding = True}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(150)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        '名称
        Dim PanText As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 10, 0)}
        Dim LabName As New TextBlock With {.Text = F.Name, .FontSize = 14, .TextTrimming = TextTrimming.CharacterEllipsis}
        Dim LabInfo As New TextBlock With {.Text = F.LastWriteTime.ToString("yyyy-MM-dd HH:mm") & "　·　" & FormatSize(F.Length), .FontSize = 12, .Foreground = New SolidColorBrush(Color.FromRgb(128, 128, 128)), .Margin = New Thickness(0, 3, 0, 0)}
        PanText.Children.Add(LabName)
        PanText.Children.Add(LabInfo)
        Grid.SetColumn(PanText, 0)
        Row.Children.Add(PanText)
        '按钮
        Dim PanButtons As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim BtnRestore As New MyButton With {.Text = "恢复", .MinWidth = 72, .Height = 30, .Margin = New Thickness(0, 0, 8, 0), .ColorType = MyButton.ColorState.Highlight}
        AddHandler BtnRestore.Click, Sub() RestoreBackup(F.FullName)
        Dim BtnDelete As New MyButton With {.Text = "删除", .MinWidth = 72, .Height = 30}
        AddHandler BtnDelete.Click, Sub() DeleteBackup(F.FullName)
        PanButtons.Children.Add(BtnRestore)
        PanButtons.Children.Add(BtnDelete)
        Grid.SetColumn(PanButtons, 2)
        Row.Children.Add(PanButtons)
        PanList.Children.Add(Row)
    End Sub

    Private Sub RestoreBackup(FileName As String)
        If PageInstanceLeft.Instance Is Nothing Then Return
        If McInstanceLock.IsLocked(PageInstanceLeft.Instance) Then
            Hint("该版本正在被修改，请稍后再恢复！", HintType.Red)
            Return
        End If
        If MyMsgBox($"确定用备份「{IO.Path.GetFileName(FileName)}」覆盖当前版本「{PageInstanceLeft.Instance.Name}」吗？版本文件夹中的现有文件将被同名覆盖！", "恢复备份", "恢复", "取消", IsWarn:=True) <> 1 Then Return
        Try
            Logger.Info($"正在恢复版本 {PageInstanceLeft.Instance.Name} 的备份：{FileName}")
            FileUtils.ExtractToDirectory(FileName, PageInstanceLeft.Instance.PathVersion)
            Hint("备份已恢复！", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "恢复备份失败", LogBehavior.Alert)
        End Try
    End Sub

    Private Sub DeleteBackup(FileName As String)
        If MyMsgBox($"确定删除备份「{IO.Path.GetFileName(FileName)}」吗？", "删除备份", "删除", "取消") <> 1 Then Return
        Try
            FileUtils.Delete(FileName)
            Hint("备份已删除。", HintType.Green)
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "删除备份失败", LogBehavior.Alert)
        End Try
    End Sub

    Private Function FormatSize(Size As Long) As String
        If Size < 1024 Then Return $"{Size} B"
        If Size < 1024 * 1024 Then Return $"{Size / 1024.0:F1} KB"
        If Size < 1024 * 1024 * 1024 Then Return $"{Size / 1024.0 / 1024.0:F1} MB"
        Return $"{Size / 1024.0 / 1024.0 / 1024.0:F1} GB"
    End Function
End Class
