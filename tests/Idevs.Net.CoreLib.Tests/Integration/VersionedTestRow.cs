using Serenity.ComponentModel;
using Serenity.Data;
using Serenity.Data.Mapping;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// Row used by optimistic-concurrency integration tests. Carries a
/// <see cref="RowVersionAttribute"/>-decorated <c>RowVersion</c> column;
/// the library's <c>UpdateAsync(TRow, …)</c> overloads are expected to
/// auto-detect it and apply the WHERE/SET guard.
/// </summary>
[ConnectionKey("Default"), Module("Integration"), TableName("[VersionedTestRows]")]
public sealed class VersionedTestRow : Row<VersionedTestRow.RowFields>, IIdRow
{
    [Identity, IdProperty]
    public int? Id { get => fields.Id[this]; set => fields.Id[this] = value; }

    [Size(50), NotNull]
    public string? Code { get => fields.Code[this]; set => fields.Code[this] = value; }

    public decimal? Amount { get => fields.Amount[this]; set => fields.Amount[this] = value; }

    [NotNull, Idevs.Repositories.RowVersion]
    public long? RowVersion { get => fields.RowVersion[this]; set => fields.RowVersion[this] = value; }

    public new static readonly RowFields Fields = (RowFields)new VersionedTestRow().GetFields();

    public sealed class RowFields : RowFieldsBase
    {
#pragma warning disable CS8618 // populated by RowFieldsBase reflection
        public Int32Field Id;
        public StringField Code;
        public DecimalField Amount;
        public Int64Field RowVersion;
#pragma warning restore CS8618
    }
}
