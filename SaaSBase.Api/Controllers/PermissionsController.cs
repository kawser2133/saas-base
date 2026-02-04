using SaaSBase.Api.Attributes;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/permissions")]
[ApiVersion("1.0")]
[Authorize]
[EnableRateLimiting("AuthPolicy")]
public class PermissionsController : ControllerBase
{
	private readonly IPermissionService _permissionService;

	public PermissionsController(IPermissionService permissionService)
	{
		_permissionService = permissionService;
	}

    [HttpGet]
	[HasPermission("Permissions.Read")]
    public async Task<IActionResult> GetPermissions([FromQuery] string? search, [FromQuery] string? category, [FromQuery] string? module, [FromQuery] string? action, [FromQuery] bool? isActive, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? sortField = null, [FromQuery] string? sortDirection = "asc", [FromQuery] DateTime? createdFrom = null, [FromQuery] DateTime? createdTo = null)
	{
        var permissions = await _permissionService.GetPermissionsAsync(search, category, module, action, isActive, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, sortField, sortDirection, createdFrom, createdTo);
		return Ok(permissions);
	}

	[HttpGet("{id}")]
	[HasPermission("Permissions.Read")]
	public async Task<IActionResult> GetPermission(Guid id)
	{
		var permission = await _permissionService.GetPermissionByIdAsync(id);
		if (permission == null) return NotFound();
		return Ok(permission);
	}

	[HttpGet("by-code/{code}")]
	public async Task<IActionResult> GetPermissionByCode(string code)
	{
		var permission = await _permissionService.GetPermissionByCodeAsync(code);
		if (permission == null) return NotFound();
		return Ok(permission);
	}

	[HttpGet("by-module/{module}")]
	public async Task<IActionResult> GetPermissionsByModule(string module)
	{
		var permissions = await _permissionService.GetPermissionsByModuleAsync(module);
		return Ok(permissions);
	}

	[HttpGet("by-category/{category}")]
	public async Task<IActionResult> GetPermissionsByCategory(string category)
	{
		var permissions = await _permissionService.GetPermissionsByCategoryAsync(category);
		return Ok(permissions);
	}

    [HttpGet("modules")]
    public async Task<IActionResult> GetModules()
    {
        var modules = await _permissionService.GetUniqueModulesAsync();
        return Ok(modules);
    }

    [HttpGet("actions")]
    public async Task<IActionResult> GetActions()
    {
        var actions = await _permissionService.GetUniqueActionsAsync();
        return Ok(actions);
    }

