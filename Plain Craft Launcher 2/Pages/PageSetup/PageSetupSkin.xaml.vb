Imports System.IO
Imports System.Net.Http
Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Media.Imaging
Imports System.Windows.Shapes

''' <summary>
''' 皮肤绘制器：支持逐像素绘制 64×64 皮肤、载入/保存/上传皮肤，并提供盔甲穿戴预览。
''' </summary>
Public Class PageSetupSkin
    Inherits MyPageRight

    ''' <summary>皮肤位图（64×64）。</summary>
    Private SkinBmp As WriteableBitmap
    Private IsDrawing As Boolean = False
    Private CurColor As Color = Color.FromRgb(90, 140, 210)
    Private CurTool As Integer = 0 '0=画笔 1=橡皮 2=填充 3=取色
    Private ArmorMode As Integer = 0 '0=无 1=铁 2=金 3=钻
    Private PaletteButtons As New List(Of MyButton)

    Private Sub PageSetupSkin_Loaded() Handles Me.Loaded
        InitCanvas()
        BuildPalette()
        RefreshColorShow()
        RenderPreview()
    End Sub

    ''' <summary>初始化画布位图与网格。</summary>
    Private Sub InitCanvas()
        SkinBmp = New WriteableBitmap(64, 64, 96, 96, PixelFormats.Bgra32, Nothing)
        '清空为全透明
        Dim ClearBytes(64 * 64 * 4 - 1) As Byte
        SkinBmp.WritePixels(New Int32Rect(0, 0, 64, 64), ClearBytes, 64 * 4, 0)
        SkinImage.Source = SkinBmp
        '绘制网格线（每 10 像素一格）
        GridCanvas.Children.Clear()
        For i = 0 To 64
            Dim VLine As New Line With {.X1 = i * 10, .Y1 = 0, .X2 = i * 10, .Y2 = 640, .Stroke = New SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), .StrokeThickness = 1}
            Dim HLine As New Line With {.X1 = 0, .Y1 = i * 10, .X2 = 640, .Y2 = i * 10, .Stroke = New SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), .StrokeThickness = 1}
            GridCanvas.Children.Add(VLine)
            GridCanvas.Children.Add(HLine)
        Next
    End Sub

    ''' <summary>构建颜色面板。</summary>
    Private Sub BuildPalette()
        PaletteGrid.Children.Clear()
        PaletteButtons.Clear()
        Dim PaletteColors() As Color = {
            Color.FromRgb(90, 140, 210), Color.FromRgb(200, 80, 80), Color.FromRgb(120, 180, 90),
            Color.FromRgb(240, 215, 120), Color.FromRgb(140, 90, 170), Color.FromRgb(60, 60, 60),
            Color.FromRgb(230, 230, 230), Color.FromRgb(160, 110, 70), Color.FromRgb(255, 180, 190),
            Color.FromRgb(120, 210, 220), Color.FromRgb(90, 90, 160), Color.FromRgb(190, 190, 170)}
        For i = 0 To PaletteColors.Length - 1
            Dim Index = i
            Dim Btn As New MyButton With {.Width = 44, .Height = 28, .Margin = New Thickness(3, 3, 3, 3)}
            Btn.Background = New SolidColorBrush(PaletteColors(i))
            AddHandler Btn.Click, Sub() SelectPaletteColor(Index)
            PaletteGrid.Children.Add(Btn)
            PaletteButtons.Add(Btn)
        Next
    End Sub

    Private Sub SelectPaletteColor(Index As Integer)
        Dim Colors() As Color = {
            Color.FromRgb(90, 140, 210), Color.FromRgb(200, 80, 80), Color.FromRgb(120, 180, 90),
            Color.FromRgb(240, 215, 120), Color.FromRgb(140, 90, 170), Color.FromRgb(60, 60, 60),
            Color.FromRgb(230, 230, 230), Color.FromRgb(160, 110, 70), Color.FromRgb(255, 180, 190),
            Color.FromRgb(120, 210, 220), Color.FromRgb(90, 90, 160), Color.FromRgb(190, 190, 170)}
        CurColor = Color.FromArgb(255, Colors(Index).R, Colors(Index).G, Colors(Index).B)
        RefreshColorShow()
        Hint($"已选择颜色 RGB({CurColor.R}, {CurColor.G}, {CurColor.B})", HintType.Blue)
    End Sub

    Private Sub RefreshColorShow()
        PanColorShow.Background = New SolidColorBrush(CurColor)
    End Sub

    ''' <summary>读取指定像素颜色。</summary>
    Private Function GetPixel(X As Integer, Y As Integer) As Color
        If X < 0 OrElse X > 63 OrElse Y < 0 OrElse Y > 63 Then Return Color.FromArgb(0, 0, 0, 0)
        Dim Bytes(3) As Byte
        SkinBmp.CopyPixels(New Int32Rect(X, Y, 1, 1), Bytes, 4, 0)
        Return Color.FromArgb(Bytes(3), Bytes(2), Bytes(1), Bytes(0))
    End Function

    ''' <summary>写入指定像素颜色（BGRA）。</summary>
    Private Sub SetPixel(X As Integer, Y As Integer, C As Color)
        If X < 0 OrElse X > 63 OrElse Y < 0 OrElse Y > 63 Then Return
        Dim Bytes() As Byte = {C.B, C.G, C.R, C.A}
        SkinBmp.WritePixels(New Int32Rect(X, Y, 1, 1), Bytes, 4, 0)
    End Sub

    Private Sub SkinImage_MouseLeftButtonDown(Sender As Object, E As MouseButtonEventArgs)
        IsDrawing = True
        SkinImage.CaptureMouse()
        PaintAt(E.GetPosition(SkinImage))
    End Sub

    Private Sub SkinImage_MouseMove(Sender As Object, E As MouseEventArgs)
        If IsDrawing Then PaintAt(E.GetPosition(SkinImage))
    End Sub

    Private Sub SkinImage_MouseLeftButtonUp(Sender As Object, E As MouseButtonEventArgs)
        IsDrawing = False
        SkinImage.ReleaseMouseCapture()
        RenderPreview()
    End Sub

    Private Sub SkinImage_MouseLeave(Sender As Object, E As MouseEventArgs)
        If IsDrawing Then RenderPreview()
    End Sub

    ''' <summary>根据当前工具在指定画布坐标执行绘制。</summary>
    Private Sub PaintAt(Point As Point)
        Dim X = CInt(Math.Floor(Point.X / 10))
        Dim Y = CInt(Math.Floor(Point.Y / 10))
        If CurTool = 2 Then
            FillAt(X, Y)
        ElseIf CurTool = 3 Then
            Dim C = GetPixel(X, Y)
            If C.A > 0 Then
                CurColor = Color.FromArgb(255, C.R, C.G, C.B)
                RefreshColorShow()
                Hint($"已取色 RGB({C.R}, {C.G}, {C.B})", HintType.Blue)
            End If
        Else
            SetPixel(X, Y, If(CurTool = 1, Color.FromArgb(0, 0, 0, 0), CurColor))
        End If
    End Sub

    ''' <summary>洪水填充：替换相邻同色区域为当前颜色。</summary>
    Private Sub FillAt(X As Integer, Y As Integer)
        If X < 0 OrElse X > 63 OrElse Y < 0 OrElse Y > 63 Then Return
        Dim Target = GetPixel(X, Y)
        If Target = CurColor Then Return
        Dim Queue As New Queue(Of Integer)
        Queue.Enqueue(X * 64 + Y)
        While Queue.Count > 0
            Dim V = Queue.Dequeue()
            Dim Px = V \ 64, Py = V Mod 64
            If GetPixel(Px, Py) = Target Then
                SetPixel(Px, Py, CurColor)
                If Px > 0 Then Queue.Enqueue((Px - 1) * 64 + Py)
                If Px < 63 Then Queue.Enqueue((Px + 1) * 64 + Py)
                If Py > 0 Then Queue.Enqueue(Px * 64 + Py - 1)
                If Py < 63 Then Queue.Enqueue(Px * 64 + Py + 1)
            End If
        End While
    End Sub

    Private Sub BtnBrush_Click(Sender As Object, E As RoutedEventArgs) Handles BtnBrush.Click
        CurTool = 0
        SetToolButtons(BtnBrush)
    End Sub

    Private Sub BtnEraser_Click(Sender As Object, E As RoutedEventArgs) Handles BtnEraser.Click
        CurTool = 1
        SetToolButtons(BtnEraser)
    End Sub

    Private Sub BtnFill_Click(Sender As Object, E As RoutedEventArgs) Handles BtnFill.Click
        CurTool = 2
        SetToolButtons(BtnFill)
    End Sub

    Private Sub BtnPick_Click(Sender As Object, E As RoutedEventArgs) Handles BtnPick.Click
        CurTool = 3
        SetToolButtons(BtnPick)
    End Sub

    Private Sub SetToolButtons(Selected As MyButton)
        For Each Btn In {BtnBrush, BtnEraser, BtnFill, BtnPick}
            Btn.ColorType = If(Btn Is Selected, MyButton.ColorState.Highlight, MyButton.ColorState.Normal)
        Next
    End Sub

    Private Sub BtnClear_Click(Sender As Object, E As RoutedEventArgs) Handles BtnClear.Click
        InitCanvas()
        RenderPreview()
        Hint("画布已清空。", HintType.Blue)
    End Sub

    ''' <summary>载入当前账号皮肤（优先微软，其次外置登录），失败时回退到手动选择。</summary>
    Private Sub BtnLoad_Click(Sender As Object, E As RoutedEventArgs) Handles BtnLoad.Click
        Dim Uuid As String = Settings.Get(Of String)("CacheMsV2Uuid")
        Dim Type As String = "Ms"
        If String.IsNullOrEmpty(Uuid) Then
            Uuid = Settings.Get(Of String)("CacheNideUuid")
            Type = "Nide"
        End If
        If String.IsNullOrEmpty(Uuid) Then
            Uuid = Settings.Get(Of String)("CacheAuthUuid")
            Type = "Auth"
        End If
        If String.IsNullOrEmpty(Uuid) Then
            '无登录账号，手动选择皮肤文件
            Dim Info = McSkinSelect()
            If Info.IsVaild Then
                LoadSkinFile(Info.LocalFile)
                Hint("皮肤文件已载入。", HintType.Green)
            End If
            Return
        End If
        Try
            Hint("正在获取当前玩家皮肤……", HintType.Blue)
            Dim Address As String = McSkinGetAddress(Uuid, Type)
            Dim FilePath As String = McSkinDownload(Address)
            LoadSkinFile(FilePath)
            Hint("已载入当前玩家皮肤！", HintType.Green)
        Catch ex As Exception
            Hint("获取皮肤失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    ''' <summary>将任意尺寸皮肤图片缩放并写入 64×64 画布。</summary>
    Private Sub LoadSkinFile(Path As String)
        Try
            Dim Bmp As New BitmapImage
            Bmp.BeginInit()
            Bmp.UriSource = New Uri(Path)
            Bmp.CacheOption = BitmapCacheOption.OnLoad
            Bmp.EndInit()
            Dim Scale = 64.0 / Math.Max(Bmp.PixelWidth, Bmp.PixelHeight)
            Dim Trans As New TransformedBitmap(Bmp, New ScaleTransform(Scale, Scale))
            Dim W = Math.Max(1, Trans.PixelWidth), H = Math.Max(1, Trans.PixelHeight)
            Dim Stride = W * 4
            Dim RawBytes(Stride * H - 1) As Byte
            Trans.CopyPixels(RawBytes, Stride, 0)
            SkinBmp.WritePixels(New Int32Rect(0, 0, W, H), RawBytes, Stride, 0)
            RenderPreview()
        Catch ex As Exception
            Hint("读取皮肤文件失败：" & ex.Message, HintType.Red)
        End Try
    End Sub

    ''' <summary>保存画布为 PNG。</summary>
    Private Sub BtnSave_Click(Sender As Object, E As RoutedEventArgs) Handles BtnSave.Click
        Dim FileName As String = Dialogs.SaveFile("保存皮肤", "Skin.png", PathTemp, {({"png"}, "PNG 图片")})
        If FileName IsNot Nothing AndAlso FileName <> "" Then
            SavePng(FileName)
            Hint("皮肤已保存到：" & FileName, HintType.Green)
        End If
    End Sub

    Private Sub SavePng(Path As String)
        Dim Encoder As New PngBitmapEncoder
        Encoder.Frames.Add(BitmapFrame.Create(SkinBmp))
        Using Stream = File.Create(Path)
            Encoder.Save(Stream)
        End Using
    End Sub

    ''' <summary>上传皮肤到微软账号（需已登录微软账号）。</summary>
    Private Sub BtnUpload_Click(Sender As Object, E As RoutedEventArgs) Handles BtnUpload.Click
        If String.IsNullOrEmpty(Settings.Get(Of String)("CacheMsV2Access")) Then
            Hint("上传皮肤需要先登录微软账号！", HintType.Red)
            Return
        End If
        Dim SavePath As String = PathTemp & "SkinEditor.png"
        Try
            SavePng(SavePath)
        Catch ex As Exception
            Hint("保存临时皮肤失败：" & ex.Message, HintType.Red)
            Return
        End Try
        Hint("正在上传皮肤……", HintType.Blue)
        RunInNewThread(Sub() UploadSkin(SavePath), "Skin Upload")
    End Sub

    Private Sub UploadSkin(LocalFile As String)
        Try
            Dim AccessToken As String = Settings.Get(Of String)("CacheMsV2Access")
            Dim Result As String = NetRequestByClientRetry("https://api.minecraftservices.com/minecraft/profile/skins", HttpMethod.Post,
                Content:=New MultipartFormDataContent From {
                    {New StringContent("classic"), "variant"},
                    {New ByteArrayContent(FileUtils.ReadAsBytes(LocalFile)), "file", "skin.png"}
                },
                Headers:={{"Authorization", "Bearer " & AccessToken}, {"Accept", "*/*"}, {"User-Agent", "MojangSharp/0.1"}})
            If Result.Contains("request requires user authentication") Then
                RunInUi(Sub() Hint("登录状态失效，请重新登录后重试", HintType.Red))
                Return
            End If
            Dim ResultJson As JObject = Result.DeserializeJson()
            If ResultJson.ContainsKey("errorMessage") Then Throw New Exception(ResultJson("errorMessage").ToString)
            RunInUi(Sub() Hint("皮肤上传成功！", HintType.Green))
        Catch ex As Exception
            RunInUi(Sub() Hint("上传失败：" & ex.Message, HintType.Red))
        End Try
    End Sub

    ''' <summary>渲染角色盔甲预览（正面：头、身、双臂、双腿）。</summary>
    Private Sub RenderPreview()
        If PrevCanvas Is Nothing Then Return
        PrevCanvas.Children.Clear()
        DrawRegion(8, 8, 8, 8, 16, 4)        '头正面
        DrawRegion(44, 16, 4, 12, 0, 52)    '左臂
        DrawRegion(48, 16, 4, 12, 48, 52)   '右臂
        DrawRegion(20, 16, 8, 12, 16, 52)   '身体正面
        DrawRegion(4, 20, 4, 12, 16, 100)   '左腿
        DrawRegion(8, 20, 4, 12, 32, 100)   '右腿
        Select Case ArmorMode
            Case 1 : OverlayArmor(Color.FromArgb(120, 190, 195, 205)) '铁
            Case 2 : OverlayArmor(Color.FromArgb(120, 235, 215, 130)) '金
            Case 3 : OverlayArmor(Color.FromArgb(120, 120, 225, 235)) '钻
        End Select
    End Sub

    Private Sub DrawRegion(SX As Integer, SY As Integer, SW As Integer, SH As Integer, OX As Integer, OY As Integer)
        For X = 0 To SW - 1
            For Y = 0 To SH - 1
                Dim C = GetPixel(SX + X, SY + Y)
                Dim R As New Rectangle With {.Width = 4, .Height = 4, .Fill = New SolidColorBrush(C), .SnapsToDevicePixels = True}
                Canvas.SetLeft(R, OX + X * 4)
                Canvas.SetTop(R, OY + Y * 4)
                PrevCanvas.Children.Add(R)
            Next
        Next
    End Sub

    Private Sub OverlayArmor(Ac As Color)
        Dim Regions() As Rect = {
            New Rect(0, 52, 64, 48),   '身体与手臂
            New Rect(16, 100, 32, 48), '双腿
            New Rect(16, 4, 32, 32)}   '头部
        For Each R In Regions
            Dim Rect As New Rectangle With {.Width = R.Width, .Height = R.Height, .Fill = New SolidColorBrush(Ac), .IsHitTestVisible = False}
            Canvas.SetLeft(Rect, R.X)
            Canvas.SetTop(Rect, R.Y)
            PrevCanvas.Children.Add(Rect)
        Next
    End Sub

    Private Sub Armor_Checked(Sender As Object, E As RoutedEventArgs) Handles BtnArmorNone.Checked, BtnArmorIron.Checked, BtnArmorGold.Checked, BtnArmorDiamond.Checked
        Dim Btn = TryCast(Sender, MyRadioButton)
        If Btn IsNot Nothing Then
            ArmorMode = CInt(Btn.Tag)
            RenderPreview()
        End If
    End Sub

End Class
