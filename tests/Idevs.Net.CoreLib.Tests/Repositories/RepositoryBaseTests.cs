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
    public async Task FirstAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.FirstAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
        Assert.Null(repo.LastUow);
    }

    [Fact]
    public async Task FirstAsync_PassesUowAndCancellationToken()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await repo.FirstAsync(_ => { }, uow, cts.Token);

        Assert.Same(uow, repo.LastUow);
        Assert.Equal(cts.Token, repo.LastCt);
    }

    [Fact]
    public async Task ListAsync_DispatchesViaExecuteAsync()
    {
        var repo = new CapturingRepo(Substitute.For<ISqlConnections>());

        await repo.ListAsync(_ => { });

        Assert.Equal(1, repo.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task GetByAsync_DispatchesViaFirstAsync()
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
}
