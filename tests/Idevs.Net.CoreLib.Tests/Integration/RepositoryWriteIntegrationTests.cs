using Idevs.Repositories;
using Serenity.Data;

namespace Idevs.Net.CoreLib.Tests.Integration;

/// <summary>
/// End-to-end repository write tests against a real SQL Server (Testcontainers).
/// Verifies that Serenity's auto-exclusion of NotMapped/Expression fields
/// holds for the new helpers, and that the include/exclude overloads emit
/// the expected column lists.
/// </summary>
[Collection(nameof(MsSqlContainerCollection))]
[Trait("Category", "Integration")]
public sealed class RepositoryWriteIntegrationTests : IDisposable
{
    private readonly MsSqlContainerFixture _fixture;
    private readonly TestRepository _repo;

    private sealed class TestRepository(ISqlConnections sqlConnections)
        : RowRepositoryBase<IntegrationTestRow, int>(sqlConnections);

    public RepositoryWriteIntegrationTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateTestTable();
        _repo = new TestRepository(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateTestTable();

    private static readonly IntegrationTestRow.RowFields Fld = IntegrationTestRow.Fields;

    /// <summary>
    /// Asserts the field-flag baseline assumed by every other test in this
    /// suite. If Serenity changes how it derives flags from row attributes
    /// (or our row's attribute usage drifts), this test fails first with a
    /// clear message — instead of cascading into N opaque "Invalid column
    /// name" failures elsewhere.
    /// </summary>
    [Fact]
    public void RowFieldFlags_MatchExpectedBaseline()
    {
        static (bool Insertable, bool Updatable, bool NotMapped, bool Calculated, bool Foreign)
            Read(Field f) => (
                (f.Flags & FieldFlags.Insertable) == FieldFlags.Insertable,
                (f.Flags & FieldFlags.Updatable) == FieldFlags.Updatable,
                (f.Flags & FieldFlags.NotMapped) != 0,
                (f.Flags & FieldFlags.Calculated) != 0,
                (f.Flags & FieldFlags.Foreign) != 0);

        // Identity column — auto-managed by SQL Server, neither insertable nor updatable.
        var id = Read(Fld.Id);
        Assert.False(id.Insertable, "Id (identity) must not be Insertable.");
        Assert.False(id.Updatable, "Id (identity) must not be Updatable.");

        // Regular table columns — must be both insertable and updatable.
        foreach (var (name, f) in new[] {
            (nameof(Fld.Code), (Field)Fld.Code),
            (nameof(Fld.Amount), (Field)Fld.Amount),
            (nameof(Fld.Status), (Field)Fld.Status)})
        {
            var r = Read(f);
            Assert.True(r.Insertable, $"{name}: expected Insertable=true.");
            Assert.True(r.Updatable, $"{name}: expected Updatable=true.");
            Assert.False(r.NotMapped, $"{name}: expected NotMapped=false.");
            Assert.False(r.Calculated, $"{name}: expected Calculated=false.");
            Assert.False(r.Foreign, $"{name}: expected Foreign=false.");
        }

        // [Expression]-decorated AmountDoubled — Serenity sets Foreign+Calculated.
        // Note: in this row the attribute does NOT clear Insertable/Updatable
        // automatically; that's the whole point of the expression-trap tests.
        var ad = Read(Fld.AmountDoubled);
        Assert.True(ad.Calculated, "AmountDoubled: expected Calculated=true (from [Expression]).");
        Assert.True(ad.Foreign, "AmountDoubled: expected Foreign=true (from [Expression]).");
    }

    /// <summary>Read columns from the test table via raw ADO.NET — avoids any
    /// Serenity row materialization in the assertions, so the test verifies
    /// what's actually persisted, independent of how it would be re-read.</summary>
    private (string? Code, decimal? Amount, string? Status) ReadRow(int id)
    {
        using var conn = _fixture.SqlConnections.NewByKey("Default");
        conn.EnsureOpen();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code, Amount, Status FROM dbo.IntegrationTestRows WHERE Id = @id";
        var p = cmd.CreateParameter();
        p.ParameterName = "@id";
        p.Value = id;
        cmd.Parameters.Add(p);
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read(), $"Row with Id={id} not found.");
        var code = reader.IsDBNull(0) ? null : reader.GetString(0);
        var amount = reader.IsDBNull(1) ? (decimal?)null : reader.GetDecimal(1);
        var status = reader.IsDBNull(2) ? null : reader.GetString(2);
        return (code, amount, status);
    }

    // ---- Default CreateAsync(row) ----

    [Fact]
    public async Task CreateAsync_RowWithNotMappedFieldAssigned_InsertsOnlyTableColumns()
    {
        // NotMapped property is set (in CLR memory) but Serenity sees no
        // backing Field for it, so the value cannot reach SQL. Expression
        // fields would also reach SQL if you ASSIGN them — so we leave
        // AmountDoubled alone (the production-correct pattern: Expression
        // fields are read-only outputs, not inputs).
        var row = new IntegrationTestRow
        {
            Code = "C-001",
            Amount = 100.50m,
            Status = "Pending",
            TransientNote = "this should NOT be persisted",  // [NotMapped]
        };

        var newId = await _repo.CreateAsync(row);

        Assert.True(newId > 0);

        // Re-read via direct SQL to confirm the persisted row.
        var (code, amount, status) = ReadRow((int)newId);
        Assert.Equal("C-001", code);
        Assert.Equal(100.50m, amount);
        Assert.Equal("Pending", status);
    }

