using System.Data;
using Idevs.Repositories;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class UnitOfWorkHelperTests
{
    // Test subject that exposes BeginUnitOfWork / CommitOnSuccessAsync via a
    // public surface. Overriding ExecuteAsync isn't needed because the helpers
    // talk to UnitOfWork directly.
    private sealed class TestService : Idevs.Repositories.RowRepositoryBase<TestSampleRow>
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

    // --- Owned-transaction commit/rollback path ---
    //
    // These tests exercise the path where uow == null, so the helper opens
    // its own connection + UnitOfWork. We substitute IDbConnection +
    // IDbTransaction and assert that BeginTransaction is called once and
    // either Commit (success path) or Rollback (exception path) follows.

    private static (ISqlConnections sqlConnections, IDbConnection connection, IDbTransaction transaction)
        CreateOwnedTransactionFixture()
    {
        var transaction = Substitute.For<IDbTransaction>();
        var connection = Substitute.For<IDbConnection>();
        connection.BeginTransaction().Returns(transaction);
        connection.State.Returns(ConnectionState.Open);
        transaction.Connection.Returns(connection);

        var sqlConnections = Substitute.For<ISqlConnections>();
        sqlConnections.NewByKey(Arg.Any<string>()).Returns(connection);

        return (sqlConnections, connection, transaction);
    }

    [Fact]
    public async Task CommitOnSuccessAsync_OwnedUow_CommitsTransactionOnSuccess()
    {
        var (sqlConnections, connection, transaction) = CreateOwnedTransactionFixture();
        var service = new TestService(sqlConnections);

        var result = await service.CommitOnSuccessAsync(
            (u, _) => Task.FromResult("ok"));

        Assert.Equal("ok", result);
        connection.Received(1).BeginTransaction();
        transaction.Received(1).Commit();
        transaction.DidNotReceive().Rollback();
        connection.Received().Dispose();
    }

    [Fact]
    public async Task CommitOnSuccessAsync_OwnedUow_DoesNotCommitOnException_AndDisposesEverything()
    {
        var (sqlConnections, connection, transaction) = CreateOwnedTransactionFixture();
        var service = new TestService(sqlConnections);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CommitOnSuccessAsync<int>(
                (_, _) => throw new InvalidOperationException("boom")));

        connection.Received(1).BeginTransaction();
        // Commit must NOT be called on the transaction when the work threw.
        transaction.DidNotReceive().Commit();
        // Serenity's UnitOfWork rolls back via transaction.Dispose() (the
        // standard IDbTransaction contract: dispose without commit = rollback),
        // not via an explicit Rollback() call. So we assert dispose chains:
        // transaction disposed + connection disposed.
        transaction.Received().Dispose();
        connection.Received().Dispose();
    }

    [Fact]
    public void BeginUnitOfWork_OwnedUow_DisposeWithoutCommit_DisposesTransactionWithoutCommit()
    {
        var (sqlConnections, connection, transaction) = CreateOwnedTransactionFixture();
        var service = new TestService(sqlConnections);

        using (var scope = service.BeginUnitOfWork())
        {
            Assert.True(scope.OwnsUnitOfWork);
            // Intentionally NOT calling scope.Commit() — leaving the using
            // block without commit must NOT call Commit and must dispose
            // the transaction (which rolls back per IDbTransaction contract).
        }

        connection.Received(1).BeginTransaction();
        transaction.DidNotReceive().Commit();
        transaction.Received().Dispose();
        connection.Received().Dispose();
    }

    [Fact]
    public void BeginUnitOfWork_OwnedUow_ExplicitCommitThenDispose_CommitsExactlyOnce()
    {
        var (sqlConnections, connection, transaction) = CreateOwnedTransactionFixture();
        var service = new TestService(sqlConnections);

        using (var scope = service.BeginUnitOfWork())
        {
            scope.Commit();
            // Calling Commit() again should be a no-op; final Commit count must still be 1.
            scope.Commit();
        }

        transaction.Received(1).Commit();
        connection.Received().Dispose();
    }

    [Fact]
    public void BeginUnitOfWork_BeginTransactionThrows_DisposesConnectionAndPropagates()
    {
        // Reproduces the leak the reviewer flagged: if creating the
        // UnitOfWork (which calls connection.BeginTransaction internally)
        // throws, BeginUnitOfWork must dispose the just-opened connection
        // before propagating, not leak it.
        var connection = Substitute.For<IDbConnection>();
        connection.State.Returns(ConnectionState.Open);
        connection
            .When(c => c.BeginTransaction())
            .Do(_ => throw new InvalidOperationException("transaction boom"));

        var sqlConnections = Substitute.For<ISqlConnections>();
        sqlConnections.NewByKey(Arg.Any<string>()).Returns(connection);
        var service = new TestService(sqlConnections);

        Assert.Throws<InvalidOperationException>(() => service.BeginUnitOfWork());

        // The connection must have been disposed even though we never returned a scope.
        connection.Received(1).Dispose();
    }
}
