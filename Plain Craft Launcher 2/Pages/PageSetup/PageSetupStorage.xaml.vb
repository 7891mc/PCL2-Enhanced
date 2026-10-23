Public Class PageSetupStorage
    Inherits MyPageRight
    Implements IRefreshable

    Private Sub PageSetupStorage_Loaded() Handles Me.Loaded
        Refresh()
    End Sub

    ''' <summary>
    ''' 刷新存储管理列表。
    ''' </summary>
    Public Sub Refresh() Implements IRefreshable.Refresh
        Try
            PanList.Children.Clear()
            AddFolderCard("游戏目录（.minecraft）", McFolderSelected, False)
            AddFolderCard("版本文件夹（versions）", McFolderSelected & "versions\", False)
            AddFolderCard("缓存目录", PathTemp, True)
            AddFolderCard("崩溃报告", McFolderSelected & "crash-reports\", False)
        Catch ex As Exception
            Logger.Error(ex, "刷新存储管理失败")
        End Try
    End Sub

    Private Sub AddFolderCard(Title As String, Folder As String, CanClean As Boolean)
        Dim Size As Long = GetFolderSize(Folder)
        Dim Card As New MyCard With {.Margin = New Thickness(0, 0, 0, 15), .MinHeight = 58}
        Dim Grid1 As New Grid With {.Margin = New Thickness(22, 8, 25, 8)}
        Grid1.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(170)})
        Grid1.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Grid1.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        '标题与大小
        Dim PanText As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center}
        Dim LabTitle As New TextBlock With {.Text = Title, .FontSize = 14, .TextTrimming = TextTrimming.CharacterEllipsis}
        Dim LabSize As New TextBlock With {.Text = If(DirectoryUtils.Exists(Folder), "占用 " & FormatSize(Size), "（文件夹不存在）"), .FontSize = 12, .Foreground = New SolidColorBrush(Color.FromRgb(128, 128, 128)), .Margin = New Thickness(0, 3, 0, 0)}
        PanText.Children.Add(LabTitle)
        PanText.Children.Add(LabSize)
        Grid.SetColumn(PanText, 0)
        Grid1.Children.Add(PanText)
        '按钮
        Dim PanButtons As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim BtnOpen As New MyButton With {.Text = "打开文件夹", .MinWidth = 110, .Height = 30, .Margin = New Thickness(0, 0, 8, 0)}
        AddHandler BtnOpen.Click, Sub() OpenExplorer(Folder)
        PanButtons.Children.Add(BtnOpen)
        If CanClean Then
            Dim BtnClean As New MyButton With {.Text = "清理", .MinWidth = 72, .Height = 30}
            AddHandler BtnClean.Click, Sub() CleanFolder(Folder)
            PanButtons.Children.Add(BtnClean)
        End If
        Grid.SetColumn(PanButtons, 2)
        Grid1.Children.Add(PanButtons)
        Card.Children.Add(Grid1)
        PanList.Children.Add(Card)
    End Sub

    Private Sub CleanFolder(Folder As String)
        If MyMsgBox($"确定清理「{Folder}」中的全部内容吗？此操作不可恢复！", "清理文件夹", "清理", "取消", IsWarn:=True) <> 1 Then Return
        Try
            Dim Info = DirectoryUtils.GetInfo(Folder)
            If Info.Exists Then
                For Each D In Info.EnumerateDirectories()
                    DirectoryUtils.Delete(D.FullName, True)
                Next
                For Each F In Info.EnumerateFiles()
                    FileUtils.Delete(F.FullName)
                Next
            End If
            Hint("清理完成！", HintType.Green)
            Refresh()
        Catch ex As Exception
            Logger.Error(ex, "清理文件夹失败", LogBehavior.Alert)
        End Try
    End Sub

    Private Function GetFolderSize(Folder As String) As Long
        Try
            Dim Info = DirectoryUtils.GetInfo(Folder)
            If Not Info.Exists Then Return 0
            Dim Size As Long = 0
            For Each F In Info.EnumerateFiles("*", SearchOption.AllDirectories)
                Size += F.Length
            Next
            Return Size
        Catch ex As Exception
            Return 0
        End Try
    End Function

    Private Function FormatSize(Size As Long) As String
        If Size < 1024 Then Return $"{Size} B"
        If Size < 1024 * 1024 Then Return $"{Size / 1024.0:F1} KB"
        If Size < 1024 * 1024 * 1024 Then Return $"{Size / 1024.0 / 1024.0:F1} MB"
        Return $"{Size / 1024.0 / 1024.0 / 1024.0:F1} GB"
    End Function
End Class
