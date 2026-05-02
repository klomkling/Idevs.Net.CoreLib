using System;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Idevs.Generators.Abstractions.Diagnostics;

/// <summary>
/// Factory for <see cref="DiagnosticDescriptor"/> instances with a consistent
/// shape (category, default-enabled, IDEVSGEN ID format).
/// </summary>
public static class IdevsDiagnostics
{
    private const string Category = "Idevs.DI";
    private static readonly Regex IdPattern = new("^IDEVSGEN[0-9]{3,}$", RegexOptions.Compiled);

    public static DiagnosticDescriptor CreateError(string id, string title, string messageFormat)
        => Create(id, title, messageFormat, DiagnosticSeverity.Error);

    public static DiagnosticDescriptor CreateWarning(string id, string title, string messageFormat)
        => Create(id, title, messageFormat, DiagnosticSeverity.Warning);

    public static DiagnosticDescriptor CreateInfo(string id, string title, string messageFormat)
        => Create(id, title, messageFormat, DiagnosticSeverity.Info);

    private static DiagnosticDescriptor Create(string id, string title, string messageFormat, DiagnosticSeverity severity)
    {
        if (id is null || !IdPattern.IsMatch(id))
            throw new ArgumentException($"Diagnostic ID '{id}' must match /^IDEVSGEN[0-9]{{3,}}$/.", nameof(id));

        return new DiagnosticDescriptor(
            id: id,
            title: title,
            messageFormat: messageFormat,
            category: Category,
            defaultSeverity: severity,
            isEnabledByDefault: true);
    }
}
