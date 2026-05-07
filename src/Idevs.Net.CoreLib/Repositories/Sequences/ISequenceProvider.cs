namespace Idevs.Repositories.Sequences;

/// <summary>
/// Atomic numeric sequence allocator. Each call returns a value that no
/// other concurrent caller can return for the same <c>sequenceKey</c>.
/// </summary>
/// <remarks>
/// <para>
/// Implementations MUST allocate independently of any ambient
/// <see cref="Serenity.Data.IUnitOfWork"/> — the allocated value survives
/// an outer caller's rollback. This is the intended semantic: gaps in the
/// allocated range are normal (rolled-back outer transactions produce
/// them), duplicate values are catastrophic. Document-number / invoice-
/// number / order-number sequences are the canonical use case.
/// </para>
/// <para>
/// Returned values are <see cref="long"/>; callers format to whatever
/// scheme their domain requires (e.g. <c>INV-2026-00042</c>). The
/// provider does not know about formatting.
/// </para>
/// </remarks>
public interface ISequenceProvider
{
    /// <summary>
    /// Allocate the next value for <paramref name="sequenceKey"/>.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The sequence has not been seeded. Call
    /// <see cref="EnsureSequenceAsync"/> first.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="sequenceKey"/> is null or empty.
    /// </exception>
    Task<long> NextAsync(string sequenceKey, CancellationToken ct = default);

    /// <summary>
    /// Allocate <paramref name="count"/> contiguous values atomically.
    /// Returns them in ascending order. Useful for bulk imports where
    /// every record needs an allocated number.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    /// The sequence has not been seeded.
    /// </exception>
    /// <exception cref="System.ArgumentException">
    /// <paramref name="sequenceKey"/> is null or empty.
    /// </exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="count"/> is not positive.
    /// </exception>
    Task<IReadOnlyList<long>> NextRangeAsync(
        string sequenceKey, int count, CancellationToken ct = default);

    /// <summary>
    /// Idempotently create the sequence row with
    /// <paramref name="startValue"/> as its first allocatable value.
    /// If a row already exists for <paramref name="sequenceKey"/>, this
    /// is a no-op — the existing <c>NextValue</c> is preserved
    /// regardless of <paramref name="startValue"/>.
    /// </summary>
    /// <param name="sequenceKey">The sequence to seed.</param>
    /// <param name="startValue">
    /// First value <see cref="NextAsync"/> will return. Defaults to 1.
    /// Ignored if the row already exists.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureSequenceAsync(
        string sequenceKey, long startValue = 1, CancellationToken ct = default);
}
