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

    public static readonly DiagnosticDescriptor LegacyAttributeUsage =
        IdevsDiagnostics.CreateWarning(
            "IDEVSGEN010",
            "Legacy attribute usage",
            "Type '{0}' uses a legacy registration attribute. Migrate to the current attribute or marker interface.");
}
