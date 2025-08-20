using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace api.Configurations
{
    public static class JwtBearerConfiguration
    {
        private static readonly string[] ClaimsToLog = new string[]
        {
            "aud", // Audience
            "exp", // Expiry
            "oid", // Object ID
            "scp", // Scopes
            "roles", // Roles
            "ver", // Access token version
        };

        public class JwtBearerEvents { }

        public static IServiceCollection ConfigureJwtBearerLogging(this IServiceCollection services)
        {
            services.Configure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Events =
                        new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                        {
                            OnAuthenticationFailed = context =>
                            {
                                var logger = context.HttpContext.RequestServices.GetRequiredService<
                                    ILogger<JwtBearerEvents>
                                >();
                                var token = context
                                    .Request.Headers["Authorization"]
                                    .ToString()
                                    ?.Replace("Bearer ", "");

                                if (!string.IsNullOrEmpty(token))
                                {
                                    try
                                    {
                                        var parts = token.Split('.');
                                        if (parts.Length == 3)
                                        {
                                            string payloadJson = DecodeBase64(parts[1]);
                                            var extractedClaims = ExtractClaimsToLog(payloadJson);

                                            logger.LogError(
                                                "Authentication failed: Token claims: {Claims}",
                                                JsonSerializer.Serialize(extractedClaims)
                                            );
                                        }
                                        else
                                        {
                                            logger.LogWarning("Invalid token format.");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError("Failed to decode token: {Exception}", ex);
                                    }
                                }
                                else
                                {
                                    logger.LogError(
                                        "Authentication failed: {Exception}. No token provided.",
                                        context.Exception
                                    );
                                }

                                return Task.CompletedTask;
                            },
                        };
                }
            );

            return services;
        }

        private static Dictionary<string, object> ExtractClaimsToLog(string payloadJson)
        {
            var result = new Dictionary<string, object>();

            try
            {
                var payloadObj = JsonSerializer.Deserialize<JsonElement>(payloadJson);

                foreach (var claimKey in ClaimsToLog)
                {
                    if (payloadObj.TryGetProperty(claimKey, out JsonElement value))
                    {
                        switch (value.ValueKind)
                        {
                            case JsonValueKind.Array:
                                var array = new List<string>();
                                foreach (var item in value.EnumerateArray())
                                {
                                    array.Add(item.ToString());
                                }
                                result[claimKey] = array;
                                break;
                            default:
                                result[claimKey] = value.ToString();
                                break;
                        }
                    }
                }
            }
            catch
            {
                result["error"] = "Failed to parse JWT payload";
            }

            return result;
        }

        public static string DecodeBase64(string base64)
        {
            base64 = base64.Replace('-', '+').Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "[Invalid Base64]";
            }
        }
    }
}
