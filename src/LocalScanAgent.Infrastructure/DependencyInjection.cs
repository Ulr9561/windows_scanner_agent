using LocalScanAgent.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LocalScanAgent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IScanSource, Scanning.FakeScanSource>();
        services.AddSingleton<IPdfService, Pdf.PdfService>();
        services.AddSingleton<IAgentLogger, Logging.AgentLogger>();

        return services;
    }
}
