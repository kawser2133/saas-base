using System;

namespace SaaSBase.Application.DTOs;

public class RoleDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string RoleType { get; set; } = string.Empty;
	public Guid? ParentRoleId { get; set; }
	public int Level { get; set; }
	public bool IsSystemRole { get; set; }
	public bool IsActive { get; set; }
	public int SortOrder { get; set; }
	public string? Color { get; set; }
	public string? Icon { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	public DateTimeOffset? LastModifiedAtUtc { get; set; }
	public string? CreatedBy { get; set; } // Keep for backward compatibility
	public string? UpdatedBy { get; set; } // Keep for backward compatibility
	public DateTimeOffset? UpdatedAtUtc { get; set; }
	public Guid? CreatedById { get; set; }
	public string? CreatedByName { get; set; }
	public Guid? ModifiedById { get; set; }
	public string? ModifiedByName { get; set; }
	
	// Additional properties for enhanced functionality
	public int PermissionCount { get; set; }
	public List<string> PermissionNames { get; set; } = new();
	
	// Navigation properties
	public RoleDto? ParentRole { get; set; }
	public List<RoleDto> ChildRoles { get; set; } = new();
	public List<PermissionDto> Permissions { get; set; } = new();
	public int UserCount { get; set; }
	
	// Organization context (for System Admin view)
	public Guid OrganizationId { get; set; }
	public string? OrganizationName { get; set; }
}

public class CreateRoleDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string RoleType { get; set; } = "CUSTOM";
	public Guid? ParentRoleId { get; set; }
	public int SortOrder { get; set; } = 0;
	public string? Color { get; set; }
	public string? Icon { get; set; }
	public bool IsActive { get; set; } = true;
}

public class UpdateRoleDto
{
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string RoleType { get; set; } = string.Empty;
	public Guid? ParentRoleId { get; set; }
	public bool IsActive { get; set; }
	public int SortOrder { get; set; }
	public string? Color { get; set; }
	public string? Icon { get; set; }
}

public class PermissionDto
{
	public Guid Id { get; set; }
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Module { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public string Resource { get; set; } = string.Empty;
	public bool IsSystemPermission { get; set; }
	public bool IsSystemAdminOnly { get; set; }
	public bool IsActive { get; set; }
	public int SortOrder { get; set; }
	public string? Category { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public string? CreatedBy { get; set; } // Keep for backward compatibility
    public string? UpdatedBy { get; set; } // Keep for backward compatibility
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? ModifiedById { get; set; }
    public string? ModifiedByName { get; set; }

    // ✅ Menu Foreign Key (Required)
    public Guid MenuId { get; set; }
	public MenuDto? Menu { get; set; }
}

public class CreatePermissionDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Module { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public string Resource { get; set; } = string.Empty;
	public int SortOrder { get; set; } = 0;
	public string? Category { get; set; }
	
	// ✅ Menu Foreign Key (Required)
	public Guid MenuId { get; set; }
	
	// System Admin only flag
	public bool IsSystemAdminOnly { get; set; } = false;
}

public class UpdatePermissionDto
{
	public string Code { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public string Module { get; set; } = string.Empty;
	public string Action { get; set; } = string.Empty;
	public string Resource { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int SortOrder { get; set; }
	public string? Category { get; set; }
	
	// ✅ Menu Foreign Key (Required)
	public Guid MenuId { get; set; }
	
	// System Admin only flag
	public bool IsSystemAdminOnly { get; set; } = false;
}

public class AssignRoleRequest
{
	public Guid UserId { get; set; }
	public Guid RoleId { get; set; }
}

public class AssignPermissionRequest
{
	public Guid RoleId { get; set; }
	public Guid PermissionId { get; set; }
}

public class RoleImportResultDto
{
	public int TotalRows { get; set; }
	public int SuccessCount { get; set; }
	public int ErrorCount { get; set; }
	public List<string> Errors { get; set; } = new();
	public List<RoleDto> ImportedRoles { get; set; } = new();
    public string? ErrorReportId { get; set; }
}

// Simple file interface to avoid AspNetCore dependency in Application layer
public interface IRoleFileUpload
{
	string FileName { get; }
	long Length { get; }
	Task<Stream> OpenReadStreamAsync();
}

public class RoleStatisticsDto
{
    public int TotalRoles { get; set; }
    public int ActiveRoles { get; set; }
    public int InactiveRoles { get; set; }
    public int SystemRoles { get; set; }
    public int BusinessRoles { get; set; }
}

public class PermissionStatisticsDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
}

public class RoleImportHistoryDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorReportId { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; set; }
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = "Completed";
    public string DuplicateHandlingStrategy { get; set; } = "Skip";
    public int Progress { get; set; }
}

public class PermissionImportHistoryDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? ErrorReportId { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; set; }
    public long FileSizeBytes { get; set; }
}

// Unified import/export history DTO for permissions
public class PermissionImportExportHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? DuplicateHandlingStrategy { get; set; }
    public string? ErrorReportId { get; set; }
    public string? AppliedFilters { get; set; }
    public long FileSizeBytes { get; set; }
    public string ImportedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
}

