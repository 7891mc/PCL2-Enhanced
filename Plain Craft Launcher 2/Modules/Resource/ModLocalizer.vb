''' <summary>
''' 模组汉化模块。
''' 从 CFPA（CurseForge 中文翻译项目）为已安装模组获取简体中文语言文件，
''' 并以"资源包"形式应用到版本中，无需修改模组文件本身，也不会破坏模组的哈希校验。
''' </summary>
Public Module ModLocalizer

    '汉化资源包文件夹名称（同时用于 options.txt 中的注册名）
    Private Const PackFolderName As String = "PCL-Localize"

    ''' <summary>
    ''' 解析模组 jar 中的 modid（命名空间）列表。
    ''' 支持 Fabric / Quilt / Forge / NeoForge / 旧版 mcmod.info。
    ''' </summary>
    Public Function GetModNamespaces(JarPath As String) As List(Of String)
        Dim Result As New List(Of String)
        Try
            Using JarArchive = FileUtils.OpenZip(JarPath)
                'Fabric
                Dim FabricEntry As ZipArchiveEntry = JarArchive.GetEntry("fabric.mod.json")
                If FabricEntry IsNot Nothing Then
                    Using Reader As New StreamReader(FabricEntry.Open)
                        Dim Json As JObject = Reader.ReadToEnd().DeserializeJson()
                        Dim Id = Json?("id")?.ToString
                        If Not String.IsNullOrEmpty(Id) Then Result.Add(Id)
                    End Using
                End If
                'Quilt
                Dim QuiltEntry As ZipArchiveEntry = JarArchive.GetEntry("quilt.mod.json")
                If QuiltEntry IsNot Nothing Then
                    Using Reader As New StreamReader(QuiltEntry.Open)
                        Dim Json As JObject = Reader.ReadToEnd().DeserializeJson()
                        Dim Id = Json?("quilt_loader")?("id")?.ToString
                        If Not String.IsNullOrEmpty(Id) Then Result.Add(Id)
                    End Using
                End If
                'Forge / NeoForge
                Dim ForgeEntry As ZipArchiveEntry = JarArchive.GetEntry("META-INF/mods.toml")
                If ForgeEntry IsNot Nothing Then
                    Using Reader As New StreamReader(ForgeEntry.Open)
                        For Each Line In Reader.ReadToEnd().SplitLines()
                            Line = Line.Trim()
                            If Line.StartsWithF("modId=") Then
                                Dim Id = Line.AfterFirst("modId=").Trim().Trim(""""c)
                                If Not String.IsNullOrEmpty(Id) Then Result.Add(Id)
                            End If
                        Next
                    End Using
                End If
                '旧版 Forge（mcmod.info）
                If Not Result.Any Then
                    Dim LegacyEntry As ZipArchiveEntry = JarArchive.GetEntry("mcmod.info")
                    If LegacyEntry IsNot Nothing Then
                        Using Reader As New StreamReader(LegacyEntry.Open)
                            Dim Json As JContainer = Reader.ReadToEnd().DeserializeJson()
                            Dim Arr = If(TryCast(Json, JArray), New JArray(Json))
                            For Each ModEntry As JObject In Arr.OfType(Of JObject)()
                                Dim Id = ModEntry?("modid")?.ToString
                                If Not String.IsNullOrEmpty(Id) Then Result.Add(Id)
                            Next
                        End Using
                    End If
                End If
            End Using
        Catch ex As Exception
            Logger.Warn(ex, $"解析模组 modid 失败：{JarPath}")
        End Try
        Return Result.Distinct.ToList
    End Function

    ''' <summary>
    ''' 根据 Minecraft 版本确定可用的 CFPA 仓库分支（按优先级排序）。
    ''' </summary>
    Private Function GetCfpaBranches(MinecraftVersion As String) As List(Of String)
        '1.21.x 及更早版本使用归档分支，更新的版本使用 main 分支
        If McUtils.CompareVersionGE(MinecraftVersion, "1.22") Then
            Return New List(Of String) From {"main", "archived-1.21-26.4.27"}
        Else
            Return New List(Of String) From {"archived-1.21-26.4.27", "main"}
        End If
    End Function

    ''' <summary>
    ''' 根据 Minecraft 版本确定 CFPA 仓库中对应的项目目录（按优先级排序）。
    ''' 例如 1.21.1 -> 1.21；Fabric 版本还会尝试 1.21-fabric。
    ''' </summary>
    Private Function GetCfpaProjectFolders(MinecraftVersion As String, Loader As ModLoaders) As List(Of String)
        Dim Result As New List(Of String)
        Dim MainFolder = If(MinecraftVersion.Contains("."), MinecraftVersion.BeforeLast("."), MinecraftVersion)
        Result.Add(MainFolder)
        If Loader.HasFlagF(ModLoaders.Fabric) Then Result.Add(MainFolder & "-fabric")
        Return Result.Distinct.ToList
    End Function

    ''' <summary>
    ''' 尝试为某个命名空间在 CFPA 仓库中查找并下载简体中文语言文件。
    ''' 成功则返回下载到的内容，失败返回 Nothing。
    ''' </summary>
    Private Function TryFetchTranslation(Branches As List(Of String), ProjectFolders As List(Of String), Namesp As String, ByRef UsedUrl As String) As String
        '候选路径：命名空间本身 / 下划线转连字符（CFPA 的 slug 通常为连字符形式）
        Dim SlugCandidates As New List(Of String) From {Namesp, Namesp.Replace("_", "-")}
        For Each Branch In Branches
            For Each Folder In ProjectFolders
                For Each Slug In SlugCandidates
                    '1.13+ 的 JSON 语言文件
                    Dim JsonUrl = $"https://raw.githubusercontent.com/CFPAOrg/Minecraft-Mod-Language-Package/{Branch}/projects/{Folder}/assets/{Slug}/{Namesp}/lang/zh_cn.json"
                    Try
                        Dim Result = NetRequestByClientRetry(JsonUrl, RequireJson:=False)
                        UsedUrl = JsonUrl
                        Return Result
                    Catch ex As Exception
                        '404 或网络错误，继续尝试下一候选
                    End Try
                    '旧版 .lang 语言文件
                    Dim LangUrl = JsonUrl.Replace("zh_cn.json", "zh_cn.lang")
                    Try
                        Dim Result = NetRequestByClientRetry(LangUrl, RequireJson:=False)
                        UsedUrl = LangUrl
                        Return Result
                    Catch ex As Exception
                    End Try
                    '旧版仓库布局（assets 直接在分支根目录）
                    Dim LegacyUrl = $"https://raw.githubusercontent.com/CFPAOrg/Minecraft-Mod-Language-Package/{Branch}/assets/{Namesp}/lang/zh_cn.json"
                    Try
                        Dim Result = NetRequestByClientRetry(LegacyUrl, RequireJson:=False)
                        UsedUrl = LegacyUrl
                        Return Result
                    Catch ex As Exception
                    End Try
                Next
            Next
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 根据 Minecraft 版本计算资源包格式（pack_format）。
    ''' 语言文件自 1.13 起均为 JSON 格式，此处按大版本映射，偏差不影响加载。
    ''' </summary>
    Private Function GetPackFormat(MinecraftVersion As String) As Integer
        If McUtils.CompareVersionGE(MinecraftVersion, "1.21.6") Then Return 61
        If McUtils.CompareVersionGE(MinecraftVersion, "1.21.5") Then Return 55
        If McUtils.CompareVersionGE(MinecraftVersion, "1.21.4") Then Return 46
        If McUtils.CompareVersionGE(MinecraftVersion, "1.21.2") Then Return 42
        If McUtils.CompareVersionGE(MinecraftVersion, "1.21") Then Return 34
        If McUtils.CompareVersionGE(MinecraftVersion, "1.20.5") Then Return 32
        If McUtils.CompareVersionGE(MinecraftVersion, "1.20.3") Then Return 22
        If McUtils.CompareVersionGE(MinecraftVersion, "1.20.2") Then Return 18
        If McUtils.CompareVersionGE(MinecraftVersion, "1.20") Then Return 15
        If McUtils.CompareVersionGE(MinecraftVersion, "1.19.4") Then Return 13
        If McUtils.CompareVersionGE(MinecraftVersion, "1.19.3") Then Return 12
        If McUtils.CompareVersionGE(MinecraftVersion, "1.19") Then Return 9
        If McUtils.CompareVersionGE(MinecraftVersion, "1.18") Then Return 8
        If McUtils.CompareVersionGE(MinecraftVersion, "1.17") Then Return 7
        If McUtils.CompareVersionGE(MinecraftVersion, "1.16.2") Then Return 6
        If McUtils.CompareVersionGE(MinecraftVersion, "1.16") Then Return 5
        If McUtils.CompareVersionGE(MinecraftVersion, "1.13") Then Return 4
        Return 1
    End Function

    ''' <summary>
    ''' 将语言文件写入版本的资源包文件夹，并在 options.txt 中注册。
    ''' </summary>
    Private Sub ApplyToResourcePack(Instance As McInstance, Namesp As String, Content As String)
        Dim PackRoot = Instance.PathIndie & "resourcepacks\" & PackFolderName & "\"
        Dim LangFolder = PackRoot & "assets\" & Namesp & "\lang\"
        DirectoryUtils.Create(LangFolder)
        '写入语言文件（旧版 .lang 格式原样保留）
        Dim TargetPath = LangFolder & If(Content.StartsWithF("{"), "zh_cn.json", "zh_cn.lang")
        FileUtils.Write(TargetPath, Content)
        '确保 pack.mcmeta 存在
        Dim PackMetaPath = PackRoot & "pack.mcmeta"
        If Not FileUtils.Exists(PackMetaPath) Then
            FileUtils.Write(PackMetaPath, $"{{""pack"":{{""pack_format"":{GetPackFormat(Instance.Version.VanillaName)},""description"":""PCL 自动汉化资源包""}}}}")
        End If
        '在 options.txt 中注册资源包
        RegisterResourcePack(Instance)
    End Sub

    ''' <summary>
    ''' 在版本的 options.txt 中注册汉化资源包（若尚未注册）。
    ''' </summary>
    Private Sub RegisterResourcePack(Instance As McInstance)
        Try
            Dim OptionsPath = Instance.PathIndie & "options.txt"
            Dim PackId = "file/" & PackFolderName
            Dim Lines As New List(Of String)
            If FileUtils.Exists(OptionsPath) Then
                Lines.AddRange(FileUtils.ReadAsLines(OptionsPath))
            Else
                Lines.Add("")
            End If
            '查找 resourcePacks 行并追加
            Dim Index = Lines.FindIndex(Function(l) l.StartsWithF("resourcePacks:"))
            If Index < 0 Then
                Lines.Insert(0, $"resourcePacks:[""vanilla"",""{PackId}""]")
            Else
                Dim Body = Lines(Index).Substring("resourcePacks:".Length).TrimStart("["c).TrimEnd("]"c).Trim()
                If Not Body.Contains(PackId) Then
                    Lines(Index) = $"resourcePacks:[{Body}{If(Body = "", "", ",")}""{PackId}""]"
                End If
            End If
            FileUtils.Write(OptionsPath, String.Join(vbCrLf, Lines))
        Catch ex As Exception
            Logger.Warn(ex, "注册汉化资源包到 options.txt 失败")
        End Try
    End Sub

    ''' <summary>
    ''' 移除已应用的汉化资源包（含 options.txt 注册项）。
    ''' </summary>
    Public Sub RemoveLocalizePack(Instance As McInstance)
        Try
            Dim PackRoot = Instance.PathIndie & "resourcepacks\" & PackFolderName & "\"
            If DirectoryUtils.Exists(PackRoot) Then DirectoryUtils.Delete(PackRoot, True)
            Dim OptionsPath = Instance.PathIndie & "options.txt"
            If FileUtils.Exists(OptionsPath) Then
                Dim Lines = FileUtils.ReadAsLines(OptionsPath).ToList
                Dim Index = Lines.FindIndex(Function(l) l.StartsWithF("resourcePacks:"))
                If Index >= 0 Then
                    Dim PackId = "file/" & PackFolderName
                    Dim Body = Lines(Index).Substring("resourcePacks:".Length).TrimStart("["c).TrimEnd("]"c).Trim()
                    Dim Tokens = Body.Split(","c).Select(Function(t) t.Trim()).Where(Function(t) t <> "" AndAlso t <> PackId).ToList
                    Lines(Index) = $"resourcePacks:[{Tokens.Join(",")}]"
                End If
                FileUtils.Write(OptionsPath, String.Join(vbCrLf, Lines))
            End If
        Catch ex As Exception
            Logger.Warn(ex, "移除汉化资源包失败")
        End Try
    End Sub

    ''' <summary>
    ''' 对一组模组执行汉化（主入口）。
    ''' 会在后台线程执行，并通过任务栏显示进度。
    ''' </summary>
    Public Sub LocalizeMods(Instance As McInstance, Mods As List(Of LocalResourceFile))
        '准备
        Dim Branches = GetCfpaBranches(Instance.Version.VanillaName)
        Dim Folders = GetCfpaProjectFolders(Instance.Version.VanillaName, Instance.Version.ModLoaders)
        Dim TargetNamespaces As New List(Of String)
        For Each ModEntry In Mods
            TargetNamespaces.AddRange(GetModNamespaces(ModEntry.File.FullName))
        Next
        TargetNamespaces = TargetNamespaces.Distinct.ToList
        '加载器（用于任务栏进度与状态提示）
        Dim SucceedNames As New List(Of String)
        Dim FailedNames As New List(Of String)
        Dim NotFoundNames As New List(Of String)
        Dim Loader As New LoaderCombo(Of String)("模组汉化：" & Instance.Name,
            {New LoaderTask(Of String, String)("获取汉化文件",
            Sub(Task As LoaderTask(Of String, String))
                For Each Namesp In TargetNamespaces
                    If Task.IsCanceled Then Return
                    Try
                        Dim UsedUrl As String = Nothing
                        Dim Content = TryFetchTranslation(Branches, Folders, Namesp, UsedUrl)
                        If Content Is Nothing Then
                            NotFoundNames.Add(Namesp)
                            Logger.Info($"未在 CFPA 仓库找到 {Namesp} 的汉化")
                            Continue For
                        End If
                        RunInUi(Sub() ApplyToResourcePack(Instance, Namesp, Content)) '文件操作放在 UI 线程避免并发冲突
                        SucceedNames.Add(Namesp)
                        Logger.Info($"已为 {Namesp} 获取汉化：{UsedUrl}")
                    Catch ex As Exception
                        FailedNames.Add(Namesp)
                        Logger.Warn(ex, $"为 {Namesp} 获取汉化失败")
                    End Try
                Next
            End Sub)})
        Loader.OnStateChanged =
        Sub(Ldr As LoaderBase)
            Select Case Ldr.State
                Case LoadState.Finished
                    Dim Messages As New List(Of String)
                    If SucceedNames.Any Then Messages.Add($"成功汉化 {SucceedNames.Count} 个模组")
                    If NotFoundNames.Any Then Messages.Add($"未找到汉化 {NotFoundNames.Count} 个（可能自带中文）")
                    If FailedNames.Any Then Messages.Add($"失败 {FailedNames.Count} 个")
                    Hint(Messages.Join("，"), If(FailedNames.Any, HintType.Red, HintType.Green))
                    If SucceedNames.Any Then
                        Hint("汉化资源包已应用到该版本，下次启动游戏即可生效", HintType.Blue)
                    End If
                    Logger.Info($"模组汉化完成：{Messages.Join("，")}")
                Case LoadState.Failed
                    Hint("模组汉化失败：" & Ldr.Error.GetDisplay(False), HintType.Red)
                Case LoadState.Canceled
                    Hint("模组汉化已中止！", HintType.Blue)
            End Select
        End Sub
        Loader.Start()
        LoaderTaskbarAdd(Loader)
    End Sub

End Module
