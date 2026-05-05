using System.Reflection;
using System.Text.Json.Serialization;
using LocalScanAgent.Application.Abstractions;
using LocalScanAgent.Application.Exceptions;
using LocalScanAgent.Application.Services;
using LocalScanAgent.Contracts;
using LocalScanAgent.Host.Configuration;
using LocalScanAgent.Infrastructure;
using Microsoft.AspNetCore.Mvc;
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

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ScanOrchestrator>(serviceProvider =>
{
    var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentOptions>>().Value;

    return new ScanOrchestrator(
        serviceProvider.GetRequiredService<IScanSource>(),
        serviceProvider.GetRequiredService<IPdfService>(),
        serviceProvider.GetRequiredService<IAgentLogger>(),
        agentOptions.AllowOnlyOneScanAtATime,
        agentOptions.ScanQueueWaitSeconds,
        agentOptions.ScanTimeoutSeconds);
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

if (corsOptions.AllowedOrigins.Length == 0)
{
    app.Logger.LogWarning(
        "No CORS origins are configured (Cors:AllowedOrigins is empty). " +
        "Browser clients will be blocked by CORS. " +
        "Add the frontend origin to appsettings.json under Cors:AllowedOrigins.");
}

app.UseSerilogRequestLogging();
app.UseCors(CorsPolicyName);

app.MapGet("/health", (ScanOrchestrator orchestrator) =>
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0";
    return TypedResults.Ok(new HealthResponse("ok", version, orchestrator.State));
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
        httpContext.Response.Headers.Append("X-Scan-Mode", request.Mode.ToString());

        return Results.File(result.Content, result.ContentType, result.FileName);
    }
    catch (ArgumentOutOfRangeException exception)
    {
        var validationProblem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            [exception.ParamName ?? "request"] = [exception.Message]
        })
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Requete de scan invalide",
            Detail = exception.Message
        };
        validationProblem.Extensions["errorCode"] = "validation_error";

        return Results.Problem(validationProblem);
    }
    catch (NotSupportedException exception)
    {
        return CreateProblem(StatusCodes.Status501NotImplemented, "Fonction non implemente", exception.Message, "not_implemented");
    }
    catch (InvalidOperationException exception)
    {
        return CreateProblem(StatusCodes.Status409Conflict, "Operation impossible", exception.Message, "invalid_operation");
    }
    catch (ScannerException exception)
    {
        var (statusCode, title) = exception switch
        {
            ScannerFeederEmptyException => (StatusCodes.Status409Conflict, "Chargeur vide"),
            ScannerNotFoundException => (StatusCodes.Status404NotFound, "Scanner introuvable"),
            ScannerUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Scanner indisponible"),
            _ => (StatusCodes.Status422UnprocessableEntity, "Echec de numerisation")
        };

        return CreateProblem(statusCode, title, exception.Message, exception.ErrorCode);
    }
});

app.Run();

static IResult CreateProblem(int statusCode, string title, string detail, string errorCode)
{
    var problemDetails = new ProblemDetails
    {
        Status = statusCode,
        Title = title,
        Detail = detail
    };
    problemDetails.Extensions["errorCode"] = errorCode;

    return Results.Problem(problemDetails);
}
