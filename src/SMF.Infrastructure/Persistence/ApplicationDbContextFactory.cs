using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SMF.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> for migrations/scaffolding.
/// Uses a placeholder SQL Server connection string so the migrations target
/// the real provider, regardless of the runtime configuration (which may use
/// the in-memory fallback for local dev).
/// Override via the SMF_MIGRATIONS_CONNECTION environment variable when needed.
/// </summary>
public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string DefaultDesignTimeConnection =
        "Server=(localdb)\\MSSQLLocalDB;Database=SMF;Trusted_Connection=True;TrustServerCertificate=True;";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SMF_MIGRATIONS_CONNECTION")
            ?? DefaultDesignTimeConnection;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options);
    }
}
