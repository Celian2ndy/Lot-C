using Kings.Cloud.Api.Configuration;
using Kings.Cloud.Api.Data;
using Kings.Cloud.Api.Packs;
using Kings.Cloud.Api.Security;
using Kings.Cloud.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Erreurs de validation/binding ([ApiController]) au format du contrat { errorCode, message }.
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = _ =>
        new ObjectResult(new { errorCode = "ERR_VALIDATION", message = "Requête invalide." })
        { StatusCode = StatusCodes.Status400BadRequest };
});

builder.Services.AddDbContext<KingsCloudDbContext>(options =>
    options.UseNpgsql(DatabaseConnection.Resolve(builder.Configuration)));

builder.Services.AddSingleton<LeaderboardScoring>();
builder.Services.AddSingleton<PackSigner>();
builder.Services.AddSingleton<IdentityHasher>();

builder.Services
    .AddAuthentication(SessionTokenAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionTokenAuthenticationHandler>(
        SessionTokenAuthenticationHandler.SchemeName, _ => { });

// Secure-by-default : tout endpoint exige un utilisateur authentifié sauf [AllowAnonymous] explicite.
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

// Filet anti-500 nu : toute exception non gérée -> réponse au format du contrat { errorCode, message }.
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    ctx.Response.ContentType = "application/json";
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(ex, "Erreur non gérée sur {Path}", ctx.Request.Path);
    await ctx.Response.WriteAsJsonAsync(new { errorCode = "ERR_INTERNAL", message = "Erreur interne." });
}));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Dev : applique les migrations + seed idempotent (compte/licence + pack de test).
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KingsCloudDbContext>();
    var signer = scope.ServiceProvider.GetRequiredService<PackSigner>();
    var identityHasher = scope.ServiceProvider.GetRequiredService<IdentityHasher>();
    await db.Database.MigrateAsync();
    await DevSeeder.SeedAsync(db, signer, identityHasher);
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposé pour les tests d'intégration (WebApplicationFactory).
public partial class Program { }
