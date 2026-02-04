using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBase.Api.Attributes;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/departments")]
[ApiVersion("1.0")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    [HttpGet]
    [HasPermission("Departments.Read")]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? organizationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDirection = "desc",
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null)
    {
        var departments = await _departmentService.GetDepartmentsAsync(
            search, isActive, organizationId, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, 
            sortField, sortDirection, createdFrom, createdTo);
        return Ok(departments);
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetDepartmentsList([FromQuery] bool? isActive = null)
    {
        var departments = await _departmentService.GetDepartmentsAsync(isActive);
        return Ok(departments);
    }

    [HttpGet("{id}")]
    [HasPermission("Departments.Read")]
    public async Task<IActionResult> GetDepartment(Guid id)
    {
        var department = await _departmentService.GetDepartmentByIdAsync(id);
        if (department == null) return NotFound();
        return Ok(department);
    }

    [HttpPost]
    [HasPermission("Departments.Create")]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentDto dto)
    {
        var created = await _departmentService.CreateDepartmentAsync(dto);
        return CreatedAtAction(nameof(GetDepartment), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [HasPermission("Departments.Update")]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] UpdateDepartmentDto dto)
    {
        var updated = await _departmentService.UpdateDepartmentAsync(id, dto);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [HasPermission("Departments.Delete")]
    public async Task<IActionResult> DeleteDepartment(Guid id)
    {
        var success = await _departmentService.DeleteDepartmentAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    [HasPermission("Departments.Delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
    {
        await _departmentService.BulkDeleteAsync(request.Ids);
        return NoContent();
    }

    [HttpPost("bulk-clone")]
    [HasPermission("Departments.Create")]
    public async Task<IActionResult> BulkClone([FromBody] BulkDeleteRequest request)
    {
        var clonedDepartments = await _departmentService.BulkCloneAsync(request.Ids);
        return Ok(new { message = $"{clonedDepartments.Count} department(s) cloned successfully", clonedDepartments });
    }

    [HttpPut("{id}/active")]
    [HasPermission("Departments.Update")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive)
    {
        var success = await _departmentService.SetActiveAsync(id, isActive);
        if (!success) return NotFound();
        return Ok(new { success = true });
    }

    [HttpGet("template")]
    [HasPermission("Departments.Import")]
    public async Task<IActionResult> GetTemplate()
    {
        var templateData = await _departmentService.GetImportTemplateAsync();
        return File(templateData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "departments_import_template.xlsx");
    }

    [HttpPost("import/async")]
    [HasPermission("Departments.Import")]
    public async Task<IActionResult> StartImportAsync([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        using var stream = file.OpenReadStream();
        var jobId = await _departmentService.StartImportJobAsync(stream, file.FileName);
        return Accepted(new { jobId });
    }

    [HttpGet("import/jobs/{jobId}")]
    public async Task<IActionResult> GetImportJobStatus(string jobId)
    {
        var status = await _departmentService.GetImportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Job not found" });
        return Ok(status);
    }

    [HttpGet("import/error-report/{errorReportId}")]
    public async Task<IActionResult> GetImportErrorReport(string errorReportId)
    {
        var errorReport = await _departmentService.GetImportErrorReportAsync(errorReportId);
        if (errorReport == null)
            return NotFound(new { message = "Error report not found or expired" });
        return File(errorReport, "text/csv", $"import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("export/async")]
    [HasPermission("Departments.Export")]
    public async Task<IActionResult> StartExportAsync([FromBody] DepartmentExportRequest request)
    {
        var filters = new Dictionary<string, object?>
        {
            ["search"] = request.Search,
            ["isActive"] = request.IsActive,
            ["createdFrom"] = request.CreatedFrom,
            ["createdTo"] = request.CreatedTo
        };

        if (request.SelectedIds != null && request.SelectedIds.Any())
        {
            filters["selectedIds"] = request.SelectedIds;
        }

        var jobId = await _departmentService.StartExportJobAsync(request.Format, filters);
        return Accepted(new { jobId });
    }

    [HttpGet("export/jobs/{jobId}")]
    public async Task<IActionResult> GetExportJobStatus(string jobId)
    {
        var status = await _departmentService.GetExportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Export job not found" });
        return Ok(status);
    }

    [HttpGet("export/jobs/{jobId}/download")]
    public async Task<IActionResult> DownloadExport(string jobId)
    {
        var fileData = await _departmentService.DownloadExportFileAsync(jobId);
        if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

        var status = await _departmentService.GetExportJobStatusAsync(jobId);
        var formatLower = status?.Format?.ToLower() ?? "excel";

        var (contentType, extension) = formatLower switch
        {
            "excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            "csv" => ("text/csv", "csv"),
            "pdf" => ("application/pdf", "pdf"),
            "json" => ("application/json", "json"),
            _ => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")
        };

        var fileName = $"departments_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
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

        var history = await _departmentService.GetImportExportHistoryAsync(operationType, page, pageSize);
        return Ok(history);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var stats = await _departmentService.GetStatisticsAsync();
        return Ok(stats);
    }

    [HttpGet("dropdown-options")]
    public async Task<IActionResult> GetDropdownOptions()
    {
        var options = await _departmentService.GetDropdownOptionsAsync();
        return Ok(options);
    }
}

public class DepartmentExportRequest
{
    public ExportFormat Format { get; set; } = ExportFormat.Excel;
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public List<Guid>? SelectedIds { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}
