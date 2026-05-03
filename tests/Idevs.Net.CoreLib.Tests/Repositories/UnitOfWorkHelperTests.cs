using System.Data;
using Idevs.Repositories;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class UnitOfWorkHelperTests
{
    // Test subject that exposes BeginUnitOfWork / CommitOnSuccessAsync via a
    // public surface. Overrides ExecuteAsync isn't needed because the helpers
    // talk to UnitOfWork directly.
    private sealed class TestService : Idevs.Repositories.RepositoryBase<TestSampleRow>
    {
        public TestService(ISqlConnections c) : base(c) { }

        // Re-expose protected helpers for direct testing.
        public new UnitOfWorkScope BeginUnitOfWork(IUnitOfWork? uow = null) =>
            base.BeginUnitOfWork(uow);

        public new Task<T> CommitOnSuccessAsync<T>(
            Func<IUnitOfWork, CancellationToken, Task<T>> work,
            IUnitOfWork? uow = null,
            CancellationToken ct = default) =>
            base.CommitOnSuccessAsync(work, uow, ct);

        public new Task CommitOnSuccessAsync(
            Func<IUnitOfWork, CancellationToken, Task> work,
            IUnitOfWork? uow = null,
            CancellationToken ct = default) =>
            base.CommitOnSuccessAsync(work, uow, ct);
    }

    [Fact]
    public void BeginUnitOfWork_WithCallerUow_ReturnsScopeWrappingIt()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());
        var callerUow = new UnitOfWork(Substitute.For<IDbConnection>());

        using var scope = service.BeginUnitOfWork(callerUow);

        Assert.Same(callerUow, scope.Uow);
        Assert.False(scope.OwnsUnitOfWork);
    }

    [Fact]
    public void BeginUnitOfWork_WithCallerUow_CommitIsNoOp()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());
        var callerConnection = Substitute.For<IDbConnection>();
        var callerUow = new UnitOfWork(callerConnection);

        using (var scope = service.BeginUnitOfWork(callerUow))
        {
            scope.Commit(); // should not affect caller's UoW
        }

        // Caller's connection should NOT have been disposed by the scope.
        callerConnection.DidNotReceive().Dispose();
    }

    [Fact]
    public void BeginUnitOfWork_WithoutUow_OwnsItAndOpensConnection()
    {
        var sqlConnections = Substitute.For<ISqlConnections>();
        var connection = Substitute.For<IDbConnection>();
        sqlConnections.NewByKey(Arg.Any<string>()).Returns(connection);
        var service = new TestService(sqlConnections);

        using (var scope = service.BeginUnitOfWork())
        {
            Assert.True(scope.OwnsUnitOfWork);
            Assert.NotNull(scope.Uow);
        }

        // Owned connection should be disposed when the scope is.
        connection.Received().Dispose();
    }

    [Fact]
    public void BeginUnitOfWork_AfterDispose_CommitThrows()
    {
        var sqlConnections = Substitute.For<ISqlConnections>();
        sqlConnections.NewByKey(Arg.Any<string>()).Returns(Substitute.For<IDbConnection>());
        var service = new TestService(sqlConnections);

        var scope = service.BeginUnitOfWork();
        scope.Dispose();

        Assert.Throws<ObjectDisposedException>(() => scope.Commit());
    }

    [Fact]
    public async Task CommitOnSuccessAsync_WithCallerUow_PassesItThrough()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());
        var callerUow = new UnitOfWork(Substitute.For<IDbConnection>());
        IUnitOfWork? observed = null;

        var result = await service.CommitOnSuccessAsync((u, _) =>
        {
            observed = u;
            return Task.FromResult(42);
        }, callerUow);

        Assert.Equal(42, result);
        Assert.Same(callerUow, observed);
    }

    [Fact]
    public async Task CommitOnSuccessAsync_NullWork_Throws()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CommitOnSuccessAsync<int>(null!));
    }

    [Fact]
    public async Task CommitOnSuccessAsync_NonGeneric_NullWork_Throws()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.CommitOnSuccessAsync((Func<IUnitOfWork, CancellationToken, Task>)null!));
    }

    [Fact]
    public async Task CommitOnSuccessAsync_CancellationRequested_Throws()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CommitOnSuccessAsync(
                (u, _) => Task.FromResult(0),
                uow: new UnitOfWork(Substitute.For<IDbConnection>()),
                ct: cts.Token));
    }

    [Fact]
    public async Task CommitOnSuccessAsync_WorkThrows_RethrowsAndDoesNotSwallow()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());
        var callerUow = new UnitOfWork(Substitute.For<IDbConnection>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitOnSuccessAsync<int>(
                (_, _) => throw new InvalidOperationException("boom"),
                callerUow));
    }

    [Fact]
    public async Task CommitOnSuccessAsync_NonGeneric_RunsAndReturns()
    {
        var service = new TestService(Substitute.For<ISqlConnections>());
        var callerUow = new UnitOfWork(Substitute.For<IDbConnection>());
        var ran = false;

        await service.CommitOnSuccessAsync((u, _) =>
        {
            ran = true;
            return Task.CompletedTask;
        }, callerUow);

        Assert.True(ran);
    }
}
