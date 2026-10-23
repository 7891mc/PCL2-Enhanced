Public Class PageInstanceDataPack
    Inherits MyPageRight
    Implements IRefreshable

    Private Sub PageInstanceDataPack_Loaded() Handles Me.Loaded
        Refresh()
    End Sub

    ''' <summary>
    ''' 刷新数据包列表。
    ''' </summary>
    Public Sub Refresh() Implements IRefreshable.Refresh
        Try
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "刷新数据包列表失败")
        End Try
    End Sub

    Private Function GetDataPackFolder() As String
        Return PageInstanceLeft.Instance.PathIndie & "datapacks\"
    End Function

    Private Sub BtnInstall_Click(sender As Object, e As EventArgs) Handles BtnInstall.Click
        If PageInstanceLeft.Instance Is Nothing Then
            Hint("请先选择一个版本！", HintType.Red)
            Return
        End If
        Dim FileName As String = Dialogs.SelectFile("选择数据包", False, filter:={({"zip"}, "数据包压缩包")}).FirstOrDefault()
        If String.IsNullOrEmpty(FileName) Then Return
        Try
            DirectoryUtils.Create(GetDataPackFolder())
            FileUtils.Copy(FileName, GetDataPackFolder() & IO.Path.GetFileName(FileName))
            Hint("数据包已安装！可在新世界中通过 /datapack list 查看。", HintType.Green)
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "安装数据包失败", LogBehavior.Alert)
        End Try
    End Sub

    Private Sub BtnOpenFolder_Click(sender As Object, e As EventArgs) Handles BtnOpenFolder.Click
        If PageInstanceLeft.Instance Is Nothing Then Return
        DirectoryUtils.Create(GetDataPackFolder())
        OpenExplorer(GetDataPackFolder())
    End Sub

    Private Sub RefreshList()
        PanList.Children.Clear()
        If PageInstanceLeft.Instance Is Nothing Then Return
        Dim FolderInfo = DirectoryUtils.GetInfo(GetDataPackFolder())
        If Not FolderInfo.Exists Then
            PanList.Children.Add(New MyHint With {.Text = "该版本还没有数据包，点击上方「安装数据包」即可添加。", .Theme = MyHint.Themes.Blue})
            Return
        End If
        Dim Entries As New List(Of KeyValuePair(Of String, String))
        '文件夹形式的数据包
        For Each SubDir In FolderInfo.EnumerateDirectories().OrderBy(Function(d) d.Name)
            Entries.Add(New KeyValuePair(Of String, String)(SubDir.FullName, SubDir.Name))
        Next
        'Zip 形式的数据包
        For Each ZipEntry In FolderInfo.EnumerateFiles("*.zip").OrderBy(Function(ze) ze.Name)
            Entries.Add(New KeyValuePair(Of String, String)(ZipEntry.FullName, ZipEntry.Name))
        Next
        If Not Entries.Any Then
            PanList.Children.Add(New MyHint With {.Text = "该版本还没有数据包，点击上方「安装数据包」即可添加。", .Theme = MyHint.Themes.Blue})
            Return
        End If
        For Each Entry In Entries
            AddRow(Entry.Key, Entry.Value)
        Next
    End Sub

    Private Function ReadPackMeta(Path As String) As JObject
        Try
            If DirectoryUtils.Exists(Path) Then
                Dim MetaFile = Path & "pack.mcmeta"
                If Not FileUtils.Exists(MetaFile) Then Return Nothing
                Using Reader As New StreamReader(MetaFile)
                    Return Reader.ReadToEnd().DeserializeJson()
                End Using
            Else
                Using Archive = FileUtils.OpenZip(Path)
                    Dim Entry = Archive.GetEntry("pack.mcmeta")
                    If Entry Is Nothing Then Return Nothing
                    Using Reader As New StreamReader(Entry.Open)
                        Return Reader.ReadToEnd().DeserializeJson()
                    End Using
                End Using
            End If
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    Private Function GetDescription(Json As JObject) As String
        Try
            If Json Is Nothing Then Return ""
            Dim Pack = Json("pack")
            If Pack Is Nothing Then Return ""
            Dim Desc = Pack("description")
            If Desc Is Nothing Then Return ""
            If TypeOf Desc Is JValue Then Return Desc.ToString
            Dim Text = Desc("text")
            If Text IsNot Nothing Then Return Text.ToString
            Return Desc.ToString
        Catch ex As Exception
            Return ""
        End Try
    End Function

    Private Sub AddRow(Path As String, Name As String)
        Dim Description As String = GetDescription(ReadPackMeta(Path))
        Dim Row As New Grid With {.Margin = New Thickness(0, 0, 0, 10), .Height = 52, .SnapsToDevicePixels = True, .UseLayoutRounding = True}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        '名称与描述
        Dim PanText As New StackPanel With {.VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(0, 0, 10, 0)}
        Dim LabName As New TextBlock With {.Text = Name, .FontSize = 14, .TextTrimming = TextTrimming.CharacterEllipsis}
        Dim LabInfo As New TextBlock With {.Text = If(String.IsNullOrEmpty(Description), "（无描述信息）", Description), .FontSize = 12, .Foreground = New SolidColorBrush(Color.FromRgb(128, 128, 128)), .Margin = New Thickness(0, 3, 0, 0), .TextTrimming = TextTrimming.CharacterEllipsis}
        PanText.Children.Add(LabName)
        PanText.Children.Add(LabInfo)
        Grid.SetColumn(PanText, 0)
        Row.Children.Add(PanText)
        '按钮
        Dim PanButtons As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center}
        Dim BtnOpen As New MyButton With {.Text = "打开", .MinWidth = 72, .Height = 30, .Margin = New Thickness(0, 0, 8, 0)}
        AddHandler BtnOpen.Click, Sub() OpenExplorer(Path)
        Dim BtnDelete As New MyButton With {.Text = "删除", .MinWidth = 72, .Height = 30}
        AddHandler BtnDelete.Click, Sub() DeleteDataPack(Path, Name)
        PanButtons.Children.Add(BtnOpen)
        PanButtons.Children.Add(BtnDelete)
        Grid.SetColumn(PanButtons, 2)
        Row.Children.Add(PanButtons)
        PanList.Children.Add(Row)
    End Sub

    Private Sub DeleteDataPack(Path As String, Name As String)
        Try
            If MyMsgBox($"确定删除数据包「{Name}」吗？", "删除数据包", "删除", "取消") <> 1 Then Return
            If DirectoryUtils.Exists(Path) Then
                DirectoryUtils.Delete(Path, True)
            ElseIf FileUtils.Exists(Path) Then
                FileUtils.Delete(Path)
            End If
            Hint("数据包已删除。", HintType.Green)
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "删除数据包失败", LogBehavior.Alert)
        End Try
    End Sub
End Class
