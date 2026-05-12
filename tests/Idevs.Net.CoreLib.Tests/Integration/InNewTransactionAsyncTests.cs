using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Pins the independent-commit semantics of <c>InNewTransactionAsync</c>: work
/// committed inside the helper survives an outer rollback; an exception
/// thrown inside rolls back only the inner write, never the outer one.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class InNewTransactionAsyncTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly TestRepository _repo;
    private static readonly IntegrationTestRow.RowFields Fld = IntegrationTestRow.Fields;

    private sealed class TestRepository(ISqlConnections sqlConnections)
        : RowRepositoryBase<IntegrationTestRow, int>(sqlConnections)
    {
        public Task<int> CreateInNewTxAsync(IntegrationTestRow row, CancellationToken ct = default) =>
            InNewTransactionAsync(async (uow, token) =>
                (int)await CreateAsync(row, uow, token), ct);

        public new Task<T> InNewTransactionAsync<T>(
            Func<IUnitOfWork, CancellationToken, Task<T>> work,
            CancellationToken ct = default) =>
            base.InNewTransactionAsync(work, ct);
    }

    public InNewTransactionAsyncTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateTestTable();
        _repo = new TestRepository(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateTestTable();

    [Fact]
    public async Task InnerCommits_OuterRollback_LeavesInnerWriteIntact()
    {
        // Outer transaction creates row "A" then calls the helper which
        // creates row "B" in a SEPARATE transaction. Outer throws -> A rolls
        // back, B survives. This is the document-number semantic.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var conn = _fixture.SqlConnections.NewByKey("Default");
            using var outerUow = new UnitOfWork(conn);

            await _repo.CreateAsync(new IntegrationTestRow { Code = "A", Amount = 1m }, outerUow);
            await _repo.CreateInNewTxAsync(new IntegrationTestRow { Code = "B", Amount = 2m });

            throw new InvalidOperationException("simulate outer failure");
            // outerUow disposes without Commit() -> rolls back the A insert.
        });

        // A rolled back, B survived.
        var count = await _repo.CountAsync(_ => { });
        Assert.Equal(1L, count);

        var b = await _repo.TryFirstAsync(q => q.SelectTableFields().Where(Fld.Code == "B"));
        Assert.NotNull(b);

        var a = await _repo.TryFirstAsync(q => q.SelectTableFields().Where(Fld.Code == "A"));
        Assert.Null(a);
    }

    [Fact]
    public async Task InnerThrows_RollsBackInnerOnly()
    {
        await _repo.CreateAsync(new IntegrationTestRow { Code = "Existing", Amount = 100m });

        await Assert.ThrowsAsync<DivideByZeroException>(() =>
            _repo.InNewTransactionAsync<int>(async (uow, ct) =>
            {
                await _repo.CreateAsync(
                    new IntegrationTestRow { Code = "ShouldRollback", Amount = 0m },
                    uow, ct);
                throw new DivideByZeroException("simulated mid-tx failure");
            }));

        // Inner write rolled back.
        var rb = await _repo.TryFirstAsync(q => q.SelectTableFields().Where(Fld.Code == "ShouldRollback"));
        Assert.Null(rb);

        // Pre-existing row untouched.
        Assert.Equal(1L, await _repo.CountAsync(_ => { }));
    }

    [Fact]
    public async Task InnerCommits_ReturnsValueToOuter()
    {
        var newId = await _repo.InNewTransactionAsync(async (uow, ct) =>
        {
            return (int)await _repo.CreateAsync(
                new IntegrationTestRow { Code = "Returned", Amount = 42m },
                uow, ct);
        });

        Assert.True(newId > 0);
        var row = await _repo.TryFirstAsync(q => q.SelectTableFields().Where(Fld.Code == "Returned"));
        Assert.NotNull(row);
        Assert.Equal(newId, row!.Id);
    }
}
