﻿Public Class PageDownloadCompFavorites

#Region "加载器信息"
    '加载器信息
    Public Loader As New LoaderTask(Of List(Of String), List(Of CompProject))("CompProject Favorites", AddressOf CompFavoritesGet, AddressOf LoaderInput)

    Private Sub PageDownloadCompFavorites_Inited(sender As Object, e As EventArgs) Handles Me.Initialized
        RefreshFavTargets()
        PageLoaderInit(Load, PanLoad, PanContent, Nothing, Loader, AddressOf Load_OnFinish, AddressOf LoaderInput)
    End Sub
    Private Sub PageDownloadCompFavorites_Loaded(sender As Object, e As EventArgs) Handles Me.Loaded
        Items_SetSelectAll(False)
        RefreshBar()
        If Loader.Input IsNot Nothing AndAlso (Not Loader.Input.Count.Equals(CompFavorites.FavoritesList.Count) OrElse Loader.Input.Except(CompFavorites.FavoritesList).Any()) Then
            RefreshFavTargets()
        End If
    End Sub

    Private Function LoaderInput() As List(Of String)
        Dim TargetList As List(Of String)
        Try
            TargetList = CurrentFavTarget.Favs
        Catch ex As Exception
            Log(ex, "[Favorites] 加载收藏夹列表时出错")
        End Try
        Return TargetList.Clone() '复制而不是直接引用！
    End Function
    Private Sub CompFavoritesGet(Task As LoaderTask(Of List(Of String), List(Of CompProject)))
        Task.Output = CompRequest.GetCompProjectsByIds(Task.Input)
    End Sub
#End Region

    Private CompItemList As New List(Of MyListItem)
    Private SelectedItemList As New List(Of MyListItem)
    Private ReadOnly Property CurrentFavTarget As CompFavorites.FavData
        Get
            Dim SelectedItem As MyComboBoxItem = ComboTargetFav.SelectedItem
            If SelectedItem Is Nothing Then
                Log("[Favorites] 异常：未选择收藏夹")
                SelectedItem = ComboTargetFav.Items.GetItemAt(0)
            End If
            Return CompFavorites.FavoritesList.Where(Function(e) e.Id = SelectedItem.Tag).First()
        End Get
    End Property

