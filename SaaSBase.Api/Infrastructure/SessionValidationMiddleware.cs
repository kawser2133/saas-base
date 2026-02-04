using SaaSBase.Application;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using System.Security.Claims;

namespace SaaSBase.Api.Infrastructure;

/// <summary>
/// Middleware to validate that the user's session is still active
/// If session is revoked, returns 401 Unauthorized
/// </summary>
public class SessionValidationMiddleware
{
	private readonly RequestDelegate _next;

	public SessionValidationMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
	{
		// Only validate for authenticated requests
		if (context.User?.Identity?.IsAuthenticated == true)
		{
			// Get sessionId from JWT claims
			var sessionIdClaim = context.User.FindFirst("sessionId")?.Value
				?? context.User.FindFirst("sid")?.Value
				?? context.User.FindFirst("jti")?.Value;

			if (!string.IsNullOrEmpty(sessionIdClaim))
			{
				// Create scope to get services
				using var scope = serviceProvider.CreateScope();
				var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var tenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				var sessionRepo = unitOfWork.Repository<UserSession>();

				// Get organization ID from tenant service
				var organizationId = tenantService.GetOrganizationId();
				if (organizationId == Guid.Empty)
				{
					// Try to get from claims
					var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value
						?? context.User.FindFirst("organizationId")?.Value;
					if (!Guid.TryParse(tenantIdClaim, out organizationId))
					{
						organizationId = Guid.Empty;
					}
				}

				// Check if session exists and is active (with organization filter)
				var session = await sessionRepo.FindAsync(s => 
					s.SessionId == sessionIdClaim && 
					s.IsActive &&
					(organizationId == Guid.Empty || s.OrganizationId == organizationId));
				
				if (session == null)
				{
					// Session not found or revoked - return 401
					context.Response.StatusCode = StatusCodes.Status401Unauthorized;
					await context.Response.WriteAsync("Session has been revoked. Please login again.");
					return;
				}

				// Check if session is expired
				if (session.ExpiresAt <= DateTimeOffset.UtcNow)
				{
					// Session expired - mark as inactive and return 401
					session.IsActive = false;
					sessionRepo.Update(session);
					await unitOfWork.SaveChangesAsync();

					context.Response.StatusCode = StatusCodes.Status401Unauthorized;
					await context.Response.WriteAsync("Session has expired. Please login again.");
					return;
				}

				// Check if organization is active
				if (organizationId != Guid.Empty)
				{
					var organizationRepo = unitOfWork.Repository<Organization>();
					var organization = await organizationRepo.FindAsync(o => o.Id == organizationId && !o.IsDeleted);
					if (organization != null && !organization.IsActive)
					{
						// Organization is inactive - return 403 Forbidden
						context.Response.StatusCode = StatusCodes.Status403Forbidden;
						await context.Response.WriteAsync("Organization is inactive. Please contact your administrator.");
						return;
					}
				}
			}
		}

		await _next(context);
	}
}

