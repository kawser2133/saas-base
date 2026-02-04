using System.Security.Claims;

namespace SaaSBase.Application.Services;

/// <summary>
/// Service for getting current user context information from HTTP context
/// </summary>
public interface IUserContextService
{
    Guid GetCurrentUserId();
    string GetCurrentUserEmail();
    string GetCurrentUserName();
    Guid GetCurrentTenantId();
    Task<(Guid userId, string email, string name)> GetCurrentUserInfoAsync();
    Dictionary<string, string> GetAllClaims();
    bool IsAuthenticated();
    bool HasClaim(string claimType);
    string? GetClaimValue(string claimType);
    string GetCurrentIpAddress();
    string GetCurrentUserAgent();
    Task<bool> IsSystemAdministratorAsync();
}
