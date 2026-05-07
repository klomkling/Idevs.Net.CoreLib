namespace Idevs.Repositories;

/// <summary>
/// Row-level lock intent applied via <see cref="SqlQueryLockExtensions.ForUpdate"/>.
/// The hint is materialised by <see cref="RowLockSqlBuilder"/> using
/// dialect-correct syntax for SqlServer, MySQL/MariaDB, PostgreSQL, and Oracle.
/// SQLite has no row-level locking and throws on every mode.
/// </summary>
public enum LockMode
{
    /// <summary>
    /// Exclusive update lock; blocks readers and writers.
    /// SqlServer: <c>WITH (UPDLOCK, HOLDLOCK, ROWLOCK)</c>.
    /// MySQL/MariaDB/PostgreSQL/Oracle: <c>FOR UPDATE</c>.
    /// </summary>
    Update,

    /// <summary>
    /// Shared (read) lock; blocks writers, allows other shared readers.
    /// SqlServer: <c>WITH (HOLDLOCK, ROWLOCK)</c>.
    /// PostgreSQL: <c>FOR SHARE</c>.
    /// MySQL/MariaDB: <c>LOCK IN SHARE MODE</c>.
    /// </summary>
    Share,

    /// <summary>
    /// Exclusive update lock; throw immediately if any matched row is currently locked.
    /// PostgreSQL / MySQL 8+ / MariaDB 10.6+ / Oracle: <c>FOR UPDATE NOWAIT</c>.
    /// SqlServer: NOT SUPPORTED — there is no in-query NOWAIT hint. Use
    /// <c>SET LOCK_TIMEOUT 0</c> at the session level and catch error 1222.
    /// SQLite: not supported.
    /// </summary>
    UpdateNoWait,

    /// <summary>
    /// Exclusive update lock; skip rows currently locked by other transactions
    /// (queue-consumer pattern).
    /// SqlServer: <c>WITH (UPDLOCK, HOLDLOCK, ROWLOCK, READPAST)</c>.
    /// PostgreSQL 9.5+ / MySQL 8+ / MariaDB 10.6+ / Oracle: <c>FOR UPDATE SKIP LOCKED</c>.
    /// SQLite: not supported.
    /// </summary>
    UpdateSkip,
}
