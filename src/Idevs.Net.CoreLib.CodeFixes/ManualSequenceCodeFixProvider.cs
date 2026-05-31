using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Idevs.Net.CoreLib.CodeFixes;

/// <summary>Codefix for IDEVSGEN103: rewrites a manual MAX()+1 expression into an
/// ISequenceProvider.NextAsync call.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualSequenceCodeFixProvider)), Shared]
public sealed class ManualSequenceCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "IDEVSGEN103";

    // The fix scaffolds the call site only: it references an injected
    // `sequenceProvider` and a placeholder key the developer must supply, and (in a
    // sync method) requires making the method async. The title signals this manual
    // follow-up so applying it is never mistaken for a complete, compiling rewrite.
    private const string Title = "Scaffold ISequenceProvider.NextAsync (inject provider, name key, make async)";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        var diagnostic = context.Diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var binary = node as BinaryExpressionSyntax ?? node.FirstAncestorOrSelf<BinaryExpressionSyntax>();
        if (binary is null)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                ct => ReplaceAsync(context.Document, root, binary),
                equivalenceKey: Title),
            diagnostic);
    }

    private static Task<Document> ReplaceAsync(Document document, SyntaxNode root, BinaryExpressionSyntax binary)
    {
        var replacement = SyntaxFactory.ParseExpression("await sequenceProvider.NextAsync(\"TODO-sequence-key\")")
            .WithTriviaFrom(binary);
        var newRoot = root.ReplaceNode(binary, replacement);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
