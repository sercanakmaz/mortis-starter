using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.Shared.Domain;

public class LocalizedTextTests
{
    [Fact]
    public void Get_Should_ReturnTheTextOfTheLanguage_ThenTheFallback_ThenAnyText()
    {
        var localizedText = LocalizedText.Of("tr", "Cıvata").With("en", "Bolt");

        Assert.Equal(["Cıvata", "Bolt", "Bolt"], [localizedText.Get("tr", "en"), localizedText.Get("en", "en"), localizedText.Get("de", "en")]);
        Assert.Equal("Cıvata", LocalizedText.Of("tr", "Cıvata").Get("de", "en"));
    }

    [Fact]
    public void With_Should_LeaveTheOriginalUnchanged()
    {
        var original = LocalizedText.Of("tr", "Cıvata");

        original.With("en", "Bolt");

        Assert.Equal(["tr"], original.Texts.Keys);
    }
}
