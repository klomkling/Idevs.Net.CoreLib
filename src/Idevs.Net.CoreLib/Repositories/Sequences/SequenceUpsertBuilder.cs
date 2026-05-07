using Serenity.Data;

namespace Idevs.Repositories.Sequences;

/// <summary>
/// Builds the dialect-specific "insert if not exists" statement used by
/// <see cref="SqlSequenceProvider.EnsureSequenceAsync"/>. Internal — the
/// generated SQL is paired with the well-known parameter names
/// <c>@key</c> (string) and <c>@val</c> (long) the caller passes via
/// <see cref="SqlServiceBase.ExecuteNonQueryAsync"/>.
/// </summary>
/// <remarks>
/// The earlier "INSERT then catch on PK violation" approach broke on
/// PostgreSQL: any error inside a transaction aborts it on PG, so the
/// follow-up SELECT and the InNewTransactionAsync commit both fail with
/// 25P02 ("current transaction is aborted, commands ignored until end
/// of transaction block"). A single dialect-aware UPSERT side-steps the
/// race and the abort, by never raising an error in the
/// already-exists case.
/// </remarks>
internal static class SequenceUpsertBuilder
{
    /// <summary>
    /// Returns a single SQL statement that inserts a row into
    /// <c>IdevsSequences (SequenceKey, NextValue)</c> if no row exists
    /// for <c>@key</c>, and is a no-op (no error) otherwise.
    /// </summary>
    /// <exception cref="System.NotSupportedException">
    /// SQLite is fine, but anything outside the documented set
    /// (SqlServer / MySQL / MariaDB / Postgres / Oracle / SQLite)
    /// has no agreed upsert syntax. Throw so consumers know to either
    /// pre-seed the row or extend this dispatch.
    /// </exception>
    public static string Build(ISqlDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(dialect);

        var serverType = dialect.ServerType ?? string.Empty;

        // NOTE: identifiers are deliberately unquoted here — the
        // schema documented in MIGRATION.md uses unquoted DDL on every
        // engine, so case-folding on PostgreSQL is consistent. If a
        // consumer quotes their CREATE TABLE differently, they'll need
        // to override this provider with one that matches their schema.
        if (IsSqlServer(serverType))
            return "IF NOT EXISTS (SELECT 1 FROM IdevsSequences WHERE SequenceKey = @key) " +
                   "INSERT INTO IdevsSequences (SequenceKey, NextValue) VALUES (@key, @val);";

        if (IsMySql(serverType))
            return "INSERT IGNORE INTO IdevsSequences (SequenceKey, NextValue) VALUES (@key, @val);";

        if (IsPostgres(serverType))
            return "INSERT INTO IdevsSequences (SequenceKey, NextValue) VALUES (@key, @val) " +
                   "ON CONFLICT (SequenceKey) DO NOTHING;";

        if (IsSqlite(serverType))
            return "INSERT OR IGNORE INTO IdevsSequences (SequenceKey, NextValue) VALUES (@key, @val);";

        // Oracle and other ANSI engines support MERGE; this is the most
        // portable fallback. Fail loudly when we can't tell.
        if (string.Equals(serverType, "Oracle", StringComparison.OrdinalIgnoreCase) ||
            serverType.StartsWith("Oracle", StringComparison.OrdinalIgnoreCase))
            return "MERGE INTO IdevsSequences t USING (SELECT @key AS K, @val AS V FROM dual) s " +
                   "ON (t.SequenceKey = s.K) WHEN NOT MATCHED THEN " +
                   "INSERT (SequenceKey, NextValue) VALUES (s.K, s.V)";

        throw new NotSupportedException(
            $"No documented insert-if-not-exists syntax for dialect '{serverType}'. " +
            "Pre-seed the IdevsSequences row in your migration pipeline, or supply a " +
            "custom ISequenceProvider implementation that knows your engine.");
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
}
