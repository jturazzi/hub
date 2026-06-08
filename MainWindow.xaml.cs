using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Hub.Models;
using Hub.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Hub;

public partial class MainWindow : Window
{
    private readonly AuthenticatedUser  _authenticatedUser;
    private readonly WebViewHostManager _webViewManager;
    private readonly HubConfig          _config;

    private readonly List<BrowserTab> _tabs = new();
    private BrowserTab? _activeTab;
    private bool _isDisposing;
    private int  _tabCounter;

    public bool RequestedLogout { get; private set; }

    private sealed class BrowserTab
    {
        public string    Key      { get; init; } = "";
        public string    Title    { get; set; }   = "Nouvel onglet";
        public AppEntry? App      { get; set; }
        public string?   Url      { get; set; }
        public bool      IsHome   { get; set; }
    }

    public MainWindow(AuthenticatedUser authenticatedUser)
    {
        _authenticatedUser = authenticatedUser;
        _config            = HubConfigService.Load();
        _webViewManager    = new WebViewHostManager(authenticatedUser.SidValue, _config.CacheFolder);

        InitializeComponent();
        ApplyConfig();

        Loaded  += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    // ─── Config & UI init ────────────────────────────────────────────────────

    private void ApplyConfig()
    {
        Title                  = $"{_config.AppTitle} - {_authenticatedUser.DisplayName}";
        UserInfoText.Text      = _authenticatedUser.DisplayName;
        UserAvatarInitials.Text = GetInitials(_authenticatedUser.DisplayName);
        LogoutText.Text        = L10n.T("Main_Logout");
        BackButton.ToolTip     = L10n.T("Main_NavBack");
        ForwardButton.ToolTip  = L10n.T("Main_NavForward");
        RefreshButton.ToolTip  = L10n.T("Main_NavRefresh");
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var startupApps = _config.Apps.Where(a => a.OpenOnStartup).ToList();

        if (startupApps.Count == 0)
        {
            // Aucun favori automatique → on ouvre la page d'accueil
            await OpenHomeTabAsync();
            return;
        }

        // Ouvre tous les favoris marqués OpenOnStartup, le 1er actif
        for (int i = 0; i < startupApps.Count; i++)
            await OpenAppInNewTabAsync(startupApps[i], activate: i == 0);
    }

    private async Task OpenAppInNewTabAsync(AppEntry app, bool activate)
    {
        var tab = new BrowserTab
        {
            Key   = $"tab-{++_tabCounter}",
            App   = app,
            Url   = app.Url,
            Title = app.Name
        };
        _tabs.Add(tab);

        if (activate)
        {
            await ShowTabAsync(tab, app.Url);
        }
        else
        {
            // Préchargement silencieux : on initialise le WebView sans le mettre dans BrowserHost
            try
            {
                var webView = _webViewManager.GetOrCreate(tab.Key);
                await _webViewManager.EnsureInitializedAsync(tab.Key);
                HookNavigationEvents(webView);
                webView.Source = new Uri(app.Url);
            }
            catch
            {
                // ignore : l'onglet sera (re)chargé au clic
            }
            RebuildTabsBar();
        }
    }

    // ─── Barre des onglets ───────────────────────────────────────────────────

    private void RebuildTabsBar()
    {
        TabsPanel.Children.Clear();
        foreach (var tab in _tabs)
            TabsPanel.Children.Add(BuildTabItem(tab));
    }

    private Border BuildTabItem(BrowserTab tab)
    {
        var isActive = tab == _activeTab;

        var label = new TextBlock
        {
            Text                = string.IsNullOrWhiteSpace(tab.Title) ? "Nouvel onglet" : tab.Title,
            FontSize            = 12,
            Foreground          = new SolidColorBrush(isActive
                                    ? Color.FromRgb(0x0F, 0x4C, 0x8C)
                                    : Color.FromRgb(0x44, 0x4A, 0x52)),
            FontWeight          = isActive ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment   = VerticalAlignment.Center,
            TextTrimming        = TextTrimming.CharacterEllipsis,
            MaxWidth            = 160,
            Margin              = new Thickness(0, 0, 8, 0)
        };

        var closeBtn = new Button
        {
            Content           = "✕",
            FontSize          = 10,
            Width             = 18,
            Height            = 18,
            Padding           = new Thickness(0),
            Background        = Brushes.Transparent,
            BorderThickness   = new Thickness(0),
            Foreground        = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0x98)),
            Cursor            = Cursors.Hand,
            FocusVisualStyle  = null,
            VerticalAlignment = VerticalAlignment.Center,
            Tag               = tab,
            ToolTip           = "Fermer",
            Template          = BuildCloseButtonTemplate()
        };
        closeBtn.Click += CloseTab_Click;

