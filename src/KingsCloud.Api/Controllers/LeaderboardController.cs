using System.Security.Claims;
using Kings.Cloud.Api.Contracts;
using Kings.Cloud.Api.Data;
using Kings.Cloud.Api.Security;
using Kings.Cloud.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Kings.Cloud.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/leaderboard")]
public sealed class LeaderboardController : ControllerBase
{
    private readonly KingsCloudDbContext _db;
    private readonly LeaderboardScoring _scoring;

    public LeaderboardController(KingsCloudDbContext db, LeaderboardScoring scoring)
    {
        _db = db;
        _scoring = scoring;
    }

    /// <summary>Soumet des métriques brutes ; le serveur RECALCULE le score (anti-triche C3).</summary>
    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitRequest req)
    {
        if (req.RawMetrics.ValueKind != JsonValueKind.Object)
            return BadRequest(new { errorCode = "ERR_INVALID_RAWMETRICS", message = "rawMetrics requis (objet)." });

        RecomputeResult rc;
        try { rc = _scoring.Recompute(req.RawMetrics); }
        catch (InvalidDataException ex) { return BadRequest(new { errorCode = "ERR_INVALID_RAWMETRICS", message = ex.Message }); }

        var accountId = Guid.Parse(User.FindFirstValue(SessionTokenAuthenticationHandler.AccountIdClaim)!);
        var entry = await _db.LeaderboardEntries.FirstOrDefaultAsync(e => e.AccountId == accountId);
        if (entry is null)
        {
            entry = new LeaderboardEntry { Id = Guid.NewGuid(), AccountId = accountId };
            _db.LeaderboardEntries.Add(entry);
        }

        // Seul le score RECALCULÉ serveur est écrit (jamais une valeur cliente).
        entry.RecomputedScore = rc.Score;
        entry.WeightsetVersion = rc.WeightsetVersion;
        entry.Tier = rc.Tier;
        entry.ConfigHash = rc.ConfigHash;
        entry.SnapshotId = req.SnapshotId;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var rank = await _db.LeaderboardEntries.CountAsync(e => e.RecomputedScore > rc.Score) + 1;
        return Ok(new SubmitResponse(rc.Score, rank));
    }

    /// <summary>Lecture du classement (global ou par tier).</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string scope, [FromQuery] string? tier)
    {
        if (scope != "global" && scope != "tier")
            return BadRequest(new { errorCode = "ERR_INVALID_SCOPE", message = "scope doit valoir 'global' ou 'tier'." });

        var query =
            from e in _db.LeaderboardEntries.AsNoTracking()
            join a in _db.Accounts.AsNoTracking() on e.AccountId equals a.Id
            select new { e.RecomputedScore, e.Tier, e.UpdatedAt, a.Display };

        if (scope == "tier" && !string.IsNullOrWhiteSpace(tier))
            query = query.Where(x => x.Tier == tier);

        var ordered = await query
            .OrderByDescending(x => x.RecomputedScore)
            .ThenBy(x => x.UpdatedAt)
            .ToListAsync();

        var result = ordered
            .Select((x, i) => new LeaderboardEntryDto(x.Display, x.RecomputedScore, i + 1, x.Tier))
            .ToList();

        return Ok(result);
    }
}
