using Kings.Cloud.Api.Configuration;
using Kings.Cloud.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<KingsCloudDbContext>(options =>
    options.UseNpgsql(DatabaseConnection.Resolve(builder.Configuration)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposé pour les tests d'intégration (WebApplicationFactory).
public partial class Program { }
