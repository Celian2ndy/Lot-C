using Kings.Cloud.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kings.Cloud.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/account")]
public sealed class AccountController : ControllerBase
{
    /// <summary>Demande d'accès ou de suppression des données (RGPD). Accusé de réception (202).</summary>
    [HttpPost("gdpr")]
    public IActionResult Gdpr([FromBody] GdprRequest req)
    {
        if (req.Kind != "access" && req.Kind != "delete")
            return BadRequest(new { errorCode = "ERR_INVALID_KIND", message = "kind doit valoir 'access' ou 'delete'." });

        // Le traitement effectif (export/suppression) est un processus humain/asynchrone hors v1.
        // On accuse réception conformément au contrat (202).
        return Accepted();
    }
}
