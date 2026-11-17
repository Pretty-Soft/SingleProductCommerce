using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace OneCart.Api.Admin;

public sealed record SmtpSettings(string Host, int Port, string From, string? Username, string? Password)
{
    public object PublicView() => new { Host, Port, From, Username, hasPassword = !string.IsNullOrEmpty(Password) };
}

public sealed class AdminSmtpSettings(IConfiguration configuration, IWebHostEnvironment environment, IDataProtectionProvider protection)
{
    private readonly object gate = new();
    private readonly IDataProtector protector = protection.CreateProtector("OneCart.Admin.SmtpSettings.v1");
    private readonly string path = Path.GetFullPath(Path.Combine(environment.ContentRootPath,
        configuration["DataProtection:KeyDirectory"] ?? "App_Data", "smtp-settings.protected"));

    public SmtpSettings Get()
    {
        lock (gate)
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<SmtpSettings>(protector.Unprotect(File.ReadAllText(path)))!;

            return new SmtpSettings(configuration["Email:Smtp:Host"] ?? "",
                int.TryParse(configuration["Email:Smtp:Port"] ?? "587", out var port) ? port : 0,
                configuration["Email:From"] ?? "", configuration["Email:Smtp:Username"], configuration["Email:Smtp:Password"]);
        }
    }

    public bool TrySave(SmtpSettings request, out SmtpSettings saved)
    {
        lock (gate)
        {
            var username = request.Username?.Trim();
            var current = Get();
            var host = request.Host?.Trim() ?? "";
            var preservePassword = string.Equals(host, current.Host, StringComparison.OrdinalIgnoreCase)
                && request.Port == current.Port && username == current.Username;
            saved = new SmtpSettings(host, request.Port, request.From?.Trim() ?? "", username,
                string.IsNullOrEmpty(username) ? null : request.Password ?? (preservePassword ? current.Password : null));

            if (Uri.CheckHostName(saved.Host) != UriHostNameType.Dns || saved.Host.Length > 253
                || saved.Port is < 1 or > 65535 or 465 || saved.From.Length > 254
                || !MailAddress.TryCreate(saved.From, out var from) || from.Address != saved.From
                || saved.Username?.Length > 254 || saved.Password?.Length > 512
                || (!string.IsNullOrEmpty(saved.Username) && string.IsNullOrEmpty(saved.Password)))
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, protector.Protect(JsonSerializer.Serialize(saved)));
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.Move(temporary, path, overwrite: true);
            }
            finally { File.Delete(temporary); }
            return true;
        }
    }
}
