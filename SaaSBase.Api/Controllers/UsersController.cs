using SaaSBase.Api.Attributes;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("AuthPolicy")]
public class UsersController : ControllerBase
{
	private readonly IUserService _userService;

	private readonly IPasswordPolicyService _passwordPolicyService;

	public UsersController(IUserService userService, IPasswordPolicyService passwordPolicyService)
	{
		_userService = userService;
		_passwordPolicyService = passwordPolicyService;
	}


	[HttpGet]
	[HasPermission("Users.Read")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] string? department, [FromQuery] string? jobTitle, [FromQuery] string? location, [FromQuery] bool? isActive, [FromQuery] bool? isEmailVerified, [FromQuery] Guid? roleId, [FromQuery] Guid? organizationId, [FromQuery] DateTimeOffset? createdFrom, [FromQuery] DateTimeOffset? createdTo, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? sortField = "createdAtUtc", [FromQuery] string? sortDirection = "desc")
	{
        var users = await _userService.GetUsersAsync(search, department, jobTitle, location, isActive, isEmailVerified, roleId, organizationId, createdFrom, createdTo, page <= 0 ? 1 : page, pageSize <= 0 ? 10 : pageSize, sortField, sortDirection);
		return Ok(users);
	}

	[HttpGet("{id}")]
	[HasPermission("Users.Read")]
	public async Task<IActionResult> GetUser(Guid id)
	{
		var user = await _userService.GetUserByIdAsync(id);
		if (user == null) return NotFound();
		return Ok(user);
	}

	[HttpPost]
	[HasPermission("Users.Create")]
	public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
	{
		var created = await _userService.CreateUserAsync(dto);
		return CreatedAtAction(nameof(GetUser), new { id = created.Id }, created);
	}

	[HttpPut("{id}")]
	[HasPermission("Users.Update")]
	public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto dto)
	{
		var updated = await _userService.UpdateUserAsync(id, dto);
		if (updated == null) return NotFound();
		return Ok(updated);
	}

	[HttpPut("{id}/active")]
	[HasPermission("Users.Update")]
	public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive)
	{
		var ok = await _userService.SetActiveAsync(id, isActive);
		if (!ok) return NotFound();
		return Ok(new { success = true });
	}

	[HttpDelete("{id}")]
	[HasPermission("Users.Delete")]
	public async Task<IActionResult> Delete(Guid id)
	{
		var ok = await _userService.DeleteUserAsync(id);
		if (!ok) return NotFound();
		return NoContent();
	}

	[HttpPost("bulk-delete")]
	[HasPermission("Users.Delete")]
	public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
	{
		await _userService.BulkDeleteAsync(request.Ids);
		return NoContent();
	}

	[HttpPost("bulk-clone")]
	[HasPermission("Users.Create")]
	public async Task<IActionResult> BulkClone([FromBody] BulkDeleteRequest request)
	{
		var clonedUsers = await _userService.BulkCloneAsync(request.Ids);
		return Ok(new { message = $"{clonedUsers.Count} user(s) cloned successfully", clonedUsers });
	}

	// Async export (non-blocking)
	[HttpPost("export/async")]
	[HasPermission("Users.Export")]
	public async Task<IActionResult> StartExportAsync([FromBody] ExportRequest request)
	{
		var filters = new Dictionary<string, object?>
		{
			["search"] = request.Search,
			["department"] = request.Department,
			["jobTitle"] = request.JobTitle,
			["location"] = request.Location,
			["isActive"] = request.IsActive,
			["isEmailVerified"] = request.IsEmailVerified,
			["roleId"] = request.RoleId,
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		// Add selected IDs if provided
		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _userService.StartExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("export/jobs/{jobId}")]
	public async Task<IActionResult> GetExportJobStatus(string jobId)
	{
		var status = await _userService.GetExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Export job not found" });
		return Ok(status);
	}

	[HttpGet("export/jobs/{jobId}/download")]
	public async Task<IActionResult> DownloadExport(string jobId)
	{
		var fileData = await _userService.DownloadExportFileAsync(jobId);
		if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

		var status = await _userService.GetExportJobStatusAsync(jobId);
		var formatLower = status?.Format?.ToLower() ?? "excel";

		var (contentType, extension) = formatLower switch
		{
			"excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
			"csv" => ("text/csv", "csv"),
			"pdf" => ("application/pdf", "pdf"),
			"json" => ("application/json", "json"),
			_ => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")
		};

		var fileName = $"users_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
		return File(fileData, contentType, fileName);
	}

	[HttpGet("template")]
	public async Task<IActionResult> GetTemplate()
	{
		var templateData = await _userService.GetImportTemplateAsync();
		return File(templateData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "users_import_template.xlsx");
	}

	[HttpGet("statistics")]
	public async Task<IActionResult> GetStatistics()
	{
		var stats = await _userService.GetUserStatisticsAsync();
		return Ok(stats);
	}

    [HttpPost("import/async")]
	[HasPermission("Users.Import")]
    public async Task<IActionResult> StartImportAsync([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        using var stream = file.OpenReadStream();
        var jobId = await _userService.StartImportJobAsync(stream, file.FileName, DuplicateHandlingStrategy.Skip);
        return Accepted(new { jobId });
    }

    [HttpGet("import/jobs/{jobId}")]
    public async Task<IActionResult> GetImportJobStatus(string jobId)
    {
        var status = await _userService.GetImportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Job not found" });
        return Ok(status);
    }

	[HttpGet("import/error-report/{errorReportId}")]
	public async Task<IActionResult> GetImportErrorReport(string errorReportId)
	{
		var errorReport = await _userService.GetImportErrorReportAsync(errorReportId);

		if (errorReport == null)
		{
			return NotFound(new { message = "Error report not found or expired" });
		}

		return File(errorReport, "text/csv", $"import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	// Unified Import/Export History (using ImportExportHistory table)
	[HttpGet("history")]
	public async Task<IActionResult> GetHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null // null = show both import and export
		};

		var history = await _userService.GetImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpPost("{id}/password-reset")]
	[HasPermission("Users.Update")]
	public async Task<IActionResult> GeneratePasswordReset(Guid id)
	{
		// Check if user exists and email is verified
		var user = await _userService.GetByIdAsync(id);
		if (user == null)
		{
			return NotFound(new { success = false, message = "User not found" });
		}

		if (!user.IsEmailVerified)
		{
			return BadRequest(new { success = false, message = "User must verify their email before password reset can be generated" });
		}

		await _userService.GeneratePasswordResetLinkAsync(id);
		return Ok(new { success = true, message = "Password reset link sent" });
	}

	[HttpPost("{id}/email-verification")]
	[HasPermission("Users.Update")]
	public async Task<IActionResult> SendEmailVerification(Guid id)
	{
		await _userService.SendEmailVerificationAsync(id);
		return Ok(new { success = true, message = "Email verification sent" });
	}

	[HttpPost("{id}/unlock")]
	[HasPermission("Users.Update")]
	public async Task<IActionResult> UnlockUser(Guid id)
	{
		// Check if user exists
		var user = await _userService.GetByIdAsync(id);
		if (user == null)
		{
			return NotFound(new { success = false, message = "User not found" });
		}

		// Unlock user by resetting failed attempts
		var success = await _passwordPolicyService.ResetFailedAttemptsAsync(id);
		if (!success)
		{
			return BadRequest(new { success = false, message = "Failed to unlock user" });
		}

		return Ok(new { success = true, message = "User unlocked successfully" });
	}

	[HttpGet("dropdown-options")]
	public async Task<IActionResult> GetDropdownOptions()
	{
		var options = await _userService.GetDropdownOptionsAsync();
		return Ok(options);
	}

	[HttpGet("dropdown-options/locations")]
	public async Task<IActionResult> GetLocationOptions()
	{
		var locations = await _userService.GetLocationOptionsAsync();
		return Ok(locations);
	}

	[HttpGet("dropdown-options/departments")]
	public async Task<IActionResult> GetDepartmentOptions()
	{
		var departments = await _userService.GetDepartmentOptionsAsync();
		return Ok(departments);
	}

	[HttpGet("dropdown-options/positions")]
	public async Task<IActionResult> GetPositionOptions()
	{
		var positions = await _userService.GetPositionOptionsAsync();
		return Ok(positions);
	}
}

public class ExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public string? Department { get; set; }
	public string? JobTitle { get; set; }
	public string? Location { get; set; }
	public bool? IsActive { get; set; }
	public bool? IsEmailVerified { get; set; }
	public Guid? RoleId { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}


