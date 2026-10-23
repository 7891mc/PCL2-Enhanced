Imports System.Windows

Public Class PageOtherTest

    Private Sub BtnOpenData_Click(Sender As Object, E As RoutedEventArgs) Handles BtnOpenData.Click
        OpenExplorer(Paths.AppDataThenName)
        Hint("已打开 PCL 数据目录。", HintType.Blue)
    End Sub

    Private Sub BtnOpenCache_Click(Sender As Object, E As RoutedEventArgs) Handles BtnOpenCache.Click
        OpenExplorer(PathTemp)
        Hint("已打开缓存目录。", HintType.Blue)
    End Sub

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
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
