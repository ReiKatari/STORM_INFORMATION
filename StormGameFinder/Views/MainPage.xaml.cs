using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using StormGameFinder.Models;
using StormGameFinder.Services;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml.Controls;

namespace StormGameFinder.Views;

public partial class MainPage : Page
{
    private readonly GameSearchService _searchService = new();
    private CancellationTokenSource? _cts;
    private string? _currentCoverUrl;
    private string? _currentGameName;

    private static readonly string[] RowColors =
        ["#7C3AED", "#3B82F6", "#06B6D4", "#10B981", "#F59E0B",
         "#EF4444", "#EC4899", "#8B5CF6", "#14B8A6", "#F97316"];

    public MainPage()
    {
        this.InitializeComponent();
        if (App.MainWindow != null) App.MainWindow.SetTitleBar(TitleBarDragArea);
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) _ = PerformSearchAsync();
    }
    private void SearchButton_Click(object sender, RoutedEventArgs e) => _ = PerformSearchAsync();

    private async Task PerformSearchAsync()
    {
        var query = SearchBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;
        _cts?.Cancel(); _cts = new CancellationTokenSource();

        SearchButton.IsEnabled = false;
        LoadingPanel.Visibility = Visibility.Visible;
        ResultsPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Collapsed;
        LoadingText.Text = $"Ищем «{query}» по всем платформам...";

        try
        {
            var result = await _searchService.SearchAsync(query, _cts.Token);
            if (_cts.IsCancellationRequested) return;
            if (result == null || (result.Platforms.Count == 0 && result.DownloadLinks.Count == 0))
            {
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorText.Text = $"Не удалось найти «{query}».";
            }
            else
            {
                PopulateResults(result);
                ResultsPanel.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = $"Ошибка: {ex.Message}";
        }
        finally { LoadingPanel.Visibility = Visibility.Collapsed; SearchButton.IsEnabled = true; }
    }

    private void PopulateResults(GameInfo info)
    {
        _currentGameName = info.Name;
        _currentCoverUrl = info.CoverImageUrl;

        GameTitle.Text = info.Name;
        GameDescription.Text = string.IsNullOrWhiteSpace(info.Description) ? "Описание недоступно" : info.Description;
        QuickVersion.Text = info.Version;
        QuickUpdated.Text = info.LastUpdated;

        // Cover image
        if (!string.IsNullOrWhiteSpace(info.CoverImageUrl))
        {
            var bmp = new BitmapImage { DecodePixelWidth = 560, DecodePixelHeight = 800 };
            bmp.ImageFailed += (_, _) => CoverFallback.Visibility = Visibility.Visible;
            bmp.UriSource = new Uri(info.CoverImageUrl);
            CoverImage.Source = bmp;
            CoverFallback.Visibility = Visibility.Collapsed;
        }
        else
        {
            CoverImage.Source = null;
            CoverFallback.Visibility = Visibility.Visible;
        }

        // Platform table
        PlatformTableRows.Children.Clear();
        NoPlatformsText.Visibility = info.Platforms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < info.Platforms.Count; i++)
        {
            var p = info.Platforms[i];
            var color = ParseColor(RowColors[i % RowColors.Length]);
            var row = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(18, color.R, color.G, color.B))
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

            // Platform name
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            namePanel.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center });
            namePanel.Children.Add(new TextBlock { Text = p.Platform, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

            if (!string.IsNullOrEmpty(p.StoreUrl))
            {
                var btn = new HyperlinkButton { Content = namePanel, NavigateUri = new Uri(p.StoreUrl), Padding = new Thickness(0) };
                Grid.SetColumn(btn, 0); grid.Children.Add(btn);
            }
            else { Grid.SetColumn(namePanel, 0); grid.Children.Add(namePanel); }

            AddCell(grid, p.GameId, 1, 12, 0.7);
            var verBlock = AddCell(grid, p.Version, 2, 12, 1.0);
            verBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            verBlock.Foreground = p.Version != "—" ? new SolidColorBrush(color) : new SolidColorBrush(Windows.UI.Color.FromArgb(100, 128, 128, 128));
            AddCell(grid, p.LastUpdated, 3, 12, 0.7);

            row.Child = grid;
            PlatformTableRows.Children.Add(row);
        }

        // Download links
        DownloadLinksPanel.Children.Clear();
        NoLinksText.Visibility = info.DownloadLinks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < info.DownloadLinks.Count; i++)
        {
            var lk = info.DownloadLinks[i];
            var color = ParseColor(RowColors[i % RowColors.Length]);
            var border = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 10, 14, 10),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(18, color.R, color.G, color.B))
            };
            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

            var numBg = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center };
            numBg.Child = new TextBlock { Text = (i + 1).ToString(), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = White(), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(numBg, 0); grid.Children.Add(numBg);

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
            sp.Children.Add(new TextBlock { Text = lk.SiteName, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 });
            sp.Children.Add(new TextBlock { Text = lk.Domain, FontSize = 10, Opacity = 0.45, MaxLines = 1 });
            Grid.SetColumn(sp, 1); grid.Children.Add(sp);

            var openBtn = new Button
            {
                Content = "ОТКРЫТЬ", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = White(), Background = new SolidColorBrush(color),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 5, 14, 5),
                VerticalAlignment = VerticalAlignment.Center, Tag = lk.Url, BorderThickness = new Thickness(0)
            };
            openBtn.Click += OpenLink_Click;
            Grid.SetColumn(openBtn, 2); grid.Children.Add(openBtn);

            border.Child = grid;
            DownloadLinksPanel.Children.Add(border);
        }
    }

    // ═══ Cover fullscreen & save ════════════════════════════════
    private async void CoverBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentCoverUrl)) return;

        var img = new Image
        {
            Source = new BitmapImage(new Uri(_currentCoverUrl)),
            Stretch = Stretch.Uniform, MaxHeight = 700, MaxWidth = 500
        };

        var dialog = new ContentDialog
        {
            Title = _currentGameName ?? "Обложка",
            Content = img,
            PrimaryButtonText = "Сохранить",
            CloseButtonText = "Закрыть",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await SaveCoverAsync();
    }

    private async Task SaveCoverAsync()
    {
        if (string.IsNullOrEmpty(_currentCoverUrl)) return;
        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeChoices.Add("JPG Image", [".jpg"]);
            picker.FileTypeChoices.Add("PNG Image", [".png"]);
            picker.SuggestedFileName = $"{_currentGameName ?? "cover"}_cover";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow!);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var bytes = await new HttpClient().GetByteArrayAsync(_currentCoverUrl);
                await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
            }
        }
        catch { }
    }

    // ═══ Cover hover ════════════════════════════════════════════
    private void Cover_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.Opacity = 0.85;
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
    }

    private void Cover_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border b) b.Opacity = 1.0;
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
    }

    // ═══ Helpers ═════════════════════════════════════════════════
    private async void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string url && url.StartsWith("http"))
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(url)); } catch { }
    }

    private static TextBlock AddCell(Grid grid, string text, int col, double fontSize, double opacity)
    {
        var tb = new TextBlock { Text = text, FontSize = fontSize, Opacity = opacity, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(tb, col); grid.Children.Add(tb);
        return tb;
    }

    private static SolidColorBrush White() => new(Windows.UI.Color.FromArgb(255, 255, 255, 255));

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }
}
