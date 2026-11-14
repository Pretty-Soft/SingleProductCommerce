namespace OneCart.Api.Admin;

public sealed class AdminAuditEvent
{
    public Guid Id { get; set; }

    public Guid ActorUserId { get; set; }

    public string ActorEmail { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public string Target { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
