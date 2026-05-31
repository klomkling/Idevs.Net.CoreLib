using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Idevs.Net.CoreLib.Generators.Tests.Analyzers;

// Thin aliases so each test reads Verify<TAnalyzer>.Test / VerifyFix<TAnalyzer,TCodeFix>.Test.
public static class Verify<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    public sealed class Test : CSharpAnalyzerTest<TAnalyzer, XUnitVerifier> { }
}

public static class VerifyFix<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    public sealed class Test : CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier> { }
}
