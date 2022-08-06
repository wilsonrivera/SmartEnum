using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ardalis.SmartEnum.SourceGeneration;

internal readonly struct SmartEnumGenerationContext
{

    public SmartEnumGenerationContext(INamedTypeSymbol symbol, ImmutableArray<string> enumMembers)
    {
        Symbol = symbol;
        Name = symbol.Name;
        EnumMembers = enumMembers;
        ContainingTypeQueue = CreateContainingTypeQueue(symbol);
        IsInGlobalNamespace = symbol.ContainingNamespace.IsGlobalNamespace;
        Namespace = IsInGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();
        InheritsFromSmartEnum = FindImplementedSmartEnumType(symbol, out var isFlagEnum, out var valueTypeSymbol);
        IsFlagEnum = isFlagEnum;
        ValueTypeSymbol = valueTypeSymbol ?? SymbolCache.Int32;
        HasConstructor = IsConstructorAvailable(symbol, ValueTypeSymbol);
    }

    public INamedTypeSymbol Symbol { get; }

    public string Name { get; }

    public bool IsInGlobalNamespace { get; }

    public string Namespace { get; }

    public ImmutableArray<string> EnumMembers { get; }

    public Queue<INamedTypeSymbol> ContainingTypeQueue { get; }

    public bool InheritsFromSmartEnum { get; }

    public bool IsFlagEnum { get; }

    public ITypeSymbol ValueTypeSymbol { get; }

    public bool HasConstructor { get; }

    private static Queue<INamedTypeSymbol> CreateContainingTypeQueue(INamedTypeSymbol symbol)
    {
        if (symbol.ContainingType == null)
        {
            return null;
        }

        var currentSymbol = symbol;
        var queue = new Queue<INamedTypeSymbol>();
        while (currentSymbol.ContainingType != null)
        {
            queue.Enqueue(currentSymbol.ContainingType);
            currentSymbol = currentSymbol.ContainingType;
        }

        return queue;
    }

    private static ImmutableArray<INamedTypeSymbol> GetAllInheritedTypes(INamedTypeSymbol symbol)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        builder.AddRange(symbol.AllInterfaces);

        var currentSymbol = symbol;
        while (currentSymbol.BaseType != null)
        {
            if (SymbolCache.Object.Equals(currentSymbol.BaseType, SymbolEqualityComparer.Default))
            {
                break;
            }

            builder.Add(currentSymbol.BaseType);
            currentSymbol = currentSymbol.BaseType;
        }

        return builder.Count > 0 ? builder.ToImmutable() : ImmutableArray<INamedTypeSymbol>.Empty;
    }

    private static bool FindImplementedSmartEnumType(
        INamedTypeSymbol symbol,
        out bool isFlagEnum,
        out ITypeSymbol valueTypeSymbol)
    {
        isFlagEnum = false;
        valueTypeSymbol = null;
        foreach (var type in GetAllInheritedTypes(symbol))
        {
            var originalDefinition = type.OriginalDefinition;
            if (originalDefinition.Equals(SymbolCache.SmartFlagEnumSymbol, SymbolEqualityComparer.Default))
            {
                isFlagEnum = true;
                valueTypeSymbol = type.TypeArguments[1];
                return true;
            }

            if (!originalDefinition.Equals(SymbolCache.SmartEnumSymbol, SymbolEqualityComparer.Default))
            {
                continue;
            }

            valueTypeSymbol = type.TypeArguments[1];
            return true;
        }

        return false;
    }

    private static bool IsConstructorAvailable(INamedTypeSymbol symbol, ISymbol valueTypeSymbol)
    {
        foreach (var ctor in symbol.Constructors)
        {
            if (ctor.IsStatic || ctor.Parameters.Length < 2 ||
                !ctor.Parameters[0].Type.Equals(SymbolCache.String, SymbolEqualityComparer.Default) ||
                !ctor.Parameters[1].Type.Equals(valueTypeSymbol, SymbolEqualityComparer.Default) ||
                (ctor.Parameters.Length > 2 && !ctor.Parameters[2].IsOptional))
            {
                continue;
            }

            return true;
        }

        return false;
    }

}