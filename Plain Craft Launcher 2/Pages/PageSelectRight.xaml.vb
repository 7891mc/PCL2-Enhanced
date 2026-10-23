Public Class PageSelectRight

    '窗口基础
    Private Sub PageSelectRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.RunOnUpdated, MaxDepth:=1, ExtraPath:="versions\")
        PanBack.ScrollToHome()
    End Sub
    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, McInstanceListLoader, AddressOf McInstanceListUI, AutoRun:=False)
    End Sub
    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If McInstanceListLoader.State = LoadState.Failed Then
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        End If
    End Sub

    '窗口属性
    ''' <summary>
    ''' 是否显示隐藏的 Minecraft 版本。
    ''' </summary>
    Public ShowHidden As Boolean = False
    ''' <summary>
    ''' 当前的分组筛选（空字符串 = 全部，&lt;未分组&gt; = 未分组）。
    ''' </summary>
    Public GroupFilter As String = ""

    ''' <summary>
    ''' 判断实例是否通过分组筛选。
    ''' </summary>
    Private Function PassGroupFilter(Instance As McInstance) As Boolean
        If GroupFilter = "" Then Return True
        Dim GroupName = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "Group", "")
        If GroupFilter = "<未分组>" Then Return GroupName = ""
        Return GroupName = GroupFilter
    End Function

    ''' <summary>
    ''' 分组筛选变化后刷新列表 UI。
    ''' </summary>
    Private Sub RefreshGroupFilter()
        If McInstanceListLoader.State = LoadState.Loading Then Return
        McInstanceListUI(Nothing)
    End Sub

    ''' <summary>
    ''' 设置实例分组（供版本列表行按钮调用）。
    ''' </summary>
    Public Shared Sub SetInstanceGroup(Instance As McInstance)
        Dim CurrentGroup = ReadIni(Instance.PathVersion & "PCL\Setup.ini", "Group", "")
        Dim Input = MyMsgBoxInput("请输入该实例的分组名称（留空表示未分组）：", , CurrentGroup)
        If Input Is Nothing Then Return
        WriteIni(Instance.PathVersion & "PCL\Setup.ini", "Group", Input)
        McInstanceListForceRefresh = True
        LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Hint(If(Input = "", "已将该实例设为未分组", $"已将该实例加入分组「{Input}」"), HintType.Green)
    End Sub

    ''' <summary>
    ''' 获取实例 mods 文件夹中的文件名列表。
    ''' </summary>
    Public Shared Function GetModFiles(Instance As McInstance) As List(Of String)
        Dim Result As New List(Of String)
        Try
            Dim ModsFolder = Instance.PathIndie & "mods\"
            If DirectoryUtils.Exists(ModsFolder) Then
                For Each FileName In IO.Directory.EnumerateFiles(ModsFolder, "*.jar")
                    Result.Add(IO.Path.GetFileName(FileName))
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取实例 Mod 列表失败")
        End Try
        Return Result
    End Function

    ''' <summary>
    ''' 显示实例关系（与其他实例的共享 Mod，即简化版关系图）。
    ''' </summary>
    Public Shared Sub ShowInstanceRelation(Instance As McInstance)
        Try
            Dim Lines As New List(Of String)
            Dim SharedTotal As Integer = 0
            For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList
                For Each Other In Card.Value
                    If PathUtils.IsSame(Other.PathVersion, Instance.PathVersion) Then Continue For
                    Dim SharedMods = GetModFiles(Instance).Intersect(GetModFiles(Other)).Ordered().ToList()
                    If SharedMods.Any Then
                        SharedTotal += SharedMods.Count
                        Lines.Add($"· {Other.Name}：共享 {SharedMods.Count} 个 Mod（{SharedMods.Take(5).Join("、")}" & If(SharedMods.Count > 5, " 等", "") & "）")
                    End If
                Next
            Next
            If Lines.Count = 0 Then
                MyMsgBox($"实例「{Instance.Name}」未与其他实例共享任何 Mod。", "实例关系图", "确定")
            Else
                MyMsgBox($"实例「{Instance.Name}」共与其他 {Lines.Count} 个实例共享 {SharedTotal} 个 Mod：{vbCrLf}{Lines.Join(vbCrLf)}", "实例关系图（共享 Mod）", "确定")
            End If
        Catch ex As Exception
            Logger.Error(ex, "显示实例关系失败", LogBehavior.Alert)
        End Try
    End Sub

#Region "结果 UI 化"

    Private Sub McInstanceListUI(Loader As LoaderTask(Of String, Integer))
        Try
            '加载 UI
            PanMain.Children.Clear()

            '分组筛选条（存在分组时才显示）
            Dim Groups As New List(Of String)
            For Each GroupCard As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList
                For Each GroupInstance In GroupCard.Value
                    Dim GroupName = ReadIni(GroupInstance.PathVersion & "PCL\Setup.ini", "Group", "")
                    If GroupName <> "" AndAlso Not Groups.Contains(GroupName) Then Groups.Add(GroupName)
                Next
            Next
            Groups.Sort()
            If Groups.Count > 0 Then
                Dim FilterCard As New MyCard With {.Title = " ", .Margin = New Thickness(0, 0, 0, 15)}
                Dim FilterStack As New StackPanel With {.Margin = New Thickness(14, 10, 14, 10), .Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Top, .HorizontalAlignment = HorizontalAlignment.Left}
                Dim BtnFilterAll As New MyRadioButton With {.Text = "全部", .ColorType = MyRadioButton.ColorState.Highlight, .Margin = New Thickness(2, 0, 2, 0), .Checked = (GroupFilter = ""), .Tag = ""}
                AddHandler BtnFilterAll.Check,
                Sub(s As MyRadioButton, raiseByMouse As Boolean)
                    GroupFilter = If(s.Tag, "")
                    RefreshGroupFilter()
                End Sub
                FilterStack.Children.Add(BtnFilterAll)
                Dim BtnFilterNone As New MyRadioButton With {.Text = "未分组", .ColorType = MyRadioButton.ColorState.Highlight, .Margin = New Thickness(2, 0, 2, 0), .Checked = (GroupFilter = "<未分组>"), .Tag = "<未分组>"}
                AddHandler BtnFilterNone.Check,
                Sub(s As MyRadioButton, raiseByMouse As Boolean)
                    GroupFilter = If(s.Tag, "")
                    RefreshGroupFilter()
                End Sub
                FilterStack.Children.Add(BtnFilterNone)
                For Each GroupName In Groups
                    Dim BtnFilterGroup As New MyRadioButton With {.Text = GroupName, .ColorType = MyRadioButton.ColorState.Highlight, .Margin = New Thickness(2, 0, 2, 0), .Checked = (GroupFilter = GroupName), .Tag = GroupName}
                    AddHandler BtnFilterGroup.Check,
                    Sub(s As MyRadioButton, raiseByMouse As Boolean)
                        GroupFilter = If(s.Tag, "")
                        RefreshGroupFilter()
                    End Sub
                    FilterStack.Children.Add(BtnFilterGroup)
                Next
                FilterCard.Children.Add(FilterStack)
                PanMain.Children.Add(FilterCard)
            End If

            For Each Card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList.ToArray
                '确认是否为隐藏版本显示状态
                If Card.Key = McInstanceCardType.Hidden Xor ShowHidden Then Continue For
                Dim Instances = Card.Value.Where(Function(Instance) Not McInstanceLock.IsLocked(Instance) AndAlso PassGroupFilter(Instance)).ToList
                If Not Instances.Any Then Continue For
#Region "确认卡片名称"
                Dim CardName As String = ""
                Select Case Card.Key
                    Case McInstanceCardType.OriginalLike
                        CardName = "常规版本"
                    Case McInstanceCardType.API
                        Dim IsForgeExists As Boolean = False
                        Dim IsNeoForgeExists As Boolean = False
                        Dim IsFabricExists As Boolean = False
                        Dim IsLiteExists As Boolean = False
                        For Each Instance As McInstance In Instances
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.Fabric) Then IsFabricExists = True
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.LiteLoader) Then IsLiteExists = True
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.Forge) Then IsForgeExists = True
                            If Instance.Version.ModLoaders.HasFlagF(ModLoaders.NeoForge) Then IsNeoForgeExists = True
                        Next
                        If If(IsLiteExists, 1, 0) + If(IsForgeExists, 1, 0) + If(IsFabricExists, 1, 0) + If(IsNeoForgeExists, 1, 0) > 1 Then
                            CardName = "可安装 Mod"
                        ElseIf IsForgeExists Then
                            CardName = "Forge 版本"
                        ElseIf IsNeoForgeExists Then
                            CardName = "NeoForge 版本"
                        ElseIf IsLiteExists Then
                            CardName = "LiteLoader 版本"
                        Else
                            CardName = "Fabric 版本"
                        End If
                    Case McInstanceCardType.Error
                        CardName = "错误的版本"
                    Case McInstanceCardType.Hidden
                        CardName = "隐藏的版本"
                    Case McInstanceCardType.Rubbish
                        CardName = "不常用版本"
                    Case McInstanceCardType.Star
                        CardName = "收藏夹"
                    Case McInstanceCardType.Fool
                        CardName = "愚人节版本"
                    Case Else
                        Throw New ArgumentException("未知的卡片种类（" & Card.Key & "）")
                End Select
#End Region
                '建立控件
                Dim CardTitle As String = CardName & If(CardName = "收藏夹", "", " (" & Instances.Count & ")")
                Dim NewCard As New MyCard With {.Title = CardTitle, .Margin = New Thickness(0, 0, 0, 15)}
                Dim NewStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0)}
                NewCard.Children.Add(NewStack)
                NewCard.SwapControl = NewStack
                NewCard.SetContentBuilder(Instances, Function(Instance) Instance.ToListItem(AddressOf McInstanceListContent))
                PanMain.Children.Add(NewCard)
                '确定卡片是否展开
                If Card.Key = McInstanceCardType.Rubbish OrElse Card.Key = McInstanceCardType.Error OrElse Card.Key = McInstanceCardType.Fool Then
                    NewCard.IsSwapped = True
                Else
                    NewCard.BuildContent()
                End If
            Next

            '若只有一个卡片，则强制展开
            If PanMain.Children.Count = 1 AndAlso CType(PanMain.Children(0), MyCard).IsSwapped Then
                CType(PanMain.Children(0), MyCard).IsSwapped = False
            End If

            '判断应该显示哪一个页面
            If PanMain.Children.Count = 0 Then
                PanEmpty.Visibility = Visibility.Visible
                PanBack.Visibility = Visibility.Collapsed
                If ShowHidden Then
                    LabEmptyTitle.Text = "无隐藏版本"
                    LabEmptyContent.Text = "没有版本被隐藏，你可以在版本设置的版本分类选项中隐藏版本。" & vbCrLf & "再次按下 F11 即可退出隐藏版本查看模式。"
                    BtnEmptyDownload.Visibility = Visibility.Collapsed
                Else
                    LabEmptyTitle.Text = "无可用版本"
                    LabEmptyContent.Text = "未找到任何版本的游戏，请先下载任意版本的游戏。" & vbCrLf & "若有已存在的游戏，请在左边的列表中选择添加文件夹，选择 .minecraft 文件夹将其导入。"
                    BtnEmptyDownload.Visibility = If(Settings.Get(Of Boolean)("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow, Visibility.Collapsed, Visibility.Visible)
                End If
            Else
                PanBack.Visibility = Visibility.Visible
                PanEmpty.Visibility = Visibility.Collapsed
            End If

        Catch ex As Exception
            Logger.Error(ex, "将版本列表转换显示时失败")
        End Try
    End Sub
    Public Shared Sub McInstanceListContent(sender As MyListItem, e As EventArgs)
        Dim Instance As McInstance = sender.Tag
        '注册点击事件
        AddHandler sender.Click, AddressOf Item_Click
        '图标按钮
        Dim BtnStar As New MyIconButton
        If Instance.IsStar Then
            BtnStar.ToolTip = "取消收藏"
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeFill
        Else
            BtnStar.ToolTip = "收藏"
            BtnStar.LogoScale = 1.1
            BtnStar.Logo = Logo.IconButtonLikeLine
        End If
        AddHandler BtnStar.Click,
        Sub()
            WriteIni(Instance.PathVersion & "PCL\Setup.ini", "IsStar", Not Instance.IsStar)
            McInstanceListForceRefresh = True
            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        End Sub
        Dim BtnDel As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonDelete}
        BtnDel.ToolTip = "删除"
        AddHandler BtnDel.Click, Sub() DeleteInstance(sender, Instance)
        Dim BtnGroup As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonEdit}
        BtnGroup.ToolTip = "设置分组"
        AddHandler BtnGroup.Click, Sub() SetInstanceGroup(Instance)
        Dim BtnRelation As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonList}
        BtnRelation.ToolTip = "实例关系（共享 Mod）"
        AddHandler BtnRelation.Click, Sub() ShowInstanceRelation(Instance)
        If Instance.State <> McInstance.McInstanceState.Error Then
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.1, .Logo = Logo.IconButtonSetup}
            BtnCont.ToolTip = "设置"
            AddHandler BtnCont.Click,
            Sub()
                PageInstanceLeft.Instance = Instance
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            AddHandler sender.MouseRightButtonUp,
            Sub()
                PageInstanceLeft.Instance = Instance
                FrmMain.PageChange(FormMain.PageType.InstanceSetup, 0)
            End Sub
            sender.Buttons = {BtnStar, BtnGroup, BtnRelation, BtnDel, BtnCont}
        Else
            Dim BtnCont As New MyIconButton With {.LogoScale = 1.15, .Logo = Logo.IconButtonOpen}
            BtnCont.ToolTip = "打开文件夹"
            AddHandler BtnCont.Click, Sub() PageInstanceOverall.OpenInstanceFolder(Instance)
            AddHandler sender.MouseRightButtonUp, Sub() PageInstanceOverall.OpenInstanceFolder(Instance)
            sender.Buttons = {BtnStar, BtnGroup, BtnRelation, BtnDel, BtnCont}
        End If
    End Sub

#End Region

#Region "页面事件"

    '点击选项
    Public Shared Sub Item_Click(sender As MyListItem, e As EventArgs)
        Dim Instance As McInstance = sender.Tag
        If McInstanceLock.IsLocked(Instance) Then
            Hint("该版本正在被修改，请稍后再选择！", HintType.Red)
            Return
        End If
        If New McInstance(Instance.PathVersion).Check Then
            '正常版本
            McInstanceSelected = Instance
            FrmMain.PageBack()
        Else
            '错误版本
            PageInstanceOverall.OpenInstanceFolder(Instance)
        End If
    End Sub

    Private Sub BtnDownload_Click(sender As Object, e As EventArgs) Handles BtnEmptyDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
    End Sub

    '修改此代码时，同时修改 PageInstanceOverall 中的代码
    Public Shared Sub DeleteInstance(Item As MyListItem, Instance As McInstance)
        Try
            If McInstanceLock.IsLocked(Instance) Then
                Hint("该版本正在被修改，请稍后再删除！", HintType.Red)
                Return
            End If
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = Instance.State <> McInstance.McInstanceState.Error AndAlso Instance.PathIndie <> McFolderSelected
            Dim SavesFolder = DirectoryUtils.GetInfo(Instance.PathIndie & "saves\")
            Dim SaveEntries = If(IsHintIndie AndAlso SavesFolder.Exists, SavesFolder.EnumerateDirectories.OrderByDescending(Function(Save) Save.LastWriteTime).Select(Function(Save) $"{Save.Name}（上次修改：{StringUtils.FormatTimeSpan(Save.LastWriteTime - Date.Now, False)}）").ToList, New List(Of String))
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除版本 {Instance.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "该版本对应的存档、资源包、Mod 等文件也将被一并删除！", "") &
                        If(SaveEntries.Any, vbCrLf & vbCrLf & "这会删除以下存档：" & vbCrLf & "· " & SaveEntries.Join(vbCrLf & "· "), ""),
                        "版本删除确认", , "取消",, True)
                Case 1
                    Instance.ResetSettingsCache()
                    DirectoryUtils.Delete(Instance.PathVersion, Not IsShiftPressed)
                    Hint("版本 " & Instance.Name & " 已删除！", HintType.Green)
                Case 2
                    Return
            End Select
            '从 UI 中移除
            If Instance.DisplayType = McInstanceCardType.Hidden OrElse Not Instance.IsStar Then
                '仅出现在当前卡片
                Dim Parent As StackPanel = Item.Parent
                If Parent.Children.Count > 2 Then '当前的项目与一个占位符
                    '删除后还有剩
                    Dim Card As MyCard = Parent.Parent
                    Card.Title = Card.Title.Replace(Parent.Children.Count - 1, Parent.Children.Count - 2) '有一个占位符
                    Parent.Children.Remove(Item)
                    If McInstanceSelected IsNot Nothing AndAlso PathUtils.IsSame(Instance.PathVersion, McInstanceSelected.PathVersion) Then
                        '删除当前版本就更改选择
                        McInstanceSelected = MyVirtualizingElement.TryInit(Parent.Children(0)).Tag
                    End If
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.UpdateOnly, MaxDepth:=1, ExtraPath:="versions\")
                Else
                    '删除后没剩了
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
                End If
            Else
                '同时出现在当前卡片与收藏夹
                LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            End If
        Catch ex As OperationCanceledException
            Logger.Warn(ex, $"删除版本 {Instance.Name} 被主动取消")
        Catch ex As Exception
            Logger.Error(ex, $"删除版本 {Instance.Name} 失败", LogBehavior.Alert)
        End Try
    End Sub

    Public Sub BtnEmptyDownload_Loaded() Handles BtnEmptyDownload.Loaded
        Dim NewVisibility = If((Settings.Get(Of Boolean)("UiHiddenPageDownload") AndAlso Not PageSetupUI.HiddenForceShow) OrElse ShowHidden, Visibility.Collapsed, Visibility.Visible)
        If BtnEmptyDownload.Visibility <> NewVisibility Then
            BtnEmptyDownload.Visibility = NewVisibility
            PanLoad.TriggerForceResize()
        End If
    End Sub

#End Region

End Class
