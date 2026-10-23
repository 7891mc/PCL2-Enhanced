Imports System.Net.Http
Imports System.IO

''' <summary>
''' Cleanroom 加载器支持模块：通过官方渠道获取并下载 Cleanroom 安装器。
''' Cleanroom 使用官方图形化安装器（installer jar），下载完成后请手动运行安装。
''' </summary>
Module ModCleanroom

    ''' <summary>
    ''' 获取 Cleanroom 最新正式安装器的下载地址与文件名。
    ''' </summary>
    Public Function CleanroomGetLatestInstaller() As KeyValuePair(Of String, String)
        Dim Release As JObject = NetRequestByClientRetry("https://api.github.com/repos/CleanroomMC/Cleanroom/releases/latest", RequireJson:=True,
            Headers:={{"User-Agent", "PCL2"}}).DeserializeJson()
        Dim InstallerAsset As JObject = Nothing
        For Each Asset As JObject In Release("assets")
            Dim Name As String = Asset("name").ToString
            If Name.Contains("installer") AndAlso Name.EndsWith(".jar") Then
                InstallerAsset = Asset
                Exit For
            End If
        Next
        If InstallerAsset Is Nothing Then Throw New Exception("未能在 Cleanroom 官方发布中找到安装器文件")
        Return New KeyValuePair(Of String, String)(InstallerAsset("name").ToString, InstallerAsset("browser_download_url").ToString)
    End Function

    ''' <summary>
    ''' 下载 Cleanroom 安装器到指定目录（默认缓存目录），返回保存路径。
    ''' </summary>
    Public Function CleanroomDownloadInstaller(Optional TargetFolder As String = Nothing) As String
        If TargetFolder Is Nothing Then TargetFolder = PathTemp & "Cleanroom\"
        DirectoryUtils.Create(TargetFolder)
        Dim Info = CleanroomGetLatestInstaller()
        Dim TargetFile As String = TargetFolder & Info.Key
        NetDownloadByClient(Info.Value, TargetFile & ".PCLDownloading")
        FileUtils.Move(TargetFile & ".PCLDownloading", TargetFile)
        Logger.Info($"Cleanroom 安装器下载成功：{TargetFile}")
        Return TargetFile
    End Function

End Module
