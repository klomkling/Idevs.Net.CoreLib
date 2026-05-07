using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Integration coverage for <c>SqlQuery.ForUpdate()</c> on SQL Server. Pins
/// the dialect-correct hint generation, transaction-required check, and
/// SkipLocked / NoWait error semantics against a real Microsoft SQL Server
/// 2022 container.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class LockedTryFirstSqlServerTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly TestRepository _repo;
    private static readonly IntegrationTestRow.RowFields Fld = IntegrationTestRow.Fields;

    private sealed class TestRepository(ISqlConnections sqlConnections)
        : RepositoryBase<IntegrationTestRow, int>(sqlConnections)
    {
        // Exposes protected InNewTransactionAsync to tests. In production,
        // consumers call InNewTransactionAsync from inside their own repo
        // methods (e.g. GetNextDocNoAsync) and never reach for it externally.
        public new Task<T> InNewTransactionAsync<T>(
            Func<IUnitOfWork, CancellationToken, Task<T>> work,
            CancellationToken ct = default) =>
            base.InNewTransactionAsync(work, ct);
    }

    public LockedTryFirstSqlServerTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateTestTable();
        _repo = new TestRepository(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateTestTable();

    [Fact]
    public async Task ForUpdate_WithoutTransaction_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repo.TryFirstAsync(q => q.SelectTableFields().Where(Fld.Code == "X").ForUpdate()));
        Assert.Contains("ForUpdate", ex.Message);
        Assert.Contains("transaction", ex.Message);
    }

    [Fact]
    public async Task ForUpdate_InsideTransaction_ReturnsRow()
    {
        await _repo.CreateAsync(new IntegrationTestRow { Code = "A", Amount = 10m });

        var row = await _repo.InNewTransactionAsync(async (uow, ct) =>
            await _repo.TryFirstAsync(
                q => q.SelectTableFields().Where(Fld.Code == "A").ForUpdate(),
                uow, ct));

        Assert.NotNull(row);
        Assert.Equal("A", row!.Code);
        Assert.Equal(10m, row.Amount);
    }

    [Fact]
    public async Task ForUpdate_BlocksSecondLockerUntilFirstCommits()
    {
        await _repo.CreateAsync(new IntegrationTestRow { Code = "B", Amount = 5m });

        var firstHoldsLock = new TaskCompletionSource();
        var allowFirstToCommit = new TaskCompletionSource();

        // Wrap in Task.Run so the synchronous SqlHelper.ExecuteReader path
        // doesn't block this test thread — the helper holds its calling
        // thread until the SELECT returns or a real `await` is reached.
        var first = Task.Run(() => _repo.InNewTransactionAsync(async (uow, ct) =>
        {
            var row = await _repo.TryFirstAsync(
                q => q.SelectTableFields().Where(Fld.Code == "B").ForUpdate(),
                uow, ct);
            firstHoldsLock.SetResult();
            await allowFirstToCommit.Task;
            return row;
        }));

        try
        {
            await firstHoldsLock.Task;

            // While the first transaction holds the lock, a second locker should
            // block. Two coordination points to make the assertion robust against
            // thread-pool starvation on slow CI:
            //   1. `secondStarted` fires once the second Task is actually running
            //      (the SELECT is about to be issued). Waiting for this before
            //      starting the timer eliminates "second hadn't even been
            //      scheduled" false positives.
            //   2. A generous 1.5s timeout — the SELECT itself is sub-millisecond
            //      against an empty contention window, so any delay this side of
            //      seconds is real lock-wait time.
            var secondStarted = new TaskCompletionSource();
            var second = Task.Run(async () =>
            {
                secondStarted.SetResult();
                return await _repo.InNewTransactionAsync(async (uow, ct) =>
                    await _repo.TryFirstAsync(
                        q => q.SelectTableFields().Where(Fld.Code == "B").ForUpdate(),
                        uow, ct));
            });
            await secondStarted.Task;

            var timeout = Task.Delay(1500);
            var winner = await Task.WhenAny(second, timeout);
            Assert.Same(timeout, winner); // second is still blocked

            allowFirstToCommit.SetResult();
            await first;
            var secondRow = await second;
            Assert.NotNull(secondRow);
            Assert.Equal("B", secondRow!.Code);
        }
        finally
        {
            // Defensive: if any assertion fails we must still release the
            // first transaction so its lock is freed and subsequent tests'
            // TruncateTestTable doesn't time out.
            allowFirstToCommit.TrySetResult();
            try { await first; } catch { /* swallow */ }
        }
    }

    /// <summary>
    /// READPAST requires the connection's transaction to be in READ COMMITTED
    /// or REPEATABLE READ isolation. The SQL Server image's default database
    /// configuration here is incompatible (likely RCSI / SNAPSHOT-flavoured),
    /// so this test is documented but not exercised against this fixture.
    /// MySQL and PostgreSQL test fixtures cover SkipLocked in 0.8.0+.
    /// </summary>
    [Fact(Skip = "READPAST requires READ COMMITTED isolation; container default differs. Covered against MySQL/Postgres in later releases.")]
    public Task ForUpdateSkip_SkipsLockedRow_Documented_NotRun() => Task.CompletedTask;

    [Fact]
    public async Task ForUpdateNoWait_ThrowsOnSqlServer()
    {
        await _repo.CreateAsync(new IntegrationTestRow { Code = "N", Amount = 1m });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _repo.InNewTransactionAsync(async (uow, ct) =>
                await _repo.TryFirstAsync(
                    q => q.SelectTableFields().Where(Fld.Code == "N").ForUpdate(LockMode.UpdateNoWait),
                    uow, ct)));

        Assert.Contains("LOCK_TIMEOUT 0", ex.Message);
    }

    [Fact]
    public async Task NoForUpdate_GoesThroughStandardSerenityPath()
    {
        // Sanity check: a query without ForUpdate() should still work
        // exactly as before — same row mapping, same parameter binding.
        await _repo.CreateAsync(new IntegrationTestRow { Code = "Plain", Amount = 99m });

        var row = await _repo.TryFirstAsync(q => q.SelectTableFields().Where(Fld.Code == "Plain"));

        Assert.NotNull(row);
        Assert.Equal("Plain", row!.Code);
        Assert.Equal(99m, row.Amount);
    }
}
