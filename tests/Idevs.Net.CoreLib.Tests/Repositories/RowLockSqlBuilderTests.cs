using Idevs.Repositories;
using NSubstitute;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Repositories;

public sealed class RowLockSqlBuilderTests
{
    private const string SimpleSelect =
        "SELECT t0.* FROM IntegrationTestRows t0 WHERE t0.Id = @p0";
    private const string SelectWithOrder =
        "SELECT t0.* FROM IntegrationTestRows t0 ORDER BY t0.Id DESC";
    private const string SelectWithJoin =
        "SELECT t0.*, t1.Name FROM IntegrationTestRows t0 LEFT JOIN Other t1 ON t1.Id = t0.OtherId WHERE t0.Code = @p0";

    private static ISqlDialect DialectFor(string serverType)
    {
        var d = Substitute.For<ISqlDialect>();
        d.ServerType.Returns(serverType);
        return d;
    }

    // ---------- SqlServer ----------

    [Theory]
    [InlineData("SqlServer2012")]
    [InlineData("SqlServer")]
    [InlineData("MSSQL2008")]
    public void SqlServer_Update_InjectsUpdLockHoldLockRowLock(string serverType)
    {
        var sql = RowLockSqlBuilder.Apply(SimpleSelect, DialectFor(serverType), LockMode.Update);

        Assert.Contains("WITH (UPDLOCK, HOLDLOCK, ROWLOCK)", sql);
        // Hint goes between table and WHERE.
        Assert.True(sql.IndexOf("IntegrationTestRows", StringComparison.Ordinal) <
                    sql.IndexOf("WITH (UPDLOCK", StringComparison.Ordinal));
        Assert.True(sql.IndexOf("WITH (UPDLOCK", StringComparison.Ordinal) <
                    sql.IndexOf("WHERE", StringComparison.Ordinal));
    }

    [Fact]
    public void SqlServer_UpdateSkip_AddsReadpastToTableHint()
    {
        var sql = RowLockSqlBuilder.Apply(SimpleSelect, DialectFor("SqlServer2012"), LockMode.UpdateSkip);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK, ROWLOCK, READPAST)", sql);
    }

    [Fact]
    public void SqlServer_Share_UsesHoldLockOnly()
    {
        var sql = RowLockSqlBuilder.Apply(SimpleSelect, DialectFor("SqlServer2012"), LockMode.Share);
        Assert.Contains("WITH (HOLDLOCK, ROWLOCK)", sql);
        Assert.DoesNotContain("UPDLOCK", sql);
    }

    [Fact]
    public void SqlServer_UpdateNoWait_Throws()
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            RowLockSqlBuilder.Apply(SimpleSelect, DialectFor("SqlServer2012"), LockMode.UpdateNoWait));
        Assert.Contains("LOCK_TIMEOUT 0", ex.Message);
    }

    [Fact]
    public void SqlServer_HintAttachesToLeadingTableInJoin()
    {
        // Table hint applies to the leading table only — the JOIN target is left alone.
        var sql = RowLockSqlBuilder.Apply(SelectWithJoin, DialectFor("SqlServer2012"), LockMode.Update);

        var hintIdx = sql.IndexOf("WITH (UPDLOCK", StringComparison.Ordinal);
        var joinIdx = sql.IndexOf("LEFT JOIN", StringComparison.Ordinal);
        Assert.True(hintIdx > 0);
        Assert.True(hintIdx < joinIdx);
        // The Other table is NOT hinted.
        Assert.Equal(1, sql.Split("WITH (UPDLOCK").Length - 1);
    }

    [Fact]
    public void SqlServer_NoFromClause_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RowLockSqlBuilder.Apply("SELECT 1", DialectFor("SqlServer2012"), LockMode.Update));
        Assert.Contains("no FROM clause", ex.Message);
    }

    // ---------- Postgres ----------

    [Theory]
    [InlineData(LockMode.Update,       " FOR UPDATE")]
    [InlineData(LockMode.UpdateNoWait, " FOR UPDATE NOWAIT")]
    [InlineData(LockMode.UpdateSkip,   " FOR UPDATE SKIP LOCKED")]
    [InlineData(LockMode.Share,        " FOR SHARE")]
    public void Postgres_AppendsCorrectClause(LockMode mode, string expectedSuffix)
    {
        var sql = RowLockSqlBuilder.Apply(SelectWithOrder, DialectFor("Postgres"), mode);
        Assert.EndsWith(expectedSuffix, sql);
        // ORDER BY must come BEFORE the lock clause.
        Assert.True(sql.IndexOf("ORDER BY", StringComparison.Ordinal) <
                    sql.LastIndexOf(expectedSuffix, StringComparison.Ordinal));
    }

    // ---------- MySQL / MariaDB ----------

    [Theory]
    [InlineData("MySql5",  LockMode.Update,       " FOR UPDATE")]
    [InlineData("MySql5",  LockMode.UpdateNoWait, " FOR UPDATE NOWAIT")]
    [InlineData("MySql5",  LockMode.UpdateSkip,   " FOR UPDATE SKIP LOCKED")]
    [InlineData("MySql5",  LockMode.Share,        " LOCK IN SHARE MODE")]
    [InlineData("MariaDb", LockMode.Update,       " FOR UPDATE")]
    [InlineData("MariaDb", LockMode.UpdateSkip,   " FOR UPDATE SKIP LOCKED")]
    public void MySql_AppendsCorrectClause(string serverType, LockMode mode, string expectedSuffix)
    {
        var sql = RowLockSqlBuilder.Apply(SelectWithOrder, DialectFor(serverType), mode);
        Assert.EndsWith(expectedSuffix, sql);
    }

    // ---------- SQLite ----------

    [Theory]
    [InlineData(LockMode.Update)]
    [InlineData(LockMode.Share)]
    [InlineData(LockMode.UpdateNoWait)]
    [InlineData(LockMode.UpdateSkip)]
    public void Sqlite_AlwaysThrows(LockMode mode)
    {
        var ex = Assert.Throws<NotSupportedException>(() =>
            RowLockSqlBuilder.Apply(SimpleSelect, DialectFor("Sqlite"), mode));
        Assert.Contains("BEGIN IMMEDIATE", ex.Message);
    }

    // ---------- Oracle / unknown ----------

    [Fact]
    public void Oracle_UsesAnsiForUpdate()
    {
        var sql = RowLockSqlBuilder.Apply(SelectWithOrder, DialectFor("Oracle12c"), LockMode.Update);
        Assert.EndsWith(" FOR UPDATE", sql);
    }

    [Fact]
    public void Oracle_ShareMode_Throws()
    {
        // FOR SHARE isn't standard ANSI — refuse rather than emit non-portable SQL.
        var ex = Assert.Throws<NotSupportedException>(() =>
            RowLockSqlBuilder.Apply(SelectWithOrder, DialectFor("Oracle12c"), LockMode.Share));
        Assert.Contains("FOR SHARE", ex.Message);
    }

    // ---------- Argument validation ----------

    [Fact]
    public void NullSql_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RowLockSqlBuilder.Apply(null!, DialectFor("Postgres"), LockMode.Update));
    }

    [Fact]
    public void NullDialect_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RowLockSqlBuilder.Apply(SimpleSelect, null!, LockMode.Update));
    }

    [Fact]
    public void InvalidLockMode_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RowLockSqlBuilder.Apply(SimpleSelect, DialectFor("Postgres"), (LockMode)999));
    }
}
