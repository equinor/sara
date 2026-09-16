using Azure.Core;
using Azure.Identity;

namespace api.Database.Context;

internal sealed class AzureCliMigrationAuthentication(TokenCredential credential)
{
    internal static readonly TimeSpan TokenTimeout = TimeSpan.FromSeconds(30);
    private const string PostgresScope = "https://ossrdbms-aad.database.windows.net/.default";

    internal string GetPassword()
    {
        using var timeout = new CancellationTokenSource(TokenTimeout);
        try
        {
            return RequireToken(
                credential.GetToken(new TokenRequestContext([PostgresScope]), timeout.Token)
            );
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                "Design-time Azure CLI PostgreSQL token acquisition timed out; no password fallback."
            );
        }
        catch (AuthenticationFailedException)
        {
            // CLI diagnostics can contain secrets; EF prints exception chains.
            throw new InvalidOperationException(
                "Design-time Azure CLI PostgreSQL token acquisition failed. Check azure/login and the migration tenant; no password fallback."
            );
        }
    }

    internal async ValueTask<string> GetPasswordAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TokenTimeout);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RequireToken(
                await credential.GetTokenAsync(
                    new TokenRequestContext([PostgresScope]),
                    timeout.Token
                )
            );
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "Design-time Azure CLI PostgreSQL token acquisition canceled; no password fallback.",
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException(
                "Design-time Azure CLI PostgreSQL token acquisition timed out; no password fallback."
            );
        }
        catch (AuthenticationFailedException)
        {
            throw new InvalidOperationException(
                "Design-time Azure CLI PostgreSQL token acquisition failed. Check azure/login and the migration tenant; no password fallback."
            );
        }
    }

    private static string RequireToken(AccessToken token)
    {
        if (string.IsNullOrWhiteSpace(token.Token))
            throw new InvalidOperationException(
                "Design-time Azure CLI returned an empty PostgreSQL token; no password fallback."
            );
        return token.Token;
    }
}
