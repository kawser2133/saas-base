namespace SaaSBase.Domain;

/// <summary>
/// Unified history table for tracking both Import and Export operations across all entities
/// Replaces entity-specific import/export history tables
/// </summary>
public class ImportExportHistory : BaseEntity
{
	// ========================================
	// Entity Information
	// ========================================

	/// <summary>
	/// Type of entity (User, Role, Product, Category, etc.)
	/// </summary>
	public string EntityType { get; set; } = string.Empty;

	/// <summary>
	/// Operation type (Import or Export)
	/// </summary>
	public ImportExportOperationType OperationType { get; set; }

	// ========================================
	// File Information
	// ========================================

	/// <summary>
	/// Original file name
	/// </summary>
	public string FileName { get; set; } = string.Empty;

	/// <summary>
	/// File format (Excel, CSV, PDF, JSON)
	/// </summary>
	public string Format { get; set; } = string.Empty;

	/// <summary>
	/// File size in bytes
	/// </summary>
	public long FileSizeBytes { get; set; }

	/// <summary>
	/// File path in storage (for downloads)
	/// </summary>
	public string? FilePath { get; set; }

	// ========================================
	// Processing Information
	// ========================================

	/// <summary>
	/// Job ID for async processing
	/// </summary>
	public string JobId { get; set; } = string.Empty;

	/// <summary>
	/// Current status
	/// </summary>
	public ProcessingStatus Status { get; set; }

	/// <summary>
	/// Progress percentage (0-100)
	/// </summary>
	public int Progress { get; set; } = 0;

	// ========================================
	// Counts
	// ========================================

	/// <summary>
	/// Total rows in file
	/// </summary>
	public int TotalRows { get; set; }

	/// <summary>
	/// Successfully processed rows
	/// </summary>
	public int SuccessCount { get; set; }

	/// <summary>
	/// Updated rows (import with Update strategy)
	/// </summary>
	public int UpdatedCount { get; set; }

	/// <summary>
	/// Skipped rows (import with Skip strategy)
	/// </summary>
	public int SkippedCount { get; set; }

	/// <summary>
	/// Failed rows
	/// </summary>
	public int ErrorCount { get; set; }

	// ========================================
	// Import-Specific Fields
	// ========================================

	/// <summary>
	/// Duplicate handling strategy (for imports only)
	/// </summary>
	public string? DuplicateHandlingStrategy { get; set; }

	/// <summary>
	/// Error report ID (for downloading detailed errors)
	/// </summary>
	public string? ErrorReportId { get; set; }

	/// <summary>
	/// Path to error report file
	/// </summary>
	public string? ErrorReportPath { get; set; }

	// ========================================
	// Export-Specific Fields
	// ========================================

	/// <summary>
	/// JSON string of applied filters (for exports)
	/// </summary>
	public string? AppliedFilters { get; set; }

	/// <summary>
	/// Download URL (for completed exports)
	/// </summary>
	public string? DownloadUrl { get; set; }

	/// <summary>
	/// File expiration date (e.g., 24 hours after creation)
	/// </summary>
	public DateTimeOffset? ExpiresAt { get; set; }

	// ========================================
	// Metadata
	// ========================================

	/// <summary>
	/// When processing started
	/// </summary>
	public DateTimeOffset? StartedAt { get; set; }

	/// <summary>
	/// When processing completed
	/// </summary>
	public DateTimeOffset? CompletedAt { get; set; }

	/// <summary>
	/// Error message (if failed)
	/// </summary>
	public string? ErrorMessage { get; set; }

	/// <summary>
	/// User who initiated the operation
	/// </summary>
	public string ImportedBy { get; set; } = string.Empty;
}

/// <summary>
/// Import/Export operation type
/// </summary>
public enum ImportExportOperationType
{
	Import = 1,
	Export = 2
}

/// <summary>
/// Processing status
/// </summary>
public enum ProcessingStatus
{
	Pending = 0,
	Processing = 1,
	Completed = 2,
	Failed = 3,
	Cancelled = 4
}
