using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Serenity.Data;
using Testcontainers.MsSql;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// xUnit collection fixture that spins up a SQL Server 2022 container via
/// Testcontainers, exposes a configured <see cref="ISqlConnections"/> bound
/// to the "Default" key, and creates the test schema once at startup.
/// </summary>
/// <remarks>
/// Requires Docker on the host. CI: GitHub-hosted Linux runners include
/// Docker out of the box. Locally: install Docker Desktop / colima / etc.
/// First run pulls ~2 GB image; subsequent runs are cached.
/// </remarks>
public sealed class MsSqlContainerFixture : IAsyncLifetime
{
    private const string SaPassword = "Strong!Passw0rd123";

    private readonly MsSqlContainer _container;

    public ISqlConnections SqlConnections { get; private set; } = default!;
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Pinned SQL Server image. We deliberately don't use the floating
    /// <c>2022-latest</c> tag — Microsoft retags it on every CU release,
    /// which can change startup behavior or SQL semantics and break CI
    /// without any change in this repo. Bump this constant intentionally
    /// when we want to validate against a newer CU.
    /// </summary>
    private const string MsSqlImage = "mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04";

    public MsSqlContainerFixture()
    {
        _container = new MsSqlBuilder()
            .WithImage(MsSqlImage)
            .WithPassword(SaPassword)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Register the SqlClient factory globally for this AppDomain so that
        // Serenity's DefaultSqlConnections (which goes through
        // DbProviderFactories.GetFactory) can construct connections by
        // provider name. Idempotent — duplicate registration throws, so we
        // only register if not already present.
        if (!DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", out _))
            DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);

        var connectionStringOptions = new ConnectionStringOptions();
        connectionStringOptions["Default"] = new ConnectionStringEntry
        {
            ConnectionString = ConnectionString,
            ProviderName = "Microsoft.Data.SqlClient",
            Dialect = "SqlServer2012"
        };

        var defaultConnectionStrings = new DefaultConnectionStrings(
            Options.Create(connectionStringOptions),
            new DefaultSqlDialectMapper());

        SqlConnections = new DefaultSqlConnections(defaultConnectionStrings);

        // Create schema for the integration test row.
        using var conn = SqlConnections.NewByKey("Default");
        SqlHelper.ExecuteNonQuery(conn, """
            IF OBJECT_ID('dbo.IntegrationTestRows', 'U') IS NOT NULL
                DROP TABLE dbo.IntegrationTestRows;

            CREATE TABLE dbo.IntegrationTestRows (
                Id INT IDENTITY(1,1) PRIMARY KEY,
                Code NVARCHAR(50) NOT NULL,
                Amount DECIMAL(18,4) NOT NULL DEFAULT 0,
                Status NVARCHAR(20) NULL
            );
            """);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Truncate the test table — call between tests for isolation when sharing
    /// the fixture across a class.
    /// </summary>
    public void TruncateTestTable()
    {
        using var conn = SqlConnections.NewByKey("Default");
        SqlHelper.ExecuteNonQuery(conn, "TRUNCATE TABLE dbo.IntegrationTestRows;");
    }
}

/// <summary>
/// xUnit collection definition so multiple integration test classes can share
/// a single container instance (the SQL Server image is large; one start per
/// test session is plenty).
/// </summary>
[CollectionDefinition(nameof(MsSqlContainerCollection))]
public sealed class MsSqlContainerCollection : ICollectionFixture<MsSqlContainerFixture>
{
}
