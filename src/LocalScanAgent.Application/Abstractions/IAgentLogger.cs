namespace LocalScanAgent.Application.Abstractions;

public interface IAgentLogger
{
    void LogInformation(string messageTemplate, params object?[] args);

    void LogWarning(string messageTemplate, params object?[] args);

    void LogError(Exception exception, string messageTemplate, params object?[] args);
}
