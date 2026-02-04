using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SaaSBase.Application.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Api.Attributes;

/// <summary>
/// Attribute to check if the current user has required permissions
/// Usage: [HasPermission("Users.Create")] or [HasPermission("Users.Create", "Users.Update")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _permissionCodes;

    public HasPermissionAttribute(params string[] permissionCodes)
    {
        _permissionCodes = permissionCodes ?? Array.Empty<string>();
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        // Skip if no permissions required
        if (_permissionCodes.Length == 0)
            return;

        var httpContext = context.HttpContext;
            var logger = httpContext.RequestServices.GetRequiredService<ILogger<HasPermissionAttribute>>();
        
        // Check if user is authenticated
        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Get user ID from claims
        var userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User?.FindFirst("sub")?.Value
            ?? httpContext.User?.FindFirst("userId")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Do not allow client-asserted role names to bypass authorization

        // Get permission service from DI
        var permissionService = httpContext.RequestServices.GetRequiredService<IPermissionService>();

        // Check if user has ALL required permissions
            foreach (var code in _permissionCodes)
            {
                try
                {
                    var hasPermission = await permissionService.UserHasPermissionAsync(userId, code);
                    if (!hasPermission)
                    {
                        logger.LogWarning(
                            "Authorization denied: User {UserId} lacks permission {PermissionCode} on {Action}",
                            userId, code, context.ActionDescriptor.DisplayName);
                        context.Result = new ForbidResult();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while checking permission {PermissionCode} for user {UserId} on {Action}", code, userId, context.ActionDescriptor.DisplayName);
                    // Fail secure: deny access on error
                    context.Result = new StatusCodeResult(500);
                    return;
                }
            }
    }
}

