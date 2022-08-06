using FluentAssertions;
using Xunit;

namespace Ardalis.SmartEnum.SourceGeneration.IntegrationTests;

public class SmartEnumSourceGenerationTests
{

    [Fact]
    public void AllFieldsAreGenerated()
    {
        TestEnum.One.Should().NotBeNull();
        TestEnum.Two.Should().NotBeNull();
        TestEnum.Three.Should().NotBeNull();
        TestEnum.Four.Should().NotBeNull();
    }

    [Fact]
    public void ShouldListAllMembers()
    {
        var allMembers = TestEnum.GetAllMembers();
        allMembers.Count.Should().Be(4);
    }

}