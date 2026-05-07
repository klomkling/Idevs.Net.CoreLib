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

            // Overflow protection: at long.MaxValue the unchecked `+ 1`
            // would wrap to long.MinValue, producing negative allocations
            // that would silently collide with future values once the
            // counter wraps forward. We want loud failure, not silent
            // corruption.
            long advanced;
            try { advanced = checked(allocated + 1); }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    $"Sequence '{sequenceKey}' has been exhausted: " +
                    $"NextValue ({allocated}) is at long.MaxValue and cannot advance.",
                    ex);
            }

            await UpdateAsync(
                u => u.Set(Fld.NextValue, advanced)
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

            // Overflow protection — same reasoning as NextAsync but the
            // jump is `+ count` instead of `+ 1`, so the threshold is
            // closer to long.MaxValue.
            long advanced;
            try { advanced = checked(first + count); }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException(
                    $"Sequence '{sequenceKey}' would overflow: " +
                    $"NextValue ({first}) + count ({count}) exceeds long.MaxValue.",
                    ex);
            }

            await UpdateAsync(
                u => u.Set(Fld.NextValue, advanced)
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
            // Single-statement dialect-aware UPSERT. No try/catch
            // required: the statement is a no-op when the row already
            // exists (no error, no transaction abort). This is the
            // critical fix for PostgreSQL — the previous "INSERT then
            // catch on PK violation" pattern aborted the surrounding
            // transaction on PG (error 25P02), so the InNewTransactionAsync
            // commit failed even when we'd correctly identified a race.
            var sql = SequenceUpsertBuilder.Build(Dialect);
            var parameters = new Dictionary<string, object?>
            {
                ["@key"] = sequenceKey,
                ["@val"] = startValue,
            };
            await ExecuteNonQueryAsync(sql, parameters, uow, token).ConfigureAwait(false);
        }, ct);
    }

    private static InvalidOperationException NotSeeded(string sequenceKey) => new(
        $"Sequence '{sequenceKey}' has not been seeded. " +
        "Call ISequenceProvider.EnsureSequenceAsync first, or insert a row " +
        "into IdevsSequences via your migration pipeline.");
}
