using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mach.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c>. Reads the connection string from
/// the <c>SQL_CONNECTION_STRING</c> environment variable, defaulting to LocalDB.
/// </summary>
public sealed class MachDbContextFactory : IDesignTimeDbContextFactory<MachDbContext>
{
    private const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=MachDb;Trusted_Connection=True;MultipleActiveResultSets=true";

    public MachDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING") ?? DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<MachDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(MachDbContextFactory).Assembly.FullName));

        return new MachDbContext(optionsBuilder.Options);
    }
}
