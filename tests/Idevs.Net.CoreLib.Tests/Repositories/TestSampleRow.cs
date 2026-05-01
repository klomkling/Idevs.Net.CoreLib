using Serenity.ComponentModel;
using Serenity.Data;
using Serenity.Data.Mapping;

namespace Idevs.Net.CoreLib.Tests.Repositories;

[ConnectionKey("Default"), Module("Test"), TableName("[TestSamples]")]
public sealed class TestSampleRow : Row<TestSampleRow.RowFields>, IIdRow, INameRow
{
    [Identity, IdProperty]
    public int? Id { get => fields.Id[this]; set => fields.Id[this] = value; }

    [Size(50), QuickSearch, NameProperty]
    public string? Code { get => fields.Code[this]; set => fields.Code[this] = value; }

    public TestSampleRow() : base() { }

    // Use the shared RowFields instance from Row<TFields> so that Serenity's
    // RowFieldsBase infrastructure populates the field members (Id, Code, etc.)
    // before any test accesses them via the static Fields property.
    public new static readonly RowFields Fields = (RowFields)new TestSampleRow().GetFields();

    public sealed class RowFields : RowFieldsBase
    {
#pragma warning disable CS8618 // Serenity populates these fields via RowFieldsBase infrastructure
        public Int32Field Id;
        public StringField Code;
#pragma warning restore CS8618
    }
}
