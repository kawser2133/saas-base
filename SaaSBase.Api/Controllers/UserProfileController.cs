using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/user-profile")]
[ApiVersion("1.0")]
[Authorize]
public class UserProfileController : ControllerBase
{
	private readonly IUserProfileService _userProfileService;

	public UserProfileController(IUserProfileService userProfileService)
	{
		_userProfileService = userProfileService;
	}

	[HttpGet("{userId}")]
	public async Task<IActionResult> GetUserProfile(Guid userId)
	{
		var profile = await _userProfileService.GetUserProfileAsync(userId);
		if (profile == null) return NotFound();
		return Ok(profile);
	}

	[HttpPut("{userId}")]
	public async Task<IActionResult> UpdateUserProfile(Guid userId, [FromBody] UpdateUserProfileDto dto)
	{
		var profile = await _userProfileService.UpdateUserProfileAsync(userId, dto);
		return Ok(profile);
	}

	[HttpPost("{userId}/avatar")]
	public async Task<IActionResult> UploadAvatar(Guid userId, [FromForm] IFormFile avatar)
	{
		if (avatar == null || avatar.Length == 0)
			return BadRequest("No file uploaded");

		var uploadDto = new UploadAvatarDto
		{
			FileName = avatar.FileName,
			ContentType = avatar.ContentType,
			FileSize = avatar.Length
		};

		// Read file data
		using var memoryStream = new MemoryStream();
		await avatar.CopyToAsync(memoryStream);
		uploadDto.FileData = memoryStream.ToArray();

		var success = await _userProfileService.UploadAvatarAsync(userId, uploadDto);
		if (!success) return BadRequest("Failed to upload avatar");
		return Ok(new { message = "Avatar uploaded successfully" });
	}

	[HttpDelete("{userId}/avatar")]
	public async Task<IActionResult> RemoveAvatar(Guid userId)
	{
		var success = await _userProfileService.RemoveAvatarAsync(userId);
		if (!success) return BadRequest("Failed to remove avatar");
		return Ok(new { message = "Avatar removed successfully" });
	}

	[HttpGet("{userId}/notification-preferences")]
	public async Task<IActionResult> GetNotificationPreferences(Guid userId)
	{
		var preferences = await _userProfileService.GetNotificationPreferencesAsync(userId);
		return Ok(preferences);
	}

	[HttpPut("{userId}/notification-preferences")]
	public async Task<IActionResult> UpdateNotificationPreferences(Guid userId, [FromBody] UpdateNotificationPreferencesDto dto)
	{
		var preferences = await _userProfileService.UpdateNotificationPreferencesAsync(userId, dto);
		return Ok(preferences);
	}

	[HttpGet("{userId}/activity-logs")]
	public async Task<IActionResult> GetUserActivityLogs(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var logs = await _userProfileService.GetUserActivityLogsAsync(userId, page, pageSize);
		return Ok(logs);
	}

	[HttpGet("{userId}/activity-logs/by-action/{action}")]
	public async Task<IActionResult> GetUserActivityLogsByAction(Guid userId, string action, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var logs = await _userProfileService.GetUserActivityLogsByActionAsync(userId, action, page, pageSize);
		return Ok(logs);
	}

	[HttpGet("{userId}/activity-logs/by-date-range")]
	public async Task<IActionResult> GetUserActivityLogsByDateRange(Guid userId, [FromQuery] DateTimeOffset startDate, [FromQuery] DateTimeOffset endDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
	{
		var logs = await _userProfileService.GetUserActivityLogsByDateRangeAsync(userId, startDate, endDate, page, pageSize);
		return Ok(logs);
	}

	[HttpPost("{userId}/log-activity")]
	public async Task<IActionResult> LogUserActivity(Guid userId, [FromBody] LogUserActivityDto dto)
	{
		var log = await _userProfileService.LogUserActivityAsync(userId, dto);
		return Ok(log);
	}

}
