using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSBase.Api.Attributes;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/mfa")]
[ApiVersion("1.0")]
[Authorize]
public class MfaController : ControllerBase
{
	private readonly IMfaService _mfaService;

	public MfaController(IMfaService mfaService)
	{
		_mfaService = mfaService;
	}

	[HttpPost("setup")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> SetupMfa([FromBody] SetupMfaRequest request)
	{
		var setup = await _mfaService.SetupMfaAsync(request.UserId, request.MfaType);
		return Ok(setup);
	}

	[HttpPost("verify")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> VerifyMfaCode([FromBody] MfaVerificationDto dto)
	{
		var isValid = await _mfaService.VerifyMfaCodeAsync(dto.UserId, dto.Code, dto.MfaType);
		return Ok(new { isValid });
	}

	[HttpGet("settings/{userId}")]
	[HasPermission("Mfa.Read")]
	public async Task<IActionResult> GetUserMfaSettings(Guid userId)
	{
		var settings = await _mfaService.GetUserMfaSettingsSummaryAsync(userId);
		return Ok(settings);
	}

	[HttpPost("enable")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> EnableMfa([FromBody] EnableMfaRequest request)
	{
		var success = await _mfaService.EnableMfaAsync(request.UserId, request.MfaType, request.Code);
		return Ok(new { success });
	}

	[HttpPost("disable")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> DisableMfa([FromBody] DisableMfaRequest request)
	{
		var success = await _mfaService.DisableMfaAsync(request.UserId, request.MfaType);
		return Ok(new { success });
	}

	[HttpPost("set-default")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> SetDefaultMfa([FromBody] SetDefaultMfaRequest request)
	{
		var success = await _mfaService.SetDefaultMfaAsync(request.UserId, request.MfaType);
		return Ok(new { success });
	}

	[HttpPost("backup-codes/generate")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> GenerateBackupCodes([FromBody] GenerateBackupCodesRequest request)
	{
		var codes = await _mfaService.GenerateBackupCodesAsync(request.UserId);
		return Ok(new { codes });
	}

	[HttpPost("backup-codes/verify")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> VerifyBackupCode([FromBody] VerifyBackupCodeRequest request)
	{
		var isValid = await _mfaService.VerifyBackupCodeAsync(request.UserId, request.Code);
		return Ok(new { isValid });
	}

	[HttpGet("enabled/{userId}")]
	[HasPermission("Mfa.Read")]
	public async Task<IActionResult> IsMfaEnabled(Guid userId)
	{
		var isEnabled = await _mfaService.IsMfaEnabledAsync(userId);
		return Ok(new { isEnabled });
	}

	[HttpPost("send-code")]
	[HasPermission("Mfa.Update")]
	public async Task<IActionResult> SendMfaCode([FromBody] SendMfaCodeRequest request)
	{
		try
		{
			var success = await _mfaService.SendMfaCodeAsync(request.UserId, request.MfaType);
			if (!success)
			{
				return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
				{
					Status = StatusCodes.Status400BadRequest,
					Title = "Failed to Send Code",
					Detail = "Unable to send verification code. Please check your configuration or try again later."
				});
			}
			return Ok(new { success = true });
		}
		catch (ArgumentException ex)
		{
			return BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "Bad Request",
				Detail = ex.Message
			});
		}
		catch (Exception ex)
		{
			return StatusCode(StatusCodes.Status500InternalServerError, new Microsoft.AspNetCore.Mvc.ProblemDetails
			{
				Status = StatusCodes.Status500InternalServerError,
				Title = "Internal Server Error",
				Detail = "An error occurred while sending the verification code. Please try again later."
			});
		}
	}

	[HttpGet("organization")]
	[HasPermission("Mfa.Read")]
	public async Task<IActionResult> GetOrganizationMfaSettings([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] Guid? organizationId = null, [FromQuery] string? search = null, [FromQuery] string? sortField = null, [FromQuery] string? sortDirection = null)
	{
		page = page <= 0 ? 1 : page;
		pageSize = pageSize <= 0 ? 10 : pageSize;

		var settings = await _mfaService.GetOrganizationMfaSettingsAsync(page, pageSize, organizationId, search, sortField, sortDirection);
		return Ok(settings);
	}

	// Async Export (non-blocking)
	public class MfaExportRequest
	{
		public string? Search { get; set; }
		public string? SortField { get; set; }
		public string? SortDirection { get; set; } = "desc";
		public ExportFormat Format { get; set; } = ExportFormat.CSV;
		public List<string>? SelectedIds { get; set; }
	}

	[HttpPost("export/async")]
	[HasPermission("Mfa.Read")]
	public async Task<IActionResult> StartExportAsync([FromBody] MfaExportRequest request)
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

		var jobId = await _mfaService.StartExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("export/jobs/{jobId}")]
	public async Task<IActionResult> GetExportJobStatus(string jobId)
	{
		var status = await _mfaService.GetExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Export job not found" });
		return Ok(status);
	}

	[HttpGet("export/jobs/{jobId}/download")]
	public async Task<IActionResult> DownloadExport(string jobId)
	{
		var fileData = await _mfaService.DownloadExportFileAsync(jobId);
		if (fileData == null) return NotFound(new { message = "Export file not found or expired" });

		var status = await _mfaService.GetExportJobStatusAsync(jobId);
		var formatLower = status?.Format?.ToLower() ?? "csv";

		var (contentType, extension) = formatLower switch
		{
			"excel" => ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
			"csv" => ("text/csv", "csv"),
			"pdf" => ("application/pdf", "pdf"),
			"json" => ("application/json", "json"),
			_ => ("text/csv", "csv")
		};

		var fileName = $"mfa_settings_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}";
		return File(fileData, contentType, fileName);
	}

	// Unified Import/Export History
	[HttpGet("history")]
	[HasPermission("Mfa.Read")]
	public async Task<IActionResult> GetHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null // null = show both import and export
		};

		var history = await _mfaService.GetImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}
}

public class SetupMfaRequest
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
}

public class EnableMfaRequest
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
}

public class DisableMfaRequest
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
}

public class SetDefaultMfaRequest
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
}

public class SendMfaCodeRequest
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
}

public class GenerateBackupCodesRequest
{
	public Guid UserId { get; set; }
}

public class VerifyBackupCodeRequest
{
	public Guid UserId { get; set; }
	public string Code { get; set; } = string.Empty;
}
