using Idevs.Generators.Abstractions.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Idevs.Net.CoreLib.Generators;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor MultipleLifetimeAttributes =
        IdevsDiagnostics.CreateError(
            "IDEVSGEN001",
            "Multiple lifetime attributes",
            "Type '{0}' has more than one lifetime attribute applied. Use exactly one.");

    public static readonly DiagnosticDescriptor MultipleLifetimeMarkers =
        IdevsDiagnostics.CreateError(
            "IDEVSGEN002",
            "Multiple lifetime marker interfaces",
            "Type '{0}' implements more than one lifetime marker interface. Use exactly one.");

    public static readonly DiagnosticDescriptor AttributeMarkerDisagreement =
        IdevsDiagnostics.CreateError(
            "IDEVSGEN003",
            "Attribute and marker lifetime disagree",
            "Type '{0}' has a lifetime attribute and a lifetime marker interface that specify different lifetimes.");

    public static readonly DiagnosticDescriptor RedundantLifetimeAttributeAndMarker =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN004",
            "Redundant lifetime attribute and marker",
            "Type '{0}' specifies the same lifetime via both an attribute and a marker interface. Remove one.");

    public static readonly DiagnosticDescriptor AmbiguousServiceType =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN005",
            "Ambiguous service type",
            "Type '{0}' implements multiple candidate service interfaces. Specify the service type explicitly.");

    public static readonly DiagnosticDescriptor CannotRegister =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN006",
            "Cannot register type",
            "Type '{0}' cannot be registered: {1}.");

    public static readonly DiagnosticDescriptor AttributeServiceTypeConflictsWithGenericMarker =
        IdevsDiagnostics.CreateError(
            "IDEVSGEN007",
            "Attribute service type conflicts with generic marker",
            "Type '{0}' specifies a service type via attribute that conflicts with the service type implied by the generic lifetime marker interface.");

    public static readonly DiagnosticDescriptor RegistrarMissingPublicCtor =
        IdevsDiagnostics.CreateError(
            "IDEVSGEN008",
            "Registrar missing public constructor",
            "Type '{0}' is used as a registrar but has no accessible public constructor.");

    public static readonly DiagnosticDescriptor RegistrarIsInternal =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN009",
            "Registrar is internal",
            "Type '{0}' is an internal registrar. Consider making it public so consumers can invoke it.");

    // 0.9.0: promoted Warning -> Error. Using a legacy registration attribute now
    // fails the build by default; downgrade via .editorconfig
    // (dotnet_diagnostic.IDEVSGEN010.severity = warning) during migration. The
    // generator still registers the type, so a downgraded build keeps working.
    // The attribute types are removed in 1.0.0.
    public static readonly DiagnosticDescriptor LegacyAttributeUsage =
        IdevsDiagnostics.CreateError(
            "IDEVSGEN010",
            "Legacy attribute usage",
            "Type '{0}' uses a legacy registration attribute. Migrate to the current attribute or marker interface.");

    public static readonly DiagnosticDescriptor MultipleConnectionsWithoutUnitOfWork =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN100",
            "Multiple connections without a UnitOfWork",
            "Method '{0}' opens {1} database connections without a shared UnitOfWork; share one connection/UnitOfWork instead.");

    public static readonly DiagnosticDescriptor SwallowLogRethrow =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN101",
            "Catch logs and rethrows",
            "Catch block logs and rethrows; let the top-level handler log it instead.");

    public static readonly DiagnosticDescriptor SyncOverAsync =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN102",
            "Synchronous work wrapped in Task.FromResult",
            "'{0}' wraps synchronous work in Task.FromResult; provide a genuinely async implementation.");

    public static readonly DiagnosticDescriptor ManualSequence =
        IdevsDiagnostics.CreateInfo(
            "IDEVSGEN103",
            "Manual sequence allocation",
            "Manual sequence allocation detected; use ISequenceProvider.NextAsync for atomic, gap-tolerant numbering.");
}
