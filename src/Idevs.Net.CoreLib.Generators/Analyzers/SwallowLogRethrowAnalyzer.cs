using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Idevs.Net.CoreLib.Generators.Analyzers;

/// <summary>IDEVSGEN101: flags catch blocks that log and rethrow.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SwallowLogRethrowAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.SwallowLogRethrow);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (catchClause.Block is null)
            return;

        var statements = catchClause.Block.Statements;

        var rethrows = statements.OfType<ThrowStatementSyntax>().Any(t =>
            t.Expression is null || t.Expression is IdentifierNameSyntax);
        if (!rethrows)
            return;

        var logs = catchClause.Block.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Any(inv => IsLoggerCall(inv, context.SemanticModel));
        if (!logs)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.SwallowLogRethrow,
            catchClause.CatchKeyword.GetLocation()));
    }

    private static bool IsLoggerCall(InvocationExpressionSyntax inv, SemanticModel model)
    {
        if (inv.Expression is not MemberAccessExpressionSyntax ma)
            return false;
        var name = ma.Name.Identifier.Text;
        if (name != "LogError" && name != "LogCritical")
            return false;

        // Unresolved-receiver posture: lean toward flagging. A LogError/LogCritical
        // call whose receiver type can't bind is treated as a logger call so a real
        // log-and-rethrow isn't missed on an incomplete compilation.
        var receiverType = model.GetTypeInfo(ma.Expression).Type;
        if (receiverType is null)
            return true;
        return receiverType.Name.Contains("Logger")
               || receiverType.AllInterfaces.Any(i => i.Name.Contains("Logger"));
    }
}
