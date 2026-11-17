namespace OneCart.Api.Admin;

public sealed class AdminSystemSetting
{
    public int Id { get; set; }

    public bool MfaRequired { get; set; }

    public string? CurrencyCode { get; set; }

    public string DefaultLanguageCode { get; set; } = "en";
}
