using System.Runtime.CompilerServices;
using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Fluent <see cref="SqlQuery"/> extension that flags a SELECT for row-level
/// locking. The lock hint is materialised at execution time by Idevs
/// repository helpers (<see cref="RepositoryBase{TRow}.TryFirstAsync"/>,
/// <see cref="RepositoryBase{TRow}.ListAsync"/>).
/// </summary>
/// <remarks>
/// <para>
/// Direct Serenity execution paths — <c>connection.TryFirst&lt;TRow&gt;(query)</c>,
/// <c>connection.Query&lt;TRow&gt;(query)</c>, etc. — DO NOT honour this flag
/// and will silently produce non-locking SQL. Always go through Idevs
/// repository helpers when locking matters.
/// </para>
/// <para>
/// Locks require an active transaction. Calling a flagged query without
/// passing a non-null <see cref="IUnitOfWork"/> throws
/// <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public static class SqlQueryLockExtensions
{
    // ConditionalWeakTable lets us attach state to a SqlQuery without
    // mutating its public shape. Entries are GC'd with the query.
    private static readonly ConditionalWeakTable<SqlQuery, object> _state = new();

    /// <summary>
    /// Mark this query for row-level locking. Lock is applied dialect-correctly
    /// when the query is executed via an Idevs repository helper.
    /// </summary>
    /// <param name="query">The query to flag.</param>
    /// <param name="mode">Lock intent. Defaults to <see cref="LockMode.Update"/>.</param>
    /// <returns><paramref name="query"/> for fluent chaining.</returns>
    public static SqlQuery ForUpdate(this SqlQuery query, LockMode mode = LockMode.Update)
    {
        ArgumentNullException.ThrowIfNull(query);
        _state.AddOrUpdate(query, mode);
        return query;
    }

    /// <summary>
    /// Read the lock mode previously attached via <see cref="ForUpdate"/>, or
    /// <c>null</c> if the query is not flagged.
    /// </summary>
    internal static LockMode? TryGetLockMode(this SqlQuery? query)
    {
        if (query is null) return null;
        return _state.TryGetValue(query, out var v) && v is LockMode m ? m : null;
    }
}