#Region "UI 化 - 自适应卡片"
    Class CompListItemContainer ' 用来存储自动依据类型生成的卡片及其相关信息
        Public Property Card As MyCard
        Public Property ContentList As StackPanel
        Public Property Title As String
        Public Property CompType As Integer
    End Class

    Dim ItemList As New List(Of CompListItemContainer)

    ''' <summary>
    ''' 刷新收藏夹列表
    ''' </summary>
    Private Sub RefreshFavTargets()
        ComboTargetFav.Items.Clear()
        For Each Target In CompFavorites.FavoritesList
            Dim Item As New MyComboBoxItem With {
                .Content = Target.Name,
                .Tag = Target.Id
            }
            ComboTargetFav.Items.Add(Item)
        Next
    End Sub

    ''' <summary>
    ''' 返回适合当前工程项目的卡片记录
    ''' </summary>
    ''' <param name="Type">工程项目类型</param>
    ''' <returns></returns>
    Private Function GetSuitListContainer(Type As Integer) As CompListItemContainer
        If ItemList.Any(Function(e) e.CompType.Equals(Type)) Then
            Return ItemList.First(Function(e) e.CompType.Equals(Type))
        Else
            Dim NewItem As New CompListItemContainer With {
            .Card = New MyCard With {
                .CanSwap = True,
                .Margin = New Thickness(0, 0, 0, 15)
            },
            .ContentList = New StackPanel With {
                .Orientation = Orientation.Vertical,
                .Margin = New Thickness(12, 38, 12, 12)
            },
            .CompType = Type
            }
            Select Case Type
                Case -1
                    NewItem.Title = "搜索结果 ({0})" ' 搜索结果
                Case CompType.Mod
                    NewItem.Title = "Mod ({0})"
                Case CompType.ModPack
                    NewItem.Title = "整合包 ({0})"
                Case CompType.ResourcePack
                    NewItem.Title = "资源包 ({0})"
                Case CompType.Shader
                    NewItem.Title = "光影包 ({0})"
                Case Else
                    NewItem.Title = "未分类类型 ({0})"
            End Select
            NewItem.Card.Title = String.Format(NewItem.Title, 0)
            NewItem.Card.Children.Add(NewItem.ContentList)
            ItemList.Add(NewItem)
            Return NewItem
        End If
    End Function

    Private Sub RefreshContent()
        For Each item In ItemList ' 清除逻辑父子关系
            item.ContentList.Children.Clear()
        Next
        PanContentList.Children.Clear()
        Dim DataSource As List(Of MyListItem) = If(IsSearching, SearchResult, CompItemList)
        For Each item As MyListItem In DataSource
            GetSuitListContainer(If(IsSearching, -1, CType(item.Tag, CompProject).Type)).ContentList.Children.Add(item)
        Next
        For Each item In ItemList
            If item.ContentList.Children.Count = 0 Then Continue For
            PanContentList.Children.Add(item.Card)
        Next
    End Sub

    Private Sub RefreshCardTitle()
        For Each item In ItemList
            item.Card.Title = String.Format(item.Title, CompItemList.Where(Function(e) CType(e.Tag, CompProject).Type = item.CompType).Count())
        Next
        If Not ItemList.Any(Function(e) e.CompType.Equals(-1)) Then Return
        Dim SearchItem = ItemList.First(Function(e) e.CompType.Equals(-1))
        If SearchItem IsNot Nothing Then
            SearchItem.Card.Title = String.Format(SearchItem.Title, SearchResult.Count)
        End If
    End Sub

#End Region

#Region "UI 化 - 加载主逻辑"

    '结果 UI 化
    Private Sub Load_OnFinish()
        If ComboTargetFav.SelectedIndex = -1 Then
            ComboTargetFav.SelectedIndex = 0 '默认选择第一个
        End If
        ItemList.Clear()
        Try
            AllowSearch = False
            PanSearchBox.Text = String.Empty
            AllowSearch = True
            CompItemList.Clear()
            Dim SomeGetFail As Boolean = Loader.Input.Count <> Loader.Output.Count
            HintGetFail.Visibility = If(SomeGetFail, Visibility.Visible, Visibility.Collapsed)
            For Each item In Loader.Output
                Dim CompItem = item.ToListItem()
                ListItemBuild(CompItem)
                CompItemList.Add(CompItem)
            Next
            If CompItemList.Any() Then '有收藏
                If Not IsSearching Then
                    PanSearchBox.Visibility = Visibility.Visible
                    PanContentList.Visibility = Visibility.Visible
                    CardNoContent.Visibility = Visibility.Collapsed
                End If
            Else '没有收藏
                PanSearchBox.Visibility = Visibility.Collapsed
                PanContentList.Visibility = Visibility.Collapsed
                CardNoContent.Visibility = Visibility.Visible
            End If

            'If SomeGetFail Then
            '    Dim FailList As New List(Of MyListItem)
            '    Dim FailIds = Loader.Input.Except(Loader.Output.Select(Function(e) e.Id))
            '    For Each Id In FailIds
            '        Dim FailItem As New MyListItem
            '        FailItem.Title = $"{Id}"
            '        FailItem.Info = "此资源获取失败，可能在线资源被删除或者未获取成功"
            '        FailItem.Tag = Id

            '        ListItemBuild(FailItem)

            '        FailList.Add(FailItem)
            '    Next
            '    CompItemList.AddRange(FailList)
            'End If

            RefreshContent()
            RefreshCardTitle()
        Catch ex As Exception
            Log(ex, "可视化收藏夹列表出错", LogLevel.Feedback)
        End Try
    End Sub

    Private Sub ListItemBuild(CompItem As MyListItem)
        CompItem.Type = MyListItem.CheckType.CheckBox
        '----添加按钮----
        '删除按钮
        Dim Btn_Delete As New MyIconButton
        Btn_Delete.Logo = Logo.IconButtonLikeFill
        Btn_Delete.ToolTip = "取消收藏"
        ToolTipService.SetPlacement(Btn_Delete, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(Btn_Delete, 30)
        ToolTipService.SetHorizontalOffset(Btn_Delete, 2)
        AddHandler Btn_Delete.Click, Sub(sender As Object, e As EventArgs)
                                         Items_CancelFavorites(CompItem)
                                         RefreshContent()
                                         RefreshCardTitle()
                                         RefreshBar()
                                     End Sub
        CompItem.Buttons = {Btn_Delete}
        '---操作逻辑---
        '右键查看详细信息界面
        If TypeOf (CompItem.Tag) Is CompProject Then
            AddHandler CompItem.MouseRightButtonUp, Sub(sender As Object, e As EventArgs)
                                                        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.CompDetail,
           .Additional = {CompItem.Tag, New List(Of String), String.Empty, CompModLoaderType.Any}})
                                                    End Sub
        End If
        '---其它事件---
        AddHandler CompItem.Changed, AddressOf ItemCheckStatusChanged
    End Sub

