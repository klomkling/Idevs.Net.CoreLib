using System.Data;
using Idevs.Repositories;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public class SqlServiceBaseTests
{
    private static ISqlConnections Conns() => Substitute.For<ISqlConnections>();

    private sealed class DefaultSubject(ISqlConnections c) : SqlServiceBase(c);

    [Idevs.Repositories.ConnectionKey("Warehouse")]
    private sealed class AttributedSubject(ISqlConnections c) : SqlServiceBase(c);

    private sealed class OverrideSubject(ISqlConnections c) : SqlServiceBase(c)
    {
        protected override string ConnectionKey => "Reports";
    }

    [Idevs.Repositories.ConnectionKey("Warehouse")]
    private sealed class AttributedAndOverrideSubject(ISqlConnections c) : SqlServiceBase(c)
    {
        protected override string ConnectionKey => "Reports";
    }

    [Fact]
    public void Ctor_NullConnections_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DefaultSubject(null!));
    }

    [Fact]
    public void ConnectionKey_DefaultsToDefault_WhenNoAttributeOrOverride()
    {
        var subject = new DefaultSubject(Conns());
        Assert.Equal("Default", InvokeProtectedConnectionKey(subject));
    }

    [Fact]
    public void ConnectionKey_ReadsFromAttribute_WhenPresent()
    {
        var subject = new AttributedSubject(Conns());
        Assert.Equal("Warehouse", InvokeProtectedConnectionKey(subject));
    }

    [Fact]
    public void ConnectionKey_VirtualOverride_WinsOverAttribute()
    {
        var subject = new AttributedAndOverrideSubject(Conns());
        Assert.Equal("Reports", InvokeProtectedConnectionKey(subject));
    }

    [Fact]
    public void ConnectionKey_VirtualOverride_WinsWithoutAttribute()
    {
        var subject = new OverrideSubject(Conns());
        Assert.Equal("Reports", InvokeProtectedConnectionKey(subject));
    }

    [Fact]
    public void Dialect_IsCachedAfterFirstAccess_ConnectionsCreatedOnce()
    {
        var conns = new TestSqlConnections();
        var subject = new ExposedSubject(conns);

        var first = subject.PublicDialect;
        var second = subject.PublicDialect;

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(1, conns.NewByKeyCallCount);
    }

    private sealed class ExposedSubject(ISqlConnections c) : SqlServiceBase(c)
    {
        public ISqlDialect PublicDialect => Dialect;
    }

    private sealed class TestSqlConnections : ISqlConnections
    {
        public int NewByKeyCallCount { get; private set; }

        public IDbConnection NewByKey(string connectionKey)
        {
            NewByKeyCallCount++;
            var conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            // We don't Open() the connection — Serenity's WrappedConnection returns the
            // pre-supplied SqliteDialect.Instance from GetDialect() without touching the
            // underlying provider, so an unopened SqliteConnection is fine for this test.
            return new WrappedConnection(conn, SqliteDialect.Instance);
        }

        // ISqlConnections — minimal stubs sufficient for the test:
        public IDbConnection New(string connectionString, string providerName, ISqlDialect dialect) =>
            NewByKey("Default");
        public IDbConnection NewFor<TRow>() where TRow : class, IRow => NewByKey("Default");
        public string DefaultDialectKey { get; set; } = "sqlite";
        public IConnectionString? TryGetConnectionString(string connectionKey) =>
            throw new NotImplementedException("Not used by current tests.");
        public IConnectionString GetConnectionString(string connectionKey) =>
            throw new NotImplementedException("Not used by current tests.");
        public IConnectionString GetConnectionStringFor<TRow>() where TRow : class, IRow =>
            throw new NotImplementedException("Not used by current tests.");
        public IEnumerable<IConnectionString> ListConnectionStrings() => [];
        public IConnectionStrings ConnectionStrings =>
            throw new NotImplementedException("Not used by current tests.");
    }

    private static string InvokeProtectedConnectionKey(SqlServiceBase subject)
    {
        var prop = typeof(SqlServiceBase).GetProperty(
            "ConnectionKey",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(prop);
        var value = prop.GetValue(subject);
        Assert.NotNull(value);
        return (string)value;
    }

    [Fact]
    public async Task ExecuteAsync_NoUow_OpensConnection_RunsWork_DisposesConnection()
    {
        var conns = Substitute.For<ISqlConnections>();
        var connection = Substitute.For<IDbConnection>();
        conns.NewByKey("Default").Returns(connection);

        var subject = new ExecutorSubject(conns);

        IDbConnection? capturedConn = null;
        var result = await subject.RunAsync(async (c, _) =>
        {
            capturedConn = c;
            return 42;
        });

        Assert.Equal(42, result);
        Assert.Same(connection, capturedConn);
        connection.Received(1).Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_WithUow_UsesUowConnection_DoesNotDispose()
    {
        var conns = Substitute.For<ISqlConnections>();
        var ownedConnection = Substitute.For<IDbConnection>();
        var uowConnection = Substitute.For<IDbConnection>();
        conns.NewByKey("Default").Returns(ownedConnection);

        var uow = new UnitOfWork(uowConnection);
        var subject = new ExecutorSubject(conns);

        IDbConnection? capturedConn = null;
        await subject.RunAsync(async (c, _) =>
        {
            capturedConn = c;
            return 0;
        }, uow);

        Assert.Same(uowConnection, capturedConn);
        uowConnection.DidNotReceive().Dispose();
        ownedConnection.DidNotReceive().Dispose();
        conns.DidNotReceive().NewByKey(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsBeforeOpeningConnection()
    {
        var conns = Substitute.For<ISqlConnections>();
        var subject = new ExecutorSubject(conns);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            subject.RunAsync(async (_, _) => 0, ct: cts.Token));

        conns.DidNotReceive().NewByKey(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_NullWork_Throws()
    {
        var subject = new ExecutorSubject(Substitute.For<ISqlConnections>());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            subject.RunAsync<int>(work: null!));
    }

    [Fact]
    public async Task ExecuteAsync_WorkThrows_ConnectionStillDisposed_ExceptionPropagates()
    {
        var conns = Substitute.For<ISqlConnections>();
        var connection = Substitute.For<IDbConnection>();
        conns.NewByKey("Default").Returns(connection);

        var subject = new ExecutorSubject(conns);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            subject.RunAsync<int>(async (_, _) => throw new InvalidOperationException("boom")));

        connection.Received(1).Dispose();
    }

    private sealed class ExecutorSubject(ISqlConnections c) : SqlServiceBase(c)
    {
        public Task<T> RunAsync<T>(
            Func<IDbConnection, CancellationToken, Task<T>> work,
            IUnitOfWork? uow = null,
            CancellationToken ct = default)
            => ExecuteAsync(work, uow, ct);
    }
}
