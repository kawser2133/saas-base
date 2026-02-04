using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;

namespace SaaSBase.Application.Implementations;

public class UserProfileService : IUserProfileService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
	private readonly IFileService _fileService;

	public UserProfileService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, IFileService fileService)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_fileService = fileService;
	}

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);

        if (user == null) return null;

        // Get notification preferences
        var notificationPreferences = await GetNotificationPreferencesAsync(userId);

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            IsPhoneVerified = user.IsPhoneVerified,
            LastLoginAt = user.LastLoginAt,
            AvatarUrl = _fileService.GetFileUrl(user.AvatarUrl),
            TimeZone = user.TimeZone,
            Language = user.Language,
            Theme = user.Theme,
            Notes = user.Notes,
            IsMfaEnabled = user.IsMfaEnabled,
            JobTitle = user.JobTitle,
            Department = user.Department,
            EmployeeId = user.EmployeeId,
            Location = user.Location,
            DateOfBirth = user.DateOfBirth,
            Address = user.Address,
            City = user.City,
            State = user.State,
            Country = user.Country,
            PostalCode = user.PostalCode,
            EmergencyContactName = user.EmergencyContactName,
            EmergencyContactPhone = user.EmergencyContactPhone,
            EmergencyContactRelation = user.EmergencyContactRelation,
            CreatedAtUtc = user.CreatedAtUtc,
            LastModifiedAtUtc = user.LastModifiedAtUtc ?? DateTimeOffset.UtcNow,
            NotificationPreferences = notificationPreferences
        };
    }

    public async Task<UserProfileDto> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto dto)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);

        if (user == null) throw new ArgumentException("User not found");

        user.FullName = dto.FullName;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PhoneNumber = dto.PhoneNumber;
        user.TimeZone = dto.TimeZone;
        user.Language = dto.Language;
        user.Theme = dto.Theme;
        user.Notes = dto.Notes;
        user.JobTitle = dto.JobTitle;
        user.Department = dto.Department;
        user.Location = dto.Location;
        user.EmployeeId = dto.EmployeeId;
        user.DateOfBirth = dto.DateOfBirth;
        user.Address = dto.Address;
        user.City = dto.City;
        user.State = dto.State;
        user.Country = dto.Country;
        user.PostalCode = dto.PostalCode;
        user.EmergencyContactName = dto.EmergencyContactName;
        user.EmergencyContactPhone = dto.EmergencyContactPhone;
        user.EmergencyContactRelation = dto.EmergencyContactRelation;

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync();

        return await GetUserProfileAsync(userId) ?? throw new InvalidOperationException("Failed to update user profile");
    }

    public async Task<bool> UploadAvatarAsync(Guid userId, UploadAvatarDto dto)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);

        if (user == null) return false;

        try
        {
            // Delete old avatar if exists
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                await _fileService.DeleteFileAsync(user.AvatarUrl);
            }

            // Save new file
            var folderPath = $"avatars/{userId}";
            var savedFilePath = await _fileService.SaveFileAsync(dto.FileData, dto.FileName, folderPath);

            // Update user with new avatar path
            user.AvatarUrl = savedFilePath;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> RemoveAvatarAsync(Guid userId)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);

        if (user == null) return false;

        try
        {
            // Delete file if exists
            if (!string.IsNullOrEmpty(user.AvatarUrl))
            {
                await _fileService.DeleteFileAsync(user.AvatarUrl);
            }

            // Clear avatar URL
            user.AvatarUrl = null;

            userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<UserNotificationPreferencesDto> GetNotificationPreferencesAsync(Guid userId)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var preferencesRepo = _unitOfWork.Repository<UserNotificationPreferences>();
        var preferences = await preferencesRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId);

        if (preferences == null)
        {
            // Create default preferences
            preferences = new UserNotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                UserId = userId,
                EmailNotifications = true,
                SmsNotifications = false,
                PushNotifications = true,
                MarketingEmails = false,
                SecurityAlerts = true,
                SystemUpdates = true,
                PreferredNotificationTime = "09:00"
            };

            await preferencesRepo.AddAsync(preferences);
            await _unitOfWork.SaveChangesAsync();
        }

        return new UserNotificationPreferencesDto
        {
            Id = preferences.Id,
            UserId = preferences.UserId,
            EmailNotifications = preferences.EmailNotifications,
            SmsNotifications = preferences.SmsNotifications,
            PushNotifications = preferences.PushNotifications,
            MarketingEmails = preferences.MarketingEmails,
            SecurityAlerts = preferences.SecurityAlerts,
            SystemUpdates = preferences.SystemUpdates,
            PreferredNotificationTime = preferences.PreferredNotificationTime
        };
    }

    public async Task<UserNotificationPreferencesDto> UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesDto dto)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var preferencesRepo = _unitOfWork.Repository<UserNotificationPreferences>();
        var preferences = await preferencesRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId);

        if (preferences == null)
        {
            preferences = new UserNotificationPreferences
            {
                Id = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                UserId = userId
            };
        }

        preferences.EmailNotifications = dto.EmailNotifications;
        preferences.SmsNotifications = dto.SmsNotifications;
        preferences.PushNotifications = dto.PushNotifications;
        preferences.MarketingEmails = dto.MarketingEmails;
        preferences.SecurityAlerts = dto.SecurityAlerts;
        preferences.SystemUpdates = dto.SystemUpdates;
        preferences.PreferredNotificationTime = dto.PreferredNotificationTime;

        if (preferences.Id == Guid.Empty)
        {
            await preferencesRepo.AddAsync(preferences);
        }
        else
        {
            preferencesRepo.Update(preferences);
        }

        await _unitOfWork.SaveChangesAsync();

        return new UserNotificationPreferencesDto
        {
            Id = preferences.Id,
            UserId = preferences.UserId,
            EmailNotifications = preferences.EmailNotifications,
            SmsNotifications = preferences.SmsNotifications,
            PushNotifications = preferences.PushNotifications,
            MarketingEmails = preferences.MarketingEmails,
            SecurityAlerts = preferences.SecurityAlerts,
            SystemUpdates = preferences.SystemUpdates,
            PreferredNotificationTime = preferences.PreferredNotificationTime
        };
    }

    public async Task<List<UserActivityLogDto>> GetUserActivityLogsAsync(Guid userId, int page = 1, int pageSize = 50)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var logRepo = _unitOfWork.Repository<UserActivityLog>();
        var logs = await logRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId);

        var totalCount = logs.Count();
        var pagedLogs = logs
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return pagedLogs.Select(x => new UserActivityLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            Action = x.Action,
            Description = x.Description,
            ResourceType = x.ResourceType,
            ResourceId = x.ResourceId,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            Location = x.Location,
            Details = x.Details,
            Severity = x.Severity,
            Timestamp = x.Timestamp,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    public async Task<UserActivityLogDto> LogUserActivityAsync(Guid userId, LogUserActivityDto dto)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var logRepo = _unitOfWork.Repository<UserActivityLog>();

        var log = new UserActivityLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            UserId = userId,
            Action = dto.Action,
            Description = dto.Description,
            ResourceType = dto.ResourceType,
            ResourceId = dto.ResourceId,
            IpAddress = dto.IpAddress,
            UserAgent = dto.UserAgent,
            Location = dto.Location,
            Details = dto.Details,
            Severity = dto.Severity,
            Timestamp = DateTimeOffset.UtcNow
        };

        await logRepo.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();

        return new UserActivityLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            Action = log.Action,
            Description = log.Description,
            ResourceType = log.ResourceType,
            ResourceId = log.ResourceId,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            Location = log.Location,
            Details = log.Details,
            Severity = log.Severity,
            Timestamp = log.Timestamp,
            CreatedAtUtc = log.CreatedAtUtc
        };
    }

    public async Task<List<UserActivityLogDto>> GetUserActivityLogsByActionAsync(Guid userId, string action, int page = 1, int pageSize = 50)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var logRepo = _unitOfWork.Repository<UserActivityLog>();
        var logs = await logRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.Action == action);

        var totalCount = logs.Count();
        var pagedLogs = logs
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return pagedLogs.Select(x => new UserActivityLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            Action = x.Action,
            Description = x.Description,
            ResourceType = x.ResourceType,
            ResourceId = x.ResourceId,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            Location = x.Location,
            Details = x.Details,
            Severity = x.Severity,
            Timestamp = x.Timestamp,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    public async Task<List<UserActivityLogDto>> GetUserActivityLogsByDateRangeAsync(Guid userId, DateTimeOffset startDate, DateTimeOffset endDate, int page = 1, int pageSize = 50)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var logRepo = _unitOfWork.Repository<UserActivityLog>();
        var logs = await logRepo.FindManyAsync(x =>
            x.UserId == userId &&
            x.OrganizationId == OrganizationId &&
            x.Timestamp >= startDate &&
            x.Timestamp <= endDate);

        var totalCount = logs.Count();
        var pagedLogs = logs
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return pagedLogs.Select(x => new UserActivityLogDto
        {
            Id = x.Id,
            UserId = x.UserId,
            Action = x.Action,
            Description = x.Description,
            ResourceType = x.ResourceType,
            ResourceId = x.ResourceId,
            IpAddress = x.IpAddress,
            UserAgent = x.UserAgent,
            Location = x.Location,
            Details = x.Details,
            Severity = x.Severity,
            Timestamp = x.Timestamp,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToList();
    }

    public async Task<bool> ClearUserActivityLogsAsync(Guid userId, DateTimeOffset? olderThan = null)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var logRepo = _unitOfWork.Repository<UserActivityLog>();
        var logs = await logRepo.FindManyAsync(x =>
            x.UserId == userId &&
            x.OrganizationId == OrganizationId &&
            (olderThan == null || x.Timestamp < olderThan.Value));

        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
