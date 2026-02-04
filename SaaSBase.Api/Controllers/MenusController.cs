using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBase.Api.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/menus")]
[ApiVersion("1.0")]
[Authorize]
public class MenusController : ControllerBase
{
	private readonly IMenuService _menuService;
    private readonly ICurrentTenantService _tenantService;

	public MenusController(IMenuService menuService, ICurrentTenantService tenantService)
	{
		_menuService = menuService;
        _tenantService = tenantService;
	}

	[HttpGet]
	[HasPermission("Menus.Read")]
	public async Task<IActionResult> GetMenus(
		[FromQuery] string? search,
		[FromQuery] string? section,
		[FromQuery] Guid? parentMenuId,
		[FromQuery] bool? isActive,
		[FromQuery] DateTime? createdFrom,
		[FromQuery] DateTime? createdTo,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		[FromQuery] string? sortField = null,
		[FromQuery] string? sortDirection = "asc")
	{
		var menus = await _menuService.GetMenusAsync(search, section, parentMenuId, isActive, createdFrom, createdTo, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, sortField, sortDirection);
		return Ok(menus);
	}

	[HttpGet("{id}")]
	[HasPermission("Menus.Read")]
	public async Task<IActionResult> GetMenu(Guid id)
	{
		var menu = await _menuService.GetMenuByIdAsync(id);
		if (menu == null) return NotFound();
		return Ok(menu);
	}

	[HttpPost]
	[HasPermission("Menus.Create")]
	public async Task<IActionResult> CreateMenu([FromBody] CreateMenuDto dto)
	{
		var menu = await _menuService.CreateMenuAsync(dto);
		return CreatedAtAction(nameof(GetMenu), new { id = menu.Id }, menu);
	}

	[HttpPut("{id}")]
	[HasPermission("Menus.Update")]
	public async Task<IActionResult> UpdateMenu(Guid id, [FromBody] UpdateMenuDto dto)
	{
		var menu = await _menuService.UpdateMenuAsync(id, dto);
		return Ok(menu);
	}

	[HttpDelete("{id}")]
	[HasPermission("Menus.Delete")]
	public async Task<IActionResult> DeleteMenu(Guid id)
	{
		var success = await _menuService.DeleteMenuAsync(id);
		if (!success) return BadRequest("Cannot delete menu. It may be a system menu, have child menus, or be assigned to permissions.");
		return Ok(new { message = "Menu deleted successfully" });
	}

	[HttpPost("bulk-delete")]
	[HasPermission("Menus.Delete")]
	public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
	{
		await _menuService.BulkDeleteAsync(request.Ids);
		return NoContent();
	}

	[HttpPost("bulk-clone")]
	[HasPermission("Menus.Create")]
	public async Task<IActionResult> BulkClone([FromBody] BulkDeleteRequest request)
	{
		var clonedMenus = await _menuService.BulkCloneAsync(request.Ids);
		return Ok(new { message = $"{clonedMenus.Count} menu(s) cloned successfully", clonedMenus });
	}

	[HttpGet("dropdown")]
	public async Task<IActionResult> GetMenuDropdownOptions()
	{
		var options = await _menuService.GetMenuDropdownOptionsAsync();
		return Ok(options);
	}

	[HttpGet("sections")]
	public async Task<IActionResult> GetSections()
	{
		var sections = await _menuService.GetUniqueSectionsAsync();
		return Ok(sections);
	}

	[HttpGet("by-section/{section}")]
	public async Task<IActionResult> GetMenusBySection(string section)
	{
		var menus = await _menuService.GetMenusBySectionAsync(section);
		return Ok(menus);
	}

	[HttpGet("by-parent/{parentMenuId}")]
	public async Task<IActionResult> GetChildMenus(Guid parentMenuId)
	{
		var menus = await _menuService.GetChildMenusAsync(parentMenuId);
		return Ok(menus);
	}

	[HttpGet("user/{userId}/navigation")]
	public async Task<IActionResult> GetUserMenus(Guid userId)
	{
		var currentUserId = _tenantService.GetCurrentUserId();
		if (currentUserId == Guid.Empty)
			return Unauthorized();

		// Disallow fetching other users' menus via this endpoint
		if (userId != currentUserId)
			return Forbid();

		var menus = await _menuService.GetUserMenusAsync(currentUserId);
		return Ok(menus);
	}

