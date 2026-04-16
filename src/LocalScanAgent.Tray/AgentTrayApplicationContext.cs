using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
namespace LocalScanAgent.Tray;

[SupportedOSPlatform("windows")]
internal sealed class AgentTrayApplicationContext : ApplicationContext
{
    private static readonly Uri HealthUri = new("http://127.0.0.1:18765/health");

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

        _startItem.Click += async (_, _) => await StartHostAsync();
        _restartItem.Click += async (_, _) => await RestartHostAsync();
        _stopItem.Click += async (_, _) => await StopHostAsync();
        _openInstallFolderItem.Click += (_, _) => OpenFolder(AppContext.BaseDirectory);
        _openLogsItem.Click += (_, _) => OpenFolder(GetLogsDirectory());
        _exitItem.Click += async (_, _) => await ExitAsync();

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
            Icon = SystemIcons.Application,
            Text = "Local Scan Agent",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += async (_, _) => await RefreshStatusAsync();

        _statusTimer = new System.Windows.Forms.Timer
        {
            Interval = 5_000
        };
        _statusTimer.Tick += async (_, _) => await RefreshStatusAsync();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await StartHostAsync(showNotification: false);
        _statusTimer.Start();
        await RefreshStatusAsync();
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
            catch
            {
                // Best effort stop for a background process with no main window.
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
        var startedAt = DateTime.UtcNow;

        while (DateTime.UtcNow - startedAt < timeout)
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

    protected override void ExitThreadCore()
    {
        _statusTimer.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _httpClient.Dispose();
        base.ExitThreadCore();
    }
}
