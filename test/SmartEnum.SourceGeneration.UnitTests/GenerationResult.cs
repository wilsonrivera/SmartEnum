using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using VerifyTests;
using VerifyXunit;

namespace Ardalis.SmartEnum.SourceGeneration.UnitTests;

internal readonly struct GenerationResult
{

    public static GenerationResult Empty = new(null!, string.Empty, ImmutableArray<GeneratedSourceFile>.Empty);

    private readonly Compilation _compilation = null;

    internal GenerationResult(
        Compilation compilation,
        string source,
        ImmutableArray<GeneratedSourceFile> generatedFiles)
    {
        _compilation = compilation;
        Source = source;
        GeneratedFiles = generatedFiles;
    }

    public string Source { get; } = string.Empty;

    public ImmutableArray<GeneratedSourceFile> GeneratedFiles { get; } = ImmutableArray<GeneratedSourceFile>.Empty;

    public void EnsureCompiles()
    {
        if (_compilation is null) return;
        var errors = _compilation.GetDiagnostics()
            .Where(d => !d.IsSuppressed)
            .Where(d =>
                d.Severity == DiagnosticSeverity.Error ||
                d.Severity == DiagnosticSeverity.Warning && d.IsWarningAsError
            )
            .ToList();

        switch (errors.Count)
        {
            case > 1:
                throw new AggregateException("Compilation failed", errors.Select(e => new Exception(e.GetMessage())));
            case 1:
                throw new Exception("Compilation failed: " + errors[0].GetMessage());
        }
    }

    public SettingsTask Verify()
    {
        var callingAssembly = Assembly.GetCallingAssembly();
        var projectDirectory = callingAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(x => x.Key == "Verify.ProjectDirectory")
            ?.Value ?? AttributeReader.GetProjectDirectory();

        return Verifier.Verify(Source).UseDirectory(Path.Combine(projectDirectory, "Snapshots"));
    }

}