using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using ZenQuotesApp.Wpf.Data;
using ZenQuotesApp.Wpf.Services;
using ZenQuotesApp.Wpf.ViewModels;

namespace ZenQuotesApp.Wpf;

public partial class MainWindow : Window
{
    // Глифы окна из шрифта Segoe MDL2 Assets (код-пойнты, чтобы не хранить PUA-символы в исходнике).
    private const int GlyphMaximize = 0xE922;
    private const int GlyphRestore = 0xE923;

    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZenQuotesApp");
        string dbPath = Path.Combine(baseDir, "quotes.db");
        string settingsPath = Path.Combine(baseDir, "settings.json");

        var repository = new QuoteRepository(dbPath);
        var service = new QuoteService(repository, new QuoteApiClient());
        var settingsStore = new SettingsStore(settingsPath);
        _viewModel = new MainViewModel(service, settingsStore);
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // WindowChrome в максимизированном окне залезает за край экрана — компенсируем отступом.
        RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaximizeGlyph.Text = ((char)(WindowState == WindowState.Maximized ? GlyphRestore : GlyphMaximize)).ToString();
    }
}
