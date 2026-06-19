using System.Security.Claims;
using Kings.Cloud.Api.Contracts;
using Kings.Cloud.Api.Data;
using Kings.Cloud.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kings.Cloud.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/license")]
public sealed class LicenseController : ControllerBase
{
    private readonly KingsCloudDbContext _db;
    public LicenseController(KingsCloudDbContext db) => _db = db;

    /// <summary>État de la licence (plan, expiration, tolérance hors-ligne restante).</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var licenseId = Guid.Parse(User.FindFirstValue(SessionTokenAuthenticationHandler.LicenseIdClaim)!);
        var license = await _db.Licenses.AsNoTracking().FirstOrDefaultAsync(l => l.Id == licenseId);
        if (license is null)
            return Unauthorized(new { errorCode = "ERR_UNAUTHORIZED", message = "Licence introuvable." });

        // offlineToleranceRemaining : provisoire — le « restant » exact nécessite un suivi du dernier
        // contrôle en ligne (non modélisé en v1) ; on renvoie la tolérance configurée.
        var dto = new LicenseStatusDto(license.Plan.ToString(), license.ExpiresAt, license.OfflineToleranceDays);
        return Ok(dto);
    }
}
