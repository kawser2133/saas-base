using System;

namespace SaaSBase.Domain;

public class Position : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? Level { get; set; } // Junior, Mid, Senior, Lead, Manager, Director, etc.
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
