using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using api.Controllers.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Test;

/// <summary>
/// Stub authentication handler used by integration tests. Authenticates every
/// request as a fixed test user with the <see cref="Role.WorkflowStatusWrite"/>
/// role so authorized controller endpoints can be exercised end-to-end.
/// </summary>
public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "Test.User"),
            new Claim(ClaimTypes.Role, "Role.Admin"),
            new Claim(ClaimTypes.Role, Role.WorkflowStatusWrite),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
