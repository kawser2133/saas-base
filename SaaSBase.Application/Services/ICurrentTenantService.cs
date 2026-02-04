using System;

namespace SaaSBase.Application.Services;

public interface ICurrentTenantService
{
	Guid GetOrganizationId();
	Guid GetCurrentOrganizationId();
	Guid GetCurrentUserId();
	string GetCurrentUserEmail();
	string GetCurrentUserName();
	bool IsInBackgroundContext();
	void SetBackgroundContext(Guid organizationId, Guid? userId = null, string? userName = null);
}