// Export job status DTO
public class PermissionExportJobStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public string? Message { get; set; }
    public string? DownloadUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

// Permission list item DTO for optimized list views
public class PermissionListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public bool IsSystemPermission { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? Category { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool ShowDropdown { get; set; }
    public bool DropdownUp { get; set; }
}

// Permission dropdown options DTO
public class PermissionDropdownOptionsDto
{
    public List<string> Modules { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}

public class ImportPermissionsResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<PermissionDto> ImportedPermissions { get; set; } = new();
    public string? ErrorReportId { get; set; }
}

public class PermissionExportDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class PermissionImportTemplateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? Category { get; set; }
    public bool IsActive { get; set; } = true;
}

public class RoleDropdownOptionsDto
{
    public List<RoleDropdownDto> Roles { get; set; } = new();
}

public class RoleDropdownDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoleType { get; set; } = string.Empty;
}

// Bulk operations
public class RoleBulkDeleteRequest
{
    public List<Guid> Ids { get; set; } = new();
}

// Import job status tracking
public class RoleImportJobStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Pending, Processing, Completed, Failed
    public int ProgressPercent { get; set; }
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int SkippedCount { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

// Export-specific DTO for clean export data
public class RoleExportDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// Generic paginated result
public class RolePagedResultDto<T>
{
	public List<T> Items { get; set; } = new();
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalCount { get; set; }
	public int TotalPages { get; set; }
}

// Import template DTO
public class RoleImportTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleType { get; set; } = "CUSTOM";
    public int SortOrder { get; set; } = 0;
    public string Color { get; set; } = "#3B82F6";
    public string Icon { get; set; } = "fas fa-user";
    public bool IsActive { get; set; } = true;
    public string Permissions { get; set; } = string.Empty;
}

// Role list item DTO for optimized list views
public class RoleListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int PermissionCount { get; set; }
    public int UserCount { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? ModifiedById { get; set; }
    public string? ModifiedByName { get; set; }
}

// Role detail DTO for detailed views
public class RoleDetailDto : RoleDto
{
    public List<PermissionDto> AssignedPermissions { get; set; } = new();
    public List<UserDto> AssignedUsers { get; set; } = new();
    public List<RoleDto> ChildRoles { get; set; } = new();
    public RoleDto? ParentRole { get; set; }
}

// Role assignment DTOs
public class RoleAssignmentDto
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTimeOffset AssignedAtUtc { get; set; }
    public Guid AssignedBy { get; set; }
}

public class PermissionAssignmentDto
{
    public Guid PermissionId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTimeOffset AssignedAtUtc { get; set; }
    public Guid AssignedBy { get; set; }
}

// Role hierarchy DTO
public class RoleHierarchyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RoleType { get; set; } = string.Empty;
    public Guid? ParentRoleId { get; set; }
    public int Level { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public List<RoleHierarchyDto> Children { get; set; } = new();
    public int UserCount { get; set; }
    public int PermissionCount { get; set; }
}

// Role validation DTOs
public class RoleValidationDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// Role audit DTO
public class RoleAuditDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Created, Updated, Deleted, Activated, Deactivated
    public string ChangedBy { get; set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; set; }
    public Dictionary<string, object> Changes { get; set; } = new();
    public string? Reason { get; set; }
}