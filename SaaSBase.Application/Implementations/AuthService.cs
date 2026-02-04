using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SaaSBase.Application.Implementations;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly IConfiguration _configuration;
    private readonly ISessionService _sessionService;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly IPasswordPolicyService _policy; // optional future use
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICacheService _cacheService;
    private readonly IMfaService _mfaService;
    private readonly IDemoDataService _demoDataService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, IConfiguration configuration, ISessionService sessionService, IPasswordPolicyService passwordPolicyService, IEmailService emailService, ISmsService smsService, IHttpContextAccessor httpContextAccessor, ICacheService cacheService, IMfaService mfaService, IDemoDataService demoDataService, IServiceScopeFactory serviceScopeFactory, ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _configuration = configuration;
        _sessionService = sessionService;
        _passwordPolicyService = passwordPolicyService;
        _emailService = emailService;
        _smsService = smsService;
        _policy = passwordPolicyService;
        _httpContextAccessor = httpContextAccessor;
        _cacheService = cacheService;
        _mfaService = mfaService;
        _demoDataService = demoDataService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var userRepo = _unitOfWork.Repository<User>();

        // During login, we don't have tenant context yet, so bypass tenant filter
        // Find user by email across all organizations (email is unique per organization)
        var userData = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(u => u.Email == dto.Email && !u.IsDeleted)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.PasswordHash,
                u.IsActive,
                u.LockedUntil,
                u.FailedLoginAttempts,
                u.FullName,
                u.FirstName,
                u.LastName,
                u.IsEmailVerified,
                u.IsPhoneVerified,
                u.LastLoginAt,
                u.AvatarUrl,
                u.TimeZone,
                u.Language,
                u.IsMfaEnabled,
                u.OrganizationId
            })
            .FirstOrDefaultAsync();

        if (userData == null)
        {
            // Don't reveal if user exists
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Set tenant context for subsequent operations using user's organization
        _tenantService.SetBackgroundContext(userData.OrganizationId, userData.Id);

        // Check if organization is active (before other checks)
        if (userData.OrganizationId != Guid.Empty)
        {
            var organizationRepo = _unitOfWork.Repository<Organization>();
            var organization = await organizationRepo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == userData.OrganizationId && !o.IsDeleted);

            if (organization != null && !organization.IsActive)
            {
                throw new UnauthorizedAccessException("Organization is inactive. Please contact your administrator.");
            }
        }

        // Check if account is locked
        if (userData.LockedUntil.HasValue && userData.LockedUntil > DateTimeOffset.UtcNow)
        {
            var remainingTime = userData.LockedUntil.Value - DateTimeOffset.UtcNow;
            throw new UnauthorizedAccessException($"Account is locked. Try again in {remainingTime.Minutes} minutes.");
        }

        // Check if account is active
        if (!userData.IsActive)
        {
            throw new UnauthorizedAccessException("Account is deactivated");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, userData.PasswordHash))
        {
            // Get full user entity only when we need to update failed attempts
            var user = await userRepo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userData.Id);
            if (user != null)
            {
                // Increment failed attempts
                user.FailedLoginAttempts++;

                // Check if should lock account - use user's organization for policy lookup
                var policy = await _passwordPolicyService.GetPasswordPolicyAsync();
                if (user.FailedLoginAttempts >= policy.MaxFailedAttempts)
                {
                    user.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(policy.LockoutDurationMinutes);
                }

                userRepo.Update(user);
                await _unitOfWork.SaveChangesAsync();
            }
            throw new UnauthorizedAccessException("Invalid credentials");
        }

        // Get full user entity only when we need to update login info
        var userForUpdate = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userData.Id);
        if (userForUpdate == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Reset failed attempts on successful password verification
        userForUpdate.FailedLoginAttempts = 0;
        userForUpdate.LockedUntil = null;

        // Check if MFA is enabled for this user
        if (userData.IsMfaEnabled)
        {
            // Get enabled MFA methods
            var mfaSettings = await _mfaService.GetUserMfaSettingsSummaryAsync(userData.Id);

            if (mfaSettings != null && mfaSettings.EnabledMethods != null && mfaSettings.EnabledMethods.Count > 0)
            {
                // Filter to only include SMS, EMAIL, and BACKUPCODE (exclude TOTP and others)
                var allowedMethods = mfaSettings.EnabledMethods
                    .Where(m => m.Equals("SMS", StringComparison.OrdinalIgnoreCase) ||
                               m.Equals("EMAIL", StringComparison.OrdinalIgnoreCase) ||
                               m.Equals("BACKUPCODE", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (allowedMethods.Count == 0)
                {
                    // If no allowed methods, fall through to normal login
                }
                else
                {
                    // Return response indicating MFA is required
                    return new AuthResponseDto
                    {
                        RequiresMfa = true,
                        EnabledMfaMethods = allowedMethods, // Only SMS, EMAIL, BACKUPCODE
                        DefaultMfaMethod = mfaSettings.DefaultMethod, // Default method
                        PhoneNumberMasked = mfaSettings.PhoneNumberMasked,
                        EmailMasked = mfaSettings.EmailMasked,
                        TempUserId = userData.Id,
                        User = new UserDto
                        {
                            Id = userData.Id,
                            Email = userData.Email,
                            FullName = userData.FullName,
                            FirstName = userData.FirstName,
                            LastName = userData.LastName,
                            IsActive = userData.IsActive,
                            IsEmailVerified = userData.IsEmailVerified,
                            IsPhoneVerified = userData.IsPhoneVerified,
                            LastLoginAt = userData.LastLoginAt,
                            AvatarUrl = userData.AvatarUrl,
                            TimeZone = userData.TimeZone,
                            Language = userData.Language,
                            IsMfaEnabled = userData.IsMfaEnabled
                        }
                    };
                }
            }
        }

        // MFA not required or not enabled - complete login
        userForUpdate.LastLoginAt = DateTimeOffset.UtcNow;

        // Create session
        var sessionDto = new CreateSessionDto
        {
            DeviceId = dto.DeviceId ?? Guid.NewGuid().ToString(),
            DeviceName = dto.DeviceName ?? "Unknown Device",
            DeviceType = dto.DeviceType ?? "DESKTOP",
            BrowserName = dto.BrowserName ?? "Unknown",
            BrowserVersion = dto.BrowserVersion ?? "Unknown",
            OperatingSystem = dto.OperatingSystem ?? "Unknown",
            IpAddress = dto.IpAddress ?? "Unknown",
            UserAgent = dto.UserAgent ?? "Unknown",
            Location = dto.Location ?? "Unknown",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };

        var session = await _sessionService.CreateSessionAsync(userForUpdate.Id, sessionDto);

        // Generate JWT token with sessionId claim
        var token = GenerateJwtToken(userForUpdate, session.SessionId);

        // Get user roles - optimized to get role names directly
        // Now tenant context is set, so queries work normally
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var roleNames = await userRoleRepo.GetQueryable()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userData.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        // Update user
        userRepo.Update(userForUpdate);
        await _unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = session.RefreshToken,
            ExpiresAt = session.ExpiresAt,
            RequiresMfa = false,
            User = new UserDto
            {
                Id = userData.Id,
                Email = userData.Email,
                FullName = userData.FullName,
                FirstName = userData.FirstName,
                LastName = userData.LastName,
                IsActive = userData.IsActive,
                IsEmailVerified = userData.IsEmailVerified,
                IsPhoneVerified = userData.IsPhoneVerified,
                LastLoginAt = userData.LastLoginAt,
                AvatarUrl = userData.AvatarUrl,
                TimeZone = userData.TimeZone,
                Language = userData.Language,
                IsMfaEnabled = userData.IsMfaEnabled
            },
            Roles = roleNames.ToList()
        };
    }

    public async Task<AuthResponseDto> CompleteLoginWithMfaAsync(VerifyMfaLoginDto dto)
    {
        var userRepo = _unitOfWork.Repository<User>();

        // Find user by ID (bypass tenant filter during login)
        var userData = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(u => u.Id == dto.UserId && !u.IsDeleted)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.IsActive,
                u.FullName,
                u.FirstName,
                u.LastName,
                u.IsEmailVerified,
                u.IsPhoneVerified,
                u.LastLoginAt,
                u.AvatarUrl,
                u.TimeZone,
                u.Language,
                u.IsMfaEnabled,
                u.OrganizationId
            })
            .FirstOrDefaultAsync();

        if (userData == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Set tenant context for subsequent operations
        _tenantService.SetBackgroundContext(userData.OrganizationId, userData.Id);

        // Check if account is active
        if (!userData.IsActive)
        {
            throw new UnauthorizedAccessException("Account is deactivated");
        }

        // Verify MFA code
        string normalizedMfaType = dto.MfaType.ToUpper();
        bool isValid = false;

        if (normalizedMfaType == "SMS" || normalizedMfaType == "EMAIL")
        {
            isValid = await _mfaService.VerifyMfaCodeAsync(dto.UserId, dto.Code, normalizedMfaType);
        }
        else if (normalizedMfaType == "BACKUPCODE")
        {
            // Pass IP and UserAgent from DTO for proper logging
            isValid = await _mfaService.VerifyBackupCodeAsync(dto.UserId, dto.Code, dto.IpAddress, dto.UserAgent);
        }
        else
        {
            throw new UnauthorizedAccessException("Invalid MFA type");
        }

        if (!isValid)
        {
            throw new UnauthorizedAccessException("Invalid MFA code");
        }

        // Get full user entity for update
        var userForUpdate = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == dto.UserId);
        if (userForUpdate == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Update last login
        userForUpdate.LastLoginAt = DateTimeOffset.UtcNow;

        // Create session
        var sessionDto = new CreateSessionDto
        {
            DeviceId = dto.DeviceId ?? Guid.NewGuid().ToString(),
            DeviceName = dto.DeviceName ?? "Unknown Device",
            DeviceType = dto.DeviceType ?? "DESKTOP",
            BrowserName = dto.BrowserName ?? "Unknown",
            BrowserVersion = dto.BrowserVersion ?? "Unknown",
            OperatingSystem = dto.OperatingSystem ?? "Unknown",
            IpAddress = dto.IpAddress ?? "Unknown",
            UserAgent = dto.UserAgent ?? "Unknown",
            Location = dto.Location ?? "Unknown",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };

        var session = await _sessionService.CreateSessionAsync(userForUpdate.Id, sessionDto);

        // Generate JWT token with sessionId claim
        var token = GenerateJwtToken(userForUpdate, session.SessionId);

        // Get user roles
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var roleNames = await userRoleRepo.GetQueryable()
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == userData.Id)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        // Update user
        userRepo.Update(userForUpdate);
        await _unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = session.RefreshToken,
            ExpiresAt = session.ExpiresAt,
            RequiresMfa = false,
            User = new UserDto
            {
                Id = userData.Id,
                Email = userData.Email,
                FullName = userData.FullName,
                FirstName = userData.FirstName,
                LastName = userData.LastName,
                IsActive = userData.IsActive,
                IsEmailVerified = userData.IsEmailVerified,
                IsPhoneVerified = userData.IsPhoneVerified,
                LastLoginAt = userData.LastLoginAt,
                AvatarUrl = userData.AvatarUrl,
                TimeZone = userData.TimeZone,
                Language = userData.Language,
                IsMfaEnabled = userData.IsMfaEnabled
            },
            Roles = roleNames.ToList()
        };
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto dto)
    {
        var sessionRepo = _unitOfWork.Repository<UserSession>();
        var userRepo = _unitOfWork.Repository<User>();
        var refreshTokenRepo = _unitOfWork.Repository<RefreshToken>();

        // During token refresh, we might not have tenant context yet, so bypass tenant filter
        // Find session by refresh token
        var session = await sessionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(s => s.RefreshToken == dto.RefreshToken && s.IsActive && !s.IsDeleted)
            .FirstOrDefaultAsync();

        if (session == null || session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        // Set tenant context for subsequent operations using session's organization
        _tenantService.SetBackgroundContext(session.OrganizationId, session.UserId);

        // Get user with roles
        // Use IgnoreQueryFilters to ensure we can access the user
        var user = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(u => u.Id == session.UserId && !u.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive");
        }

        // Generate new JWT token with sessionId claim
        var token = GenerateJwtToken(user, session.SessionId);

        // Get user roles
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var roleRepo = _unitOfWork.Repository<Role>();

        // Use GetQueryable() to ensure proper filtering with tenant context now set
        var userRoles = await userRoleRepo.GetQueryable()
            .Where(ur => ur.UserId == user.Id && ur.OrganizationId == session.OrganizationId && !ur.IsDeleted)
            .ToListAsync();

        var roleNames = new List<string>();
        foreach (var userRole in userRoles)
        {
            var role = await roleRepo.GetQueryable()
                .Where(r => r.Id == userRole.RoleId && r.OrganizationId == session.OrganizationId && !r.IsDeleted)
                .FirstOrDefaultAsync();
            if (role != null)
                roleNames.Add(role.Name);
        }

        // Update session
        session.LastActivityAt = DateTimeOffset.UtcNow;
        sessionRepo.Update(session);

        await _unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = session.RefreshToken,
            ExpiresAt = session.ExpiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                IsEmailVerified = user.IsEmailVerified,
                IsPhoneVerified = user.IsPhoneVerified,
                LastLoginAt = user.LastLoginAt,
                AvatarUrl = user.AvatarUrl,
                TimeZone = user.TimeZone,
                Language = user.Language,
                IsMfaEnabled = user.IsMfaEnabled
            },
            Roles = roleNames
        };
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordDto dto)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.GetByIdAsync(dto.UserId);
        if (user == null) throw new ArgumentException("User not found");

        if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
        {
            // Use ArgumentException instead of UnauthorizedAccessException
            // This is a validation error, not an authentication failure
            // UnauthorizedAccessException causes logout in frontend
            throw new ArgumentException("Current password is incorrect");
        }

        // Get password policy once (pass OrganizationId to avoid tenant context dependency)
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(user.OrganizationId);

        // Validate password against policy (pass organizationId to avoid duplicate GetPasswordPolicyAsync call)
        var validationResult = await _passwordPolicyService.ValidatePasswordAsync(dto.NewPassword, dto.UserId, user.OrganizationId);

        if (!validationResult.IsValid)
        {
            // Combine all validation errors into a single message
            var errorMessage = validationResult.Errors.Count > 0
                ? string.Join("; ", validationResult.Errors)
                : "Password does not meet the policy requirements";
            throw new ArgumentException(errorMessage);
        }

        // Check password history (only if enabled)
        if (policy.PasswordHistoryCount > 0)
        {
            var isInHistory = await _passwordPolicyService.IsPasswordInHistoryAsync(dto.UserId, dto.NewPassword);
            if (isInHistory)
            {
                throw new ArgumentException($"Password cannot be one of your last {policy.PasswordHistoryCount} passwords");
            }
        }

        // Store old password hash for history before updating
        var oldPasswordHash = user.PasswordHash;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.MustChangePasswordOnNextLogin = false;

        var logRepo = _unitOfWork.Repository<UserActivityLog>();
        var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";

        await logRepo.AddAsync(new UserActivityLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            UserId = user.Id,
            Action = "PASSWORD_CHANGED",
            Description = "User changed their password",
            Timestamp = DateTimeOffset.UtcNow,
            Severity = "INFO",
            Details = oldPasswordHash, // Store old hash for password history
            IpAddress = ipAddress,
            UserAgent = userAgent
        });

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordDto dto)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var passwordResetRepo = _unitOfWork.Repository<PasswordResetToken>();

        var userData = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(u => u.Email.ToLower() == dto.Email.ToLower() && !u.IsDeleted)
            .Select(u => new { u.Id, u.Email, u.OrganizationId })
            .FirstOrDefaultAsync();

        if (userData == null)
        {
            // Don't reveal if user exists (security best practice)
            return true;
        }

        // Create password reset token bound to the user's organization
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            OrganizationId = userData.OrganizationId,
            UserId = userData.Id,
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            IsUsed = false
        };

        await passwordResetRepo.AddAsync(resetToken);
        await _unitOfWork.SaveChangesAsync();

        // Send email with reset token
        await _emailService.SendPasswordResetEmailAsync(userData.Email, resetToken.Token);

        return true;
    }

    public async Task<bool> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var passwordResetRepo = _unitOfWork.Repository<PasswordResetToken>();

        // During password reset, we don't have tenant context yet, so bypass tenant filter
        var tokenData = await passwordResetRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(t => t.Token == dto.Token && !t.IsUsed && !t.IsDeleted)
            .Select(t => new { t.Id, t.UserId, t.OrganizationId, t.ExpiresAt })
            .FirstOrDefaultAsync();

        if (tokenData == null || tokenData.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentException("Invalid or expired reset token");
        }

        // Get only user data needed for password update
        // Use IgnoreQueryFilters here too since we don't have tenant context yet
        var userData = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(u => u.Id == tokenData.UserId && !u.IsDeleted)
            .Select(u => new { u.Id, u.OrganizationId, u.PasswordHash })
            .FirstOrDefaultAsync();

        if (userData == null)
        {
            throw new ArgumentException("User not found");
        }

        // Defense-in-depth: ensure the token's organization matches the user's organization
        if (userData.OrganizationId != tokenData.OrganizationId)
        {
            throw new ArgumentException("Invalid reset token");
        }

        // Get password policy once (pass OrganizationId to avoid tenant context dependency)
        var policy = await _passwordPolicyService.GetPasswordPolicyAsync(userData.OrganizationId);

        // Validate new password against policy (pass organizationId to avoid duplicate GetPasswordPolicyAsync call)
        var validationResult = await _passwordPolicyService.ValidatePasswordAsync(dto.NewPassword, userData.Id, userData.OrganizationId);

        if (!validationResult.IsValid)
        {
            // Combine all validation errors into a single message
            var errorMessage = validationResult.Errors.Count > 0
                ? string.Join("; ", validationResult.Errors)
                : "Password does not meet the policy requirements";
            throw new ArgumentException(errorMessage);
        }

        // Check password history (only if enabled)
        if (policy.PasswordHistoryCount > 0)
        {
            var isInHistory = await _passwordPolicyService.IsPasswordInHistoryAsync(userData.Id, dto.NewPassword);
            if (isInHistory)
            {
                throw new ArgumentException($"Password cannot be one of your last {policy.PasswordHistoryCount} passwords");
            }
        }

        // Update password - get full user entity only when we need to update
        // Use IgnoreQueryFilters to ensure we can access the user even without tenant context
        var user = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userData.Id && !u.IsDeleted);
        if (user == null)
        {
            throw new ArgumentException("User not found");
        }

        // Store old password hash for history before updating
        var oldPasswordHash = user.PasswordHash;

        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.PasswordHash = newHash;
        user.PasswordChangedAt = DateTimeOffset.UtcNow;
        user.MustChangePasswordOnNextLogin = false;

        // Record password history in activity logs
        var logRepo = _unitOfWork.Repository<UserActivityLog>();
        var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var userAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";

        await logRepo.AddAsync(new UserActivityLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = user.OrganizationId,
            UserId = user.Id,
            Action = "PASSWORD_CHANGED",
            Description = "User password changed",
            Timestamp = DateTimeOffset.UtcNow,
            Severity = "INFO",
            Details = oldPasswordHash, // Store old hash for password history
            IpAddress = ipAddress ?? "0.0.0.0",
            UserAgent = userAgent ?? "Unknown"
        });

        // Mark token as used - get full token entity only when we need to update
        // Use IgnoreQueryFilters to ensure we can access the token even without tenant context
        var resetToken = await passwordResetRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tokenData.Id && !t.IsDeleted);
        if (resetToken != null)
        {
            resetToken.IsUsed = true;
            resetToken.UsedAt = DateTimeOffset.UtcNow;
            passwordResetRepo.Update(resetToken);
        }

        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SendEmailVerificationAsync(Guid userId)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var userRepo = _unitOfWork.Repository<User>();
        var emailTokenRepo = _unitOfWork.Repository<EmailVerificationToken>();

        // Optimized: Check if user exists and is not already verified
        var userData = await userRepo.FindSelectAsync(
            u => u.Id == userId && u.OrganizationId == OrganizationId,
            u => new { u.Id, u.Email, u.IsEmailVerified }
        );

        if (userData == null || userData.IsEmailVerified)
        {
            return false;
        }

        // Optimized: Invalidate previous unused tokens using bulk update
        var existingTokenIds = await emailTokenRepo.FindManySelectAsync(
            t => t.UserId == userData.Id && t.OrganizationId == OrganizationId && !t.IsUsed && t.ExpiresAt > DateTimeOffset.UtcNow,
            t => t.Id
        );

        if (existingTokenIds.Any())
        {
            // Use GetQueryable() to ensure proper filtering with tenant context
            var existingTokens = await emailTokenRepo.GetQueryable()
                .Where(t => existingTokenIds.Contains(t.Id) && t.OrganizationId == OrganizationId && !t.IsDeleted)
                .ToListAsync();

            foreach (var token in existingTokens)
            {
                token.IsUsed = true;
                token.UsedAt = DateTimeOffset.UtcNow;
                emailTokenRepo.Update(token);
            }
        }

        // Generate new verification token
        var tokenValue = Guid.NewGuid().ToString("N");
        var emailToken = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            UserId = userData.Id,
            Token = tokenValue,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            IsUsed = false
        };

        await emailTokenRepo.AddAsync(emailToken);
        await _unitOfWork.SaveChangesAsync();

        // Send email
        await _emailService.SendEmailVerificationAsync(userData.Email, tokenValue);

        return true;
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var emailTokenRepo = _unitOfWork.Repository<EmailVerificationToken>();
        var userRepo = _unitOfWork.Repository<User>();

        // During email verification, we don't have tenant context yet, so bypass tenant filter
        // Optimized: Get only token data needed for validation
        var tokenData = await emailTokenRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(t => t.Token == token && !t.IsUsed && !t.IsDeleted)
            .Select(t => new { t.Id, t.UserId, t.OrganizationId, t.ExpiresAt })
            .FirstOrDefaultAsync();

        if (tokenData == null || tokenData.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        // Get only user data needed for verification
        // Use IgnoreQueryFilters here too since we don't have tenant context yet
        var userData = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(u => u.Id == tokenData.UserId && !u.IsDeleted)
            .Select(u => new { u.Id, u.OrganizationId, u.Email, u.FullName })
            .FirstOrDefaultAsync();

        if (userData == null)
        {
            return false;
        }

        // Defense-in-depth: ensure the token's organization matches the user's organization
        if (userData.OrganizationId != tokenData.OrganizationId)
        {
            return false;
        }

        // Get full user entity only when we need to update
        // Use IgnoreQueryFilters to ensure we can access the user even without tenant context
        var user = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userData.Id && !u.IsDeleted);
        if (user == null)
        {
            return false;
        }

        user.IsEmailVerified = true;
        userRepo.Update(user);

        // Get full token entity only when we need to update
        // Use IgnoreQueryFilters to ensure we can access the token even without tenant context
        var emailToken = await emailTokenRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tokenData.Id && !t.IsDeleted);
        if (emailToken != null)
        {
            emailToken.IsUsed = true;
            emailToken.UsedAt = DateTimeOffset.UtcNow;
            emailTokenRepo.Update(emailToken);
        }

        // After verification: set a temporary password and send it
        var tempPassword = GenerateInitialPasswordForVerifiedUser(user);
        userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate user caches since email verification status changed
        await _cacheService.RemoveCacheAsync($"user:detail:{userData.Id}");
        await _cacheService.RemoveCacheByPatternAsync($"users:list:{userData.OrganizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:stats:{userData.OrganizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:dropdown:{userData.OrganizationId}");

        await _emailService.SendAccountPasswordEmailAsync(userData.Email, userData.FullName, tempPassword);

        return true;
    }

    // Generate and set a fresh random password after verification
    private string GenerateInitialPasswordForVerifiedUser(User user)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var rng = new Random();
        var password = new string(Enumerable.Range(0, 10).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        return password;
    }

    // Removed inline password email HTML; handled by EmailService

    private (string Name, string? Website, string? Email) GetOrganizationInfo()
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var orgRepo = _unitOfWork.Repository<Organization>();
        var org = orgRepo.FindAsync(o => o.Id == OrganizationId).GetAwaiter().GetResult();
        return (org?.Name ?? "SaaS Base", org?.Website, org?.Email);
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterDto dto)
    {
        var organizationRepo = _unitOfWork.Repository<Organization>();
        var userRepo = _unitOfWork.Repository<User>();
        var roleRepo = _unitOfWork.Repository<Role>();
        var userRoleRepo = _unitOfWork.Repository<UserRole>();

        // Check if organization with same email already exists (allow duplicate names)
        if (!string.IsNullOrEmpty(dto.Organization.Email))
        {
            var existingOrg = await organizationRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(o => !o.IsDeleted &&
                    o.Email != null && o.Email.ToLower() == dto.Organization.Email.ToLower())
                .FirstOrDefaultAsync();

            if (existingOrg != null)
            {
                throw new ArgumentException("An organization with this email already exists");
            }
        }

        // Check if user with same email already exists (across all organizations)
        var existingUser = await userRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(u => !u.IsDeleted && u.Email.ToLower() == dto.AdminUser.Email.ToLower())
            .FirstOrDefaultAsync();

        if (existingUser != null)
        {
            throw new ArgumentException("A user with this email already exists");
        }

        // Validate password against policy (if available)
        // Note: We don't have organization context yet, so we'll use a default policy check
        // In a real scenario, you might want to have a global password policy for registration
        if (string.IsNullOrWhiteSpace(dto.AdminUser.Password) || dto.AdminUser.Password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters long");
        }

        // Create organization
        var organizationId = Guid.NewGuid();
        var organization = new Organization
        {
            Id = organizationId,
            OrganizationId = organizationId, // Self-referencing for tenant isolation
            Name = dto.Organization.Name,
            Description = dto.Organization.Description,
            Email = dto.Organization.Email,
            Phone = dto.Organization.Phone,
            Address = dto.Organization.Address,
            City = dto.Organization.City,
            State = dto.Organization.State,
            Country = dto.Organization.Country,
            PostalCode = dto.Organization.PostalCode,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        await organizationRepo.AddAsync(organization);
        await _unitOfWork.SaveChangesAsync();

        // Set tenant context for subsequent operations
        _tenantService.SetBackgroundContext(organizationId, Guid.Empty);

        // Create default Administrator role for the organization
        var adminRole = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Administrator",
            Description = "Organization administrator with full access",
            RoleType = "SYSTEM",
            IsSystemRole = true,
            IsActive = true,
            SortOrder = 1,
            Color = "#6f42c1",
            Icon = "person-gear",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        await roleRepo.AddAsync(adminRole);
        await _unitOfWork.SaveChangesAsync();

        // Create admin user
        var fullName = !string.IsNullOrWhiteSpace(dto.AdminUser.FullName)
            ? dto.AdminUser.FullName.Trim()
            : $"{dto.AdminUser.FirstName} {dto.AdminUser.LastName}".Trim();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = dto.AdminUser.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AdminUser.Password),
            FullName = fullName,
            FirstName = dto.AdminUser.FirstName,
            LastName = dto.AdminUser.LastName,
            IsActive = dto.AdminUser.IsActive,
            IsEmailVerified = false, // Email verification will be sent after registration
            IsPhoneVerified = false,
            TimeZone = "UTC",
            Language = "en",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        await userRepo.AddAsync(adminUser);
        await _unitOfWork.SaveChangesAsync();

        // Assign Administrator role to admin user
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = adminUser.Id,
            RoleId = adminRole.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        await userRoleRepo.AddAsync(userRole);
        await _unitOfWork.SaveChangesAsync();

        // Seed default menus, permissions, and assign all permissions to Administrator role
        // This is critical - admin must have access even without demo data
        try
        {
            await SeedDefaultAccessDataForOrganizationAsync(organizationId, adminRole.Id);
        }
        catch (Exception ex)
        {
            // Log error but don't fail registration - admin can manually set permissions later
            _logger.LogWarning(ex, "Failed to seed default access data for organization {OrganizationId}", organizationId);
        }

        // Send email verification (fire and forget)
        // Note: This will be handled by the existing SendEmailVerificationAsync method
        // which can be called later, or we can create the token here synchronously
        var emailTokenRepo = _unitOfWork.Repository<EmailVerificationToken>();
        var tokenValue = Guid.NewGuid().ToString("N");
        var emailToken = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = adminUser.Id,
            Token = tokenValue,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            IsUsed = false,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = "System"
        };

        await emailTokenRepo.AddAsync(emailToken);
        await _unitOfWork.SaveChangesAsync();

        // Capture values before background tasks
        var adminUserEmail = adminUser.Email;
        var adminUserId = adminUser.Id;
        var adminUserFullName = adminUser.FullName;

        // Send email verification (fire and forget) - create new scope for background task
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var tenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

                // Set background context
                tenantService.SetBackgroundContext(organizationId, adminUserId, adminUserFullName);

                await emailService.SendEmailVerificationAsync(adminUserEmail, tokenValue);
            }
            catch { /* Ignore email errors */ }
        });

        // Seed demo data only if requested (fire and forget) - create new scope for background task

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var demoDataService = scope.ServiceProvider.GetRequiredService<IDemoDataService>();
                var tenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

                // Set background context
                tenantService.SetBackgroundContext(organizationId, adminUserId, adminUserFullName);

                await demoDataService.SeedDemoDataForOrganizationAsync(organizationId);
            }
            catch { /* Ignore demo data errors - registration still succeeds */ }
        });

        return new RegisterResponseDto
        {
            OrganizationId = organizationId,
            OrganizationName = organization.Name,
            UserId = adminUser.Id,
            UserEmail = adminUser.Email,
            UserFullName = adminUser.FullName,
            Message = dto.CreateDemoData
                ? "Registration successful. Demo data is being populated. Please check your email to verify your account."
                : "Registration successful. Please check your email to verify your account."
        };
    }

    private static string[] GetCompanyAdminPermissionCodes()
    {
        // Define which permissions Company Administrator should have (based on permission matrix)
        return new[]
        {
            // Dashboard
            "Dashboard.Read",
            // Users - Full access
            "Users.Read",
            "Users.Create",
            "Users.Update",
            "Users.Delete",
            "Users.Import",
            "Users.Export",
            // Roles - Full access
            "Roles.Read",
            "Roles.Create",
            "Roles.Update",
            "Roles.Delete",
            "Roles.Import",
            "Roles.Export",
            // Permissions - Read access for assigning permissions to roles
            "Permissions.Read",
            "Permissions.Update",
            // Departments - Full access
            "Departments.Read",
            "Departments.Create",
            "Departments.Update",
            "Departments.Delete",
            "Departments.Import",
            "Departments.Export",
            // Positions - Full access
            "Positions.Read",
            "Positions.Create",
            "Positions.Update",
            "Positions.Delete",
            "Positions.Import",
            "Positions.Export",
            // Sessions - Read and Delete
            "Sessions.Read",
            "Sessions.Delete",
            // MFA - Read and Update
            "Mfa.Read",
            "Mfa.Update",
            // Organizations - Read and Update
            "Organizations.Read",
            "Organizations.Update",
            // Organization Settings - Full access
            "Organizations.Locations.Read",
            "Organizations.Locations.Create",
            "Organizations.Locations.Update",
            "Organizations.Locations.Delete",
            "Organizations.Locations.Import",
            "Organizations.Locations.Export",
            "Organizations.BusinessSettings.Read",
            "Organizations.BusinessSettings.Create",
            "Organizations.BusinessSettings.Update",
            "Organizations.BusinessSettings.Delete",
            "Organizations.BusinessSettings.Import",
            "Organizations.BusinessSettings.Export",
            "Organizations.Currencies.Read",
            "Organizations.Currencies.Create",
            "Organizations.Currencies.Update",
            "Organizations.Currencies.Delete",
            "Organizations.Currencies.Import",
            "Organizations.Currencies.Export",
            "Organizations.TaxRates.Read",
            "Organizations.TaxRates.Create",
            "Organizations.TaxRates.Update",
            "Organizations.TaxRates.Delete",
            "Organizations.TaxRates.Import",
            "Organizations.TaxRates.Export",
            "Organizations.IntegrationSettings.Read",
            "Organizations.IntegrationSettings.Create",
            "Organizations.IntegrationSettings.Update",
            "Organizations.IntegrationSettings.Delete",
            "Organizations.IntegrationSettings.Import",
            "Organizations.IntegrationSettings.Export",
            "Organizations.NotificationTemplates.Read",
            "Organizations.NotificationTemplates.Create",
            "Organizations.NotificationTemplates.Update",
            "Organizations.NotificationTemplates.Delete",
            "Organizations.NotificationTemplates.Import",
            "Organizations.NotificationTemplates.Export",
            // Password Policy - Read access for self account settings
            "PasswordPolicy.Read"
        };
    }

    private async Task SeedDefaultAccessDataForOrganizationAsync(Guid organizationId, Guid adminRoleId)
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();

        // Get Company Admin permission codes (used in multiple places)
        var adminPermissionCodes = GetCompanyAdminPermissionCodes();

        // Permissions are system-wide, get them from system organization
        var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var allPermissions = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => p.OrganizationId == systemOrganizationId && p.IsActive)
            .ToListAsync();

        // Menus are global - they're already seeded in SeedData.cs
        // We only need to assign permissions to Administrator role
        // Check if admin role already has permissions assigned
        var adminHasPermissions = await rolePermissionRepo.GetQueryable()
            .AnyAsync(rp => rp.RoleId == adminRoleId && rp.OrganizationId == organizationId && !rp.IsDeleted);

        if (!adminHasPermissions && allPermissions.Any())
        {
            // Assign only permissions that are NOT System Admin only (IsSystemAdminOnly = false)
            var adminRolePermissions = allPermissions
                .Where(p => !p.IsSystemAdminOnly && adminPermissionCodes.Contains(p.Code))
                .Select(p => new RolePermission
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    RoleId = adminRoleId,
                    PermissionId = p.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = "System"
                }).ToList();

            if (adminRolePermissions.Any())
            {
                await rolePermissionRepo.AddRangeAsync(adminRolePermissions);
                await _unitOfWork.SaveChangesAsync();
            }
        }
        else if (adminHasPermissions)
        {
            // Sync missing permissions to Administrator role
            var existingRolePermissions = await rolePermissionRepo.GetQueryable()
                .Where(rp => rp.RoleId == adminRoleId && rp.OrganizationId == organizationId && !rp.IsDeleted)
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var existingPermissionIds = existingRolePermissions.ToHashSet();
            var missingPermissions = allPermissions
                .Where(p => !p.IsSystemAdminOnly && adminPermissionCodes.Contains(p.Code) && !existingPermissionIds.Contains(p.Id))
                .Select(p => new RolePermission
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    RoleId = adminRoleId,
                    PermissionId = p.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = "System"
                }).ToList();

            if (missingPermissions.Any())
            {
                await rolePermissionRepo.AddRangeAsync(missingPermissions);
                await _unitOfWork.SaveChangesAsync();
            }
        }
    }

    // OLD CODE BELOW - REMOVED: Permissions are now system-wide, not organization-specific
    // This entire section was removed because permissions are now seeded system-wide in SeedData.cs
    // Permissions are no longer created per-organization, they are system-wide and seeded once
    /*
    private void OldSeedDefaultAccessDataForOrganizationAsync_Removed_CreatePermissions(Guid organizationId, Guid adminRoleId, List<Menu> menus)
    {
        var menuRepo = _unitOfWork.Repository<Menu>();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();

        // Get menu IDs for permission mapping
        var organizationsMenu = menus.FirstOrDefault(m => m.Label == "Organizations");
        var organizationSettingsMenu = menus.FirstOrDefault(m => m.Label == "Organization Settings");
        var departmentsMenu = menus.FirstOrDefault(m => m.Label == "Departments");
        var positionsMenu = menus.FirstOrDefault(m => m.Label == "Positions");
        var usersMenu = menus.FirstOrDefault(m => m.Label == "Users");
        var rolesMenu = menus.FirstOrDefault(m => m.Label == "Roles");
        var permissionsMenu = menus.FirstOrDefault(m => m.Label == "Permissions");
        var sessionsMenu = menus.FirstOrDefault(m => m.Label == "Sessions");
        var mfaMenu = menus.FirstOrDefault(m => m.Label == "MFA");
        var dashboardMenuId = menus.FirstOrDefault(m => m.Label == "Dashboard")?.Id ?? Guid.Empty;

        // Create default permissions mapped to menus
        var permissions = new List<Permission>
        {
            // Dashboard permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Dashboard.Read",
                Name = "Read Dashboard",
                Description = "View overview dashboard",
                Module = "Dashboard",
                Action = "Read",
                Resource = "Dashboard",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 11,
                Category = "CRUD",
                MenuId = dashboardMenuId,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Users permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Users.Read",
                Name = "Read Users",
                Module = "Users",
                Action = "Read",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = usersMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Users.Create",
                Name = "Create Users",
                Module = "Users",
                Action = "Create",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = usersMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Users.Update",
                Name = "Update Users",
                Module = "Users",
                Action = "Update",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 3,
                Category = "CRUD",
                MenuId = usersMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Users.Delete",
                Name = "Delete Users",
                Module = "Users",
                Action = "Delete",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 4,
                Category = "CRUD",
                MenuId = usersMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Users.Import",
                Name = "Import Users",
                Module = "Users",
                Action = "Import",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 5,
                Category = "Import",
                MenuId = usersMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Users.Export",
                Name = "Export Users",
                Module = "Users",
                Action = "Export",
                Resource = "Users",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 6,
                Category = "Export",
                MenuId = usersMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Roles permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Roles.Read",
                Name = "Read Roles",
                Module = "Roles",
                Action = "Read",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = rolesMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Roles.Create",
                Name = "Create Roles",
                Module = "Roles",
                Action = "Create",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = rolesMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Roles.Update",
                Name = "Update Roles",
                Module = "Roles",
                Action = "Update",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 3,
                Category = "CRUD",
                MenuId = rolesMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Roles.Delete",
                Name = "Delete Roles",
                Module = "Roles",
                Action = "Delete",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 4,
                Category = "CRUD",
                MenuId = rolesMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Roles.Import",
                Name = "Import Roles",
                Module = "Roles",
                Action = "Import",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 5,
                Category = "Import",
                MenuId = rolesMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Roles.Export",
                Name = "Export Roles",
                Module = "Roles",
                Action = "Export",
                Resource = "Roles",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 6,
                Category = "Export",
                MenuId = rolesMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Permissions permissions - Only Read and Update (for activate/deactivate)
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Permissions.Read",
                Name = "Read Permissions",
                Module = "Permissions",
                Action = "Read",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = permissionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Permissions.Update",
                Name = "Update Permissions",
                Module = "Permissions",
                Action = "Update",
                Resource = "Permissions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = permissionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Organizations permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Organizations.Read",
                Name = "Read Organizations",
                Module = "Organizations",
                Action = "Read",
                Resource = "Organizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = organizationSettingsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Organizations.Update",
                Name = "Update Organizations",
                Module = "Organizations",
                Action = "Update",
                Resource = "Organizations",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = organizationSettingsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Departments permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Departments.Read",
                Name = "Read Departments",
                Module = "Departments",
                Action = "Read",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = departmentsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Departments.Create",
                Name = "Create Departments",
                Module = "Departments",
                Action = "Create",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = departmentsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Departments.Update",
                Name = "Update Departments",
                Module = "Departments",
                Action = "Update",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 3,
                Category = "CRUD",
                MenuId = departmentsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Departments.Delete",
                Name = "Delete Departments",
                Module = "Departments",
                Action = "Delete",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 4,
                Category = "CRUD",
                MenuId = departmentsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Departments.Import",
                Name = "Import Departments",
                Module = "Departments",
                Action = "Import",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 5,
                Category = "Import",
                MenuId = departmentsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Departments.Export",
                Name = "Export Departments",
                Module = "Departments",
                Action = "Export",
                Resource = "Departments",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 6,
                Category = "Export",
                MenuId = departmentsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Positions permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Positions.Read",
                Name = "Read Positions",
                Module = "Positions",
                Action = "Read",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = positionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Positions.Create",
                Name = "Create Positions",
                Module = "Positions",
                Action = "Create",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = positionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Positions.Update",
                Name = "Update Positions",
                Module = "Positions",
                Action = "Update",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 3,
                Category = "CRUD",
                MenuId = positionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Positions.Delete",
                Name = "Delete Positions",
                Module = "Positions",
                Action = "Delete",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 4,
                Category = "CRUD",
                MenuId = positionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Positions.Import",
                Name = "Import Positions",
                Module = "Positions",
                Action = "Import",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 5,
                Category = "Import",
                MenuId = positionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Positions.Export",
                Name = "Export Positions",
                Module = "Positions",
                Action = "Export",
                Resource = "Positions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 6,
                Category = "Export",
                MenuId = positionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Sessions permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Sessions.Read",
                Name = "Read Sessions",
                Module = "Sessions",
                Action = "Read",
                Resource = "Sessions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = sessionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Sessions.Delete",
                Name = "Delete Sessions",
                Module = "Sessions",
                Action = "Delete",
                Resource = "Sessions",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = sessionsMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // MFA permissions
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Mfa.Read",
                Name = "Read MFA",
                Module = "Mfa",
                Action = "Read",
                Resource = "Mfa",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 1,
                Category = "CRUD",
                MenuId = mfaMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Code = "Mfa.Update",
                Name = "Update MFA",
                Module = "Mfa",
                Action = "Update",
                Resource = "Mfa",
                IsSystemPermission = true,
                IsSystemAdminOnly = false, // Company Admin accessible
                IsActive = true,
                SortOrder = 2,
                Category = "CRUD",
                MenuId = mfaMenu?.Id ?? Guid.Empty,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            },
            // Organization Settings permissions - Full access for Company Admin
            // Locations
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Locations.Read", Name = "Read Locations", Module = "Organizations", Action = "Read", Resource = "Locations", IsSystemPermission = true, IsActive = true, SortOrder = 1, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Locations.Create", Name = "Create Locations", Module = "Organizations", Action = "Create", Resource = "Locations", IsSystemPermission = true, IsActive = true, SortOrder = 2, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Locations.Update", Name = "Update Locations", Module = "Organizations", Action = "Update", Resource = "Locations", IsSystemPermission = true, IsActive = true, SortOrder = 3, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Locations.Delete", Name = "Delete Locations", Module = "Organizations", Action = "Delete", Resource = "Locations", IsSystemPermission = true, IsActive = true, SortOrder = 4, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Locations.Import", Name = "Import Locations", Module = "Organizations", Action = "Import", Resource = "Locations", IsSystemPermission = true, IsActive = true, SortOrder = 5, Category = "Import", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Locations.Export", Name = "Export Locations", Module = "Organizations", Action = "Export", Resource = "Locations", IsSystemPermission = true, IsActive = true, SortOrder = 6, Category = "Export", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            // Business Settings
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.BusinessSettings.Read", Name = "Read Business Settings", Module = "Organizations", Action = "Read", Resource = "BusinessSettings", IsSystemPermission = true, IsActive = true, SortOrder = 1, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.BusinessSettings.Create", Name = "Create Business Settings", Module = "Organizations", Action = "Create", Resource = "BusinessSettings", IsSystemPermission = true, IsActive = true, SortOrder = 2, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.BusinessSettings.Update", Name = "Update Business Settings", Module = "Organizations", Action = "Update", Resource = "BusinessSettings", IsSystemPermission = true, IsActive = true, SortOrder = 3, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.BusinessSettings.Delete", Name = "Delete Business Settings", Module = "Organizations", Action = "Delete", Resource = "BusinessSettings", IsSystemPermission = true, IsActive = true, SortOrder = 4, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.BusinessSettings.Import", Name = "Import Business Settings", Module = "Organizations", Action = "Import", Resource = "BusinessSettings", IsSystemPermission = true, IsActive = true, SortOrder = 5, Category = "Import", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.BusinessSettings.Export", Name = "Export Business Settings", Module = "Organizations", Action = "Export", Resource = "BusinessSettings", IsSystemPermission = true, IsActive = true, SortOrder = 6, Category = "Export", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            // Currencies
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Currencies.Read", Name = "Read Currencies", Module = "Organizations", Action = "Read", Resource = "Currencies", IsSystemPermission = true, IsActive = true, SortOrder = 1, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Currencies.Create", Name = "Create Currencies", Module = "Organizations", Action = "Create", Resource = "Currencies", IsSystemPermission = true, IsActive = true, SortOrder = 2, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Currencies.Update", Name = "Update Currencies", Module = "Organizations", Action = "Update", Resource = "Currencies", IsSystemPermission = true, IsActive = true, SortOrder = 3, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Currencies.Delete", Name = "Delete Currencies", Module = "Organizations", Action = "Delete", Resource = "Currencies", IsSystemPermission = true, IsActive = true, SortOrder = 4, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Currencies.Import", Name = "Import Currencies", Module = "Organizations", Action = "Import", Resource = "Currencies", IsSystemPermission = true, IsActive = true, SortOrder = 5, Category = "Import", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.Currencies.Export", Name = "Export Currencies", Module = "Organizations", Action = "Export", Resource = "Currencies", IsSystemPermission = true, IsActive = true, SortOrder = 6, Category = "Export", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            // Tax Rates
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.TaxRates.Read", Name = "Read Tax Rates", Module = "Organizations", Action = "Read", Resource = "TaxRates", IsSystemPermission = true, IsActive = true, SortOrder = 1, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.TaxRates.Create", Name = "Create Tax Rates", Module = "Organizations", Action = "Create", Resource = "TaxRates", IsSystemPermission = true, IsActive = true, SortOrder = 2, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.TaxRates.Update", Name = "Update Tax Rates", Module = "Organizations", Action = "Update", Resource = "TaxRates", IsSystemPermission = true, IsActive = true, SortOrder = 3, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.TaxRates.Delete", Name = "Delete Tax Rates", Module = "Organizations", Action = "Delete", Resource = "TaxRates", IsSystemPermission = true, IsActive = true, SortOrder = 4, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.TaxRates.Import", Name = "Import Tax Rates", Module = "Organizations", Action = "Import", Resource = "TaxRates", IsSystemPermission = true, IsActive = true, SortOrder = 5, Category = "Import", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.TaxRates.Export", Name = "Export Tax Rates", Module = "Organizations", Action = "Export", Resource = "TaxRates", IsSystemPermission = true, IsActive = true, SortOrder = 6, Category = "Export", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            // Integration Settings
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.IntegrationSettings.Read", Name = "Read Integration Settings", Module = "Organizations", Action = "Read", Resource = "IntegrationSettings", IsSystemPermission = true, IsActive = true, SortOrder = 1, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.IntegrationSettings.Create", Name = "Create Integration Settings", Module = "Organizations", Action = "Create", Resource = "IntegrationSettings", IsSystemPermission = true, IsActive = true, SortOrder = 2, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.IntegrationSettings.Update", Name = "Update Integration Settings", Module = "Organizations", Action = "Update", Resource = "IntegrationSettings", IsSystemPermission = true, IsActive = true, SortOrder = 3, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.IntegrationSettings.Delete", Name = "Delete Integration Settings", Module = "Organizations", Action = "Delete", Resource = "IntegrationSettings", IsSystemPermission = true, IsActive = true, SortOrder = 4, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.IntegrationSettings.Import", Name = "Import Integration Settings", Module = "Organizations", Action = "Import", Resource = "IntegrationSettings", IsSystemPermission = true, IsActive = true, SortOrder = 5, Category = "Import", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.IntegrationSettings.Export", Name = "Export Integration Settings", Module = "Organizations", Action = "Export", Resource = "IntegrationSettings", IsSystemPermission = true, IsActive = true, SortOrder = 6, Category = "Export", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            // Notification Templates
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.NotificationTemplates.Read", Name = "Read Notification Templates", Module = "Organizations", Action = "Read", Resource = "NotificationTemplates", IsSystemPermission = true, IsActive = true, SortOrder = 1, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.NotificationTemplates.Create", Name = "Create Notification Templates", Module = "Organizations", Action = "Create", Resource = "NotificationTemplates", IsSystemPermission = true, IsActive = true, SortOrder = 2, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.NotificationTemplates.Update", Name = "Update Notification Templates", Module = "Organizations", Action = "Update", Resource = "NotificationTemplates", IsSystemPermission = true, IsActive = true, SortOrder = 3, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.NotificationTemplates.Delete", Name = "Delete Notification Templates", Module = "Organizations", Action = "Delete", Resource = "NotificationTemplates", IsSystemPermission = true, IsActive = true, SortOrder = 4, Category = "CRUD", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.NotificationTemplates.Import", Name = "Import Notification Templates", Module = "Organizations", Action = "Import", Resource = "NotificationTemplates", IsSystemPermission = true, IsActive = true, SortOrder = 5, Category = "Import", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "Organizations.NotificationTemplates.Export", Name = "Export Notification Templates", Module = "Organizations", Action = "Export", Resource = "NotificationTemplates", IsSystemPermission = true, IsActive = true, SortOrder = 6, Category = "Export", MenuId = organizationSettingsMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" },
            // Password Policy - Read access for self account settings
            // Use Users menu since this is for user account settings
            new Permission { Id = Guid.NewGuid(), OrganizationId = organizationId, Code = "PasswordPolicy.Read", Name = "Read Password Policy", Module = "PasswordPolicy", Action = "Read", Resource = "PasswordPolicy", IsSystemPermission = true, IsActive = true, SortOrder = 1, Category = "CRUD", MenuId = usersMenu?.Id ?? Guid.Empty, CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = "System" }
        };

        // Filter out permissions with invalid MenuId
        var validPermissions = permissions.Where(p => p.MenuId != Guid.Empty).ToList();
        await permissionRepo.AddRangeAsync(validPermissions);
        await _unitOfWork.SaveChangesAsync();

        // Assign only specific permissions to Administrator role (not all permissions)
        // Assign only permissions that are NOT System Admin only (IsSystemAdminOnly = false)
        var adminPermissions = validPermissions
            .Where(p => !p.IsSystemAdminOnly && adminPermissionCodes.Contains(p.Code))
            .Select(p => new RolePermission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                RoleId = adminRoleId,
                PermissionId = p.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "System"
            }).ToList();

        await rolePermissionRepo.AddRangeAsync(adminPermissions);
        await _unitOfWork.SaveChangesAsync();
    }
    */

    /// <summary>
    /// Sync missing permissions for an organization that are in Company Admin permission codes
    /// This ensures that if new permissions are added to GetCompanyAdminPermissionCodes(), 
    /// they are automatically available to existing organizations
    /// </summary>
    private async Task SyncMissingPermissionsForOrganizationAsync(
        Guid organizationId,
        string[] adminPermissionCodes,
        List<Permission> existingPermissions,
        IRepository<Menu> menuRepo,
        IRepository<Permission> permissionRepo)
    {
        var existingPermissionCodes = existingPermissions.Select(p => p.Code).ToHashSet();
        var missingPermissionCodes = adminPermissionCodes.Where(code => !existingPermissionCodes.Contains(code)).ToList();

        if (!missingPermissionCodes.Any()) return;

        // Get menus for this organization to map permissions
        var menus = await menuRepo.GetQueryable()
            .Where(m => m.OrganizationId == organizationId && !m.IsDeleted && m.IsActive)
            .ToListAsync();

        var usersMenu = menus.FirstOrDefault(m => m.Label == "Users");
        var rolesMenu = menus.FirstOrDefault(m => m.Label == "Roles");
        var permissionsMenu = menus.FirstOrDefault(m => m.Label == "Permissions");
        var departmentsMenu = menus.FirstOrDefault(m => m.Label == "Departments");
        var positionsMenu = menus.FirstOrDefault(m => m.Label == "Positions");
        var sessionsMenu = menus.FirstOrDefault(m => m.Label == "Sessions");
        var mfaMenu = menus.FirstOrDefault(m => m.Label == "MFA");
        var organizationSettingsMenu = menus.FirstOrDefault(m => m.Label == "Organization Settings");
        var dashboardMenu = menus.FirstOrDefault(m => m.Label == "Dashboard" && m.ParentMenuId != null);

        var newPermissions = new List<Permission>();

        foreach (var code in missingPermissionCodes)
        {
            Guid? menuId = null;
            string module = "";
            string action = "";
            string resource = "";
            string category = "CRUD";

            // Map permission code to menu and details
            if (code.StartsWith("Dashboard."))
            {
                menuId = dashboardMenu?.Id;
                module = "Dashboard";
                action = code.Replace("Dashboard.", "");
                resource = "Dashboard";
            }
            else if (code.StartsWith("Users."))
            {
                menuId = usersMenu?.Id;
                module = "Users";
                action = code.Replace("Users.", "");
                resource = "Users";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Roles."))
            {
                menuId = rolesMenu?.Id;
                module = "Roles";
                action = code.Replace("Roles.", "");
                resource = "Roles";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Permissions."))
            {
                menuId = permissionsMenu?.Id;
                module = "Permissions";
                action = code.Replace("Permissions.", "");
                resource = "Permissions";
            }
            else if (code.StartsWith("Departments."))
            {
                menuId = departmentsMenu?.Id;
                module = "Departments";
                action = code.Replace("Departments.", "");
                resource = "Departments";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Positions."))
            {
                menuId = positionsMenu?.Id;
                module = "Positions";
                action = code.Replace("Positions.", "");
                resource = "Positions";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Sessions."))
            {
                menuId = sessionsMenu?.Id;
                module = "Sessions";
                action = code.Replace("Sessions.", "");
                resource = "Sessions";
            }
            else if (code.StartsWith("Mfa."))
            {
                menuId = mfaMenu?.Id;
                module = "Mfa";
                action = code.Replace("Mfa.", "");
                resource = "Mfa";
            }
            else if (code.StartsWith("Organizations.Locations."))
            {
                menuId = organizationSettingsMenu?.Id;
                module = "Organizations";
                action = code.Replace("Organizations.Locations.", "");
                resource = "Locations";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Organizations.BusinessSettings."))
            {
                menuId = organizationSettingsMenu?.Id;
                module = "Organizations";
                action = code.Replace("Organizations.BusinessSettings.", "");
                resource = "BusinessSettings";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Organizations.Currencies."))
            {
                menuId = organizationSettingsMenu?.Id;
                module = "Organizations";
                action = code.Replace("Organizations.Currencies.", "");
                resource = "Currencies";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Organizations.TaxRates."))
            {
                menuId = organizationSettingsMenu?.Id;
                module = "Organizations";
                action = code.Replace("Organizations.TaxRates.", "");
                resource = "TaxRates";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Organizations.IntegrationSettings."))
            {
                menuId = organizationSettingsMenu?.Id;
                module = "Organizations";
                action = code.Replace("Organizations.IntegrationSettings.", "");
                resource = "IntegrationSettings";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Organizations.NotificationTemplates."))
            {
                menuId = organizationSettingsMenu?.Id;
                module = "Organizations";
                action = code.Replace("Organizations.NotificationTemplates.", "");
                resource = "NotificationTemplates";
                category = code.Contains("Import") || code.Contains("Export") ? (code.Contains("Import") ? "Import" : "Export") : "CRUD";
            }
            else if (code.StartsWith("Organizations.") && !code.Contains("."))
            {
                menuId = organizationSettingsMenu?.Id;
                module = "Organizations";
                action = code.Replace("Organizations.", "");
                resource = "Organizations";
            }
            else if (code == "PasswordPolicy.Read")
            {
                menuId = usersMenu?.Id; // Use Users menu for password policy
                module = "PasswordPolicy";
                action = "Read";
                resource = "PasswordPolicy";
            }

            // Only create permission if menu exists
            if (menuId.HasValue && menuId.Value != Guid.Empty)
            {
                newPermissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Code = code,
                    Name = $"{action} {resource}",
                    Description = $"{action} {resource}",
                    Module = module,
                    Action = action,
                    Resource = resource,
                    Category = category,
                    MenuId = menuId.Value,
                    IsSystemPermission = true,
                    IsSystemAdminOnly = false, // Company Admin permissions are not System Admin only
                    IsActive = true,
                    SortOrder = 0,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = "System"
                });
            }
        }

        if (newPermissions.Any())
        {
            await permissionRepo.AddRangeAsync(newPermissions);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private string GenerateJwtToken(User user, string? sessionId = null)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSection["Key"] ?? "");
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new("tenant_id", user.OrganizationId.ToString()),
            new("is_active", user.IsActive.ToString()),
            new("is_email_verified", user.IsEmailVerified.ToString())
        };

        // Add sessionId claim if provided (for session validation)
        if (!string.IsNullOrEmpty(sessionId))
        {
            claims.Add(new Claim("sessionId", sessionId));
            claims.Add(new Claim("sid", sessionId)); // Alternative claim name
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
