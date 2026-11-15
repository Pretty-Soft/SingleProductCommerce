using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Catalog;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public sealed class AdminSecuritySettingsService(StoreDbContext database)
{
    public async Task<bool> GetMfaRequiredAsync(CancellationToken cancellationToken) =>
        await database.AdminSystemSettings.AsNoTracking().Select(setting => (bool?)setting.MfaRequired)
            .SingleOrDefaultAsync(cancellationToken) ?? false;

    public async Task<bool> SetMfaRequiredAsync(
        bool required,
        Guid actorId,
        string actorEmail,
        CancellationToken cancellationToken)
    {
        var setting = await database.AdminSystemSettings.SingleOrDefaultAsync(cancellationToken);
        var previous = setting?.MfaRequired ?? false;
        if (previous == required) return true;
        setting ??= new AdminSystemSetting { Id = 1 };
        setting.MfaRequired = required;
        if (database.Entry(setting).State == EntityState.Detached) database.AdminSystemSettings.Add(setting);
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(), ActorUserId = actorId, ActorEmail = actorEmail,
            Action = "admin_security.mfa_policy.update", TargetId = Guid.Empty, Target = "administrator accounts",
            Details = JsonSerializer.Serialize(new { required, previous }), CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }
}
