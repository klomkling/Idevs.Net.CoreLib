using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Idevs.Net.CoreLib.Generators.Analyzers;

/// <summary>IDEVSGEN102: flags Task-returning methods whose body is
/// Task.FromResult(syncCall()).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SyncOverAsyncAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.SyncOverAsync);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Exact name match: both Task and Task<T> report Name == "Task", so this
        // covers the generic case without matching unrelated user types like
        // TaskQueue/TaskResult.
        var returnType = context.SemanticModel.GetTypeInfo(method.ReturnType).Type;
        if (returnType is null || returnType.Name != "Task")
            return;

        var inner = ExtractSingleReturnExpression(method);
        if (inner is not InvocationExpressionSyntax fromResult || !IsTaskFromResult(fromResult, context.SemanticModel))
            return;

        var arg = fromResult.ArgumentList.Arguments.Count == 1
            ? fromResult.ArgumentList.Arguments[0].Expression
            : null;
        if (arg is not InvocationExpressionSyntax)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.SyncOverAsync, method.Identifier.GetLocation(), method.Identifier.Text));
    }

    private static ExpressionSyntax? ExtractSingleReturnExpression(MethodDeclarationSyntax method)
    {
        if (method.ExpressionBody is not null)
            return method.ExpressionBody.Expression;
        if (method.Body is { Statements.Count: 1 } body && body.Statements[0] is ReturnStatementSyntax ret)
            return ret.Expression;
        return null;
    }

    private static bool IsTaskFromResult(InvocationExpressionSyntax inv, SemanticModel model)
    {
        if (inv.Expression is not MemberAccessExpressionSyntax ma || ma.Name.Identifier.Text != "FromResult")
            return false;
        // Unresolved-symbol posture: lean toward flagging. If the symbol can't bind
        // (incomplete compilation), treat `X.FromResult(call())` as Task.FromResult
        // rather than silently skipping a likely-real occurrence.
        var symbol = model.GetSymbolInfo(inv).Symbol;
        return symbol is null || symbol.ContainingType?.Name == "Task";
    }
}
