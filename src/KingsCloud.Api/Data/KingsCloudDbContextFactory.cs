using Kings.Cloud.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kings.Cloud.Api.Data;

/// <summary>Fabrique design-time pour <c>dotnet ef</c> (migrations) sans démarrer l'application.</summary>
public sealed class KingsCloudDbContextFactory : IDesignTimeDbContextFactory<KingsCloudDbContext>
{
    public KingsCloudDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KingsCloudDbContext>()
            .UseNpgsql(DatabaseConnection.Resolve())
            .Options;
        return new KingsCloudDbContext(options);
    }
}
