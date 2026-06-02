using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;
using api.Configurations;
using api.Database.Context;
using api.MQTT;
using api.Services;
using api.Services.HostedServices;
using api.Services.ResultHandlers.AnalysisResultHandlers;
using api.Services.ResultHandlers.WorkflowResultHandlers;
using api.Utilities;
using Azure.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"\nENVIRONMENT IS SET TO '{builder.Environment.EnvironmentName}'\n");

builder.AddDotEnvironmentVariables(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

if (builder.Configuration.GetSection("KeyVault").GetValue<bool>("UseKeyVault"))
{
    string? vaultUri = builder.Configuration.GetSection("KeyVault")["VaultUri"];
    if (!string.IsNullOrEmpty(vaultUri))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(vaultUri),
            CustomServiceConfigurations.CreateCredential(builder.Configuration)
        );
    }
    else
    {
        Console.WriteLine("NO KEYVAULT IN CONFIG");
    }
}

// Register the runtime credential (ClientSecret / WorkloadIdentity) as a singleton.
builder.Services.AddSingleton<TokenCredential>(
    CustomServiceConfigurations.CreateRuntimeCredential(builder.Configuration)
);

var applicationName = builder.Configuration["AppName"] ?? "SaraBackend";

builder.ConfigureLogger();

builder.Services.ConfigureDatabase(builder.Configuration);
builder.Services.ConfigureMQTT();

var openTelemetryEnabled = builder.Configuration.GetValue<bool?>("OpenTelemetry:Enabled") ?? false;
var otelActivitySource = new ActivitySource(applicationName);
var otelMeter = new Meter($"{applicationName}.Metrics", "0.0.1");
if (openTelemetryEnabled)
{
    builder.AddCustomOpenTelemetry(otelActivitySource, otelMeter);
}
else
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services.Configure<AzureAdOptions>(builder.Configuration.GetSection("AzureAd"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<EndpointConfig>(builder.Configuration.GetSection("EndpointConfig"));
builder.Services.Configure<AnalysisOptions>(
    builder.Configuration.GetSection(AnalysisOptions.SectionName)
);

builder.Services.AddScoped<IThermalReferenceMetadataService, ThermalReferenceMetadataService>();
builder.Services.AddScoped<IInspectionRecordService, InspectionRecordService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddScoped<IAnalysisGroupService, AnalysisGroupService>();
builder.Services.AddScoped<IAnalysisRunService, AnalysisRunService>();
builder.Services.AddScoped<IMqttPublisherService, MqttPublisherService>();

builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddHttpClient(WorkflowService.ArgoHttpClientName);
builder.Services.AddScoped<ITriggerPayloadEnricher, AnonymizerPayloadEnricher>();
builder.Services.AddScoped<ITriggerPayloadEnricher, ThermalReadingPayloadEnricher>();

// Per-workflow result handlers — fire on each successful Workflow step.
builder.Services.AddScoped<IWorkflowResultHandler, AnonymizerResultHandler>();
builder.Services.AddScoped<IWorkflowResultHandler, CLOEResultHandler>();
builder.Services.AddScoped<IWorkflowResultHandler, FencillaResultHandler>();
builder.Services.AddScoped<IWorkflowResultHandler, ThermalReadingResultHandler>();

// Per-analysis result handlers — fire once per successful AnalysisRun for cross-step
// / aggregate reporting. Interface defined for future use; no implementations
// registered yet, so dispatch is a no-op. Add registrations here when needed:
//   builder.Services.AddScoped<IAnalysisResultHandler, MyAggregateResultHandler>();
builder.Services.AddScoped<IAnalysisTriggerService, AnalysisTriggerService>();
builder.Services.AddScoped<ITimeseriesService, TimeseriesService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAnalysisGroupTimeoutProcessor, AnalysisGroupTimeoutProcessor>();

builder.Services.AddHostedService<MqttEventHandler>();
builder.Services.AddHostedService<MqttService>();
builder.Services.AddHostedService<AnalysisGroupTimeoutService>();

builder
    .Services.AddControllers(options =>
    {
        options.Conventions.Add(new ApiRoutePrefixConvention("api"));
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureSwagger(builder.Configuration);

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.ConfigureJwtBearerLogging();

builder
    .Services.AddAuthorizationBuilder()
    .AddFallbackPolicy("RequireAuthenticatedUser", policy => policy.RequireAuthenticatedUser());

var app = builder.Build();

string basePath = builder.Configuration["ApiBaseRoute"] ?? "";
app.UseSwagger(c =>
{
    var swaggerScheme = builder.Configuration["EndpointConfig:DefaultScheme"] ?? "https";
    swaggerScheme = swaggerScheme.Trim().TrimEnd(':');

    c.PreSerializeFilters.Add(
        (swaggerDoc, httpReq) =>
        {
            swaggerDoc.Servers =
            [
                new() { Url = $"{swaggerScheme}://{httpReq.Host.Value}{basePath}" },
            ];
        }
    );
});
app.UseSwaggerUI(c =>
{
    c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
    // The following parameter represents the "audience" of the access token.
    c.OAuthAdditionalQueryStringParams(
        new Dictionary<string, string>
        {
            {
                "Resource",
                builder.Configuration["AzureAd:ClientId"]
                    ?? throw new ArgumentException("No Azure Ad ClientId")
            },
        }
    );
    c.OAuthUsePkce();
});

var enableFrontend = builder.Configuration.GetValue<bool?>("Frontend:Enabled") ?? true;

if (enableFrontend)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}
else
{
    var option = new RewriteOptions();
    option.AddRedirect("^$", "swagger");
    app.UseRewriter(option);
}

string[] allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
app.UseCors(corsBuilder =>
    corsBuilder
        .WithOrigins(allowedOrigins)
        .SetIsOriginAllowedToAllowWildcardSubdomains()
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

app.MapGet(
        "/api/config",
        (IConfiguration configuration) =>
            new
            {
                AzureAd = new
                {
                    ClientId = configuration["AzureAd:ClientId"] ?? "",
                    TenantId = configuration["AzureAd:TenantId"] ?? "",
                },
                BasePath = (configuration["ApiBaseRoute"] ?? "").TrimEnd('/'),
            }
    )
    .AllowAnonymous();

app.MapGet(
        "/api/config/analyses",
        (Microsoft.Extensions.Options.IOptions<AnalysisOptions> opts) =>
            opts.Value.Analyses.Select(kvp => new
            {
                Name = kvp.Key,
                Workflows = kvp.Value.Workflows,
            })
    )
    .RequireAuthorization();

if (enableFrontend)
{
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var displayUrl = builder.Configuration["DISPLAY_URL"];
    if (!string.IsNullOrEmpty(displayUrl))
    {
        Console.WriteLine($"Now listening on: \x1b[36m{displayUrl}\x1b[0m");
    }
    else
    {
        foreach (var url in app.Urls)
        {
            Console.WriteLine($"Now listening on: \x1b[36m{url}\x1b[0m");
        }
    }
});

app.Run();
