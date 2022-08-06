using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ardalis.SmartEnum.SourceGeneration;

[Generator]
public sealed class SmartEnumGenerator : IIncrementalGenerator
{

    private const string kSmartEnumAttributeTypeFullName = "Ardalis.SmartEnum.SmartEnumAttribute";
    private const string kEnumMemberAttributeTypeFullName = "Ardalis.SmartEnum.EnumMemberAttribute";
    private const string kSmartEnumTypeFullName = "Ardalis.SmartEnum.SmartEnum`2";
    private const string kSmartFlagEnumTypeFullName = "Ardalis.SmartEnum.SmartFlagEnum`2";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var typeDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => IsSyntaxTargetForGeneration(node),
                GetSemanticTargetForGeneration
            )
            .Where(static s => s != null);

        var compilationAndDeclarations = context.CompilationProvider.Combine(typeDeclarations.Collect());
        context.RegisterSourceOutput(
            compilationAndDeclarations,
            static (spc, source) => Generate(source.Left, source.Right, spc)
        );
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 } classDeclarationSyntax &&
            classDeclarationSyntax.Modifiers.Any(SyntaxKind.PartialKeyword) &&
            !classDeclarationSyntax.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
            classDeclarationSyntax.TypeParameterList is not { Parameters.Count: > 0 };

    private static ClassDeclarationSyntax GetSemanticTargetForGeneration(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax) context.Node;
        return HasSmartEnumAttribute(context, classDeclarationSyntax, cancellationToken)
            ? classDeclarationSyntax
            : null;
    }

    private static void Generate(
        Compilation compilation,
        ImmutableArray<ClassDeclarationSyntax> discoveredClassDeclarations,
        in SourceProductionContext context)
    {
        if (discoveredClassDeclarations.IsDefaultOrEmpty)
        {
            return;
        }
        
        SymbolCache.Object = compilation.GetSpecialType(SpecialType.System_Object);
        SymbolCache.Int32 = compilation.GetSpecialType(SpecialType.System_Int32);
        SymbolCache.String = compilation.GetSpecialType(SpecialType.System_String);
        
        // Ensure that the attributes can be found in the current compilation context. We need to keep in mind
        // that these should be the exact names of the types. The only reason these could be null is if we
        // make the source generator its own package and these attributes are defined in the `SmartEnum` package but
        // said package is not included as a dependency for the project that is being compiled at the time
        SymbolCache.SmartEnumAttributeSymbol = compilation.GetTypeByMetadataName(kSmartEnumAttributeTypeFullName);
        SymbolCache.EnumMemberAttributeSymbol = compilation.GetTypeByMetadataName(kEnumMemberAttributeTypeFullName);
        SymbolCache.SmartEnumSymbol = compilation.GetTypeByMetadataName(kSmartEnumTypeFullName);
        SymbolCache.SmartFlagEnumSymbol = compilation.GetTypeByMetadataName(kSmartFlagEnumTypeFullName);
        if (SymbolCache.SmartEnumAttributeSymbol == null || SymbolCache.EnumMemberAttributeSymbol == null ||
            SymbolCache.SmartEnumSymbol == null || SymbolCache.SmartFlagEnumSymbol == null)
        {
            return;
        }

        foreach (var classDeclarationSyntax in discoveredClassDeclarations)
        {
            var semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            var typeSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax, context.CancellationToken);
            if (typeSymbol == null)
            {
                continue;
            }

            SmartEnumSourceEmitter.Emit(
                new SmartEnumGenerationContext(
                    typeSymbol,
                    GetAllEnumMembers(typeSymbol)
                ),
                context
            );
        }
    }

    private static ImmutableArray<string> GetAllEnumMembers(INamespaceOrTypeSymbol classSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var memberSymbol in classSymbol.GetMembers())
        {
            if (!HasAttribute(memberSymbol, SymbolCache.EnumMemberAttributeSymbol))
            {
                continue;
            }

            switch (memberSymbol)
            {
                case IFieldSymbol { IsConst: false, IsStatic: true } fieldSymbol when
                    fieldSymbol.Type.Equals(classSymbol, SymbolEqualityComparer.Default):
                    builder.Add(fieldSymbol.Name);
                    break;
                case IPropertySymbol { IsStatic: true } propertySymbol when
                    propertySymbol.Type.Equals(classSymbol, SymbolEqualityComparer.Default):
                    builder.Add(propertySymbol.Name);
                    break;
            }
        }

        return builder.Count > 0 ? builder.ToImmutable() : ImmutableArray<string>.Empty;
    }

    private static bool HasSmartEnumAttribute(
        in GeneratorSyntaxContext context,
        MemberDeclarationSyntax node,
        CancellationToken cancellationToken = default)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        foreach (var attribute in node.AttributeLists.SelectMany(x => x.Attributes))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var symbolInfo = context.SemanticModel.GetSymbolInfo(attribute, cancellationToken);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                // Weird, we couldn't get the symbol, ignore it
                continue;
            }

            // Is this the attribute we are looking for?
            var attributeContainingTypeSymbol = methodSymbol.ContainingType;
            var fullName = attributeContainingTypeSymbol.ToDisplayString();
            if (!string.Equals(kSmartEnumAttributeTypeFullName, fullName, StringComparison.Ordinal))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool HasAttribute(ISymbol symbol, ISymbol typeSymbol)
    {
        foreach (var attributeData in symbol.GetAttributes())
        {
            if (typeSymbol.Equals(attributeData.AttributeClass, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }

        return false;
    }

}