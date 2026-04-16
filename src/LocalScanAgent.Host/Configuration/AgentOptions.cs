using LocalScanAgent.Contracts;

namespace LocalScanAgent.Host.Configuration;

public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    public string BindAddress { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 18765;

    public bool AllowOnlyOneScanAtATime { get; init; } = true;

    public string[] PreferredDriverOrder { get; init; } = ["twain", "wia"];

    public string TempRoot { get; init; } = "temp";

    public ScanMode Mode { get; init; } = ScanMode.Fake;
}
