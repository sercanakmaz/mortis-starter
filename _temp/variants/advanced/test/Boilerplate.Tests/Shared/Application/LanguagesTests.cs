using Boilerplate.Shared.Application;

namespace Boilerplate.Tests.Shared.Application;

public class LanguagesTests
{
    [Theory]
    [InlineData("tr", null, "tr")]
    [InlineData("TR-tr", null, "tr")]
    [InlineData("de", "tr", "tr")]
    [InlineData("de", null, "en")]
    [InlineData(null, "", "en")]
    public void Resolve_Should_TakeTheFirstSupportedCandidateOrItsPrimaryTagElseTheDefault(string first, string second, string language)
    {
        Assert.Equal(language, Languages.Resolve([first, second]));
    }
}
