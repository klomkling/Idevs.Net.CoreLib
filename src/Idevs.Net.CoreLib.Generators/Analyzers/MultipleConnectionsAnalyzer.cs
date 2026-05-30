using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Idevs.Net.CoreLib.Generators.Analyzers;

/// <summary>IDEVSGEN100: flags methods that open two or more database connections
/// without a shared UnitOfWork.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MultipleConnectionsAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.MultipleConnectionsWithoutUnitOfWork);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Body is null && method.ExpressionBody is null)
            return;

        var model = context.SemanticModel;
        var opens = method.DescendantNodes().Count(n => IsConnectionOpen(n, model));
        if (opens < 2)
            return;

        var hasUow = method.DescendantNodes().Any(n => ReferencesUnitOfWork(n, model));
        if (hasUow)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.MultipleConnectionsWithoutUnitOfWork,
            method.Identifier.GetLocation(),
            method.Identifier.Text,
            opens.ToString()));
    }

    private static bool IsConnectionOpen(SyntaxNode node, SemanticModel model)
    {
        switch (node)
        {
            case InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax ma:
            {
                var name = ma.Name.Identifier.Text;
                if (name != "NewByKey" && name != "NewFor")
                    return false;
                var receiverType = model.GetTypeInfo(ma.Expression).Type;
                return receiverType is not null && SimpleNameMatches(receiverType, "ISqlConnections");
            }
            case ObjectCreationExpressionSyntax oc:
            {
                var created = model.GetTypeInfo(oc).Type;
                return created is not null && created.Name.EndsWith("Connection", System.StringComparison.Ordinal)
                       && created.Name != "Connection";
            }
            default:
                return false;
        }
    }

    private static bool ReferencesUnitOfWork(SyntaxNode node, SemanticModel model)
    {
        ITypeSymbol? type = node switch
        {
            ObjectCreationExpressionSyntax oc => model.GetTypeInfo(oc).Type,
            IdentifierNameSyntax id => model.GetTypeInfo(id).Type,
            _ => null,
        };
        return type is not null &&
               (SimpleNameMatches(type, "IUnitOfWork") || SimpleNameMatches(type, "UnitOfWorkScope"));
    }

    private static bool SimpleNameMatches(ITypeSymbol type, string simpleName)
    {
        if (type.Name == simpleName)
            return true;
        return type.AllInterfaces.Any(i => i.Name == simpleName);
    }
}
