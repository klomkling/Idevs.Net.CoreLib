using Serenity.ComponentModel;
using Serenity.Data;
using Serenity.Data.Mapping;

namespace Idevs.Repositories.Sequences;

/// <summary>
/// Persistent row backing <see cref="SqlSequenceProvider"/>. One row per
/// distinct <c>SequenceKey</c>; <c>NextValue</c> holds the value the next
/// <see cref="ISequenceProvider.NextAsync"/> call will return.
/// </summary>
/// <remarks>
/// The library does not ship database migrations. Consumers create the
/// underlying <c>IdevsSequences</c> table in their own migration
/// pipeline; the schema is documented in MIGRATION.md (v0.7.6 → v0.7.7).
/// </remarks>
[ConnectionKey("Default"), Module("Idevs"), TableName("[IdevsSequences]")]
public sealed class IdevsSequenceRow : Row<IdevsSequenceRow.RowFields>
{
    /// <summary>
    /// Caller-defined sequence identifier. Convention: namespace with
    /// colons (e.g. <c>"DocNo:SaleOrder"</c>, <c>"DocNo:Invoice:2026"</c>).
    /// Primary key.
    /// </summary>
    [Size(100), NotNull, PrimaryKey]
    public string? SequenceKey
    {
        get => fields.SequenceKey[this];
        set => fields.SequenceKey[this] = value;
    }

    /// <summary>
    /// The value the next <see cref="ISequenceProvider.NextAsync"/> call
    /// will return for this sequence. After allocation, the row is
    /// updated to <c>NextValue + 1</c> (or <c>+ count</c> for
    /// <see cref="ISequenceProvider.NextRangeAsync"/>).
    /// </summary>
    [NotNull]
    public long? NextValue
    {
        get => fields.NextValue[this];
        set => fields.NextValue[this] = value;
    }

    public new static readonly RowFields Fields = (RowFields)new IdevsSequenceRow().GetFields();

    public sealed class RowFields : RowFieldsBase
    {
#pragma warning disable CS8618 // populated by RowFieldsBase reflection
        public StringField SequenceKey;
        public Int64Field NextValue;
#pragma warning restore CS8618
    }
}
