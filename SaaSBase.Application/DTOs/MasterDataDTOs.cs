using System;

namespace SaaSBase.Application.DTOs;

public class DepartmentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastModifiedAtUtc { get; set; }
    
    // Organization context (for System Admin view)
    public Guid OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
}

public class CreateDepartmentDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class UpdateDepartmentDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public Guid? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class PositionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? Level { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastModifiedAtUtc { get; set; }
    
    // Organization context (for System Admin view)
    public Guid OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
}

public class CreatePositionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? Level { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class UpdatePositionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? Level { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class LocationDropdownDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class DepartmentDropdownDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class DepartmentStatisticsDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
}

public class PositionStatisticsDto
{
    public int Total { get; set; }
    public int Active { get; set; }
    public int Inactive { get; set; }
}

public class DepartmentDropdownOptionsDto
{
    public List<string> Levels { get; set; } = new();
}

public class PositionDropdownOptionsDto
{
    public List<string> Levels { get; set; } = new();
    public List<DepartmentDropdownDto> Departments { get; set; } = new();
}