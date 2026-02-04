using SaaSBase.Application.Services;

namespace SaaSBase.Api.Infrastructure;

public class CurrentTenantService : ICurrentTenantService
{
	private readonly IHttpContextAccessor _httpContextAccessor;
	private readonly IBackgroundContextService _backgroundContextService;

	public CurrentTenantService(IHttpContextAccessor httpContextAccessor, IBackgroundContextService backgroundContextService)
	{
		_httpContextAccessor = httpContextAccessor;
		_backgroundContextService = backgroundContextService;
	}

	public Guid GetOrganizationId()
	{
		// First try to get from background context (for background operations)
		var backgroundOrgId = _backgroundContextService.GetOrganizationId();
		if (backgroundOrgId != Guid.Empty)
			return backgroundOrgId;

		// Fallback to HTTP context
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext == null) return Guid.Empty;

		// From header X-Organization-Id; fallback to route/query in future
		var header = httpContext.Request.Headers["X-Organization-Id"].ToString();
		return Guid.TryParse(header, out var organizationId) ? organizationId : Guid.Empty;
	}

	public Guid GetCurrentOrganizationId()
	{
		return GetOrganizationId();
	}

	public Guid GetCurrentUserId()
	{
		// First try to get from background context (for background operations)
		var backgroundUserId = _backgroundContextService.GetUserId();
		if (backgroundUserId != Guid.Empty)
			return backgroundUserId;

		// Fallback to HTTP context
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext == null) return Guid.Empty;

		var userIdClaim = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
		return Guid.TryParse(userIdClaim?.Value, out var userId) ? userId : Guid.Empty;
	}

	public string GetCurrentUserEmail()
	{
		// First try to get from background context (for background operations)
		var backgroundUserName = _backgroundContextService.GetUserName();
		if (!string.IsNullOrEmpty(backgroundUserName) && backgroundUserName != "System")
			return backgroundUserName;

		// Fallback to HTTP context
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext == null) return string.Empty;

		return httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty;
	}

	public string GetCurrentUserName()
	{
		// First try to get from background context (for background operations)
		var backgroundUserName = _backgroundContextService.GetUserName();
		if (!string.IsNullOrEmpty(backgroundUserName) && backgroundUserName != "System")
			return backgroundUserName;

		// Fallback to HTTP context
		var httpContext = _httpContextAccessor.HttpContext;
		if (httpContext == null) return string.Empty;

		return httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
	}

	public bool IsInBackgroundContext()
	{
		return _backgroundContextService.GetOrganizationId() != Guid.Empty;
	}

	public void SetBackgroundContext(Guid organizationId, Guid? userId = null, string? userName = null)
	{
		_backgroundContextService.SetOrganizationContext(organizationId, userId, userName);
	}
}


