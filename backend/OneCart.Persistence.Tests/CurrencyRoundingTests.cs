using OneCart.Api.Catalog;
using Xunit;

namespace OneCart.Persistence.Tests;

public sealed class CurrencyRoundingTests
{
    [Theory]
    [InlineData("BDT", "169.9915", "169.99")]
    [InlineData("USD", "1.005", "1.01")]
    [InlineData("EUR", "1.004", "1.00")]
    [InlineData("JPY", "125.5", "126")]
    [InlineData("KWD", "1.2345", "1.235")]
    public void Rounds_to_currency_precision(string currency, string input, string expected)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal(decimal.Parse(expected, culture), CurrencyRounding.Round(decimal.Parse(input, culture), currency));
    }
}
