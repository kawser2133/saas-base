namespace SaaSBase.Application.Services;

public interface IDemoDataService
{
	Task<bool> SeedDemoDataForOrganizationAsync(Guid organizationId);
}
