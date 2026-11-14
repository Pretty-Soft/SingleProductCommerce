using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using OneCart.Api.Catalog;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin");

        group.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext context) =>
            Results.Ok(new { requestToken = antiforgery.GetAndStoreTokens(context).RequestToken }));

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapPost("/logout", async (SignInManager<AdminUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.NoContent();
            })
            .RequireAuthorization()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization();

        group.MapPost("/password", ChangePasswordAsync)
            .RequireAuthorization()
            .AddEndpointFilter(ValidateAntiforgeryAsync);

        group.MapGet("/roles", GetRolesAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/users", GetUsersAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/audit", GetAuditEventsAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPost("/users", CreateUserAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/users/{userId:guid}/roles", UpdateUserRolesAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/catalog/product", GetAdminProductAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/catalog/product", SaveAdminProductAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPost("/catalog/product/images", UploadProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .WithMetadata(new RequestSizeLimitAttribute(ProductMediaService.MaximumImageBytes + 128 * 1024))
            .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = ProductMediaService.MaximumImageBytes + 128 * 1024 })
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapGet("/catalog/product/images/{imageId:guid}", OpenAdminProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapPut("/catalog/product/images/{imageId:guid}", UpdateProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        group.MapDelete("/catalog/product/images/{imageId:guid}", DeleteProductImageAsync)
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(ValidateAntiforgeryAsync)
            .AddEndpointFilter(RequireChangedPasswordAsync);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        AdminLoginRequest request,
        SignInManager<AdminUser> signInManager)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Length > 256
            || string.IsNullOrEmpty(request.Password)
            || request.Password.Length > 1024)
        {
            return Results.BadRequest(new { error = "Email and password are required." });
        }

        var result = await signInManager.PasswordSignInAsync(
            request.Email.Trim(), request.Password, isPersistent: false, lockoutOnFailure: true);

        return result.Succeeded
            ? Results.Ok(new { signedIn = true })
            : Results.Unauthorized();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Results.Ok(new AdminUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles.ToArray(),
            user.MustChangePassword,
            user.LockoutEnd));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        UserManager<AdminUser> userManager,
        SignInManager<AdminUser> signInManager,
        HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrEmpty(request.CurrentPassword)
            || string.IsNullOrEmpty(request.NewPassword)
            || request.CurrentPassword.Length > 1024
            || request.NewPassword.Length > 256)
        {
            return Results.BadRequest(new { error = "Enter a valid current password and a new password up to 256 characters." });
        }

        user.MustChangePassword = false;
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            user.MustChangePassword = true;
            return Results.ValidationProblem(result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
        }

        await signInManager.RefreshSignInAsync(user);
        return Results.NoContent();
    }

    private static async Task<IResult> GetRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        var roles = await roleManager.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new AdminRoleResponse(role.Id, role.Name ?? string.Empty))
            .ToListAsync();

        return Results.Ok(roles);
    }

    private static async Task<IResult> GetUsersAsync(UserManager<AdminUser> userManager)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.Email)
            .ToListAsync();
        var result = new List<AdminUserResponse>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new AdminUserResponse(
                user.Id,
                user.Email ?? string.Empty,
                roles.ToArray(),
                user.MustChangePassword,
                user.LockoutEnd));
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAuditEventsAsync(StoreDbContext database)
    {
        var events = await database.AdminAuditEvents
            .AsNoTracking()
            .OrderByDescending(auditEvent => auditEvent.CreatedAtUtc)
            .Take(100)
            .Select(auditEvent => new AdminAuditResponse(
                auditEvent.Id,
                auditEvent.ActorEmail,
                auditEvent.Action,
                auditEvent.Target,
                auditEvent.Details,
                auditEvent.CreatedAtUtc))
            .ToListAsync();

        return Results.Ok(events);
    }

    private static async Task<IResult> GetAdminProductAsync(
        AdminCatalogService catalog,
        CancellationToken cancellationToken)
    {
        var product = await catalog.GetProductAsync(cancellationToken);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> SaveAdminProductAsync(
        AdminProductRequest request,
        AdminCatalogService catalog,
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        var userId = userManager.GetUserId(context.User);
        if (!Guid.TryParse(userId, out var actorId))
        {
            return Results.Unauthorized();
        }

        var result = await catalog.SaveProductAsync(
            request,
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);
        return result.Errors is not null
            ? Results.ValidationProblem(result.Errors)
            : Results.Ok(result.Product);
    }

    private static async Task<IResult> UploadProductImageAsync(
        HttpContext context,
        ProductMediaService media,
        UserManager<AdminUser> userManager)
    {
        if (context.Request.ContentLength is > ProductMediaService.MaximumImageBytes + 128 * 1024)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(new { error = "Upload a valid multipart form." });
        }

        var file = form.Files.GetFile("file");
        if (file is null)
        {
            return Results.BadRequest(new { error = "Choose an image to upload." });
        }
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId))
        {
            return Results.Unauthorized();
        }

        await using var stream = file.OpenReadStream();
        var result = await media.UploadAsync(
            stream,
            file.Length,
            form["mediaKind"].ToString(),
            form["altText"].ToString(),
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);

        if (result.Error is not null) return Results.BadRequest(new { error = result.Error });
        return result.Image is null
            ? Results.BadRequest(new { error = "Create a product draft before uploading media." })
            : Results.Created(result.Image.Url, result.Image);
    }

    private static async Task<IResult> OpenAdminProductImageAsync(
        Guid imageId,
        ProductMediaService media,
        CancellationToken cancellationToken)
    {
        var image = await media.OpenAdminImageAsync(imageId, cancellationToken);
        return image is null
            ? Results.NotFound()
            : Results.File(image.Content, image.ContentType, enableRangeProcessing: false);
    }

    private static async Task<IResult> UpdateProductImageAsync(
        Guid imageId,
        AdminProductImageUpdateRequest request,
        ProductMediaService media,
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId)) return Results.Unauthorized();
        var result = await media.UpdateAsync(
            imageId,
            request.AltText,
            request.DisplayOrder,
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);
        if (result.IsNotFound) return Results.NotFound();
        return result.Error is not null ? Results.BadRequest(new { error = result.Error }) : Results.Ok(result.Image);
    }

    private static async Task<IResult> DeleteProductImageAsync(
        Guid imageId,
        ProductMediaService media,
        UserManager<AdminUser> userManager,
        HttpContext context)
    {
        if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId)) return Results.Unauthorized();
        var deleted = await media.DeleteAsync(
            imageId,
            actorId,
            userManager.GetUserName(context.User) ?? string.Empty,
            context.RequestAborted);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> CreateUserAsync(
        CreateAdminUserRequest request,
        UserManager<AdminUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        StoreDbContext database,
        HttpContext context)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email))
        {
            return Results.BadRequest(new { error = "Enter a valid email address." });
        }

        if (string.IsNullOrEmpty(request.TemporaryPassword) || request.TemporaryPassword.Length > 256)
        {
            return Results.BadRequest(new { error = "Enter a temporary password up to 256 characters long." });
        }

        var roleNames = NormalizeRoleNames(request.RoleNames);
        if (roleNames.Length == 0 || roleNames.Any(role => !AdminRoles.BuiltIn.Contains(role, StringComparer.Ordinal)))
        {
            return Results.BadRequest(new { error = "Select at least one valid role." });
        }

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return Results.BadRequest(new { error = "One or more selected roles do not exist." });
            }
        }

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            MustChangePassword = true
        };

        await using var transaction = await database.Database.BeginTransactionAsync(context.RequestAborted);
        var createResult = await userManager.CreateAsync(user, request.TemporaryPassword ?? string.Empty);
        if (!createResult.Succeeded)
        {
            return IdentityValidationResult(createResult);
        }

        var roleResult = await userManager.AddToRolesAsync(user, roleNames);
        if (!roleResult.Succeeded)
        {
            return IdentityValidationResult(roleResult);
        }

        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = Guid.Parse(userManager.GetUserId(context.User)!),
            ActorEmail = userManager.GetUserName(context.User) ?? string.Empty,
            Action = "admin_user.create",
            TargetId = user.Id,
            Target = email,
            Details = JsonSerializer.Serialize(new { roles = roleNames }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(context.RequestAborted);
        await transaction.CommitAsync(context.RequestAborted);

        return Results.Created($"/api/v1/admin/users/{user.Id}", new AdminUserResponse(
            user.Id, email, roleNames, user.MustChangePassword, user.LockoutEnd));
    }

    private static async Task<IResult> UpdateUserRolesAsync(
        Guid userId,
        UpdateAdminUserRolesRequest request,
        UserManager<AdminUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        StoreDbContext database,
        SignInManager<AdminUser> signInManager,
        HttpContext context)
    {
        var target = await userManager.FindByIdAsync(userId.ToString());
        if (target is null)
        {
            return Results.NotFound();
        }

        var roleNames = NormalizeRoleNames(request.RoleNames);
        if (roleNames.Length == 0 || roleNames.Any(role => !AdminRoles.BuiltIn.Contains(role, StringComparer.Ordinal)))
        {
            return Results.BadRequest(new { error = "Select at least one valid role." });
        }

        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                return Results.BadRequest(new { error = "One or more selected roles do not exist." });
            }
        }

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, context.RequestAborted);
        var currentRoles = await userManager.GetRolesAsync(target);
        var isRemovingSystemAdmin = currentRoles.Contains(AdminRoles.SystemAdmin, StringComparer.Ordinal)
            && !roleNames.Contains(AdminRoles.SystemAdmin, StringComparer.Ordinal);
        if (isRemovingSystemAdmin)
        {
            var systemAdmins = await userManager.GetUsersInRoleAsync(AdminRoles.SystemAdmin);
            if (systemAdmins.Count <= 1)
            {
                return Results.Conflict(new { error = "The last SystemAdmin role cannot be removed." });
            }
        }

        var removeResult = await userManager.RemoveFromRolesAsync(target, currentRoles);
        if (!removeResult.Succeeded)
        {
            return IdentityValidationResult(removeResult);
        }

        var addResult = await userManager.AddToRolesAsync(target, roleNames);
        if (!addResult.Succeeded)
        {
            return IdentityValidationResult(addResult);
        }

        await userManager.UpdateSecurityStampAsync(target);
        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = Guid.Parse(userManager.GetUserId(context.User)!),
            ActorEmail = userManager.GetUserName(context.User) ?? string.Empty,
            Action = "admin_user.roles.update",
            TargetId = target.Id,
            Target = target.Email ?? string.Empty,
            Details = JsonSerializer.Serialize(new { previousRoles = currentRoles, roles = roleNames }),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(context.RequestAborted);
        await transaction.CommitAsync(context.RequestAborted);

        if (string.Equals(target.Id.ToString(), userManager.GetUserId(context.User), StringComparison.Ordinal))
        {
            if (roleNames.Contains(AdminRoles.SystemAdmin, StringComparer.Ordinal))
            {
                await signInManager.RefreshSignInAsync(target);
            }
            else
            {
                await signInManager.SignOutAsync();
            }
        }

        return Results.NoContent();
    }

    private static string[] NormalizeRoleNames(IEnumerable<string>? roleNames) =>
        roleNames?
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

    private static IResult IdentityValidationResult(IdentityResult result) =>
        Results.ValidationProblem(result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));

    internal static async ValueTask<object?> ValidateAntiforgeryAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest(new { error = "A valid anti-forgery token is required." });
        }

        return await next(context);
    }

    internal static async ValueTask<object?> RequireChangedPasswordAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<AdminUser>>();
        var user = await userManager.GetUserAsync(context.HttpContext.User);
        return user is null
            ? Results.Unauthorized()
            : user.MustChangePassword
                ? Results.StatusCode(StatusCodes.Status403Forbidden)
                : await next(context);
    }

    public sealed record AdminLoginRequest(string Email, string Password);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public sealed record CreateAdminUserRequest(string Email, string TemporaryPassword, string[] RoleNames);
    public sealed record UpdateAdminUserRolesRequest(string[] RoleNames);
    public sealed record AdminProductImageUpdateRequest(string AltText, int DisplayOrder);
    public sealed record AdminRoleResponse(Guid Id, string Name);
    public sealed record AdminAuditResponse(
        Guid Id,
        string ActorEmail,
        string Action,
        string Target,
        string Details,
        DateTimeOffset CreatedAtUtc);
    public sealed record AdminUserResponse(
        Guid Id,
        string Email,
        string[] Roles,
        bool MustChangePassword,
        DateTimeOffset? LockoutEnd);
}
