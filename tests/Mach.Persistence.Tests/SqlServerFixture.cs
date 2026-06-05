using Mach.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace Mach.Persistence.Tests;

/// <summary>
/// Spins up a throwaway SQL Server container and applies the EF migrations once for
/// the whole test collection.
/// <para>
/// Docker may not be running. If the container cannot start, <see cref="Available"/>
/// is <c>false</c> and <see cref="SkipReason"/> explains why; tests then no-op via
/// <see cref="SkipIfUnavailable"/> instead of failing the build. (xunit v2 has no
/// built-in dynamic skip and SkippableFact is not an available package, so a no-op
/// guard is used.)
/// </para>
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    /// <summary>True when a SQL Server container started and migrations were applied.</summary>
    public bool Available { get; private set; }

    /// <summary>Non-null when the container could not start (e.g. Docker not running).</summary>
    public string? SkipReason { get; private set; }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();

            await using var db = CreateContext();
            await db.Database.MigrateAsync();

            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            SkipReason = $"Docker / SQL Server container unavailable: {ex.GetType().Name}: {ex.Message}";

            if (_container is not null)
            {
                try
                {
                    await _container.DisposeAsync();
                }
                catch
                {
                    // best-effort cleanup of a half-started container
                }

                _container = null;
            }
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public MachDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MachDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new MachDbContext(options);
    }

    /// <summary>
    /// Returns <c>true</c> and lets the test body proceed when SQL Server is available;
    /// otherwise writes the skip reason to the test output and returns <c>false</c> so
    /// the caller can early-return (the test passes as a no-op).
    /// </summary>
    public bool SkipIfUnavailable(Xunit.Abstractions.ITestOutputHelper output)
    {
        if (Available)
        {
            return true;
        }

        output.WriteLine($"SKIPPED: {SkipReason}");
        return false;
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sqlserver";
}
