using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Integration tests for <c>CountAsync</c> and <c>ExistsAsync</c> against a
/// real SQL Server (Testcontainers). Verifies that the SQL emitted matches
/// what the engine expects and that the scalar result is returned correctly.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class RepositoryCountExistsIntegrationTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly TestRepository _repo;

    private sealed class TestRepository(ISqlConnections sqlConnections)
        : RepositoryBase<IntegrationTestRow, int>(sqlConnections);

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
        Assert.Equal(0, n);
    }

    [Fact]
    public async Task CountAsync_NoConfigure_ReturnsTotalRowCount()
    {
        await SeedAsync(
            ("A", 1m, "Active"),
            ("B", 2m, "Active"),
            ("C", 3m, "Inactive"));

        var n = await _repo.CountAsync(_ => { });
        Assert.Equal(3, n);
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
        Assert.Equal(3, active);

        var inactive = await _repo.CountAsync(q => q.Where(Fld.Status == "Inactive"));
        Assert.Equal(1, inactive);
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

        Assert.Equal(2, n);
    }

    [Fact]
    public async Task CountAsync_NoMatch_ReturnsZero()
    {
        await SeedAsync(("A", 1m, "Active"));

        var n = await _repo.CountAsync(q => q.Where(Fld.Code == "DOES-NOT-EXIST"));

        Assert.Equal(0, n);
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
