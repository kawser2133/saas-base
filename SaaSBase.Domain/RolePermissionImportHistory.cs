using System;

namespace SaaSBase.Domain;

public class RoleImportHistory : BaseEntity, ITenantEntity
{
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

public class PermissionImportHistory : BaseEntity, ITenantEntity
{
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
