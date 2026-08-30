using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using SparkDash.DesktopTile.Core;
using SparkDash.StatusCore;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace SparkDash.DesktopTile;

[SuppressMessage("Maintainability", "CA1515", Justification = "WPF requires the generated window type to remain public.")]
[SuppressMessage("Design", "CA1001", Justification = "Window-owned tray resources are disposed deterministically when the application exits.")]
public partial class MainWindow : Window
{
    private const string ApplicationTitle = "sparkDash Desktop Tile";
    private const string HideTileText = "Hide tile";
    private const string ShowTileText = "Show tile";
    private static readonly Uri DashboardUri = new("http://127.0.0.1:5555/");
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private readonly StatusSummaryClient summaryClient;
    private readonly TileSettingsStore settingsStore;
    private readonly StartupRegistration startupRegistration;
    private readonly TileViewModel viewModel = new();
    private readonly DispatcherTimer refreshTimer;
    private readonly Forms.NotifyIcon notifyIcon;
    private readonly Forms.ContextMenuStrip trayMenu;
    private readonly Forms.ToolStripMenuItem showHideMenuItem;
    private readonly Forms.ToolStripMenuItem topmostMenuItem;
    private readonly Forms.ToolStripMenuItem startupMenuItem;
    private readonly Drawing.Icon trayIcon;
    private int refreshInProgress;
    private bool updatingMenuState;
    private bool exiting;

    [SuppressMessage("Reliability", "CA2000", Justification = "The ContextMenuStrip owns and disposes every item added to its Items collection.")]
    [SuppressMessage("Globalization", "CA1303", Justification = "The initial desktop-tile UI is intentionally English-only.")]
    internal MainWindow(
        StatusSummaryClient summaryClient,
        TileSettingsStore settingsStore,
        StartupRegistration startupRegistration)
    {
        this.summaryClient = summaryClient;
        this.settingsStore = settingsStore;
        this.startupRegistration = startupRegistration;
        InitializeComponent();
        DataContext = viewModel;

        var settings = settingsStore.Load();
        Width = settings.Width;
        Height = settings.Height;
        Topmost = settings.Topmost;
        ApplySavedPosition(settings);

        refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = RefreshInterval,
        };
        refreshTimer.Tick += RefreshTimer_Tick;

