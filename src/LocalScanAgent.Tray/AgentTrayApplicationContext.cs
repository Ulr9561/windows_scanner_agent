using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
namespace LocalScanAgent.Tray;

[SupportedOSPlatform("windows")]
internal sealed class AgentTrayApplicationContext : ApplicationContext
{
    private static readonly Uri HealthUri = ResolveHealthUri();

    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _startItem;
    private readonly ToolStripMenuItem _restartItem;
    private readonly ToolStripMenuItem _stopItem;
    private readonly ToolStripMenuItem _openInstallFolderItem;
    private readonly ToolStripMenuItem _openLogsItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly System.Windows.Forms.Timer _statusTimer;

    private bool _refreshInProgress;

    public AgentTrayApplicationContext()
    {
        _statusItem = new ToolStripMenuItem("Etat: initialisation") { Enabled = false };
        _startItem = new ToolStripMenuItem("Demarrer l'agent");
        _restartItem = new ToolStripMenuItem("Redemarrer l'agent");
        _stopItem = new ToolStripMenuItem("Arreter l'agent");
        _openInstallFolderItem = new ToolStripMenuItem("Ouvrir le dossier de l'agent");
        _openLogsItem = new ToolStripMenuItem("Ouvrir les logs");
        _exitItem = new ToolStripMenuItem("Quitter");

        _startItem.Click += async (_, _) => await SafeRunAsync(() => StartHostAsync(), "Erreur de demarrage");
        _restartItem.Click += async (_, _) => await SafeRunAsync(RestartHostAsync, "Erreur de redemarrage");
        _stopItem.Click += async (_, _) => await SafeRunAsync(() => StopHostAsync(), "Erreur d'arret");
        _openInstallFolderItem.Click += (_, _) => OpenFolder(AppContext.BaseDirectory);
        _openLogsItem.Click += (_, _) => OpenFolder(GetLogsDirectory());
        _exitItem.Click += async (_, _) => await SafeRunAsync(ExitAsync, "Erreur a la fermeture");

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.AddRange(
        [
            _statusItem,
            new ToolStripSeparator(),
            _startItem,
            _restartItem,
            _stopItem,
            new ToolStripSeparator(),
            _openInstallFolderItem,
            _openLogsItem,
            new ToolStripSeparator(),
            _exitItem
        ]);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Local Scan Agent",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += async (_, _) => await SafeRunAsync(RefreshStatusAsync, "Erreur de refresh");

        _statusTimer = new System.Windows.Forms.Timer
        {
            Interval = 5_000
        };
        _statusTimer.Tick += async (_, _) => await SafeRunAsync(RefreshStatusAsync, "Erreur de refresh");

        _ = SafeRunAsync(InitializeAsync, "Erreur d'initialisation");
    }

    private async Task InitializeAsync()
    {
        await StartHostAsync(showNotification: false);
        _statusTimer.Start();
        await RefreshStatusAsync();
    }

    private async Task SafeRunAsync(Func<Task> action, string errorTitle)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _notifyIcon.ShowBalloonTip(4000, errorTitle, ex.Message, ToolTipIcon.Error);
        }
    }

    private async Task StartHostAsync(bool showNotification = true)
    {
        var hostPath = ResolveHostExecutablePath();
        if (hostPath is null)
        {
            UpdateStatus("Executable du host introuvable", hostRunning: false);
            if (showNotification)
            {
                _notifyIcon.ShowBalloonTip(4000, "Local Scan Agent", "Le fichier LocalScanAgent.Host.exe est introuvable.", ToolTipIcon.Error);
            }

            return;
        }

        if (await IsHealthyAsync())
        {
            UpdateStatus("Agent deja demarre", hostRunning: true);
            return;
        }

        if (GetHostProcesses().Length == 0)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = hostPath,
                WorkingDirectory = Path.GetDirectoryName(hostPath)!,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(startInfo);
        }

        var started = await WaitForHealthyAsync(TimeSpan.FromSeconds(15));
        UpdateStatus(started ? "Agent en ligne" : "Demarrage du host en cours", hostRunning: started || GetHostProcesses().Length > 0);

        if (showNotification)
        {
            var title = started ? "Agent demarre" : "Agent en cours de demarrage";
            var message = started
                ? "Le scanner local est pret."
                : "Le host a ete lance mais ne repond pas encore sur /health.";
            _notifyIcon.ShowBalloonTip(3000, title, message, started ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
    }

    private async Task RestartHostAsync()
    {
        await StopHostAsync(showNotification: false);
        await StartHostAsync();
    }

    private async Task StopHostAsync(bool showNotification = true)
    {
        foreach (var process in GetHostProcesses())
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tray] Failed to stop host process: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        UpdateStatus("Agent arrete", hostRunning: false);

        if (showNotification)
        {
            _notifyIcon.ShowBalloonTip(3000, "Agent arrete", "Le host local a ete stoppe.", ToolTipIcon.Info);
        }
    }

    private async Task ExitAsync()
    {
        _statusTimer.Stop();
        await StopHostAsync(showNotification: false);
        ExitThread();
    }

    private async Task RefreshStatusAsync()
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;

        try
        {
            if (await IsHealthyAsync())
            {
                UpdateStatus("Agent en ligne", hostRunning: true);
                return;
            }

            var hasProcess = GetHostProcesses().Length > 0;
            UpdateStatus(hasProcess ? "Host demarre mais /health indisponible" : "Agent arrete", hostRunning: hasProcess);
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync(HealthUri);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WaitForHealthyAsync(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            if (await IsHealthyAsync())
            {
                return true;
            }

            await Task.Delay(750);
        }

        return false;
    }

    private void UpdateStatus(string status, bool hostRunning)
    {
        _statusItem.Text = $"Etat: {status}";
        _notifyIcon.Text = hostRunning ? "Local Scan Agent - en ligne" : "Local Scan Agent - arrete";

        _startItem.Enabled = !hostRunning;
        _stopItem.Enabled = hostRunning;
        _restartItem.Enabled = hostRunning;
    }

    private static string? ResolveHostExecutablePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "host", "LocalScanAgent.Host.exe"),
            Path.Combine(AppContext.BaseDirectory, "LocalScanAgent.Host.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static Process[] GetHostProcesses()
    {
        return Process.GetProcessesByName("LocalScanAgent.Host");
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static string GetLogsDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "host", "logs");
    }

    private static Icon LoadTrayIcon()
    {
        var pngPath = Path.Combine(AppContext.BaseDirectory, "logo.png");
        if (!File.Exists(pngPath))
            return SystemIcons.Application;
        try
        {
            using var bitmap = new Bitmap(pngPath);
            return Icon.FromHandle(bitmap.GetHicon());
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private static Uri ResolveHealthUri()
    {
        const int defaultPort = 18765;
        var port = defaultPort;

        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "host", "appsettings.json");
        if (File.Exists(appSettingsPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
                if (doc.RootElement.TryGetProperty("Agent", out var agent) &&
                    agent.TryGetProperty("Port", out var portElement) &&
                    portElement.TryGetInt32(out var parsed))
                {
                    port = parsed;
                }
            }
            catch
            {
                // Fall back to default port if config is unreadable.
            }
        }

        return new Uri($"http://127.0.0.1:{port}/health");
    }

    protected override void ExitThreadCore()
    {
        _statusTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _httpClient.Dispose();
        base.ExitThreadCore();
    }
}
