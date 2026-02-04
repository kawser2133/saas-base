using SaaSBase.Api.Attributes;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SaaSBase.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/organizations")]
[ApiVersion("1.0")]
[Authorize]
public class OrganizationsController : ControllerBase
{
	private readonly IOrganizationService _organizationService;
	private readonly ICurrentTenantService _tenantService;

	public OrganizationsController(IOrganizationService organizationService, ICurrentTenantService tenantService)
	{
		_organizationService = organizationService;
		_tenantService = tenantService;
	}

	[HttpGet]
	[HasPermission("Organizations.Read")]
	public async Task<IActionResult> GetOrganizations()
	{
		var organizations = await _organizationService.GetOrganizationsAsync();
		return Ok(organizations);
	}

	[HttpGet("{id}")]
	[HasPermission("Organizations.Read")]
	public async Task<IActionResult> GetOrganization(Guid id)
	{
		var organization = await _organizationService.GetOrganizationByIdAsync(id);
		if (organization == null) return NotFound();
		return Ok(organization);
	}

	[HttpPost]
	[HasPermission("Organizations.Create")]
	public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationDto dto)
	{
		var organization = await _organizationService.CreateOrganizationAsync(dto);
		return CreatedAtAction(nameof(GetOrganization), new { id = organization.Id }, organization);
	}

	[HttpPut("{id}")]
	[HasPermission("Organizations.Update")]
	public async Task<IActionResult> UpdateOrganization(Guid id, [FromBody] UpdateOrganizationDto dto)
	{
		try
		{
			var organization = await _organizationService.UpdateOrganizationAsync(id, dto);
			return Ok(organization);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
	}

	[HttpDelete("{id}")]
	[HasPermission("Organizations.Delete")]
	public async Task<IActionResult> DeleteOrganization(Guid id)
	{
		var success = await _organizationService.DeleteOrganizationAsync(id);
		if (!success) return NotFound();
		return Ok(new { message = "Organization deleted successfully" });
	}

	[HttpGet("{id}/summary")]
	[HasPermission("Organizations.Read")]
	public async Task<IActionResult> GetOrganizationSummary(Guid id)
	{
		try
		{
			var summary = await _organizationService.GetOrganizationSummaryAsync(id);
			return Ok(summary);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
	}

	[HttpPost("{id}/logo")]
	[HasPermission("Organizations.Update")]
	public async Task<IActionResult> UploadLogo(Guid id, [FromForm] IFormFile logo)
	{
		if (logo == null || logo.Length == 0)
			return BadRequest("No file uploaded");

		var uploadDto = new UploadLogoDto
		{
			FileName = logo.FileName,
			ContentType = logo.ContentType,
			FileSize = logo.Length
		};

		// Read file data
		using var memoryStream = new MemoryStream();
		await logo.CopyToAsync(memoryStream);
		uploadDto.FileData = memoryStream.ToArray();

		var success = await _organizationService.UploadLogoAsync(id, uploadDto);
		if (!success) return BadRequest("Failed to upload logo");
		return Ok(new { message = "Logo uploaded successfully" });
	}

	[HttpDelete("{id}/logo")]
	[HasPermission("Organizations.Update")]
	public async Task<IActionResult> RemoveLogo(Guid id)
	{
		var success = await _organizationService.RemoveLogoAsync(id);
		if (!success) return BadRequest("Failed to remove logo");
		return Ok(new { message = "Logo removed successfully" });
	}

	// Location Management
	[HttpGet("locations")]
	[HasPermission("Organizations.Locations.Read")]
	public async Task<IActionResult> GetLocations(
		[FromQuery] string? search = null,
		[FromQuery] bool? isActive = null,
		[FromQuery] string? country = null,
		[FromQuery] string? city = null,
		[FromQuery] DateTimeOffset? createdFrom = null,
		[FromQuery] DateTimeOffset? createdTo = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? sortField = "createdAtUtc",
		[FromQuery] string? sortDirection = "desc",
		[FromQuery] Guid? organizationId = null)
	{
		// organizationId parameter allows system admin to view another organization's locations
		var result = await _organizationService.GetLocationsPagedAsync(search, isActive, country, city, createdFrom, createdTo, page, pageSize, sortField, sortDirection, organizationId);
		return Ok(result);
	}

	[HttpGet("locations/{id}")]
	[HasPermission("Organizations.Locations.Read")]
	public async Task<IActionResult> GetLocation(Guid id)
	{
		var location = await _organizationService.GetLocationByIdAsync(id);
		if (location == null) return NotFound();
		return Ok(location);
	}

	[HttpPost("locations")]
	[HasPermission("Organizations.Locations.Create")]
	public async Task<IActionResult> CreateLocation([FromBody] CreateLocationDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var location = await _organizationService.CreateLocationAsync(organizationId, dto);
		return CreatedAtAction(nameof(GetLocation), new { id = location.Id }, location);
	}

	[HttpPut("locations/{id}")]
	[HasPermission("Organizations.Locations.Update")]
	public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationDto dto)
	{
		try
		{
			var location = await _organizationService.UpdateLocationAsync(id, dto);
			return Ok(location);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
	}

	[HttpDelete("locations/{id}")]
	[HasPermission("Organizations.Locations.Delete")]
	public async Task<IActionResult> DeleteLocation(Guid id)
	{
		var success = await _organizationService.DeleteLocationAsync(id);
		if (!success) return NotFound();
		return Ok(new { message = "Location deleted successfully" });
	}

	[HttpGet("locations/hierarchy")]
	[HasPermission("Organizations.Locations.Read")]
	public async Task<IActionResult> GetLocationHierarchy()
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var hierarchy = await _organizationService.GetLocationHierarchyAsync(organizationId);
		return Ok(hierarchy);
	}

	// Location Import/Export/History
	[HttpGet("locations/import/template")]
	[HasPermission("Organizations.Locations.Import")]
	public async Task<IActionResult> GetLocationImportTemplate()
	{
		var template = await _organizationService.GetLocationImportTemplateAsync();
		return File(template, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "location_import_template.xlsx");
	}

	[HttpPost("locations/export/async")]
	[HasPermission("Organizations.Locations.Export")]
	public async Task<IActionResult> StartLocationExport([FromBody] LocationExportRequest request)
	{
		var filters = new Dictionary<string, object?>
		{
			["search"] = request.Search,
			["isActive"] = request.IsActive,
			["country"] = request.Country,
			["city"] = request.City,
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _organizationService.StartLocationExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("locations/export/jobs/{jobId}")]
	[HasPermission("Organizations.Locations.Export")]
	public async Task<IActionResult> GetLocationExportJobStatus(string jobId)
	{
		var status = await _organizationService.GetLocationExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("locations/export/jobs/{jobId}/download")]
	[HasPermission("Organizations.Locations.Export")]
	public async Task<IActionResult> DownloadLocationExportFile(string jobId)
	{
		var file = await _organizationService.DownloadLocationExportFileAsync(jobId);
		if (file == null) return NotFound(new { message = "File not found or job not completed" });
		
		var status = await _organizationService.GetLocationExportJobStatusAsync(jobId);
		var format = status?.Format ?? "xlsx";
		var contentType = format switch
		{
			"xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			"csv" => "text/csv",
			"pdf" => "application/pdf",
			"json" => "application/json",
			_ => "application/octet-stream"
		};
		var fileName = $"locations_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
		
		return File(file, contentType, fileName);
	}

	[HttpPost("locations/import/async")]
	[HasPermission("Organizations.Locations.Import")]
	public async Task<IActionResult> StartLocationImport([FromForm] IFormFile file, [FromForm] string duplicateStrategy = "Skip")
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		var strategy = duplicateStrategy switch
		{
			"Update" => DuplicateHandlingStrategy.Update,
			"CreateNew" => DuplicateHandlingStrategy.CreateNew,
			_ => DuplicateHandlingStrategy.Skip
		};

		using var stream = file.OpenReadStream();
		var jobId = await _organizationService.StartLocationImportJobAsync(stream, file.FileName, strategy);
		return Accepted(new { jobId });
	}

	[HttpGet("locations/import/jobs/{jobId}")]
	[HasPermission("Organizations.Locations.Import")]
	public async Task<IActionResult> GetLocationImportJobStatus(string jobId)
	{
		var status = await _organizationService.GetLocationImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("locations/import/error-report/{errorReportId}")]
	[HasPermission("Organizations.Locations.Import")]
	public async Task<IActionResult> GetLocationImportErrorReport(string errorReportId)
	{
		var errorReport = await _organizationService.GetLocationImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
		return File(errorReport, "text/csv", $"location_import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	[HttpGet("locations/history")]
	[HasPermission("Organizations.Locations.Read")]
	public async Task<IActionResult> GetLocationHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null
		};

		var history = await _organizationService.GetLocationImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpGet("locations/filter-options/countries")]
	[HasPermission("Organizations.Locations.Read")]
	public async Task<IActionResult> GetLocationCountries()
	{
		var countries = await _organizationService.GetLocationCountriesAsync();
		return Ok(countries);
	}

	[HttpGet("locations/filter-options/cities")]
	[HasPermission("Organizations.Locations.Read")]
	public async Task<IActionResult> GetLocationCities()
	{
		var cities = await _organizationService.GetLocationCitiesAsync();
		return Ok(cities);
	}

	[HttpPost("locations/bulk-clone")]
	[HasPermission("Organizations.Locations.Create")]
	public async Task<IActionResult> BulkCloneLocations([FromBody] List<Guid> ids)
	{
		var clonedLocations = await _organizationService.BulkCloneLocationsAsync(ids);
		return Ok(new { items = clonedLocations, message = $"{clonedLocations.Count} location(s) cloned successfully" });
	}

	// Business Settings
	[HttpGet("business-settings")]
	[HasPermission("Organizations.BusinessSettings.Read")]
	public async Task<IActionResult> GetBusinessSettings(
		[FromQuery] string? search = null,
		[FromQuery] bool? isActive = null,
		[FromQuery] DateTimeOffset? createdFrom = null,
		[FromQuery] DateTimeOffset? createdTo = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? sortField = "createdAtUtc",
		[FromQuery] string? sortDirection = "desc",
		[FromQuery] Guid? organizationId = null)
	{
		// organizationId parameter allows system admin to view another organization's business settings
		var result = await _organizationService.GetBusinessSettingsPagedAsync(search, isActive, createdFrom, createdTo, page, pageSize, sortField, sortDirection, organizationId);
		return Ok(result);
	}

	[HttpPost("business-settings")]
	[HasPermission("Organizations.BusinessSettings.Create")]
	public async Task<IActionResult> CreateBusinessSetting([FromBody] CreateBusinessSettingDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		try
		{
			var setting = await _organizationService.CreateBusinessSettingAsync(organizationId, dto);
			return CreatedAtAction(nameof(GetBusinessSettings), setting);
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { message = ex.Message });
		}
	}

	[HttpGet("business-settings/{id}")]
	[HasPermission("Organizations.BusinessSettings.Read")]
	public async Task<IActionResult> GetBusinessSetting(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var setting = await _organizationService.GetBusinessSettingByIdAsync(organizationId, id);
		if (setting == null) return NotFound();
		return Ok(setting);
	}

	[HttpPut("business-settings/{id}")]
	[HasPermission("Organizations.BusinessSettings.Update")]
	public async Task<IActionResult> UpdateBusinessSetting(Guid id, [FromBody] CreateBusinessSettingDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		try
		{
			var setting = await _organizationService.UpdateBusinessSettingAsync(organizationId, id, dto);
			return Ok(setting);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
		catch (InvalidOperationException ex)
		{
			return BadRequest(new { message = ex.Message });
		}
	}

	[HttpDelete("business-settings/{id}")]
	[HasPermission("Organizations.BusinessSettings.Delete")]
	public async Task<IActionResult> DeleteBusinessSetting(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var success = await _organizationService.DeleteBusinessSettingAsync(organizationId, id);
		if (!success) return NotFound();
		return Ok(new { message = "Business setting deleted successfully" });
	}

	// Business Settings Import/Export/History
	[HttpGet("business-settings/import/template")]
	[HasPermission("Organizations.BusinessSettings.Import")]
	public async Task<IActionResult> GetBusinessSettingImportTemplate()
	{
		var template = await _organizationService.GetBusinessSettingImportTemplateAsync();
		return File(template, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "business_setting_import_template.xlsx");
	}

	[HttpPost("business-settings/export/async")]
	[HasPermission("Organizations.BusinessSettings.Export")]
	public async Task<IActionResult> StartBusinessSettingExport([FromBody] BusinessSettingExportRequest request)
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

		var jobId = await _organizationService.StartBusinessSettingExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("business-settings/export/jobs/{jobId}")]
	[HasPermission("Organizations.BusinessSettings.Export")]
	public async Task<IActionResult> GetBusinessSettingExportJobStatus(string jobId)
	{
		var status = await _organizationService.GetBusinessSettingExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("business-settings/export/jobs/{jobId}/download")]
	[HasPermission("Organizations.BusinessSettings.Export")]
	public async Task<IActionResult> DownloadBusinessSettingExportFile(string jobId)
	{
		var file = await _organizationService.DownloadBusinessSettingExportFileAsync(jobId);
		if (file == null) return NotFound(new { message = "File not found or job not completed" });
		
		var status = await _organizationService.GetBusinessSettingExportJobStatusAsync(jobId);
		var format = status?.Format ?? "xlsx";
		var contentType = format switch
		{
			"xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			"csv" => "text/csv",
			"pdf" => "application/pdf",
			"json" => "application/json",
			_ => "application/octet-stream"
		};
		var fileName = $"business_settings_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
		
		return File(file, contentType, fileName);
	}

	[HttpPost("business-settings/import/async")]
	[HasPermission("Organizations.BusinessSettings.Import")]
	public async Task<IActionResult> StartBusinessSettingImport([FromForm] IFormFile file, [FromForm] string duplicateStrategy = "Skip")
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		var strategy = duplicateStrategy switch
		{
			"Update" => DuplicateHandlingStrategy.Update,
			"CreateNew" => DuplicateHandlingStrategy.CreateNew,
			_ => DuplicateHandlingStrategy.Skip
		};

		using var stream = file.OpenReadStream();
		var jobId = await _organizationService.StartBusinessSettingImportJobAsync(stream, file.FileName, strategy);
		return Accepted(new { jobId });
	}

	[HttpGet("business-settings/import/jobs/{jobId}")]
	[HasPermission("Organizations.BusinessSettings.Import")]
	public async Task<IActionResult> GetBusinessSettingImportJobStatus(string jobId)
	{
		var status = await _organizationService.GetBusinessSettingImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("business-settings/import/error-report/{errorReportId}")]
	[HasPermission("Organizations.BusinessSettings.Import")]
	public async Task<IActionResult> GetBusinessSettingImportErrorReport(string errorReportId)
	{
		var errorReport = await _organizationService.GetBusinessSettingImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
		return File(errorReport, "text/csv", $"business_setting_import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	[HttpGet("business-settings/history")]
	[HasPermission("Organizations.BusinessSettings.Read")]
	public async Task<IActionResult> GetBusinessSettingHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null
		};

		var history = await _organizationService.GetBusinessSettingImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpPost("business-settings/bulk-clone")]
	[HasPermission("Organizations.BusinessSettings.Create")]
	public async Task<IActionResult> BulkCloneBusinessSettings([FromBody] List<Guid> ids)
	{
		var clonedSettings = await _organizationService.BulkCloneBusinessSettingsAsync(ids);
		return Ok(new { items = clonedSettings, message = $"{clonedSettings.Count} setting(s) cloned successfully" });
	}

	// Currency Management
	[HttpGet("currencies")]
	[HasPermission("Organizations.Currencies.Read")]
	public async Task<IActionResult> GetCurrencies(
		[FromQuery] string? search = null,
		[FromQuery] bool? isActive = null,
		[FromQuery] string? code = null,
		[FromQuery] DateTimeOffset? createdFrom = null,
		[FromQuery] DateTimeOffset? createdTo = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10,
		[FromQuery] string? sortField = "createdAtUtc",
		[FromQuery] string? sortDirection = "desc",
		[FromQuery] Guid? organizationId = null)
	{
		// organizationId parameter allows system admin to view another organization's currencies
		var result = await _organizationService.GetCurrenciesPagedAsync(search, isActive, code, createdFrom, createdTo, page, pageSize, sortField, sortDirection, organizationId);
		return Ok(result);
	}

	[HttpGet("currencies/{id}")]
	[HasPermission("Organizations.Currencies.Read")]
	public async Task<IActionResult> GetCurrency(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var currency = await _organizationService.GetCurrencyByIdAsync(organizationId, id);
		if (currency == null) return NotFound();
		return Ok(currency);
	}

	[HttpPost("currencies")]
	[HasPermission("Organizations.Currencies.Create")]
	public async Task<IActionResult> CreateCurrency([FromBody] CreateCurrencyDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var currency = await _organizationService.CreateCurrencyAsync(organizationId, dto);
		return CreatedAtAction(nameof(GetCurrencies), currency);
	}

	[HttpPut("currencies/{id}")]
	[HasPermission("Organizations.Currencies.Update")]
	public async Task<IActionResult> UpdateCurrency(Guid id, [FromBody] CreateCurrencyDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		try
		{
			var currency = await _organizationService.UpdateCurrencyAsync(organizationId, id, dto);
			return Ok(currency);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
	}

	[HttpDelete("currencies/{id}")]
	[HasPermission("Organizations.Currencies.Delete")]
	public async Task<IActionResult> DeleteCurrency(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var success = await _organizationService.DeleteCurrencyAsync(organizationId, id);
		if (!success) return NotFound();
		return Ok(new { message = "Currency deleted successfully" });
	}

	// Currency Import/Export/History
	[HttpGet("currencies/import/template")]
	[HasPermission("Organizations.Currencies.Import")]
	public async Task<IActionResult> GetCurrencyImportTemplate()
	{
		var template = await _organizationService.GetCurrencyImportTemplateAsync();
		return File(template, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "currency_import_template.xlsx");
	}

	[HttpPost("currencies/export/async")]
	[HasPermission("Organizations.Currencies.Export")]
	public async Task<IActionResult> StartCurrencyExport([FromBody] CurrencyExportRequest request)
	{
		var filters = new Dictionary<string, object?>
		{
			["search"] = request.Search,
			["isActive"] = request.IsActive,
			["code"] = request.Code,
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _organizationService.StartCurrencyExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("currencies/export/jobs/{jobId}")]
	[HasPermission("Organizations.Currencies.Export")]
	public async Task<IActionResult> GetCurrencyExportJobStatus(string jobId)
	{
		var status = await _organizationService.GetCurrencyExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("currencies/export/jobs/{jobId}/download")]
	[HasPermission("Organizations.Currencies.Export")]
	public async Task<IActionResult> DownloadCurrencyExportFile(string jobId)
	{
		var file = await _organizationService.DownloadCurrencyExportFileAsync(jobId);
		if (file == null) return NotFound(new { message = "File not found or job not completed" });
		
		var status = await _organizationService.GetCurrencyExportJobStatusAsync(jobId);
		var format = status?.Format ?? "xlsx";
		var contentType = format switch
		{
			"xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			"csv" => "text/csv",
			"pdf" => "application/pdf",
			"json" => "application/json",
			_ => "application/octet-stream"
		};
		var fileName = $"currencies_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
		
		return File(file, contentType, fileName);
	}

	[HttpPost("currencies/import/async")]
	[HasPermission("Organizations.Currencies.Import")]
	public async Task<IActionResult> StartCurrencyImport([FromForm] IFormFile file, [FromForm] string duplicateStrategy = "Skip")
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		var strategy = duplicateStrategy switch
		{
			"Update" => DuplicateHandlingStrategy.Update,
			"CreateNew" => DuplicateHandlingStrategy.CreateNew,
			_ => DuplicateHandlingStrategy.Skip
		};

		using var stream = file.OpenReadStream();
		var jobId = await _organizationService.StartCurrencyImportJobAsync(stream, file.FileName, strategy);
		return Accepted(new { jobId });
	}

	[HttpGet("currencies/import/jobs/{jobId}")]
	[HasPermission("Organizations.Currencies.Import")]
	public async Task<IActionResult> GetCurrencyImportJobStatus(string jobId)
	{
		var status = await _organizationService.GetCurrencyImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("currencies/import/error-report/{errorReportId}")]
	[HasPermission("Organizations.Currencies.Import")]
	public async Task<IActionResult> GetCurrencyImportErrorReport(string errorReportId)
	{
		var errorReport = await _organizationService.GetCurrencyImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
		return File(errorReport, "text/csv", $"currency_import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	[HttpGet("currencies/history")]
	[HasPermission("Organizations.Currencies.Read")]
	public async Task<IActionResult> GetCurrencyHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null
		};

		var history = await _organizationService.GetCurrencyImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpGet("currencies/filter-options/codes")]
	[HasPermission("Organizations.Currencies.Read")]
	public async Task<IActionResult> GetCurrencyCodes()
	{
		var codes = await _organizationService.GetCurrencyCodesAsync();
		return Ok(codes);
	}

	[HttpPost("currencies/bulk-clone")]
	[HasPermission("Organizations.Currencies.Create")]
	public async Task<IActionResult> BulkCloneCurrencies([FromBody] List<Guid> ids)
	{
		var clonedCurrencies = await _organizationService.BulkCloneCurrenciesAsync(ids);
		return Ok(new { items = clonedCurrencies, message = $"{clonedCurrencies.Count} currency(ies) cloned successfully" });
	}

	// Tax Rate Management
	[HttpGet("tax-rates")]
	[HasPermission("Organizations.TaxRates.Read")]
	public async Task<IActionResult> GetTaxRates([FromQuery] string? search = null, [FromQuery] bool? isActive = null, [FromQuery] string? taxType = null, [FromQuery] DateTimeOffset? createdFrom = null, [FromQuery] DateTimeOffset? createdTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? sortField = "createdAtUtc", [FromQuery] string? sortDirection = "desc", [FromQuery] Guid? organizationId = null)
	{
		// organizationId parameter allows system admin to view another organization's tax rates
		var taxRates = await _organizationService.GetTaxRatesPagedAsync(search, isActive, taxType, createdFrom, createdTo, page, pageSize, sortField, sortDirection, organizationId);
		return Ok(taxRates);
	}

	[HttpGet("tax-rates/{id}")]
	[HasPermission("Organizations.TaxRates.Read")]
	public async Task<IActionResult> GetTaxRate(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var taxRate = await _organizationService.GetTaxRateByIdAsync(organizationId, id);
		if (taxRate == null) return NotFound();
		return Ok(taxRate);
	}

	[HttpPost("tax-rates")]
	[HasPermission("Organizations.TaxRates.Create")]
	public async Task<IActionResult> CreateTaxRate([FromBody] CreateTaxRateDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var taxRate = await _organizationService.CreateTaxRateAsync(organizationId, dto);
		return CreatedAtAction(nameof(GetTaxRates), taxRate);
	}

	[HttpPut("tax-rates/{id}")]
	[HasPermission("Organizations.TaxRates.Update")]
	public async Task<IActionResult> UpdateTaxRate(Guid id, [FromBody] CreateTaxRateDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		try
		{
			var taxRate = await _organizationService.UpdateTaxRateAsync(organizationId, id, dto);
			return Ok(taxRate);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
	}

	[HttpDelete("tax-rates/{id}")]
	[HasPermission("Organizations.TaxRates.Delete")]
	public async Task<IActionResult> DeleteTaxRate(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var success = await _organizationService.DeleteTaxRateAsync(organizationId, id);
		if (!success) return NotFound();
		return Ok(new { message = "Tax rate deleted successfully" });
	}

	// Tax Rate Import/Export/History
	[HttpGet("tax-rates/import/template")]
	[HasPermission("Organizations.TaxRates.Import")]
	public async Task<IActionResult> GetTaxRateImportTemplate()
	{
		var template = await _organizationService.GetTaxRateImportTemplateAsync();
		return File(template, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "tax_rate_import_template.xlsx");
	}

	[HttpPost("tax-rates/export/async")]
	[HasPermission("Organizations.TaxRates.Export")]
	public async Task<IActionResult> StartTaxRateExport([FromBody] TaxRateExportRequest request)
	{
		var filters = new Dictionary<string, object?>
		{
			["search"] = request.Search,
			["isActive"] = request.IsActive,
			["taxType"] = request.TaxType,
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _organizationService.StartTaxRateExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("tax-rates/export/jobs/{jobId}")]
	[HasPermission("Organizations.TaxRates.Export")]
	public async Task<IActionResult> GetTaxRateExportJobStatus(string jobId)
	{
		var status = await _organizationService.GetTaxRateExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("tax-rates/export/jobs/{jobId}/download")]
	[HasPermission("Organizations.TaxRates.Export")]
	public async Task<IActionResult> DownloadTaxRateExportFile(string jobId)
	{
		var file = await _organizationService.DownloadTaxRateExportFileAsync(jobId);
		if (file == null) return NotFound(new { message = "File not found or job not completed" });
		
		var status = await _organizationService.GetTaxRateExportJobStatusAsync(jobId);
		var format = status?.Format ?? "xlsx";
		var contentType = format switch
		{
			"xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			"csv" => "text/csv",
			"pdf" => "application/pdf",
			"json" => "application/json",
			_ => "application/octet-stream"
		};
		var fileName = $"tax_rates_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
		
		return File(file, contentType, fileName);
	}

	[HttpPost("tax-rates/import/async")]
	[HasPermission("Organizations.TaxRates.Import")]
	public async Task<IActionResult> StartTaxRateImport([FromForm] IFormFile file, [FromForm] string duplicateStrategy = "Skip")
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		var strategy = duplicateStrategy switch
		{
			"Update" => DuplicateHandlingStrategy.Update,
			"CreateNew" => DuplicateHandlingStrategy.CreateNew,
			_ => DuplicateHandlingStrategy.Skip
		};

		using var stream = file.OpenReadStream();
		var jobId = await _organizationService.StartTaxRateImportJobAsync(stream, file.FileName, strategy);
		return Accepted(new { jobId });
	}

	[HttpGet("tax-rates/import/jobs/{jobId}")]
	[HasPermission("Organizations.TaxRates.Import")]
	public async Task<IActionResult> GetTaxRateImportJobStatus(string jobId)
	{
		var status = await _organizationService.GetTaxRateImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("tax-rates/import/error-report/{errorReportId}")]
	[HasPermission("Organizations.TaxRates.Import")]
	public async Task<IActionResult> GetTaxRateImportErrorReport(string errorReportId)
	{
		var errorReport = await _organizationService.GetTaxRateImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
		return File(errorReport, "text/csv", $"tax_rate_import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	[HttpGet("tax-rates/history")]
	[HasPermission("Organizations.TaxRates.Read")]
	public async Task<IActionResult> GetTaxRateHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null
		};

		var history = await _organizationService.GetTaxRateImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpGet("tax-rates/filter-options/tax-types")]
	[HasPermission("Organizations.TaxRates.Read")]
	public async Task<IActionResult> GetTaxTypes()
	{
		var taxTypes = await _organizationService.GetTaxTypesAsync();
		return Ok(taxTypes);
	}

	[HttpPost("tax-rates/bulk-clone")]
	[HasPermission("Organizations.TaxRates.Create")]
	public async Task<IActionResult> BulkCloneTaxRates([FromBody] List<Guid> ids)
	{
		var clonedTaxRates = await _organizationService.BulkCloneTaxRatesAsync(ids);
		return Ok(new { items = clonedTaxRates, message = $"{clonedTaxRates.Count} tax rate(s) cloned successfully" });
	}

	// Notification Templates
	[HttpGet("notification-templates")]
	[HasPermission("Organizations.NotificationTemplates.Read")]
	public async Task<IActionResult> GetNotificationTemplates([FromQuery] string? search = null, [FromQuery] bool? isActive = null, [FromQuery] string? category = null, [FromQuery] string? templateType = null, [FromQuery] DateTimeOffset? createdFrom = null, [FromQuery] DateTimeOffset? createdTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? sortField = "createdAtUtc", [FromQuery] string? sortDirection = "desc", [FromQuery] Guid? organizationId = null)
	{
		// organizationId parameter allows system admin to view another organization's notification templates
		var templates = await _organizationService.GetNotificationTemplatesPagedAsync(search, isActive, category, templateType, createdFrom, createdTo, page, pageSize, sortField, sortDirection, organizationId);
		return Ok(templates);
	}

	[HttpGet("notification-templates/{id}")]
	[HasPermission("Organizations.NotificationTemplates.Read")]
	public async Task<IActionResult> GetNotificationTemplate(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var template = await _organizationService.GetNotificationTemplateByIdAsync(organizationId, id);
		if (template == null) return NotFound();
		return Ok(template);
	}

	[HttpPost("notification-templates")]
	[HasPermission("Organizations.NotificationTemplates.Create")]
	public async Task<IActionResult> CreateNotificationTemplate([FromBody] CreateNotificationTemplateDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var template = await _organizationService.CreateNotificationTemplateAsync(organizationId, dto);
		return CreatedAtAction(nameof(GetNotificationTemplates), template);
	}

	[HttpPut("notification-templates/{id}")]
	[HasPermission("Organizations.NotificationTemplates.Update")]
	public async Task<IActionResult> UpdateNotificationTemplate(Guid id, [FromBody] CreateNotificationTemplateDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		try
		{
			var template = await _organizationService.UpdateNotificationTemplateAsync(organizationId, id, dto);
			return Ok(template);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
	}

	[HttpDelete("notification-templates/{id}")]
	[HasPermission("Organizations.NotificationTemplates.Delete")]
	public async Task<IActionResult> DeleteNotificationTemplate(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var success = await _organizationService.DeleteNotificationTemplateAsync(organizationId, id);
		if (!success) return NotFound();
		return Ok(new { message = "Notification template deleted successfully" });
	}

	// Notification Template Import/Export/History
	[HttpGet("notification-templates/import/template")]
	[HasPermission("Organizations.NotificationTemplates.Import")]
	public async Task<IActionResult> GetNotificationTemplateImportTemplate()
	{
		var template = await _organizationService.GetNotificationTemplateImportTemplateAsync();
		return File(template, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "notification_template_import_template.xlsx");
	}

	[HttpPost("notification-templates/export/async")]
	[HasPermission("Organizations.NotificationTemplates.Export")]
	public async Task<IActionResult> StartNotificationTemplateExport([FromBody] NotificationTemplateExportRequest request)
	{
		var filters = new Dictionary<string, object?>
		{
			["search"] = request.Search,
			["isActive"] = request.IsActive,
			["category"] = request.Category,
			["templateType"] = request.TemplateType,
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _organizationService.StartNotificationTemplateExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("notification-templates/export/jobs/{jobId}")]
	[HasPermission("Organizations.NotificationTemplates.Export")]
	public async Task<IActionResult> GetNotificationTemplateExportJobStatus(string jobId)
	{
		var status = await _organizationService.GetNotificationTemplateExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("notification-templates/export/jobs/{jobId}/download")]
	[HasPermission("Organizations.NotificationTemplates.Export")]
	public async Task<IActionResult> DownloadNotificationTemplateExportFile(string jobId)
	{
		var file = await _organizationService.DownloadNotificationTemplateExportFileAsync(jobId);
		if (file == null) return NotFound(new { message = "File not found or job not completed" });
		
		var status = await _organizationService.GetNotificationTemplateExportJobStatusAsync(jobId);
		var format = status?.Format ?? "xlsx";
		var contentType = format switch
		{
			"xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			"csv" => "text/csv",
			"pdf" => "application/pdf",
			"json" => "application/json",
			_ => "application/octet-stream"
		};
		var fileName = $"notification_templates_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
		
		return File(file, contentType, fileName);
	}

	[HttpPost("notification-templates/import/async")]
	[HasPermission("Organizations.NotificationTemplates.Import")]
	public async Task<IActionResult> StartNotificationTemplateImport([FromForm] IFormFile file, [FromForm] string duplicateStrategy = "Skip")
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		var strategy = duplicateStrategy switch
		{
			"Update" => DuplicateHandlingStrategy.Update,
			"CreateNew" => DuplicateHandlingStrategy.CreateNew,
			_ => DuplicateHandlingStrategy.Skip
		};

		using var stream = file.OpenReadStream();
		var jobId = await _organizationService.StartNotificationTemplateImportJobAsync(stream, file.FileName, strategy);
		return Accepted(new { jobId });
	}

	[HttpGet("notification-templates/import/jobs/{jobId}")]
	[HasPermission("Organizations.NotificationTemplates.Import")]
	public async Task<IActionResult> GetNotificationTemplateImportJobStatus(string jobId)
	{
		var status = await _organizationService.GetNotificationTemplateImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("notification-templates/import/error-report/{errorReportId}")]
	[HasPermission("Organizations.NotificationTemplates.Import")]
	public async Task<IActionResult> GetNotificationTemplateImportErrorReport(string errorReportId)
	{
		var errorReport = await _organizationService.GetNotificationTemplateImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
		return File(errorReport, "text/csv", $"notification_template_import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	[HttpGet("notification-templates/history")]
	[HasPermission("Organizations.NotificationTemplates.Read")]
	public async Task<IActionResult> GetNotificationTemplateHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null
		};

		var history = await _organizationService.GetNotificationTemplateImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpGet("notification-templates/filter-options/categories")]
	[HasPermission("Organizations.NotificationTemplates.Read")]
	public async Task<IActionResult> GetNotificationTemplateCategories()
	{
		var categories = await _organizationService.GetNotificationTemplateCategoriesAsync();
		return Ok(categories);
	}

	[HttpPost("notification-templates/bulk-clone")]
	[HasPermission("Organizations.NotificationTemplates.Create")]
	public async Task<IActionResult> BulkCloneNotificationTemplates([FromBody] List<Guid> ids)
	{
		var clonedTemplates = await _organizationService.BulkCloneNotificationTemplatesAsync(ids);
		return Ok(new { items = clonedTemplates, message = $"{clonedTemplates.Count} template(s) cloned successfully" });
	}

	// Integration Settings
	[HttpGet("integration-settings")]
	[HasPermission("Organizations.IntegrationSettings.Read")]
	public async Task<IActionResult> GetIntegrationSettings([FromQuery] string? search = null, [FromQuery] bool? isActive = null, [FromQuery] string? provider = null, [FromQuery] string? integrationType = null, [FromQuery] DateTimeOffset? createdFrom = null, [FromQuery] DateTimeOffset? createdTo = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? sortField = "createdAtUtc", [FromQuery] string? sortDirection = "desc", [FromQuery] Guid? organizationId = null)
	{
		// organizationId parameter allows system admin to view another organization's integration settings
		var integrations = await _organizationService.GetIntegrationSettingsPagedAsync(search, isActive, provider, integrationType, createdFrom, createdTo, page, pageSize, sortField, sortDirection, organizationId);
		return Ok(integrations);
	}

	[HttpGet("integration-settings/{id}")]
	[HasPermission("Organizations.IntegrationSettings.Read")]
	public async Task<IActionResult> GetIntegrationSetting(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var integration = await _organizationService.GetIntegrationSettingByIdAsync(organizationId, id);
		if (integration == null) return NotFound();
		return Ok(integration);
	}

	[HttpPost("integration-settings")]
	[HasPermission("Organizations.IntegrationSettings.Create")]
	public async Task<IActionResult> CreateIntegrationSetting([FromBody] CreateIntegrationSettingDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var integration = await _organizationService.CreateIntegrationSettingAsync(organizationId, dto);
		return CreatedAtAction(nameof(GetIntegrationSettings), integration);
	}

	[HttpPut("integration-settings/{id}")]
	[HasPermission("Organizations.IntegrationSettings.Update")]
	public async Task<IActionResult> UpdateIntegrationSetting(Guid id, [FromBody] CreateIntegrationSettingDto dto)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		try
		{
			var integration = await _organizationService.UpdateIntegrationSettingAsync(organizationId, id, dto);
			return Ok(integration);
		}
		catch (ArgumentException)
		{
			return NotFound();
		}
	}

	[HttpDelete("integration-settings/{id}")]
	[HasPermission("Organizations.IntegrationSettings.Delete")]
	public async Task<IActionResult> DeleteIntegrationSetting(Guid id)
	{
		var organizationId = _tenantService.GetOrganizationId();
		if (organizationId == Guid.Empty) return BadRequest(new { message = "Organization ID not found" });
		var success = await _organizationService.DeleteIntegrationSettingAsync(organizationId, id);
		if (!success) return NotFound();
		return Ok(new { message = "Integration setting deleted successfully" });
	}

	// Integration Setting Import/Export/History
	[HttpGet("integration-settings/import/template")]
	[HasPermission("Organizations.IntegrationSettings.Import")]
	public async Task<IActionResult> GetIntegrationSettingImportTemplate()
	{
		var template = await _organizationService.GetIntegrationSettingImportTemplateAsync();
		return File(template, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "integration_setting_import_template.xlsx");
	}

	[HttpPost("integration-settings/export/async")]
	[HasPermission("Organizations.IntegrationSettings.Export")]
	public async Task<IActionResult> StartIntegrationSettingExport([FromBody] IntegrationSettingExportRequest request)
	{
		var filters = new Dictionary<string, object?>
		{
			["search"] = request.Search,
			["isActive"] = request.IsActive,
			["provider"] = request.Provider,
			["createdFrom"] = request.CreatedFrom,
			["createdTo"] = request.CreatedTo
		};

		if (request.SelectedIds != null && request.SelectedIds.Any())
		{
			filters["selectedIds"] = request.SelectedIds;
		}

		var jobId = await _organizationService.StartIntegrationSettingExportJobAsync(request.Format, filters);
		return Accepted(new { jobId });
	}

	[HttpGet("integration-settings/export/jobs/{jobId}")]
	[HasPermission("Organizations.IntegrationSettings.Export")]
	public async Task<IActionResult> GetIntegrationSettingExportJobStatus(string jobId)
	{
		var status = await _organizationService.GetIntegrationSettingExportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("integration-settings/export/jobs/{jobId}/download")]
	[HasPermission("Organizations.IntegrationSettings.Export")]
	public async Task<IActionResult> DownloadIntegrationSettingExportFile(string jobId)
	{
		var file = await _organizationService.DownloadIntegrationSettingExportFileAsync(jobId);
		if (file == null) return NotFound(new { message = "File not found or job not completed" });
		
		var status = await _organizationService.GetIntegrationSettingExportJobStatusAsync(jobId);
		var format = status?.Format ?? "xlsx";
		var contentType = format switch
		{
			"xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			"csv" => "text/csv",
			"pdf" => "application/pdf",
			"json" => "application/json",
			_ => "application/octet-stream"
		};
		var fileName = $"integration_settings_export_{DateTime.UtcNow:yyyyMMddHHmmss}.{format}";
		
		return File(file, contentType, fileName);
	}

	[HttpPost("integration-settings/import/async")]
	[HasPermission("Organizations.IntegrationSettings.Import")]
	public async Task<IActionResult> StartIntegrationSettingImport([FromForm] IFormFile file, [FromForm] string duplicateStrategy = "Skip")
	{
		if (file == null || file.Length == 0)
			return BadRequest(new { message = "No file uploaded" });

		var strategy = duplicateStrategy switch
		{
			"Update" => DuplicateHandlingStrategy.Update,
			"CreateNew" => DuplicateHandlingStrategy.CreateNew,
			_ => DuplicateHandlingStrategy.Skip
		};

		using var stream = file.OpenReadStream();
		var jobId = await _organizationService.StartIntegrationSettingImportJobAsync(stream, file.FileName, strategy);
		return Accepted(new { jobId });
	}

	[HttpGet("integration-settings/import/jobs/{jobId}")]
	[HasPermission("Organizations.IntegrationSettings.Import")]
	public async Task<IActionResult> GetIntegrationSettingImportJobStatus(string jobId)
	{
		var status = await _organizationService.GetIntegrationSettingImportJobStatusAsync(jobId);
		if (status == null) return NotFound(new { message = "Job not found" });
		return Ok(status);
	}

	[HttpGet("integration-settings/import/error-report/{errorReportId}")]
	[HasPermission("Organizations.IntegrationSettings.Import")]
	public async Task<IActionResult> GetIntegrationSettingImportErrorReport(string errorReportId)
	{
		var errorReport = await _organizationService.GetIntegrationSettingImportErrorReportAsync(errorReportId);
		if (errorReport == null)
			return NotFound(new { message = "Error report not found or expired" });
		return File(errorReport, "text/csv", $"integration_setting_import_errors_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
	}

	[HttpGet("integration-settings/history")]
	[HasPermission("Organizations.IntegrationSettings.Read")]
	public async Task<IActionResult> GetIntegrationSettingHistory([FromQuery] string? type = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
	{
		ImportExportType? operationType = type?.ToLower() switch
		{
			"import" => ImportExportType.Import,
			"export" => ImportExportType.Export,
			_ => null
		};

		var history = await _organizationService.GetIntegrationSettingImportExportHistoryAsync(operationType, page, pageSize);
		return Ok(history);
	}

	[HttpGet("integration-settings/filter-options/providers")]
	[HasPermission("Organizations.IntegrationSettings.Read")]
	public async Task<IActionResult> GetIntegrationProviders()
	{
		var providers = await _organizationService.GetIntegrationProvidersAsync();
		return Ok(providers);
	}

	[HttpPost("integration-settings/bulk-clone")]
	[HasPermission("Organizations.IntegrationSettings.Create")]
	public async Task<IActionResult> BulkCloneIntegrationSettings([FromBody] List<Guid> ids)
	{
		var clonedIntegrations = await _organizationService.BulkCloneIntegrationSettingsAsync(ids);
		return Ok(new { items = clonedIntegrations, message = $"{clonedIntegrations.Count} integration(s) cloned successfully" });
	}
}

public class LocationExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
	public string? Country { get; set; }
	public string? City { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}

public class BusinessSettingExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}

public class CurrencyExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
	public string? Code { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}

public class TaxRateExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
	public string? TaxType { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}

public class NotificationTemplateExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
	public string? Category { get; set; }
	public string? TemplateType { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}

public class IntegrationSettingExportRequest
{
	public ExportFormat Format { get; set; } = ExportFormat.Excel;
	public string? Search { get; set; }
	public bool? IsActive { get; set; }
	public string? Provider { get; set; }
	public DateTimeOffset? CreatedFrom { get; set; }
	public DateTimeOffset? CreatedTo { get; set; }
	public List<Guid>? SelectedIds { get; set; }
}