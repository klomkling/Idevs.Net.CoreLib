using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Behavioral integration tests for the raw-SQL helpers (ExecuteScalarAsync /
/// ExecuteNonQueryAsync) on SqlServiceBase, against a real SQL Server
/// (Testcontainers). Validates observable results — scalar values, affected
/// row counts, parameterized queries — without inspecting the generated SQL.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class RawSqlHelperIntegrationTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly TestService _svc;

    /// <summary>Test subject — re-exposes the protected raw-SQL helpers.</summary>
    private sealed class TestService : RepositoryBase<IntegrationTestRow, int>
    {
        public TestService(ISqlConnections c) : base(c) { }

        public new Task<T?> ExecuteScalarAsync<T>(
            string sql,
            IDictionary<string, object?>? parameters = null,
            IUnitOfWork? uow = null,
            CancellationToken ct = default) =>
            base.ExecuteScalarAsync<T>(sql, parameters, uow, ct);

        public new Task<int> ExecuteNonQueryAsync(
            string sql,
            IDictionary<string, object?>? parameters = null,
            IUnitOfWork? uow = null,
            CancellationToken ct = default) =>
            base.ExecuteNonQueryAsync(sql, parameters, uow, ct);
    }

    public RawSqlHelperIntegrationTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateTestTable();
        _svc = new TestService(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateTestTable();

    private async Task SeedAsync(params (string Code, decimal Amount, string? Status)[] rows)
    {
        foreach (var (code, amount, status) in rows)
            await _svc.CreateAsync(new IntegrationTestRow
            {
                Code = code,
                Amount = amount,
                Status = status,
            });
    }

    // ---- ExecuteScalarAsync ----

    [Fact]
    public async Task ExecuteScalarAsync_LiteralOne_ReturnsOne()
    {
        var n = await _svc.ExecuteScalarAsync<int>("SELECT 1");
        Assert.Equal(1, n);
    }

    [Fact]
    public async Task ExecuteScalarAsync_AggregateOverSeededRows_ReturnsExpectedTotal()
    {
        await SeedAsync(("A", 10m, null), ("B", 20m, null), ("C", 30m, null));

        var sum = await _svc.ExecuteScalarAsync<decimal>(
            "SELECT SUM(Amount) FROM dbo.IntegrationTestRows");

        Assert.Equal(60m, sum);
    }

    [Fact]
    public async Task ExecuteScalarAsync_NoRows_MaxReturnsDefault()
    {
        // MAX over an empty table returns NULL → helper returns default(T).
        var max = await _svc.ExecuteScalarAsync<decimal?>(
            "SELECT MAX(Amount) FROM dbo.IntegrationTestRows");

        Assert.Null(max);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ParameterizedQuery_HonorsParam()
    {
        await SeedAsync(
            ("A", 1m, "Active"),
            ("B", 2m, "Inactive"),
            ("C", 3m, "Active"));

        var activeCount = await _svc.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.IntegrationTestRows WHERE Status = @status",
            new Dictionary<string, object?> { ["@status"] = "Active" });

        Assert.Equal(2, activeCount);
    }

    // ---- ExecuteNonQueryAsync ----

    [Fact]
    public async Task ExecuteNonQueryAsync_DeleteWithParam_ReturnsAffectedRowCount()
    {
        await SeedAsync(
            ("A", 1m, "Stale"),
            ("B", 2m, "Stale"),
            ("C", 3m, "Active"));

        var deleted = await _svc.ExecuteNonQueryAsync(
            "DELETE FROM dbo.IntegrationTestRows WHERE Status = @status",
            new Dictionary<string, object?> { ["@status"] = "Stale" });

        Assert.Equal(2, deleted);

        var remaining = await _svc.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.IntegrationTestRows");
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_UpdateAffectingNothing_ReturnsZero()
    {
        await SeedAsync(("A", 1m, "Active"));

        var updated = await _svc.ExecuteNonQueryAsync(
            "UPDATE dbo.IntegrationTestRows SET Status = 'X' WHERE Code = @code",
            new Dictionary<string, object?> { ["@code"] = "DOES-NOT-EXIST" });

        Assert.Equal(0, updated);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_NoParameters_RunsAndReturnsAffected()
    {
        await SeedAsync(("A", 1m, "Active"), ("B", 2m, "Active"));

        var deleted = await _svc.ExecuteNonQueryAsync(
            "DELETE FROM dbo.IntegrationTestRows");

        Assert.Equal(2, deleted);
    }
}
