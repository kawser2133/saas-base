using SaaSBase.Api.Attributes;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Implementations;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/roles")]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("AuthPolicy")]
public class RolesController : ControllerBase
{
	private readonly IRoleService _roleService;

	public RolesController(IRoleService roleService)
	{
		_roleService = roleService;
	}

	[HttpGet]
	[HasPermission("Roles.Read")]
    public async Task<IActionResult> GetRoles([FromQuery] string? search, [FromQuery] bool? isActive, [FromQuery] string? roleType, [FromQuery] Guid? organizationId, [FromQuery] DateTime? createdFrom, [FromQuery] DateTime? createdTo, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? sortField = null, [FromQuery] string? sortDirection = "asc")
	{
        var roles = await _roleService.GetRolesAsync(search, isActive, roleType, organizationId, createdFrom, createdTo, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, sortField, sortDirection);
		return Ok(roles);
	}

	[HttpGet("hierarchy")]
	[HasPermission("Roles.Read")]
	public async Task<IActionResult> GetRoleHierarchy()
	{
		var hierarchy = await _roleService.GetRoleHierarchyAsync();
		return Ok(hierarchy);
	}

	[HttpGet("statistics")]
	[HasPermission("Roles.Read")]
	public async Task<IActionResult> GetStatistics()
	{
		var stats = await _roleService.GetStatisticsAsync();
		return Ok(stats);
	}

	[HttpGet("{id}")]
	[HasPermission("Roles.Read")]
	public async Task<IActionResult> GetRole(Guid id)
	{
		var role = await _roleService.GetRoleByIdAsync(id);
		if (role == null) return NotFound();
		return Ok(role);
	}

	[HttpGet("{id}/children")]
	public async Task<IActionResult> GetChildRoles(Guid id)
	{
		var children = await _roleService.GetChildRolesAsync(id);
		return Ok(children);
	}

	[HttpGet("{id}/permissions")]
	public async Task<IActionResult> GetRolePermissions(Guid id)
	{
		var permissions = await _roleService.GetRolePermissionsAsync(id);
		return Ok(permissions);
	}

	[HttpGet("{id}/effective-permissions")]
	public async Task<IActionResult> GetEffectivePermissions(Guid id)
	{
		var permissions = await _roleService.GetEffectivePermissionsAsync(id);
		return Ok(permissions);
	}

	[HttpGet("{id}/users")]
	public async Task<IActionResult> GetRoleUsers(Guid id)
	{
		var users = await _roleService.GetRoleUsersAsync(id);
		return Ok(users);
	}

