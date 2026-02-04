using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Application.Implementations;

public class PermissionService : IPermissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly ICacheService _cacheService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IImportExportService _importExportService;
    private readonly IUserContextService _userContextService;
    private readonly ILogger<PermissionService> _logger;
    private static readonly SemaphoreSlim _permissionCacheSemaphore = new(1, 1);

    public PermissionService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, ICacheService cacheService, IServiceScopeFactory serviceScopeFactory, IImportExportService importExportService, IUserContextService userContextService, ILogger<PermissionService> logger)
    {
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _cacheService = cacheService;
        _serviceScopeFactory = serviceScopeFactory;
        _importExportService = importExportService;
        _userContextService = userContextService;
        _logger = logger;
    }

    public async Task<PagedResultDto<PermissionDto>> GetPermissionsAsync(string? search, string? category, string? module, string? action, bool? isActive, int page, int pageSize, string? sortField = null, string? sortDirection = "desc", DateTime? createdFrom = null, DateTime? createdTo = null)
    {
        // Check if user is System Admin FIRST - needed for cache key
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();

        // Generate cache key - permissions are system-wide, use system organization ID
        var systemOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var cacheKey = _cacheService.GenerateListCacheKey("permissions", systemOrgId, page, pageSize,
            search, sortField, sortDirection,
            category, module, action, isActive, createdFrom, createdTo, isSystemAdmin);

        // Check cache
        var cachedResult = await _cacheService.GetCachedAsync<PagedResultDto<PermissionDto>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // Build query - Permissions are system-wide, ignore organization filter
        var query = _unitOfWork.Repository<Permission>().GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted);

        // Filter out System Admin only permissions for Company Admin
        if (!isSystemAdmin)
        {
            query = query.Where(p => !p.IsSystemAdminOnly);
        }

        // Apply search filter with case-insensitive search
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                p.Code.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);

        if (!string.IsNullOrEmpty(module))
            query = query.Where(p => p.Module == module);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(p => p.Action == action);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (createdFrom.HasValue)
            query = query.Where(p => p.CreatedAtUtc >= createdFrom.Value);

        if (createdTo.HasValue)
            query = query.Where(p => p.CreatedAtUtc <= createdTo.Value);

        var totalCount = await query.CountAsync();

        // Apply sorting at database level
        query = ApplySortingAtDatabase(query, sortField, sortDirection);

        var permissions = await query
            .Include(p => p.Menu) // ✅ Include Menu navigation
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Module = p.Module,
                Action = p.Action,
                Resource = p.Resource,
                IsSystemPermission = p.IsSystemPermission,
                IsSystemAdminOnly = p.IsSystemAdminOnly,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder,
                Category = p.Category,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.ModifiedAtUtc,
                CreatedBy = p.CreatedBy, // Will resolve to name below
                UpdatedBy = p.ModifiedBy, // Will resolve to name below
                MenuId = p.MenuId, // ✅ MenuId
                Menu = p.Menu != null ? new MenuDto
                {
                    Id = p.Menu.Id,
                    Label = p.Menu.Label,
                    Route = p.Menu.Route,
                    Icon = p.Menu.Icon,
                    Section = p.Menu.Section
                } : null
            })
            .ToListAsync();

        // Resolve CreatedBy and UpdatedBy user names
        if (permissions.Any())
        {
            var userIdStrings = permissions
                .SelectMany(p => new[] { p.CreatedBy, p.UpdatedBy })
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            if (userIdStrings.Any())
            {
                var userIds = userIdStrings
                    .Where(id => Guid.TryParse(id, out _))
                    .Select(Guid.Parse)
                    .ToList();

                if (userIds.Any())
                {
                    // Users are organization-specific, but permissions are system-wide
                    var userNames = await _unitOfWork.Repository<User>()
                        .GetQueryable()
                        .IgnoreQueryFilters()
                        .Where(u => userIds.Contains(u.Id))
                        .Select(u => new { Id = u.Id.ToString(), FullName = u.FullName })
                        .ToDictionaryAsync(u => u.Id, u => u.FullName);

                    foreach (var permission in permissions)
                    {
                        // Parse and set CreatedBy ID and Name
                        if (!string.IsNullOrEmpty(permission.CreatedBy) && Guid.TryParse(permission.CreatedBy, out var createdId))
                        {
                            permission.CreatedById = createdId;
                            if (userNames.TryGetValue(permission.CreatedBy, out var createdByName))
                            {
                                permission.CreatedByName = createdByName;
                                permission.CreatedBy = createdByName; // Keep for backward compatibility
                            }
                        }

                        // Parse and set ModifiedBy ID and Name (UpdatedBy is ModifiedBy)
                        if (!string.IsNullOrEmpty(permission.UpdatedBy) && Guid.TryParse(permission.UpdatedBy, out var modifiedId))
                        {
                            permission.ModifiedById = modifiedId;
                            if (userNames.TryGetValue(permission.UpdatedBy, out var modifiedByName))
                            {
                                permission.ModifiedByName = modifiedByName;
                                permission.UpdatedBy = modifiedByName; // Keep for backward compatibility
                            }
                        }
                    }
                }
            }
        }

        var result = new PagedResultDto<PermissionDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = permissions
        };

        // Cache the result
        await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));

        return result;
    }

    public async Task<PermissionDto?> GetPermissionByIdAsync(Guid id)
    {
        var cacheKey = $"permission:detail:{id}";
        var cachedResult = await _cacheService.GetCachedAsync<PermissionDto>(cacheKey);
        if (cachedResult != null) return cachedResult;

        // Permissions are system-wide, ignore organization filter
        var permission = await _unitOfWork.Repository<Permission>().GetQueryable()
            .IgnoreQueryFilters()
            .Include(p => p.Menu)
            .Where(p => p.Id == id && !p.IsDeleted)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Module = p.Module,
                Action = p.Action,
                Resource = p.Resource,
                IsSystemPermission = p.IsSystemPermission,
                IsSystemAdminOnly = p.IsSystemAdminOnly,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder,
                Category = p.Category,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.ModifiedAtUtc,
                CreatedBy = p.CreatedBy, // Will resolve to name below
                UpdatedBy = p.ModifiedBy, // Will resolve to name below
                MenuId = p.MenuId,
                Menu = p.Menu != null ? new MenuDto
                {
                    Id = p.Menu.Id,
                    Label = p.Menu.Label,
                    Route = p.Menu.Route,
                    Icon = p.Menu.Icon,
                    Section = p.Menu.Section
                } : null
            })
            .FirstOrDefaultAsync();

        if (permission != null)
        {
            // Resolve CreatedBy and UpdatedBy user names and IDs
            var userRepo = _unitOfWork.Repository<User>();
            Guid? createdById = null;
            Guid? modifiedById = null;
            string? createdByName = null;
            string? modifiedByName = null;

            if (!string.IsNullOrEmpty(permission.CreatedBy) && Guid.TryParse(permission.CreatedBy, out var createdId))
            {
                createdById = createdId;
                // Users are organization-specific, but permissions are system-wide
                var createdByUser = await userRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == createdId);
                if (createdByUser != null)
                {
                    createdByName = createdByUser.FullName;
                    permission.CreatedBy = createdByName; // Keep for backward compatibility
                }
            }

            if (!string.IsNullOrEmpty(permission.UpdatedBy) && Guid.TryParse(permission.UpdatedBy, out var modifiedId))
            {
                modifiedById = modifiedId;
                // Users are organization-specific, but permissions are system-wide
                var modifiedByUser = await userRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == modifiedId);
                if (modifiedByUser != null)
                {
                    modifiedByName = modifiedByUser.FullName;
                    permission.UpdatedBy = modifiedByName; // Keep for backward compatibility
                }
            }

            permission.CreatedById = createdById;
            permission.CreatedByName = createdByName;
            permission.ModifiedById = modifiedById;
            permission.ModifiedByName = modifiedByName;

            await _cacheService.SetCacheAsync(cacheKey, permission, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
        }

        return permission;
    }

    public async Task<PermissionDto?> GetPermissionByCodeAsync(string code)
    {
        // Permissions are system-wide, cache key doesn't need organization
        var cacheKey = $"permission:code:{code}";
        var cachedResult = await _cacheService.GetCachedAsync<PermissionDto>(cacheKey);
        if (cachedResult != null) return cachedResult;

        // Permissions are system-wide, ignore organization filter
        var permission = await _unitOfWork.Repository<Permission>().GetQueryable()
            .IgnoreQueryFilters()
            .Include(p => p.Menu)
            .Where(p => p.Code == code && !p.IsDeleted)
            .Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Module = p.Module,
                Action = p.Action,
                Resource = p.Resource,
                IsSystemPermission = p.IsSystemPermission,
                IsSystemAdminOnly = p.IsSystemAdminOnly,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder,
                Category = p.Category,
                CreatedAtUtc = p.CreatedAtUtc,
                UpdatedAtUtc = p.ModifiedAtUtc,
                CreatedBy = p.CreatedBy,
                UpdatedBy = p.ModifiedBy,
                MenuId = p.MenuId,
                Menu = p.Menu != null ? new MenuDto
                {
                    Id = p.Menu.Id,
                    Label = p.Menu.Label,
                    Route = p.Menu.Route,
                    Icon = p.Menu.Icon,
                    Section = p.Menu.Section
                } : null
            })
            .FirstOrDefaultAsync();

        if (permission != null)
        {
            await _cacheService.SetCacheAsync(cacheKey, permission, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
        }

        return permission;
    }

    public async Task<PermissionDto> CreatePermissionAsync(CreatePermissionDto dto)
    {
        // Only System Admin can create permissions
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        if (!isSystemAdmin)
        {
            throw new UnauthorizedAccessException("Only System Administrators can create permissions.");
        }

        var userId = _tenantService.GetCurrentUserId();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var menuRepo = _unitOfWork.Repository<Menu>();

        // Validate Menu exists - Menus might still be organization-specific, but we'll check system-wide
        var menu = await menuRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == dto.MenuId && !m.IsDeleted);
        if (menu == null)
            throw new ArgumentException($"Menu with ID {dto.MenuId} not found");

        // Check if permission code already exists (system-wide)
        var existingPermission = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Code == dto.Code && !p.IsDeleted);
        if (existingPermission != null)
        {
            throw new ArgumentException($"Permission with code '{dto.Code}' already exists.");
        }

        // Use a system organization ID (can be a fixed GUID or null if OrganizationId is nullable)
        // For now, we'll use a system organization ID - you may need to adjust this based on your schema
        var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // System organization

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            OrganizationId = systemOrganizationId, // System-wide permissions use a system organization ID
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Module = dto.Module,
            Action = dto.Action,
            Resource = dto.Resource,
            SortOrder = dto.SortOrder,
            Category = dto.Category,
            MenuId = dto.MenuId, // ✅ Set MenuId
            IsSystemPermission = true, // All permissions created by System Admin are system permissions
            IsSystemAdminOnly = dto.IsSystemAdminOnly, // System Admin only flag
            IsActive = true,
            CreatedBy = userId.ToString(),
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedBy = userId.ToString(),
            ModifiedAtUtc = DateTime.UtcNow
        };

        await permissionRepo.AddAsync(permission);
        await _unitOfWork.SaveChangesAsync();

        // If this permission is NOT System Admin only, assign it to all Administrator roles across all organizations
        if (!permission.IsSystemAdminOnly)
        {
            await SyncPermissionToAdministratorRolesAsync(dto.Code);
        }

        // Invalidate all permission caches (system-wide)
        await InvalidateAllPermissionCachesAsync();
        // Invalidate all user permission caches across all organizations
        await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_*");

        // Invalidate menu caches (if menus are organization-specific, invalidate for all orgs)
        await _cacheService.RemoveCacheAsync($"menu:detail:{dto.MenuId}");
        await _cacheService.RemoveCacheByPatternAsync($"menus:list:*");
        await _cacheService.RemoveCacheByPatternAsync($"menus:dropdown:*");

        return MapToDto(permission);
    }

    public async Task<PermissionDto> UpdatePermissionAsync(Guid id, UpdatePermissionDto dto)
    {
        // Only System Admin can update permissions
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        if (!isSystemAdmin)
        {
            throw new UnauthorizedAccessException("Only System Administrators can update permissions.");
        }

        var permissionRepo = _unitOfWork.Repository<Permission>();
        var menuRepo = _unitOfWork.Repository<Menu>();
        var userId = _tenantService.GetCurrentUserId();

        // Permissions are system-wide, ignore organization filter
        var permission = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (permission == null) throw new ArgumentException("Permission not found");

        // Validate Menu exists - Menus might still be organization-specific
        var menu = await menuRepo.GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == dto.MenuId && !m.IsDeleted);
        if (menu == null)
            throw new ArgumentException($"Menu with ID {dto.MenuId} not found");

        var oldMenuId = permission.MenuId;
        permission.Code = dto.Code;
        permission.Name = dto.Name;
        permission.Description = dto.Description;
        permission.Module = dto.Module;
        permission.Action = dto.Action;
        permission.Resource = dto.Resource;
        permission.IsActive = dto.IsActive;
        permission.SortOrder = dto.SortOrder;
        permission.Category = dto.Category;
        permission.MenuId = dto.MenuId; // ✅ Update MenuId
        permission.IsSystemAdminOnly = dto.IsSystemAdminOnly; // System Admin only flag

        permission.ModifiedBy = userId.ToString();
        permission.ModifiedAtUtc = DateTime.UtcNow;

        permissionRepo.Update(permission);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate all permission caches (system-wide)
        await InvalidateAllPermissionCachesAsync();
        await _cacheService.RemoveCacheAsync($"permission:detail:{id}");
        await _cacheService.RemoveCacheAsync($"permission:code:{permission.Code}");
        // Invalidate all user permission caches across all organizations
        await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_*");

        // If MenuId changed, invalidate menu caches
        if (oldMenuId != dto.MenuId)
        {
            await _cacheService.RemoveCacheAsync($"menu:detail:{oldMenuId}");
            await _cacheService.RemoveCacheAsync($"menu:detail:{dto.MenuId}");
            await _cacheService.RemoveCacheByPatternAsync($"menus:list:*");
            await _cacheService.RemoveCacheByPatternAsync($"menus:dropdown:*");
        }

        return MapToDto(permission);
    }

    public async Task<bool> DeletePermissionAsync(Guid id)
    {
        // Only System Admin can delete permissions
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        if (!isSystemAdmin)
        {
            throw new UnauthorizedAccessException("Only System Administrators can delete permissions.");
        }

        var userId = _tenantService.GetCurrentUserId();

        // Permissions are system-wide, ignore organization filter
        var permission = await _unitOfWork.Repository<Permission>().GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (permission == null)
            return false;

        // Check if permission is assigned to any role (across all organizations)
        var roleCount = await _unitOfWork.Repository<RolePermission>().GetQueryable()
            .IgnoreQueryFilters()
            .Where(rp => rp.PermissionId == id && !rp.IsDeleted)
            .CountAsync();

        if (roleCount > 0)
        {
            throw new InvalidOperationException("Cannot delete permission that is assigned to roles. Please remove all role assignments first.");
        }

        permission.IsDeleted = true;
        permission.DeletedAtUtc = DateTimeOffset.UtcNow;
        permission.DeletedBy = userId.ToString();
        permission.ModifiedAtUtc = DateTimeOffset.UtcNow;
        permission.ModifiedBy = userId.ToString();

        var menuId = permission.MenuId; // Store menuId before deletion
        _unitOfWork.Repository<Permission>().Update(permission);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate all permission caches (system-wide)
        await InvalidateAllPermissionCachesAsync();
        await _cacheService.RemoveCacheAsync($"permission:detail:{id}");
        await _cacheService.RemoveCacheAsync($"permission:code:{permission.Code}");
        // Invalidate all user permission caches across all organizations
        await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_*");
        // Invalidate menu caches
        await _cacheService.RemoveCacheAsync($"menu:detail:{menuId}");
        await _cacheService.RemoveCacheByPatternAsync($"menus:list:*");
        await _cacheService.RemoveCacheByPatternAsync($"menus:dropdown:*");

        return true;
    }

    public async Task BulkDeleteAsync(List<Guid> ids)
    {
        // Only System Admin can delete permissions
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        if (!isSystemAdmin)
        {
            throw new UnauthorizedAccessException("Only System Administrators can delete permissions.");
        }

        if (ids == null || !ids.Any())
            return;

        var userId = _tenantService.GetCurrentUserId();

        // Permissions are system-wide, ignore organization filter
        var permissions = await _unitOfWork.Repository<Permission>().GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync();

        if (!permissions.Any())
            return;

        // Check if any permissions are assigned to roles (across all organizations)
        var assignedPermissions = await _unitOfWork.Repository<RolePermission>().GetQueryable()
            .IgnoreQueryFilters()
            .Where(rp => ids.Contains(rp.PermissionId) && !rp.IsDeleted)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync();

        if (assignedPermissions.Any())
        {
            throw new InvalidOperationException($"Cannot delete permissions that are assigned to roles. Please remove role assignments for permissions: {string.Join(", ", assignedPermissions)}");
        }

        foreach (var permission in permissions)
        {
            permission.IsDeleted = true;
            permission.DeletedAtUtc = DateTimeOffset.UtcNow;
            permission.ModifiedAtUtc = DateTimeOffset.UtcNow;
            permission.ModifiedBy = userId.ToString();
            _unitOfWork.Repository<Permission>().Update(permission);
        }

        await _unitOfWork.SaveChangesAsync();

        // Get menuIds before deleting for cache invalidation
        var menuIds = permissions.Select(p => p.MenuId).Distinct().ToList();

        // Invalidate all permission caches (system-wide)
        await InvalidateAllPermissionCachesAsync();
        foreach (var id in ids)
        {
            await _cacheService.RemoveCacheAsync($"permission:detail:{id}");
        }
        // Invalidate all user permission caches across all organizations
        await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_*");
        // Invalidate menu caches
        foreach (var menuId in menuIds)
        {
            await _cacheService.RemoveCacheAsync($"menu:detail:{menuId}");
        }
        await _cacheService.RemoveCacheByPatternAsync($"menus:list:*");
        await _cacheService.RemoveCacheByPatternAsync($"menus:dropdown:*");
    }

    public async Task<List<PermissionDto>> BulkCloneAsync(List<Guid> ids)
    {
        // Only System Admin can clone permissions
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        if (!isSystemAdmin)
        {
            throw new UnauthorizedAccessException("Only System Administrators can clone permissions.");
        }

        if (ids == null || !ids.Any())
            return new List<PermissionDto>();

        var userId = _tenantService.GetCurrentUserId();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var systemOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // System organization

        // Get original permissions - Permissions are system-wide
        var originalPermissions = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => ids.Contains(p.Id) && !p.IsDeleted)
            .ToListAsync();

        if (!originalPermissions.Any())
            return new List<PermissionDto>();

        var clonedPermissions = new List<PermissionDto>();
        var generatedCodes = new HashSet<string>(); // Track codes in current batch
        var generatedNames = new HashSet<string>(); // Track names in current batch
        var clonedPermissionEntities = new List<Permission>(); // Store entities before saving

        // First, get all existing codes and names from database to avoid conflicts (system-wide)
        var existingCodes = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .Select(p => p.Code)
            .ToListAsync();
        var existingNames = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .Select(p => p.Name)
            .ToListAsync();

        generatedCodes.UnionWith(existingCodes);
        generatedNames.UnionWith(existingNames);

        foreach (var originalPermission in originalPermissions)
        {
            // Generate unique permission code with GUID to ensure uniqueness
            var baseCode = originalPermission.Code;
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            var newCode = $"{baseCode}.clone{uniqueSuffix}";
            var counter = 1;

            // Check if code already exists in current batch (includes database codes)
            while (generatedCodes.Contains(newCode))
            {
                uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
                newCode = $"{baseCode}.clone{uniqueSuffix}";
                counter++;
                if (counter > 100) break; // Safety limit
            }

            generatedCodes.Add(newCode); // Track this code for current batch

            // Generate unique permission name with GUID to ensure uniqueness
            var baseName = originalPermission.Name;
            var nameUniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            var newName = $"{baseName} (Copy {nameUniqueSuffix})";
            var nameCounter = 1;

            // Check if name already exists in current batch (includes database names)
            while (generatedNames.Contains(newName))
            {
                nameUniqueSuffix = Guid.NewGuid().ToString("N")[..8];
                newName = $"{baseName} (Copy {nameUniqueSuffix})";
                nameCounter++;
                if (nameCounter > 100) break; // Safety limit
            }

            generatedNames.Add(newName); // Track this name for current batch

            // Create cloned permission - use system organization ID
            var clonedPermission = new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = systemOrganizationId, // System-wide permissions use system organization ID
                Code = newCode,
                Name = newName,
                Description = originalPermission.Description,
                Module = originalPermission.Module,
                Action = originalPermission.Action,
                Resource = originalPermission.Resource,
                SortOrder = originalPermission.SortOrder,
                Category = originalPermission.Category,
                MenuId = originalPermission.MenuId, // Keep same menu
                IsSystemPermission = true, // Cloned permissions are system permissions
                IsSystemAdminOnly = originalPermission.IsSystemAdminOnly, // Keep same flag
                IsActive = false, // Cloned permissions start as inactive
                CreatedBy = userId.ToString(),
                CreatedAtUtc = DateTime.UtcNow,
                ModifiedBy = userId.ToString(),
                ModifiedAtUtc = DateTime.UtcNow
            };

            clonedPermissionEntities.Add(clonedPermission);
        }

        // Add all entities at once
        foreach (var clonedPermission in clonedPermissionEntities)
        {
            await permissionRepo.AddAsync(clonedPermission);
        }

        // Save all cloned permissions in a single transaction
        await _unitOfWork.SaveChangesAsync();

        // Build DTOs after saving
        foreach (var clonedPermission in clonedPermissionEntities)
        {
            clonedPermissions.Add(MapToDto(clonedPermission));
        }

        // Invalidate caches - system-wide
        await InvalidateAllPermissionCachesAsync();
        // Invalidate all user permission caches across all organizations
        await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_*");

        // Invalidate menu caches
        var affectedMenuIds = clonedPermissionEntities.Select(cp => cp.MenuId).Distinct().ToList();

        foreach (var menuId in affectedMenuIds)
        {
            await _cacheService.RemoveCacheAsync($"menu:detail:{menuId}");
        }
        await _cacheService.RemoveCacheByPatternAsync($"menus:list:*");
        await _cacheService.RemoveCacheByPatternAsync($"menus:dropdown:*");

        return clonedPermissions;
    }

    public async Task<List<PermissionDto>> GetPermissionsByModuleAsync(string module)
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();
        // Permissions are system-wide, ignore organization filter
        var permissions = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(x => x.Module == module && !x.IsDeleted)
            .ToListAsync();

        return permissions
            .OrderBy(x => x.SortOrder)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<List<PermissionDto>> GetPermissionsByCategoryAsync(string category)
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();
        // Permissions are system-wide, ignore organization filter
        var permissions = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(x => x.Category == category && !x.IsDeleted)
            .ToListAsync();

        return permissions
            .OrderBy(x => x.SortOrder)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<PermissionStatisticsDto> GetStatisticsAsync()
    {
        var repo = _unitOfWork.Repository<Permission>();
        // Permissions are system-wide, ignore organization filter
        var all = await repo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .ToListAsync();
        var total = all.Count();
        var active = all.Count(p => p.IsActive);
        return new PermissionStatisticsDto { Total = total, Active = active, Inactive = total - active };
    }

    public async Task<List<string>> GetUniqueModulesAsync()
    {
        var repo = _unitOfWork.Repository<Permission>();
        // Permissions are system-wide, ignore organization filter
        var modules = await repo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive && !string.IsNullOrEmpty(p.Module))
            .Select(p => p.Module)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync();

        return modules;
    }

    public async Task<List<string>> GetUniqueActionsAsync()
    {
        var repo = _unitOfWork.Repository<Permission>();
        // Permissions are system-wide, ignore organization filter
        var actions = await repo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive && !string.IsNullOrEmpty(p.Action))
            .Select(p => p.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        return actions;
    }

    // Old synchronous export/import methods removed - use async versions:
    // StartExportJobAsync() and StartImportJobAsync()

    public async Task<byte[]> GetImportTemplateAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();

        // Get master data for reference - Permissions are system-wide
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var allPermissions = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(x => !x.IsDeleted)
            .ToListAsync();

        var modules = allPermissions.Where(p => !string.IsNullOrEmpty(p.Module)).Select(p => p.Module).Distinct().OrderBy(m => m).ToList();
        var actions = allPermissions.Where(p => !string.IsNullOrEmpty(p.Action)).Select(p => p.Action).Distinct().OrderBy(a => a).ToList();
        var categories = allPermissions.Where(p => !string.IsNullOrEmpty(p.Category)).Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

        // ✅ Get menus for reference - Menus might still be organization-specific
        var menuRepo = _unitOfWork.Repository<Menu>();
        var menus = await menuRepo.FindManyAsync(m => m.OrganizationId == OrganizationId && !m.IsDeleted && m.IsActive);
        var menuLabels = menus.OrderBy(m => m.Section).ThenBy(m => m.Label).Select(m => $"{m.Label} ({m.Section ?? "No Section"})").ToList();

        // Generate Excel template with dropdown validation
        return GenerateExcelImportTemplate(modules, actions, categories, menuLabels);
    }

    private byte[] GenerateExcelImportTemplate(
        IEnumerable<string> modules,
        IEnumerable<string> actions,
        IEnumerable<string> categories,
        IEnumerable<string> menuLabels)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Import Data Template
        var importSheet = workbook.Worksheets.Add("Import Data");

        // Set up headers (✅ Added Menu column)
        var headers = new[] { "Code", "Name", "Description", "Module", "Action", "Resource", "Menu", "Category", "Sort Order", "Status" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = importSheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Add sample data row (✅ Added Menu sample)
        importSheet.Cell(2, 1).Value = "Products.Read";
        importSheet.Cell(2, 2).Value = "Read Products";
        importSheet.Cell(2, 3).Value = "View product information";
        importSheet.Cell(2, 4).Value = "Products";
        importSheet.Cell(2, 5).Value = "Read";
        importSheet.Cell(2, 6).Value = "Products";
        importSheet.Cell(2, 7).Value = menuLabels.FirstOrDefault() ?? "Select Menu";
        importSheet.Cell(2, 8).Value = "CRUD";
        importSheet.Cell(2, 9).Value = 10;
        importSheet.Cell(2, 10).Value = "Active";

        // Sheet 2: Reference Data
        var referenceSheet = workbook.Worksheets.Add("Reference Data");

        // Modules
        referenceSheet.Cell(1, 1).Value = "Modules";
        referenceSheet.Cell(1, 1).Style.Font.Bold = true;
        for (int i = 0; i < modules.Count(); i++)
        {
            referenceSheet.Cell(i + 2, 1).Value = modules.ToList()[i];
        }

        // Actions
        referenceSheet.Cell(1, 2).Value = "Actions";
        referenceSheet.Cell(1, 2).Style.Font.Bold = true;
        for (int i = 0; i < actions.Count(); i++)
        {
            referenceSheet.Cell(i + 2, 2).Value = actions.ToList()[i];
        }

        // Categories
        referenceSheet.Cell(1, 3).Value = "Categories";
        referenceSheet.Cell(1, 3).Style.Font.Bold = true;
        for (int i = 0; i < categories.Count(); i++)
        {
            referenceSheet.Cell(i + 2, 3).Value = categories.ToList()[i];
        }

        // ✅ Menus
        referenceSheet.Cell(1, 4).Value = "Menus";
        referenceSheet.Cell(1, 4).Style.Font.Bold = true;
        for (int i = 0; i < menuLabels.Count(); i++)
        {
            referenceSheet.Cell(i + 2, 4).Value = menuLabels.ToList()[i];
        }

        // Status values
        referenceSheet.Cell(1, 5).Value = "Status";
        referenceSheet.Cell(1, 5).Style.Font.Bold = true;
        referenceSheet.Cell(2, 5).Value = "Active";
        referenceSheet.Cell(3, 5).Value = "Inactive";

        // Define named ranges for dropdown validations
        var moduleCount = modules.Count();
        var actionCount = actions.Count();
        var categoryCount = categories.Count();
        var menuCount = menuLabels.Count(); // ✅ Menu count

        if (moduleCount > 0)
            workbook.NamedRanges.Add("Modules", referenceSheet.Range(2, 1, moduleCount + 1, 1));
        if (actionCount > 0)
            workbook.NamedRanges.Add("Actions", referenceSheet.Range(2, 2, actionCount + 1, 2));
        if (categoryCount > 0)
            workbook.NamedRanges.Add("Categories", referenceSheet.Range(2, 3, categoryCount + 1, 3));
        if (menuCount > 0) // ✅ Menu named range
            workbook.NamedRanges.Add("Menus", referenceSheet.Range(2, 4, menuCount + 1, 4));

        workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 5, 3, 5));

        // Add data validation (dropdowns) to Import Data sheet (rows 2-1000)
        // Module column (D)
        if (moduleCount > 0)
        {
            var moduleValidation = importSheet.Range("D2:D1000").SetDataValidation();
            moduleValidation.List("=Modules", true);
            moduleValidation.IgnoreBlanks = true;
            moduleValidation.InCellDropdown = true;
        }

        // Action column (E)
        if (actionCount > 0)
        {
            var actionValidation = importSheet.Range("E2:E1000").SetDataValidation();
            actionValidation.List("=Actions", true);
            actionValidation.IgnoreBlanks = true;
            actionValidation.InCellDropdown = true;
        }

        // ✅ Menu column (G) - Required field
        if (menuCount > 0)
        {
            var menuValidation = importSheet.Range("G2:G1000").SetDataValidation();
            menuValidation.List("=Menus", true);
            menuValidation.IgnoreBlanks = false; // Required field
            menuValidation.InCellDropdown = true;
        }

        // Category column (H) - moved due to Menu insertion
        if (categoryCount > 0)
        {
            var categoryValidation = importSheet.Range("H2:H1000").SetDataValidation();
            categoryValidation.List("=Categories", true);
            categoryValidation.IgnoreBlanks = true;
            categoryValidation.InCellDropdown = true;
        }

        // Status column (J) - moved due to Menu insertion
        var statusValidation = importSheet.Range("J2:J1000").SetDataValidation();
        statusValidation.List("=StatusValues", true);
        statusValidation.IgnoreBlanks = true;
        statusValidation.InCellDropdown = true;

        // Auto-fit columns
        importSheet.Columns().AdjustToContents();
        referenceSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ========================================
    // Import Methods (Using Unified IImportExportService)
    // ========================================

    public async Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId)
    {
        return await _importExportService.GetImportJobStatusAsync(jobId);
    }

    public async Task<byte[]?> GetImportErrorReportAsync(string errorReportId)
    {
        return await _importExportService.GetImportErrorReportAsync(errorReportId);
    }

    public async Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();

        return await _importExportService.StartImportJobAsync<CreatePermissionDto>(
            entityType: "Permission",
            fileStream: fileStream,
            fileName: fileName,
            rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
            {
                // Validate required fields
                if (!rowData.TryGetValue("Code", out var code) || string.IsNullOrWhiteSpace(code))
                    return (false, "Code is required", false, false);

                if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
                    return (false, "Name is required", false, false);

                if (!rowData.TryGetValue("Module", out var module) || string.IsNullOrWhiteSpace(module))
                    return (false, "Module is required", false, false);

                // ✅ Validate Menu (Required)
                if (!rowData.TryGetValue("Menu", out var menuLabel) || string.IsNullOrWhiteSpace(menuLabel))
                    return (false, "Menu is required", false, false);

                // Use scoped repositories from the passed UnitOfWork
                var permissionRepo = scopedUnitOfWork.Repository<Permission>();
                var menuRepo = scopedUnitOfWork.Repository<Menu>();

                // ✅ Find Menu by Label (format: "Label (Section)" or just "Label")
                Menu? selectedMenu = null;
                var menuLabelParts = menuLabel.ToString()!.Trim();
                var menuName = menuLabelParts;
                if (menuLabelParts.Contains("(") && menuLabelParts.Contains(")"))
                {
                    menuName = menuLabelParts.Substring(0, menuLabelParts.IndexOf("(")).Trim();
                }

                selectedMenu = await menuRepo.FindAsync(m =>
                    m.OrganizationId == organizationId &&
                    !m.IsDeleted &&
                    m.IsActive &&
                    m.Label == menuName);

                if (selectedMenu == null)
                    return (false, $"Menu '{menuLabel}' not found. Please use an existing menu from the dropdown.", false, false);

                // Check if permission already exists by code
                var existingPermission = await permissionRepo.FindAsync(p =>
                    p.OrganizationId == organizationId && p.Code.ToLower() == code.ToLower() && !p.IsDeleted);

                if (existingPermission != null)
                {
                    if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
                        return (true, null, false, true); // Skip

                    if (duplicateStrategy == DuplicateHandlingStrategy.Update)
                    {
                        // Update existing permission
                        existingPermission.Name = name;
                        existingPermission.Description = rowData.GetValueOrDefault("Description");
                        existingPermission.Module = module;
                        existingPermission.Action = rowData.GetValueOrDefault("Action") ?? "";
                        existingPermission.Resource = rowData.GetValueOrDefault("Resource") ?? "";
                        existingPermission.Category = rowData.GetValueOrDefault("Category");
                        existingPermission.MenuId = selectedMenu!.Id; // ✅ Update MenuId

                        if (rowData.TryGetValue("Status", out var status))
                            existingPermission.IsActive = status?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;

                        if (rowData.TryGetValue("Sort Order", out var sortOrderStr) && int.TryParse(sortOrderStr, out var sortOrder))
                            existingPermission.SortOrder = sortOrder;

                        existingPermission.ModifiedAtUtc = DateTimeOffset.UtcNow;
                        existingPermission.ModifiedBy = userId.ToString();

                        permissionRepo.Update(existingPermission);
                        return (true, null, true, false); // Updated
                    }
                }

                // Create new permission
                var newPermission = new Permission
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Code = code,
                    Name = name,
                    Description = rowData.GetValueOrDefault("Description"),
                    Module = module,
                    Action = rowData.GetValueOrDefault("Action") ?? "",
                    Resource = rowData.GetValueOrDefault("Resource") ?? "",
                    Category = rowData.GetValueOrDefault("Category"),
                    MenuId = selectedMenu!.Id, // ✅ Set MenuId (Required)
                    IsActive = true,
                    SortOrder = 0,
                    IsSystemPermission = false,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = userId.ToString()
                };

                // Handle Status
                if (rowData.TryGetValue("Status", out var statusValue))
                    newPermission.IsActive = statusValue?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;

                // Handle Sort Order
                if (rowData.TryGetValue("Sort Order", out var sortStr) && int.TryParse(sortStr, out var sortVal))
                    newPermission.SortOrder = sortVal;

                await permissionRepo.AddAsync(newPermission);
                return (true, null, false, false); // Success
            },
            duplicateStrategy: duplicateStrategy
        );
    }

    public async Task<bool> UserHasPermissionAsync(Guid userId, string permissionCode)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var cacheKey = $"user_permissions_{userId}_{OrganizationId}";

        HashSet<string>? cachedPermissions = null;
        try { cachedPermissions = await _cacheService.GetCachedAsync<HashSet<string>>(cacheKey); } catch { /* fallback to DB */ }
        if (cachedPermissions != null)
        {
            return cachedPermissions.Contains(permissionCode);
        }

        await _permissionCacheSemaphore.WaitAsync();
        try
        {
            // Double-check after lock
            try { cachedPermissions = await _cacheService.GetCachedAsync<HashSet<string>>(cacheKey); } catch { /* ignore */ }
            if (cachedPermissions != null)
            {
                return cachedPermissions.Contains(permissionCode);
            }

            var codes = await BuildUserPermissionCodesAsync(userId, OrganizationId);
            try { await _cacheService.SetCacheAsync(cacheKey, codes, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes())); } catch { /* ignore cache failures */ }
            return codes.Contains(permissionCode);
        }
        finally
        {
            _permissionCacheSemaphore.Release();
        }
    }

    public async Task<List<string>> GetUserPermissionCodesAsync(Guid userId)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var cacheKey = $"user_permissions_{userId}_{OrganizationId}";

        HashSet<string>? cachedPermissions = null;
        try { cachedPermissions = await _cacheService.GetCachedAsync<HashSet<string>>(cacheKey); } catch { /* fallback to DB */ }
        if (cachedPermissions != null)
        {
            return cachedPermissions.ToList();
        }

        await _permissionCacheSemaphore.WaitAsync();
        try
        {
            // Double-check after lock
            try { cachedPermissions = await _cacheService.GetCachedAsync<HashSet<string>>(cacheKey); } catch { /* ignore */ }
            if (cachedPermissions != null)
            {
                return cachedPermissions.ToList();
            }

            var codes = await BuildUserPermissionCodesAsync(userId, OrganizationId);
            try { await _cacheService.SetCacheAsync(cacheKey, codes, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes())); } catch { /* ignore cache failures */ }
            return codes.ToList();
        }
        finally
        {
            _permissionCacheSemaphore.Release();
        }
    }

    private async Task<HashSet<string>> BuildUserPermissionCodesAsync(Guid userId, Guid organizationId)
    {
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var roleRepo = _unitOfWork.Repository<Role>();
        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        // User roles are organization-specific
        var userRoles = await userRoleRepo.FindManyAsync(x => x.UserId == userId && x.OrganizationId == organizationId);
        var roleIds = new HashSet<Guid>(userRoles.Select(ur => ur.RoleId));

        // Collect parent roles iteratively to avoid deep recursion and reduce queries
        var allRoleIds = new HashSet<Guid>(roleIds);
        var frontier = new Queue<Guid>(roleIds);
        while (frontier.Count > 0)
        {
            var batchIds = new List<Guid>();
            while (frontier.Count > 0 && batchIds.Count < 50)
            {
                batchIds.Add(frontier.Dequeue());
            }
            // Roles are organization-specific
            var roles = await roleRepo.GetQueryable()
                .Where(r => batchIds.Contains(r.Id) && r.OrganizationId == organizationId && !r.IsDeleted)
                .ToListAsync();
            foreach (var role in roles)
            {
                if (role.ParentRoleId.HasValue && !allRoleIds.Contains(role.ParentRoleId.Value))
                {
                    allRoleIds.Add(role.ParentRoleId.Value);
                    frontier.Enqueue(role.ParentRoleId.Value);
                }
            }
        }

        // Fetch role-permissions - RolePermissions are organization-specific, but Permissions are system-wide
        var rolePermissions = await rolePermissionRepo.GetQueryable()
            .Where(rp => allRoleIds.Contains(rp.RoleId) && rp.OrganizationId == organizationId && !rp.IsDeleted)
            .ToListAsync();
        var permissionIds = rolePermissions.Select(rp => rp.PermissionId).Distinct().ToList();

        if (!permissionIds.Any()) return new HashSet<string>();

        // Permissions are system-wide, ignore organization filter
        var permissionCodes = await permissionRepo
            .GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => permissionIds.Contains(p.Id) && !p.IsDeleted && p.IsActive)
            .Select(p => p.Code)
            .ToListAsync();

        return permissionCodes.ToHashSet();
    }

    public async Task<bool> SeedDefaultPermissionsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var defaultPermissions = new[]
        {
			// User Management
			new { Code = "Users.Read", Name = "Read Users", Module = "Users", Action = "Read", Resource = "Users", Category = "CRUD" },
            new { Code = "Users.Create", Name = "Create Users", Module = "Users", Action = "Create", Resource = "Users", Category = "CRUD" },
            new { Code = "Users.Update", Name = "Update Users", Module = "Users", Action = "Update", Resource = "Users", Category = "CRUD" },
            new { Code = "Users.Delete", Name = "Delete Users", Module = "Users", Action = "Delete", Resource = "Users", Category = "CRUD" },
			
			// Role Management
			new { Code = "Roles.Read", Name = "Read Roles", Module = "Roles", Action = "Read", Resource = "Roles", Category = "CRUD" },
            new { Code = "Roles.Create", Name = "Create Roles", Module = "Roles", Action = "Create", Resource = "Roles", Category = "CRUD" },
            new { Code = "Roles.Update", Name = "Update Roles", Module = "Roles", Action = "Update", Resource = "Roles", Category = "CRUD" },
            new { Code = "Roles.Delete", Name = "Delete Roles", Module = "Roles", Action = "Delete", Resource = "Roles", Category = "CRUD" },
			
			// Permission Management
			new { Code = "Permissions.Read", Name = "Read Permissions", Module = "Permissions", Action = "Read", Resource = "Permissions", Category = "CRUD" },
            new { Code = "Permissions.Create", Name = "Create Permissions", Module = "Permissions", Action = "Create", Resource = "Permissions", Category = "CRUD" },
            new { Code = "Permissions.Update", Name = "Update Permissions", Module = "Permissions", Action = "Update", Resource = "Permissions", Category = "CRUD" },
            new { Code = "Permissions.Delete", Name = "Delete Permissions", Module = "Permissions", Action = "Delete", Resource = "Permissions", Category = "CRUD" },
			
			// Organization Management
			new { Code = "Organizations.Read", Name = "Read Organizations", Module = "Organizations", Action = "Read", Resource = "Organizations", Category = "CRUD" },
            new { Code = "Organizations.Create", Name = "Create Organizations", Module = "Organizations", Action = "Create", Resource = "Organizations", Category = "CRUD" },
            new { Code = "Organizations.Update", Name = "Update Organizations", Module = "Organizations", Action = "Update", Resource = "Organizations", Category = "CRUD" },
            new { Code = "Organizations.Delete", Name = "Delete Organizations", Module = "Organizations", Action = "Delete", Resource = "Organizations", Category = "CRUD" },
			
			// Product Management
			new { Code = "Products.Read", Name = "Read Products", Module = "Products", Action = "Read", Resource = "Products", Category = "CRUD" },
            new { Code = "Products.Create", Name = "Create Products", Module = "Products", Action = "Create", Resource = "Products", Category = "CRUD" },
            new { Code = "Products.Update", Name = "Update Products", Module = "Products", Action = "Update", Resource = "Products", Category = "CRUD" },
            new { Code = "Products.Delete", Name = "Delete Products", Module = "Products", Action = "Delete", Resource = "Products", Category = "CRUD" },
			
			// Inventory Management
			new { Code = "Inventory.Read", Name = "Read Inventory", Module = "Inventory", Action = "Read", Resource = "Inventory", Category = "CRUD" },
            new { Code = "Inventory.Create", Name = "Create Inventory", Module = "Inventory", Action = "Create", Resource = "Inventory", Category = "CRUD" },
            new { Code = "Inventory.Update", Name = "Update Inventory", Module = "Inventory", Action = "Update", Resource = "Inventory", Category = "CRUD" },
            new { Code = "Inventory.Delete", Name = "Delete Inventory", Module = "Inventory", Action = "Delete", Resource = "Inventory", Category = "CRUD" },
			
			// System Administration
			new { Code = "System.Admin", Name = "System Administration", Module = "System", Action = "Admin", Resource = "System", Category = "ADMIN" },
            new { Code = "System.Settings", Name = "System Settings", Module = "System", Action = "Settings", Resource = "System", Category = "SETTINGS" }
        };

        var existingCodes = await permissionRepo.FindManyAsync(x => x.OrganizationId == OrganizationId);
        var existingCodeSet = existingCodes.Select(x => x.Code).ToHashSet();

        var newPermissions = defaultPermissions
            .Where(p => !existingCodeSet.Contains(p.Code))
            .Select(p => new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                Code = p.Code,
                Name = p.Name,
                Description = $"{p.Action} {p.Resource}",
                Module = p.Module,
                Action = p.Action,
                Resource = p.Resource,
                Category = p.Category,
                SortOrder = 0,
                IsSystemPermission = true,
                IsActive = true
            })
            .ToList();

        foreach (var permission in newPermissions)
        {
            await permissionRepo.AddAsync(permission);
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    private async Task CollectRolePermissionCodesAsync(Guid roleId, HashSet<string> permissionCodes)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();
        var roleRepo = _unitOfWork.Repository<Role>();

        // Collect direct permissions with explicit OrganizationId filter
        var directPermissions = await rolePermissionRepo.GetQueryable()
            .Where(x => x.RoleId == roleId && x.OrganizationId == OrganizationId && !x.IsDeleted)
            .ToListAsync();

        foreach (var rolePermission in directPermissions)
        {
            var permission = await GetPermissionByIdAsync(rolePermission.PermissionId);
            if (permission != null)
            {
                permissionCodes.Add(permission.Code);
            }
        }

        // Collect inherited permissions with explicit OrganizationId filter
        var role = await roleRepo.GetQueryable()
            .Where(x => x.Id == roleId && x.OrganizationId == OrganizationId && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (role?.ParentRoleId.HasValue == true)
        {
            await CollectRolePermissionCodesAsync(role.ParentRoleId.Value, permissionCodes);
        }
    }

    private async Task<bool> RoleHasPermissionRecursiveAsync(Role role, string permissionCode)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();
        var roleRepo = _unitOfWork.Repository<Role>();

        // Check direct permissions with explicit OrganizationId filter
        var hasDirectPermission = await rolePermissionRepo.GetQueryable()
            .AnyAsync(x => x.RoleId == role.Id && x.OrganizationId == OrganizationId && !x.IsDeleted);

        if (hasDirectPermission)
        {
            var rolePermissions = await rolePermissionRepo.GetQueryable()
                .Where(x => x.RoleId == role.Id && x.OrganizationId == OrganizationId && !x.IsDeleted)
                .ToListAsync();

            foreach (var rolePermission in rolePermissions)
            {
                var permission = await GetPermissionByIdAsync(rolePermission.PermissionId);
                if (permission?.Code == permissionCode)
                {
                    return true;
                }
            }
        }

        // Check inherited permissions with explicit OrganizationId filter
        if (role.ParentRoleId.HasValue)
        {
            var parentRole = await roleRepo.GetQueryable()
                .Where(x => x.Id == role.ParentRoleId.Value && x.OrganizationId == OrganizationId && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (parentRole != null)
            {
                return await RoleHasPermissionRecursiveAsync(parentRole, permissionCode);
            }
        }

        return false;
    }

    private PermissionDto MapToDto(Permission permission)
    {
        return new PermissionDto
        {
            Id = permission.Id,
            Code = permission.Code,
            Name = permission.Name,
            Description = permission.Description,
            Module = permission.Module,
            Action = permission.Action,
            Resource = permission.Resource,
            IsSystemPermission = permission.IsSystemPermission,
            IsSystemAdminOnly = permission.IsSystemAdminOnly,
            IsActive = permission.IsActive,
            SortOrder = permission.SortOrder,
            Category = permission.Category,
            CreatedAtUtc = permission.CreatedAtUtc,
            UpdatedAtUtc = permission.ModifiedAtUtc,
            CreatedBy = permission.CreatedBy,
            UpdatedBy = permission.ModifiedBy,
            MenuId = permission.MenuId, // ✅ MenuId
            Menu = permission.Menu != null ? new MenuDto
            {
                Id = permission.Menu.Id,
                Label = permission.Menu.Label,
                Route = permission.Menu.Route,
                Icon = permission.Menu.Icon,
                Section = permission.Menu.Section,
                ParentMenuId = permission.Menu.ParentMenuId,
                SortOrder = permission.Menu.SortOrder,
                IsActive = permission.Menu.IsActive
            } : null
        };
    }

    // ========================================
    // Async Export Features (like UserService) - Using IImportExportService
    // ========================================

    public async Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
    {
        // Capture context before background task
        var organizationId = _tenantService.GetCurrentOrganizationId();

        return await _importExportService.StartExportJobAsync<Permission>(
            entityType: "Permission",
            format: format,
            dataFetcher: async (f) =>
            {
                // Create new scope to get fresh DbContext using IServiceScopeFactory
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();

                // CRITICAL: Set background context to ensure DbContext global query filter works correctly
                var orgId = organizationId; // Use captured value
                scopedTenantService.SetBackgroundContext(orgId, null, null);

                var repo = scopedUnitOfWork.Repository<Permission>();

                // Fetch permissions based on filters
                var search = f.GetValueOrDefault("search")?.ToString();
                var category = f.GetValueOrDefault("category")?.ToString();
                var module = f.GetValueOrDefault("module")?.ToString();
                var action = f.GetValueOrDefault("action")?.ToString();
                var isActive = f.GetValueOrDefault("isActive") as bool?;
                var createdFrom = f.GetValueOrDefault("createdFrom") as DateTime?;
                var createdTo = f.GetValueOrDefault("createdTo") as DateTime?;
                var selectedIds = f.GetValueOrDefault("selectedIds");

                // Convert empty strings to null
                search = string.IsNullOrWhiteSpace(search) ? null : search;
                category = string.IsNullOrWhiteSpace(category) ? null : category;
                module = string.IsNullOrWhiteSpace(module) ? null : module;
                action = string.IsNullOrWhiteSpace(action) ? null : action;

                var searchLower = search?.ToLower();
                List<Permission> permissionsToExport;

                // Convert selectedIds to list if present
                List<Guid>? idsList = null;
                if (selectedIds != null)
                {
                    idsList = selectedIds as List<Guid>;
                    if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
                    {
                        idsList = enumerableIds.ToList();
                    }
                }

                // If specific IDs are selected, only fetch those
                if (idsList != null && idsList.Any())
                {
                    var permissionIds = idsList; // Make a copy for lambda
                    var query = repo.GetQueryable()
                        .Include(p => p.Menu) // ✅ Include Menu for export
                        .Where(p => p.OrganizationId == orgId && !p.IsDeleted && permissionIds.Contains(p.Id));
                    permissionsToExport = await query.ToListAsync();
                }
                else
                {
                    // Otherwise, apply filters
                    var query = repo.GetQueryable()
                        .Include(p => p.Menu) // ✅ Include Menu for export
                        .Where(p => p.OrganizationId == orgId && !p.IsDeleted &&
                            (searchLower == null || (p.Name.ToLower().Contains(searchLower) || (p.Description != null && p.Description.ToLower().Contains(searchLower)) || p.Code.ToLower().Contains(searchLower))) &&
                            (category == null || p.Category == category) &&
                            (module == null || p.Module == module) &&
                            (action == null || p.Action == action) &&
                            (isActive == null || p.IsActive == isActive) &&
                            (createdFrom == null || p.CreatedAtUtc >= createdFrom.Value) &&
                            (createdTo == null || p.CreatedAtUtc <= createdTo.Value));
                    permissionsToExport = await query.ToListAsync();
                }

                return permissionsToExport;
            },
            filters: filters,
            columnMapper: MapPermissionToExportColumns
        );
    }

    public async Task<ExportJobStatusDto?> GetExportJobStatusAsync(string jobId)
    {
        return await _importExportService.GetExportJobStatusAsync(jobId);
    }

    public async Task<byte[]?> DownloadExportFileAsync(string jobId)
    {
        return await _importExportService.DownloadExportFileAsync(jobId);
    }

    private Dictionary<string, object> MapPermissionToExportColumns(Permission permission)
    {
        // ✅ Get Menu information for export
        var menuLabel = "";
        if (permission.Menu != null)
        {
            menuLabel = string.IsNullOrEmpty(permission.Menu.Section)
                ? permission.Menu.Label
                : $"{permission.Menu.Label} ({permission.Menu.Section})";
        }

        return new Dictionary<string, object>
        {
            ["ID"] = permission.Id.ToString(),
            ["Code"] = permission.Code,
            ["Name"] = permission.Name,
            ["Description"] = permission.Description ?? "",
            ["Module"] = permission.Module,
            ["Action"] = permission.Action,
            ["Resource"] = permission.Resource,
            ["Menu"] = menuLabel, // ✅ Menu column in export
            ["Category"] = permission.Category ?? "",
            ["Status"] = permission.IsActive ? "Active" : "Inactive",
            ["Sort Order"] = permission.SortOrder,
            ["Created Date"] = permission.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
        };
    }

    // ========================================
    // Unified Import/Export History
    // ========================================

    public async Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
    {
        return await _importExportService.GetHistoryAsync("Permission", type, page, pageSize);
    }

    // ========================================
    // Additional Dropdown Options
    // ========================================

    public async Task<List<string>> GetUniqueCategoriesAsync()
    {
        var repo = _unitOfWork.Repository<Permission>();
        // Permissions are system-wide, ignore organization filter
        var categories = await repo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive && !string.IsNullOrEmpty(p.Category))
            .Select(p => p.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return categories;
    }

    public async Task<PermissionDropdownOptionsDto> GetDropdownOptionsAsync()
    {
        var modules = await GetUniqueModulesAsync();
        var actions = await GetUniqueActionsAsync();
        var categories = await GetUniqueCategoriesAsync();

        return new PermissionDropdownOptionsDto
        {
            Modules = modules,
            Actions = actions,
            Categories = categories
        };
    }

    // ========================================
    // Helper Methods
    // ========================================

    /// <summary>
    /// Invalidates ALL permission-related caches (system-wide)
    /// </summary>
    private async Task InvalidateAllPermissionCachesAsync()
    {
        // Clear all permission cache types (system-wide)
        await _cacheService.RemoveCacheByPatternAsync($"permissions:list:*");
        await _cacheService.RemoveCacheByPatternAsync($"permissions:stats:*");
        await _cacheService.RemoveCacheByPatternAsync($"permissions:dropdown:*");
    }

    /// <summary>
    /// Applies sorting at database level for better performance
    /// </summary>
    private IQueryable<Permission> ApplySortingAtDatabase(IQueryable<Permission> query, string? sortField, string? sortDirection)
    {
        // Default to SortOrder then Name (consistent with Role service)
        if (string.IsNullOrEmpty(sortField))
        {
            return query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name);
        }

        return sortField.ToLower() switch
        {
            "name" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "code" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.Code) : query.OrderBy(p => p.Code),
            "module" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.Module) : query.OrderBy(p => p.Module),
            "action" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.Action) : query.OrderBy(p => p.Action),
            "category" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.Category) : query.OrderBy(p => p.Category),
            "createdat" or "createdatutc" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.CreatedAtUtc) : query.OrderBy(p => p.CreatedAtUtc),
            "isactive" or "status" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.IsActive) : query.OrderBy(p => p.IsActive),
            "menu" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.Menu != null ? p.Menu.Label : "") : query.OrderBy(p => p.Menu != null ? p.Menu.Label : ""),
            "sortorder" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(p => p.SortOrder) : query.OrderBy(p => p.SortOrder),
            _ => query.OrderBy(p => p.SortOrder).ThenBy(p => p.Name) // Default to SortOrder then Name
        };
    }

    /// <summary>
    /// Get Company Admin permission codes dynamically based on IsSystemAdminOnly flag
    /// Returns all permissions that are NOT System Admin only (IsSystemAdminOnly = false)
    /// Permissions are system-wide, so no organization filter needed
    /// </summary>
    private async Task<string[]> GetCompanyAdminPermissionCodesAsync()
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var permissions = await permissionRepo.GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive && !p.IsSystemAdminOnly)
            .Select(p => p.Code)
            .ToListAsync();

        return permissions.ToArray();
    }

    /// <summary>
    /// Sync a permission to all Administrator roles across all organizations if it's NOT System Admin only
    /// </summary>
    private async Task SyncPermissionToAdministratorRolesAsync(string permissionCode)
    {
        try
        {
            var roleRepo = _unitOfWork.Repository<Role>();
            var permissionRepo = _unitOfWork.Repository<Permission>();
            var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();
            var organizationRepo = _unitOfWork.Repository<Organization>();

            // Find permission system-wide (by code) - Only sync permissions that are NOT System Admin only
            var permission = await permissionRepo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Code == permissionCode
                    && p.IsActive
                    && !p.IsDeleted
                    && !p.IsSystemAdminOnly);

            if (permission == null) return; // Permission not found or is System Admin only

            // Get all organizations
            var allOrganizations = await organizationRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(o => !o.IsDeleted && o.IsActive)
                .Select(o => o.Id)
                .ToListAsync();

            // For each organization, find Administrator role and assign the permission
            foreach (var orgId in allOrganizations)
            {
                // Find Administrator role in this organization
                var adminRole = await roleRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(r => r.OrganizationId == orgId
                        && r.Name == "Administrator"
                        && r.IsActive
                        && !r.IsDeleted);

                if (adminRole == null) continue;

                // Check if already assigned
                var existingAssignment = await rolePermissionRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .AnyAsync(rp => rp.RoleId == adminRole.Id
                        && rp.PermissionId == permission.Id
                        && rp.OrganizationId == orgId
                        && !rp.IsDeleted);

                if (!existingAssignment)
                {
                    // Assign permission to Administrator role
                    var rolePermission = new RolePermission
                    {
                        Id = Guid.NewGuid(),
                        OrganizationId = orgId,
                        RoleId = adminRole.Id,
                        PermissionId = permission.Id,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        CreatedBy = "System"
                    };

                    await rolePermissionRepo.AddAsync(rolePermission);

                    // Invalidate caches for this organization
                    await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*_{orgId}");
                    await _cacheService.RemoveCacheByPatternAsync($"user_menus_*_{orgId}");
                    await _cacheService.RemoveCacheAsync($"role:permissions:{adminRole.Id}:{orgId}");
                }
            }

            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log error but don't fail permission creation
            _logger.LogError(ex, "Error syncing permission {PermissionCode} to Administrator roles", permissionCode);
        }
    }
}
