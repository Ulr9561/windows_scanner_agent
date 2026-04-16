using LocalScanAgent.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LocalScanAgent.Infrastructure.Logging;

public sealed class AgentLogger(ILogger<AgentLogger> logger) : IAgentLogger
{
    public void LogInformation(string messageTemplate, params object?[] args)
        => logger.LogInformation(messageTemplate, args);

    public void LogWarning(string messageTemplate, params object?[] args)
        => logger.LogWarning(messageTemplate, args);

    public void LogError(Exception exception, string messageTemplate, params object?[] args)
        => logger.LogError(exception, messageTemplate, args);
}
