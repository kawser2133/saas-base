using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface IRoleService
{
    Task<PagedResultDto<RoleDto>> GetRolesAsync(string? search, bool? isActive, string? roleType, Guid? organizationId, DateTime? createdFrom, DateTime? createdTo, int page, int pageSize, string? sortField = null, string? sortDirection = "asc");
	Task<RoleDto?> GetRoleByIdAsync(Guid id);
	Task<RoleDto> CreateRoleAsync(CreateRoleDto dto);
	Task<RoleDto> UpdateRoleAsync(Guid id, UpdateRoleDto dto);
	Task<bool> DeleteRoleAsync(Guid id);
	Task<bool> SetActiveAsync(Guid id, bool isActive);
	Task BulkDeleteAsync(List<Guid> ids);
	Task<List<RoleDto>> BulkCloneAsync(List<Guid> ids);
	Task<List<RoleHierarchyDto>> GetRoleHierarchyAsync(); // Returns RoleHierarchyDto instead of RoleDto
	Task<List<RoleDto>> GetChildRolesAsync(Guid parentRoleId);
	Task<bool> AssignPermissionToRoleAsync(Guid roleId, Guid permissionId);
	Task<bool> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId);
	Task<List<PermissionDto>> GetRolePermissionsAsync(Guid roleId);
	Task<List<PermissionDto>> GetEffectivePermissionsAsync(Guid roleId);
	Task<bool> AssignRoleToUserAsync(Guid userId, Guid roleId);
	Task<bool> RemoveRoleFromUserAsync(Guid userId, Guid roleId);
	Task<List<UserDto>> GetRoleUsersAsync(Guid roleId);
	Task<List<RoleDto>> GetUserRolesAsync(Guid userId);

	// Async export (non-blocking) - NEW
	Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadExportFileAsync(string jobId);

	// Import - Updated to match Users pattern (EXACT match - no format parameter)
	Task<byte[]> GetImportTemplateAsync();
	Task<byte[]?> GetImportErrorReportAsync(string errorReportId);
	Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId);

	// Statistics & Options
	Task<RoleStatisticsDto> GetStatisticsAsync();
	Task<RoleDropdownOptionsDto> GetDropdownOptionsAsync();

	// Unified import/export history (using ImportExportHistory table)
	Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
}
