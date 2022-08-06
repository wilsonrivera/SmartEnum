using Microsoft.CodeAnalysis;

namespace Ardalis.SmartEnum.SourceGeneration;

internal static class SymbolCache
{

    public static INamedTypeSymbol Object { get; set; }

    public static INamedTypeSymbol Int32 { get; set; }

    public static INamedTypeSymbol String { get; set; }

    public static INamedTypeSymbol SmartEnumAttributeSymbol { get; set; }

    public static INamedTypeSymbol EnumMemberAttributeSymbol { get; set; }

    public static INamedTypeSymbol SmartEnumSymbol { get; set; }

    public static INamedTypeSymbol SmartFlagEnumSymbol { get; set; }

}