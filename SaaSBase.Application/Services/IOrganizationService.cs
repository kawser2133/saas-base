using SaaSBase.Application.DTOs;
using System.IO;

namespace SaaSBase.Application.Services;

public interface IOrganizationService
{
	// Organization Management
	Task<List<OrganizationDto>> GetOrganizationsAsync();
	Task<OrganizationDto?> GetOrganizationByIdAsync(Guid id);
	Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationDto dto);
	Task<OrganizationDto> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto dto);
	Task<bool> DeleteOrganizationAsync(Guid id);
	Task<DTOs.OrganizationSummaryDto> GetOrganizationSummaryAsync(Guid organizationId);
	Task<bool> UploadLogoAsync(Guid organizationId, UploadLogoDto dto);
	Task<bool> RemoveLogoAsync(Guid organizationId);

	// Location Management
	Task<List<LocationDto>> GetLocationsAsync(Guid organizationId);
	Task<PagedResultDto<LocationDto>> GetLocationsPagedAsync(string? search, bool? isActive, string? country, string? city, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null);
	Task<LocationDto?> GetLocationByIdAsync(Guid id);
	Task<LocationDto> CreateLocationAsync(Guid organizationId, CreateLocationDto dto);
	Task<LocationDto> UpdateLocationAsync(Guid id, UpdateLocationDto dto);
	Task<bool> DeleteLocationAsync(Guid id);
	Task<List<LocationHierarchyDto>> GetLocationHierarchyAsync(Guid organizationId);
	Task<List<LocationDto>> BulkCloneLocationsAsync(List<Guid> ids);
	
	// Location Export/Import/History
	Task<string> StartLocationExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetLocationExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadLocationExportFileAsync(string jobId);
	Task<byte[]> GetLocationImportTemplateAsync();
	Task<string> StartLocationImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetLocationImportJobStatusAsync(string jobId);
	Task<byte[]?> GetLocationImportErrorReportAsync(string errorReportId);
	Task<PagedResultDto<ImportExportHistoryDto>> GetLocationImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
	
	// Location Filter Options
	Task<List<string>> GetLocationCountriesAsync();
	Task<List<string>> GetLocationCitiesAsync();
	
	// Dropdown Options
	Task<List<LocationDropdownDto>> GetLocationDropdownOptionsAsync(bool? isActive = null);

	// Business Settings Management
	Task<List<BusinessSettingDto>> GetBusinessSettingsAsync(Guid organizationId);
	Task<PagedResultDto<BusinessSettingDto>> GetBusinessSettingsPagedAsync(string? search, bool? isActive, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null);
	Task<BusinessSettingDto?> GetBusinessSettingByIdAsync(Guid organizationId, Guid id);
	Task<BusinessSettingDto> CreateBusinessSettingAsync(Guid organizationId, CreateBusinessSettingDto dto);
	Task<BusinessSettingDto> UpdateBusinessSettingAsync(Guid organizationId, Guid id, CreateBusinessSettingDto dto);
	Task<bool> DeleteBusinessSettingAsync(Guid organizationId, Guid id);
	Task<List<BusinessSettingDto>> BulkCloneBusinessSettingsAsync(List<Guid> ids);
	
	// Business Settings Export/Import/History
	Task<string> StartBusinessSettingExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetBusinessSettingExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadBusinessSettingExportFileAsync(string jobId);
	Task<byte[]> GetBusinessSettingImportTemplateAsync();
	Task<string> StartBusinessSettingImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetBusinessSettingImportJobStatusAsync(string jobId);
	Task<byte[]?> GetBusinessSettingImportErrorReportAsync(string errorReportId);
	Task<PagedResultDto<ImportExportHistoryDto>> GetBusinessSettingImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);

	// Currency Management
	Task<List<CurrencyDto>> GetCurrenciesAsync(Guid organizationId);
	Task<PagedResultDto<CurrencyDto>> GetCurrenciesPagedAsync(string? search, bool? isActive, string? code, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null);
	Task<CurrencyDto?> GetCurrencyByIdAsync(Guid organizationId, Guid id);
	Task<CurrencyDto> CreateCurrencyAsync(Guid organizationId, CreateCurrencyDto dto);
	Task<CurrencyDto> UpdateCurrencyAsync(Guid organizationId, Guid id, CreateCurrencyDto dto);
	Task<bool> DeleteCurrencyAsync(Guid organizationId, Guid id);
	Task<List<CurrencyDto>> BulkCloneCurrenciesAsync(List<Guid> ids);
	
	// Currency Export/Import/History
	Task<string> StartCurrencyExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetCurrencyExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadCurrencyExportFileAsync(string jobId);
	Task<byte[]> GetCurrencyImportTemplateAsync();
	Task<string> StartCurrencyImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetCurrencyImportJobStatusAsync(string jobId);
	Task<byte[]?> GetCurrencyImportErrorReportAsync(string errorReportId);
	Task<PagedResultDto<ImportExportHistoryDto>> GetCurrencyImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
	
	// Currency Filter Options
	Task<List<string>> GetCurrencyCodesAsync();

	// Tax Rate Management
	Task<List<TaxRateDto>> GetTaxRatesAsync(Guid organizationId);
	Task<PagedResultDto<TaxRateDto>> GetTaxRatesPagedAsync(string? search, bool? isActive, string? taxType, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null);
	Task<TaxRateDto?> GetTaxRateByIdAsync(Guid organizationId, Guid id);
	Task<TaxRateDto> CreateTaxRateAsync(Guid organizationId, CreateTaxRateDto dto);
	Task<TaxRateDto> UpdateTaxRateAsync(Guid organizationId, Guid id, CreateTaxRateDto dto);
	Task<bool> DeleteTaxRateAsync(Guid organizationId, Guid id);
	Task<List<TaxRateDto>> BulkCloneTaxRatesAsync(List<Guid> ids);
	
	// Tax Rate Export/Import/History
	Task<string> StartTaxRateExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetTaxRateExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadTaxRateExportFileAsync(string jobId);
	Task<byte[]> GetTaxRateImportTemplateAsync();
	Task<string> StartTaxRateImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetTaxRateImportJobStatusAsync(string jobId);
	Task<byte[]?> GetTaxRateImportErrorReportAsync(string errorReportId);
	Task<PagedResultDto<ImportExportHistoryDto>> GetTaxRateImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
	
	// Tax Rate Filter Options
	Task<List<string>> GetTaxTypesAsync();

	// Notification Templates
	Task<List<NotificationTemplateDto>> GetNotificationTemplatesAsync(Guid organizationId);
	Task<PagedResultDto<NotificationTemplateDto>> GetNotificationTemplatesPagedAsync(string? search, bool? isActive, string? category, string? templateType, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null);
	Task<NotificationTemplateDto?> GetNotificationTemplateByIdAsync(Guid organizationId, Guid id);
	Task<NotificationTemplateDto> CreateNotificationTemplateAsync(Guid organizationId, CreateNotificationTemplateDto dto);
	Task<NotificationTemplateDto> UpdateNotificationTemplateAsync(Guid organizationId, Guid id, CreateNotificationTemplateDto dto);
	Task<bool> DeleteNotificationTemplateAsync(Guid organizationId, Guid id);
	Task<List<NotificationTemplateDto>> BulkCloneNotificationTemplatesAsync(List<Guid> ids);
	
	// Notification Template Export/Import/History
	Task<string> StartNotificationTemplateExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetNotificationTemplateExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadNotificationTemplateExportFileAsync(string jobId);
	Task<byte[]> GetNotificationTemplateImportTemplateAsync();
	Task<string> StartNotificationTemplateImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetNotificationTemplateImportJobStatusAsync(string jobId);
	Task<byte[]?> GetNotificationTemplateImportErrorReportAsync(string errorReportId);
	Task<PagedResultDto<ImportExportHistoryDto>> GetNotificationTemplateImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
	
	// Notification Template Filter Options
	Task<List<string>> GetNotificationTemplateCategoriesAsync();

	// Integration Settings
	Task<List<IntegrationSettingDto>> GetIntegrationSettingsAsync(Guid organizationId);
	Task<PagedResultDto<IntegrationSettingDto>> GetIntegrationSettingsPagedAsync(string? search, bool? isActive, string? provider, string? integrationType, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null);
	Task<IntegrationSettingDto?> GetIntegrationSettingByIdAsync(Guid organizationId, Guid id);
	Task<IntegrationSettingDto> CreateIntegrationSettingAsync(Guid organizationId, CreateIntegrationSettingDto dto);
	Task<IntegrationSettingDto> UpdateIntegrationSettingAsync(Guid organizationId, Guid id, CreateIntegrationSettingDto dto);
	Task<bool> DeleteIntegrationSettingAsync(Guid organizationId, Guid id);
	Task<List<IntegrationSettingDto>> BulkCloneIntegrationSettingsAsync(List<Guid> ids);
	
	// Integration Setting Export/Import/History
	Task<string> StartIntegrationSettingExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetIntegrationSettingExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadIntegrationSettingExportFileAsync(string jobId);
	Task<byte[]> GetIntegrationSettingImportTemplateAsync();
	Task<string> StartIntegrationSettingImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip);
	Task<ImportJobStatusDto?> GetIntegrationSettingImportJobStatusAsync(string jobId);
	Task<byte[]?> GetIntegrationSettingImportErrorReportAsync(string errorReportId);
	Task<PagedResultDto<ImportExportHistoryDto>> GetIntegrationSettingImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
	
	// Integration Setting Filter Options
	Task<List<string>> GetIntegrationProvidersAsync();
}