        var inner = new StackPanel
        {
            Orientation       = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(12, 0, 6, 0)
        };
        inner.Children.Add(label);
        inner.Children.Add(closeBtn);

        var container = new Border
        {
            Background    = isActive ? Brushes.White : Brushes.Transparent,
            Margin        = new Thickness(2, 5, 0, 0),
            Cursor        = Cursors.Hand,
            Tag           = tab,
            Child         = inner,
            Height        = 33,
            MinWidth      = 120,
            MaxWidth      = 220,
            CornerRadius  = new CornerRadius(8, 8, 0, 0)
        };

        if (isActive)
        {
            container.Effect = new DropShadowEffect
            {
                Color       = Color.FromRgb(0, 0, 0),
                Opacity     = 0.08,
                BlurRadius  = 6,
                ShadowDepth = 0
            };
        }

        container.MouseLeftButtonDown += TabContainer_Click;
        return container;
    }

    private static ControlTemplate BuildCloseButtonTemplate()
    {
        var tpl = new ControlTemplate(typeof(Button));

        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Bd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
        border.AppendChild(presenter);

        tpl.VisualTree = border;

        tpl.Triggers.Add(new Trigger
        {
            Property = UIElement.IsMouseOverProperty,
            Value    = true,
            Setters  =
            {
                new Setter(Border.BackgroundProperty,
                    new SolidColorBrush(Color.FromArgb(0x22, 0xE0, 0x1B, 0x0D)), "Bd"),
                new Setter(Control.ForegroundProperty,
                    new SolidColorBrush(Color.FromRgb(0xE0, 0x1B, 0x0D)))
            }
        });

        return tpl;
    }

    private async void TabContainer_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: BrowserTab tab })
            await ShowTabAsync(tab);
    }

    // ─── Création d'onglets ─────────────────────────────────────────────────

    private async Task OpenHomeTabAsync()
    {
        var tab = new BrowserTab
        {
            Key    = $"tab-{++_tabCounter}",
            Title  = "Accueil",
            IsHome = true
        };
        _tabs.Add(tab);

        await ShowTabAsync(tab, BuildHomeHtmlDataUri());
    }

    private async void AddTab_Click(object sender, RoutedEventArgs e)
    {
        await OpenHomeTabAsync();
    }

    private async void Home_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null)
        {
            await OpenHomeTabAsync();
            return;
        }

        _activeTab.IsHome = true;
        _activeTab.Title  = "Accueil";
        _activeTab.Url    = null;
        _activeTab.App    = null;
        RebuildTabsBar();

        await ShowTabAsync(_activeTab, BuildHomeHtmlDataUri());
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: BrowserTab tab }) return;

        var index = _tabs.IndexOf(tab);
        _tabs.Remove(tab);
        _webViewManager.Remove(tab.Key);

        if (_activeTab == tab)
        {
            _activeTab = null;
            if (_tabs.Count == 0)
            {
                await OpenHomeTabAsync();
                return;
            }
            var next = _tabs[Math.Min(index, _tabs.Count - 1)];
            await ShowTabAsync(next);
        }
        else
        {
            RebuildTabsBar();
        }
    }

    // ─── Affichage d'un onglet ──────────────────────────────────────────────

    private async Task ShowTabAsync(BrowserTab tab, string? navigateTo = null)
    {
        try
        {
            try { CoreWebView2Environment.GetAvailableBrowserVersionString(); }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    L10n.T("Msg_WebView2Missing", ex.Message),
                    L10n.T("Msg_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _activeTab = tab;
            RebuildTabsBar();

            var webView = _webViewManager.GetOrCreate(tab.Key);
            BrowserHost.Content = webView;

            await _webViewManager.EnsureInitializedAsync(tab.Key);
            HookNavigationEvents(webView);

            if (!string.IsNullOrEmpty(navigateTo))
            {
                if (navigateTo.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    webView.CoreWebView2.NavigateToString(BuildHomeHtml());
                else
                    webView.Source = new Uri(navigateTo);
            }
            else
            {
                UpdateNavBar(webView);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, L10n.T("Msg_LoadError", tab.Title, ex.Message),
                L10n.T("Msg_Error"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─── Page d'accueil "Nouvel onglet" ─────────────────────────────────────

    private static string BuildHomeHtmlDataUri() => "data:home";

    private string BuildHomeHtml()
    {
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html><html><head><meta charset="utf-8"><title>Nouvel onglet</title>
<style>
  body{margin:0;font-family:'Segoe UI Variable Text','Segoe UI',sans-serif;
       background:linear-gradient(180deg,#F7F9FC 0%,#EEF2F8 100%);color:#1A1A1A;
       min-height:100vh;display:flex;flex-direction:column;align-items:center;padding:80px 24px;}
  h1{font-size:28px;font-weight:600;margin:0 0 8px;color:#0F4C8C;}
  p{color:#5A6470;font-size:14px;margin:0 0 40px;}
  .grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));
        gap:16px;max-width:900px;width:100%;}
  .card{background:#fff;border-radius:12px;padding:20px 18px;text-decoration:none;
        color:#1A1A1A;box-shadow:0 1px 3px rgba(0,0,0,.06);transition:.18s;
      display:flex;align-items:center;gap:14px;cursor:pointer;}
  .card:hover{transform:translateY(-2px);box-shadow:0 6px 16px rgba(0,0,0,.10);}
  .pill{width:38px;height:38px;border-radius:50%;display:flex;align-items:center;
        justify-content:center;color:#fff;font-weight:700;font-size:14px;flex-shrink:0;}
  .name{font-size:14px;font-weight:600;}
</style></head><body>
<h1>Hub
""");
        sb.Append(System.Net.WebUtility.HtmlEncode(_config.AppTitle is { Length: > 0 } t ? " - " + t : ""));
        sb.Append("</h1><p>Choisissez un favori pour démarrer</p><div class='grid'>");

        for (int i = 0; i < _config.Apps.Count; i++)
        {
            var app   = _config.Apps[i];
            var color = AvatarColor(i);
            var hex   = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            var init  = GetInitials(app.Name).Substring(0, 1);
                        sb.Append($"<a class='card' href='{System.Net.WebUtility.HtmlEncode(app.Url)}'>")
              .Append($"<div class='pill' style='background:{hex}'>{System.Net.WebUtility.HtmlEncode(init)}</div>")
                            .Append("<div class='name'>")
              .Append(System.Net.WebUtility.HtmlEncode(app.Name))
                            .Append("</div></a>");
        }
        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    // ─── Barre de navigation ─────────────────────────────────────────────────

    private void HookNavigationEvents(WebView2 webView)
    {
        if (webView.CoreWebView2 is null) return;

        webView.CoreWebView2.SourceChanged       -= OnSourceChanged;
        webView.CoreWebView2.HistoryChanged      -= OnHistoryChanged;
        webView.CoreWebView2.NavigationStarting  -= OnNavigationStarting;
        webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        webView.CoreWebView2.DocumentTitleChanged -= OnDocumentTitleChanged;

        webView.CoreWebView2.SourceChanged       += OnSourceChanged;
        webView.CoreWebView2.HistoryChanged      += OnHistoryChanged;
        webView.CoreWebView2.NavigationStarting  += OnNavigationStarting;
        webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        webView.CoreWebView2.DocumentTitleChanged += OnDocumentTitleChanged;

        UpdateNavBar(webView);
    }

    private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        if (sender is not CoreWebView2 core) return;
        Dispatcher.Invoke(() =>
        {
            var src = core.Source ?? string.Empty;
            // Ne pas afficher l'URL "about:blank" interne de la page d'accueil
            if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || src == "about:blank")
                AddressBar.Text = string.Empty;
            else
                AddressBar.Text = src;

            if (_activeTab is { IsHome: false } tab)
                tab.Url = src;
        });
    }

    private void OnHistoryChanged(object? sender, object e)
    {
        if (BrowserHost.Content is WebView2 webView)
            Dispatcher.Invoke(() =>
            {
                BackButton.IsEnabled    = webView.CoreWebView2?.CanGoBack    ?? false;
                ForwardButton.IsEnabled = webView.CoreWebView2?.CanGoForward ?? false;
            });
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshIcon.Text      = "✕";
            LoadingBar.Visibility = Visibility.Visible;
        });
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshIcon.Text      = "↻";
            LoadingBar.Visibility = Visibility.Collapsed;
            if (BrowserHost.Content is WebView2 webView)
                UpdateNavBar(webView);
        });
    }

    private void OnDocumentTitleChanged(object? sender, object e)
    {
        if (sender is not CoreWebView2 core || _activeTab is null) return;
        Dispatcher.Invoke(() =>
        {
            var t = core.DocumentTitle;
            if (!string.IsNullOrWhiteSpace(t))
            {
                _activeTab.Title = t;
                RebuildTabsBar();
            }
        });
    }

    private void UpdateNavBar(WebView2 webView)
    {
        var src = webView.CoreWebView2?.Source ?? string.Empty;
        AddressBar.Text         = (src == "about:blank" || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                                  ? string.Empty : src;
        BackButton.IsEnabled    = webView.CoreWebView2?.CanGoBack    ?? false;
        ForwardButton.IsEnabled = webView.CoreWebView2?.CanGoForward ?? false;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserHost.Content is WebView2 wv) wv.CoreWebView2?.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserHost.Content is WebView2 wv) wv.CoreWebView2?.GoForward();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (BrowserHost.Content is WebView2 wv)
        {
            if (RefreshIcon.Text == "✕") wv.CoreWebView2?.Stop();
            else                         wv.CoreWebView2?.Reload();
        }
    }

    private void AddressBar_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (BrowserHost.Content is not WebView2 webView) return;

        var input = AddressBar.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;
        if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
         && !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            input = "https://" + input;

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            if (_activeTab is not null) _activeTab.IsHome = false;
            webView.Source = uri;
        }
    }

    private void AddressBar_GotFocus(object sender, RoutedEventArgs e)
    {
        AddressBar.SelectAll();
        AddressBarBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
        AddressBarBorder.Background  = new SolidColorBrush(Colors.White);
    }

    private void AddressBar_LostFocus(object sender, RoutedEventArgs e)
    {
        AddressBarBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE2, 0xE8));
        AddressBarBorder.Background  = new SolidColorBrush(Color.FromRgb(0xF3, 0xF5, 0xF8));
    }

    // ─── Défilement horizontal de la barre d'onglets à la molette ───────────

    private void TabsScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    // ─── Déconnexion / fermeture ─────────────────────────────────────────────

    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        ConnectionLogService.LogLogout(_authenticatedUser.SamAccountName);
        RequestedLogout = true;
        Close();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDisposing) return;
        _isDisposing = true;
        Loaded  -= MainWindow_Loaded;
        Closing -= MainWindow_Closing;

        if (!RequestedLogout)
            ConnectionLogService.LogWindowClosed(_authenticatedUser.SamAccountName);

        _webViewManager.Dispose();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static string GetInitials(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";
        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{char.ToUpper(parts[0][0])}{char.ToUpper(parts[^1][0])}"
            : char.ToUpper(displayName[0]).ToString();
    }

    private static Color AvatarColor(int index)
    {
        var palette = new[]
        {
            Color.FromRgb(0x0F, 0x4C, 0x8C),
            Color.FromRgb(0x2E, 0xA0, 0x43),
            Color.FromRgb(0xE0, 0x1B, 0x0D),
            Color.FromRgb(0xC2, 0x41, 0x0E),
            Color.FromRgb(0x6F, 0x42, 0xC1),
            Color.FromRgb(0x0E, 0x86, 0xAB),
            Color.FromRgb(0xD8, 0x3A, 0x9C),
            Color.FromRgb(0x4E, 0x5D, 0x6C)
        };
        return palette[Math.Abs(index) % palette.Length];
    }
}
