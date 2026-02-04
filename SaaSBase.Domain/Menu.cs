using System;
using System.Collections.Generic;

namespace SaaSBase.Domain;

public class Menu : BaseEntity
{
    public string Label { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Section { get; set; }
    public Guid? ParentMenuId { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string? Badge { get; set; }
    public string? BadgeColor { get; set; }
    public bool IsSystemMenu { get; set; } = false;

    // Navigation properties
    public Menu? ParentMenu { get; set; }
    public ICollection<Menu> ChildMenus { get; set; } = new List<Menu>();
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}

