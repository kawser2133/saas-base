using System;

namespace SaaSBase.Application.DTOs;

public class MenuDto
{
	public Guid Id { get; set; }
	public string Label { get; set; } = string.Empty;
	public string Route { get; set; } = string.Empty;
	public string Icon { get; set; } = string.Empty;
	public string? Section { get; set; }
	public Guid? ParentMenuId { get; set; }
	public int SortOrder { get; set; }
	public bool IsActive { get; set; }
	public string? Description { get; set; }
	public string? Badge { get; set; }
	public string? BadgeColor { get; set; }
	public bool IsSystemMenu { get; set; }
	public DateTimeOffset CreatedAtUtc { get; set; }
	
	// Navigation properties (optional)
	public MenuDto? ParentMenu { get; set; }
	public List<MenuDto> ChildMenus { get; set; } = new();
}

public class CreateMenuDto
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
}

public class UpdateMenuDto
{
	public string Label { get; set; } = string.Empty;
	public string Route { get; set; } = string.Empty;
	public string Icon { get; set; } = string.Empty;
	public string? Section { get; set; }
	public Guid? ParentMenuId { get; set; }
	public int SortOrder { get; set; }
	public bool IsActive { get; set; }
	public string? Description { get; set; }
	public string? Badge { get; set; }
	public string? BadgeColor { get; set; }
}

// For dropdowns (simplified)
public class MenuDropdownDto
{
	public Guid Id { get; set; }
	public string Label { get; set; } = string.Empty;
	public string Route { get; set; } = string.Empty;
	public string? Section { get; set; }
}

// Menu for user navigation (includes permissions)
public class UserMenuDto
{
	public string Label { get; set; } = string.Empty;
	public string Icon { get; set; } = string.Empty;
	public string Route { get; set; } = string.Empty;
	public string? Section { get; set; }
	public int SortOrder { get; set; }
	public List<UserMenuDto> Submenu { get; set; } = new();
	public string? Badge { get; set; }
	public string? BadgeColor { get; set; }
}

public class MenuSectionDto
{
	public string Title { get; set; } = string.Empty;
	public List<UserMenuDto> Items { get; set; } = new();
}

public class UserMenuResponseDto
{
	public List<MenuSectionDto> Sections { get; set; } = new();
}

public class MenuStatisticsDto
{
	public int Total { get; set; }
	public int Active { get; set; }
	public int Inactive { get; set; }
	public int SystemMenus { get; set; }
}

