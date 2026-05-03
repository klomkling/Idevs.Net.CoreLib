using System.Data;
using Serenity.Data;

namespace Idevs.Repositories;

/// <summary>
/// Disposable scope around a <see cref="IUnitOfWork"/> created by
/// <see cref="SqlServiceBase.BeginUnitOfWork"/>. Either wraps a caller-owned
/// UoW (Commit/Dispose are no-ops) or owns a freshly opened connection +
/// transaction (Commit must be called explicitly; Dispose rolls back when
/// Commit was not called).
/// </summary>
/// <remarks>
/// The explicit <see cref="Commit"/> requirement matches the
/// <see cref="System.Transactions.TransactionScope"/> idiom — leaving the
/// using block without calling Commit() is treated as failure and rolls back.
/// </remarks>
public sealed class UnitOfWorkScope : IDisposable, IAsyncDisposable
{
    private readonly IDbConnection? _ownedConnection;
    private readonly UnitOfWork? _ownedUow;
    private bool _committed;
    private bool _disposed;

    /// <summary>The unit of work to pass to repository calls.</summary>
    public IUnitOfWork Uow { get; }

    /// <summary>
    /// True when this scope owns the connection + UoW it wraps. False when it
    /// wraps a caller-owned UoW (in which case Commit/Dispose are no-ops).
    /// </summary>
    public bool OwnsUnitOfWork => _ownedUow is not null;

    /// <summary>Wrap a caller-owned <see cref="IUnitOfWork"/>. Commit and Dispose are no-ops.</summary>
    internal UnitOfWorkScope(IUnitOfWork callerUow)
    {
        Uow = callerUow ?? throw new ArgumentNullException(nameof(callerUow));
        // Caller owns the transaction; nothing for this scope to commit or roll back.
        _committed = true;
    }

    /// <summary>Take ownership of a freshly opened connection + new <see cref="UnitOfWork"/>.</summary>
    internal UnitOfWorkScope(IDbConnection connection, UnitOfWork uow)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(uow);
        _ownedConnection = connection;
        _ownedUow = uow;
        Uow = uow;
    }

    /// <summary>
    /// Commit the owned transaction. No-op when this scope wraps a caller-owned
    /// UoW or when Commit has already been called.
    /// </summary>
    public void Commit()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(UnitOfWorkScope));
        if (_ownedUow is null || _committed) return;

        _ownedUow.Commit();
        _committed = true;
    }

    /// <summary>
    /// Dispose the scope. Owned UoWs that were not committed are rolled back
    /// (via the underlying <see cref="UnitOfWork"/> dispose). Wrapped caller
    /// UoWs are left untouched.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Owned UoW: dispose triggers rollback if Commit was not called.
        // Caller-owned UoW: nothing to do.
        _ownedUow?.Dispose();
        _ownedConnection?.Dispose();
    }

    /// <summary>Async dispose — current implementation is synchronous; provided for `await using` ergonomics.</summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
