using Kings.Cloud.Api.Contracts;
using Kings.Cloud.Api.Data;
using Kings.Cloud.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kings.Cloud.Api.Controllers;

[ApiController]
[Route("v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly KingsCloudDbContext _db;
    private readonly IdentityHasher _identityHasher;

    public AuthController(KingsCloudDbContext db, IdentityHasher identityHasher)
    {
        _db = db;
        _identityHasher = identityHasher;
    }

    /// <summary>Ouvre une session liée à la licence et renvoie un jeton (client unique : le Cœur).</summary>
    [AllowAnonymous]
    [HttpPost("session")]
    public async Task<IActionResult> OpenSession([FromBody] SessionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LicenseKey))
            return Unauthorized(new { errorCode = "ERR_UNAUTHORIZED", message = "Clé de licence manquante." });

        var keyHash = _identityHasher.Hash(req.LicenseKey);
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.LicenseKeyHash == keyHash);

        // Fail-closed : on exige Active ET non expirée (le Status seul ne suffit pas — l'expiration
        // pourrait ne pas avoir basculé l'état).
        var expired = license?.ExpiresAt is { } exp && exp <= DateTimeOffset.UtcNow;
        if (license is null || license.Status != LicenseState.Active || expired)
            return Unauthorized(new { errorCode = "ERR_UNAUTHORIZED", message = "Licence invalide, inactive ou expirée." });

        var token = Hashing.NewToken();
        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TokenHash = Hashing.Sha256Hex(token),   // seul le HACHÉ est stocké
            AccountId = license.AccountId,
            LicenseId = license.Id,
            CreatedAt = now,
            ExpiresAt = now.AddHours(12),
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new SessionResponse(token, session.ExpiresAt));
    }
}