	[HttpPost]
	[HasPermission("Roles.Create")]
	public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto dto)
	{
		var role = await _roleService.CreateRoleAsync(dto);
		return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
	}

	[HttpPut("{id}")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleDto dto)
	{
		var role = await _roleService.UpdateRoleAsync(id, dto);
		return Ok(role);
	}

	[HttpDelete("{id}")]
	[HasPermission("Roles.Delete")]
	public async Task<IActionResult> DeleteRole(Guid id)
	{
		var success = await _roleService.DeleteRoleAsync(id);
		if (!success) return BadRequest("Cannot delete role. It may be a system role, have child roles, or be assigned to users.");
		return Ok(new { message = "Role deleted successfully" });
	}

	[HttpPost("{roleId}/permissions/{permissionId}")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> AssignPermissionToRole(Guid roleId, Guid permissionId)
	{
		var success = await _roleService.AssignPermissionToRoleAsync(roleId, permissionId);
		if (!success) return BadRequest("Failed to assign permission to role");
		return Ok(new { message = "Permission assigned to role successfully" });
	}

	[HttpDelete("{roleId}/permissions/{permissionId}")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> RemovePermissionFromRole(Guid roleId, Guid permissionId)
	{
		var success = await _roleService.RemovePermissionFromRoleAsync(roleId, permissionId);
		if (!success) return BadRequest("Failed to remove permission from role");
		return Ok(new { message = "Permission removed from role successfully" });
	}

	[HttpPost("assign")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> AssignRoleToUser([FromBody] AssignRoleRequest request)
	{
		var success = await _roleService.AssignRoleToUserAsync(request.UserId, request.RoleId);
		if (!success) return BadRequest("Failed to assign role to user");
		return Ok(new { message = "Role assigned to user successfully" });
	}

	[HttpPost("unassign")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> RemoveRoleFromUser([FromBody] AssignRoleRequest request)
	{
		var success = await _roleService.RemoveRoleFromUserAsync(request.UserId, request.RoleId);
		if (!success) return BadRequest("Failed to remove role from user");
		return Ok(new { message = "Role removed from user successfully" });
	}

	[HttpGet("user/{userId}")]
	public async Task<IActionResult> GetUserRoles(Guid userId)
	{
		var roles = await _roleService.GetUserRolesAsync(userId);
		return Ok(roles);
	}

	[HttpPut("{id}/active")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive)
	{
		var ok = await _roleService.SetActiveAsync(id, isActive);
		if (!ok) return NotFound();
		return Ok(new { success = true });
	}

	[HttpPost("bulk-delete")]
	[HasPermission("Roles.Delete")]
	public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
	{
		await _roleService.BulkDeleteAsync(request.Ids);
		return NoContent();
	}

	[HttpPost("bulk-clone")]
	[HasPermission("Roles.Create")]
	public async Task<IActionResult> BulkClone([FromBody] BulkDeleteRequest request)
	{
		var clonedRoles = await _roleService.BulkCloneAsync(request.Ids);
		return Ok(new { message = $"{clonedRoles.Count} role(s) cloned successfully", clonedRoles });
	}

	// Async export (non-blocking)
	[HttpPost("export/async")]
	[HasPermission("Roles.Export")]
	public async Task<IActionResult> StartExportAsync([FromBody] RoleExportRequest request)
	{
		var filters = new Dictionary<string, object?>
		{
			["search"] = request.Search,
			["isActive"] = request.IsActive,
			["roleType"] = request.RoleType,
			["parentRoleId"] = request.ParentRoleId?.ToString(),
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		// Add selected IDs if provided
		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _roleService.StartExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("export/jobs/{jobId}")]
	public async Task<IActionResult> GetExportJobStatus(string jobId)
	{
		var status = await _roleService.GetExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Export job not found" });
		return Ok(status);
	}

	[HttpGet("export/jobs/{jobId}/download")]
	public async Task<IActionResult> DownloadExport(string jobId)
	{
		var fileData = await _roleService.DownloadExportFileAsync(jobId);
		if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

		var status = await _roleService.GetExportJobStatusAsync(jobId);
		var formatLower = status?.Format?.ToLower() ?? "excel";

		var (contentType, extension) = formatLower switch
		{
			"excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
			"csv" => ("text/csv", "csv"),
			"pdf" => ("application/pdf", "pdf"),
			"json" => ("application/json", "json"),
			_ => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")
		};

		var fileName = $"roles_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
		return File(fileData, contentType, fileName);
	}

	[HttpGet("template")]
	public async Task<IActionResult> GetTemplate()
	{
		var templateData = await _roleService.GetImportTemplateAsync();
		return File(templateData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "roles_import_template.xlsx");
	}

	[HttpPost("import/async")]
	[HasPermission("Roles.Import")]
	public async Task<IActionResult> StartImportAsync([FromForm] IFormFile file, [FromQuery] DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		using var stream = file.OpenReadStream();
		var jobId = await _roleService.StartImportJobAsync(stream, file.FileName, duplicateStrategy);
		return Accepted(new { jobId });
	}

	[HttpGet("import/jobs/{jobId}")]
	public async Task<IActionResult> GetImportJobStatus(string jobId)
	{
		var status = await _roleService.GetImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("import/error-report/{errorReportId}")]
	public async Task<IActionResult> GetImportErrorReport(string errorReportId)
	{
		var errorReport = await _roleService.GetImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
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

		var history = await _roleService.GetImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpGet("dropdown-options")]
	public async Task<IActionResult> GetDropdownOptions()
	{
		var options = await _roleService.GetDropdownOptionsAsync();
		return Ok(options);
	}

	// Additional endpoints for enhanced functionality
	[HttpGet("{id}/detail")]
	public async Task<IActionResult> GetRoleDetail(Guid id)
	{
		var role = await _roleService.GetRoleByIdAsync(id);
		if (role == null) return NotFound();

		// Get additional details
		var permissions = await _roleService.GetRolePermissionsAsync(id);
		var users = await _roleService.GetRoleUsersAsync(id);
		var childRoles = await _roleService.GetChildRolesAsync(id);

		var detail = new RoleDetailDto
		{
			Id = role.Id,
			Name = role.Name,
			Description = role.Description,
			RoleType = role.RoleType,
			ParentRoleId = role.ParentRoleId,
			Level = role.Level,
			IsSystemRole = role.IsSystemRole,
			IsActive = role.IsActive,
			SortOrder = role.SortOrder,
			Color = role.Color,
			Icon = role.Icon,
			CreatedAtUtc = role.CreatedAtUtc,
			LastModifiedAtUtc = role.LastModifiedAtUtc,
			CreatedBy = role.CreatedBy,
			UpdatedBy = role.UpdatedBy,
			UpdatedAtUtc = role.UpdatedAtUtc,
			PermissionCount = role.PermissionCount,
			PermissionNames = role.PermissionNames,
			UserCount = role.UserCount,
			AssignedPermissions = permissions,
			AssignedUsers = users,
			ChildRoles = childRoles
		};

		return Ok(detail);
	}

	[HttpPost("validate")]
	public async Task<IActionResult> ValidateRole([FromBody] CreateRoleDto dto)
	{
		// Basic validation logic
		var validation = new RoleValidationDto { IsValid = true };

		if (string.IsNullOrWhiteSpace(dto.Name))
		{
			validation.IsValid = false;
			validation.Errors.Add("Role name is required");
		}

		if (dto.Name?.Length > 100)
		{
			validation.IsValid = false;
			validation.Errors.Add("Role name cannot exceed 100 characters");
		}

		// Check for duplicate names
		var existingRoles = await _roleService.GetRolesAsync(dto.Name, null, null, null, null, null, 1, 10);
		if (existingRoles.Items.Any())
		{
			validation.IsValid = false;
			validation.Errors.Add("A role with this name already exists");
		}

		return Ok(validation);
	}

	[HttpPost("bulk-assign-permissions")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> BulkAssignPermissions([FromBody] BulkAssignPermissionsRequest request)
	{
		var results = new List<object>();
		
		foreach (var permissionId in request.PermissionIds)
		{
			try
			{
				var success = await _roleService.AssignPermissionToRoleAsync(request.RoleId, permissionId);
				results.Add(new { PermissionId = permissionId, Success = success });
			}
			catch (Exception ex)
			{
				results.Add(new { PermissionId = permissionId, Success = false, Error = ex.Message });
			}
		}

		return Ok(new { Results = results });
	}

	[HttpPost("bulk-remove-permissions")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> BulkRemovePermissions([FromBody] BulkAssignPermissionsRequest request)
	{
		var results = new List<object>();
		
		foreach (var permissionId in request.PermissionIds)
		{
			try
			{
				var success = await _roleService.RemovePermissionFromRoleAsync(request.RoleId, permissionId);
				results.Add(new { PermissionId = permissionId, Success = success });
			}
			catch (Exception ex)
			{
				results.Add(new { PermissionId = permissionId, Success = false, Error = ex.Message });
			}
		}

		return Ok(new { Results = results });
	}

	[HttpPost("bulk-assign-users")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> BulkAssignUsers([FromBody] BulkAssignUsersRequest request)
	{
		var results = new List<object>();
		
		foreach (var userId in request.UserIds)
		{
			try
			{
				var success = await _roleService.AssignRoleToUserAsync(userId, request.RoleId);
				results.Add(new { UserId = userId, Success = success });
			}
			catch (Exception ex)
			{
				results.Add(new { UserId = userId, Success = false, Error = ex.Message });
			}
		}

		return Ok(new { Results = results });
	}

	[HttpPost("bulk-remove-users")]
	[HasPermission("Roles.Update")]
	public async Task<IActionResult> BulkRemoveUsers([FromBody] BulkAssignUsersRequest request)
	{
		var results = new List<object>();
		
		foreach (var userId in request.UserIds)
		{
			try
			{
				var success = await _roleService.RemoveRoleFromUserAsync(userId, request.RoleId);
				results.Add(new { UserId = userId, Success = success });
			}
			catch (Exception ex)
			{
				results.Add(new { UserId = userId, Success = false, Error = ex.Message });
			}
		}

		return Ok(new { Results = results });
	}

	[HttpGet("hierarchy/tree")]
	public async Task<IActionResult> GetRoleHierarchyTree()
	{
		var hierarchy = await _roleService.GetRoleHierarchyAsync();

		// Build hierarchy tree
		var tree = BuildHierarchyTree(hierarchy);

		return Ok(tree);
	}

	private List<RoleHierarchyDto> BuildHierarchyTree(List<RoleHierarchyDto> roles)
	{
		var hierarchy = new List<RoleHierarchyDto>();
		var roleDict = roles.ToDictionary(r => r.Id);

		foreach (var role in roleDict.Values)
		{
			if (role.ParentRoleId.HasValue && roleDict.ContainsKey(role.ParentRoleId.Value))
			{
				roleDict[role.ParentRoleId.Value].Children.Add(role);
			}
			else
			{
				hierarchy.Add(role);
			}
		}

		return hierarchy.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToList();
	}
}

public class RoleExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
	public string? RoleType { get; set; }
	public Guid? ParentRoleId { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}

public class AssignRoleRequest
{
	public Guid UserId { get; set; }
	public Guid RoleId { get; set; }
}

public class BulkAssignPermissionsRequest
{
	public Guid RoleId { get; set; }
	public List<Guid> PermissionIds { get; set; } = new();
}

public class BulkAssignUsersRequest
{
	public Guid RoleId { get; set; }
	public List<Guid> UserIds { get; set; } = new();
}
