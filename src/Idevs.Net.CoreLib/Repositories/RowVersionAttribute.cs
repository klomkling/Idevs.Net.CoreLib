namespace Idevs.Repositories;

/// <summary>
/// Marks a <see cref="long"/>? property on a Serenity row as that row's
/// optimistic-concurrency version counter. The
/// <see cref="RowRepositoryBase{TRow,TKey}.UpdateAsync(TRow, Serenity.Data.IUnitOfWork?, System.Threading.CancellationToken)"/>
/// overloads detect this attribute and guard the UPDATE with a
/// <c>WHERE RowVersion = @captured</c> + <c>SET RowVersion = RowVersion + 1</c>
/// pair; on conflict they throw <see cref="OptimisticConcurrencyException"/>.
/// </summary>
/// <remarks>
/// <para>
/// Apply to the property — not the field — so consumer rows pick up the
/// flag through the standard Serenity attribute pipeline.
/// </para>
/// <para>
/// Constraints (validated at first use, with an explicit error message
/// when violated):
/// </para>
/// <list type="bullet">
/// <item><description>At most one property per row may be flagged.</description></item>
/// <item><description>The property type MUST be <see cref="long"/>? (nullable long).
/// Other integer types are rejected for consistency.</description></item>
/// <item><description>The corresponding <see cref="Serenity.Data.Field"/> in
/// <c>RowFields</c> must exist and have <c>FieldFlags.Updatable</c> set, since
/// the library overwrites the value as part of every guarded UPDATE.</description></item>
/// </list>
/// <para>
/// <b>Schema:</b> the backing column is a plain <c>BIGINT NOT NULL DEFAULT 0</c>
/// in every supported dialect — application-managed, portable across
/// SqlServer / MySQL / MariaDB / PostgreSQL / Oracle / SQLite. The library
/// does not use SqlServer's native <c>rowversion</c> / <c>timestamp</c> type;
/// see MIGRATION.md (v0.7.7 → v0.7.8) for the rationale.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class RowVersionAttribute : Attribute
{
}