	[HttpPost]
	[HasPermission("Permissions.Create")]
	public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionDto dto)
	{
		var permission = await _permissionService.CreatePermissionAsync(dto);
		return CreatedAtAction(nameof(GetPermission), new { id = permission.Id }, permission);
	}

	[HttpPut("{id}")]
	[HasPermission("Permissions.Update")]
	public async Task<IActionResult> UpdatePermission(Guid id, [FromBody] UpdatePermissionDto dto)
	{
		var permission = await _permissionService.UpdatePermissionAsync(id, dto);
		return Ok(permission);
	}

	[HttpDelete("{id}")]
	[HasPermission("Permissions.Delete")]
	public async Task<IActionResult> DeletePermission(Guid id)
	{
		var success = await _permissionService.DeletePermissionAsync(id);
		if (!success) return BadRequest("Cannot delete permission. It may be a system permission or assigned to roles.");
		return Ok(new { message = "Permission deleted successfully" });
	}

    [HttpPost("bulk-delete")]
    [HasPermission("Permissions.Delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
    {
        await _permissionService.BulkDeleteAsync(request.Ids);
        return NoContent();
    }

    [HttpPost("bulk-clone")]
    [HasPermission("Permissions.Create")]
    public async Task<IActionResult> BulkClone([FromBody] BulkDeleteRequest request)
    {
        var clonedPermissions = await _permissionService.BulkCloneAsync(request.Ids);
        return Ok(new { message = $"{clonedPermissions.Count} permission(s) cloned successfully", clonedPermissions });
    }

	[HttpGet("user/{userId}/check/{permissionCode}")]
	public async Task<IActionResult> CheckUserPermission(Guid userId, string permissionCode)
	{
		var hasPermission = await _permissionService.UserHasPermissionAsync(userId, permissionCode);
		return Ok(new { hasPermission });
	}

	[HttpGet("user/{userId}/codes")]
	public async Task<IActionResult> GetUserPermissionCodes(Guid userId)
	{
		var codes = await _permissionService.GetUserPermissionCodesAsync(userId);
		return Ok(codes);
	}

	[HttpPost("seed-default")]
	public async Task<IActionResult> SeedDefaultPermissions()
	{
		var success = await _permissionService.SeedDefaultPermissionsAsync();
		return Ok(new { success, message = "Default permissions seeded successfully" });
	}

    // Old synchronous export/import endpoints removed - use async versions instead:
    // POST /api/v1/permissions/export/async
    // POST /api/v1/permissions/import/async

    [HttpGet("template")]
    public async Task<IActionResult> GetTemplate()
    {
        var templateData = await _permissionService.GetImportTemplateAsync();
        return File(templateData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "permissions_import_template.xlsx");
    }

    [HttpGet("import/history")]
    public async Task<IActionResult> GetImportHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        // Use unified import/export history instead of old import-only history
        var history = await _permissionService.GetImportExportHistoryAsync(ImportExportType.Import, page, pageSize);
        return Ok(history);
    }

    [HttpGet("import/error-report/{errorReportId}")]
    public async Task<IActionResult> GetImportErrorReport(string errorReportId)
    {
        var errorReport = await _permissionService.GetImportErrorReportAsync(errorReportId);
        if (errorReport == null)
            return NotFound(new { message = "Error report not found or expired" });
        return File(errorReport, "text/csv", $"import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("import/async")]
	[HasPermission("Permissions.Import")]
    public async Task<IActionResult> StartImportAsync([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        using var stream = file.OpenReadStream();
        var jobId = await _permissionService.StartImportJobAsync(stream, file.FileName);
        return Accepted(new { jobId });
    }

    [HttpGet("import/jobs/{jobId}")]
    public async Task<IActionResult> GetImportJobStatus(string jobId)
    {
        var status = await _permissionService.GetImportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Job not found" });
        return Ok(status);
    }

    // ========================================
    // Async Export (non-blocking)
    // ========================================

    [HttpPost("export/async")]
	[HasPermission("Permissions.Export")]
    public async Task<IActionResult> StartExportAsync([FromBody] PermissionExportRequest request)
    {
        var filters = new Dictionary<string, object?>
        {
            ["search"] = request.Search,
            ["category"] = request.Category,
            ["module"] = request.Module,
            ["action"] = request.Action,
            ["isActive"] = request.IsActive,
            ["createdFrom"] = request.CreatedFrom,
            ["createdTo"] = request.CreatedTo
        };

        // Add selected IDs if provided
        if (request.SelectedIds != null && request.SelectedIds.Any())
        {
            filters["selectedIds"] = request.SelectedIds;
        }

        var jobId = await _permissionService.StartExportJobAsync(request.Format, filters);
        return Accepted(new { jobId });
    }

    [HttpGet("export/jobs/{jobId}")]
    public async Task<IActionResult> GetExportJobStatus(string jobId)
    {
        var status = await _permissionService.GetExportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Export job not found" });
        return Ok(status);
    }

    [HttpGet("export/jobs/{jobId}/download")]
    public async Task<IActionResult> DownloadExport(string jobId)
    {
        var fileData = await _permissionService.DownloadExportFileAsync(jobId);
        if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

        var status = await _permissionService.GetExportJobStatusAsync(jobId);
        var formatLower = status?.Format?.ToLower() ?? "excel";

        var (contentType, extension) = formatLower switch
        {
            "excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            "csv" => ("text/csv", "csv"),
            "pdf" => ("application/pdf", "pdf"),
            "json" => ("application/json", "json"),
            _ => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")
        };

        var fileName = $"permissions_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
        return File(fileData, contentType, fileName);
    }

    // ========================================
    // Unified Import/Export History
    // ========================================

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        ImportExportType? operationType = type?.ToLower() switch
        {
            "import" => ImportExportType.Import,
            "export" => ImportExportType.Export,
            _ => null // null = show both import and export
        };

        var history = await _permissionService.GetImportExportHistoryAsync(operationType, page, pageSize);
        return Ok(history);
    }

    // ========================================
    // Statistics & Dropdown Options
    // ========================================

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var stats = await _permissionService.GetStatisticsAsync();
        return Ok(stats);
    }

    [HttpGet("dropdown-options")]
    public async Task<IActionResult> GetDropdownOptions()
    {
        var options = await _permissionService.GetDropdownOptionsAsync();
        return Ok(options);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _permissionService.GetUniqueCategoriesAsync();
        return Ok(categories);
    }

    // Adapter to convert IFormFile to IFileUpload
    private class FormFileAdapter : IFileUpload
    {
        private readonly IFormFile _file;

        public FormFileAdapter(IFormFile file)
        {
            _file = file;
        }

        public string FileName => _file.FileName;
        public long Length => _file.Length;
        public string ContentType => _file.ContentType;
        public Stream OpenReadStream() => _file.OpenReadStream();
        public Task<Stream> OpenReadStreamAsync() => Task.FromResult(_file.OpenReadStream());
    }
}

public class PermissionExportRequest
{
    public ExportFormat Format { get; set; } = ExportFormat.Excel;
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }
    public bool? IsActive { get; set; }
    public List<Guid>? SelectedIds { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}
