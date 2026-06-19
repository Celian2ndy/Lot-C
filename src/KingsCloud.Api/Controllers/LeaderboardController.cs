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
        await UpsertEntryAsync(accountId, rc, req.SnapshotId);

        // Rang GLOBAL, avec le MÊME tri/tie-break que GET (score décroissant, puis UpdatedAt croissant).
        var me = await _db.LeaderboardEntries.AsNoTracking().FirstAsync(e => e.AccountId == accountId);
        var rank = await _db.LeaderboardEntries.CountAsync(e =>
            e.RecomputedScore > me.RecomputedScore
            || (e.RecomputedScore == me.RecomputedScore && e.UpdatedAt < me.UpdatedAt)) + 1;

        return Ok(new SubmitResponse(rc.Score, rank));
    }

    /// <summary>Upsert résistant à la concurrence : sur violation d'unicité (double-submit du même
    /// compte sur la 1re soumission), on bascule en mise à jour de l'entrée existante.</summary>
    private async Task UpsertEntryAsync(Guid accountId, RecomputeResult rc, Guid snapshotId)
    {
        var entry = await _db.LeaderboardEntries.FirstOrDefaultAsync(e => e.AccountId == accountId);
        var isNew = entry is null;
        entry ??= new LeaderboardEntry { Id = Guid.NewGuid(), AccountId = accountId };
        if (isNew) _db.LeaderboardEntries.Add(entry);
        Apply(entry, rc, snapshotId);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (isNew)
        {
            _db.Entry(entry).State = EntityState.Detached;
            var existing = await _db.LeaderboardEntries.FirstAsync(e => e.AccountId == accountId);
            Apply(existing, rc, snapshotId);
            await _db.SaveChangesAsync();
        }

        // Seul le score RECALCULÉ serveur est écrit (jamais une valeur cliente).
        static void Apply(LeaderboardEntry e, RecomputeResult rc, Guid snapshotId)
        {
            e.RecomputedScore = rc.Score;
            e.WeightsetVersion = rc.WeightsetVersion;
            e.Tier = rc.Tier;
            e.ConfigHash = rc.ConfigHash;
            e.SnapshotId = snapshotId;
            e.UpdatedAt = DateTimeOffset.UtcNow;
        }
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
