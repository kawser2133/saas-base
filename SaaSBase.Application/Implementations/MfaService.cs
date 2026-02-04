using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace SaaSBase.Application.Implementations;

public class MfaService : IMfaService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
	private readonly ICurrentUserService _currentUserService;
	private readonly IUserContextService _userContextService;
	private readonly IEmailService _emailService;
	private readonly ISmsService _smsService;
	private readonly IImportExportService _importExportService;
	private readonly IServiceScopeFactory _serviceScopeFactory;

	public MfaService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, ICurrentUserService currentUserService, IUserContextService userContextService, IEmailService emailService, ISmsService smsService, IImportExportService importExportService, IServiceScopeFactory serviceScopeFactory)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_currentUserService = currentUserService;
		_userContextService = userContextService;
		_emailService = emailService;
		_smsService = smsService;
		_importExportService = importExportService;
		_serviceScopeFactory = serviceScopeFactory;
	}

	public async Task<MfaSetupDto> SetupMfaAsync(Guid userId, string mfaType)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var userRepo = _unitOfWork.Repository<User>();
		var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);

		if (user == null) throw new ArgumentException("User not found");

		var setupDto = new MfaSetupDto
		{
			UserId = userId,
			MfaType = mfaType,
			SecretKey = GenerateSecretKey(),
			QrCodeUrl = GenerateQrCodeUrl(user.Email, GenerateSecretKey()),
			BackupCodes = GenerateBackupCodes()
		};

		return setupDto;
	}

	public async Task<bool> VerifyMfaCodeAsync(Guid userId, string code, string mfaType)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		string normalizedMfaType = mfaType.ToUpper(); // Normalize to uppercase for consistency
		
		// If tenant context is not set (e.g., during login), get user's organization
		if (OrganizationId == null || OrganizationId == Guid.Empty)
		{
			var userRepo = _unitOfWork.Repository<User>();
			var userOrgId = await userRepo.GetQueryable()
				.IgnoreQueryFilters()
				.Where(x => x.Id == userId && !x.IsDeleted)
				.Select(x => x.OrganizationId)
				.FirstOrDefaultAsync();
			
			if (userOrgId != Guid.Empty)
			{
				OrganizationId = userOrgId;
				// Set tenant context for subsequent operations
				_tenantService.SetBackgroundContext(OrganizationId, userId);
			}
			else
			{
				return false; // User not found
			}
		}
		
		// For SMS and Email: verify against most recent sent code (valid for 10 minutes)
		if (normalizedMfaType == "SMS" || normalizedMfaType == "EMAIL")
		{
			var attemptRepo = _unitOfWork.Repository<MfaAttempt>();
			var recentAttempt = await attemptRepo.GetQueryable()
				.IgnoreQueryFilters() // Need to bypass tenant filter if called during login
				.Where(a => a.UserId == userId && 
					a.OrganizationId == OrganizationId && 
					a.MfaType == normalizedMfaType && // Use normalized type
					a.Code == code &&
					a.AttemptedAt >= DateTimeOffset.UtcNow.AddMinutes(-10) && // Code valid for 10 minutes
					!a.IsSuccessful) // Not already used
				.OrderByDescending(a => a.AttemptedAt)
				.FirstOrDefaultAsync();

			if (recentAttempt != null)
			{
				// Mark as successful
				recentAttempt.IsSuccessful = true;
				attemptRepo.Update(recentAttempt);
				
				// Update LastUsedAt in MfaSettings
				var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
				var mfaSetting = await mfaSettingsRepo.GetQueryable()
					.IgnoreQueryFilters()
					.FirstOrDefaultAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType == normalizedMfaType && x.IsActive);
				
				if (mfaSetting != null)
				{
					mfaSetting.LastUsedAt = DateTimeOffset.UtcNow;
					mfaSettingsRepo.Update(mfaSetting);
				}
				
				await _unitOfWork.SaveChangesAsync();
				return true;
			}
			return false;
		}

		// For TOTP: verify using secret key
		if (mfaType.Equals("TOTP", StringComparison.OrdinalIgnoreCase))
		{
			var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
			var settings = await mfaSettingsRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType == mfaType && x.IsActive);
			if (settings != null && !string.IsNullOrEmpty(settings.SecretKey))
			{
				return VerifyTotpCode(settings.SecretKey, code);
			}
			return false;
		}

		// For BackupCode: verify against stored backup codes
		if (mfaType.Equals("BackupCode", StringComparison.OrdinalIgnoreCase))
		{
			return await VerifyBackupCodeAsync(userId, code);
		}

		return false;
	}

	public async Task<bool> SendMfaCodeAsync(Guid userId, string mfaType)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var userRepo = _unitOfWork.Repository<User>();
		
		// If tenant context is not set (e.g., during login), find user without tenant filter
		User? user;
		if (OrganizationId == null || OrganizationId == Guid.Empty)
		{
			user = await userRepo.GetQueryable()
				.IgnoreQueryFilters()
				.FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted);
			
			if (user != null)
			{
				OrganizationId = user.OrganizationId;
				// Set tenant context for subsequent operations
				_tenantService.SetBackgroundContext(OrganizationId, userId);
			}
		}
		else
		{
			user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		}

		if (user == null) throw new ArgumentException("User not found");

		string normalizedMfaType = mfaType.ToUpper(); // Normalize to uppercase for consistency
		
		// BackupCode cannot receive codes - they are pre-generated
		if (normalizedMfaType == "BACKUPCODE")
		{
			throw new ArgumentException("Backup codes are pre-generated and cannot be sent. Use a previously generated backup code.");
		}

		var code = GenerateRandomCode(6);
		bool sent = false;
		
		switch (normalizedMfaType)
		{
			case "EMAIL":
				sent = await _emailService.SendMfaCodeEmailAsync(user.Email, code);
				break;
			case "SMS":
				if (string.IsNullOrEmpty(user.PhoneNumber))
					throw new ArgumentException("Phone number not found for SMS MFA");
				sent = await _smsService.SendMfaCodeSmsAsync(user.PhoneNumber, code);
				break;
			default:
				throw new ArgumentException($"Unsupported MFA type: {mfaType}. Only SMS and Email codes can be sent.");
		}

		// Store the code in MfaAttempt for verification (valid for 10 minutes)
		if (sent)
		{
			var attemptRepo = _unitOfWork.Repository<MfaAttempt>();
			var attempt = new MfaAttempt
			{
				Id = Guid.NewGuid(),
				OrganizationId = OrganizationId,
				UserId = userId,
				MfaType = normalizedMfaType, // Store in uppercase for consistency
				Code = code, // Store the code for verification
				IsSuccessful = false, // Will be updated when verified
				AttemptedAt = DateTimeOffset.UtcNow,
				IpAddress = _currentUserService.GetCurrentUserIpAddress() ?? "",
				UserAgent = _currentUserService.GetCurrentUserAgent() ?? ""
			};
			await attemptRepo.AddAsync(attempt);
			await _unitOfWork.SaveChangesAsync();
		}

		return sent;
	}

	private string GenerateRandomCode(int length)
	{
		const string chars = "0123456789";
		var random = new Random();
		return new string(Enumerable.Repeat(chars, length)
			.Select(s => s[random.Next(s.Length)]).ToArray());
	}

	public async Task<List<MfaSettingsDto>> GetUserMfaSettingsAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		var settings = await mfaSettingsRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId);

		return settings.Select(x => new MfaSettingsDto
		{
			Id = x.Id,
			UserId = x.UserId,
			MfaType = x.MfaType,
			IsActive = x.IsActive,
			IsDefault = x.IsDefault,
			CreatedAtUtc = x.CreatedAtUtc
		}).ToList();
	}

	public async Task<UserMfaSettingsSummaryDto> GetUserMfaSettingsSummaryAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		var userRepo = _unitOfWork.Repository<User>();
		
		// If tenant context is not set (e.g., during login), find user without tenant filter
		User? user;
		if (OrganizationId == null || OrganizationId == Guid.Empty)
		{
			user = await userRepo.GetQueryable()
				.IgnoreQueryFilters()
				.FirstOrDefaultAsync(x => x.Id == userId && !x.IsDeleted);
			
			if (user != null)
			{
				OrganizationId = user.OrganizationId;
				// Set tenant context for subsequent operations
				_tenantService.SetBackgroundContext(OrganizationId, userId);
			}
		}
		else
		{
			user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		}
		
		if (user == null)
		{
			return new UserMfaSettingsSummaryDto();
		}

		// Get active MFA settings (IsActive = true means the method is enabled)
		var settings = await mfaSettingsRepo.GetQueryable()
			.IgnoreQueryFilters() // Need to bypass tenant filter if called during login
			.Where(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsActive)
			.ToListAsync();
		
		// For backward compatibility: include settings where IsActive = true (even if IsEnabled is false for old records)
		// New records will have both IsActive = true and IsEnabled = true
		var enabledMethods = settings.Select(x => x.MfaType).ToList();
		
		// Mask phone number
		string? phoneNumberMasked = null;
		if (!string.IsNullOrEmpty(user.PhoneNumber))
		{
			var phone = user.PhoneNumber;
			if (phone.Length > 4)
			{
				phoneNumberMasked = $"***-***-{phone.Substring(phone.Length - 4)}";
			}
			else
			{
				phoneNumberMasked = "***";
			}
		}

		// Mask email
		string? emailMasked = null;
		if (!string.IsNullOrEmpty(user.Email))
		{
			var email = user.Email;
			var parts = email.Split('@');
			if (parts.Length == 2)
			{
				var localPart = parts[0];
				var domain = parts[1];
				if (localPart.Length > 2)
				{
					emailMasked = $"{localPart.Substring(0, 2)}***@{domain}";
				}
				else
				{
					emailMasked = $"***@{domain}";
				}
			}
			else
			{
				emailMasked = "***";
			}
		}

		// Get the default method
		var defaultSetting = settings.FirstOrDefault(x => x.IsDefault);
		string? defaultMethod = defaultSetting?.MfaType;

		return new UserMfaSettingsSummaryDto
		{
			IsMfaEnabled = user.IsMfaEnabled,
			EnabledMethods = enabledMethods,
			DefaultMethod = defaultMethod,
			PhoneNumberMasked = phoneNumberMasked,
			EmailMasked = emailMasked
		};
	}

	public async Task<bool> EnableMfaAsync(Guid userId, string mfaType, string code)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		var userRepo = _unitOfWork.Repository<User>();
		string normalizedMfaType = mfaType.ToUpper(); // Normalize to uppercase for consistency
		
		// Check if there's already an ACTIVE setting (not just any setting)
		var existingActiveSettings = await mfaSettingsRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType == normalizedMfaType && x.IsActive);
		if (existingActiveSettings != null) return false; // Already enabled

		// Verify the code first
		if (!await VerifyMfaCodeAsync(userId, code, normalizedMfaType)) return false;

		// Check if there are any other active MFA methods
		var hasOtherActiveMethods = await mfaSettingsRepo.ExistsAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType != normalizedMfaType && x.IsActive);
		bool shouldSetAsDefault = !hasOtherActiveMethods; // Set as default if this is the first/only method
		
		// Check if there's an existing but inactive setting (from previous disable)
		var existingInactiveSettings = await mfaSettingsRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType == normalizedMfaType && !x.IsActive);
		
		if (existingInactiveSettings != null)
		{
			// Reactivate the existing setting
			existingInactiveSettings.IsActive = true;
			existingInactiveSettings.IsEnabled = true;
			existingInactiveSettings.SecretKey = GenerateSecretKey(); // Generate new secret key
			
			// If this should be default and there's currently a default, remove it
			if (shouldSetAsDefault)
			{
				var currentDefault = await mfaSettingsRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsDefault && x.IsActive);
				if (currentDefault != null)
				{
					currentDefault.IsDefault = false;
					mfaSettingsRepo.Update(currentDefault);
				}
				existingInactiveSettings.IsDefault = true;
			}
			
			mfaSettingsRepo.Update(existingInactiveSettings);
		}
		else
		{
			// If this should be default and there's currently a default, remove it
			if (shouldSetAsDefault)
			{
				var currentDefault = await mfaSettingsRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsDefault && x.IsActive);
				if (currentDefault != null)
				{
					currentDefault.IsDefault = false;
					mfaSettingsRepo.Update(currentDefault);
				}
			}
			
			// Create new setting
			var settings = new MfaSettings
			{
				Id = Guid.NewGuid(),
				OrganizationId = OrganizationId,
				UserId = userId,
				MfaType = normalizedMfaType, // Store in uppercase
				SecretKey = GenerateSecretKey(),
				IsActive = true,
				IsEnabled = true, // Set IsEnabled flag to true
				IsDefault = shouldSetAsDefault
			};

			await mfaSettingsRepo.AddAsync(settings);
		}

		// Update user MFA status
		var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
		if (user != null)
		{
			user.IsMfaEnabled = true;
			userRepo.Update(user);
		}

		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<bool> DisableMfaAsync(Guid userId, string mfaType)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		var userRepo = _unitOfWork.Repository<User>();
		string normalizedMfaType = mfaType.ToUpper(); // Normalize to uppercase for consistency
		
		var settings = await mfaSettingsRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType == normalizedMfaType);
		if (settings == null) return false;

		// Check if there are other MFA methods enabled
		var hasOtherMfa = await mfaSettingsRepo.ExistsAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType != normalizedMfaType && x.IsActive);
		
		settings.IsActive = false;
		settings.IsEnabled = false; // Also set IsEnabled to false
		mfaSettingsRepo.Update(settings);

		// Update user MFA status if no other methods are active
		if (!hasOtherMfa)
		{
			var user = await userRepo.FindAsync(x => x.Id == userId && x.OrganizationId == OrganizationId);
			if (user != null)
			{
				user.IsMfaEnabled = false;
				userRepo.Update(user);
			}
		}

		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<bool> SetDefaultMfaAsync(Guid userId, string mfaType)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		string normalizedMfaType = mfaType.ToUpper(); // Normalize to uppercase for consistency
		
		var allActiveSettings = await mfaSettingsRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsActive);
		var targetSetting = allActiveSettings.FirstOrDefault(x => x.MfaType.Equals(normalizedMfaType, StringComparison.OrdinalIgnoreCase));
		
		if (targetSetting == null)
		{
			return false; // Method not found or not active
		}
		
		// Remove default flag from all active settings
		foreach (var setting in allActiveSettings)
		{
			setting.IsDefault = false;
			mfaSettingsRepo.Update(setting);
		}
		
		// Set the selected method as default
		targetSetting.IsDefault = true;
		mfaSettingsRepo.Update(targetSetting);
		
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<List<string>> GenerateBackupCodesAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		
		var codes = GenerateBackupCodes();
		
		// Store backup codes (simplified implementation)
		var settings = await mfaSettingsRepo.FindAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType == "BackupCode");
		if (settings == null)
		{
			settings = new MfaSettings
			{
				Id = Guid.NewGuid(),
				OrganizationId = OrganizationId,
				UserId = userId,
				MfaType = "BackupCode",
				SecretKey = string.Join(",", codes),
				IsActive = true,
				IsDefault = false
			};
			await mfaSettingsRepo.AddAsync(settings);
		}
		else
		{
			settings.SecretKey = string.Join(",", codes);
			mfaSettingsRepo.Update(settings);
		}

		await _unitOfWork.SaveChangesAsync();
		return codes;
	}

	public async Task<bool> VerifyBackupCodeAsync(Guid userId, string code, string? ipAddress = null, string? userAgent = null)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		
		// If tenant context is not set (e.g., during login), get user's organization
		if (OrganizationId == null || OrganizationId == Guid.Empty)
		{
			var userRepo = _unitOfWork.Repository<User>();
			var userOrgId = await userRepo.GetQueryable()
				.IgnoreQueryFilters()
				.Where(x => x.Id == userId && !x.IsDeleted)
				.Select(x => x.OrganizationId)
				.FirstOrDefaultAsync();
			
			if (userOrgId != Guid.Empty)
			{
				OrganizationId = userOrgId;
				// Set tenant context for subsequent operations
				_tenantService.SetBackgroundContext(OrganizationId, userId);
			}
			else
			{
				return false; // User not found
			}
		}
		
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		var settings = await mfaSettingsRepo.GetQueryable()
			.IgnoreQueryFilters() // Need to bypass tenant filter if called during login
			.FirstOrDefaultAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.MfaType == "BackupCode" && x.IsActive);

		if (settings == null || string.IsNullOrEmpty(settings.SecretKey)) return false;

		var backupCodes = settings.SecretKey.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
		if (!backupCodes.Contains(code)) return false;

		// Remove the used code
		backupCodes.Remove(code);
		settings.SecretKey = backupCodes.Count > 0 ? string.Join(",", backupCodes) : null;
		
		// Update LastUsedAt
		settings.LastUsedAt = DateTimeOffset.UtcNow;
		
		// If no codes left, deactivate backup codes
		if (backupCodes.Count == 0)
		{
			settings.IsActive = false;
			settings.IsEnabled = false; // Also set IsEnabled to false
		}
		
		mfaSettingsRepo.Update(settings);
		
		// Log MfaAttempt for BackupCode
		var attemptRepo = _unitOfWork.Repository<MfaAttempt>();
		
		// Use provided IP/UserAgent (from login DTO) or get from current user service
		var finalIpAddress = ipAddress ?? _currentUserService.GetCurrentUserIpAddress() ?? "";
		var finalUserAgent = userAgent ?? _currentUserService.GetCurrentUserAgent() ?? "";
		
		var attempt = new MfaAttempt
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			UserId = userId,
			MfaType = "BACKUPCODE", // Store in uppercase for consistency
			Code = code,
			IsSuccessful = true,
			AttemptedAt = DateTimeOffset.UtcNow,
			IpAddress = finalIpAddress,
			UserAgent = finalUserAgent
		};
		await attemptRepo.AddAsync(attempt);
		
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<bool> IsMfaEnabledAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		return await mfaSettingsRepo.ExistsAsync(x => x.UserId == userId && x.OrganizationId == OrganizationId && x.IsActive);
	}

	public async Task<MfaAttemptDto> LogMfaAttemptAsync(Guid userId, string mfaType, string code, bool isSuccessful, string? failureReason = null)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var attemptRepo = _unitOfWork.Repository<MfaAttempt>();
		
		var attempt = new MfaAttempt
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			UserId = userId,
			MfaType = mfaType,
			Code = code,
			IsSuccessful = isSuccessful,
			FailureReason = failureReason,
			AttemptedAt = DateTimeOffset.UtcNow,
			IpAddress = _currentUserService.GetCurrentUserIpAddress() ?? "",
			UserAgent = _currentUserService.GetCurrentUserAgent() ?? ""
		};

		await attemptRepo.AddAsync(attempt);
		await _unitOfWork.SaveChangesAsync();

		return new MfaAttemptDto
		{
			Id = attempt.Id,
			UserId = attempt.UserId,
			MfaType = attempt.MfaType,
			IsSuccessful = attempt.IsSuccessful,
			FailureReason = attempt.FailureReason,
			AttemptedAt = attempt.AttemptedAt,
			IpAddress = attempt.IpAddress,
			UserAgent = attempt.UserAgent
		};
	}

	private string GenerateSecretKey()
	{
		using var rng = RandomNumberGenerator.Create();
		var bytes = new byte[32];
		rng.GetBytes(bytes);
		return Convert.ToBase64String(bytes);
	}

	private string GenerateQrCodeUrl(string email, string secretKey)
	{
		var issuer = Uri.EscapeDataString("SaaSBase");
		var label = Uri.EscapeDataString(email);
		var secret = Uri.EscapeDataString(secretKey);
		// TOTP defaults: algorithm=SHA1, digits=6, period=30
		return $"otpauth://totp/{issuer}:{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
	}

	private static bool VerifyTotpCode(string base32Secret, string code)
	{
		// Accept codes in a small time window to account for clock skew
		var timeStep = 30; // seconds
		var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		for (int offset = -1; offset <= 1; offset++)
		{
			var counter = (unixTime / timeStep) + offset;
			var expected = GenerateTotp(base32Secret, counter);
			if (string.Equals(expected, code, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static string GenerateTotp(string base32Secret, long counter)
	{
		var key = Base32Decode(base32Secret);
		var counterBytes = BitConverter.GetBytes(counter);
		if (BitConverter.IsLittleEndian)
		{
			Array.Reverse(counterBytes);
		}
		using var hmac = new System.Security.Cryptography.HMACSHA1(key);
		var hash = hmac.ComputeHash(counterBytes);
		int offset = hash[^1] & 0x0F;
		int binaryCode = ((hash[offset] & 0x7F) << 24)
			| ((hash[offset + 1] & 0xFF) << 16)
			| ((hash[offset + 2] & 0xFF) << 8)
			| (hash[offset + 3] & 0xFF);
		int otp = binaryCode % (int)Math.Pow(10, 6);
		return otp.ToString("D6");
	}

	private static byte[] Base32Decode(string input)
	{
		const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
		var paddingChar = '=';
		input = input.TrimEnd(paddingChar).Replace(" ", string.Empty).ToUpperInvariant();
		var outputBytes = new List<byte>();
		int buffer = 0;
		int bitsLeft = 0;
		foreach (char c in input)
		{
			int val = alphabet.IndexOf(c);
			if (val < 0) continue;
			buffer = (buffer << 5) | val;
			bitsLeft += 5;
			if (bitsLeft >= 8)
			{
				bitsLeft -= 8;
				outputBytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
			}
		}
		return outputBytes.ToArray();
	}

	private List<string> GenerateBackupCodes()
	{
		var codes = new List<string>();
		for (int i = 0; i < 10; i++)
		{
			codes.Add(Guid.NewGuid().ToString("N")[..8].ToUpper());
		}
		return codes;
	}

	public async Task<PagedResultDto<UserMfaSettingsDto>> GetOrganizationMfaSettingsAsync(int page, int pageSize, Guid? organizationId = null, string? search = null, string? sortField = null, string? sortDirection = null)
	{
		var currentOrganizationId = _tenantService.GetOrganizationId();
		// Use UserContextService to properly check if user is System Administrator
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		// Determine which organization to filter by
		Guid filterOrganizationId;
		if (isSystemAdmin && organizationId.HasValue)
		{
			// System Admin can filter by any organization
			filterOrganizationId = organizationId.Value;
		}
		else
		{
			// Regular users (including Company Admin) can only see their own organization
			// Ignore any provided organizationId for non-System Admin users
			filterOrganizationId = currentOrganizationId;
		}
		
		var mfaSettingsRepo = _unitOfWork.Repository<MfaSettings>();
		
		// Build query with proper filtering
		IQueryable<MfaSettings> query;
		if (isSystemAdmin && !organizationId.HasValue)
		{
			// System Admin without filter - show all organizations
			query = mfaSettingsRepo.GetQueryable()
				.IgnoreQueryFilters()
				.Where(x => x.IsActive);
		}
		else
		{
			// Filter by specific organization
			query = mfaSettingsRepo.GetQueryable()
				.IgnoreQueryFilters()
				.Where(x => x.OrganizationId == filterOrganizationId && x.IsActive);
		}
		
		var all = await query.ToListAsync();

		var userRepo = _unitOfWork.Repository<User>();
		var orgRepo = _unitOfWork.Repository<Organization>();
		var userIdsAll = all.Select(s => s.UserId).Distinct().ToList();
		var orgIdsAll = all.Select(s => s.OrganizationId).Distinct().ToList();
		
		// For System Admin viewing cross-organization data, use IgnoreQueryFilters
		IQueryable<User> usersQuery;
		if (isSystemAdmin && !organizationId.HasValue)
		{
			usersQuery = userRepo.GetQueryable().IgnoreQueryFilters().Where(u => userIdsAll.Contains(u.Id) && !u.IsDeleted);
		}
		else
		{
			usersQuery = userRepo.GetQueryable().IgnoreQueryFilters().Where(u => userIdsAll.Contains(u.Id) && u.OrganizationId == filterOrganizationId && !u.IsDeleted);
		}
		var usersAll = await usersQuery.ToListAsync();
		var emailMapAll = usersAll.ToDictionary(u => u.Id, u => u.Email);
		
		// Get organization names
		var orgsAll = await orgRepo.GetQueryable()
			.IgnoreQueryFilters()
			.Where(o => orgIdsAll.Contains(o.Id) && !o.IsDeleted)
			.Select(o => new { o.Id, o.Name })
			.ToListAsync();
		var orgNameMap = orgsAll.ToDictionary(o => o.Id, o => o.Name);

		var dtos = all.Select(x => new UserMfaSettingsDto
		{
			Id = x.Id,
			UserId = x.UserId,
			UserEmail = emailMapAll.TryGetValue(x.UserId, out var em) ? em : string.Empty,
			MfaType = x.MfaType,
			IsActive = x.IsActive,
			IsDefault = x.IsDefault,
			LastUsedAt = x.LastUsedAt,
			CreatedAtUtc = x.CreatedAtUtc,
			OrganizationId = x.OrganizationId,
			OrganizationName = orgNameMap.TryGetValue(x.OrganizationId, out var orgName) ? orgName : null
		}).ToList();

		if (!string.IsNullOrWhiteSpace(search))
		{
			var q = search.Trim().ToLowerInvariant();
			dtos = dtos.Where(x =>
				(x.UserEmail ?? string.Empty).ToLower().Contains(q) ||
				(x.MfaType ?? string.Empty).ToLower().Contains(q) ||
				(x.OrganizationName ?? string.Empty).ToLower().Contains(q)
			).ToList();
		}

		bool desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
		dtos = (sortField?.ToLowerInvariant()) switch
		{
			"useremail" => (desc ? dtos.OrderByDescending(x => x.UserEmail) : dtos.OrderBy(x => x.UserEmail)).ToList(),
			"mfatype" => (desc ? dtos.OrderByDescending(x => x.MfaType) : dtos.OrderBy(x => x.MfaType)).ToList(),
			"isactive" or "status" => (desc ? dtos.OrderByDescending(x => x.IsActive) : dtos.OrderBy(x => x.IsActive)).ToList(),
			"isdefault" => (desc ? dtos.OrderByDescending(x => x.IsDefault) : dtos.OrderBy(x => x.IsDefault)).ToList(),
			"organizationname" or "organizationid" => (desc ? dtos.OrderByDescending(x => x.OrganizationName ?? string.Empty) : dtos.OrderBy(x => x.OrganizationName ?? string.Empty)).ToList(),
			"lastusedat" => (desc ? dtos.OrderByDescending(x => x.LastUsedAt) : dtos.OrderBy(x => x.LastUsedAt)).ToList(),
			"createdatutc" => (desc ? dtos.OrderByDescending(x => x.CreatedAtUtc) : dtos.OrderBy(x => x.CreatedAtUtc)).ToList(),
			_ => dtos.OrderByDescending(x => x.CreatedAtUtc).ToList()
		};

		var total = dtos.LongCount();
		var items = dtos
			.Skip(Math.Max(0, (page - 1) * pageSize))
			.Take(Math.Max(1, pageSize))
			.ToList();

		return new PagedResultDto<UserMfaSettingsDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = total,
			Items = items
		};
	}

	// Async Export (non-blocking)
	public async Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var organizationId = _tenantService.GetOrganizationId();

		// Add organization context to filters
		filters["organizationId"] = organizationId;

		return await _importExportService.StartExportJobAsync<UserMfaSettingsDto>(
			entityType: "MfaSettings",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				
				// CRITICAL: Set background context to ensure DbContext global query filter works correctly
				var orgId = organizationId; // Use captured value from outer scope
				scopedTenantService.SetBackgroundContext(orgId, Guid.Empty, null);
				
				var scopedMfaSettingsRepo = scopedUnitOfWork.Repository<MfaSettings>();
				var scopedUserRepo = scopedUnitOfWork.Repository<User>();
				
				var s = f.GetValueOrDefault("search")?.ToString();
				var sf = f.GetValueOrDefault("sortField")?.ToString();
				var sd = f.GetValueOrDefault("sortDirection")?.ToString();
				var selectedIds = f.GetValueOrDefault("selectedIds") as List<string>;

				// Get all active MFA settings for the organization
				var all = await scopedMfaSettingsRepo.FindManyAsync(x => x.OrganizationId == orgId && x.IsActive);
				var mfaSettings = all.ToList();

				// Filter by selectedIds if provided
				if (selectedIds != null && selectedIds.Any())
				{
					mfaSettings = mfaSettings.Where(x => selectedIds.Contains(x.Id.ToString())).ToList();
				}

				var userIdsAll = mfaSettings.Select(s => s.UserId).Distinct().ToList();
				var usersAll = await scopedUserRepo.FindManyAsync(u => userIdsAll.Contains(u.Id) && u.OrganizationId == orgId && !u.IsDeleted);
				var emailMapAll = usersAll.ToDictionary(u => u.Id, u => u.Email);

				var dtos = mfaSettings.Select(x => new UserMfaSettingsDto
				{
					Id = x.Id,
					UserId = x.UserId,
					UserEmail = emailMapAll.TryGetValue(x.UserId, out var em) ? em : string.Empty,
					MfaType = x.MfaType,
					IsActive = x.IsActive,
					IsDefault = x.IsDefault,
					LastUsedAt = x.LastUsedAt,
					CreatedAtUtc = x.CreatedAtUtc
				}).ToList();

				if (!string.IsNullOrWhiteSpace(s))
				{
					var q = s.Trim().ToLowerInvariant();
					dtos = dtos.Where(x =>
						(x.UserEmail ?? string.Empty).ToLower().Contains(q) ||
						(x.MfaType ?? string.Empty).ToLower().Contains(q)
					).ToList();
				}

				bool desc = string.Equals(sd, "desc", StringComparison.OrdinalIgnoreCase);
				dtos = (sf?.ToLowerInvariant()) switch
				{
					"useremail" => (desc ? dtos.OrderByDescending(x => x.UserEmail) : dtos.OrderBy(x => x.UserEmail)).ToList(),
					"mfatype" => (desc ? dtos.OrderByDescending(x => x.MfaType) : dtos.OrderBy(x => x.MfaType)).ToList(),
					"lastusedat" => (desc ? dtos.OrderByDescending(x => x.LastUsedAt) : dtos.OrderBy(x => x.LastUsedAt)).ToList(),
					"createdatutc" => (desc ? dtos.OrderByDescending(x => x.CreatedAtUtc) : dtos.OrderBy(x => x.CreatedAtUtc)).ToList(),
					_ => dtos.OrderByDescending(x => x.CreatedAtUtc).ToList()
				};

				return dtos;
			},
			filters: filters,
			columnMapper: (mfa) => new Dictionary<string, object>
			{
				["UserEmail"] = mfa?.UserEmail ?? string.Empty,
				["MfaType"] = mfa?.MfaType ?? string.Empty,
				["IsActive"] = mfa?.IsActive == true ? "Active" : "Inactive",
				["IsDefault"] = mfa?.IsDefault == true ? "Yes" : "No",
				["LastUsedAt"] = mfa?.LastUsedAt?.ToString("o") ?? string.Empty,
				["CreatedAtUtc"] = mfa?.CreatedAtUtc.ToString("o") ?? string.Empty
			});
	}

	public Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId)
	{
		return _importExportService.GetExportJobStatusAsync(jobId);
	}

	public Task<byte[]?> DownloadExportFileAsync(string jobId)
	{
		return _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		var organizationId = _tenantService.GetOrganizationId();

		var query = _unitOfWork.Repository<ImportExportHistory>().GetQueryable()
			.Where(h => h.OrganizationId == organizationId && h.EntityType == "MfaSettings");

		if (type.HasValue)
			query = query.Where(h => h.OperationType == (ImportExportOperationType)type.Value);

		var totalCount = await query.CountAsync();

		var history = await query
			.OrderByDescending(h => h.CreatedAtUtc)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(h => new ImportExportHistoryDto
			{
				Id = h.Id,
				JobId = h.JobId,
				EntityType = h.EntityType,
				OperationType = h.OperationType.ToString(),
				FileName = h.FileName,
				Format = h.Format,
				FileSizeBytes = h.FileSizeBytes,
				Status = h.Status.ToString(),
				Progress = h.Progress,
				ErrorMessage = h.ErrorMessage,
				TotalRows = h.TotalRows,
				SuccessCount = h.SuccessCount,
				UpdatedCount = h.UpdatedCount,
				SkippedCount = h.SkippedCount,
				ErrorCount = h.ErrorCount,
				DuplicateHandlingStrategy = h.DuplicateHandlingStrategy,
				ErrorReportId = h.ErrorReportId,
				DownloadUrl = h.DownloadUrl,
				AppliedFilters = h.AppliedFilters,
				CreatedAtUtc = h.CreatedAtUtc,
				CompletedAtUtc = h.CompletedAt,
				ImportedBy = h.ImportedBy
			})
			.ToListAsync();

		return new PagedResultDto<ImportExportHistoryDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			Items = history
		};
	}
}
