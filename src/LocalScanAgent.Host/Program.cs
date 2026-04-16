using System.Reflection;
using System.Text.Json.Serialization;
using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Services;
using LocalScanAgent.Contracts;
using LocalScanAgent.Host.Configuration;
using LocalScanAgent.Infrastructure;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

builder.Host.UseSerilog((context, _, configuration) =>
{
    var logPath = context.Configuration["Logging:Path"] ?? "logs/agent-.log";

    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(logPath, rollingInterval: RollingInterval.Day);
});

builder.Services.AddInfrastructure();
builder.Services.AddSingleton<ScanOrchestrator>(serviceProvider =>
{
    var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentOptions>>().Value;

    return new ScanOrchestrator(
        serviceProvider.GetRequiredService<IScanSource>(),
        serviceProvider.GetRequiredService<IPdfService>(),
        serviceProvider.GetRequiredService<IAgentLogger>(),
        agentOptions.AllowOnlyOneScanAtATime);
});

var agentOptions = builder.Configuration.GetSection(AgentOptions.SectionName).Get<AgentOptions>() ?? new AgentOptions();
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

const string CorsPolicyName = "FrontendOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.WebHost.UseUrls($"http://{agentOptions.BindAddress}:{agentOptions.Port}");

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors(CorsPolicyName);

app.MapGet("/health", () =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
    return TypedResults.Ok(new HealthResponse("ok", version, ScannerState.Ready));
});

app.MapGet("/devices", async (ScanOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var devices = await orchestrator.GetDevicesAsync(cancellationToken);
    return TypedResults.Ok(devices);
});

app.MapPost("/scan/pdf", async (ScanPdfRequest request, ScanOrchestrator orchestrator, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await orchestrator.ScanToPdfAsync(request, cancellationToken);
        httpContext.Response.Headers.Append("X-Page-Count", result.PageCount.ToString());

        return Results.File(result.Content, result.ContentType, result.FileName);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        });
    }
    catch (NotSupportedException exception)
    {
        return Results.Problem(statusCode: StatusCodes.Status501NotImplemented, title: exception.Message);
    }
    catch (InvalidOperationException exception)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
    }
});

app.Run();
