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
[Route("api/v{version:apiVersion}/positions")]
[ApiVersion("1.0")]
[Authorize]
public class PositionsController : ControllerBase
{
    private readonly IPositionService _positionService;

    public PositionsController(IPositionService positionService)
    {
        _positionService = positionService;
    }

    [HttpGet]
    [HasPermission("Positions.Read")]
    public async Task<IActionResult> GetPositions(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? departmentId,
        [FromQuery] Guid? organizationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortField = null,
        [FromQuery] string? sortDirection = "desc",
        [FromQuery] DateTime? createdFrom = null,
        [FromQuery] DateTime? createdTo = null)
    {
        var positions = await _positionService.GetPositionsAsync(
            search, isActive, departmentId, organizationId, page <= 0 ? 1 : page, pageSize <= 0 ? 20 : pageSize, 
            sortField, sortDirection, createdFrom, createdTo);
        return Ok(positions);
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetPositionsList([FromQuery] bool? isActive = null, [FromQuery] Guid? departmentId = null)
    {
        var positions = await _positionService.GetPositionsAsync(isActive, departmentId);
        return Ok(positions);
    }

    [HttpGet("{id}")]
    [HasPermission("Positions.Read")]
    public async Task<IActionResult> GetPosition(Guid id)
    {
        var position = await _positionService.GetPositionByIdAsync(id);
        if (position == null) return NotFound();
        return Ok(position);
    }

    [HttpPost]
    [HasPermission("Positions.Create")]
    public async Task<IActionResult> CreatePosition([FromBody] CreatePositionDto dto)
    {
        var created = await _positionService.CreatePositionAsync(dto);
        return CreatedAtAction(nameof(GetPosition), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [HasPermission("Positions.Update")]
    public async Task<IActionResult> UpdatePosition(Guid id, [FromBody] UpdatePositionDto dto)
    {
        var updated = await _positionService.UpdatePositionAsync(id, dto);
        if (updated == null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    [HasPermission("Positions.Delete")]
    public async Task<IActionResult> DeletePosition(Guid id)
    {
        var success = await _positionService.DeletePositionAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    [HasPermission("Positions.Delete")]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteRequest request)
    {
        await _positionService.BulkDeleteAsync(request.Ids);
        return NoContent();
    }

    [HttpPost("bulk-clone")]
    [HasPermission("Positions.Create")]
    public async Task<IActionResult> BulkClone([FromBody] BulkDeleteRequest request)
    {
        var clonedPositions = await _positionService.BulkCloneAsync(request.Ids);
        return Ok(new { message = $"{clonedPositions.Count} position(s) cloned successfully", clonedPositions });
    }

    [HttpPut("{id}/active")]
    [HasPermission("Positions.Update")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive)
    {
        var success = await _positionService.SetActiveAsync(id, isActive);
        if (!success) return NotFound();
        return Ok(new { success = true });
    }

    [HttpGet("template")]
    [HasPermission("Positions.Import")]
    public async Task<IActionResult> GetTemplate()
    {
        var templateData = await _positionService.GetImportTemplateAsync();
        return File(templateData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "positions_import_template.xlsx");
    }

    [HttpPost("import/async")]
    [HasPermission("Positions.Import")]
    public async Task<IActionResult> StartImportAsync([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        using var stream = file.OpenReadStream();
        var jobId = await _positionService.StartImportJobAsync(stream, file.FileName);
        return Accepted(new { jobId });
    }

    [HttpGet("import/jobs/{jobId}")]
    public async Task<IActionResult> GetImportJobStatus(string jobId)
    {
        var status = await _positionService.GetImportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Job not found" });
        return Ok(status);
    }

    [HttpGet("import/error-report/{errorReportId}")]
    public async Task<IActionResult> GetImportErrorReport(string errorReportId)
    {
        var errorReport = await _positionService.GetImportErrorReportAsync(errorReportId);
        if (errorReport == null)
            return NotFound(new { message = "Error report not found or expired" });
        return File(errorReport, "text/csv", $"import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpPost("export/async")]
    [HasPermission("Positions.Export")]
    public async Task<IActionResult> StartExportAsync([FromBody] PositionExportRequest request)
    {
        var filters = new Dictionary<string, object?>
        {
            ["search"] = request.Search,
            ["isActive"] = request.IsActive,
            ["departmentId"] = request.DepartmentId,
            ["createdFrom"] = request.CreatedFrom,
            ["createdTo"] = request.CreatedTo
        };

        if (request.SelectedIds != null && request.SelectedIds.Any())
        {
            filters["selectedIds"] = request.SelectedIds;
        }

        var jobId = await _positionService.StartExportJobAsync(request.Format, filters);
        return Accepted(new { jobId });
    }

    [HttpGet("export/jobs/{jobId}")]
    public async Task<IActionResult> GetExportJobStatus(string jobId)
    {
        var status = await _positionService.GetExportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Export job not found" });
        return Ok(status);
    }

    [HttpGet("export/jobs/{jobId}/download")]
    public async Task<IActionResult> DownloadExport(string jobId)
    {
        var fileData = await _positionService.DownloadExportFileAsync(jobId);
        if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

        var status = await _positionService.GetExportJobStatusAsync(jobId);
        var formatLower = status?.Format?.ToLower() ?? "excel";

        var (contentType, extension) = formatLower switch
        {
            "excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            "csv" => ("text/csv", "csv"),
            "pdf" => ("application/pdf", "pdf"),
            "json" => ("application/json", "json"),
            _ => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx")
        };

        var fileName = $"positions_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
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

        var history = await _positionService.GetImportExportHistoryAsync(operationType, page, pageSize);
        return Ok(history);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics()
    {
        var stats = await _positionService.GetStatisticsAsync();
        return Ok(stats);
    }

    [HttpGet("dropdown-options")]
    public async Task<IActionResult> GetDropdownOptions()
    {
        var options = await _positionService.GetDropdownOptionsAsync();
        return Ok(options);
    }
}

public class PositionExportRequest
{
    public ExportFormat Format { get; set; } = ExportFormat.Excel;
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public Guid? DepartmentId { get; set; }
    public List<Guid>? SelectedIds { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
}
