using System;

namespace SaaSBase.Application.DTOs;

public class UserProfileDto
{
	public Guid Id { get; set; }
	public string Email { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? PhoneNumber { get; set; }
	public bool IsActive { get; set; }
	public bool IsEmailVerified { get; set; }
	public bool IsPhoneVerified { get; set; }
	public DateTimeOffset? LastLoginAt { get; set; }
	public string? AvatarUrl { get; set; }
	public string? TimeZone { get; set; }
	public string? Language { get; set; }
	public string? Theme { get; set; }
	public string? Notes { get; set; }
	public bool IsMfaEnabled { get; set; }
	public string? JobTitle { get; set; }
	public string? Department { get; set; }
	public string? Location { get; set; }
	public string? EmployeeId { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string? EmergencyContactName { get; set; }
	public string? EmergencyContactPhone { get; set; }
	public string? EmergencyContactRelation { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset LastModifiedAtUtc { get; set; }
	
	// Notification Preferences
	public UserNotificationPreferencesDto? NotificationPreferences { get; set; }
}

public class UpdateUserProfileDto
{
	public string FullName { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? PhoneNumber { get; set; }
	public string? TimeZone { get; set; }
	public string? Language { get; set; }
	public string? Theme { get; set; }
	public string? Notes { get; set; }
	public string? JobTitle { get; set; }
	public string? Department { get; set; }
	public string? Location { get; set; }
	public string? EmployeeId { get; set; }
	public DateTimeOffset? DateOfBirth { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string? EmergencyContactName { get; set; }
	public string? EmergencyContactPhone { get; set; }
	public string? EmergencyContactRelation { get; set; }
}

public class UploadAvatarDto
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public byte[] FileData { get; set; } = Array.Empty<byte>();
	public long FileSize { get; set; }
}

public class UserNotificationPreferencesDto
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public bool EmailNotifications { get; set; }
	public bool SmsNotifications { get; set; }
	public bool PushNotifications { get; set; }
	public bool InventoryAlerts { get; set; }
	public bool OrderNotifications { get; set; }
	public bool SystemNotifications { get; set; }
	public bool MarketingEmails { get; set; }
	public bool SecurityAlerts { get; set; }
	public bool SystemUpdates { get; set; }
	public bool WeeklyReports { get; set; }
	public bool MonthlyReports { get; set; }
	public string NotificationFrequency { get; set; } = "IMMEDIATE"; // IMMEDIATE, DAILY, WEEKLY
	public string PreferredNotificationTime { get; set; } = "09:00"; // HH:MM format
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset LastModifiedAtUtc { get; set; }
}

public class UpdateNotificationPreferencesDto
{
	public bool EmailNotifications { get; set; }
	public bool SmsNotifications { get; set; }
	public bool PushNotifications { get; set; }
	public bool InventoryAlerts { get; set; }
	public bool OrderNotifications { get; set; }
	public bool SystemNotifications { get; set; }
	public bool MarketingEmails { get; set; }
	public bool SecurityAlerts { get; set; }
	public bool SystemUpdates { get; set; }
	public bool WeeklyReports { get; set; }
	public bool MonthlyReports { get; set; }
	public string NotificationFrequency { get; set; } = "IMMEDIATE";
	public string PreferredNotificationTime { get; set; } = "09:00";
}

public class UserActivityLogDto
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public string Action { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? ResourceType { get; set; }
	public Guid? ResourceId { get; set; }
	public string IpAddress { get; set; } = string.Empty;
	public string UserAgent { get; set; } = string.Empty;
	public string Location { get; set; } = string.Empty;
	public string? Details { get; set; }
	public string Severity { get; set; } = string.Empty;
	public DateTimeOffset Timestamp { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
}

public class LogUserActivityDto
{
	public string Action { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string? ResourceType { get; set; }
	public Guid? ResourceId { get; set; }
	public string IpAddress { get; set; } = string.Empty;
	public string UserAgent { get; set; } = string.Empty;
	public string Location { get; set; } = string.Empty;
	public string? Details { get; set; }
	public string Severity { get; set; } = "INFO";
}

public class DropdownOptionsDto
{
	public List<string> Locations { get; set; } = new();
	public List<string> Departments { get; set; } = new();
	public List<string> Positions { get; set; } = new();
    public List<RoleDropdownDto> Roles { get; set; } = new();
}