        trayIcon = CreateTrayIcon();
        trayMenu = new Forms.ContextMenuStrip();
        showHideMenuItem = new Forms.ToolStripMenuItem(HideTileText);
        showHideMenuItem.Click += (_, _) => Dispatcher.Invoke(ToggleVisibility);
        topmostMenuItem = new Forms.ToolStripMenuItem("Always on top")
        {
            CheckOnClick = true,
            Checked = Topmost,
        };
        topmostMenuItem.CheckedChanged += TopmostMenuItem_CheckedChanged;
        startupMenuItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = startupRegistration.IsEnabled,
        };
        startupMenuItem.CheckedChanged += StartupMenuItem_CheckedChanged;
        var openMenuItem = new Forms.ToolStripMenuItem("Open sparkDash");
        openMenuItem.Click += (_, _) => Dispatcher.Invoke(OpenDashboard);
        var exitMenuItem = new Forms.ToolStripMenuItem("Exit");
        exitMenuItem.Click += (_, _) => Dispatcher.Invoke(ExitApplication);
        trayMenu.Items.AddRange(
        [
            showHideMenuItem,
            topmostMenuItem,
            startupMenuItem,
            new Forms.ToolStripSeparator(),
            openMenuItem,
            exitMenuItem,
        ]);
        notifyIcon = new Forms.NotifyIcon
        {
            Text = ApplicationTitle,
            Icon = trayIcon,
            ContextMenuStrip = trayMenu,
            Visible = true,
        };
        notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ToggleVisibility);
        UpdateTopmostAppearance();
    }

    private static Drawing.Icon CreateTrayIcon()
    {
        var processPath = Environment.ProcessPath;
        return processPath is null
            ? (Drawing.Icon)Drawing.SystemIcons.Application.Clone()
            : Drawing.Icon.ExtractAssociatedIcon(processPath)
                ?? (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
    }

    [SuppressMessage("Reliability", "CA2007", Justification = "The WPF continuation must resume on the dispatcher thread.")]
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        refreshTimer.Start();
        await RefreshAsync();
    }

    [SuppressMessage("Reliability", "CA2007", Justification = "The WPF continuation must resume on the dispatcher thread.")]
    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    [SuppressMessage("Design", "CA1031", Justification = "A refresh failure must leave the desktop tile available with an explicit offline state.")]
    [SuppressMessage("Reliability", "CA2007", Justification = "The WPF continuation must resume on the dispatcher thread.")]
    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref refreshInProgress, 1) != 0)
        {
            return;
        }

        try
        {
            var result = await summaryClient.GetSummaryJsonAsync();
            viewModel.Apply(TileSummaryParser.Parse(result.Json));
        }
        catch (Exception error)
        {
            Trace.TraceError($"Could not refresh the sparkDash desktop tile: {error}");
            viewModel.ApplyUnavailable();
        }
        finally
        {
            Interlocked.Exchange(ref refreshInProgress, 0);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
        SaveSettings();
    }

    private void ResizeGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Clamp(Width + e.HorizontalChange, MinWidth, MaxWidth);
        Height = Math.Clamp(Height + e.VerticalChange, MinHeight, MaxHeight);
    }

    private void ResizeGrip_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        SaveSettings();
    }

    private void TopmostButton_Click(object sender, RoutedEventArgs e)
    {
        SetTopmost(!Topmost);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideTile();
    }

    private void OpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        OpenDashboard();
    }

    private void TopmostMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        if (!updatingMenuState)
        {
            Dispatcher.Invoke(() => SetTopmost(topmostMenuItem.Checked));
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "A startup-registration failure is non-fatal and is reverted in the menu.")]
    private void StartupMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        if (updatingMenuState)
        {
            return;
        }

        try
        {
            startupRegistration.SetEnabled(startupMenuItem.Checked);
            SaveSettings();
        }
        catch (Exception error) when (
            error is SecurityException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            Trace.TraceError($"Could not change desktop tile startup registration: {error}");
            updatingMenuState = true;
            startupMenuItem.Checked = startupRegistration.IsEnabled;
            updatingMenuState = false;
        }
    }

    internal void ShowFromExternalActivation()
    {
        ShowTile();
    }

    internal void PrepareForSystemShutdown()
    {
        exiting = true;
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            HideTile();
        }
        else
        {
            ShowTile();
        }
    }

    [SuppressMessage("Globalization", "CA1303", Justification = "The initial desktop-tile UI is intentionally English-only.")]
    private void ShowTile()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        refreshTimer.Start();
        showHideMenuItem.Text = HideTileText;
        _ = RefreshAsync();
    }

    [SuppressMessage("Globalization", "CA1303", Justification = "The initial desktop-tile UI is intentionally English-only.")]
    private void HideTile()
    {
        SaveSettings();
        refreshTimer.Stop();
        Hide();
        showHideMenuItem.Text = ShowTileText;
    }

    private void SetTopmost(bool value)
    {
        Topmost = value;
        updatingMenuState = true;
        topmostMenuItem.Checked = value;
        updatingMenuState = false;
        UpdateTopmostAppearance();
        SaveSettings();
    }

    private void UpdateTopmostAppearance()
    {
        TopmostButton.Opacity = Topmost ? 1 : 0.5;
        TopmostButton.ToolTip = Topmost ? "Always on top is enabled" : "Always on top is disabled";
    }

    [SuppressMessage("Design", "CA1031", Justification = "Opening the dashboard is optional and must not terminate the tile.")]
    private static void OpenDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DashboardUri.AbsoluteUri)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception error)
        {
            Trace.TraceError($"Could not open the local sparkDash dashboard: {error}");
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "Settings persistence must not prevent the tile from running or exiting.")]
    private void SaveSettings()
    {
        try
        {
            settingsStore.Save(new TileSettings(
                Left,
                Top,
                ActualWidth,
                ActualHeight,
                Topmost,
                startupMenuItem.Checked));
        }
        catch (Exception error)
        {
            Trace.TraceError($"Could not save sparkDash desktop tile settings: {error}");
        }
    }

    private void ApplySavedPosition(TileSettings settings)
    {
        if (settings.Left is not double left ||
            settings.Top is not double top ||
            !IsOnVirtualScreen(left, top, settings.Width, settings.Height))
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
    }

    private static bool IsOnVirtualScreen(double left, double top, double width, double height)
    {
        const double VisibleEdge = 48;
        var right = left + width;
        var bottom = top + height;
        return right >= SystemParameters.VirtualScreenLeft + VisibleEdge &&
            left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - VisibleEdge &&
            bottom >= SystemParameters.VirtualScreenTop + VisibleEdge &&
            top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - VisibleEdge;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!exiting)
        {
            e.Cancel = true;
            HideTile();
            return;
        }

        SaveSettings();
        refreshTimer.Stop();
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        trayMenu.Dispose();
        trayIcon.Dispose();
    }

    private void ExitApplication()
    {
        exiting = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }
}