#End Region

#Region "UI 化 - 选择操作"

    Private BottomBarShownCount As Integer = 0

    Private Sub RefreshBar()
        Dim NewCount As Integer = SelectedItemList.Count
        Dim Selected = NewCount > 0
        If Selected Then LabSelect.Text = $"已选择 {NewCount} 个收藏项目" '取消所有选择时不更新数字
        '更新显示状态
        If AniControlEnabled = 0 Then
            PanContentList.Margin = New Thickness(0, 0, 0, If(Selected, 80, 0))
            If Selected Then
                '仅在数量增加时播放出现/跳跃动画
                If BottomBarShownCount >= NewCount Then
                    BottomBarShownCount = NewCount
                    Return
                Else
                    BottomBarShownCount = NewCount
                End If
                '出现/跳跃动画
                CardSelect.Visibility = Visibility.Visible
                AniStart({
                    AaOpacity(CardSelect, 1 - CardSelect.Opacity, 60),
                    AaTranslateY(CardSelect, -27 - TransSelect.Y, 120, Ease:=New AniEaseOutFluent(AniEasePower.Weak)),
                    AaTranslateY(CardSelect, 3, 150, 120, Ease:=New AniEaseInoutFluent(AniEasePower.Weak)),
                    AaTranslateY(CardSelect, -1, 90, 270, Ease:=New AniEaseInoutFluent(AniEasePower.Weak))
                }, "CompFavorites Sidebar")
            Else
                '不重复播放隐藏动画
                If BottomBarShownCount = 0 Then Return
                BottomBarShownCount = 0
                '隐藏动画
                AniStart({
                    AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                    AaTranslateY(CardSelect, -10 - TransSelect.Y, 90, Ease:=New AniEaseInFluent(AniEasePower.Weak)),
                    AaCode(Sub() CardSelect.Visibility = Visibility.Collapsed, After:=True)
                }, "CompFavorites Sidebar")
            End If
        Else
            AniStop("CompFavorites Sidebar")
            BottomBarShownCount = NewCount
            If Selected Then
                CardSelect.Visibility = Visibility.Visible
                CardSelect.Opacity = 1
                TransSelect.Y = -25
            Else
                CardSelect.Visibility = Visibility.Collapsed
                CardSelect.Opacity = 0
                TransSelect.Y = -10
            End If
        End If
    End Sub

#End Region

