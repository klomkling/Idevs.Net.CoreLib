using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Integration coverage for the <see cref="RowVersionAttribute"/>-driven
/// optimistic-concurrency guard on <see cref="RepositoryBase{TRow,TKey}"/>'s
/// three TRow-shaped UpdateAsync overloads. Verifies the happy path,
/// the conflict-detection path, the manual retry loop, and confirms
/// rows without [RowVersion] are unchanged from 0.7.7 behavior.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class OptimisticConcurrencyTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly TestRepository _repo;
    private static readonly VersionedTestRow.RowFields Fld = VersionedTestRow.Fields;

    private sealed class TestRepository(ISqlConnections sqlConnections)
        : RepositoryBase<VersionedTestRow, int>(sqlConnections);

    public OptimisticConcurrencyTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateVersionedTable();
        _repo = new TestRepository(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateVersionedTable();

    private async Task<VersionedTestRow> SeedAsync(string code, decimal amount = 0m)
    {
        var newId = (int)await _repo.CreateAsync(new VersionedTestRow { Code = code, Amount = amount });
        var row = await _repo.GetByIdAsync(newId);
        Assert.NotNull(row);
        return row!;
    }

    // ---------- happy path ----------

    [Fact]
    public async Task UpdateAsync_HappyPath_IncrementsRowVersion()
    {
        var row = await SeedAsync("A", 10m);
        var initialVersion = row.RowVersion!.Value;

        row.Amount = 99m;
        var updated = await _repo.UpdateAsync(row);

        Assert.True(updated);

        // Library wrote the new version back to the in-memory instance.
        Assert.Equal(initialVersion + 1, row.RowVersion);

        // And the database actually advanced.
        var fresh = await _repo.GetByIdAsync(row.Id!.Value);
        Assert.NotNull(fresh);
        Assert.Equal(99m, fresh!.Amount);
        Assert.Equal(initialVersion + 1, fresh.RowVersion);
    }

    [Fact]
    public async Task UpdateAsync_WithFields_IncrementsRowVersion()
    {
        var row = await SeedAsync("B", 5m);
        var initialVersion = row.RowVersion!.Value;

        row.Amount = 50m;
        var updated = await _repo.UpdateAsync(row, [Fld.Amount]);

        Assert.True(updated);
        Assert.Equal(initialVersion + 1, row.RowVersion);

        var fresh = await _repo.GetByIdAsync(row.Id!.Value);
        Assert.Equal(50m, fresh!.Amount);
        Assert.Equal(initialVersion + 1, fresh.RowVersion);
    }

    [Fact]
    public async Task UpdateExcludingAsync_IncrementsRowVersion_EvenIfRowVersionInExcludeList()
    {
        // Excluding RowVersion from the SET list does NOT bypass the guard.
        // The library reapplies the increment regardless.
        var row = await SeedAsync("C", 1m);
        var initialVersion = row.RowVersion!.Value;

        row.Amount = 7m;
        var updated = await _repo.UpdateExcludingAsync(row, [Fld.RowVersion]);

        Assert.True(updated);
        Assert.Equal(initialVersion + 1, row.RowVersion);

        var fresh = await _repo.GetByIdAsync(row.Id!.Value);
        Assert.Equal(7m, fresh!.Amount);
        Assert.Equal(initialVersion + 1, fresh.RowVersion);
    }

    // ---------- conflict detection ----------

    [Fact]
    public async Task UpdateAsync_StaleRowVersion_ThrowsOptimisticConcurrencyException()
    {
        var row = await SeedAsync("Race", 1m);
        var stale = await _repo.GetByIdAsync(row.Id!.Value);   // Both copies have the same RowVersion.
        Assert.NotNull(stale);

        // First caller commits — RowVersion advances by 1.
        row.Amount = 100m;
        Assert.True(await _repo.UpdateAsync(row));

        // Second caller, holding the now-stale version, must conflict.
        stale!.Amount = 200m;
        var ex = await Assert.ThrowsAsync<OptimisticConcurrencyException>(() =>
            _repo.UpdateAsync(stale));

        Assert.Contains("VersionedTestRows", ex.TableName);
        Assert.Equal(row.Id, ex.RowId);
        Assert.Equal(0L, ex.CapturedVersion);   // Both reads got version 0.

        // Database state reflects the FIRST caller's win.
        var fresh = await _repo.GetByIdAsync(row.Id!.Value);
        Assert.Equal(100m, fresh!.Amount);
        Assert.Equal(1L, fresh.RowVersion);
    }

    [Fact]
    public async Task UpdateAsync_FiftyConcurrentUpdaters_ExactlyOneSucceeds()
    {
        var row = await SeedAsync("Stampede", 1m);
        var rowId = row.Id!.Value;

        const int n = 50;

        // Capture 50 stale copies BEFORE any UPDATE runs — without this
        // step the read-then-write pairs interleave such that each task's
        // SELECT picks up the previous task's increment and they all
        // succeed sequentially. With all 50 holding version=0
        // simultaneously, the update phase becomes a real race: exactly
        // one wins, the other 49 must conflict.
        var copies = new VersionedTestRow[n];
        for (var i = 0; i < n; i++)
        {
            copies[i] = (await _repo.GetByIdAsync(rowId))!;
            Assert.Equal(0L, copies[i].RowVersion);
        }

        var tasks = copies.Select(async (copy, i) =>
        {
            copy.Amount = i + 100m;
            try
            {
                await _repo.UpdateAsync(copy);
                return (success: true, conflict: false);
            }
            catch (OptimisticConcurrencyException)
            {
                return (success: false, conflict: true);
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        var successes = results.Count(r => r.success);
        var conflicts = results.Count(r => r.conflict);

        Assert.Equal(1, successes);
        Assert.Equal(n - 1, conflicts);

        var fresh = await _repo.GetByIdAsync(rowId);
        Assert.Equal(1L, fresh!.RowVersion); // exactly one increment
    }

    // ---------- manual retry pattern ----------

    [Fact]
    public async Task ManualRetryPattern_Succeeds()
    {
        var row = await SeedAsync("Retry", 0m);
        var rowId = row.Id!.Value;

        async Task IncrementWithRetryAsync(int rowId, int maxAttempts)
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var fresh = await _repo.GetByIdAsync(rowId);
                fresh!.Amount = (fresh.Amount ?? 0m) + 1m;
                try
                {
                    await _repo.UpdateAsync(fresh);
                    return;
                }
                catch (OptimisticConcurrencyException) when (attempt < maxAttempts)
                {
                    // re-read on next iteration
                }
            }
            throw new InvalidOperationException("Max retries exceeded.");
        }

        // Five concurrent +1 increments; manual retry resolves all conflicts.
        const int callers = 5;
        await Task.WhenAll(Enumerable.Range(0, callers)
            .Select(_ => IncrementWithRetryAsync(rowId, maxAttempts: callers + 2)));

        var fresh = await _repo.GetByIdAsync(rowId);
        Assert.Equal(callers, fresh!.Amount);
        Assert.Equal(callers, fresh.RowVersion);
    }

    // ---------- precondition / misuse ----------

    [Fact]
    public async Task UpdateAsync_NullRowVersion_Throws()
    {
        // Construct a row by hand without reading it — RowVersion is null.
        // The guard refuses to update silently when the captured version is unknown.
        await SeedAsync("NoVersion", 0m);

        var hand = new VersionedTestRow { Id = 1, Code = "NoVersion", Amount = 99m, RowVersion = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.UpdateAsync(hand));

        Assert.Contains("RowVersion", ex.Message);
        Assert.Contains("null", ex.Message);
    }

    // ---------- non-versioned regression ----------

    [Fact]
    public async Task NonVersionedRow_BehaviorUnchanged()
    {
        // IntegrationTestRow has no [RowVersion] — UpdateAsync should
        // behave exactly as in 0.7.7: no exception type changes, no
        // WHERE-RowVersion clause emitted. Quick sanity test against
        // the existing IntegrationTestRow infrastructure.
        var unversionedRepo = new RepositoryBase<IntegrationTestRow, int>(_fixture.SqlConnections);

        var newId = (int)await unversionedRepo.CreateAsync(new IntegrationTestRow { Code = "Plain", Amount = 1m });
        var row = await unversionedRepo.GetByIdAsync(newId);
        row!.Amount = 999m;

        // Should not throw, should not require a [RowVersion]-style guard.
        Assert.True(await unversionedRepo.UpdateAsync(row));

        // Cleanup so the existing IntegrationTestRow tests' baseline isn't polluted.
        _fixture.TruncateTestTable();
    }
}
