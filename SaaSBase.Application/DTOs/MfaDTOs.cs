using System;

namespace SaaSBase.Application.DTOs;

public class MfaSettingsDto
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public bool IsEnabled { get; set; }
	public string MfaType { get; set; } = string.Empty;
	public string? SecretKey { get; set; }
	public bool IsActive { get; set; }
	public bool IsDefault { get; set; }
	public string? PhoneNumber { get; set; }
	public string? EmailAddress { get; set; }
	public DateTimeOffset? LastUsedAt { get; set; }
	public int FailedAttempts { get; set; }
	public DateTimeOffset? LockedUntil { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
}

public class MfaSetupDto
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
	public string? SecretKey { get; set; }
	public string? QrCodeUrl { get; set; }
	public string? PhoneNumber { get; set; }
	public string? EmailAddress { get; set; }
	public List<string> BackupCodes { get; set; } = new();
}

public class MfaVerificationDto
{
	public Guid UserId { get; set; }
	public string Code { get; set; } = string.Empty;
	public string MfaType { get; set; } = string.Empty;
}

public class MfaAttemptDto
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public bool IsSuccessful { get; set; }
	public string IpAddress { get; set; } = string.Empty;
	public string UserAgent { get; set; } = string.Empty;
	public DateTimeOffset AttemptedAt { get; set; }
	public string? FailureReason { get; set; }
}

public class UserMfaSettingsDto
{
	public Guid Id { get; set; }
	public Guid UserId { get; set; }
	public string UserEmail { get; set; } = string.Empty;
	public string MfaType { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public bool IsDefault { get; set; }
	public DateTimeOffset? LastUsedAt { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	
	// Organization context (for System Admin view)
	public Guid OrganizationId { get; set; }
	public string? OrganizationName { get; set; }
}

public class UserMfaSettingsSummaryDto
{
	public bool IsMfaEnabled { get; set; }
	public List<string> EnabledMethods { get; set; } = new();
	public string? DefaultMethod { get; set; }
	public string? PhoneNumberMasked { get; set; }
	public string? EmailMasked { get; set; }
	public string? TotpSetupQrCode { get; set; }
}