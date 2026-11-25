using Boilerplate.Infrastructure.Localization;
using Boilerplate.Shared.Application;
using Boilerplate.Domain;

namespace Boilerplate.Tests.Infrastructure.Localization;

public class MessageLocalizerTests
{
    [Fact]
    public void MessagesOf_TheDefaultLanguage_Should_HaveATextForEveryCataloguedCode()
    {
        var messages = new MessageLocalizer().MessagesOf(Languages.Default);

        Assert.DoesNotContain(ErrorCatalogue.Codes, code => !messages.ContainsKey(code));
    }

    [Fact]
    public void MessagesOf_EverySupportedLanguage_Should_HaveATextForEveryCataloguedCode()
    {
        var messageLocalizer = new MessageLocalizer();

        var missing = Languages.Supported.SelectMany(language => ErrorCatalogue.Codes.Where(code => !messageLocalizer.MessagesOf(language).ContainsKey(code)).Select(code => $"{language}: {code}"));

        Assert.Empty(missing);
    }

    [Fact]
    public void Get_Should_FillTheParametersIntoTheTextOfTheLanguage()
    {
        var message = new MessageLocalizer().Get("invalid_parameter", new Dictionary<string, object> { ["name"] = "stock", ["reason"] = "eksi olamaz" }, "tr");

        Assert.Equal("stock geçersiz: eksi olamaz", message);
    }

    [Fact]
    public void Get_WithAnUnsupportedLanguage_Should_FallBackToTheDefault_And_WithAnUnknownCode_Should_ReturnNull()
    {
        var messageLocalizer = new MessageLocalizer();

        Assert.Equal("The order is not open.", messageLocalizer.Get("order_not_open", new Dictionary<string, object>(), "de"));
        Assert.Null(messageLocalizer.Get("no_such_code", new Dictionary<string, object>(), "en"));
    }
}
