using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

/// <summary>
/// Generic service for handling async import/export operations for any entity type
/// This service handles the heavy lifting so entity services don't need to duplicate code
/// </summary>
public interface IImportExportService
{
	// ========================================
	// EXPORT Operations (Async with Job Tracking)
	// ========================================

	/// <summary>
	/// Start async export job for any entity type
	/// </summary>
	/// <param name="entityType">Entity type (e.g., "User", "Role", "Product")</param>
	/// <param name="format">Export format</param>
	/// <param name="dataFetcher">Function to fetch data with filters</param>
	/// <param name="filters">Filters to apply</param>
	/// <param name="columnMapper">Function to map entity to export columns</param>
	/// <returns>Job ID for tracking</returns>
	Task<string> StartExportJobAsync<TEntity>(
		string entityType,
		ExportFormat format,
		Func<Dictionary<string, object?>, Task<List<TEntity>>> dataFetcher,
		Dictionary<string, object?> filters,
		Func<TEntity, Dictionary<string, object>> columnMapper) where TEntity : class;

	/// <summary>
	/// Get export job status
	/// </summary>
	Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId);

	/// <summary>
	/// Download completed export file
	/// </summary>
	Task<byte[]?> DownloadExportFileAsync(string jobId);

	// ========================================
	// IMPORT Operations (Async with Job Tracking)
	// ========================================

	/// <summary>
	/// Start async import job for any entity type
	/// </summary>
	/// <param name="entityType">Entity type (e.g., "User", "Role", "Product")</param>
	/// <param name="fileStream">File stream</param>
	/// <param name="fileName">File name</param>
	/// <param name="rowProcessor">Function to process each row</param>
	/// <param name="duplicateStrategy">How to handle duplicates</param>
	/// <param name="headerMapping">Optional custom header mapping</param>
	/// <returns>Job ID for tracking</returns>
	Task<string> StartImportJobAsync<TCreateDto>(
		string entityType,
		Stream fileStream,
		string fileName,
		Func<IUnitOfWork, Dictionary<string, string>, TCreateDto?, Task<(bool success, string? error, bool isUpdate, bool isSkip)>> rowProcessor,
		DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip,
		Dictionary<string, string>? headerMapping = null) where TCreateDto : class, new();

	/// <summary>
	/// Get import job status
	/// </summary>
	Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId);

	/// <summary>
	/// Download import error report
	/// </summary>
	Task<byte[]?> GetImportErrorReportAsync(string errorReportId);

	// ========================================
	// TEMPLATE Generation
	// ========================================

	/// <summary>
	/// Generate import template for any entity type
	/// </summary>
	/// <param name="entityType">Entity type</param>
	/// <param name="format">Template format</param>
	/// <param name="headers">Column headers</param>
	/// <param name="sampleData">Sample data rows (optional)</param>
	/// <param name="dropdownOptions">Dropdown validation options (optional)</param>
	/// <returns>Template file bytes</returns>
	Task<byte[]> GenerateImportTemplateAsync(
		string entityType,
		ImportExportFormat format,
		List<string> headers,
		List<Dictionary<string, object>>? sampleData = null,
		Dictionary<string, List<string>>? dropdownOptions = null);

	// ========================================
	// HISTORY Management
	// ========================================

	/// <summary>
	/// Get import/export history for an entity type
	/// </summary>
	Task<PagedResultDto<ImportExportHistoryDto>> GetHistoryAsync(
		string entityType,
		ImportExportType? operationType,
		int page,
		int pageSize);

	/// <summary>
	/// Get all import/export history (admin view)
	/// </summary>
	Task<PagedResultDto<ImportExportHistoryDto>> GetAllHistoryAsync(
		string? entityType,
		ImportExportType? operationType,
		ProcessingStatus? status,
		int page,
		int pageSize);

	/// <summary>
	/// Clean up expired export files
	/// </summary>
	Task CleanupExpiredFilesAsync();

	// ========================================
	// FILE Storage
	// ========================================

	/// <summary>
	/// Store file for download
	/// </summary>
	Task<string> StoreFileAsync(string fileName, byte[] fileData, TimeSpan? expiresIn = null);

	/// <summary>
	/// Get file from storage
	/// </summary>
	Task<byte[]?> GetFileAsync(string filePath);

	/// <summary>
	/// Delete file from storage
	/// </summary>
	Task DeleteFileAsync(string filePath);
}

/// <summary>
/// Processing status enum
/// </summary>
public enum ProcessingStatus
{
	Pending = 0,
	Processing = 1,
	Completed = 2,
	Failed = 3,
	Cancelled = 4
}
