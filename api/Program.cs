using Microsoft.EntityFrameworkCore;
using api.Services;
using Azure.Identity;
using api.Models;
using api.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine($"\nENVIRONMENT IS SET TO '{builder.Environment.EnvironmentName}'\n");

builder.AddAppSettingsEnvironmentVariables();
builder.AddDotEnvironmentVariables(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

if (builder.Configuration.GetSection("KeyVault").GetValue<bool>("UseKeyVault"))
{
    // The ExcludeSharedTokenCacheCredential option is a recommended workaround by Azure for dockerization
    // See https://github.com/Azure/azure-sdk-for-net/issues/17052
    string? vaultUri = builder.Configuration.GetSection("KeyVault")["VaultUri"];
    if (!string.IsNullOrEmpty(vaultUri))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(vaultUri),
            new DefaultAzureCredential(
                new DefaultAzureCredentialOptions
                {
                    ExcludeSharedTokenCacheCredential = true
                }
            )
        );
    }
    else
    {
        Console.WriteLine("NO KEYVAULT IN CONFIG");
    }
}

builder.ConfigureLogger();

builder.Services.Configure<AzureAdOptions>(builder.Configuration.GetSection("AzureAd"));
builder.Services.Configure<BlobOptions>(builder.Configuration.GetSection("Storage"));

builder.Services.AddDbContext<IdaDbContext>(opt =>
    opt.UseInMemoryDatabase("TodoList"));

builder.Services.AddScoped<IBlobService, BlobService>();
builder.Services.AddScoped<IInspectionDataService, InspectionDataService>();

builder.Services
    .AddControllers()
    .AddJsonOptions(
        options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        }
    );

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureSwagger(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

builder.Services.AddAuthorizationBuilder().AddFallbackPolicy(
    "RequireAuthenticatedUser", policy => policy.RequireAuthenticatedUser()
);

var app = builder.Build();

string basePath = builder.Configuration["ApiBaseRoute"] ?? "";
app.UseSwagger(
    c =>
    {
        c.PreSerializeFilters.Add(
            (swaggerDoc, httpReq) =>
            {
                swaggerDoc.Servers =
                [
                    new()
                    {
                        Url = $"https://{httpReq.Host.Value}{basePath}"
                    },
                    new()
                    {
                        Url = $"http://{httpReq.Host.Value}{basePath}"
                    }
                ];
            }
        );
    }
);
app.UseSwaggerUI(
    c =>
    {
        c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
        // The following parameter represents the "audience" of the access token.
        c.OAuthAdditionalQueryStringParams(
            new Dictionary<string, string>
            {
                {
                    "Resource", builder.Configuration["AzureAd:ClientId"] ?? throw new ArgumentException("No Azure Ad ClientId")
                }
            }
        );
        c.OAuthUsePkce();
    }
);

var option = new RewriteOptions();
option.AddRedirect("^$", "swagger");
app.UseRewriter(option);

string[] allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
app.UseCors(
    corsBuilder =>
        corsBuilder
            .WithOrigins(allowedOrigins)
            .SetIsOriginAllowedToAllowWildcardSubdomains()
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
