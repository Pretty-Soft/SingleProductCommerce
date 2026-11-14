using Microsoft.AspNetCore.Identity;

namespace OneCart.Api.Admin;

public sealed class AdminUser : IdentityUser<Guid>
{
    public bool MustChangePassword { get; set; }
}
