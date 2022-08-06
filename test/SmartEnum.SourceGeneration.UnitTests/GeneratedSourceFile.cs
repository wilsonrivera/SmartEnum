namespace Ardalis.SmartEnum.SourceGeneration.UnitTests;

internal readonly struct GeneratedSourceFile
{

    public GeneratedSourceFile(string fileName, string source)
    {
        FileName = fileName;
        Source = source;
    }

    public string FileName { get; }

    public string Source { get; }

}