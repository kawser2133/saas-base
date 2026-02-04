using System;

namespace SaaSBase.Domain;

public class PasswordPolicy : BaseEntity, ITenantEntity
{
	public int MinLength { get; set; } = 8;
	public int MaxLength { get; set; } = 128;
	public bool RequireUppercase { get; set; } = true;
	public bool RequireLowercase { get; set; } = true;
	public bool RequireNumbers { get; set; } = true;
	public bool RequireSpecialCharacters { get; set; } = true;
	public int MinSpecialCharacters { get; set; } = 1;
	public int MaxConsecutiveCharacters { get; set; } = 3;
	public bool PreventCommonPasswords { get; set; } = true;
	public bool PreventUserInfoInPassword { get; set; } = true;
	public int PasswordHistoryCount { get; set; } = 5;
	public int MaxFailedAttempts { get; set; } = 5;
	public int LockoutDurationMinutes { get; set; } = 30;
	public int PasswordExpiryDays { get; set; } = 90;
	public bool RequirePasswordChangeOnFirstLogin { get; set; } = true;
	public string? AllowedSpecialCharacters { get; set; } = "!@#$%^&*()_+-=[]{}|;:,.<>?";
	public string? DisallowedPasswords { get; set; } = "password,123456,admin,root,user";
	public bool IsActive { get; set; } = true;
	public DateTimeOffset? LastModifiedAtUtc { get; set; }
}

