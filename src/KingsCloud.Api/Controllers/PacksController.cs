using System.Text.Json;
using Kings.Cloud.Api.Contracts;
using Kings.Cloud.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kings.Cloud.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/packs")]
public sealed class PacksController : ControllerBase
{
    private readonly KingsCloudDbContext _db;
    public PacksController(KingsCloudDbContext db) => _db = db;

    /// <summary>Dernier pack disponible avec son manifeste signé.</summary>
    [HttpGet("latest")]
    public async Task<IActionResult> Latest()
    {
        var pack = await _db.Packs.AsNoTracking().OrderByDescending(p => p.PublishedAt).FirstOrDefaultAsync();
        return pack is null ? NotFound() : Ok(ToManifest(pack));
    }

    /// <summary>Téléchargement d'un pack par identifiant.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ById(Guid id)
    {
        var pack = await _db.Packs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return pack is null ? NotFound() : Ok(ToManifest(pack));
    }

    private static PackManifestDto ToManifest(Pack p)
    {
        using var doc = JsonDocument.Parse(p.PayloadJson);
        return new PackManifestDto(p.Id, p.Version, p.MinAppVersion, p.WeightsetVersion, p.Signature, doc.RootElement.Clone());
    }
}
