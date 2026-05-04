using Serenity.ComponentModel;
using Serenity.Data;
using Serenity.Data.Mapping;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Row used by repository write integration tests. Mixes table fields with a
/// <see cref="NotMappedAttribute"/> property and an <see cref="ExpressionAttribute"/>
/// joined column to verify Serenity's auto-exclusion behavior end-to-end
/// against a real SQL Server.
/// </summary>
[ConnectionKey("Default"), Module("Integration"), TableName("[IntegrationTestRows]")]
public sealed class IntegrationTestRow : Row<IntegrationTestRow.RowFields>, IIdRow, INameRow
{
    [Identity, IdProperty]
    public int? Id { get => fields.Id[this]; set => fields.Id[this] = value; }

    [Size(50), NotNull, NameProperty]
    public string? Code { get => fields.Code[this]; set => fields.Code[this] = value; }

    public decimal? Amount { get => fields.Amount[this]; set => fields.Amount[this] = value; }

    [Size(20)]
    public string? Status { get => fields.Status[this]; set => fields.Status[this] = value; }

    private string? _transientNote;

    /// <summary>
    /// Plain CLR property with no <see cref="Field"/> in <see cref="RowFields"/>.
    /// This is the production-correct pattern for a transient property that
    /// must round-trip through application code but never appear in any SQL
    /// — the field is simply not part of the row's metadata, so Serenity has
    /// nothing to insert/update/select.
    /// </summary>
    [Serenity.Data.Mapping.NotMapped]
    public string? TransientNote
    {
        get => _transientNote;
        set => _transientNote = value;
    }

    /// <summary>
    /// SQL expression-based field — derived from another column (or a JOIN in
    /// real schemas). Reading it via SELECT works (Serenity rewrites the
    /// expression into the projection); writing must NOT appear in
    /// INSERT/UPDATE because no real column named "AmountDoubled" exists.
    /// As with NotMapped, we pair <c>[Expression]</c> with
    /// <c>[SetFieldFlags]</c> to clear Insertable/Updatable explicitly.
    /// </summary>
    [Serenity.Data.Mapping.Expression("(t0.Amount * 2)")]
    [Serenity.Data.Mapping.SetFieldFlags(FieldFlags.None, FieldFlags.Insertable | FieldFlags.Updatable)]
    public decimal? AmountDoubled
    {
        get => fields.AmountDoubled[this];
        set => fields.AmountDoubled[this] = value;
    }

    public new static readonly RowFields Fields = (RowFields)new IntegrationTestRow().GetFields();

    public sealed class RowFields : RowFieldsBase
    {
#pragma warning disable CS8618 // populated by RowFieldsBase reflection
        public Int32Field Id;
        public StringField Code;
        public DecimalField Amount;
        public StringField Status;

        // TransientNote intentionally omitted — see the [NotMapped] property
        // above. AmountDoubled remains because [Expression] needs a backing
        // field for SELECT-time materialization.
        public DecimalField AmountDoubled;
#pragma warning restore CS8618
    }
}
