using System.Security.Claims;
using SaaSBase.Application.Services;
using Microsoft.AspNetCore.Http;

namespace SaaSBase.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }

    public string? GetCurrentUserEmail()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;
    }

    public string? GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
    }

    public string? GetCurrentUserRole()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
    }

    public string? GetCurrentUserIpAddress()
    {
        return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    public string? GetCurrentUserAgent()
    {
        var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers?.UserAgent;
        return userAgent?.ToString();
    }

    public bool IsAuthenticated()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    }

    public ClaimsPrincipal? GetCurrentUser()
    {
        return _httpContextAccessor.HttpContext?.User;
    }
}
