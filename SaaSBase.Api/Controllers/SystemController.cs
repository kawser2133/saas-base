using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBase.Application.Services;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class SystemController : ControllerBase
{
	private readonly ISystemService _systemService;
	private readonly ICurrentTenantService _tenantService;

	public SystemController(ISystemService systemService, ICurrentTenantService tenantService)
	{
		_systemService = systemService;
		_tenantService = tenantService;
	}

	[HttpGet("stats")]
	public async Task<IActionResult> GetSystemStats()
	{
		// Only System Administrator can access
		var userId = _tenantService.GetCurrentUserId();
		if (userId == Guid.Empty)
		{
			return Unauthorized();
		}

		// Check if user has System Administrator role
		// This should be checked via permission/role guard in production
		var stats = await _systemService.GetSystemStatsAsync();
		return Ok(stats);
	}

	[HttpGet("organizations")]
	public async Task<IActionResult> GetAllOrganizations()
	{
		// Only System Administrator can access
		var userId = _tenantService.GetCurrentUserId();
		if (userId == Guid.Empty)
		{
			return Unauthorized();
		}

		var organizations = await _systemService.GetAllOrganizationsAsync();
		return Ok(organizations);
	}

	[HttpGet("trends/organizations")]
	public async Task<IActionResult> GetOrganizationRegistrationTrend([FromQuery] int months = 12)
	{
		var userId = _tenantService.GetCurrentUserId();
		if (userId == Guid.Empty)
		{
			return Unauthorized();
		}

		var trend = await _systemService.GetOrganizationRegistrationTrendAsync(months);
		return Ok(trend);
	}

	[HttpGet("trends/users")]
	public async Task<IActionResult> GetUserGrowthTrend([FromQuery] int months = 12)
	{
		var userId = _tenantService.GetCurrentUserId();
		if (userId == Guid.Empty)
		{
			return Unauthorized();
		}

		var trend = await _systemService.GetUserGrowthTrendAsync(months);
		return Ok(trend);
	}

	[HttpGet("stats/organizations/active-inactive")]
	public async Task<IActionResult> GetOrganizationActiveInactive()
	{
		var userId = _tenantService.GetCurrentUserId();
		if (userId == Guid.Empty)
		{
			return Unauthorized();
		}

		var stats = await _systemService.GetOrganizationActiveInactiveAsync();
		return Ok(stats);
	}

	[HttpGet("stats/users/active-inactive")]
	public async Task<IActionResult> GetUserActiveInactive()
	{
		var userId = _tenantService.GetCurrentUserId();
		if (userId == Guid.Empty)
		{
			return Unauthorized();
		}

		var stats = await _systemService.GetUserActiveInactiveAsync();
		return Ok(stats);
	}
}