	[HttpGet("user/navigation")]
	public async Task<IActionResult> GetCurrentUserMenus()
	{
		var currentUserId = _tenantService.GetCurrentUserId();
		if (currentUserId == Guid.Empty)
			return Unauthorized();
		var menus = await _menuService.GetUserMenusAsync(currentUserId);
		return Ok(menus);
	}

	[HttpGet("statistics")]
	public async Task<IActionResult> GetStatistics()
	{
		var stats = await _menuService.GetStatisticsAsync();
		return Ok(stats);
	}

	[HttpGet("hierarchy")]
	public async Task<IActionResult> GetMenuHierarchy()
	{
		var hierarchy = await _menuService.GetMenuHierarchyAsync();
		return Ok(hierarchy);
	}

	[HttpPut("{id}/active")]
	[HasPermission("Menus.Update")]
	public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive)
	{
		var success = await _menuService.SetActiveAsync(id, isActive);
		if (!success) return NotFound();
		return Ok(new { message = $"Menu {(isActive ? "activated" : "deactivated")} successfully" });
	}

	[HttpGet("template")]
	[HasPermission("Menus.Import")]
	public async Task<IActionResult> GetTemplate()
	{
		var templateData = await _menuService.GetImportTemplateAsync();
		return File(templateData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "menus_import_template.xlsx");
	}

	[HttpPost("import/async")]
	[HasPermission("Menus.Import")]
	public async Task<IActionResult> StartImportAsync([FromForm] IFormFile file)
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		using var stream = file.OpenReadStream();
		var jobId = await _menuService.StartImportJobAsync(stream, file.FileName);
		return Accepted(new { jobId });
	}

	[HttpGet("import/jobs/{jobId}")]
	public async Task<IActionResult> GetImportJobStatus(string jobId)
	{
		var status = await _menuService.GetImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("import/error-report/{errorReportId}")]
	public async Task<IActionResult> GetImportErrorReport(string errorReportId)
	{
		var errorReport = await _menuService.GetImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
		return File(errorReport, "text/csv", $"import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	[HttpPost("export/async")]
	[HasPermission("Menus.Export")]
	public async Task<IActionResult> StartExportAsync([FromBody] MenuExportRequest request)
	{
		// Clean up empty strings and invalid values
		var filters = new Dictionary<string, object?>
		{
			["search"] = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search,
			["section"] = string.IsNullOrWhiteSpace(request.Section) ? null : request.Section,
			["parentMenuId"] = request.ParentMenuId.HasValue && request.ParentMenuId.Value != Guid.Empty ? request.ParentMenuId : null,
			["isActive"] = request.IsActive,
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _menuService.StartExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("export/jobs/{jobId}")]
	public async Task<IActionResult> GetExportJobStatus(string jobId)
	{
		var status = await _menuService.GetExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Export job not found" });
		return Ok(status);
	}

	[HttpGet("export/jobs/{jobId}/download")]
	public async Task<IActionResult> DownloadExport(string jobId)
	{
		var fileData = await _menuService.DownloadExportFileAsync(jobId);
		if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

		var status = await _menuService.GetExportJobStatusAsync(jobId);
		var formatLower = status?.Format?.ToLower() ?? "excel";

		var (contentType, extension) = formatLower switch
		{
			"excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
			"csv" => ("text/csv", "csv"),
			"pdf" => ("application/pdf", "pdf"),
			"json" => ("application/json", "json"),
			_ => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")
		};

		var fileName = $"menus_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
		return File(fileData, contentType, fileName);
	}

	[HttpGet("history")]
	public async Task<IActionResult> GetHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null
		};

		var history = await _menuService.GetImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}
}

public class MenuExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public string? Section { get; set; }
	public Guid? ParentMenuId { get; set; }
	public bool? IsActive { get; set; }
	public DateTime? CreatedFrom { get; set; }
	public DateTime? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}

