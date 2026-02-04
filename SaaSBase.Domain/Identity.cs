using System;
using System.Collections.Generic;

namespace SaaSBase.Domain;

public class User : BaseEntity, ITenantEntity, IAggregateRoot
{
	public string Email { get; set; } = string.Empty;
	public string PasswordHash { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
	public string? FirstName { get; set; }
	public string? LastName { get; set; }
	public string? PhoneNumber { get; set; }
	public bool IsActive { get; set; } = true;
	public bool IsEmailVerified { get; set; } = false;
	public bool IsPhoneVerified { get; set; } = false;
	public DateTimeOffset? LastLoginAt { get; set; }
	public DateTimeOffset? PasswordChangedAt { get; set; }
	public int FailedLoginAttempts { get; set; } = 0;
	public DateTimeOffset? LockedUntil { get; set; }
	public string? AvatarUrl { get; set; }
	public string? TimeZone { get; set; }
	public string? Language { get; set; } = "en";
	public string? Theme { get; set; } = "light"; // light, dark
	public string? Notes { get; set; }
	public bool IsMfaEnabled { get; set; } = false;
	public DateTimeOffset? LastPasswordChange { get; set; }
	public DateTimeOffset? PasswordExpiresAt { get; set; }
	public bool MustChangePasswordOnNextLogin { get; set; } = false;
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
	public DateTimeOffset? LastModifiedAtUtc { get; set; }

	// Navigation properties
	public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
	public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
	public ICollection<MfaSettings> MfaSettings { get; set; } = new List<MfaSettings>();
	public ICollection<UserActivityLog> ActivityLogs { get; set; } = new List<UserActivityLog>();
	public UserNotificationPreferences? NotificationPreferences { get; set; }
}

public class Role : BaseEntity, ITenantEntity
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string RoleType { get; set; } = "CUSTOM"; // SYSTEM, CUSTOM, INHERITED
	public Guid? ParentRoleId { get; set; }
	public int Level { get; set; } = 0;
	public bool IsSystemRole { get; set; } = false;
	public bool IsActive { get; set; } = true;
	public int SortOrder { get; set; } = 0;
	public string? Color { get; set; }
	public string? Icon { get; set; }
	public DateTimeOffset? LastModifiedAtUtc { get; set; }
	//public Guid? UpdatedBy { get; set; }
	//public DateTimeOffset? UpdatedAtUtc { get; set; }

	// Navigation properties
	public Role? ParentRole { get; set; }
	public ICollection<Role> ChildRoles { get; set; } = new List<Role>();
	public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
	public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission : BaseEntity, ITenantEntity
{
	public string Code { get; set; } = string.Empty; // e.g., Users.Read
	public string Name { get; set; } = string.Empty; // e.g., "Read Users"
	public string? Description { get; set; }
	public string Module { get; set; } = string.Empty; // e.g., "Users", "Products", "Inventory"
	public string Action { get; set; } = string.Empty; // e.g., "Read", "Write", "Delete", "Execute"
	public string Resource { get; set; } = string.Empty; // e.g., "Users", "Products"
	public bool IsSystemPermission { get; set; } = false;
	public bool IsSystemAdminOnly { get; set; } = false; // If true, only System Admin can see/use this permission
	public bool IsActive { get; set; } = true;
	public int SortOrder { get; set; } = 0;
	public string? Category { get; set; } // e.g., "CRUD", "REPORTS", "SETTINGS"
	
	// ✅ Menu Foreign Key (Required)
	public Guid MenuId { get; set; }
	public Menu Menu { get; set; } = null!;

	// Navigation properties
	public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class UserRole : BaseEntity, ITenantEntity
{
	public Guid UserId { get; set; }
	public User User { get; set; } = null!;

	public Guid RoleId { get; set; }
	public Role Role { get; set; } = null!;
}

public class RolePermission : BaseEntity, ITenantEntity
{
	public Guid RoleId { get; set; }
	public Role Role { get; set; } = null!;

	public Guid PermissionId { get; set; }
	public Permission Permission { get; set; } = null!;
}

public class UserImportHistory : BaseEntity, ITenantEntity
{
	public string FileName { get; set; } = string.Empty;
	public int TotalRows { get; set; }
	public int SuccessCount { get; set; }
	public int UpdatedCount { get; set; }
	public int SkippedCount { get; set; }
	public int ErrorCount { get; set; }
	public string DuplicateStrategy { get; set; } = "Skip"; // Skip, Update, CreateNew
	public string? ErrorSummary { get; set; } // JSON string of errors
	public string? ErrorReportId { get; set; } // ID for downloading detailed error report
	public Guid ImportedByUserId { get; set; }
	public string ImportedByUserName { get; set; } = string.Empty;
	public DateTimeOffset ImportedAt { get; set; }
	public long FileSizeBytes { get; set; }
	public string Status { get; set; } = "Completed"; // Processing, Completed, Failed
}


