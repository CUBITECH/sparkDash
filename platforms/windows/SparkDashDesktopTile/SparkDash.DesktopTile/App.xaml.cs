using SparkDash.DesktopTile.Core;
using SparkDash.StatusCore;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Windows;

namespace SparkDash.DesktopTile;

[SuppressMessage("Maintainability", "CA1515", Justification = "WPF requires the generated application type to remain public.")]
[SuppressMessage("Design", "CA1001", Justification = "Application-owned resources are disposed deterministically in OnExit.")]
public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\sparkDash.DesktopTile";
    private const string ActivationEventName = @"Local\sparkDash.DesktopTile.Activate";
    private Mutex? instanceMutex;
    private EventWaitHandle? activationEvent;
    private RegisteredWaitHandle? activationRegistration;
    private MainWindow? tileWindow;
    private bool ownsMutex;
    private HttpClient? httpClient;

    [SuppressMessage("Reliability", "CA2000", Justification = "HttpClient owns and disposes its handler during application shutdown.")]
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);
        instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out ownsMutex);
        if (!ownsMutex)
        {
            activationEvent.Set();
            activationEvent.Dispose();
            activationEvent = null;
            Shutdown();
            return;
        }

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CheckCertificateRevocationList = true,
            UseProxy = false,
        };
        httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        var summaryClient = new StatusSummaryClient(
            httpClient,
            new Uri("http://127.0.0.1:5555/"),
            TimeSpan.FromSeconds(5));
        var settingsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "sparkDash",
            "desktop-tile.json");
        var settingsStore = new TileSettingsStore(settingsPath);
        var startupRegistration = new StartupRegistration("sparkDash Desktop Tile");

        var window = new MainWindow(summaryClient, settingsStore, startupRegistration);
        tileWindow = window;
        MainWindow = window;
        window.Show();
        activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            ActivationRequested,
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        tileWindow?.PrepareForSystemShutdown();

        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        activationRegistration?.Unregister(waitObject: null);
        activationEvent?.Dispose();
        httpClient?.Dispose();
        if (ownsMutex)
        {
            instanceMutex?.ReleaseMutex();
        }
        instanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void ActivationRequested(object? state, bool timedOut)
    {
        if (timedOut)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => tileWindow?.ShowFromExternalActivation());
    }
}
