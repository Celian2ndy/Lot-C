using Kings.Cloud.Api.Configuration;
using Kings.Cloud.Api.Data;
using Kings.Cloud.Api.Security;
using Kings.Cloud.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<KingsCloudDbContext>(options =>
    options.UseNpgsql(DatabaseConnection.Resolve(builder.Configuration)));

builder.Services.AddSingleton<LeaderboardScoring>();

builder.Services
    .AddAuthentication(SessionTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionTokenAuthenticationHandler>(
        SessionTokenAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Dev : applique les migrations + seed idempotent (compte/licence de test).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KingsCloudDbContext>();
    await db.Database.MigrateAsync();
    await DevSeeder.SeedAsync(db);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposé pour les tests d'intégration (WebApplicationFactory).
public partial class Program { }
