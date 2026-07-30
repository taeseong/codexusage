using CodexUsage.Windows.Diagnostics;

namespace CodexUsage.Windows.Tests;

public sealed class BuildProvenanceTests
{
    [Theory]
    [InlineData("0.1.3+0123456789abcdef", "0123456789ab")]
    [InlineData("0.1.3+ABCDEF1234567.dirty", "abcdef123456")]
    [InlineData("0.1.3", "local build")]
    [InlineData(null, "local build")]
    public void FromValues_UsesOnlyAShortSourceRevision(string? informationalVersion, string expectedRevision)
    {
        var provenance = BuildProvenance.FromValues("0.1.3", informationalVersion);

        Assert.Equal("0.1.3", provenance.Version);
        Assert.Equal(expectedRevision, provenance.Revision);
    }
}
