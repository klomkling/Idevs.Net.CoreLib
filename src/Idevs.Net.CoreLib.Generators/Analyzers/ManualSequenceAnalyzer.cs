using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Idevs.Net.CoreLib.Generators.Analyzers;

/// <summary>IDEVSGEN103: flags hand-rolled MAX()+1 / Count()+1 sequence allocation.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualSequenceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ManualSequence);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AddExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var add = (BinaryExpressionSyntax)context.Node;

        if (add.Right is not LiteralExpressionSyntax lit ||
            !lit.IsKind(SyntaxKind.NumericLiteralExpression) ||
            lit.Token.ValueText != "1")
            return;

        if (!IsMaxOrCountCall(add.Left))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ManualSequence, add.GetLocation()));
    }

    private static bool IsMaxOrCountCall(ExpressionSyntax expr)
    {
        if (expr is not InvocationExpressionSyntax inv || inv.Expression is not MemberAccessExpressionSyntax ma)
            return false;
        var name = ma.Name.Identifier.Text;
        return name is "Max" or "MaxAsync" or "Count" or "CountAsync";
    }
}