#Region "事件"
    '选中状态改变
    Private Sub ItemCheckStatusChanged(sender As Object, e As RouteEventArgs)
        Dim SenderItem As MyListItem = sender
        If SelectedItemList.Contains(SenderItem) Then SelectedItemList.Remove(SenderItem)
        If SenderItem.Checked Then SelectedItemList.Add(SenderItem)
        RefreshBar()
    End Sub

    '自动重试
    Private Sub Load_State(sender As Object, state As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState) Handles Load.StateChanged
        Select Case Loader.State
            Case LoadState.Failed
                Dim ErrorMessage As String = ""
                If Loader.Error IsNot Nothing Then ErrorMessage = Loader.Error.Message
                If ErrorMessage.Contains("不是有效的 json 文件") Then
                    Log("[Download] 下载的工程列表 JSON 文件损坏，已自动重试", LogLevel.Debug)
                    PageLoaderRestart()
                End If
        End Select
    End Sub

    Private Sub Btn_FavoritesCancel_Clicked(sender As Object, e As RouteEventArgs) Handles Btn_FavoritesCancel.Click
        For Each Items In SelectedItemList.Clone()
            Items_CancelFavorites(Items)
        Next
        If CompItemList.Any Then
            RefreshContent()
            RefreshCardTitle()
        Else
            Loader.Start()
        End If
        RefreshBar()
    End Sub

    Private Sub Btn_SelectCancel_Clicked(sender As Object, e As RouteEventArgs) Handles Btn_SelectCancel.Click
        SelectedItemList.Clear()
        Items_SetSelectAll(False)
    End Sub

    Private Sub Items_SetSelectAll(TargetStatus As Boolean)
        If IsSearching Then
            For Each Item As MyListItem In SearchResult
                Item.Checked = TargetStatus
            Next
        Else
            For Each Item As MyListItem In CompItemList
                Item.Checked = TargetStatus
            Next
        End If
        SelectedItemList = CompItemList.Where(Function(e) e.Checked).ToList()
    End Sub

    Private Sub Items_CancelFavorites(Item As MyListItem)
        Try
            CompItemList.Remove(Item)
            If SelectedItemList.Contains(Item) Then SelectedItemList.Remove(Item)
            If SearchResult.Contains(Item) Then SearchResult.Remove(Item)
            CurrentFavTarget.Favs.Remove(Item.Tag.Id)
            CompFavorites.Save()
        Catch ex As Exception
            Log(ex, "[CompFavourites] 移除收藏时发生错误")
        End Try
    End Sub

    Private Sub Page_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        If My.Computer.Keyboard.CtrlKeyDown AndAlso e.Key = Key.A Then Items_SetSelectAll(True)
    End Sub

    Private Sub Manage_Click(sender As Object, e As MouseButtonEventArgs)
        Dim Body As New ContextMenu()
        Dim NewItem As New MyMenuItem With {
            .Header = "分享当前收藏夹",
            .Icon = Logo.IconButtonShare
        }
        AddHandler NewItem.Click, Sub()
                                      Hint("努力开发中")
                                  End Sub
        Body.Items.Add(NewItem)
        NewItem = New MyMenuItem With {
            .Header = "导入收藏",
            .Icon = Logo.IconButtonAdd
        }
        AddHandler NewItem.Click, Sub()
                                      Hint("努力开发中")
                                  End Sub
        Body.Items.Add(NewItem)
        NewItem = New MyMenuItem With {
            .Header = "新建收藏夹",
            .Icon = Logo.IconButtonCreate
        }
        AddHandler NewItem.Click, Sub()
                                      Dim NewFavName As String = MyMsgBoxInput("新建收藏夹", "请输入新收藏夹名称")
                                      If String.IsNullOrWhiteSpace(NewFavName) Then Exit Sub
                                      CompFavorites.FavoritesList.Add(CompFavorites.GetNewFav(NewFavName, Nothing))
                                      CompFavorites.Save()
                                      RefreshFavTargets()
                                      ComboTargetFav.SelectedIndex = ComboTargetFav.Items.Count - 1 '默认选择第一个
                                  End Sub
        Body.Items.Add(NewItem)
        NewItem = New MyMenuItem With {
            .Header = "删除当前收藏夹",
            .Icon = Logo.IconButtonDelete
        }
        AddHandler NewItem.Click, Sub()
                                      If CompFavorites.FavoritesList.Count = 1 Then
                                          Hint("您不能删除最后一个收藏夹")
                                          Exit Sub
                                      End If
                                      Dim content = $"确认删除 {CurrentFavTarget.Name} 收藏夹？" & vbCrLf & vbCrLf
                                      content &= $"此收藏夹有 {CurrentFavTarget.Favs.Count} 个收藏项目" & vbCrLf
                                      content &= "收藏夹 ID 为 " & CurrentFavTarget.Id & vbCrLf
                                      content &= "此操作不可逆！"
                                      Dim res = MyMsgBox(content, "删除确认", IsWarn:=True, Button1:="否", Button2:="否", Button3:="是")
                                      If res = 3 Then
                                          CompFavorites.FavoritesList.Remove(CurrentFavTarget)
                                          CompFavorites.Save()
                                          Hint("已删除收藏夹", HintType.Finish)
                                          RefreshFavTargets()
                                          ComboTargetFav.SelectedIndex = 0
                                      End If
                                  End Sub
        Body.Items.Add(NewItem)
        Body.PlacementTarget = sender
        Body.Placement = Primitives.PlacementMode.Bottom
        Body.IsOpen = True
    End Sub

    Private Sub ComboTargetFav_Selected(sender As Object, e As RoutedEventArgs) Handles ComboTargetFav.SelectionChanged
        If ComboTargetFav.SelectedItem Is Nothing Then Exit Sub
        Loader.Start(IsForceRestart:=True)
    End Sub

    Private Sub HintGetFail_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles HintGetFail.MouseLeftButtonDown
        Dim Content As String = "由于在线资源被删除或者网络问题等因素导致以下资源未获取成功（以资源的 ID 展示）" & vbCrLf & vbCrLf
        Dim FailIds = Loader.Input.Except(Loader.Output.Select(Function(i) i.Id))
        For Each Id In FailIds
            Content &= $" - {Id}" & vbCrLf
        Next
        MyMsgBox(Content,
                 "部分收藏项目获取失败",
                 Button2:="复制这些 ID",
                 Button3:="移除这些收藏",
                 Button2Action:=Sub()
                                    ClipboardSet(FailIds.Join(vbCrLf))
                                End Sub,
                 Button3Action:=Sub()
                                    For Each Id In FailIds
                                        CurrentFavTarget.Favs.Remove(Id)
                                    Next
                                    Hint("已移除相关收藏", HintType.Finish)
                                End Sub)
    End Sub

