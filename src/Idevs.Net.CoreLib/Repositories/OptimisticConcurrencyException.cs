namespace Idevs.Repositories;

/// <summary>
/// Thrown when an UPDATE guarded by a <see cref="RowVersionAttribute"/>
/// field affects zero rows — meaning another caller modified the row
/// after this caller read it. Callers handle this by re-reading the
/// row, re-applying their change, and retrying the UPDATE.
/// </summary>
/// <remarks>
/// Carries enough context to log or surface a useful error: the table
/// name, the row's primary-key value, and the version the failing
/// caller had captured. The actual database row's current
/// <c>RowVersion</c> is intentionally NOT carried — re-reading the row
/// is the only safe way to recover, and exposing the current version
/// here would invite consumers to "retry with this value" without
/// re-reading dependent fields.
/// </remarks>
public sealed class OptimisticConcurrencyException : Exception
{
    /// <summary>
    /// Construct a conflict exception with the contextual fields the
    /// library can report from inside an UPDATE guard.
    /// </summary>
    public OptimisticConcurrencyException(string tableName, object? rowId, long capturedVersion)
        : base(BuildMessage(tableName, rowId, capturedVersion))
    {
        TableName = tableName;
        RowId = rowId;
        CapturedVersion = capturedVersion;
    }

    /// <summary>The name of the table whose UPDATE was guarded.</summary>
    public string TableName { get; }

    /// <summary>
    /// The primary-key value of the row the caller tried to update.
    /// May be <c>null</c> only in the unusual case of a row type without
    /// an Id — in practice always set.
    /// </summary>
    public object? RowId { get; }

    /// <summary>
    /// The <c>RowVersion</c> value the caller had captured when they
    /// read the row. The current database value is higher; re-read the
    /// row to find out by how much.
    /// </summary>
    public long CapturedVersion { get; }

    private static string BuildMessage(string tableName, object? rowId, long capturedVersion) =>
        $"Optimistic concurrency conflict on '{tableName}' " +
        $"(id={rowId ?? "<null>"}, captured RowVersion={capturedVersion}). " +
        "The row was modified by another caller since it was read. " +
        "Re-read the row, re-apply your changes, and retry the UPDATE.";
}
