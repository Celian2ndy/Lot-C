using System.Security.Claims;
using System.Text.Encodings.Web;
using Kings.Cloud.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kings.Cloud.Api.Security;

/// <summary>
/// Authentification par jeton de session lié à la licence (détenu par le Cœur — Lot A). Le jeton
/// présenté en <c>Authorization: Bearer</c> est haché puis comparé aux sessions valides non expirées.
/// Le challenge renvoie un 401 au format du contrat (<c>{ errorCode, message }</c>).
/// </summary>
public sealed class SessionTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SessionToken";

    public const string AccountIdClaim = "accountId";
    public const string LicenseIdClaim = "licenseId";

    public SessionTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (token.Length == 0) return AuthenticateResult.NoResult();

        var hash = Hashing.Sha256Hex(token);
        var db = Context.RequestServices.GetRequiredService<KingsCloudDbContext>();
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == hash);

        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("Jeton absent, invalide ou expiré.");

        var claims = new[]
        {
            new Claim(AccountIdClaim, session.AccountId.ToString()),
            new Claim(LicenseIdClaim, session.LicenseId.ToString()),
        };
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(new { errorCode = "ERR_UNAUTHORIZED", message = "Token absent, invalide ou expiré." });
    }
}
