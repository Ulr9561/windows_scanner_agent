using LocalScanAgent.Application.Abstractions;

namespace LocalScanAgent.Tests.Stubs;

internal sealed class StubAgentLogger : IAgentLogger
{
    public void LogInformation(string messageTemplate, params object?[] args) { }
    public void LogWarning(string messageTemplate, params object?[] args) { }
    public void LogError(Exception exception, string messageTemplate, params object?[] args) { }
}
