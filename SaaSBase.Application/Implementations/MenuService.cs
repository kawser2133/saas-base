using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;

namespace SaaSBase.Application.Implementations;

public class MenuService : IMenuService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
	private readonly ICacheService _cacheService;
	private readonly IPermissionService _permissionService;
	private readonly IImportExportService _importExportService;
	private readonly IServiceScopeFactory _serviceScopeFactory;

	public MenuService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, ICacheService cacheService, IPermissionService permissionService, IImportExportService importExportService, IServiceScopeFactory serviceScopeFactory)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_cacheService = cacheService;
		_permissionService = permissionService;
		_importExportService = importExportService;
		_serviceScopeFactory = serviceScopeFactory;
	}

	public async Task<PagedResultDto<MenuDto>> GetMenusAsync(string? search, string? section, Guid? parentMenuId, bool? isActive, DateTime? createdFrom, DateTime? createdTo, int page, int pageSize, string? sortField = null, string? sortDirection = "asc")
	{
		var cacheKey = _cacheService.GenerateListCacheKey("menus", Guid.Empty, page, pageSize,
			search, sortField, sortDirection, section, parentMenuId, isActive, createdFrom, createdTo);

		var cachedResult = await _cacheService.GetCachedAsync<PagedResultDto<MenuDto>>(cacheKey);
		if (cachedResult != null)
		{
			return cachedResult;
		}

		var query = _unitOfWork.Repository<Menu>().GetQueryable()
			.Where(m => !m.IsDeleted);

		if (!string.IsNullOrEmpty(search))
		{
			var searchLower = search.ToLower();
			query = query.Where(m =>
				m.Label.ToLower().Contains(searchLower) ||
				(m.Description != null && m.Description.ToLower().Contains(searchLower)) ||
				m.Route.ToLower().Contains(searchLower));
		}

		if (!string.IsNullOrEmpty(section))
			query = query.Where(m => m.Section == section);

		if (parentMenuId.HasValue)
			query = query.Where(m => m.ParentMenuId == parentMenuId.Value);
		// Don't filter by parentMenuId when not specified - show all menus

		if (isActive.HasValue)
			query = query.Where(m => m.IsActive == isActive.Value);

		if (createdFrom.HasValue)
			query = query.Where(m => m.CreatedAtUtc >= createdFrom.Value);

		if (createdTo.HasValue)
			query = query.Where(m => m.CreatedAtUtc <= createdTo.Value.AddDays(1).AddTicks(-1)); // Include the entire day

		var totalCount = await query.CountAsync();

		query = ApplySorting(query, sortField, sortDirection);

		var menus = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(m => new MenuDto
			{
				Id = m.Id,
				Label = m.Label,
				Route = m.Route,
				Icon = m.Icon,
				Section = m.Section,
				ParentMenuId = m.ParentMenuId,
				SortOrder = m.SortOrder,
				IsActive = m.IsActive,
				Description = m.Description,
				Badge = m.Badge,
				BadgeColor = m.BadgeColor,
				IsSystemMenu = m.IsSystemMenu,
				CreatedAtUtc = m.CreatedAtUtc
			})
			.ToListAsync();

		var result = new PagedResultDto<MenuDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			Items = menus
		};

		await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
		return result;
	}

	public async Task<MenuDto?> GetMenuByIdAsync(Guid id)
	{
		var cacheKey = $"menu:detail:{id}";
		
		var cachedResult = await _cacheService.GetCachedAsync<MenuDto>(cacheKey);
		if (cachedResult != null) return cachedResult;

		var menu = await _unitOfWork.Repository<Menu>().GetQueryable()
			.Where(m => m.Id == id && !m.IsDeleted)
			.Select(m => new MenuDto
			{
				Id = m.Id,
				Label = m.Label,
				Route = m.Route,
				Icon = m.Icon,
				Section = m.Section,
				ParentMenuId = m.ParentMenuId,
				SortOrder = m.SortOrder,
				IsActive = m.IsActive,
				Description = m.Description,
				Badge = m.Badge,
				BadgeColor = m.BadgeColor,
				IsSystemMenu = m.IsSystemMenu,
				CreatedAtUtc = m.CreatedAtUtc
			})
			.FirstOrDefaultAsync();

		if (menu != null)
		{
			await _cacheService.SetCacheAsync(cacheKey, menu, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
		}

		return menu;
	}

	public async Task<MenuDto> CreateMenuAsync(CreateMenuDto dto)
	{
		var menuRepo = _unitOfWork.Repository<Menu>();

		if (dto.ParentMenuId.HasValue)
		{
			var parentMenuExists = await menuRepo.GetQueryable()
				.AsNoTracking()
				.AnyAsync(m => m.Id == dto.ParentMenuId.Value && !m.IsDeleted);
			if (!parentMenuExists)
				throw new ArgumentException($"Parent menu with ID {dto.ParentMenuId.Value} not found");
		}

		var menu = new Menu
		{
			Id = Guid.NewGuid(),
			OrganizationId = Guid.Empty,
			Label = dto.Label,
			Route = dto.Route,
			Icon = dto.Icon,
			Section = dto.Section,
			ParentMenuId = dto.ParentMenuId,
			SortOrder = dto.SortOrder,
			IsActive = dto.IsActive,
			Description = dto.Description,
			Badge = dto.Badge,
			BadgeColor = dto.BadgeColor,
			IsSystemMenu = dto.IsSystemMenu
		};

		await menuRepo.AddAsync(menu);
		await _unitOfWork.SaveChangesAsync();

		await InvalidateAllMenuCachesAsync();
		await InvalidatePermissionCachesForMenuAsync(menu.Id);

		return MapToDto(menu);
	}

	public async Task<MenuDto> UpdateMenuAsync(Guid id, UpdateMenuDto dto)
	{
		var menuRepo = _unitOfWork.Repository<Menu>();
		
		var menu = await menuRepo.FindAsync(m => m.Id == id && !m.IsDeleted);
		if (menu == null) throw new ArgumentException("Menu not found");

		// For system menus, only allow updates to safe properties (not Label, Route, Section, ParentMenuId)
		if (menu.IsSystemMenu)
		{
			// Check if trying to change protected properties (normalize null/empty/whitespace for comparison)
			var normalizedDtoLabel = string.IsNullOrWhiteSpace(dto.Label) ? string.Empty : dto.Label.Trim();
			var normalizedMenuLabel = string.IsNullOrWhiteSpace(menu.Label) ? string.Empty : menu.Label.Trim();
			var normalizedDtoRoute = string.IsNullOrWhiteSpace(dto.Route) ? string.Empty : dto.Route.Trim();
			var normalizedMenuRoute = string.IsNullOrWhiteSpace(menu.Route) ? string.Empty : menu.Route.Trim();
			var normalizedDtoSection = string.IsNullOrWhiteSpace(dto.Section) ? null : dto.Section.Trim();
			var normalizedMenuSection = string.IsNullOrWhiteSpace(menu.Section) ? null : menu.Section.Trim();
			
			var labelChanged = !string.Equals(normalizedDtoLabel, normalizedMenuLabel, StringComparison.Ordinal);
			var routeChanged = !string.Equals(normalizedDtoRoute, normalizedMenuRoute, StringComparison.Ordinal);
			var sectionChanged = !string.Equals(normalizedDtoSection, normalizedMenuSection, StringComparison.Ordinal); // Null-aware comparison
			var parentMenuIdChanged = dto.ParentMenuId != menu.ParentMenuId;
			
			if (labelChanged || routeChanged || sectionChanged || parentMenuIdChanged)
			{
				throw new InvalidOperationException("Cannot update Label, Route, Section, or ParentMenuId for system menus. Only IsActive, SortOrder, Icon, Description, Badge, and BadgeColor can be updated.");
			}
			
			// Allow safe updates for system menus
			menu.Icon = dto.Icon;
			menu.SortOrder = dto.SortOrder;
			menu.IsActive = dto.IsActive;
			menu.Description = dto.Description;
			menu.Badge = dto.Badge;
			menu.BadgeColor = dto.BadgeColor;
		}
		else
		{
			// For non-system menus, allow all updates
			// Validate parent menu if specified - use AsNoTracking for read-only validation
			if (dto.ParentMenuId.HasValue && dto.ParentMenuId != menu.ParentMenuId)
			{
				var parentMenuExists = await menuRepo.GetQueryable()
					.AsNoTracking()
					.AnyAsync(m => m.Id == dto.ParentMenuId.Value && !m.IsDeleted);
				if (!parentMenuExists)
					throw new ArgumentException($"Parent menu with ID {dto.ParentMenuId.Value} not found");
				
				// Prevent circular reference
				if (dto.ParentMenuId == id)
					throw new InvalidOperationException("Menu cannot be its own parent");
			}

			menu.Label = dto.Label;
			menu.Route = dto.Route;
			menu.Icon = dto.Icon;
			menu.Section = dto.Section;
			menu.ParentMenuId = dto.ParentMenuId;
			menu.SortOrder = dto.SortOrder;
			menu.IsActive = dto.IsActive;
			menu.Description = dto.Description;
			menu.Badge = dto.Badge;
			menu.BadgeColor = dto.BadgeColor;
		}

		menuRepo.Update(menu);
		await _unitOfWork.SaveChangesAsync();

		await InvalidateAllMenuCachesAsync();
		await _cacheService.RemoveCacheAsync($"menu:detail:{id}");
		await InvalidatePermissionCachesForMenuAsync(id);

		return MapToDto(menu);
	}

	public async Task<bool> DeleteMenuAsync(Guid id)
	{
		var userId = _tenantService.GetCurrentUserId();

		var menu = await _unitOfWork.Repository<Menu>().GetQueryable()
			.FirstOrDefaultAsync(m => m.Id == id && !m.IsDeleted);

		if (menu == null)
			return false;

		if (menu.IsSystemMenu)
			throw new InvalidOperationException("Cannot delete system menu");

		var childCount = await _unitOfWork.Repository<Menu>().GetQueryable()
			.CountAsync(m => m.ParentMenuId == id && !m.IsDeleted);

		if (childCount > 0)
			throw new InvalidOperationException("Cannot delete menu that has child menus. Please delete or reassign child menus first.");

		var permissionCount = await _unitOfWork.Repository<Permission>().GetQueryable()
			.CountAsync(p => p.MenuId == id && !p.IsDeleted);

		if (permissionCount > 0)
			throw new InvalidOperationException("Cannot delete menu that is assigned to permissions. Please reassign permissions to other menus first.");

		menu.IsDeleted = true;
		menu.DeletedAtUtc = DateTimeOffset.UtcNow;
		menu.ModifiedAtUtc = DateTimeOffset.UtcNow;
		menu.ModifiedBy = userId.ToString();

		_unitOfWork.Repository<Menu>().Update(menu);
		await _unitOfWork.SaveChangesAsync();

		await InvalidateAllMenuCachesAsync();
		await _cacheService.RemoveCacheAsync($"menu:detail:{id}");
		await InvalidatePermissionCachesForMenuAsync(id);

		return true;
	}

	public async Task BulkDeleteAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return;

		var userId = _tenantService.GetCurrentUserId();

		var menus = await _unitOfWork.Repository<Menu>().GetQueryable()
			.Where(m => ids.Contains(m.Id) && !m.IsDeleted)
			.ToListAsync();

		if (!menus.Any())
			return;

		var systemMenus = menus.Where(m => m.IsSystemMenu).ToList();
		if (systemMenus.Any())
		{
			throw new InvalidOperationException($"Cannot delete system menus: {string.Join(", ", systemMenus.Select(m => m.Label))}");
		}

		var hasChildren = menus.Any(m => _unitOfWork.Repository<Menu>().GetQueryable()
			.Any(cm => cm.ParentMenuId == m.Id && !cm.IsDeleted));

		if (hasChildren)
		{
			throw new InvalidOperationException("Cannot delete menus that have child menus");
		}

		var usedMenus = await _unitOfWork.Repository<Permission>().GetQueryable()
			.Where(p => ids.Contains(p.MenuId) && !p.IsDeleted)
			.Select(p => p.MenuId)
			.Distinct()
			.ToListAsync();

		if (usedMenus.Any())
		{
			throw new InvalidOperationException($"Cannot delete menus that are assigned to permissions: {string.Join(", ", usedMenus)}");
		}

		foreach (var menu in menus)
		{
			menu.IsDeleted = true;
			menu.DeletedAtUtc = DateTimeOffset.UtcNow;
			menu.ModifiedAtUtc = DateTimeOffset.UtcNow;
			menu.ModifiedBy = userId.ToString();
			_unitOfWork.Repository<Menu>().Update(menu);
		}

		await _unitOfWork.SaveChangesAsync();

		await InvalidateAllMenuCachesAsync();
		foreach (var id in ids)
		{
			await _cacheService.RemoveCacheAsync($"menu:detail:{id}");
			await InvalidatePermissionCachesForMenuAsync(id);
		}
	}

	public async Task<List<MenuDto>> BulkCloneAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return new List<MenuDto>();

		var userId = _tenantService.GetCurrentUserId();
		var menuRepo = _unitOfWork.Repository<Menu>();

		// Get original menus
		var originalMenus = await menuRepo.GetQueryable()
			.Where(m => ids.Contains(m.Id) && !m.IsDeleted)
			.ToListAsync();

		if (!originalMenus.Any())
			return new List<MenuDto>();

		var clonedMenus = new List<MenuDto>();
		var generatedLabels = new HashSet<string>(); // Track labels in current batch
		var generatedRoutes = new HashSet<string>(); // Track routes in current batch
		var clonedMenuEntities = new List<Menu>(); // Store menu entities before saving

		// First, get all existing menu labels and routes from database to avoid conflicts
		var existingLabels = await menuRepo.GetQueryable()
			.Where(m => !m.IsDeleted)
			.Select(m => m.Label)
			.ToListAsync();
		var existingRoutes = await menuRepo.GetQueryable()
			.Where(m => !m.IsDeleted)
			.Select(m => m.Route)
			.ToListAsync();
		
		generatedLabels.UnionWith(existingLabels);
		generatedRoutes.UnionWith(existingRoutes);

		foreach (var originalMenu in originalMenus)
		{
			// Generate unique menu label with GUID to ensure uniqueness
			var baseLabel = originalMenu.Label;
			var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
			var newLabel = $"{baseLabel} (Copy {uniqueSuffix})";
			var counter = 1;
			
			// Check if label already exists in current batch (includes database labels)
			while (generatedLabels.Contains(newLabel))
			{
				uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
				newLabel = $"{baseLabel} (Copy {uniqueSuffix})";
				counter++;
				if (counter > 100) break; // Safety limit
			}
			
			generatedLabels.Add(newLabel); // Track this label for current batch

			// Generate unique route if route exists
			string? newRoute = null;
			if (!string.IsNullOrWhiteSpace(originalMenu.Route))
			{
				var baseRoute = originalMenu.Route;
				uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
				newRoute = $"{baseRoute}-copy-{uniqueSuffix}";
				counter = 1;
				
				// Check if route already exists in current batch (includes database routes)
				while (generatedRoutes.Contains(newRoute))
				{
					uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
					newRoute = $"{baseRoute}-copy-{uniqueSuffix}";
					counter++;
					if (counter > 100) break; // Safety limit
				}
				
				generatedRoutes.Add(newRoute); // Track this route for current batch
			}

			// Create cloned menu
			var clonedMenu = new Menu
			{
				Id = Guid.NewGuid(),
				Label = newLabel,
				Route = newRoute ?? originalMenu.Route,
				Icon = originalMenu.Icon,
				Section = originalMenu.Section,
				ParentMenuId = originalMenu.ParentMenuId, // Keep same parent
				SortOrder = originalMenu.SortOrder,
				IsActive = false, // Cloned menus start as inactive
				Description = originalMenu.Description,
				Badge = originalMenu.Badge,
				BadgeColor = originalMenu.BadgeColor,
				IsSystemMenu = false, // Cloned menus are never system menus
				OrganizationId = Guid.Empty, // Menus are system-wide
				CreatedBy = userId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow,
				ModifiedBy = userId.ToString(),
				ModifiedAtUtc = DateTimeOffset.UtcNow
			};

			clonedMenuEntities.Add(clonedMenu);
		}

		// Add all menu entities at once
		foreach (var clonedMenu in clonedMenuEntities)
		{
			await menuRepo.AddAsync(clonedMenu);
		}

		// Save all cloned menus in a single transaction
		await _unitOfWork.SaveChangesAsync();

		// Build DTOs after saving
		foreach (var clonedMenu in clonedMenuEntities)
		{
			clonedMenus.Add(MapToDto(clonedMenu));
		}

		// Invalidate caches
		await InvalidateAllMenuCachesAsync();
		foreach (var clonedMenu in clonedMenuEntities)
		{
			await _cacheService.RemoveCacheAsync($"menu:detail:{clonedMenu.Id}");
			await InvalidatePermissionCachesForMenuAsync(clonedMenu.Id);
		}

		return clonedMenus;
	}

	public async Task<bool> SetActiveAsync(Guid id, bool isActive)
	{
		var menuRepo = _unitOfWork.Repository<Menu>();
		
		var menu = await menuRepo.FindAsync(m => m.Id == id && !m.IsDeleted);
		if (menu == null) return false;

		menu.IsActive = isActive;
		menuRepo.Update(menu);
		await _unitOfWork.SaveChangesAsync();

		await InvalidateAllMenuCachesAsync();
		await _cacheService.RemoveCacheAsync($"menu:detail:{id}");
		await InvalidatePermissionCachesForMenuAsync(id);

		return true;
	}

	public async Task<List<MenuDropdownDto>> GetMenuDropdownOptionsAsync()
	{
		var cacheKey = $"menus:dropdown:global";

		var cached = await _cacheService.GetCachedAsync<List<MenuDropdownDto>>(cacheKey);
		if (cached != null) return cached;

		var menus = await _unitOfWork.Repository<Menu>().GetQueryable()
			.Where(m => !m.IsDeleted && m.IsActive)
			.OrderBy(m => m.Section)
			.ThenBy(m => m.SortOrder)
			.Select(m => new MenuDropdownDto
			{
				Id = m.Id,
				Label = m.Label,
				Route = m.Route,
				Section = m.Section
			})
			.ToListAsync();

		await _cacheService.SetCacheAsync(cacheKey, menus, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
		return menus;
	}

	public async Task<List<string>> GetUniqueSectionsAsync()
	{
		var repo = _unitOfWork.Repository<Menu>();
		
		var menus = await repo.FindManyAsync(m => !m.IsDeleted && m.IsActive && !string.IsNullOrEmpty(m.Section));
		
		return menus
			.Select(m => m.Section!)
			.Distinct()
			.OrderBy(s => s)
			.ToList();
	}

	public async Task<List<MenuDto>> GetMenusBySectionAsync(string section)
	{
		var repo = _unitOfWork.Repository<Menu>();
		
		var menus = await repo.FindManyAsync(m => m.Section == section && !m.IsDeleted);
		
		return menus
			.OrderBy(m => m.SortOrder)
			.Select(MapToDto)
			.ToList();
	}

	public async Task<List<MenuDto>> GetChildMenusAsync(Guid parentMenuId)
	{
		var repo = _unitOfWork.Repository<Menu>();
		
		var menus = await repo.FindManyAsync(m => m.ParentMenuId == parentMenuId && !m.IsDeleted);
		
		return menus
			.OrderBy(m => m.SortOrder)
			.Select(MapToDto)
			.ToList();
	}

	public async Task<UserMenuResponseDto> GetUserMenusAsync(Guid userId)
	{
		var OrganizationId = _tenantService.GetCurrentOrganizationId();
		var cacheKey = $"user_menus_{userId}_{OrganizationId}";

		var cachedMenus = await _cacheService.GetCachedAsync<UserMenuResponseDto>(cacheKey);
		if (cachedMenus != null)
		{
			return cachedMenus;
		}

		var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
		
		var isSystemAdmin = await _unitOfWork.Repository<UserRole>().GetQueryable()
			.Include(ur => ur.Role)
			.AnyAsync(ur => ur.UserId == userId
				&& ur.Role != null
				&& ur.Role.OrganizationId == systemOrganizationId
				&& ur.Role.Name == "System Administrator"
				&& ur.Role.IsActive
				&& !ur.IsDeleted);

		List<MenuItem> menuItems;

		if (isSystemAdmin)
		{
			var allMenus = await _unitOfWork.Repository<Menu>().GetQueryable()
				.Where(m => !m.IsDeleted && m.IsActive)
				.Select(m => new
				{
					MenuId = m.Id,
					Label = m.Label,
					Route = m.Route,
					Icon = m.Icon,
					Section = m.Section,
					ParentMenuId = m.ParentMenuId,
					SortOrder = m.SortOrder,
					Badge = m.Badge,
					BadgeColor = m.BadgeColor
				})
				.ToListAsync();

			menuItems = allMenus.Select(m => new MenuItem
			{
				MenuId = m.MenuId,
				Label = m.Label,
				Route = m.Route,
				Icon = m.Icon,
				Section = m.Section ?? "OTHER",
				ParentMenuId = m.ParentMenuId,
				SortOrder = m.SortOrder,
				Badge = m.Badge,
				BadgeColor = m.BadgeColor
			}).ToList();
		}
		else
		{
			// Get user's permission codes
			var permissionCodes = await _permissionService.GetUserPermissionCodesAsync(userId);

			// Menus directly tied to user's permissions
			// Permissions are system-wide, filter by system organization ID
			// Filter out System Admin only permissions for Company Admin
			// Use explicit join to ensure Menu is loaded correctly (menus are global, no tenant filter)
			// Ignore query filters for Permission to ensure we can query by systemOrganizationId
			var permissionMenuData = await (from p in _unitOfWork.Repository<Permission>().GetQueryable().IgnoreQueryFilters()
											join m in _unitOfWork.Repository<Menu>().GetQueryable() on p.MenuId equals m.Id
											where p.OrganizationId == systemOrganizationId
												&& !p.IsDeleted
												&& p.IsActive
												&& !p.IsSystemAdminOnly // Filter out System Admin only permissions
												&& permissionCodes.Contains(p.Code)
												&& p.MenuId != Guid.Empty // Ensure MenuId is set
												&& !m.IsDeleted
												&& m.IsActive
											select new
											{
												MenuId = m.Id,
												Label = m.Label,
												Route = m.Route,
												Icon = m.Icon,
												Section = m.Section,
												ParentMenuId = m.ParentMenuId,
												SortOrder = m.SortOrder,
												Badge = m.Badge,
												BadgeColor = m.BadgeColor
											})
											.Distinct()
											.ToListAsync();

			// Build initial set
			menuItems = permissionMenuData.Select(m => new MenuItem
			{
				MenuId = m.MenuId,
				Label = m.Label,
				Route = m.Route,
				Icon = m.Icon,
				Section = m.Section ?? "OTHER",
				ParentMenuId = m.ParentMenuId,
				SortOrder = m.SortOrder,
				Badge = m.Badge,
				BadgeColor = m.BadgeColor
			}).ToList();

			// Ensure ancestor parent menus are included so top-level nodes render
			if (menuItems.Count > 0)
			{
				var allActiveMenus = await _unitOfWork.Repository<Menu>().GetQueryable()
					.Where(m => !m.IsDeleted && m.IsActive)
					.Select(m => new { m.Id, m.ParentMenuId, m.Label, m.Route, m.Icon, m.Section, m.SortOrder, m.Badge, m.BadgeColor })
					.ToListAsync();

				var idToMenu = allActiveMenus.ToDictionary(m => m.Id, m => m);
				var includedIds = menuItems.Select(mi => mi.MenuId).ToHashSet();

				foreach (var item in menuItems.ToList())
				{
					var currentParentId = item.ParentMenuId;
					while (currentParentId.HasValue)
					{
						if (!idToMenu.TryGetValue(currentParentId.Value, out var parent)) break;
						if (!includedIds.Contains(parent.Id))
						{
							menuItems.Add(new MenuItem
							{
								MenuId = parent.Id,
								Label = parent.Label,
								Route = parent.Route,
								Icon = parent.Icon ?? string.Empty,
								Section = parent.Section ?? "OTHER",
								ParentMenuId = parent.ParentMenuId,
								SortOrder = parent.SortOrder,
								Badge = parent.Badge,
								BadgeColor = parent.BadgeColor
							});
							includedIds.Add(parent.Id);
						}
						currentParentId = parent.ParentMenuId;
					}
				}
			}
		}

		// Group by section and build menu structure
		var sections = menuItems
			.GroupBy(m => m.Section)
			.Select(g => new MenuSectionDto
			{
				Title = g.Key,
				Items = BuildMenuHierarchy(g.ToList(), menuItems)
			})
			.OrderBy(s => s.Title)
			.ToList();

		var menuResponse = new UserMenuResponseDto { Sections = sections };

		await _cacheService.SetCacheAsync(cacheKey, menuResponse, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));

		return menuResponse;
	}

	private class MenuItem
	{
		public Guid MenuId { get; set; }
		public string Label { get; set; } = string.Empty;
		public string Route { get; set; } = string.Empty;
		public string Icon { get; set; } = string.Empty;
		public string Section { get; set; } = string.Empty;
		public Guid? ParentMenuId { get; set; }
		public int SortOrder { get; set; }
		public string? Badge { get; set; }
		public string? BadgeColor { get; set; }
	}

	private List<UserMenuDto> BuildMenuHierarchy(
		List<MenuItem> menus,
		List<MenuItem> allMenus)
	{
		var topLevelMenus = menus.Where(m => m.ParentMenuId == null).OrderBy(m => m.SortOrder).ToList();
		var menuItems = new List<UserMenuDto>();

		foreach (var menu in topLevelMenus)
		{
			var menuItem = new UserMenuDto
			{
				Label = menu.Label,
				Icon = menu.Icon,
				Route = menu.Route,
				SortOrder = menu.SortOrder,
				Badge = menu.Badge,
				BadgeColor = menu.BadgeColor,
				Submenu = new List<UserMenuDto>()
			};

			// Find child menus
			var children = allMenus
				.Where(m => m.ParentMenuId == menu.MenuId)
				.OrderBy(m => m.SortOrder)
				.Select(m => new UserMenuDto
				{
					Label = m.Label,
					Icon = m.Icon,
					Route = m.Route,
					SortOrder = m.SortOrder,
					Badge = m.Badge,
					BadgeColor = m.BadgeColor,
					Submenu = new List<UserMenuDto>()
				})
				.ToList();

			menuItem.Submenu = children;
			menuItems.Add(menuItem);
		}

		return menuItems;
	}

	public async Task<MenuStatisticsDto> GetStatisticsAsync()
	{
		var repo = _unitOfWork.Repository<Menu>();
		
		var all = await repo.FindManyAsync(m => !m.IsDeleted);
		var total = all.Count();
		var active = all.Count(m => m.IsActive);
		var systemMenus = all.Count(m => m.IsSystemMenu);
		
		return new MenuStatisticsDto 
		{ 
			Total = total, 
			Active = active, 
			Inactive = total - active,
			SystemMenus = systemMenus
		};
	}

	public async Task<List<MenuDto>> GetMenuHierarchyAsync()
	{
		var repo = _unitOfWork.Repository<Menu>();
		
		var menus = await repo.FindManyAsync(m => !m.IsDeleted);
		
		return menus
			.OrderBy(m => m.Section)
			.ThenBy(m => m.SortOrder)
			.Select(MapToDto)
			.ToList();
	}

	private MenuDto MapToDto(Menu menu)
	{
		return new MenuDto
		{
			Id = menu.Id,
			Label = menu.Label,
			Route = menu.Route,
			Icon = menu.Icon,
			Section = menu.Section,
			ParentMenuId = menu.ParentMenuId,
			SortOrder = menu.SortOrder,
			IsActive = menu.IsActive,
			Description = menu.Description,
			Badge = menu.Badge,
			BadgeColor = menu.BadgeColor,
			IsSystemMenu = menu.IsSystemMenu,
			CreatedAtUtc = menu.CreatedAtUtc
		};
	}

	public async Task<byte[]> GetImportTemplateAsync()
	{
		var repo = _unitOfWork.Repository<Menu>();
		
		var menus = await repo.GetQueryable()
			.Where(m => !m.IsDeleted)
			.Select(m => new { m.Label, m.Route, m.Icon, m.Section })
			.ToListAsync();

		using var workbook = new XLWorkbook();
		var importSheet = workbook.Worksheets.Add("Import Data");
		var referenceSheet = workbook.Worksheets.Add("Reference Data");

		// Headers
		importSheet.Cell(1, 1).Value = "Label";
		importSheet.Cell(1, 2).Value = "Route";
		importSheet.Cell(1, 3).Value = "Icon";
		importSheet.Cell(1, 4).Value = "Section";
		importSheet.Cell(1, 5).Value = "Parent Menu Label";
		importSheet.Cell(1, 6).Value = "Sort Order";
		importSheet.Cell(1, 7).Value = "Status";
		importSheet.Cell(1, 8).Value = "Description";
		importSheet.Cell(1, 9).Value = "Badge";
		importSheet.Cell(1, 10).Value = "Badge Color";

		// Style headers
		var headerRange = importSheet.Range(1, 1, 1, 10);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

		// Status values
		referenceSheet.Cell(1, 1).Value = "Status";
		referenceSheet.Cell(1, 1).Style.Font.Bold = true;
		referenceSheet.Cell(2, 1).Value = "Active";
		referenceSheet.Cell(3, 1).Value = "Inactive";

		workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 1, 3, 1));

		// Add data validation
		var statusValidation = importSheet.Range("G2:G1000").SetDataValidation();
		statusValidation.List("=StatusValues", true);
		statusValidation.IgnoreBlanks = true;
		statusValidation.InCellDropdown = true;

		importSheet.Columns().AdjustToContents();
		referenceSheet.Columns().AdjustToContents();

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}

	// Thread-safe storage for parent menu labels during export
	private static readonly ConcurrentDictionary<Guid, string> _parentMenuLabelCache = new();

	public async Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var userId = _tenantService.GetCurrentUserId();
		var userName = _tenantService.GetCurrentUserName();

		_parentMenuLabelCache.Clear();

		return await _importExportService.StartExportJobAsync<MenuDto>(
			entityType: "Menu",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

				var search = f.ContainsKey("search") ? f["search"]?.ToString() : null;
				var section = f.ContainsKey("section") ? f["section"]?.ToString() : null;
				var parentMenuId = f.ContainsKey("parentMenuId") ? (Guid?)f["parentMenuId"] : null;
				var isActive = f.ContainsKey("isActive") ? (bool?)f["isActive"] : null;
				var createdFrom = f.ContainsKey("createdFrom") && f["createdFrom"] != null ? (DateTime?)Convert.ToDateTime(f["createdFrom"]) : null;
				var createdTo = f.ContainsKey("createdTo") && f["createdTo"] != null ? (DateTime?)Convert.ToDateTime(f["createdTo"]) : null;
				var selectedIds = f.ContainsKey("selectedIds") ? (List<Guid>?)f["selectedIds"] : null;

				var query = scopedUnitOfWork.Repository<Menu>().GetQueryable()
					.Where(m => !m.IsDeleted);

				if (!string.IsNullOrEmpty(search))
				{
					var searchLower = search.ToLower();
					query = query.Where(m =>
						m.Label.ToLower().Contains(searchLower) ||
						(m.Description != null && m.Description.ToLower().Contains(searchLower)) ||
						m.Route.ToLower().Contains(searchLower));
				}

				if (!string.IsNullOrEmpty(section))
					query = query.Where(m => m.Section == section);

				if (parentMenuId.HasValue)
					query = query.Where(m => m.ParentMenuId == parentMenuId.Value);
				// Don't filter by parentMenuId when not specified - export all menus

				if (isActive.HasValue)
					query = query.Where(m => m.IsActive == isActive.Value);

				if (createdFrom.HasValue)
					query = query.Where(m => m.CreatedAtUtc >= createdFrom.Value);

				if (createdTo.HasValue)
					query = query.Where(m => m.CreatedAtUtc <= createdTo.Value.AddDays(1).AddTicks(-1)); // Include the entire day

				if (selectedIds != null && selectedIds.Any())
					query = query.Where(m => selectedIds.Contains(m.Id));

				// Include parent menu for label lookup
				var menusWithParent = await query
					.Include(m => m.ParentMenu)
					.OrderBy(m => m.Section).ThenBy(m => m.SortOrder).ThenBy(m => m.Label)
					.Select(m => new
					{
						Menu = new MenuDto
						{
							Id = m.Id,
							Label = m.Label,
							Route = m.Route,
							Icon = m.Icon,
							Section = m.Section,
							ParentMenuId = m.ParentMenuId,
							SortOrder = m.SortOrder,
							IsActive = m.IsActive,
							Description = m.Description,
							Badge = m.Badge,
							BadgeColor = m.BadgeColor,
							IsSystemMenu = m.IsSystemMenu,
							CreatedAtUtc = m.CreatedAtUtc
						},
						ParentMenuLabel = m.ParentMenu != null ? m.ParentMenu.Label : null
					})
					.ToListAsync();

				// Store parent menu labels in cache for column mapper to access
				foreach (var item in menusWithParent)
				{
					if (item.Menu.ParentMenuId.HasValue && !string.IsNullOrEmpty(item.ParentMenuLabel))
					{
						_parentMenuLabelCache.TryAdd(item.Menu.Id, item.ParentMenuLabel);
					}
				}

				return menusWithParent.Select(x => x.Menu).ToList();
			},
			filters: filters,
			columnMapper: (m) =>
			{
				// Get parent menu label from cache
				var parentMenuLabel = "";
				if (m.ParentMenuId.HasValue)
				{
					_parentMenuLabelCache.TryGetValue(m.Id, out parentMenuLabel);
					if (string.IsNullOrEmpty(parentMenuLabel))
					{
						// Fallback: if not in cache, try to get from ParentMenuId
						// But we can't query here, so just leave it empty or use a lookup
						parentMenuLabel = "";
					}
				}
				
				return new Dictionary<string, object>
				{
					["Label"] = m.Label ?? "",
					["Route"] = m.Route ?? "",
					["Icon"] = m.Icon ?? "",
					["Section"] = m.Section ?? "",
					["Parent Menu Name"] = parentMenuLabel ?? "",
					["Sort Order"] = m.SortOrder.ToString(),
					["Status"] = m.IsActive ? "Active" : "Inactive",
					["Description"] = m.Description ?? "",
					["Badge"] = m.Badge ?? "",
					["Badge Color"] = m.BadgeColor ?? "",
					["Is System Menu"] = m.IsSystemMenu ? "Yes" : "No",
					["Created At"] = m.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
				};
			});
	}

	public async Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetExportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> DownloadExportFileAsync(string jobId)
	{
		return await _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		var userId = _tenantService.GetCurrentUserId();

		return await _importExportService.StartImportJobAsync<CreateMenuDto>(
			entityType: "Menu",
			fileStream: fileStream,
			fileName: fileName,
			rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
			{
				if (!rowData.TryGetValue("Label", out var label) || string.IsNullOrWhiteSpace(label))
					return (false, "Label is required", false, false);

				var repo = scopedUnitOfWork.Repository<Menu>();

				var route = rowData.GetValueOrDefault("Route");
				var existingMenu = !string.IsNullOrWhiteSpace(route)
					? await repo.GetQueryable()
						.AsNoTracking()
						.FirstOrDefaultAsync(m => m.Route.ToLower() == route.ToLower() && !m.IsDeleted)
					: await repo.GetQueryable()
						.AsNoTracking()
						.FirstOrDefaultAsync(m => m.Label.ToLower() == label.ToLower() && !m.IsDeleted);

				Guid? parentMenuId = null;
				var parentMenuLabel = rowData.GetValueOrDefault("Parent Menu Label");
				if (!string.IsNullOrWhiteSpace(parentMenuLabel))
				{
					var parentMenu = await repo.GetQueryable()
						.AsNoTracking()
						.FirstOrDefaultAsync(m => 
							m.Label == parentMenuLabel && 
							!m.IsDeleted);
					if (parentMenu != null)
					{
						parentMenuId = parentMenu.Id;
					}
				}

				if (existingMenu != null)
				{
					if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
						return (true, null, false, true);

					if (duplicateStrategy == DuplicateHandlingStrategy.Update && !existingMenu.IsSystemMenu)
					{
						var menuToUpdate = await repo.GetQueryable()
							.FirstOrDefaultAsync(m => m.Id == existingMenu.Id && !m.IsDeleted);
						
						if (menuToUpdate == null)
							return (false, "Menu not found for update", false, false);

						menuToUpdate.Label = label;
						menuToUpdate.Route = route ?? "";
						menuToUpdate.Icon = rowData.GetValueOrDefault("Icon") ?? "";
						menuToUpdate.Section = rowData.GetValueOrDefault("Section");
						menuToUpdate.ParentMenuId = parentMenuId;
						if (int.TryParse(rowData.GetValueOrDefault("Sort Order"), out var sortOrder))
							menuToUpdate.SortOrder = sortOrder;
						if (rowData.TryGetValue("Status", out var status))
							menuToUpdate.IsActive = status?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;
						menuToUpdate.Description = rowData.GetValueOrDefault("Description");
						menuToUpdate.Badge = rowData.GetValueOrDefault("Badge");
						menuToUpdate.BadgeColor = rowData.GetValueOrDefault("Badge Color");
						menuToUpdate.ModifiedAtUtc = DateTimeOffset.UtcNow;
						menuToUpdate.ModifiedBy = userId.ToString();
						repo.Update(menuToUpdate);
						return (true, null, true, false);
					}
				}

				var newMenu = new Menu
				{
					Id = Guid.NewGuid(),
					OrganizationId = Guid.Empty,
					Label = label,
					Route = route ?? "",
					Icon = rowData.GetValueOrDefault("Icon") ?? "",
					Section = rowData.GetValueOrDefault("Section"),
					ParentMenuId = parentMenuId,
					IsActive = true,
					SortOrder = 0,
					Description = rowData.GetValueOrDefault("Description"),
					Badge = rowData.GetValueOrDefault("Badge"),
					BadgeColor = rowData.GetValueOrDefault("Badge Color"),
					IsSystemMenu = false,
					CreatedAtUtc = DateTimeOffset.UtcNow,
					CreatedBy = userId.ToString()
				};

				if (rowData.TryGetValue("Status", out var statusValue))
					newMenu.IsActive = statusValue?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;
				if (int.TryParse(rowData.GetValueOrDefault("Sort Order"), out var newSortOrder))
					newMenu.SortOrder = newSortOrder;

				await repo.AddAsync(newMenu);
				return (true, null, false, false);
			},
			duplicateStrategy: duplicateStrategy);
	}

	public async Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetImportJobStatusAsync(jobId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		return await _importExportService.GetHistoryAsync("Menu", type, page, pageSize);
	}

	public async Task<byte[]?> GetImportErrorReportAsync(string errorReportId)
	{
		return await _importExportService.GetImportErrorReportAsync(errorReportId);
	}

	private async Task InvalidateAllMenuCachesAsync()
	{
		await _cacheService.RemoveCacheByPatternAsync($"menus:list:*");
		await _cacheService.RemoveCacheByPatternAsync($"menus:dropdown:*");
		await _cacheService.RemoveCacheByPatternAsync($"menus:stats:*");
		await _cacheService.RemoveCacheByPatternAsync($"user_menus_*");
		await _cacheService.RemoveCacheByPatternAsync($"menu:detail:*");
	}

	private async Task InvalidatePermissionCachesForMenuAsync(Guid menuId)
	{
		var organizationId = _tenantService.GetCurrentOrganizationId();
		
		var permissionIds = await _unitOfWork.Repository<Permission>().GetQueryable()
			.Where(p => p.MenuId == menuId && p.OrganizationId == organizationId && !p.IsDeleted)
			.Select(p => p.Id)
			.ToListAsync();

		foreach (var permissionId in permissionIds)
		{
			await _cacheService.RemoveCacheAsync($"permission:detail:{permissionId}");
		}

		await _cacheService.RemoveCacheByPatternAsync($"permissions:list:{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"permissions:stats:{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*_{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"user_menus_*_{organizationId}");
	}

	private IQueryable<Menu> ApplySorting(IQueryable<Menu> query, string? sortField, string? sortDirection)
	{
		if (string.IsNullOrEmpty(sortField))
		{
			return query.OrderBy(m => m.Section).ThenBy(m => m.SortOrder);
		}

		return sortField.ToLower() switch
		{
			"label" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(m => m.Label) : query.OrderBy(m => m.Label),
			"route" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(m => m.Route) : query.OrderBy(m => m.Route),
			"section" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(m => m.Section) : query.OrderBy(m => m.Section),
			"sortorder" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(m => m.SortOrder) : query.OrderBy(m => m.SortOrder),
			"createdat" or "createdatutc" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(m => m.CreatedAtUtc) : query.OrderBy(m => m.CreatedAtUtc),
			"isactive" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(m => m.IsActive) : query.OrderBy(m => m.IsActive),
			_ => query.OrderBy(m => m.Section).ThenBy(m => m.SortOrder)
		};
	}
}

