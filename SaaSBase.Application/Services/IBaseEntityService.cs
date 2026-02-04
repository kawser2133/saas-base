using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

/// <summary>
/// Generic base service interface for all entities with common CRUD and import/export operations
/// </summary>
/// <typeparam name="TDto">Entity DTO</typeparam>
/// <typeparam name="TCreateDto">Create DTO</typeparam>
/// <typeparam name="TUpdateDto">Update DTO</typeparam>
/// <typeparam name="TStatisticsDto">Statistics DTO</typeparam>
/// <typeparam name="TDropdownOptionsDto">Dropdown options DTO</typeparam>
public interface IBaseEntityService<TDto, TCreateDto, TUpdateDto, TStatisticsDto, TDropdownOptionsDto>
	where TDto : class
	where TCreateDto : class
	where TUpdateDto : class
	where TStatisticsDto : class
	where TDropdownOptionsDto : class
{
	// ========================================
	// CRUD Operations (Common for all entities)
	// ========================================

	/// <summary>
	/// Get paginated list with filters and sorting
	/// </summary>
	Task<PagedResultDto<TDto>> GetAllAsync(
		Dictionary<string, object?> filters,
		int page,
		int pageSize,
		string? sortField = null,
		string? sortDirection = "desc");

	/// <summary>
	/// Get single entity by ID
	/// </summary>
	Task<TDto?> GetByIdAsync(Guid id);

	/// <summary>
	/// Create new entity
	/// </summary>
	Task<TDto> CreateAsync(TCreateDto dto);

	/// <summary>
	/// Update existing entity
	/// </summary>
	Task<TDto?> UpdateAsync(Guid id, TUpdateDto dto);

	/// <summary>
	/// Toggle active status
	/// </summary>
	Task<bool> SetActiveAsync(Guid id, bool isActive);

	/// <summary>
	/// Delete single entity
	/// </summary>
	Task<bool> DeleteAsync(Guid id);

	/// <summary>
	/// Bulk delete multiple entities
	/// </summary>
	Task BulkDeleteAsync(List<Guid> ids);

	// ========================================
	// Statistics (Common for all entities)
	// ========================================

	/// <summary>
	/// Get entity statistics
	/// </summary>
	Task<TStatisticsDto> GetStatisticsAsync();

	// ========================================
	// Dropdown Options (Common for all entities)
	// ========================================

	/// <summary>
	/// Get dropdown options for filters
	/// </summary>
	Task<TDropdownOptionsDto> GetDropdownOptionsAsync();

	// ========================================
	// Export Operations (Async with History)
	// ========================================

	/// <summary>
	/// Start async export job (for large datasets)
	/// </summary>
	Task<string> StartExportJobAsync(
		ExportFormat format,
		Dictionary<string, object?> filters);

	/// <summary>
	/// Get export job status
	/// </summary>
	Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId);

	/// <summary>
	/// Download completed export file
	/// </summary>
	Task<byte[]?> DownloadExportFileAsync(string jobId);

	// ========================================
	// Import Operations (Async with History)
	// ========================================

	/// <summary>
	/// Download import template
	/// </summary>
	Task<byte[]> GetImportTemplateAsync(ImportExportFormat format);

	/// <summary>
	/// Start async import job
	/// </summary>
	Task<string> StartImportJobAsync(
		Stream fileStream,
		string fileName,
		DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);

	/// <summary>
	/// Get import job status
	/// </summary>
	Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId);

	/// <summary>
	/// Download import error report (if errors occurred)
	/// </summary>
	Task<byte[]?> GetImportErrorReportAsync(string errorReportId);

	// ========================================
	// Import/Export History (Unified)
	// ========================================

	/// <summary>
	/// Get import/export history for this entity type
	/// </summary>
	Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(
		ImportExportType? type,  // Import, Export, or null for both
		int page,
		int pageSize);
}

/// <summary>
/// Export format options
/// </summary>
public enum ExportFormat
{
	Excel = 1,
	CSV = 2,
	PDF = 3,
	JSON = 4
}

/// <summary>
/// Import/Export format options
/// </summary>
public enum ImportExportFormat
{
	Excel = 1,
	CSV = 2
}

/// <summary>
/// Import/Export type for history tracking
/// </summary>
public enum ImportExportType
{
	Import = 1,
	Export = 2
}

/// <summary>
/// Duplicate handling strategy for imports
/// </summary>
public enum DuplicateHandlingStrategy
{
	Skip = 0,      // Skip duplicate entries (default)
	Update = 1,    // Update existing entities
	CreateNew = 2  // Create new entity anyway (allow duplicates)
}
