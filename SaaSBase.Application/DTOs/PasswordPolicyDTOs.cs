using System;

namespace SaaSBase.Application.DTOs;

public class PasswordPolicyDto
{
	public Guid Id { get; set; }
	public int MinLength { get; set; }
	public int MaxLength { get; set; }
	public bool RequireUppercase { get; set; }
	public bool RequireLowercase { get; set; }
	public bool RequireNumbers { get; set; }
	public bool RequireSpecialCharacters { get; set; }
	public int MinSpecialCharacters { get; set; }
	public int MaxConsecutiveCharacters { get; set; }
	public bool PreventCommonPasswords { get; set; }
	public bool PreventUserInfoInPassword { get; set; }
	public int PasswordHistoryCount { get; set; }
	public int MaxFailedAttempts { get; set; }
	public int LockoutDurationMinutes { get; set; }
	public int PasswordExpiryDays { get; set; }
	public bool RequirePasswordChangeOnFirstLogin { get; set; }
	public string? AllowedSpecialCharacters { get; set; }
	public string? DisallowedPasswords { get; set; }
	public bool IsActive { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset LastModifiedAtUtc { get; set; }
}

public class UpdatePasswordPolicyDto
{
	public int MinLength { get; set; }
	public int MaxLength { get; set; }
	public bool RequireUppercase { get; set; }
	public bool RequireLowercase { get; set; }
	public bool RequireNumbers { get; set; }
	public bool RequireSpecialCharacters { get; set; }
	public int MinSpecialCharacters { get; set; }
	public int MaxConsecutiveCharacters { get; set; }
	public bool PreventCommonPasswords { get; set; }
	public bool PreventUserInfoInPassword { get; set; }
	public int PasswordHistoryCount { get; set; }
	public int MaxFailedAttempts { get; set; }
	public int LockoutDurationMinutes { get; set; }
	public int PasswordExpiryDays { get; set; }
	public bool RequirePasswordChangeOnFirstLogin { get; set; }
	public string? AllowedSpecialCharacters { get; set; }
	public string? DisallowedPasswords { get; set; }
	public bool IsActive { get; set; }
}

public class PasswordValidationResult
{
	public bool IsValid { get; set; }
	public List<string> Errors { get; set; } = new();
	public List<string> Warnings { get; set; } = new();
	public int StrengthScore { get; set; }
	public string StrengthLevel { get; set; } = string.Empty; // WEAK, MEDIUM, STRONG, VERY_STRONG
}

public class PasswordChangeDto
{
	public Guid UserId { get; set; }
	public string CurrentPassword { get; set; } = string.Empty;
	public string NewPassword { get; set; } = string.Empty;
	public string ConfirmPassword { get; set; } = string.Empty;
}

public class PasswordResetRequestDto
{
	public string Email { get; set; } = string.Empty;
}

public class PasswordResetDto
{
	public string Token { get; set; } = string.Empty;
	public string NewPassword { get; set; } = string.Empty;
	public string ConfirmPassword { get; set; } = string.Empty;
}

public class PasswordValidationResultDto
{
	public bool IsValid { get; set; }
	public List<string> Errors { get; set; } = new();
}

public class PasswordStrengthDto
{
	public int Score { get; set; }
	public string Level { get; set; } = string.Empty;
	public List<string> Feedback { get; set; } = new();
	public List<string> Suggestions { get; set; } = new();
}
