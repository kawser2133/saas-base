using Microsoft.EntityFrameworkCore;
using SaaSBase.Application.Services;
using SaaSBase.Domain;

namespace SaaSBase.Application.Implementations;

public class SystemService : ISystemService
{
	private readonly IUnitOfWork _unitOfWork;

	public SystemService(IUnitOfWork unitOfWork)
	{
		_unitOfWork = unitOfWork;
	}

	public async Task<SystemStatsDto> GetSystemStatsAsync()
	{
		// Get stats across all organizations (ignore tenant filter)
		var orgRepo = _unitOfWork.Repository<Organization>();
		var userRepo = _unitOfWork.Repository<User>();
		var sessionRepo = _unitOfWork.Repository<UserSession>();
		var deptRepo = _unitOfWork.Repository<Department>();
		var positionRepo = _unitOfWork.Repository<Position>();

		var totalOrganizations = await orgRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(o => !o.IsDeleted)
			.CountAsync();

		var activeOrganizations = await orgRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(o => !o.IsDeleted && o.IsActive)
			.CountAsync();

		var totalUsers = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => !u.IsDeleted)
			.CountAsync();

		var activeSessions = await sessionRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(s => !s.IsDeleted && s.IsActive && s.ExpiresAt > DateTimeOffset.UtcNow)
			.CountAsync();

		var totalDepartments = await deptRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(d => !d.IsDeleted)
			.CountAsync();

		var totalPositions = await positionRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(p => !p.IsDeleted)
			.CountAsync();

		return new SystemStatsDto
		{
			TotalOrganizations = totalOrganizations,
			ActiveOrganizations = activeOrganizations,
			TotalUsers = totalUsers,
			ActiveSessions = activeSessions,
			TotalDepartments = totalDepartments,
			TotalPositions = totalPositions
		};
	}

	public async Task<List<OrganizationSummaryDto>> GetAllOrganizationsAsync()
	{
		var orgRepo = _unitOfWork.Repository<Organization>();
		var userRepo = _unitOfWork.Repository<User>();
		var locationRepo = _unitOfWork.Repository<Location>();

		var organizations = await orgRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(o => !o.IsDeleted)
			.Select(o => new
			{
				o.Id,
				o.Name,
				o.IsActive,
				o.CreatedAtUtc
			})
			.ToListAsync();

		var result = new List<OrganizationSummaryDto>();

		foreach (var org in organizations)
		{
			var userCount = await userRepo.GetQueryable()
				.IgnoreQueryFilters()
				.Where(u => !u.IsDeleted && u.OrganizationId == org.Id)
				.CountAsync();

			var locationCount = await locationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.Where(l => !l.IsDeleted && l.OrganizationId == org.Id)
				.CountAsync();

			result.Add(new OrganizationSummaryDto
			{
				Id = org.Id,
				Name = org.Name,
				IsActive = org.IsActive,
				UserCount = userCount,
				LocationCount = locationCount,
				CreatedAtUtc = org.CreatedAtUtc
			});
		}

		return result.OrderByDescending(o => o.CreatedAtUtc).ToList();
	}

	public async Task<OrganizationTrendDto> GetOrganizationRegistrationTrendAsync(int months = 12)
	{
		var orgRepo = _unitOfWork.Repository<Organization>();
		var endDate = DateTimeOffset.UtcNow;
		var startDate = endDate.AddMonths(-months);

		var organizations = await orgRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(o => !o.IsDeleted && o.CreatedAtUtc >= startDate && o.CreatedAtUtc <= endDate)
			.Select(o => new { o.CreatedAtUtc })
			.ToListAsync();

		var labels = new List<string>();
		var values = new List<int>();

		for (int i = months - 1; i >= 0; i--)
		{
			var monthStart = endDate.AddMonths(-i).Date;
			var monthEnd = monthStart.AddMonths(1).AddDays(-1);
			var monthLabel = monthStart.ToString("MMM yyyy");

			var count = organizations.Count(o => o.CreatedAtUtc.Date >= monthStart && o.CreatedAtUtc.Date <= monthEnd);

			labels.Add(monthLabel);
			values.Add(count);
		}

		return new OrganizationTrendDto
		{
			Labels = labels,
			Values = values
		};
	}

	public async Task<UserGrowthTrendDto> GetUserGrowthTrendAsync(int months = 12)
	{
		var userRepo = _unitOfWork.Repository<User>();
		var endDate = DateTimeOffset.UtcNow;
		var startDate = endDate.AddMonths(-months);

		var users = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => !u.IsDeleted && u.CreatedAtUtc >= startDate && u.CreatedAtUtc <= endDate)
			.Select(u => new { u.CreatedAtUtc })
			.ToListAsync();

		var labels = new List<string>();
		var values = new List<int>();

		for (int i = months - 1; i >= 0; i--)
		{
			var monthStart = endDate.AddMonths(-i).Date;
			var monthEnd = monthStart.AddMonths(1).AddDays(-1);
			var monthLabel = monthStart.ToString("MMM yyyy");

			var count = users.Count(u => u.CreatedAtUtc.Date >= monthStart && u.CreatedAtUtc.Date <= monthEnd);

			labels.Add(monthLabel);
			values.Add(count);
		}

		return new UserGrowthTrendDto
		{
			Labels = labels,
			Values = values
		};
	}

	public async Task<ActiveInactiveDto> GetOrganizationActiveInactiveAsync()
	{
		var orgRepo = _unitOfWork.Repository<Organization>();

		var active = await orgRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(o => !o.IsDeleted && o.IsActive)
			.CountAsync();

		var inactive = await orgRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(o => !o.IsDeleted && !o.IsActive)
			.CountAsync();

		return new ActiveInactiveDto
		{
			Active = active,
			Inactive = inactive
		};
	}

	public async Task<ActiveInactiveDto> GetUserActiveInactiveAsync()
	{
		var userRepo = _unitOfWork.Repository<User>();

		var active = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => !u.IsDeleted && u.IsActive)
			.CountAsync();

		var inactive = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => !u.IsDeleted && !u.IsActive)
			.CountAsync();

		return new ActiveInactiveDto
		{
			Active = active,
			Inactive = inactive
		};
	}
}
