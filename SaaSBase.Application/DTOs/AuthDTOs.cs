using System;

namespace SaaSBase.Application.DTOs;

public class LoginDto
{
	public string Email { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public string? DeviceId { get; set; }
	public string? DeviceName { get; set; }
	public string? DeviceType { get; set; }
	public string? BrowserName { get; set; }
	public string? BrowserVersion { get; set; }
	public string? OperatingSystem { get; set; }
	public string? IpAddress { get; set; }
	public string? UserAgent { get; set; }
	public string? Location { get; set; }
}


public class RefreshTokenDto
{
	public string RefreshToken { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
	public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
	public string Token { get; set; } = string.Empty;
	public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordDto
{
    public Guid UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class AuthResponseDto
{
	public string AccessToken { get; set; } = string.Empty;
	public string? RefreshToken { get; set; }
	public string Token { get; set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; set; }
	public string? SessionId { get; set; }
	public UserDto? User { get; set; }
	public List<string> Roles { get; set; } = new();
	public bool RequiresMfa { get; set; }
	public List<string> EnabledMfaMethods { get; set; } = new();
	public string? DefaultMfaMethod { get; set; }
	public string? PhoneNumberMasked { get; set; }
	public string? EmailMasked { get; set; }
	public Guid? TempUserId { get; set; } // Temporary user ID for MFA verification
}

public class UserListItemDto
{
	public Guid Id { get; set; }
	public string Email { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
	public bool IsActive { get; set; }
	public bool IsEmailVerified { get; set; }
	public string? AvatarUrl { get; set; }
	public string? Department { get; set; }
	public string? JobTitle { get; set; }
	public string? Location { get; set; }
	public string? EmployeeId { get; set; }
	public DateTimeOffset? LastLoginAt { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
	public Guid? RoleId { get; set; }
	public string? RoleName { get; set; }
	public List<Guid> RoleIds { get; set; } = new();
	public List<string> RoleNames { get; set; } = new();
	public DateTimeOffset? LockedUntil { get; set; } // Account lock expiration date
	public Guid OrganizationId { get; set; } // Organization ID
	public string? OrganizationName { get; set; } // Organization Name (for System Admin view)
}

public class UserDetailsDto : UserDto
{
}

public class CreateUserDto
{
	public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
	public string? PhoneNumber { get; set; }
	public bool IsActive { get; set; } = true;
	public string? Department { get; set; }
	public string? JobTitle { get; set; }
	public string? Location { get; set; }
	public string? EmployeeId { get; set; }
	public Guid RoleId { get; set; }
}

public class UpdateUserDto
{
    public string? FullName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
	public string? PhoneNumber { get; set; }
	public bool? IsActive { get; set; }
	public string? Department { get; set; }
	public string? JobTitle { get; set; }
	public string? Location { get; set; }
	public string? EmployeeId { get; set; }
	public Guid RoleId { get; set; } // Keep for backward compatibility
	public List<Guid> RoleIds { get; set; } = new(); // Multi-role support
}

public class PagedResultDto<T>
{
	public int Page { get; set; }
	public int PageSize { get; set; }
	public long TotalCount { get; set; }
	public int TotalPages { get; set; }
	public List<T> Items { get; set; } = new();
}

public class UserDto
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
	public bool IsMfaEnabled { get; set; }
	public string? JobTitle { get; set; }
	public string? Department { get; set; }
	public string? Location { get; set; }
	public string? EmployeeId { get; set; }
	public DateTime? DateOfBirth { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
	public string? EmergencyContactName { get; set; }
	public string? EmergencyContactPhone { get; set; }
	public string? EmergencyContactRelation { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public Guid? RoleId { get; set; }
	public List<Guid>? RoleIds { get; set; }
	public string? RoleName { get; set; }
	public List<string>? RoleNames { get; set; }
	public DateTimeOffset? LockedUntil { get; set; } // Account lock expiration date
	public Guid OrganizationId { get; set; } // Organization ID
	public string? OrganizationName { get; set; } // Organization Name (for System Admin view)

    // Metadata
    public string? CreatedBy { get; set; } // Keep for backward compatibility
	public DateTimeOffset? ModifiedAtUtc { get; set; }
	public string? ModifiedBy { get; set; } // Keep for backward compatibility
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
}

public class SendEmailVerificationDto
{
	public Guid UserId { get; set; }
}

public class VerifyEmailDto
{
	public string Token { get; set; } = string.Empty;
}

public class SendMfaCodeLoginDto
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
}

public class VerifyMfaLoginDto
{
	public Guid UserId { get; set; }
	public string MfaType { get; set; } = string.Empty;
	public string Code { get; set; } = string.Empty;
	public string? DeviceId { get; set; }
	public string? DeviceName { get; set; }
	public string? DeviceType { get; set; }
	public string? BrowserName { get; set; }
	public string? BrowserVersion { get; set; }
	public string? OperatingSystem { get; set; }
	public string? IpAddress { get; set; }
	public string? UserAgent { get; set; }
	public string? Location { get; set; }
}

public class UserStatisticsDto : BaseStatisticsDto
{
	public int EmailVerifiedUsers { get; set; }
	public int EmailUnverifiedUsers { get; set; }
	public int RecentlyCreatedUsers { get; set; } // Last 30 days
}

public class RegisterDto
{
	public OrganizationRegisterDto Organization { get; set; } = new();
	public AdminUserRegisterDto AdminUser { get; set; } = new();
	public bool CreateDemoData { get; set; } = false;
}

public class OrganizationRegisterDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string? Email { get; set; }
	public string? Phone { get; set; }
	public string? Address { get; set; }
	public string? City { get; set; }
	public string? State { get; set; }
	public string? Country { get; set; }
	public string? PostalCode { get; set; }
}

public class AdminUserRegisterDto
{
	public string Email { get; set; } = string.Empty;
	public string Password { get; set; } = string.Empty;
	public string FirstName { get; set; } = string.Empty;
	public string LastName { get; set; } = string.Empty;
	public string? FullName { get; set; }
	public bool IsActive { get; set; } = true;
}

public class RegisterResponseDto
{
	public Guid OrganizationId { get; set; }
	public string OrganizationName { get; set; } = string.Empty;
	public Guid UserId { get; set; }
	public string UserEmail { get; set; } = string.Empty;
	public string UserFullName { get; set; } = string.Empty;
	public string Message { get; set; } = "Registration successful";
}
