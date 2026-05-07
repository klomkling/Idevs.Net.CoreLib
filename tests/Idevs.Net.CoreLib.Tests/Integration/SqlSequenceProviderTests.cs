using Idevs.Repositories.Sequences;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Integration coverage for <see cref="SqlSequenceProvider"/>: Ensure
/// seeding semantics, NextAsync sequencing, NextRangeAsync atomic block
/// allocation, and missing-key behavior. Concurrent allocation is
/// covered by the dedicated test class so the underlying race is pinned
/// at both the primitive (0.7.6) and helper (0.7.7) layers.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class SqlSequenceProviderTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly SqlSequenceProvider _sequences;

    public SqlSequenceProviderTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateSequencesTable();
        _sequences = new SqlSequenceProvider(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateSequencesTable();

    [Fact]
    public async Task EnsureSequenceAsync_NewKey_CreatesRowWithStartValue()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Test", startValue: 1);

        var first = await _sequences.NextAsync("DocNo:Test");
        Assert.Equal(1L, first);
    }

    [Fact]
    public async Task EnsureSequenceAsync_ExistingKey_PreservesNextValue()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Test", startValue: 1);
        await _sequences.NextAsync("DocNo:Test"); // returns 1, NextValue is now 2
        await _sequences.NextAsync("DocNo:Test"); // returns 2, NextValue is now 3

        // Re-ensure with a different starting value — must NOT reset.
        await _sequences.EnsureSequenceAsync("DocNo:Test", startValue: 999);

        var next = await _sequences.NextAsync("DocNo:Test");
        Assert.Equal(3L, next);
    }

    [Fact]
    public async Task EnsureSequenceAsync_DefaultStartValueIsOne()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Default");
        Assert.Equal(1L, await _sequences.NextAsync("DocNo:Default"));
    }

    [Fact]
    public async Task EnsureSequenceAsync_CustomStartValue_UsedOnFirstNext()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Invoice2026", startValue: 1000);
        Assert.Equal(1000L, await _sequences.NextAsync("DocNo:Invoice2026"));
    }

    [Fact]
    public async Task NextAsync_ReturnsSequentialValues()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Seq", startValue: 10);

        Assert.Equal(10L, await _sequences.NextAsync("DocNo:Seq"));
        Assert.Equal(11L, await _sequences.NextAsync("DocNo:Seq"));
        Assert.Equal(12L, await _sequences.NextAsync("DocNo:Seq"));
    }

    [Fact]
    public async Task NextAsync_DifferentKeys_AreIndependent()
    {
        await _sequences.EnsureSequenceAsync("A", startValue: 1);
        await _sequences.EnsureSequenceAsync("B", startValue: 100);

        Assert.Equal(1L, await _sequences.NextAsync("A"));
        Assert.Equal(100L, await _sequences.NextAsync("B"));
        Assert.Equal(2L, await _sequences.NextAsync("A"));
        Assert.Equal(101L, await _sequences.NextAsync("B"));
    }

    [Fact]
    public async Task NextAsync_MissingKey_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sequences.NextAsync("DoesNotExist"));
        Assert.Contains("EnsureSequenceAsync", ex.Message);
    }

    [Fact]
    public async Task NextAsync_NullOrEmptyKey_Throws()
    {
        // ThrowIfNullOrEmpty throws ArgumentNullException for null and
        // ArgumentException for empty — both derive from ArgumentException.
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _sequences.NextAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _sequences.NextAsync(""));
    }

    [Fact]
    public async Task NextRangeAsync_AllocatesContiguousBlock()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Bulk", startValue: 1);

        var range = await _sequences.NextRangeAsync("DocNo:Bulk", 5);

        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, range);

        // Subsequent NextAsync continues from 6 — the block was reserved.
        Assert.Equal(6L, await _sequences.NextAsync("DocNo:Bulk"));
    }

    [Fact]
    public async Task NextRangeAsync_LargeBlock_AdvancesNextValue()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Bulk2", startValue: 100);

        var range = await _sequences.NextRangeAsync("DocNo:Bulk2", 1000);

        Assert.Equal(1000, range.Count);
        Assert.Equal(100L, range[0]);
        Assert.Equal(1099L, range[^1]);
        Assert.Equal(1100L, await _sequences.NextAsync("DocNo:Bulk2"));
    }

    [Fact]
    public async Task NextRangeAsync_NonPositiveCount_Throws()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Bulk3");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _sequences.NextRangeAsync("DocNo:Bulk3", 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _sequences.NextRangeAsync("DocNo:Bulk3", -1));
    }

    [Fact]
    public async Task NextRangeAsync_MissingKey_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sequences.NextRangeAsync("DoesNotExist", 5));
        Assert.Contains("EnsureSequenceAsync", ex.Message);
    }

    /// <summary>
    /// 50 concurrent allocators against the same sequence. Pins the race
    /// fix at the higher level — duplicates would mean the underlying
    /// 0.7.6 ForUpdate + InNewTransactionAsync isn't being used correctly.
    /// </summary>
    [Fact]
    public async Task FiftyConcurrentAllocators_ProduceDistinctSequentialValues()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Concurrent", startValue: 1);

        const int n = 50;
        var tasks = Enumerable.Range(0, n)
            .Select(_ => _sequences.NextAsync("DocNo:Concurrent"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(n, results.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, n).Select(i => (long)i).OrderBy(x => x),
            results.OrderBy(x => x));
    }

    /// <summary>
    /// Outer business transaction throws AFTER allocation. The allocated
    /// number must remain (committed independently). This is the
    /// deliberate trade-off — gaps in document numbers are normal,
    /// duplicates are catastrophic.
    /// </summary>
    [Fact]
    public async Task OuterRollback_DoesNotRollBackAllocatedValue()
    {
        await _sequences.EnsureSequenceAsync("DocNo:Outer", startValue: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var conn = _fixture.SqlConnections.NewByKey("Default");
            using var outerUow = new UnitOfWork(conn);

            // Allocate inside an outer transaction that subsequently throws.
            var allocated = await _sequences.NextAsync("DocNo:Outer");
            Assert.Equal(1L, allocated);

            throw new InvalidOperationException("simulate outer failure");
            // outerUow disposes without Commit() -> outer state rolls back,
            // but the sequence allocation is in its own transaction.
        });

        // The next allocation continues from 2, not 1 — proving the
        // earlier allocation survived the outer rollback.
        Assert.Equal(2L, await _sequences.NextAsync("DocNo:Outer"));
    }
}
