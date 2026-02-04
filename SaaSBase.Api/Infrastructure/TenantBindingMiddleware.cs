using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SaaSBase.Api.Infrastructure;

public class TenantBindingMiddleware
{
	private readonly RequestDelegate _next;

	public TenantBindingMiddleware(RequestDelegate next)
	{
		_next = next;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		// Only enforce for authenticated requests; anonymous endpoints (e.g., login) skip
		if (context.User?.Identity?.IsAuthenticated == true)
		{
			var claimsPrincipal = context.User;
			var tokenTenantId = FindTenantIdFromClaims(claimsPrincipal);
			var headerTenantId = context.Request.Headers["X-Organization-Id"].ToString();

			// If token contains a tenant, require it and ensure the header matches (if provided)
			if (tokenTenantId != Guid.Empty)
			{
				if (Guid.TryParse(headerTenantId, out var headerGuid))
				{
					if (headerGuid != tokenTenantId)
					{
						context.Response.StatusCode = StatusCodes.Status400BadRequest;
						await context.Response.WriteAsync("Tenant mismatch between token and header.");
						return;
					}
				}

				// Ensure header is set for downstream components (DbContext tenant provider relies on header)
				context.Request.Headers["X-Organization-Id"] = tokenTenantId.ToString();
			}
			else
			{
				// If token has no tenant claim, reject authenticated access to tenant-scoped APIs
				context.Response.StatusCode = StatusCodes.Status400BadRequest;
				await context.Response.WriteAsync("Missing tenant in token claims.");
				return;
			}
		}

		await _next(context);
	}

	private static Guid FindTenantIdFromClaims(ClaimsPrincipal principal)
	{
		var candidate = principal.FindFirst("tenant_id")?.Value
			?? principal.FindFirst("organizationId")?.Value
			?? principal.FindFirst("org_id")?.Value
			?? principal.FindFirst("tenant")?.Value;
		return Guid.TryParse(candidate, out var tenantId) ? tenantId : Guid.Empty;
	}
}


