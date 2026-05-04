using System.Data;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

/// <summary>
/// Unit tests for the raw-SQL helpers (ExecuteScalarAsync /
/// ExecuteNonQueryAsync) on SqlServiceBase. Verifies dispatch through
/// ExecuteAsync, null-arg guards, and UoW/CT propagation. End-to-end
/// behavior against a real SQL Server lives in
/// <c>RawSqlHelperIntegrationTests</c>.
/// </summary>
public class RawSqlHelperTests
{
    private sealed class TestService : Idevs.Repositories.RepositoryBase<TestSampleRow>
    {
        public int ExecuteAsyncCallCount { get; private set; }
        public IUnitOfWork? LastUow { get; private set; }
        public CancellationToken LastCt { get; private set; }

        public TestService(ISqlConnections c) : base(c) { }

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

        // Re-expose protected helpers for direct testing.
        public new Task<T?> ExecuteScalarAsync<T>(
            string sql,
            IDictionary<string, object?>? parameters = null,
            IUnitOfWork? uow = null,
            CancellationToken ct = default) =>
            base.ExecuteScalarAsync<T>(sql, parameters, uow, ct);

        public new Task<int> ExecuteNonQueryAsync(
            string sql,
            IDictionary<string, object?>? parameters = null,
            IUnitOfWork? uow = null,
            CancellationToken ct = default) =>
            base.ExecuteNonQueryAsync(sql, parameters, uow, ct);
    }

    [Fact]
    public async Task ExecuteScalarAsync_NullSql_Throws()
    {
        var svc = new TestService(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.ExecuteScalarAsync<int>(null!));
    }

    [Fact]
    public async Task ExecuteScalarAsync_DispatchesViaExecuteAsync()
    {
        var svc = new TestService(Substitute.For<ISqlConnections>());

        await svc.ExecuteScalarAsync<int>("SELECT 1");

        Assert.Equal(1, svc.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task ExecuteScalarAsync_PassesUowAndCancellationToken()
    {
        var svc = new TestService(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await svc.ExecuteScalarAsync<int>("SELECT 1", uow: uow, ct: cts.Token);

        Assert.Same(uow, svc.LastUow);
        Assert.Equal(cts.Token, svc.LastCt);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_NullSql_Throws()
    {
        var svc = new TestService(Substitute.For<ISqlConnections>());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.ExecuteNonQueryAsync(null!));
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_DispatchesViaExecuteAsync()
    {
        var svc = new TestService(Substitute.For<ISqlConnections>());

        await svc.ExecuteNonQueryAsync("DELETE FROM x");

        Assert.Equal(1, svc.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_PassesUowAndCancellationToken()
    {
        var svc = new TestService(Substitute.For<ISqlConnections>());
        var uow = new UnitOfWork(Substitute.For<IDbConnection>());
        using var cts = new CancellationTokenSource();

        await svc.ExecuteNonQueryAsync("DELETE FROM x", uow: uow, ct: cts.Token);

        Assert.Same(uow, svc.LastUow);
        Assert.Equal(cts.Token, svc.LastCt);
    }
}
