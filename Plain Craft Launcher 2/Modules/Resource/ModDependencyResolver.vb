''' <summary>
''' 模组前置一键下载模块。
''' 通过 Modrinth 解析本地模组的前置依赖，自动检测缺失的前置并递归下载到模组文件夹。
''' </summary>
Public Module ModDependencyResolver

    ''' <summary>
    ''' 解析并下载缺失的模组前置（主入口）。
    ''' 会在后台线程执行，并通过任务栏显示进度。
    ''' </summary>
    Public Sub ResolveDependencies(Instance As McInstance, Mods As List(Of LocalResourceFile))
        Dim VanillaVersion = Instance.Version.VanillaName
        Dim TargetLoader = Instance.Version.ModLoaders
        Dim ModsFolder = Instance.PathIndie & "mods\"
        '已安装的 Modrinth 项目 ID 与本地文件哈希
        Dim InstalledProjectIds As New HashSet(Of String)
        Dim InstalledHashes As New HashSet(Of String)
        For Each ModEntry In Mods
            InstalledHashes.Add(ModEntry.ModrinthHash.Lower())
            If ModEntry.Project IsNot Nothing Then InstalledProjectIds.Add(ModEntry.Project.Id)
        Next
        '结果统计与下载列表（通过闭包传递给后续加载器）
        Dim DownloadedFiles As New List(Of String)
        Dim SkippedFiles As New List(Of String)
        Dim FailedDependencies As New List(Of String)
        Dim FileList As New List(Of NetFile)
        Dim FileCopyList As New Dictionary(Of String, String)
        Dim VersionCache As New Dictionary(Of String, JArray) '项目 ID -> 原始版本 JSON 列表

        '步骤 1、2：分析前置依赖
        Dim AnalyzeLoader As New LoaderTask(Of String, List(Of NetFile))("解析前置依赖",
        Sub(Task As LoaderTask(Of String, List(Of NetFile)))
            '步骤 1：通过哈希获取本地模组的版本信息，并记录其前置
            Dim Queue As New Queue(Of String)
            Dim QueuedSet As New HashSet(Of String)
            Try
                Dim HashList = Mods.Select(Function(m) m.ModrinthHash).Where(Function(h) Not String.IsNullOrEmpty(h)).ToList
                If HashList.Any Then
                    Dim VersionData As JObject = DlModRequest("https://api.modrinth.com/v2/version_files", HttpMethod.Post,
                        $"{{""hashes"": [""{String.Join(""",""", HashList)}""], ""algorithm"": ""sha1""}}", "application/json")
                    For Each VersionJson As JProperty In VersionData.Properties()
                        Dim VersionObject As JObject = CType(VersionJson.Value, JObject)
                        Dim ProjectId = VersionObject?("project_id")?.ToString
                        If Not String.IsNullOrEmpty(ProjectId) Then InstalledProjectIds.Add(ProjectId)
                        '记录该版本的前置
                        Dim DepArray = If(VersionObject?("dependencies"), New JArray())
                        For Each Dependency As JObject In DepArray.OfType(Of JObject)()
                            If Dependency?("dependency_type")?.ToString = "required" Then
                                Dim DepId = Dependency("project_id").ToString
                                If Not String.IsNullOrEmpty(DepId) AndAlso Not InstalledProjectIds.Contains(DepId) AndAlso Not QueuedSet.Contains(DepId) Then
                                    Queue.Enqueue(DepId)
                                    QueuedSet.Add(DepId)
                                End If
                            End If
                        Next
                    Next
                End If
            Catch ex As Exception
                Logger.Warn(ex, "获取本地模组版本信息失败")
            End Try
            '步骤 2：递归解析并下载缺失的前置
            Dim SeenProjects As New HashSet(Of String)
            While Queue.Any
                If Task.IsCanceled Then Exit While
                Dim ProjectId = Queue.Dequeue()
                If InstalledProjectIds.Contains(ProjectId) OrElse Not SeenProjects.Add(ProjectId) Then Continue While
                Try
                    '获取项目信息，仅处理 Mod 类型
                    Dim ProjectJson As JObject = DlModRequest($"https://api.modrinth.com/v2/project/{ProjectId}")
                    If ProjectJson?("project_type")?.ToString <> "mod" Then Continue While
                    '获取版本列表并挑选合适版本
                    Dim VersionArray As JArray = Nothing
                    If Not VersionCache.TryGetValue(ProjectId, VersionArray) Then
                        VersionArray = DlModRequest($"https://api.modrinth.com/v2/project/{ProjectId}/version")
                        VersionCache(ProjectId) = VersionArray
                    End If
                    Dim Versions = VersionArray.OfType(Of JObject).
                        Select(Function(v) ResourceVersion.FromPlatformJson(v, ResourceTypes.Mod)).
                        Where(Function(v) v.DownloadAvailable).ToList
                    Dim BestVersion = PickBestDependencyVersion(Versions, VanillaVersion, TargetLoader)
                    If BestVersion Is Nothing Then
                        FailedDependencies.Add(If(ProjectJson?("slug")?.ToString, ProjectId))
                        Continue While
                    End If
                    '检查是否已经安装（按哈希）或目标文件夹已存在同名文件
                    If InstalledHashes.Contains(BestVersion.Hash.Lower()) Then
                        SkippedFiles.Add(BestVersion.FileName)
                        Continue While
                    End If
                    Dim RealAddress = ModsFolder & BestVersion.FileName
                    If FileUtils.Exists(RealAddress) Then
                        SkippedFiles.Add(BestVersion.FileName)
                        Continue While
                    End If
                    '加入下载列表
                    Dim TempAddress = PathTemp & "mods\" & BestVersion.FileName
                    If FileCopyList.ContainsKey(TempAddress) Then Continue While
                    FileList.Add(BestVersion.ToNetFile(TempAddress, ResourceVersion.DownloadReason.Update, VanillaVersion, TargetLoader))
                    FileCopyList(TempAddress) = RealAddress
                    DownloadedFiles.Add(BestVersion.FileName)
                    Logger.Info($"已加入前置下载：{BestVersion.FileName}（项目 {ProjectId}）")
                    '记录其前置，继续递归
                    Dim BestJson = VersionArray.OfType(Of JObject).FirstOrDefault(Function(v) v("id")?.ToString = BestVersion.Id)
                    If BestJson IsNot Nothing Then
                        Dim DepArray = If(BestJson?("dependencies"), New JArray())
                        For Each Dependency As JObject In DepArray.OfType(Of JObject)()
                            If Dependency?("dependency_type")?.ToString = "required" Then
                                Dim DepId = Dependency("project_id").ToString
                                If Not String.IsNullOrEmpty(DepId) AndAlso Not InstalledProjectIds.Contains(DepId) AndAlso
                                   Not SeenProjects.Contains(DepId) AndAlso Not QueuedSet.Contains(DepId) Then
                                    Queue.Enqueue(DepId)
                                    QueuedSet.Add(DepId)
                                End If
                            End If
                        Next
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, $"解析前置失败：{ProjectId}")
                    FailedDependencies.Add(ProjectId)
                End Try
            End While
            Task.Output = FileList
        End Sub)
        '步骤 3：下载与移动（分析完成后依次执行）
        Dim InstallLoaders As New List(Of LoaderBase)
        InstallLoaders.Add(New LoaderDownload("下载前置 Mod 文件", Enumerable.Empty(Of NetFile)()) With {.ProgressWeight = 10})
        InstallLoaders.Add(New LoaderTask(Of List(Of NetFile), Integer)("移动到模组文件夹",
        Sub()
            Try
                For Each Entry As KeyValuePair(Of String, String) In FileCopyList
                    If FileUtils.Exists(Entry.Key) Then
                        If DirectoryUtils.Exists(PathUtils.RemoveLastPart(Entry.Value)) Then
                            FileUtils.Move(Entry.Key, Entry.Value)
                        Else
                            Logger.Warn($"前置目标文件夹不存在：{Entry.Value}")
                        End If
                    Else
                        Logger.Warn($"前置下载文件不存在：{Entry.Key}")
                    End If
                Next
            Catch ex As Exception
                Logger.Warn(ex, "移动前置文件失败")
            End Try
        End Sub))
        '组装加载器
        Dim Loader As New LoaderCombo(Of String)("下载模组前置：" & Instance.Name, {AnalyzeLoader})
        Loader.Loaders.AddRange(InstallLoaders)
        '进度与结果
        Loader.OnStateChanged =
        Sub(Ldr As LoaderBase)
            Select Case Ldr.State
                Case LoadState.Finished
                    Dim Messages As New List(Of String)
                    If DownloadedFiles.Any Then Messages.Add($"已下载 {DownloadedFiles.Count} 个前置")
                    If SkippedFiles.Any Then Messages.Add($"已存在 {SkippedFiles.Count} 个")
                    If FailedDependencies.Any Then Messages.Add($"未找到 {FailedDependencies.Count} 个")
                    Hint(Messages.Join("，"), If(FailedDependencies.Any AndAlso Not DownloadedFiles.Any, HintType.Red, HintType.Green))
                    Logger.Info($"前置下载完成：{Messages.Join("，")}")
                Case LoadState.Failed
                    Hint("前置下载失败：" & Ldr.Error.GetDisplay(False), HintType.Red)
                Case LoadState.Canceled
                    Hint("前置下载已中止！", HintType.Blue)
            End Select
        End Sub
        Loader.Start()
        LoaderTaskbarAdd(Loader)
    End Sub

    ''' <summary>
    ''' 从版本列表中挑选适合当前 Minecraft 版本与加载器的最新版本。
    ''' </summary>
    Private Function PickBestDependencyVersion(Versions As List(Of ResourceVersion), VanillaName As String, TargetLoader As ModLoaders) As ResourceVersion
        If Not Versions.Any Then Return Nothing
        Dim Candidates = Versions.Where(Function(v) v.DownloadAvailable).ToList
        '加载器匹配
        If TargetLoader.Flags().Any Then
            Dim LoaderMatched = Candidates.Where(Function(v) TargetLoader.Flags().Any(Function(l) v.ModLoaders.HasFlagF(l))).ToList
            If LoaderMatched.Any Then Candidates = LoaderMatched
        End If
        'MC 版本匹配：优先完全匹配，其次匹配同一 Drop（如 1.21.1 兼容 1.21）
        Dim Drop = McVersion.VersionToDrop(VanillaName)
        Dim ExactMatched = Candidates.Where(Function(v) v.GameVersions.Any(Function(g) g = VanillaName)).ToList
        If ExactMatched.Any Then
            Candidates = ExactMatched
        Else
            Dim DropMatched = Candidates.Where(Function(v) v.GameVersions.Any(Function(g) McVersion.IsStandardFormat(g) AndAlso McVersion.VersionToDrop(g) = Drop)).ToList
            If DropMatched.Any Then Candidates = DropMatched
        End If
        Return Candidates.OrderByDescending(Function(v) v.ReleaseDate).FirstOrDefault
    End Function

End Module