    // ---- Default UpdateAsync(row) ----

    [Fact]
    public async Task UpdateAsync_RowWithNotMappedAssigned_UpdatesOnlyTableColumns()
    {
        // Insert baseline row.
        var seedId = await _repo.CreateAsync(new IntegrationTestRow
        {
            Code = "U-001",
            Amount = 10m,
            Status = "Initial",
        });

        // Mutate including a write to the NotMapped property — Serenity has
        // no Field for it so it cannot be in the UPDATE.
        var update = new IntegrationTestRow
        {
            Id = (int)seedId,
            Status = "Updated",
            TransientNote = "should not persist",
        };

        var ok = await _repo.UpdateAsync(update);
        Assert.True(ok);

        var (code, amount, status) = ReadRow((int)seedId);
        Assert.Equal("U-001", code);          // Code untouched
        Assert.Equal(10m, amount);            // Amount untouched
        Assert.Equal("Updated", status);      // Status updated
    }

    // ---- Include-only CreateAsync(row, fields) ----

    [Fact]
    public async Task CreateAsync_WithExplicitFields_InsertsOnlyListedColumns()
    {
        var row = new IntegrationTestRow
        {
            Code = "I-001",
            Amount = 999m,                       // assigned but NOT in include list
            Status = "Hidden",                   // assigned but NOT in include list
        };

        var newId = await _repo.CreateAsync(row, [Fld.Code, Fld.Amount]);
        Assert.True(newId > 0);

        var (code, amount, status) = ReadRow((int)newId);
        Assert.Equal("I-001", code);
        Assert.Equal(999m, amount);
        Assert.Null(status);   // not in include list — DB default (null) wins
    }

    // ---- Include-only UpdateAsync(row, fields) ----

    [Fact]
    public async Task UpdateAsync_WithExplicitFields_UpdatesOnlyListedColumns()
    {
        var seedId = await _repo.CreateAsync(new IntegrationTestRow
        {
            Code = "I-002",
            Amount = 50m,
            Status = "Initial",
        });

        var update = new IntegrationTestRow
        {
            Id = (int)seedId,
            Code = "WRONG-CODE",
            Amount = 75m,
            Status = "ShouldBePreserved",
        };

        // Only Amount is in the include list, so Code and Status must NOT change.
        var ok = await _repo.UpdateAsync(update, [Fld.Amount]);
        Assert.True(ok);

        var (code, amount, status) = ReadRow((int)seedId);
        Assert.Equal("I-002", code);          // unchanged
        Assert.Equal(75m, amount);            // updated
        Assert.Equal("Initial", status);      // unchanged
    }

    // ---- Exclude CreateExcludingAsync(row, excludeFields) ----

    [Fact]
    public async Task CreateExcludingAsync_OmitsListedFields_AndStillSkipsNotMapped()
    {
        var row = new IntegrationTestRow
        {
            Code = "E-001",
            Amount = 200m,
            Status = "WillBeOmitted",
            TransientNote = "still ignored",  // [NotMapped] — never reaches SQL
        };

        // Exclude Status explicitly; Code and Amount should be inserted.
        var newId = await _repo.CreateExcludingAsync(row, [Fld.Status]);
        Assert.True(newId > 0);

        var (code, amount, status) = ReadRow((int)newId);
        Assert.Equal("E-001", code);
        Assert.Equal(200m, amount);
        Assert.Null(status);
    }

    // ---- Exclude UpdateExcludingAsync(row, excludeFields) ----

    [Fact]
    public async Task UpdateExcludingAsync_OmitsListedFields_AndStillSkipsNotMapped()
    {
        var seedId = await _repo.CreateAsync(new IntegrationTestRow
        {
            Code = "E-002",
            Amount = 1m,
            Status = "PreservedStatus",
        });

        var update = new IntegrationTestRow
        {
            Id = (int)seedId,
            Code = "WRONG-CODE",                  // assigned, but excluded below
            Amount = 5m,                          // should update
            Status = "WRONG-STATUS",              // assigned, but excluded below
            TransientNote = "ignored",            // [NotMapped] — never reaches SQL
        };

        var ok = await _repo.UpdateExcludingAsync(update, [Fld.Code, Fld.Status]);
        Assert.True(ok);

        var (code, amount, status) = ReadRow((int)seedId);
        Assert.Equal("E-002", code);                // excluded — preserved
        Assert.Equal(5m, amount);                   // updated
        Assert.Equal("PreservedStatus", status);    // excluded — preserved
    }

    // ---- Argument guards ----

