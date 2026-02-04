using SaaSBase.Application.Services;
using SaaSBase.Application;
using SaaSBase.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Application.Implementations;

/// <summary>
/// Implementation of IUserContextService for getting current user context information
/// </summary>
public class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly ILogger<UserContextService> _logger;

    public UserContextService(
        IHttpContextAccessor httpContextAccessor,
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService,
        ILogger<UserContextService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _logger = logger;
    }

    public Guid GetCurrentUserId()
    {
        try
        {
            var userIdClaim = GetClaimValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier") ??
                             GetClaimValue("sub") ??
                             GetClaimValue("user_id") ??
                             GetClaimValue("id");

            return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    public string GetCurrentUserEmail()
    {
        try
        {
            return GetClaimValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ??
                   GetClaimValue("email") ??
                   GetClaimValue("preferred_username") ??
                   _httpContextAccessor.HttpContext?.User?.Identity?.Name ??
                   "system";
        }
        catch
        {
            return "system";
        }
    }

    public string GetCurrentUserName()
    {
        try
        {
            return GetClaimValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name") ??
                   GetClaimValue("name") ??
                   "System";
        }
        catch
        {
            return "System";
        }
    }

    public Guid GetCurrentTenantId()
    {
        try
        {
            var tenantIdClaim = GetClaimValue("tenant_id");
            return Guid.TryParse(tenantIdClaim, out var tenantId) ? tenantId : Guid.Empty;
        }
        catch
        {
            return Guid.Empty;
        }
    }

    /// <summary>
    /// Check if current user is System Administrator (can access all organizations)
    /// </summary>
    public async Task<bool> IsSystemAdministratorAsync()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return false;

            var userRoleRepo = _unitOfWork.Repository<UserRole>();
            var roleRepo = _unitOfWork.Repository<Role>();

            // Check if user has "System Administrator" role
            var isSystemAdmin = await (from ur in userRoleRepo.GetQueryable()
                                      join r in roleRepo.GetQueryable() on ur.RoleId equals r.Id
                                      where ur.UserId == userId
                                         && r.Name == "System Administrator"
                                         && r.IsActive
                                         && !r.IsDeleted
                                         && !ur.IsDeleted
                                      select ur).AnyAsync();

            return isSystemAdmin;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(Guid userId, string email, string name)> GetCurrentUserInfoAsync()
    {
        var currentUserId = Guid.Empty;
        var currentUserEmail = "system";
        var currentUserName = "System";

        try
        {
            // Try to get user ID from claims
            currentUserId = GetCurrentUserId();
            currentUserEmail = GetCurrentUserEmail();
            currentUserName = GetCurrentUserName();

            if (currentUserId != Guid.Empty)
            {
                // Get user details from database
                var userRepo = _unitOfWork.Repository<User>();
                var organizationId = _tenantService.GetOrganizationId();
                var currentUser = await userRepo.FindAsync(x => x.Id == currentUserId && x.OrganizationId == organizationId);

                if (currentUser != null)
                {
                    currentUserEmail = currentUser.Email;
                    currentUserName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                }
            }
            else if (!string.IsNullOrEmpty(currentUserEmail) && currentUserEmail != "system")
            {
                // Try to find user by email
                var userRepo = _unitOfWork.Repository<User>();
                var organizationId = _tenantService.GetOrganizationId();
                var currentUser = await userRepo.FindAsync(x => x.Email == currentUserEmail && x.OrganizationId == organizationId);

                if (currentUser != null)
                {
                    currentUserId = currentUser.Id;
                    currentUserName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but continue
            _logger.LogWarning(ex, "Failed to get current user info");
            currentUserEmail = "system";
            currentUserName = "System";
        }

        return (currentUserId, currentUserEmail, currentUserName);
    }

    public Dictionary<string, string> GetAllClaims()
    {
        var claims = new Dictionary<string, string>();

        try
        {
            if (_httpContextAccessor.HttpContext?.User?.Claims != null)
            {
                foreach (var claim in _httpContextAccessor.HttpContext.User.Claims)
                {
                    claims[claim.Type] = claim.Value;
                }
            }
        }
        catch
        {
            // Return empty dictionary on error
        }

        return claims;
    }

    public bool IsAuthenticated()
    {
        try
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        }
        catch
        {
            return false;
        }
    }

    public bool HasClaim(string claimType)
    {
        try
        {
            return _httpContextAccessor.HttpContext?.User?.HasClaim(c => c.Type == claimType) ?? false;
        }
        catch
        {
            return false;
        }
    }

    public string? GetClaimValue(string claimType)
    {
        try
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public string GetCurrentIpAddress()
    {
        try
        {
            return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        }
        catch
        {
            return "0.0.0.0";
        }
    }

    public string GetCurrentUserAgent()
    {
        try
        {
            return _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
