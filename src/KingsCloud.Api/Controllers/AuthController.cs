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
    public AuthController(KingsCloudDbContext db) => _db = db;

    /// <summary>Ouvre une session liée à la licence et renvoie un jeton (client unique : le Cœur).</summary>
    [AllowAnonymous]
    [HttpPost("session")]
    public async Task<IActionResult> OpenSession([FromBody] SessionRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LicenseKey))
            return Unauthorized(new { errorCode = "ERR_UNAUTHORIZED", message = "Clé de licence manquante." });

        var keyHash = Hashing.Sha256Hex(req.LicenseKey);
        var license = await _db.Licenses.FirstOrDefaultAsync(l => l.LicenseKeyHash == keyHash);
        if (license is null || license.Status != LicenseState.Active)
            return Unauthorized(new { errorCode = "ERR_UNAUTHORIZED", message = "Licence invalide ou inactive." });

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
