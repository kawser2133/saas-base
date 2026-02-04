using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Application;
using SaaSBase.Domain;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SaaSBase.Application.Implementations;

public class PasswordPolicyService : IPasswordPolicyService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;

	public PasswordPolicyService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
	}

	public async Task<PasswordPolicyDto> GetPasswordPolicyAsync(Guid? organizationId = null)
	{
		// Use provided organizationId or get from tenant context
		var OrganizationId = organizationId ?? _tenantService.GetOrganizationId();
		var policyRepo = _unitOfWork.Repository<PasswordPolicy>();
		
		// Use IgnoreQueryFilters to find existing policy even if soft-deleted
		// This prevents duplicate key violations when a policy exists but query filters hide it
		var policy = await policyRepo.GetQueryable()
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (policy == null)
		{
			// Create default policy
			policy = new PasswordPolicy
			{
				Id = Guid.NewGuid(),
				OrganizationId = OrganizationId,
				MinLength = 8,
				MaxLength = 128,
				RequireUppercase = true,
				RequireLowercase = true,
				RequireNumbers = true,
				RequireSpecialCharacters = true,
				MinSpecialCharacters = 1,
				MaxConsecutiveCharacters = 3,
				PasswordHistoryCount = 5,
				MaxFailedAttempts = 5,
				LockoutDurationMinutes = 30,
				PasswordExpiryDays = 90,
				RequirePasswordChangeOnFirstLogin = true,
				IsActive = true,
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			await policyRepo.AddAsync(policy);
			await _unitOfWork.SaveChangesAsync();
		}
		else if (policy.IsDeleted)
		{
			// Restore if soft-deleted
			policy.IsDeleted = false;
			policy.DeletedAtUtc = null;
			policy.DeletedBy = null;
			policyRepo.Update(policy);
			await _unitOfWork.SaveChangesAsync();
		}

		return new PasswordPolicyDto
		{
			Id = policy.Id,
			MinLength = policy.MinLength,
			MaxLength = policy.MaxLength,
			RequireUppercase = policy.RequireUppercase,
			RequireLowercase = policy.RequireLowercase,
			RequireNumbers = policy.RequireNumbers,
			RequireSpecialCharacters = policy.RequireSpecialCharacters,
			MinSpecialCharacters = policy.MinSpecialCharacters,
			MaxConsecutiveCharacters = policy.MaxConsecutiveCharacters,
			PasswordHistoryCount = policy.PasswordHistoryCount,
			MaxFailedAttempts = policy.MaxFailedAttempts,
			LockoutDurationMinutes = policy.LockoutDurationMinutes,
			PasswordExpiryDays = policy.PasswordExpiryDays,
			RequirePasswordChangeOnFirstLogin = policy.RequirePasswordChangeOnFirstLogin,
			IsActive = policy.IsActive,
			CreatedAtUtc = policy.CreatedAtUtc,
			LastModifiedAtUtc = policy.LastModifiedAtUtc ?? DateTimeOffset.UtcNow
		};
	}

	public async Task<PasswordPolicyDto> UpdatePasswordPolicyAsync(UpdatePasswordPolicyDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var policyRepo = _unitOfWork.Repository<PasswordPolicy>();
		
		// Use IgnoreQueryFilters to find existing policy even if soft-deleted
		// This prevents duplicate key violations when a policy exists but query filters hide it
		var policy = await policyRepo.GetQueryable()
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);

		var isNew = policy == null;
		
		if (isNew)
		{
			policy = new PasswordPolicy
			{
				Id = Guid.NewGuid(),
				OrganizationId = OrganizationId,
				CreatedAtUtc = DateTimeOffset.UtcNow
			};
		}
		else
		{
			// Restore if soft-deleted
			if (policy.IsDeleted)
			{
				policy.IsDeleted = false;
				policy.DeletedAtUtc = null;
				policy.DeletedBy = null;
			}
		}

		policy.MinLength = dto.MinLength;
		policy.MaxLength = dto.MaxLength;
		policy.RequireUppercase = dto.RequireUppercase;
		policy.RequireLowercase = dto.RequireLowercase;
		policy.RequireNumbers = dto.RequireNumbers;
		policy.RequireSpecialCharacters = dto.RequireSpecialCharacters;
		policy.MinSpecialCharacters = dto.MinSpecialCharacters;
		policy.MaxConsecutiveCharacters = dto.MaxConsecutiveCharacters;
		policy.PasswordHistoryCount = dto.PasswordHistoryCount;
		policy.MaxFailedAttempts = dto.MaxFailedAttempts;
		policy.LockoutDurationMinutes = dto.LockoutDurationMinutes;
		policy.PasswordExpiryDays = dto.PasswordExpiryDays;
		policy.RequirePasswordChangeOnFirstLogin = dto.RequirePasswordChangeOnFirstLogin;
		policy.IsActive = dto.IsActive;
		policy.LastModifiedAtUtc = DateTimeOffset.UtcNow;
		policy.ModifiedAtUtc = DateTimeOffset.UtcNow;

		if (isNew)
		{
			await policyRepo.AddAsync(policy);
		}
		else
		{
			policyRepo.Update(policy);
		}

		await _unitOfWork.SaveChangesAsync();

		return new PasswordPolicyDto
		{
			Id = policy.Id,
			MinLength = policy.MinLength,
			MaxLength = policy.MaxLength,
			RequireUppercase = policy.RequireUppercase,
			RequireLowercase = policy.RequireLowercase,
			RequireNumbers = policy.RequireNumbers,
			RequireSpecialCharacters = policy.RequireSpecialCharacters,
			MinSpecialCharacters = policy.MinSpecialCharacters,
			MaxConsecutiveCharacters = policy.MaxConsecutiveCharacters,
			PasswordHistoryCount = policy.PasswordHistoryCount,
			MaxFailedAttempts = policy.MaxFailedAttempts,
			LockoutDurationMinutes = policy.LockoutDurationMinutes,
			PasswordExpiryDays = policy.PasswordExpiryDays,
			RequirePasswordChangeOnFirstLogin = policy.RequirePasswordChangeOnFirstLogin,
			IsActive = policy.IsActive,
			CreatedAtUtc = policy.CreatedAtUtc,
			LastModifiedAtUtc = policy.LastModifiedAtUtc ?? DateTimeOffset.UtcNow
		};
	}

	public async Task<PasswordValidationResult> ValidatePasswordAsync(string password, Guid? userId = null, Guid? organizationId = null)
	{
		// Use provided organizationId or get from tenant context
		// This avoids duplicate GetPasswordPolicyAsync calls when organizationId is already known
		var policy = await GetPasswordPolicyAsync(organizationId);

		var result = new PasswordValidationResult
		{
			IsValid = true,
			Errors = new List<string>()
		};

		// Check length
		if (password.Length < policy.MinLength)
		{
			result.IsValid = false;
			result.Errors.Add($"Password must be at least {policy.MinLength} characters long");
		}

		if (password.Length > policy.MaxLength)
		{
			result.IsValid = false;
			result.Errors.Add($"Password must be no more than {policy.MaxLength} characters long");
		}

		// Check uppercase
		if (policy.RequireUppercase && !password.Any(char.IsUpper))
		{
			result.IsValid = false;
			result.Errors.Add("Password must contain at least one uppercase letter");
		}

		// Check lowercase
		if (policy.RequireLowercase && !password.Any(char.IsLower))
		{
			result.IsValid = false;
			result.Errors.Add("Password must contain at least one lowercase letter");
		}

		// Check numbers
		if (policy.RequireNumbers && !password.Any(char.IsDigit))
		{
			result.IsValid = false;
			result.Errors.Add("Password must contain at least one number");
		}

		// Check special characters
		if (policy.RequireSpecialCharacters)
		{
			var specialCharCount = password.Count(c => !char.IsLetterOrDigit(c));
			if (specialCharCount < policy.MinSpecialCharacters)
			{
				result.IsValid = false;
				result.Errors.Add($"Password must contain at least {policy.MinSpecialCharacters} special characters");
			}
		}

		// Check consecutive characters
		if (policy.MaxConsecutiveCharacters > 0)
		{
			var maxConsecutive = GetMaxConsecutiveCharacters(password);
			if (maxConsecutive > policy.MaxConsecutiveCharacters)
			{
				result.IsValid = false;
				result.Errors.Add($"Password cannot have more than {policy.MaxConsecutiveCharacters} consecutive identical characters");
			}
		}

		// Check common passwords
		if (IsCommonPassword(password))
		{
			result.IsValid = false;
			result.Errors.Add("Password is too common and not allowed");
		}

		// Check if password contains user info (only if userId is provided and policy requires it)
		if (userId.HasValue && policy.PreventUserInfoInPassword)
		{
			var containsUserInfo = await CheckUserInfoInPasswordAsync(userId.Value, password);
			if (containsUserInfo)
			{
				result.IsValid = false;
				result.Errors.Add("Password cannot contain your name, email, or phone number");
			}
		}

		return result;
	}

	public async Task<bool> CheckPasswordHistoryAsync(Guid userId, string newPassword)
	{
		// Get user's OrganizationId first (tenant context might not be available during password reset)
		var userRepo = _unitOfWork.Repository<User>();
		var userData = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => u.Id == userId && !u.IsDeleted)
			.Select(u => new { u.OrganizationId })
			.FirstOrDefaultAsync();
		
		if (userData == null)
		{
			return false; // User not found, can't check history
		}
		
		var OrganizationId = userData.OrganizationId;
		
		// Pass OrganizationId directly to GetPasswordPolicyAsync to avoid tenant context dependency
		var policy = await GetPasswordPolicyAsync(OrganizationId);
		
		// If password history is disabled, don't check
		if (policy.PasswordHistoryCount <= 0)
		{
			return false;
		}
		
		var logRepo = _unitOfWork.Repository<UserActivityLog>();
		
		// Use GetQueryable().IgnoreQueryFilters() to bypass query filters
		// This ensures we can access all logs even if query filters are applied
		// Explicitly filter by OrganizationId, UserId, Action, and IsDeleted
		var historyLogs = await logRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(x => 
				x.OrganizationId == OrganizationId && 
				x.UserId == userId && 
				x.Action == "PASSWORD_CHANGED" &&
				!x.IsDeleted)
			.OrderByDescending(x => x.Timestamp)
			.Take(policy.PasswordHistoryCount)
			.ToListAsync();
		
		// historyLogs already contains the most recent N password changes
		var recent = historyLogs;
		
		// Check if the new password matches any of the historical passwords
		foreach (var log in recent)
		{
			var storedHash = log.Details; // Old password hash is stored in Details field
			if (!string.IsNullOrEmpty(storedHash) && BCrypt.Net.BCrypt.Verify(newPassword, storedHash))
			{
				return true; // Password matches one of the historical passwords
			}
		}
		
		return false; // Password is not in history
	}

	public async Task<bool> IsPasswordExpiredAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var policy = await GetPasswordPolicyAsync();
		var userRepo = _unitOfWork.Repository<User>();
		
		var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		if (user == null) return false;

		if (user.PasswordChangedAt == null) return true;

		var daysSinceChange = (DateTimeOffset.UtcNow - user.PasswordChangedAt.Value).Days;
		return daysSinceChange >= policy.PasswordExpiryDays;
	}

	public async Task<bool> IsAccountLockedAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var userRepo = _unitOfWork.Repository<User>();
		
		var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		if (user == null) return false;

		return user.LockedUntil.HasValue && user.LockedUntil > DateTimeOffset.UtcNow;
	}

	public async Task<int> GetRemainingAttemptsAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var policy = await GetPasswordPolicyAsync();
		var userRepo = _unitOfWork.Repository<User>();
		
		var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		if (user == null) return 0;

		return Math.Max(0, policy.MaxFailedAttempts - user.FailedLoginAttempts);
	}

	public async Task<bool> RecordFailedAttemptAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var policy = await GetPasswordPolicyAsync();
		var userRepo = _unitOfWork.Repository<User>();
		
		var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		if (user == null) return false;

		user.FailedLoginAttempts++;

		if (user.FailedLoginAttempts >= policy.MaxFailedAttempts)
		{
			user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(policy.LockoutDurationMinutes);
		}

		userRepo.Update(user);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<bool> ResetFailedAttemptsAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var userRepo = _unitOfWork.Repository<User>();
		
		var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		if (user == null) return false;

		user.FailedLoginAttempts = 0;
		user.LockedUntil = null;

		userRepo.Update(user);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<PasswordStrengthDto> GetPasswordStrengthAsync(string password)
	{
		var policy = await GetPasswordPolicyAsync();
		var strength = CalculatePasswordStrength(password, policy);

		return new PasswordStrengthDto
		{
			Score = strength.Score,
			Level = strength.Level,
			Feedback = strength.Feedback,
			Suggestions = strength.Suggestions
		};
	}

	private int GetMaxConsecutiveCharacters(string password)
	{
		if (string.IsNullOrEmpty(password)) return 0;

		int maxConsecutive = 1;
		int currentConsecutive = 1;

		for (int i = 1; i < password.Length; i++)
		{
			if (password[i] == password[i - 1])
			{
				currentConsecutive++;
			}
			else
			{
				maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
				currentConsecutive = 1;
			}
		}

		return Math.Max(maxConsecutive, currentConsecutive);
	}

	private bool IsCommonPassword(string password)
	{
		var commonPasswords = new[]
		{
			"password", "123456", "123456789", "qwerty", "abc123",
			"password123", "admin", "letmein", "welcome", "monkey"
		};

		return commonPasswords.Contains(password.ToLower());
	}

	private (int Score, string Level, List<string> Feedback, List<string> Suggestions) CalculatePasswordStrength(string password, PasswordPolicyDto policy)
	{
		var score = 0;
		var feedback = new List<string>();
		var suggestions = new List<string>();

		// Length scoring
		if (password.Length >= 8) score += 1;
		if (password.Length >= 12) score += 1;
		if (password.Length >= 16) score += 1;

		// Character variety scoring
		if (password.Any(char.IsLower)) score += 1;
		if (password.Any(char.IsUpper)) score += 1;
		if (password.Any(char.IsDigit)) score += 1;
		if (password.Any(c => !char.IsLetterOrDigit(c))) score += 1;

		// Complexity scoring
		var uniqueChars = password.Distinct().Count();
		if (uniqueChars >= password.Length * 0.7) score += 1;

		// Determine level and feedback
		string level;
		if (score < 3)
		{
			level = "Weak";
			feedback.Add("Password is too weak");
			suggestions.Add("Use a longer password");
			suggestions.Add("Include uppercase and lowercase letters");
			suggestions.Add("Add numbers and special characters");
		}
		else if (score < 5)
		{
			level = "Fair";
			feedback.Add("Password strength is fair");
			suggestions.Add("Consider adding more variety");
		}
		else if (score < 7)
		{
			level = "Good";
			feedback.Add("Password strength is good");
		}
		else
		{
			level = "Strong";
			feedback.Add("Password strength is strong");
		}

		return (score, level, feedback, suggestions);
	}

	public async Task<bool> IsPasswordInHistoryAsync(Guid userId, string password)
	{
		return await CheckPasswordHistoryAsync(userId, password);
	}

	public async Task<List<string>> GetPasswordHistoryAsync(Guid userId)
	{
		// Get user's OrganizationId first (tenant context might not be available)
		var userRepo = _unitOfWork.Repository<User>();
		var userData = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => u.Id == userId && !u.IsDeleted)
			.Select(u => new { u.OrganizationId })
			.FirstOrDefaultAsync();
		
		if (userData == null)
		{
			return new List<string>(); // User not found, return empty list
		}
		
		var OrganizationId = userData.OrganizationId;
		var logRepo = _unitOfWork.Repository<UserActivityLog>();
		
		// Use GetQueryable().IgnoreQueryFilters() to bypass query filters
		var logs = await logRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(x => 
				x.OrganizationId == OrganizationId && 
				x.UserId == userId && 
				x.Action == "PASSWORD_CHANGED" &&
				!x.IsDeleted)
			.OrderByDescending(x => x.Timestamp)
			.Select(x => x.Details ?? "")
			.ToListAsync();
		
		return logs;
	}

	public async Task<bool> CheckPasswordComplexityAsync(string password)
	{
		var result = await ValidatePasswordAsync(password);
		return result.IsValid;
	}

	public async Task<bool> CheckPasswordHistoryAsync(Guid userId, string password, int historyCount = 5)
	{
		if (historyCount <= 0)
		{
			return false;
		}
		
		// Get user's OrganizationId first (tenant context might not be available during password reset)
		var userRepo = _unitOfWork.Repository<User>();
		var userData = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => u.Id == userId && !u.IsDeleted)
			.Select(u => new { u.OrganizationId })
			.FirstOrDefaultAsync();
		
		if (userData == null)
		{
			return false; // User not found, can't check history
		}
		
		var OrganizationId = userData.OrganizationId;
		var logRepo = _unitOfWork.Repository<UserActivityLog>();
		
		// Use GetQueryable().IgnoreQueryFilters() to bypass query filters
		var recent = await logRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(x => 
				x.OrganizationId == OrganizationId && 
				x.UserId == userId && 
				x.Action == "PASSWORD_CHANGED" &&
				!x.IsDeleted)
			.OrderByDescending(x => x.Timestamp)
			.Take(historyCount)
			.ToListAsync();
		
		foreach (var log in recent)
		{
			var storedHash = log.Details;
			if (!string.IsNullOrEmpty(storedHash) && BCrypt.Net.BCrypt.Verify(password, storedHash))
			{
				return true;
			}
		}
		return false;
	}

	public async Task<bool> CheckCommonPasswordsAsync(string password)
	{
		return !IsCommonPassword(password);
	}

	public async Task<bool> CheckUserInfoInPasswordAsync(Guid userId, string password)
	{
		// Get user's OrganizationId first (tenant context might not be available)
		var userRepo = _unitOfWork.Repository<User>();
		var userData = await userRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(u => u.Id == userId && !u.IsDeleted)
			.Select(u => new { u.OrganizationId, u.FirstName, u.LastName, u.Email, u.PhoneNumber })
			.FirstOrDefaultAsync();

		if (userData == null) return false;

		var userInfo = new[] { userData.FirstName, userData.LastName, userData.Email, userData.PhoneNumber }
			.Where(x => !string.IsNullOrEmpty(x))
			.ToList();

		return userInfo.Any(info => password.Contains(info, StringComparison.OrdinalIgnoreCase));
	}
}
