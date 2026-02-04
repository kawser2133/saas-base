using System;

namespace SaaSBase.Domain;

public class MfaSettings : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public bool IsEnabled { get; set; } = false;
	public string MfaType { get; set; } = string.Empty; // TOTP, SMS, Email, BackupCode
	public string? SecretKey { get; set; }
	public string? BackupCodes { get; set; } // JSON array of backup codes
	public string? PhoneNumber { get; set; }
	public string? EmailAddress { get; set; }
	public DateTimeOffset? LastUsedAt { get; set; }
	public int FailedAttempts { get; set; } = 0;
	public DateTimeOffset? LockedUntil { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsDefault { get; set; } = false;

	// Navigation properties
	public User User { get; set; } = null!;
}

public class MfaAttempt : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public bool IsSuccessful { get; set; }
	public string? FailureReason { get; set; }
	public DateTimeOffset AttemptedAt { get; set; }
	public string? IpAddress { get; set; }
	public string? UserAgent { get; set; }

	// Navigation properties
	public User User { get; set; } = null!;
}

public class UserSession : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public string SessionId { get; set; } = string.Empty;
	public string? DeviceId { get; set; }
	public string? DeviceName { get; set; }
	public string? DeviceType { get; set; }
	public string? BrowserName { get; set; }
	public string? BrowserVersion { get; set; }
	public string? OperatingSystem { get; set; }
	public string? IpAddress { get; set; }
	public string? UserAgent { get; set; }
	public string? Location { get; set; }
	public DateTimeOffset LastActivityAt { get; set; }
	public DateTimeOffset ExpiresAt { get; set; }
	public bool IsActive { get; set; } = true;
	public string? Notes { get; set; }
	public string? RefreshToken { get; set; }

	// Navigation properties
	public User User { get; set; } = null!;
}

public class UserActivityLog : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public string Action { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? ResourceType { get; set; }
	public Guid? ResourceId { get; set; }
	public string? IpAddress { get; set; }
	public string? UserAgent { get; set; }
	public string? Location { get; set; }
	public string? Details { get; set; }
	public string? Severity { get; set; } = "INFO"; // INFO, WARNING, ERROR, CRITICAL
	public DateTimeOffset Timestamp { get; set; }

	// Navigation properties
	public User User { get; set; } = null!;
}