    [Fact]
    public async Task CreateAsync_NullFields_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() =>
               _repo.CreateAsync(new IntegrationTestRow { Code = "x" }, fields: null!));

    [Fact]
    public async Task CreateAsync_EmptyFields_Throws()
        => await Assert.ThrowsAsync<ArgumentException>(() =>
               _repo.CreateAsync(new IntegrationTestRow { Code = "x" }, fields: []));

    [Fact]
    public async Task UpdateAsync_WithFieldsButMissingId_Throws()
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
               _repo.UpdateAsync(new IntegrationTestRow { Code = "x" }, [Fld.Code]));

    [Fact]
    public async Task UpdateExcludingAsync_MissingId_Throws()
        => await Assert.ThrowsAsync<InvalidOperationException>(() =>
               _repo.UpdateExcludingAsync(new IntegrationTestRow { Code = "x" }, [Fld.Status]));

    /// <summary>
    /// Documents Serenity's actual behavior: an Expression-decorated field
    /// CAN end up in the INSERT/UPDATE if its value is "assigned" on the row.
    /// This is per Serenity's IsAssigned-based filter — Expression fields are
    /// auto-skipped when un-assigned (the production-correct case: they're
    /// read-only outputs of a SELECT projection), but become writable when
    /// you set them. The fix is either: (1) don't assign them, (2) use
    /// CreateExcludingAsync / UpdateExcludingAsync to opt them out, or
    /// (3) use the include-only CreateAsync(row, fields) overload to control
    /// the column list explicitly.
    /// </summary>
    [Fact]
    public async Task CreateAsync_AssignedExpressionField_FailsAtSqlBecauseExpressionIsNotARealColumn()
    {
        var row = new IntegrationTestRow
        {
            Code = "X-001",
            Amount = 1m,
            AmountDoubled = 9999m,   // Expression field — assigning it puts it in INSERT
        };

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            _repo.CreateAsync(row));
    }

    /// <summary>
    /// And the cure: <see cref="RowRepositoryBase{TRow}.CreateExcludingAsync"/>
    /// lets the caller drop the Expression field even when it was assigned.
    /// </summary>
    [Fact]
    public async Task CreateExcludingAsync_DropsAssignedExpressionField()
    {
        var row = new IntegrationTestRow
        {
            Code = "X-002",
            Amount = 1m,
            AmountDoubled = 9999m,   // assigned, but excluded below
        };

        var newId = await _repo.CreateExcludingAsync(row, [Fld.AmountDoubled]);
        Assert.True(newId > 0);

        var (code, amount, _) = ReadRow((int)newId);
        Assert.Equal("X-002", code);
        Assert.Equal(1m, amount);
    }

    /// <summary>
    /// Symmetric trap on the UPDATE path: assigning an Expression field on a
    /// row passed to UpdateAsync(row) puts it in the SET list and SQL Server
    /// rejects the statement. Documents that the trap is not INSERT-only.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_AssignedExpressionField_FailsAtSqlBecauseExpressionIsNotARealColumn()
    {
        var seedId = await _repo.CreateAsync(new IntegrationTestRow
        {
            Code = "X-003",
            Amount = 2m,
            Status = "Initial",
        });

        var update = new IntegrationTestRow
        {
            Id = (int)seedId,
            Status = "Updated",
            AmountDoubled = 8888m,    // Expression — assigning puts it in SET
        };

        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            _repo.UpdateAsync(update));
    }

    /// <summary>
    /// And the cure on the UPDATE path:
    /// <see cref="RowRepositoryBase{TRow, TKey}.UpdateExcludingAsync"/> drops
    /// the assigned Expression field before building the SET list.
    /// </summary>
    [Fact]
    public async Task UpdateExcludingAsync_DropsAssignedExpressionField()
    {
        var seedId = await _repo.CreateAsync(new IntegrationTestRow
        {
            Code = "X-004",
            Amount = 3m,
            Status = "Initial",
        });

        var update = new IntegrationTestRow
        {
            Id = (int)seedId,
            Status = "Updated",
            AmountDoubled = 7777m,    // assigned, but excluded below
        };

        var ok = await _repo.UpdateExcludingAsync(update, [Fld.AmountDoubled]);
        Assert.True(ok);

        var (code, amount, status) = ReadRow((int)seedId);
        Assert.Equal("X-004", code);          // unchanged
        Assert.Equal(3m, amount);             // unchanged
        Assert.Equal("Updated", status);      // updated
    }

    /// <summary>
    /// Validation guard on UpdateAsync(row, fields): cannot include the Id
    /// column in the SET list. The Id belongs in WHERE.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WithFieldsContainingIdField_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _repo.UpdateAsync(
                new IntegrationTestRow { Id = 1, Code = "x" },
                [Fld.Id, Fld.Code]));
        Assert.Contains("Id", ex.Message);
    }

    /// <summary>
    /// Validation guard on CreateAsync(row, fields): cannot include
    /// non-insertable fields (identity column, NotMapped, Expression with
    /// Insertable=false, etc.).
    /// </summary>
    [Fact]
    public async Task CreateAsync_WithFieldsContainingNonInsertable_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _repo.CreateAsync(
                new IntegrationTestRow { Code = "x" },
                [Fld.Code, Fld.Id])); // Id is identity, not insertable
        Assert.Contains("Id", ex.Message);
    }
}
