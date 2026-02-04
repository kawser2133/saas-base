using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface ISessionService
{
	Task<UserSessionDto> CreateSessionAsync(Guid userId, CreateSessionDto dto);
    Task<UserSessionDto?> GetSessionAsync(string sessionId);
    Task<PagedResultDto<UserSessionDto>> GetUserSessionsAsync(Guid userId, int page, int pageSize, string? search = null, string? sortField = null, string? sortDirection = null);
    Task<PagedResultDto<UserSessionDto>> GetOrganizationSessionsAsync(int page, int pageSize, Guid? organizationId = null, string? search = null, string? sortField = null, string? sortDirection = null);
	Task<bool> UpdateSessionActivityAsync(string sessionId);
	Task<bool> RevokeSessionAsync(string sessionId);
	Task<bool> RevokeAllUserSessionsAsync(Guid userId);
	Task<int> BulkRevokeSessionsAsync(List<string> sessionIds);
	Task<bool> RevokeExpiredSessionsAsync();
	Task<List<UserSessionDto>> GetActiveSessionsAsync(Guid userId);

	// Async Export (non-blocking)
	Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters);
	Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId);
	Task<byte[]?> DownloadExportFileAsync(string jobId);
	
	// History
	Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize);
}
