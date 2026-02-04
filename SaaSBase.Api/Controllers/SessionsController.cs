using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBase.Api.Attributes;
using System.Security.Claims;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/sessions")]
[ApiVersion("1.0")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentTenantService _tenantService;

    public SessionsController(ISessionService sessionService, IPermissionService permissionService, ICurrentTenantService tenantService)
	{
		_sessionService = sessionService;
        _permissionService = permissionService;
        _tenantService = tenantService;
	}

    [HttpGet]
    [HasPermission("Sessions.Read")]
    public async Task<IActionResult> GetSessions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? organizationId = null, [FromQuery] string? search = null, [FromQuery] string? sortField = null, [FromQuery] string? sortDirection = null)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        // Resolve current user from tenant context
        var currentUserId = _tenantService.GetCurrentUserId();

        // System Admin or anyone with Sessions.ReadAll sees org-wide sessions; others see own sessions
        var canReadAll = currentUserId != Guid.Empty && await _permissionService.UserHasPermissionAsync(currentUserId, "Sessions.ReadAll");
        if (canReadAll)
        {
            var orgSessions = await _sessionService.GetOrganizationSessionsAsync(page, pageSize, organizationId, search, sortField, sortDirection);
            return Ok(orgSessions);
        }

        var sessions = await _sessionService.GetUserSessionsAsync(currentUserId, page, pageSize, search, sortField, sortDirection);
        return Ok(sessions);
    }

    // ========================================
    // Async Export (non-blocking) - follows permissions pattern
    // ========================================

    public class SessionsExportRequest
    {
        public string? Search { get; set; }
        public string? SortField { get; set; }
        public string? SortDirection { get; set; } = "desc";
        public ExportFormat Format { get; set; } = ExportFormat.CSV;
        public List<string>? SelectedIds { get; set; }
    }

    public class BulkRevokeSessionsRequest
    {
        public List<string> SessionIds { get; set; } = new();
    }

    [HttpPost("export/async")]
    [HasPermission("Sessions.Read")]
    public async Task<IActionResult> StartExportAsync([FromBody] SessionsExportRequest request)
    {
        var filters = new Dictionary<string, object?>
        {
            ["search"] = request.Search,
            ["sortField"] = request.SortField,
            ["sortDirection"] = request.SortDirection
        };

        // Add selected IDs if provided
        if (request.SelectedIds != null && request.SelectedIds.Any())
        {
            filters["selectedIds"] = request.SelectedIds;
        }

        var jobId = await _sessionService.StartExportJobAsync(request.Format, filters);
        return Accepted(new { jobId });
    }

    [HttpGet("export/jobs/{jobId}")]
    public async Task<IActionResult> GetExportJobStatus(string jobId)
    {
        var status = await _sessionService.GetExportJobStatusAsync(jobId);
        if (status == null) return NotFound(new { message = "Export job not found" });
        return Ok(status);
    }

    [HttpGet("export/jobs/{jobId}/download")]
    public async Task<IActionResult> DownloadExport(string jobId)
    {
        var fileData = await _sessionService.DownloadExportFileAsync(jobId);
        if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

        var status = await _sessionService.GetExportJobStatusAsync(jobId);
        var formatLower = status?.Format?.ToLower() ?? "csv";

        var (contentType, extension) = formatLower switch
        {
            "excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            "csv" => ("text/csv", "csv"),
            "pdf" => ("application/pdf", "pdf"),
            "json" => ("application/json", "json"),
            _ => ("text/csv", "csv")
        };

        var fileName = $"sessions_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
        return File(fileData, contentType, fileName);
    }

	[HttpGet("my-sessions")]
	// No permission check needed - any authenticated user can view their own sessions (for settings page)
	public async Task<IActionResult> GetMySessions([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? search = null, [FromQuery] string? sortField = null, [FromQuery] string? sortDirection = null)
	{
		page = page <= 0 ? 1 : page;
		pageSize = pageSize <= 0 ? 10 : pageSize;

		// Always get current user's sessions only (for settings page)
		var currentUserId = _tenantService.GetCurrentUserId();
		if (currentUserId == Guid.Empty)
		{
			return Unauthorized(new { message = "User not authenticated" });
		}

		var sessions = await _sessionService.GetUserSessionsAsync(currentUserId, page, pageSize, search, sortField, sortDirection);
		return Ok(sessions);
	}

	[HttpGet("active")]
	[HasPermission("Sessions.Read")]
	public async Task<IActionResult> GetActiveSessions([FromQuery] Guid userId)
	{
		var sessions = await _sessionService.GetActiveSessionsAsync(userId);
		return Ok(sessions);
	}

	[HttpGet("{sessionId}")]
	[HasPermission("Sessions.Read")]
	public async Task<IActionResult> GetSession(string sessionId)
	{
		var session = await _sessionService.GetSessionAsync(sessionId);
		if (session == null) return NotFound();
		return Ok(session);
	}

	[HttpPost("{sessionId}/activity")]
	[HasPermission("Sessions.Update")]
	public async Task<IActionResult> UpdateSessionActivity(string sessionId)
	{
		var success = await _sessionService.UpdateSessionActivityAsync(sessionId);
		if (!success) return NotFound();
		return Ok(new { message = "Session activity updated" });
	}

	[HttpDelete("{sessionId}")]
	// Allow users to revoke their own sessions without Sessions.Delete permission (for settings page)
	public async Task<IActionResult> RevokeSession(string sessionId)
	{
		var currentUserId = _tenantService.GetCurrentUserId();
		
		// Check if session belongs to current user
		var session = await _sessionService.GetSessionAsync(sessionId);
		if (session == null) return NotFound();
		
		// If session belongs to current user, allow revoke without permission check
		// Otherwise, require Sessions.Delete permission
		if (session.UserId != currentUserId)
		{
			var hasPermission = currentUserId != Guid.Empty && await _permissionService.UserHasPermissionAsync(currentUserId, "Sessions.Delete");
			if (!hasPermission)
			{
				return Forbid("You don't have permission to revoke this session");
			}
		}
		
		var success = await _sessionService.RevokeSessionAsync(sessionId);
		if (!success) return NotFound();
		return Ok(new { message = "Session revoked successfully" });
	}

	[HttpPost("bulk-revoke")]
	[HasPermission("Sessions.Delete")]
	public async Task<IActionResult> BulkRevokeSessions([FromBody] BulkRevokeSessionsRequest request)
	{
		if (request.SessionIds == null || !request.SessionIds.Any())
		{
			return BadRequest(new { message = "No session IDs provided" });
		}

		var revokedCount = await _sessionService.BulkRevokeSessionsAsync(request.SessionIds);
		return Ok(new { message = $"{revokedCount} session(s) revoked successfully", revokedCount });
	}

	[HttpDelete("user/{userId}")]
	[HasPermission("Sessions.Delete")]
	public async Task<IActionResult> RevokeAllUserSessions(Guid userId)
	{
		var success = await _sessionService.RevokeAllUserSessionsAsync(userId);
		if (!success) return NotFound();
		return Ok(new { message = "All user sessions revoked successfully" });
	}

	[HttpPost("cleanup")]
	[HasPermission("Sessions.Delete")]
	public async Task<IActionResult> RevokeExpiredSessions()
	{
		var success = await _sessionService.RevokeExpiredSessionsAsync();
		return Ok(new { message = "Expired sessions cleaned up", success });
	}

	// Unified Import/Export History (using ImportExportHistory table)
	[HttpGet("history")]
	[HasPermission("Sessions.Read")]
	public async Task<IActionResult> GetHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null // null = show both import and export
		};

		var history = await _sessionService.GetImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}
}
