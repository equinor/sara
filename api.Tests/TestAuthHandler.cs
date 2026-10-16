using System.Linq;
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
/// request as an admin unless test-specific roles are supplied in a header.
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
        var roles = Request.Headers.TryGetValue("X-Test-Roles", out var header)
            ? header.ToString().Split(',', System.StringSplitOptions.RemoveEmptyEntries)
            : ["Role.Admin"];
        var claims = new[] { new Claim(ClaimTypes.Name, "Test.User") }.Concat(
            roles.Select(role => new Claim(ClaimTypes.Role, role))
        );
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