#End Region

#Region "搜索"

    Private ReadOnly Property IsSearching As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(PanSearchBox.Text)
        End Get
    End Property

    Private AllowSearch As Boolean = True
    Private SearchResult As New List(Of MyListItem)
    Public Sub SearchRun() Handles PanSearchBox.TextChanged
        If Not AllowSearch Then Exit Sub
        If IsSearching Then
            '构造请求
            Dim QueryList As New List(Of SearchEntry(Of MyListItem))
            For Each Item As MyListItem In CompItemList
                If TypeOf Item.Tag IsNot CompProject Then Continue For
                Dim Entry As CompProject = Item.Tag
                Dim SearchSource As New List(Of KeyValuePair(Of String, Double))
                SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.RawName, 1))
                If Entry.Description IsNot Nothing AndAlso Entry.Description <> "" Then
                    SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.Description, 0.4))
                End If
                If Entry.TranslatedName <> Entry.RawName Then SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.TranslatedName, 1))
                SearchSource.Add(New KeyValuePair(Of String, Double)(String.Join("", Entry.Tags), 0.2))
                QueryList.Add(New SearchEntry(Of MyListItem) With {.Item = Item, .SearchSource = SearchSource})
            Next
            '进行搜索
            SearchResult = Search(QueryList, PanSearchBox.Text, MaxBlurCount:=6, MinBlurSimilarity:=0.35).Select(Function(r) r.Item).ToList
        End If
        RefreshContent()
        RefreshCardTitle()
    End Sub

#End Region

End Class
