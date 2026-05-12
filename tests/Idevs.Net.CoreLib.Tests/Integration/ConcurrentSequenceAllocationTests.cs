using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Pins the race condition that motivated <c>SqlQuery.ForUpdate()</c> +
/// <c>InNewTransactionAsync</c>: many concurrent allocators pulling from the
/// same sequence row produce distinct, monotonically-increasing values.
/// Without the lock hint the test would fail intermittently with
/// duplicate / overlapping values.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class ConcurrentSequenceAllocationTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly SequenceRepository _repo;
    private static readonly IntegrationTestRow.RowFields Fld = IntegrationTestRow.Fields;

    /// <summary>
    /// Stand-in for <c>DocumentNumberRepository</c> — uses the same
    /// SELECT-FOR-UPDATE then UPDATE pattern <c>GetNextDocNoAsync</c> needs.
    /// The <c>Code</c> column is the sequence key; <c>Amount</c> holds the
    /// counter value (decimal here, but the pattern is identical for long).
    /// </summary>
    private sealed class SequenceRepository(ISqlConnections sqlConnections)
        : RowRepositoryBase<IntegrationTestRow, int>(sqlConnections)
    {
        public Task<long> AllocateNextAsync(string sequenceKey, CancellationToken ct = default) =>
            InNewTransactionAsync(async (uow, token) =>
            {
                var row = await TryFirstAsync(
                    q => q.SelectTableFields().Where(Fld.Code == sequenceKey).ForUpdate(),
                    uow, token);
                if (row is null)
                    throw new InvalidOperationException($"No sequence row for {sequenceKey}.");

                var next = (long)(row.Amount!.Value + 1);
                row.Amount = next;
                await UpdateAsync(row, uow, token);
                return next;
            }, ct);
    }

    public ConcurrentSequenceAllocationTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateTestTable();
        _repo = new SequenceRepository(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateTestTable();

    [Fact]
    public async Task FiftyConcurrentAllocators_ProduceDistinctValues()
    {
        await _repo.CreateAsync(new IntegrationTestRow { Code = "DOC", Amount = 0m });

        const int n = 50;
        var tasks = Enumerable.Range(0, n)
            .Select(_ => _repo.AllocateNextAsync("DOC"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Every allocator gets a distinct value.
        Assert.Equal(n, results.Distinct().Count());
        // The values cover exactly 1..n (no gaps, no duplicates).
        Assert.Equal(
            Enumerable.Range(1, n).Select(i => (long)i).OrderBy(x => x),
            results.OrderBy(x => x));
    }
}
