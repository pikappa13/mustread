using MustRead.App.Utils;
using Xunit;

namespace MustRead.Tests.Utils;

public class PathSafetyTests
{
    [Theory]
    [InlineData("mario")]
    [InlineData("PC-UFFICIO01")]
    [InlineData("DOMAIN")]
    [InlineData("nome.cognome")]
    public void IsSafeSegment_AcceptsNormalNames(string value)
        => Assert.True(PathSafety.IsSafeSegment(value));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(@"a\b")]
    [InlineData("a/b")]
    [InlineData("a:b")]
    [InlineData(" padded")]
    [InlineData(null)]
    public void IsSafeSegment_RejectsTraversalAndInvalidNames(string? value)
        => Assert.False(PathSafety.IsSafeSegment(value));

    [Fact]
    public void EnsureSafeSegment_Throws_OnUnsafeValue()
        => Assert.Throws<ArgumentException>(() => PathSafety.EnsureSafeSegment(".."));
}
