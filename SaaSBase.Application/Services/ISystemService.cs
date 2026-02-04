using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface ISystemService
{
	Task<SystemStatsDto> GetSystemStatsAsync();
	Task<List<OrganizationSummaryDto>> GetAllOrganizationsAsync();
	Task<OrganizationTrendDto> GetOrganizationRegistrationTrendAsync(int months = 12);
	Task<UserGrowthTrendDto> GetUserGrowthTrendAsync(int months = 12);
	Task<ActiveInactiveDto> GetOrganizationActiveInactiveAsync();
	Task<ActiveInactiveDto> GetUserActiveInactiveAsync();
}

public class SystemStatsDto
{
	public int TotalOrganizations { get; set; }
	public int ActiveOrganizations { get; set; }
	public int TotalUsers { get; set; }
	public int ActiveSessions { get; set; }
	public int TotalDepartments { get; set; }
	public int TotalPositions { get; set; }
}

public class OrganizationSummaryDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int UserCount { get; set; }
	public int LocationCount { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
}

public class OrganizationTrendDto
{
	public List<string> Labels { get; set; } = new();
	public List<int> Values { get; set; } = new();
}

public class UserGrowthTrendDto
{
	public List<string> Labels { get; set; } = new();
	public List<int> Values { get; set; } = new();
}

public class ActiveInactiveDto
{
	public int Active { get; set; }
	public int Inactive { get; set; }
}
