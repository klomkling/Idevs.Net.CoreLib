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
        : RepositoryBase<IntegrationTestRow, int>(sqlConnections);

    public RepositoryWriteIntegrationTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
        _fixture.TruncateTestTable();
        _repo = new TestRepository(fixture.SqlConnections);
    }

    public void Dispose() => _fixture.TruncateTestTable();

    private static readonly IntegrationTestRow.RowFields Fld = IntegrationTestRow.Fields;

    [Fact]
    public void Diagnose_FieldFlags()
    {
        foreach (var f in IntegrationTestRow.Fields)
        {
            var insertable = (f.Flags & FieldFlags.Insertable) == FieldFlags.Insertable;
            var updatable = (f.Flags & FieldFlags.Updatable) == FieldFlags.Updatable;
            var notMapped = (f.Flags & FieldFlags.NotMapped) != 0;
            var calculated = (f.Flags & FieldFlags.Calculated) != 0;
            var foreign = (f.Flags & FieldFlags.Foreign) != 0;
            Console.WriteLine(
                $"{f.Name}: Flags={f.Flags} Insertable={insertable} Updatable={updatable} " +
                $"NotMapped={notMapped} Calculated={calculated} Foreign={foreign}");
        }
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
    /// And the cure: <see cref="RepositoryBase{TRow}.CreateExcludingAsync"/>
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
}
