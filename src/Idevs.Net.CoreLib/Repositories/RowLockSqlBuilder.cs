using System.Text.RegularExpressions;
using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Applies dialect-specific row-lock hints to a materialised SELECT statement.
/// Internal — callers go through
/// <see cref="SqlQueryLockExtensions.ForUpdate"/> on a query that is then
/// materialised by <see cref="RepositoryBase{TRow}.TryFirstAsync"/> or a
/// sibling helper.
/// </summary>
internal static class RowLockSqlBuilder
{
    /// <summary>
    /// Insert (or append) a row-lock clause to <paramref name="sql"/> using
    /// the syntax appropriate for <paramref name="dialect"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="sql"/> or <paramref name="dialect"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined enum value.</exception>
    /// <exception cref="NotSupportedException">
    /// SQLite (no row-level locking) on any mode; SqlServer on <see cref="LockMode.UpdateNoWait"/>;
    /// non-SqlServer/MySQL/Postgres dialects on <see cref="LockMode.Share"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// SqlServer dialect but the SQL has no FROM clause — cannot inject a table hint.
    /// </exception>
    public static string Apply(string sql, ISqlDialect dialect, LockMode mode)
    {
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(dialect);

        var serverType = dialect.ServerType ?? string.Empty;

        if (IsSqlServer(serverType))
            return ApplySqlServer(sql, mode);
        if (IsPostgres(serverType))
            return sql + ApplyAnsi(mode, shareClause: " FOR SHARE", dialectName: serverType);
        if (IsMySql(serverType))
            return sql + ApplyAnsi(mode, shareClause: " LOCK IN SHARE MODE", dialectName: serverType);
        if (IsSqlite(serverType))
            throw new NotSupportedException(
                "SQLite does not support row-level locking. Begin the transaction with " +
                "BEGIN IMMEDIATE for a database-wide write lock instead.");

        // Oracle and other ANSI-standard dialects accept FOR UPDATE [NOWAIT|SKIP LOCKED].
        // Share mode is dialect-specific and not portable here.
        return sql + ApplyAnsi(mode, shareClause: null, dialectName: serverType);
    }

    private static string ApplyAnsi(LockMode mode, string? shareClause, string dialectName) => mode switch
    {
        LockMode.Update => " FOR UPDATE",
        LockMode.UpdateNoWait => " FOR UPDATE NOWAIT",
        LockMode.UpdateSkip => " FOR UPDATE SKIP LOCKED",
        LockMode.Share when shareClause is not null => shareClause,
        LockMode.Share => throw new NotSupportedException(
            $"FOR SHARE is not portable to dialect '{dialectName}'. Use Update mode."),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown LockMode."),
    };

    private static string ApplySqlServer(string sql, LockMode mode)
    {
        var hint = mode switch
        {
            LockMode.Update => "WITH (UPDLOCK, HOLDLOCK, ROWLOCK)",
            LockMode.Share => "WITH (HOLDLOCK, ROWLOCK)",
            LockMode.UpdateSkip => "WITH (UPDLOCK, HOLDLOCK, ROWLOCK, READPAST)",
            LockMode.UpdateNoWait => throw new NotSupportedException(
                "SqlServer has no in-query NOWAIT hint. Set 'SET LOCK_TIMEOUT 0' " +
                "at the session/transaction level before the SELECT, then catch " +
                "error 1222 (lock-request timeout)."),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown LockMode."),
        };
        return InjectFirstTableHint(sql, hint);
    }

    private static bool IsSqlServer(string s) =>
        s.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ||
        s.Contains("MSSQL", StringComparison.OrdinalIgnoreCase);

    private static bool IsPostgres(string s) =>
        s.Contains("Postgres", StringComparison.OrdinalIgnoreCase);

    private static bool IsMySql(string s) =>
        s.Contains("MySql", StringComparison.OrdinalIgnoreCase) ||
        s.Contains("MariaDb", StringComparison.OrdinalIgnoreCase);

    private static bool IsSqlite(string s) =>
        s.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Insert <paramref name="hint"/> immediately after the table reference in
    /// the first FROM clause. Stops at the next major SQL keyword
    /// (WHERE / JOIN / GROUP / ORDER / HAVING / UNION / EXCEPT / INTERSECT)
    /// or end-of-statement.
    /// </summary>
    /// <remarks>
    /// Works for the simple <c>SELECT … FROM &lt;table&gt; [alias] WHERE … ORDER BY …</c>
    /// shape that <c>SqlQuery</c> emits for single-table selects. Whitespace-
    /// tolerant — Serenity's <c>SqlQuery.ToString()</c> emits multi-line SQL
    /// with newlines between clauses. JOINs are supported because SqlServer
    /// table hints attach to the leading table only; the rest of the join is
    /// unaffected. Bails loudly when the SELECT has no FROM clause rather
    /// than silently producing wrong SQL.
    /// </remarks>
    private static string InjectFirstTableHint(string sql, string hint)
    {
        // Word-boundary FROM (any whitespace before/after, including newlines).
        var fromMatch = Regex.Match(sql, @"\sFROM\s", RegexOptions.IgnoreCase);
        if (!fromMatch.Success)
            throw new InvalidOperationException(
                "Cannot apply SqlServer table hint: SELECT has no FROM clause. Got: " + sql);

        var afterTable = fromMatch.Index + fromMatch.Length;

        // Find the earliest stop keyword AFTER the FROM <table> [alias]. Each
        // keyword is matched on a word boundary so Serenity's newline-separated
        // formatting works the same as single-line SQL.
        var stops = new[]
        {
            "WHERE", "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "OUTER",
            "JOIN", "GROUP", "ORDER", "HAVING", "UNION", "EXCEPT", "INTERSECT",
        };
        var injectAt = sql.Length;
        foreach (var kw in stops)
        {
            var m = Regex.Match(sql[afterTable..], $@"\s{kw}\s", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var absolute = afterTable + m.Index;
                if (absolute < injectAt) injectAt = absolute;
            }
        }
        return sql[..injectAt] + " " + hint + sql[injectAt..];
    }
}
