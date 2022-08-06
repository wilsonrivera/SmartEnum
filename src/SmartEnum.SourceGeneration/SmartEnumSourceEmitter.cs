using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Ardalis.SmartEnum.SourceGeneration
{

    internal static class SmartEnumSourceEmitter
    {

        private static readonly StringBuilder sHintNameBuilder = new(64); // This is used to construct the file name

        private static readonly UsingDirectiveSyntax sSystemCollectionsGenericDirective =
            UsingDirective(IdentifierName("System.Collections.Generic"));
        private static readonly UsingDirectiveSyntax sArdalisSmartEnumDirective =
            UsingDirective(IdentifierName("Ardalis.SmartEnum"));

        private static readonly GenericNameSyntax sReadOnlyCollectionName = GenericName("IReadOnlyCollection");
        private static readonly GenericNameSyntax sSmartEnumName = GenericName("SmartEnum");

        private static readonly IdentifierNameSyntax sNameOfIdentifier = IdentifierName(Identifier(
            default,
            SyntaxKind.NameOfKeyword,
            "nameof",
            "nameof",
            default
        ));

        private static readonly Dictionary<SpecialType, TypeSyntax> sSpecialTypeToTypeMap =
            new()
            {
                [SpecialType.System_String] = PredefinedType(Token(SyntaxKind.StringKeyword)),

                [SpecialType.System_Byte] = PredefinedType(Token(SyntaxKind.ByteKeyword)),
                [SpecialType.System_SByte] = PredefinedType(Token(SyntaxKind.SByteKeyword)),
                [SpecialType.System_Int16] = PredefinedType(Token(SyntaxKind.ShortKeyword)),
                [SpecialType.System_Int32] = PredefinedType(Token(SyntaxKind.IntKeyword)),
                [SpecialType.System_Int64] = PredefinedType(Token(SyntaxKind.LongKeyword)),
                [SpecialType.System_UInt16] = PredefinedType(Token(SyntaxKind.UIntKeyword)),
                [SpecialType.System_UInt32] = PredefinedType(Token(SyntaxKind.IntKeyword)),
                [SpecialType.System_UInt64] = PredefinedType(Token(SyntaxKind.ULongKeyword)),
                [SpecialType.System_Decimal] = PredefinedType(Token(SyntaxKind.DecimalKeyword)),
                [SpecialType.System_Double] = PredefinedType(Token(SyntaxKind.DoubleKeyword)),
                [SpecialType.System_Single] = PredefinedType(Token(SyntaxKind.FloatKeyword)),
            };

        public static void Emit(
            in SmartEnumGenerationContext generationContext,
            in SourceProductionContext sourceProductionContext)
        {
            if (generationContext.EnumMembers.IsDefaultOrEmpty)
            {
                return;
            }

            // Make sure that we can handle the value type of the enum, it'd be really hard to
            // automatically generate values for something like a Guid without it changing every
            // time the generator is executed
            if (!sSpecialTypeToTypeMap.TryGetValue(generationContext.ValueTypeSymbol.SpecialType, out _))
            {
                return;
            }

            //
            var usingDirectives = new List<UsingDirectiveSyntax> { sSystemCollectionsGenericDirective };
            MemberDeclarationSyntax memberDeclaration = GetTypeDeclaration(generationContext.Symbol)
                .WithMembers(GetTypeMembers(generationContext));

            if (!generationContext.InheritsFromSmartEnum)
            {
                usingDirectives.Add(sArdalisSmartEnumDirective);
                memberDeclaration = ((TypeDeclarationSyntax) memberDeclaration)
                    .WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                        SimpleBaseType(MakeGeneric(sSmartEnumName, ParseTypeName(generationContext.Name)))
                    )));
            }

            // He need to resolve the name under which the file will be generated as, this is to prevent generating
            // conflicts when more than one type with the same name exists under different namespaces, or even
            // nested in other types
            //
            // We are also wrapping the type declaration with all the proper containing types, and finally the
            // containing namespace (if any)
            sHintNameBuilder.Clear();
            while (generationContext.ContainingTypeQueue is { Count: > 0 })
            {
                var containingType = generationContext.ContainingTypeQueue.Dequeue();
                sHintNameBuilder.Insert(0, '_').Insert(0, containingType.Name);
                memberDeclaration = GetTypeDeclaration(containingType)
                    .WithMembers(SingletonList(memberDeclaration));
            }

            if (!generationContext.IsInGlobalNamespace)
            {
                sHintNameBuilder.Insert(0, '.').Insert(0, generationContext.Namespace);
                memberDeclaration = NamespaceDeclaration(IdentifierName(generationContext.Namespace))
                    .WithMembers(SingletonList(memberDeclaration));
            }

            var hintName = sHintNameBuilder.Append(generationContext.Symbol.Name).Append(".SmartEnum.g.cs").ToString();

            // We are using a compilation unit so we can add the `using` directives as it is not guaranteed that
            // the type we are generating source for is contained in a namespace and we can't add these directives
            // directly to the generated type
            var compilationUnit = CompilationUnit()
                .WithUsings(usingDirectives.Count == 1 ? SingletonList(usingDirectives[0]) : List(usingDirectives))
                .WithMembers(SingletonList(memberDeclaration));

            var formattedUnit = compilationUnit.NormalizeWhitespace();

            sourceProductionContext.AddSource(hintName, formattedUnit.ToFullString());
        }

        private static TypeDeclarationSyntax GetTypeDeclaration(INamedTypeSymbol typeSymbol)
        {
            TypeDeclarationSyntax declaration;
            if (typeSymbol.IsRecord)
            {
                declaration = RecordDeclaration(Token(SyntaxKind.RecordKeyword), typeSymbol.Name);
                if (typeSymbol.TypeKind == TypeKind.Struct)
                {
                    declaration = ((RecordDeclarationSyntax) declaration).WithClassOrStructKeyword(
                        Token(SyntaxKind.StructKeyword));
                }
            }
            else if (typeSymbol.TypeKind == TypeKind.Struct)
            {
                declaration = StructDeclaration(typeSymbol.Name);
            }
            else
            {
                declaration = ClassDeclaration(typeSymbol.Name);
            }

            declaration = declaration.WithModifiers(GetTypeModifiers(typeSymbol));
            if (typeSymbol.TypeParameters.Length == 0)
            {
                return declaration;
            }

            var typeParameterList = new List<TypeParameterSyntax>();
            foreach (var typeParameter in typeSymbol.TypeParameters)
            {
                typeParameterList.Add(TypeParameter(typeParameter.Name));
            }

            var separatedParametersList = typeParameterList.Count > 1
                ? SeparatedList(typeParameterList)
                : SingletonSeparatedList(typeParameterList[0]);

            return declaration.WithTypeParameterList(TypeParameterList(separatedParametersList));
        }

        private static SyntaxTokenList GetTypeModifiers(ITypeSymbol typeSymbol)
        {
            var tokens = new List<SyntaxToken>();
            switch (typeSymbol.DeclaredAccessibility)
            {
                case Accessibility.Private:
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
                    break;
                case Accessibility.Protected:
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    break;
                case Accessibility.Internal:
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    break;
                case Accessibility.Public:
                    tokens.Add(Token(SyntaxKind.PublicKeyword));
                    break;
                case Accessibility.ProtectedAndInternal:
                    tokens.Add(Token(SyntaxKind.PrivateKeyword));
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    break;
                case Accessibility.ProtectedOrInternal:
                    tokens.Add(Token(SyntaxKind.ProtectedKeyword));
                    tokens.Add(Token(SyntaxKind.InternalKeyword));
                    break;
            }

            if (typeSymbol.IsSealed && typeSymbol.TypeKind == TypeKind.Class)
            {
                tokens.Add(Token(SyntaxKind.SealedKeyword));
            }
            else if (typeSymbol.IsReadOnly && typeSymbol.TypeKind == TypeKind.Struct)
            {
                tokens.Add(Token(SyntaxKind.ReadOnlyKeyword));
            }

            tokens.Add(Token(SyntaxKind.PartialKeyword));

            return TokenList(tokens);
        }

        private static TypeSyntax MakeGeneric(GenericNameSyntax genericType, TypeSyntax typeParameter)
            => genericType.WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(typeParameter)));

        private static SyntaxTokenList MakeTokenList(params SyntaxKind[] syntaxKinds)
        {
            var list = new List<SyntaxToken>();
            foreach (var token in syntaxKinds)
            {
                list.Add(Token(token));
            }

            return TokenList(list);
        }

        private static ArgumentListSyntax MakeArgumentList(params ExpressionSyntax[] expressions)
        {
            var list = new List<ArgumentSyntax>();
            foreach (var expr in expressions)
            {
                list.Add(Argument(expr));
            }

            return ArgumentList(list.Count == 1 ? SingletonSeparatedList(list[0]) : SeparatedList(list));
        }

        private static SyntaxList<MemberDeclarationSyntax> GetTypeMembers(
            in SmartEnumGenerationContext generationContext)
        {
            var allMembersIdentifier = IdentifierName("_allMembers");
            var readOnlyCollection = MakeGeneric(sReadOnlyCollectionName, ParseTypeName(generationContext.Name));

            var members = new List<MemberDeclarationSyntax>
            {
                FieldDeclaration(
                        VariableDeclaration(
                            readOnlyCollection,
                            SingletonSeparatedList(VariableDeclarator(allMembersIdentifier.Identifier))
                        )
                    )
                    .WithModifiers(MakeTokenList(
                        SyntaxKind.PrivateKeyword,
                        SyntaxKind.StaticKeyword,
                        SyntaxKind.ReadOnlyKeyword
                    )),
                CreateStaticConstructor(generationContext, allMembersIdentifier),
            };

            if (!generationContext.HasConstructor)
            {
                var specialValueType = generationContext.ValueTypeSymbol?.SpecialType ?? SpecialType.System_Int32;
                members.Add(
                    ConstructorDeclaration(generationContext.Name)
                        .WithModifiers(MakeTokenList(SyntaxKind.PrivateKeyword))
                        .WithParameterList(ParameterList(SeparatedList(new[]
                        {
                            Parameter(Identifier("name")).WithType(sSpecialTypeToTypeMap[SpecialType.System_String]),
                            Parameter(Identifier("value")).WithType(sSpecialTypeToTypeMap[specialValueType])
                        })))
                        .WithInitializer(ConstructorInitializer(
                            SyntaxKind.BaseConstructorInitializer,
                            MakeArgumentList(IdentifierName("name"), IdentifierName("value"))
                        ))
                        .WithBody(Block())
                );
            }

            members.Add(
                MethodDeclaration(readOnlyCollection, "GetAllMembers")
                    .WithModifiers(MakeTokenList(SyntaxKind.PublicKeyword, SyntaxKind.StaticKeyword))
                    .WithExpressionBody(ArrowExpressionClause(allMembersIdentifier))
                    .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
            );

            return List(members);
        }

        private static ConstructorDeclarationSyntax CreateStaticConstructor(
            in SmartEnumGenerationContext generationContext,
            ExpressionSyntax allMembersIdentifier)
        {
            var typeName = ParseTypeName(generationContext.Name);
            var statements = new List<StatementSyntax>();
            var allMemberExpressions = new List<ExpressionSyntax>();

            foreach (var member in generationContext.EnumMembers)
            {
                var memberIdentifier = IdentifierName(member);
                var nameOfMember = InvocationExpression(sNameOfIdentifier)
                    .WithArgumentList(MakeArgumentList(memberIdentifier));

                allMemberExpressions.Add(memberIdentifier);

                ExpressionSyntax literalValue;
                if (generationContext.ValueTypeSymbol == null ||
                    !generationContext.ValueTypeSymbol.Equals(SymbolCache.String, SymbolEqualityComparer.Default))
                {
                    literalValue = LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(
                            !generationContext.IsFlagEnum
                                ? statements.Count
                                : statements.Count == 0
                                    ? 0
                                    : 1 << statements.Count - 1
                        )
                    );
                }
                else if (generationContext.ValueTypeSymbol != null &&
                         generationContext.ValueTypeSymbol.Equals(SymbolCache.String, SymbolEqualityComparer.Default))
                {
                    literalValue = nameOfMember;
                }
                else
                {
                    // We should never reach this branch but still this so we can compile without errors
                    continue;
                }

                statements.Add(ExpressionStatement(AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(member),
                    ObjectCreationExpression(typeName)
                        .WithArgumentList(MakeArgumentList(nameOfMember, literalValue))
                )));
            }

            statements.Add(ExpressionStatement(AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                allMembersIdentifier,
                ImplicitArrayCreationExpression(InitializerExpression(
                    SyntaxKind.ArrayInitializerExpression,
                    allMemberExpressions.Count == 1
                        ? SingletonSeparatedList(allMemberExpressions[0])
                        : SeparatedList(allMemberExpressions)
                ))
            )));

            return ConstructorDeclaration(generationContext.Name)
                .WithModifiers(MakeTokenList(SyntaxKind.StaticKeyword))
                .WithBody(Block(statements));
        }

    }

}