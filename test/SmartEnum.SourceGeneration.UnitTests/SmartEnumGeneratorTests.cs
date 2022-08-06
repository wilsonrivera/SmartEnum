using System.Threading.Tasks;
using FluentAssertions;
using VerifyXunit;
using Xunit;

namespace Ardalis.SmartEnum.SourceGeneration.UnitTests;

[UsesVerify]
public class SmartEnumGeneratorTests
{

    [Fact]
    public Task CanGenerateForTypeInGlobalNamespace()
    {
        const string source = @"using Ardalis.SmartEnum;
[SmartEnumAttribute]
public partial class TestEnum
{
    [EnumMember] public static readonly TestEnum One;
    [EnumMember] public static readonly TestEnum Two;
    [EnumMember] public static readonly TestEnum Three;
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task CanGenerateForTypeInNamespace()
    {
        const string source = @"using Ardalis.SmartEnum;
namespace Some.Name.Space
{
    [SmartEnumAttribute]
    public partial class TestEnum
    {
        [EnumMember] public static readonly TestEnum One;
        [EnumMember] public static readonly TestEnum Two;
        [EnumMember] public static readonly TestEnum Three;
    }
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task CanGenerateForDeeplyNestedType()
    {
        const string source = @"using Ardalis.SmartEnum;
namespace Some.Name.Space
{
    public partial class A
    {
        public partial class B
        {
            [SmartEnumAttribute]
            public partial class TestEnum
            {
                [EnumMember] public static readonly TestEnum One;
                [EnumMember] public static readonly TestEnum Two;
                [EnumMember] public static readonly TestEnum Three;
            }
        }
    }
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task CanGenerateFlagsEnum()
    {
        const string source = @"using Ardalis.SmartEnum;
[SmartEnumAttribute]
public partial class TestEnum : SmartFlagEnum<TestEnum>
{
    [EnumMember] public static readonly TestEnum One;
    [EnumMember] public static readonly TestEnum Two;
    [EnumMember] public static readonly TestEnum Three;
    [EnumMember] public static readonly TestEnum Four;
    [EnumMember] public static readonly TestEnum Five;
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task ShouldNotGenerateCtorWhenOneExists()
    {
        const string source = @"using Ardalis.SmartEnum;
[SmartEnumAttribute]
public partial class TestEnum
{
    [EnumMember] public static readonly TestEnum One;
    [EnumMember] public static readonly TestEnum Two;
    [EnumMember] public static readonly TestEnum Three;

    private TestEnum(string name, int value)
        : base(name, value)
    {
    }
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task ShouldGenerateCtorWhenNotValidOneExists()
    {
        const string source = @"using Ardalis.SmartEnum;
[SmartEnumAttribute]
public partial class TestEnum
{
    [EnumMember] public static readonly TestEnum One;
    [EnumMember] public static readonly TestEnum Two;
    [EnumMember] public static readonly TestEnum Three;

    private TestEnum(string name, int value, bool unusedParameter)
        : base(name, value)
    {
    }
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task ShouldNotGenerateCtorWhenCtorWithOptionalParameterExists()
    {
        const string source = @"using Ardalis.SmartEnum;
[SmartEnumAttribute]
public partial class TestEnum
{
    [EnumMember] public static readonly TestEnum One;
    [EnumMember] public static readonly TestEnum Two;
    [EnumMember] public static readonly TestEnum Three;

    private TestEnum(string name, int value, bool unusedParameter = false)
        : base(name, value)
    {
    }
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task CanGenerateForSmartEnumWithCustomType()
    {
        const string source = @"using Ardalis.SmartEnum;
[SmartEnumAttribute]
public partial class TestEnum : SmartEnum<TestEnum, float>
{
    [EnumMember] public static readonly TestEnum One;
    [EnumMember] public static readonly TestEnum Two;
    [EnumMember] public static readonly TestEnum Three;
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    [Fact]
    public Task CanGenerateForMarkedFieldsAndProperties()
    {
        const string source = @"using Ardalis.SmartEnum;
[SmartEnumAttribute]
public partial class TestEnum : SmartEnum<TestEnum, float>
{
    [EnumMember] public static readonly TestEnum One;
    [EnumMember] public static TestEnum Two { get; }
    [EnumMember] public static TestEnum Three { get; }
}";

        var result = Generate(source);
        result.Source.Should().NotBeNullOrWhiteSpace();
        result.GeneratedFiles.Should().HaveCount(1);
        result.EnsureCompiles();
        return result.Verify();
    }

    private static GenerationResult Generate(string source)
        => SmartEnumGeneratorVerifier.New()
            .AddSource(source)
            .Generate();

}