using System.Security.Claims;

namespace SaaSBase.Application.Services;

public interface ICurrentUserService
{
    Guid? GetCurrentUserId();
    string? GetCurrentUserEmail();
    string? GetCurrentUserName();
    string? GetCurrentUserRole();
    string? GetCurrentUserIpAddress();
    string? GetCurrentUserAgent();
    bool IsAuthenticated();
    ClaimsPrincipal? GetCurrentUser();
}
