using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Behavioral integration tests for <c>CountAsync</c> and <c>ExistsAsync</c>
/// against a real SQL Server (Testcontainers). Validates the observable
/// results — counts match the seeded data, existence checks return the
/// expected bool — without inspecting the generated SQL text. If the
/// generated SQL ever became invalid for SQL Server, these tests would
/// fail at execute time.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class RepositoryCountExistsIntegrationTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly TestRepository _repo;

    private sealed class TestRepository(ISqlConnections sqlConnections)
        : RowRepositoryBase<IntegrationTestRow, int>(sqlConnections);

    public RepositoryCountExistsIntegrationTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateTestTable();
        _repo = new TestRepository(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateTestTable();

    private static readonly IntegrationTestRow.RowFields Fld = IntegrationTestRow.Fields;

    private async Task SeedAsync(params (string Code, decimal Amount, string? Status)[] rows)
    {
        foreach (var (code, amount, status) in rows)
            await _repo.CreateAsync(new IntegrationTestRow
            {
                Code = code,
                Amount = amount,
                Status = status,
            });
    }

    // ---- CountAsync ----

    [Fact]
    public async Task CountAsync_EmptyTable_ReturnsZero()
    {
        var n = await _repo.CountAsync(_ => { });
        Assert.Equal(0L, n);
    }

    [Fact]
    public async Task CountAsync_NoConfigure_ReturnsTotalRowCount()
    {
        await SeedAsync(
            ("A", 1m, "Active"),
            ("B", 2m, "Active"),
            ("C", 3m, "Inactive"));

        var n = await _repo.CountAsync(_ => { });
        Assert.Equal(3L, n);
    }

    [Fact]
    public async Task CountAsync_WithWhere_ReturnsMatchingRowCount()
    {
        await SeedAsync(
            ("A", 1m, "Active"),
            ("B", 2m, "Active"),
            ("C", 3m, "Inactive"),
            ("D", 4m, "Active"));

        var active = await _repo.CountAsync(q => q.Where(Fld.Status == "Active"));
        Assert.Equal(3L, active);

        var inactive = await _repo.CountAsync(q => q.Where(Fld.Status == "Inactive"));
        Assert.Equal(1L, inactive);
    }

    [Fact]
    public async Task CountAsync_WithCompositeWhere_ReturnsExactMatchingRowCount()
    {
        await SeedAsync(
            ("A", 1m, "Active"),
            ("B", 5m, "Active"),
            ("C", 5m, "Inactive"),
            ("D", 10m, "Active"));

        var n = await _repo.CountAsync(q => q
            .Where(Fld.Status == "Active" && Fld.Amount >= 5m));

        Assert.Equal(2L, n);
    }

    [Fact]
    public async Task CountAsync_NoMatch_ReturnsZero()
    {
        await SeedAsync(("A", 1m, "Active"));

        var n = await _repo.CountAsync(q => q.Where(Fld.Code == "DOES-NOT-EXIST"));

        Assert.Equal(0L, n);
    }

    [Fact]
    public async Task CountAsync_ReturnsLong_NotInt()
    {
        // Documents the API: COUNT(*) is returned as long to accommodate
        // 64-bit count columns on PostgreSQL/MySQL even though SQL Server's
        // COUNT(*) is 32-bit. Compile-time check via target type:
        await SeedAsync(("A", 1m, "Active"));

        long n = await _repo.CountAsync(_ => { });   // long, not int
        Assert.Equal(1L, n);
    }

    // ---- ExistsAsync ----

    [Fact]
    public async Task ExistsAsync_EmptyTable_ReturnsFalse()
    {
        var ok = await _repo.ExistsAsync(_ => { });
        Assert.False(ok);
    }

    [Fact]
    public async Task ExistsAsync_HasMatchingRow_ReturnsTrue()
    {
        await SeedAsync(("A", 1m, "Active"));

        var ok = await _repo.ExistsAsync(q => q.Where(Fld.Code == "A"));
        Assert.True(ok);
    }

    [Fact]
    public async Task ExistsAsync_NoMatchingRow_ReturnsFalse()
    {
        await SeedAsync(("A", 1m, "Active"));

        var ok = await _repo.ExistsAsync(q => q.Where(Fld.Code == "MISSING"));
        Assert.False(ok);
    }

    [Fact]
    public async Task ExistsAsync_ManyMatchingRows_ReturnsTrue()
    {
        // Even with many matches, ExistsAsync should return true (the LIMIT 1
        // optimization should not introduce false negatives).
        await SeedAsync(
            ("A", 1m, "Active"),
            ("B", 2m, "Active"),
            ("C", 3m, "Active"),
            ("D", 4m, "Active"));

        var ok = await _repo.ExistsAsync(q => q.Where(Fld.Status == "Active"));
        Assert.True(ok);
    }

    [Fact]
    public async Task ExistsAsync_WithCompositeWhere_HonorsAllPredicates()
    {
        await SeedAsync(
            ("A", 1m, "Active"),
            ("B", 5m, "Inactive"));

        // Active AND amount >= 5 → no match
        var ok = await _repo.ExistsAsync(q => q
            .Where(Fld.Status == "Active" && Fld.Amount >= 5m));
        Assert.False(ok);

        // Inactive AND amount >= 5 → matches row B
        ok = await _repo.ExistsAsync(q => q
            .Where(Fld.Status == "Inactive" && Fld.Amount >= 5m));
        Assert.True(ok);
    }
}
