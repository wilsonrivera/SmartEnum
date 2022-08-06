using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Ardalis.SmartEnum.SourceGeneration.UnitTests;

internal sealed class SmartEnumGeneratorVerifier
{

    private readonly List<SourceText> _sources = new();
    private readonly List<MetadataReference> _references = new();

    private SmartEnumGeneratorVerifier()
    {
        var candidateAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(_ => !_.IsDynamic && !string.IsNullOrWhiteSpace(_.Location))
            .Where(IsCandidateAssembly);

        foreach (var candidate in candidateAssemblies)
        {
            AddReference(candidate);
        }

        AddReferenceOf(typeof(System.Text.Json.JsonSerializer));
        AddReferenceOf<ISmartEnum>();
    }

    private SmartEnumGeneratorVerifier(SmartEnumGeneratorVerifier other)
    {
        _sources.AddRange(other._sources);
        _references.AddRange(other._references);
    }

    private CSharpParseOptions ParseOptions { get; } = new(LanguageVersion.CSharp10);

    private CSharpCompilationOptions CompilationOptions { get; } = new(OutputKind.DynamicallyLinkedLibrary);

    public static SmartEnumGeneratorVerifier New() => new();

    public SmartEnumGeneratorVerifier AddSource(string source)
    {
        AddSource(SourceText.From(source, Encoding.UTF8));
        return this;
    }

    public SmartEnumGeneratorVerifier AddSource(SourceText source)
    {
        _sources.Add(source);
        return this;
    }

    public SmartEnumGeneratorVerifier AddReferenceOf<T>() => AddReferenceOf(typeof(T));

    public SmartEnumGeneratorVerifier AddReferenceOf(Type typeInReferencedAssembly)
        => AddReference(typeInReferencedAssembly.Assembly);

    public SmartEnumGeneratorVerifier AddReference(Assembly assembly)
    {
        _references.Add(MetadataReference.CreateFromFile(assembly.Location));
        return this;
    }

    public GenerationResult Generate()
    {
        var compilation = CreateCompilation();
        var generator = new SmartEnumGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.WithUpdatedParseOptions(compilation.SyntaxTrees.First().Options);

        var originalTreeCount = compilation.SyntaxTrees.Count();
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Id.Equals("CS8785", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(diagnostic.GetMessage());
            }
        }

        var trees = outputCompilation.SyntaxTrees.ToList();
        if (trees.Count == originalTreeCount) return GenerationResult.Empty;

        var generatedFiles = new List<GeneratedSourceFile>();
        var clonedVerifier = new SmartEnumGeneratorVerifier(this);
        var stringBuilder = new StringBuilder();
        foreach (var tree in trees)
        {
            if (string.IsNullOrWhiteSpace(tree.FilePath)) continue;

            var generatedFile = new GeneratedSourceFile(Path.GetFileName(tree.FilePath), tree.ToString());
            generatedFiles.Add(generatedFile);
            clonedVerifier.AddSource(generatedFile.Source);
            stringBuilder.Append("// HintName: ").AppendLine(generatedFile.FileName);
            stringBuilder.Append(generatedFile.Source);
        }

        var generatedOutput = stringBuilder.ToString();
        return new GenerationResult(
            clonedVerifier.CreateCompilation(),
            generatedOutput,
            generatedFiles.ToImmutableArray()
        );
    }

    private Compilation CreateCompilation()
    {
        return CSharpCompilation.Create(
            "compilation",
            _sources.Select(source => CSharpSyntaxTree.ParseText(source, ParseOptions)),
            _references,
            CompilationOptions
        );
    }

    private static bool IsCandidateAssembly(Assembly _)
        => _.FullName?.StartsWith("System.") is true ||
            _.FullName?.StartsWith("Microsoft.") is true ||
            _.FullName?.StartsWith("netstandard") is true;

}