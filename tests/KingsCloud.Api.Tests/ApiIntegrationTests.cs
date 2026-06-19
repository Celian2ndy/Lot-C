using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kings.Cloud.Api.Data;
using Kings.Score.Contracts.Snapshot;
using Kings.Score.Json;
using Kings.Score.Scoring;

namespace KingsCloud.Api.Tests;

public sealed class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ApiIntegrationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Auth_opens_session_and_returns_token()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/session", new { licenseKey = DevSeeder.DevLicenseKey });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.True(body.GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Bad_license_key_is_unauthorized()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/session", new { licenseKey = "NOPE" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401_with_errorCode()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/v1/license/status");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("errorCode").GetString()));
    }

    [Fact]
    public async Task License_status_returns_pro_plan()
    {
        var client = await AuthenticatedClientAsync();
        var resp = await client.GetAsync("/v1/license/status");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pro", body.GetProperty("plan").GetString());
        Assert.True(body.TryGetProperty("offlineToleranceRemaining", out _));
    }

    [Fact]
    public async Task Leaderboard_submit_recomputes_score_and_ignores_client_supplied_score()
    {
        var client = await AuthenticatedClientAsync();
        var input = ApiFixtures.HighEndInput();

        // Score attendu = celui du MÊME moteur sur le même snapshot (preuve anti-triche).
        var snapshot = JsonSerializer.Deserialize<SystemSnapshot>(input.ToJsonString(), KingsJson.Options)!;
        var expected = new ScoreEngine().ComputeCore(snapshot).Global;

        // Le client glisse des valeurs de score BIDON : elles doivent être ignorées.
        var body = new JsonObject
        {
            ["snapshotId"] = input["snapshotId"]!.DeepClone(),
            ["rawMetrics"] = input.DeepClone(),
            ["score"] = 999,
            ["recomputedScore"] = 999,
        };
        var resp = await client.PostAsync("/v1/leaderboard/submit",
            new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var serverScore = result.GetProperty("recomputedScore").GetInt32();

        Assert.Equal(expected, serverScore);   // score recalculé serveur
        Assert.NotEqual(999, serverScore);      // jamais la valeur cliente
        Assert.True(result.GetProperty("rank").GetInt32() >= 1);
    }

    [Fact]
    public async Task Leaderboard_get_returns_recomputed_entry()
    {
        var client = await AuthenticatedClientAsync();
        await SubmitHighEndAsync(client);

        var resp = await client.GetAsync("/v1/leaderboard?scope=global");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var entries = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(entries.GetArrayLength() >= 1);
        var first = entries[0];
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("display").GetString()));
        Assert.InRange(first.GetProperty("recomputedScore").GetInt32(), 0, 100);
        Assert.Equal(1, first.GetProperty("rank").GetInt32());
    }

    [Fact]
    public async Task Packs_latest_returns_signed_manifest()
    {
        var client = await AuthenticatedClientAsync();
        var resp = await client.GetAsync("/v1/packs/latest");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var m = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(m.GetProperty("packId").GetString(), out _));
        Assert.Matches(@"^\d+\.\d+\.\d+$", m.GetProperty("packVersion").GetString()!);
        Assert.Matches(@"^\d+\.\d+\.\d+$", m.GetProperty("minAppVersion").GetString()!);
        Assert.False(string.IsNullOrWhiteSpace(m.GetProperty("signature").GetString()));
        Assert.Equal(JsonValueKind.Object, m.GetProperty("payload").ValueKind);
    }

    [Fact]
    public async Task Packs_by_id_returns_manifest_then_404_for_unknown()
    {
        var client = await AuthenticatedClientAsync();
        var latest = await (await client.GetAsync("/v1/packs/latest")).Content.ReadFromJsonAsync<JsonElement>();
        var id = latest.GetProperty("packId").GetString();

        var ok = await client.GetAsync($"/v1/packs/{id}");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var missing = await client.GetAsync($"/v1/packs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Gdpr_request_is_accepted()
    {
        var client = await AuthenticatedClientAsync();
        var resp = await client.PostAsJsonAsync("/v1/account/gdpr", new { kind = "access" });
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    // ----- helpers -----

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/v1/auth/session", new { licenseKey = DevSeeder.DevLicenseKey });
        resp.EnsureSuccessStatusCode();
        var token = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task SubmitHighEndAsync(HttpClient client)
    {
        var input = ApiFixtures.HighEndInput();
        var body = new JsonObject
        {
            ["snapshotId"] = input["snapshotId"]!.DeepClone(),
            ["rawMetrics"] = input.DeepClone(),
        };
        var resp = await client.PostAsync("/v1/leaderboard/submit",
            new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
    }
}
