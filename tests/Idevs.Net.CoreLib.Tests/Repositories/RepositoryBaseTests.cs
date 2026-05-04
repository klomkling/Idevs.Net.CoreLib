using System.Data;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class RepositoryBaseTests
{
    // A test subject that overrides ExecuteAsync so we can verify dispatch
    // without depending on Serenity SQL extension methods over a mock connection.
    private sealed class CapturingRepo : Idevs.Repositories.RepositoryBase<TestSampleRow>
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
            // Return default for the test return type without invoking work.
            return Task.FromResult(default(T)!);
        }
    }

    [Fact]
    public async Task TryFirstAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.TryFirstAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
        Assert.Null(repo.LastUow);
    }

    [Fact]
    public async Task TryFirstAsync_PassesUowAndCancellationToken()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await repo.TryFirstAsync(_ => { }, uow, cts.Token);

        Assert.Same(uow, repo.LastUow);
        Assert.Equal(cts.Token, repo.LastCt);
    }

    [Fact]
    public async Task TryFirstAsync_NullConfigure_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.TryFirstAsync(null!));
    }

    [Fact]
#pragma warning disable CS0618 // intentional: verify deprecated FirstAsync still dispatches via TryFirstAsync
    public async Task FirstAsync_Obsolete_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.FirstAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }
#pragma warning restore CS0618

    [Fact]
    public async Task ListAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.ListAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task CountAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.CountAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task CountAsync_PassesUowAndCancellationToken()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await repo.CountAsync(_ => { }, uow, cts.Token);

        Assert.Same(uow, repo.LastUow);
        Assert.Equal(cts.Token, repo.LastCt);
    }

    [Fact]
    public async Task CountAsync_NullConfigure_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.CountAsync(null!));
    }

    [Fact]
    public async Task ExistsAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.ExistsAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task ExistsAsync_PassesUowAndCancellationToken()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await repo.ExistsAsync(_ => { }, uow, cts.Token);

        Assert.Same(uow, repo.LastUow);
        Assert.Equal(cts.Token, repo.LastCt);
    }

    [Fact]
    public async Task ExistsAsync_NullConfigure_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.ExistsAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.UpdateAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task UpdateAsync_PassesUowAndCancellationToken()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await repo.UpdateAsync(_ => { }, ExpectedRows.Ignore, uow, cts.Token);

        Assert.Same(uow, repo.LastUow);
        Assert.Equal(cts.Token, repo.LastCt);
    }

    [Fact]
    public async Task UpdateAsync_NullConfigure_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.UpdateAsync(null!));
    }

    [Fact]
    public async Task UpdateManyAsync_DispatchesViaUpdateAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.UpdateManyAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task DeleteAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.DeleteAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task DeleteAsync_PassesUowAndCancellationToken()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await repo.DeleteAsync(_ => { }, ExpectedRows.Ignore, uow, cts.Token);

        Assert.Same(uow, repo.LastUow);
        Assert.Equal(cts.Token, repo.LastCt);
    }

    [Fact]
    public async Task DeleteAsync_NullConfigure_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.DeleteAsync(null!));
    }

    [Fact]
    public async Task DeleteManyAsync_DispatchesViaDeleteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.DeleteManyAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task GetByAsync_DispatchesViaTryFirstAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        await repo.GetByAsync(TestSampleRow.Fields.Code, "abc");
        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task GetByAsync_NullField_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repo.GetByAsync<string>(keyField: null!, value: "abc"));
    }

    [Fact]
    public async Task CreateAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var row = new TestSampleRow { Code = "new" };

        await repo.CreateAsync(row);

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task CreateAsync_NullRow_Throws()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.CreateAsync(null!));
    }
}
