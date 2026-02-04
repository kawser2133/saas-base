namespace SaaSBase.Application.DTOs;

// ========================================
// COMMON IMPORT/EXPORT DTOs
// ========================================

/// <summary>
/// Unified history for both Import and Export operations
/// Replaces entity-specific import/export history DTOs
/// </summary>
public class ImportExportHistoryDto
{
	public Guid Id { get; set; }
	public string JobId { get; set; } = string.Empty; // Job ID for downloading files
	public string EntityType { get; set; } = string.Empty; // "User", "Role", "Product", etc.
	public string OperationType { get; set; } = string.Empty; // "Import" or "Export"
	public string FileName { get; set; } = string.Empty;
	public string Format { get; set; } = string.Empty; // "Excel", "CSV", "PDF", "JSON"

	// Counts (applicable for both import and export)
	public int TotalRows { get; set; }
	public int SuccessCount { get; set; }
	public int UpdatedCount { get; set; }  // For imports with Update strategy
	public int SkippedCount { get; set; }   // For imports with Skip strategy
	public int ErrorCount { get; set; }

	// Status tracking
	public string Status { get; set; } = string.Empty; // "Pending", "Processing", "Completed", "Failed"
	public int Progress { get; set; } = 0; // 0-100

	// Import-specific
	public string? DuplicateHandlingStrategy { get; set; } // "Skip", "Update", "CreateNew"
	public string? ErrorReportId { get; set; }

	// Export-specific
	public string? DownloadUrl { get; set; } // For completed exports
	public string? AppliedFilters { get; set; } // JSON string of filters used

	// Common metadata
	public long FileSizeBytes { get; set; }
	public string ImportedBy { get; set; } = string.Empty;
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? CompletedAtUtc { get; set; }
	public string? ErrorMessage { get; set; }
}

/// <summary>
/// Job status for async imports
/// </summary>
public class CommonImportJobStatusDto
{
	public string JobId { get; set; } = string.Empty;
	public string EntityType { get; set; } = string.Empty;
	public string Status { get; set; } = string.Empty; // "Pending", "Processing", "Completed", "Failed"
	public int ProgressPercent { get; set; } = 0;
	public int TotalRows { get; set; }
	public int ProcessedRows { get; set; }
	public int SuccessCount { get; set; }
	public int UpdatedCount { get; set; }
	public int SkippedCount { get; set; }
	public int ErrorCount { get; set; }
	public string? Message { get; set; }
	public string? ErrorReportId { get; set; }
	public DateTimeOffset? StartedAt { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// Alias for CommonImportJobStatusDto (for backward compatibility)
/// </summary>
public class ImportJobStatusDto : CommonImportJobStatusDto
{
}

/// <summary>
/// Job status for async exports
/// </summary>
public class ExportJobStatusDto
{
	public string JobId { get; set; } = string.Empty;
	public string EntityType { get; set; } = string.Empty;
	public string Format { get; set; } = string.Empty; // "Excel", "CSV", "PDF", "JSON"
	public string Status { get; set; } = string.Empty; // "Pending", "Processing", "Completed", "Failed"
	public int ProgressPercent { get; set; } = 0;
	public int TotalRows { get; set; }
	public int ProcessedRows { get; set; }
	public string? Message { get; set; }
	public string? DownloadUrl { get; set; }
	public long? FileSizeBytes { get; set; }
	public DateTimeOffset? StartedAt { get; set; }
	public DateTimeOffset? CompletedAt { get; set; }
	public DateTimeOffset? ExpiresAt { get; set; } // File expiry (e.g., 24 hours)
}

/// <summary>
/// Generic import result
/// </summary>
public class ImportResultDto
{
	public int TotalRows { get; set; }
	public int SuccessCount { get; set; }
	public int UpdatedCount { get; set; }
	public int SkippedCount { get; set; }
	public int ErrorCount { get; set; }
	public List<string> Errors { get; set; } = new();
	public string? ErrorReportId { get; set; }
	public List<ImportErrorDetailDto> ErrorDetails { get; set; } = new();

	// Entity-specific imported data (optional)
	public List<object>? ImportedRoles { get; set; }
	public List<object>? ImportedPermissions { get; set; }
	public List<object>? ImportedUsers { get; set; }
}

/// <summary>
/// Generic import error detail
/// </summary>
public class ImportErrorDetailDto
{
	public int RowNumber { get; set; }
	public string EntityIdentifier { get; set; } = string.Empty; // e.g., email, name, code
	public string ErrorType { get; set; } = string.Empty; // "Validation", "Duplicate", "System"
	public string ErrorMessage { get; set; } = string.Empty;
	public string? Column { get; set; }
	public Dictionary<string, string> RowData { get; set; } = new(); // All row data for reference
}

/// <summary>
/// Bulk delete request (common for all entities)
/// </summary>
public class BulkDeleteRequest
{
	public List<Guid> Ids { get; set; } = new();
}

/// <summary>
/// Generic paginated result
/// </summary>
public class CommonPagedResultDto<T>
{
	public List<T> Items { get; set; } = new();
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalCount { get; set; }
	public int TotalPages { get; set; }
}

/// <summary>
/// File upload abstraction
/// </summary>
public interface IFileUpload
{
	string FileName { get; }
	long Length { get; }
	Stream OpenReadStream();
	Task<Stream> OpenReadStreamAsync();
	string ContentType { get; }
}

/// <summary>
/// Base statistics DTO (extend for entity-specific stats)
/// </summary>
public class BaseStatisticsDto
{
	public int Total { get; set; }
	public int Active { get; set; }
	public int Inactive { get; set; }
}

/// <summary>
/// Base dropdown options (extend for entity-specific dropdowns)
/// </summary>
public class BaseDropdownOptionsDto
{
	// Override in entity-specific DTO
}
