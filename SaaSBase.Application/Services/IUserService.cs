using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface IUserService : IBaseEntityService<UserDetailsDto, CreateUserDto, UpdateUserDto, UserStatisticsDto, DropdownOptionsDto>
{
    // Custom methods specific to User (beyond generic CRUD)
    Task<PagedResultDto<UserListItemDto>> GetUsersAsync(string? search, string? department, string? jobTitle, string? location, bool? isActive, bool? isEmailVerified, Guid? roleId, Guid? organizationId, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc");
	Task<UserDetailsDto?> GetUserByIdAsync(Guid id);
	Task<UserDetailsDto> CreateUserAsync(CreateUserDto dto);
	Task<UserDetailsDto?> UpdateUserAsync(Guid id, UpdateUserDto dto);
	Task<bool> SetActiveAsync(Guid id, bool isActive);
	Task<bool> DeleteUserAsync(Guid id);
	Task BulkDeleteAsync(List<Guid> ids);
	Task<List<UserDetailsDto>> BulkCloneAsync(List<Guid> ids);

	// Async export (non-blocking)
	Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadExportFileAsync(string jobId);

	Task<byte[]> GetImportTemplateAsync();
	Task<byte[]?> GetImportErrorReportAsync(string errorReportId);
	Task GeneratePasswordResetLinkAsync(Guid id);
	Task SendEmailVerificationAsync(Guid id);
	Task ResendInvitationAsync(Guid id);
	Task<UserStatisticsDto> GetUserStatisticsAsync();
	Task<DropdownOptionsDto> GetDropdownOptionsAsync();
	Task<List<string>> GetLocationOptionsAsync();
	Task<List<string>> GetDepartmentOptionsAsync();
	Task<List<string>> GetPositionOptionsAsync();

	// Unified import/export history (using ImportExportHistory table)
	Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);

	// Async import jobs
	Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId);
}

public class UserImportHistoryDto
{
	public Guid Id { get; set; }
	public string FileName { get; set; } = string.Empty;
	public int TotalRows { get; set; }
	public int SuccessCount { get; set; }
	public int UpdatedCount { get; set; }
	public int SkippedCount { get; set; }
	public int ErrorCount { get; set; }
	public string DuplicateStrategy { get; set; } = string.Empty;
	public string? ErrorReportId { get; set; }
	public string ImportedByUserName { get; set; } = string.Empty;
	public DateTimeOffset ImportedAt { get; set; }
	public long FileSizeBytes { get; set; }
	public string Status { get; set; } = string.Empty;
}

public class ImportResult
{
	public int SuccessCount { get; set; }
	public int UpdatedCount { get; set; }
	public int SkippedCount { get; set; }
	public int ErrorCount { get; set; }
	public List<string> Errors { get; set; } = new();
	public string? ErrorReportId { get; set; } // For downloading detailed error report
	public List<ImportErrorDetail> ErrorDetails { get; set; } = new();
}

public class ImportErrorDetail
{
	public int RowNumber { get; set; }
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? Email { get; set; }
	public string ErrorType { get; set; } = string.Empty; // "Validation", "Duplicate", "System"
	public string ErrorMessage { get; set; } = string.Empty;
	public string? Column { get; set; }
}


