using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface ILocationService
{
    Task<List<LocationDto>> GetLocationsAsync(bool? isActive = null);
    Task<LocationDto?> GetLocationByIdAsync(Guid id);
    Task<LocationDto> CreateLocationAsync(CreateLocationDto dto);
    Task<LocationDto?> UpdateLocationAsync(Guid id, UpdateLocationDto dto);
    Task<bool> DeleteLocationAsync(Guid id);
    Task<bool> SetActiveAsync(Guid id, bool isActive);
    Task<List<LocationHierarchyDto>> GetLocationHierarchyAsync();
    Task<List<LocationDto>> GetLocationsByTypeAsync(string locationType);
    Task<List<LocationDto>> GetChildLocationsAsync(Guid parentLocationId);
    Task<List<LocationDto>> GetRootLocationsAsync();
    Task<List<LocationDropdownDto>> GetLocationDropdownOptionsAsync(bool? isActive = null);
}

public interface IDepartmentService
{
    // Basic CRUD
    Task<PagedResultDto<DepartmentDto>> GetDepartmentsAsync(string? search, bool? isActive, Guid? organizationId, int page, int pageSize, string? sortField = null, string? sortDirection = "desc", DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<List<DepartmentDto>> GetDepartmentsAsync(bool? isActive = null);
    Task<DepartmentDto?> GetDepartmentByIdAsync(Guid id);
    Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto);
    Task<DepartmentDto?> UpdateDepartmentAsync(Guid id, UpdateDepartmentDto dto);
    Task<bool> DeleteDepartmentAsync(Guid id);
    Task BulkDeleteAsync(List<Guid> ids);
    Task<List<DepartmentDto>> BulkCloneAsync(List<Guid> ids);
    Task<bool> SetActiveAsync(Guid id, bool isActive);

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
    Task<DepartmentStatisticsDto> GetStatisticsAsync();
    Task<DepartmentDropdownOptionsDto> GetDropdownOptionsAsync();

    // Error Report
    Task<byte[]?> GetImportErrorReportAsync(string errorReportId);
}

public interface IPositionService
{
    // Basic CRUD
    Task<PagedResultDto<PositionDto>> GetPositionsAsync(string? search, bool? isActive, Guid? departmentId, Guid? organizationId, int page, int pageSize, string? sortField = null, string? sortDirection = "desc", DateTime? createdFrom = null, DateTime? createdTo = null);
    Task<List<PositionDto>> GetPositionsAsync(bool? isActive = null, Guid? departmentId = null);
    Task<PositionDto?> GetPositionByIdAsync(Guid id);
    Task<PositionDto> CreatePositionAsync(CreatePositionDto dto);
    Task<PositionDto?> UpdatePositionAsync(Guid id, UpdatePositionDto dto);
    Task<bool> DeletePositionAsync(Guid id);
    Task BulkDeleteAsync(List<Guid> ids);
    Task<List<PositionDto>> BulkCloneAsync(List<Guid> ids);
    Task<bool> SetActiveAsync(Guid id, bool isActive);

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
    Task<PositionStatisticsDto> GetStatisticsAsync();
    Task<PositionDropdownOptionsDto> GetDropdownOptionsAsync();

    // Error Report
    Task<byte[]?> GetImportErrorReportAsync(string errorReportId);
}
