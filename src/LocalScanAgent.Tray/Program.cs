using System.Runtime.Versioning;

namespace LocalScanAgent.Tray;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new AgentTrayApplicationContext());
    }
}
