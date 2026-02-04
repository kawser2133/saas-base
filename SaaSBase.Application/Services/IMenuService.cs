using SaaSBase.Application.DTOs;
using System.IO;

namespace SaaSBase.Application.Services;

public interface IMenuService
{
	// CRUD Operations
	Task<PagedResultDto<MenuDto>> GetMenusAsync(string? search, string? section, Guid? parentMenuId, bool? isActive, DateTime? createdFrom, DateTime? createdTo, int page, int pageSize, string? sortField = null, string? sortDirection = "asc");
	Task<MenuDto?> GetMenuByIdAsync(Guid id);
	Task<MenuDto> CreateMenuAsync(CreateMenuDto dto);
	Task<MenuDto> UpdateMenuAsync(Guid id, UpdateMenuDto dto);
	Task<bool> DeleteMenuAsync(Guid id);
	Task BulkDeleteAsync(List<Guid> ids);
	Task<List<MenuDto>> BulkCloneAsync(List<Guid> ids);
	Task<bool> SetActiveAsync(Guid id, bool isActive);

	// Dropdown & Options
	Task<List<MenuDropdownDto>> GetMenuDropdownOptionsAsync();
	Task<List<string>> GetUniqueSectionsAsync();
	Task<List<MenuDto>> GetMenusBySectionAsync(string section);
	Task<List<MenuDto>> GetChildMenusAsync(Guid parentMenuId);

	// User-specific menus (based on permissions)
	Task<UserMenuResponseDto> GetUserMenusAsync(Guid userId);

	// Statistics
	Task<MenuStatisticsDto> GetStatisticsAsync();

	// Hierarchy
	Task<List<MenuDto>> GetMenuHierarchyAsync();

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

	// Error Report
	Task<byte[]?> GetImportErrorReportAsync(string errorReportId);
}

