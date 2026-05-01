using System.Data;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class RepositoryBaseTKeyTests
{
    private sealed class CapturingRepo : Idevs.Repositories.RepositoryBase<TestSampleRow, int>
    {
        public int ExecuteAsyncCallCount { get; private set; }
        public IUnitOfWork? LastUow { get; private set; }
        public CancellationToken LastCt { get; private set; }

        public CapturingRepo(ISqlConnections c) : base(c) { }

        protected override Task<T> ExecuteAsync<T>(
            Func<IDbConnection, CancellationToken, Task<T>> work,
            IUnitOfWork? uow = null,
            CancellationToken ct = default)
        {
            ExecuteAsyncCallCount++;
            LastUow = uow;
            LastCt = ct;
            return Task.FromResult(default(T)!);
        }
    }

    [Fact]
    public async Task GetByIdAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.GetByIdAsync(42);

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_PassesUowAndCancellationToken()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await repo.GetByIdAsync(42, uow, cts.Token);

        Assert.Same(uow, repo.LastUow);
        Assert.Equal(cts.Token, repo.LastCt);
    }

    [Fact]
    public async Task GetByIdsAsync_EmptyInput_DoesNotCallExecuteAsync_ReturnsEmptyList()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        var result = await repo.GetByIdsAsync(Array.Empty<int>());

        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.Equal(0, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task GetByIdsAsync_NullInput_DoesNotCallExecuteAsync_ReturnsEmptyList()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        var result = await repo.GetByIdsAsync(null!);

        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.Equal(0, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task GetByIdsAsync_NonEmpty_DispatchesViaListAsyncWhichDispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.GetByIdsAsync(new[] { 1, 2, 3 });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task UpdateAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var row = new TestSampleRow { Id = 1, Code = "edit" };

        await repo.UpdateAsync(row);

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task UpdateAsync_NullRow_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.UpdateAsync(null!));
    }
}
