namespace Idevs.Generators.Abstractions.Diagnostics;

/// <summary>
/// Reserved diagnostic ID ranges for Idevs source generators and analyzers.
/// Consumer-authored generators should pick IDs from <see cref="ConsumerRange"/>
/// to avoid future collisions with our reserved ranges.
/// </summary>
public static class DiagnosticIdRange
{
    /// <summary>Reserved for the CoreLib DI source generator.</summary>
    public const string CoreLibDIRange = "IDEVSGEN001-IDEVSGEN099";

    /// <summary>Reserved for future CoreLib misuse analyzers (Track 2 in 0.8.0).</summary>
    public const string CoreLibAnalyzersRange = "IDEVSGEN100-IDEVSGEN199";

    /// <summary>Recommended range for consumer-authored generator diagnostics.</summary>
    public const string ConsumerRange = "IDEVSGEN200+";
}
