using System.Data.Common;
using Idevs.ComponentModels;
using Serenity.Data;

namespace Idevs.Repositories.Sequences;

/// <summary>
/// SQL-backed default <see cref="ISequenceProvider"/>. Stores one row per
/// sequence in the <c>IdevsSequences</c> table; allocates values via
/// <see cref="SqlServiceBase.InNewTransactionAsync{T}(System.Func{IUnitOfWork, System.Threading.CancellationToken, System.Threading.Tasks.Task{T}}, System.Threading.CancellationToken)"/>
/// + <see cref="SqlQueryLockExtensions.ForUpdate"/> so the lock window is
/// the duration of the SELECT…FOR UPDATE + UPDATE — typically
/// sub-millisecond on a same-region database.
/// </summary>
/// <remarks>
/// <para>
/// Registered via <c>[Scoped(ServiceType = typeof(ISequenceProvider))]</c>
/// for source-generator DI (the named-property form is required —
/// <see cref="ScopedAttribute"/> has no positional constructor for the
/// service type). Manual setups can call
/// <see cref="SequenceServiceCollectionExtensions.AddIdevsSequenceProvider"/>.
/// </para>
/// <para>
/// Defaults to <see cref="IdevsSequenceRow"/>'s <c>"Default"</c>
/// connection key. To put sequences on a different connection, subclass
/// <see cref="IdevsSequenceRow"/> with a different
/// <c>[ConnectionKey]</c> attribute and supply a custom
/// <see cref="ISequenceProvider"/> implementation against that subclass.
/// </para>
/// </remarks>
[Scoped(ServiceType = typeof(ISequenceProvider))]
public sealed class SqlSequenceProvider(ISqlConnections sqlConnections)
    : RepositoryBase<IdevsSequenceRow>(sqlConnections), ISequenceProvider
{
    private static readonly IdevsSequenceRow.RowFields Fld = IdevsSequenceRow.Fields;

    /// <inheritdoc />
    public Task<long> NextAsync(string sequenceKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sequenceKey);

        return InNewTransactionAsync(async (uow, token) =>
        {
            var row = await TryFirstAsync(
                q => q.SelectTableFields()
                      .Where(Fld.SequenceKey == sequenceKey)
                      .ForUpdate(),
                uow, token).ConfigureAwait(false)
                ?? throw NotSeeded(sequenceKey);

            var allocated = row.NextValue!.Value;
            await UpdateAsync(
                u => u.Set(Fld.NextValue, allocated + 1)
                      .Where(Fld.SequenceKey == sequenceKey),
                ExpectedRows.One, uow, token).ConfigureAwait(false);
            return allocated;
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<long>> NextRangeAsync(
        string sequenceKey, int count, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sequenceKey);
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count),
                "Range count must be a positive integer.");

        return InNewTransactionAsync(async (uow, token) =>
        {
            var row = await TryFirstAsync(
                q => q.SelectTableFields()
                      .Where(Fld.SequenceKey == sequenceKey)
                      .ForUpdate(),
                uow, token).ConfigureAwait(false)
                ?? throw NotSeeded(sequenceKey);

            var first = row.NextValue!.Value;
            await UpdateAsync(
                u => u.Set(Fld.NextValue, first + count)
                      .Where(Fld.SequenceKey == sequenceKey),
                ExpectedRows.One, uow, token).ConfigureAwait(false);

            var values = new long[count];
            for (var i = 0; i < count; i++) values[i] = first + i;
            return (IReadOnlyList<long>)values;
        }, ct);
    }

    /// <inheritdoc />
    public Task EnsureSequenceAsync(
        string sequenceKey, long startValue = 1, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sequenceKey);

        return InNewTransactionAsync(async (uow, token) =>
        {
            // Plain TryFirstAsync (no ForUpdate) is fine here — concurrent
            // EnsureSequenceAsync calls for the same key both see "no row",
            // both attempt CreateAsync, the second hits the primary-key
            // unique constraint and throws. Catching that path keeps the
            // happy path free of the lock overhead. For a sequence that's
            // about to be allocated against, the FIRST NextAsync will take
            // the lock anyway; race here is irrelevant.
            var existing = await TryFirstAsync(
                q => q.SelectTableFields().Where(Fld.SequenceKey == sequenceKey),
                uow, token).ConfigureAwait(false);
            if (existing is not null) return;

            try
            {
                await CreateAsync(new IdevsSequenceRow
                {
                    SequenceKey = sequenceKey,
                    NextValue = startValue,
                }, uow, token).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // Narrowed catch: only DbException (covers SqlException,
                // MySqlException, NpgsqlException, etc.). Non-database
                // errors — OperationCanceledException, ArgumentException,
                // NullReferenceException — propagate without being
                // inspected, so configuration / cancellation bugs surface
                // immediately instead of being swallowed by the race
                // recovery path.
                //
                // Re-check the row: if a concurrent EnsureSequenceAsync
                // won the create race, treat as a no-op. Otherwise (e.g.
                // permission error, schema missing, transient connection
                // failure with no row yet) rethrow.
                var racing = await TryFirstAsync(
                    q => q.SelectTableFields().Where(Fld.SequenceKey == sequenceKey),
                    uow, token).ConfigureAwait(false);
                if (racing is null) throw;
            }
        }, ct);
    }

    private static InvalidOperationException NotSeeded(string sequenceKey) => new(
        $"Sequence '{sequenceKey}' has not been seeded. " +
        "Call ISequenceProvider.EnsureSequenceAsync first, or insert a row " +
        "into IdevsSequences via your migration pipeline.");
}
