using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface IMfaService
{
	Task<MfaSetupDto> SetupMfaAsync(Guid userId, string mfaType);
	Task<bool> VerifyMfaCodeAsync(Guid userId, string code, string mfaType);
	Task<bool> SendMfaCodeAsync(Guid userId, string mfaType);
	Task<List<MfaSettingsDto>> GetUserMfaSettingsAsync(Guid userId);
	Task<UserMfaSettingsSummaryDto> GetUserMfaSettingsSummaryAsync(Guid userId);
	Task<bool> EnableMfaAsync(Guid userId, string mfaType, string code);
	Task<bool> DisableMfaAsync(Guid userId, string mfaType);
	Task<bool> SetDefaultMfaAsync(Guid userId, string mfaType);
	Task<List<string>> GenerateBackupCodesAsync(Guid userId);
	Task<bool> VerifyBackupCodeAsync(Guid userId, string code, string? ipAddress = null, string? userAgent = null);
	Task<bool> IsMfaEnabledAsync(Guid userId);
	Task<MfaAttemptDto> LogMfaAttemptAsync(Guid userId, string mfaType, string code, bool isSuccessful, string? failureReason = null);
	Task<PagedResultDto<UserMfaSettingsDto>> GetOrganizationMfaSettingsAsync(int page, int pageSize, Guid? organizationId = null, string? search = null, string? sortField = null, string? sortDirection = null);
	
	// Async Export (non-blocking)
	Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadExportFileAsync(string jobId);
	
	// History
	Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
}
