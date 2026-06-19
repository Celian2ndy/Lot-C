using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kings.Cloud.Api.Contracts;

// Formes STRICTEMENT conformes à openapi/kings-cloud.openapi.yaml (v1.0.1), noms de champs au champ près.

public sealed record SessionRequest(
    [property: JsonPropertyName("licenseKey")] string LicenseKey);

public sealed record SessionResponse(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);

public sealed record LicenseStatusDto(
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("offlineToleranceRemaining")] int OfflineToleranceRemaining);

public sealed record SubmitRequest(
    [property: JsonPropertyName("snapshotId")] Guid SnapshotId,
    [property: JsonPropertyName("rawMetrics")] JsonElement RawMetrics);

public sealed record SubmitResponse(
    [property: JsonPropertyName("recomputedScore")] int RecomputedScore,
    [property: JsonPropertyName("rank")] int Rank);

public sealed record LeaderboardEntryDto(
    [property: JsonPropertyName("display")] string Display,
    [property: JsonPropertyName("recomputedScore")] int RecomputedScore,
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("tier")] string Tier);

public sealed record PackManifestDto(
    [property: JsonPropertyName("packId")] Guid PackId,
    [property: JsonPropertyName("packVersion")] string PackVersion,
    [property: JsonPropertyName("minAppVersion")] string MinAppVersion,
    [property: JsonPropertyName("weightsetVersion")] string? WeightsetVersion,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("payload")] JsonElement Payload);

public sealed record GdprRequest(
    [property: JsonPropertyName("kind")] string Kind);
