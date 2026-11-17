namespace OneCart.Api.Catalog;

public static class CurrencyRounding
{
    public static int DecimalPlaces(string currencyCode) => currencyCode.Trim().ToUpperInvariant() switch
    {
        "JPY" or "KRW" or "VND" or "CLP" or "PYG" or "RWF" or "UGX" or "VUV" or "XAF" or "XOF" or "XPF" or "BIF" or "DJF" or "GNF" or "KMF" => 0,
        "BHD" or "IQD" or "JOD" or "KWD" or "LYD" or "OMR" or "TND" => 3,
        "CLF" or "UYW" => 4,
        _ => 2
    };

    public static decimal Round(decimal amount, string currencyCode) =>
        decimal.Round(amount, DecimalPlaces(currencyCode), MidpointRounding.AwayFromZero);
}
