using SaaSBase.Application.DTOs;

namespace SaaSBase.Application.Services;

public interface IUserProfileService
{
	Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
	Task<UserProfileDto> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto);
	Task<bool> UploadAvatarAsync(Guid userId, UploadAvatarDto dto);
	Task<bool> RemoveAvatarAsync(Guid userId);
	Task<UserNotificationPreferencesDto> GetNotificationPreferencesAsync(Guid userId);
	Task<UserNotificationPreferencesDto> UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesDto dto);
	Task<List<UserActivityLogDto>> GetUserActivityLogsAsync(Guid userId, int page = 1, int pageSize = 50);
	Task<UserActivityLogDto> LogUserActivityAsync(Guid userId, LogUserActivityDto dto);
	Task<List<UserActivityLogDto>> GetUserActivityLogsByActionAsync(Guid userId, string action, int page = 1, int pageSize = 50);
	Task<List<UserActivityLogDto>> GetUserActivityLogsByDateRangeAsync(Guid userId, DateTimeOffset startDate, DateTimeOffset endDate, int page = 1, int pageSize = 50);
	Task<bool> ClearUserActivityLogsAsync(Guid userId, DateTimeOffset? olderThan = null);
}
