using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface IPermissionService
{
    Task<PagedResultDto<PermissionDto>> GetPermissionsAsync(string? search, string? category, string? module, string? action, bool? isActive, int page, int pageSize, string? sortField = null, string? sortDirection = "desc", DateTime? createdFrom = null, DateTime? createdTo = null);
	Task<PermissionDto?> GetPermissionByIdAsync(Guid id);
	Task<PermissionDto?> GetPermissionByCodeAsync(string code);
	Task<PermissionDto> CreatePermissionAsync(CreatePermissionDto dto);
	Task<PermissionDto> UpdatePermissionAsync(Guid id, UpdatePermissionDto dto);
	Task<bool> DeletePermissionAsync(Guid id);
    Task BulkDeleteAsync(List<Guid> ids);
	Task<List<PermissionDto>> BulkCloneAsync(List<Guid> ids);
	Task<List<PermissionDto>> GetPermissionsByModuleAsync(string module);
	Task<List<PermissionDto>> GetPermissionsByCategoryAsync(string category);
	Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode);
	Task<List<string>> GetUserPermissionCodesAsync(Guid userId);
	Task<bool> SeedDefaultPermissionsAsync();

    // Import Template
    Task<byte[]> GetImportTemplateAsync();

    // Async Export (non-blocking)
    Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
    Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId);
    Task<byte[]?> DownloadExportFileAsync(string jobId);

    // Async Import (non-blocking)
    Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
    Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId);

    // Unified Import/Export History
    Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);

    // Statistics & Dropdown Options
    Task<PermissionStatisticsDto> GetStatisticsAsync();
    Task<List<string>> GetUniqueModulesAsync();
    Task<List<string>> GetUniqueActionsAsync();
    Task<List<string>> GetUniqueCategoriesAsync();
    Task<PermissionDropdownOptionsDto> GetDropdownOptionsAsync();

    // Error Report
    Task<byte[]?> GetImportErrorReportAsync(string errorReportId);
}
