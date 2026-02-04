using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SaaSBase.Application.Implementations;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly IUserContextService _userContextService;
    private readonly IBackgroundOperationService _backgroundOperationService;
    private readonly ICacheService _cacheService;
    private readonly IPerformanceService _performanceService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IImportExportService _importExportService;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService,
        IUserContextService userContextService,
        IBackgroundOperationService backgroundOperationService,
        ICacheService cacheService,
        IPerformanceService performanceService,
        IServiceScopeFactory serviceScopeFactory,
        IImportExportService importExportService,
        ILogger<RoleService> logger)
    {
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _userContextService = userContextService;
        _backgroundOperationService = backgroundOperationService;
        _cacheService = cacheService;
        _performanceService = performanceService;
        _serviceScopeFactory = serviceScopeFactory;
        _importExportService = importExportService;
        _logger = logger;
    }

    public async Task<PagedResultDto<RoleDto>> GetRolesAsync(string? search, bool? isActive, string? roleType, Guid? organizationId, DateTime? createdFrom, DateTime? createdTo, int page, int pageSize, string? sortField = null, string? sortDirection = "asc")
    {
        return await _performanceService.MonitorAsync("GetRolesAsync", async () =>
        {
            var currentOrganizationId = _tenantService.GetCurrentOrganizationId();
            var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
            
            // Determine which organization to filter by
            Guid filterOrganizationId;
            if (isSystemAdmin && organizationId.HasValue)
            {
                // System Admin can filter by any organization
                filterOrganizationId = organizationId.Value;
            }
            else
            {
                // Regular users can only see their own organization
                filterOrganizationId = currentOrganizationId;
            }

            var cacheKey = _cacheService.GenerateListCacheKey("roles", filterOrganizationId, page, pageSize,
                search, sortField, sortDirection,
                isActive, roleType, organizationId, createdFrom, createdTo);

            var cachedResult = await _cacheService.GetCachedAsync<PagedResultDto<RoleDto>>(cacheKey);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            // Build query with proper filtering
            IQueryable<Role> query;
            if (isSystemAdmin && !organizationId.HasValue)
            {
                // System Admin without filter - show all organizations
                query = _unitOfWork.Repository<Role>().GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(r => !r.IsDeleted);
            }
            else
            {
                // Filter by specific organization
                query = _unitOfWork.Repository<Role>().GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(r => r.OrganizationId == filterOrganizationId && !r.IsDeleted);
            }

            // Apply search filter with case-insensitive search
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(r =>
                    r.Name.ToLower().Contains(searchLower) ||
                    (r.Description != null && r.Description.ToLower().Contains(searchLower)));
            }

            if (isActive.HasValue)
                query = query.Where(r => r.IsActive == isActive.Value);

            if (!string.IsNullOrEmpty(roleType) && roleType != "all")
                query = query.Where(r => r.RoleType == roleType);

            if (createdFrom.HasValue)
                query = query.Where(r => r.CreatedAtUtc >= createdFrom.Value);

            if (createdTo.HasValue)
                query = query.Where(r => r.CreatedAtUtc <= createdTo.Value);

            var totalCount = await query.CountAsync();

            query = ApplySortingAtDatabase(query, sortField, sortDirection);

            var roles = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    RoleType = r.RoleType,
                    ParentRoleId = r.ParentRoleId,
                    Level = r.Level,
                    IsSystemRole = r.IsSystemRole,
                    IsActive = r.IsActive,
                    SortOrder = r.SortOrder,
                    Color = r.Color,
                    Icon = r.Icon,
                    CreatedAtUtc = r.CreatedAtUtc,
                    LastModifiedAtUtc = r.LastModifiedAtUtc ?? r.CreatedAtUtc,
                    CreatedBy = r.CreatedBy, // Will resolve to name below
                    UpdatedBy = r.ModifiedBy, // Will resolve to name below
                    UpdatedAtUtc = r.ModifiedAtUtc,
                    OrganizationId = r.OrganizationId,
                    OrganizationName = null // Will be populated below
                })
                .ToListAsync();

            // Resolve CreatedBy and UpdatedBy user names
            if (roles.Any())
            {
                var userIdStrings = roles
                    .SelectMany(r => new[] { r.CreatedBy, r.UpdatedBy })
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
                        var userNames = await _unitOfWork.Repository<User>()
                            .GetQueryable()
                            .IgnoreQueryFilters()
                            .Where(u => userIds.Contains(u.Id))
                            .Select(u => new { Id = u.Id.ToString(), FullName = u.FullName })
                            .ToDictionaryAsync(u => u.Id, u => u.FullName);

                        foreach (var role in roles)
                        {
                            // Parse and set CreatedBy ID and Name
                            if (!string.IsNullOrEmpty(role.CreatedBy) && Guid.TryParse(role.CreatedBy, out var createdId))
                            {
                                role.CreatedById = createdId;
                                if (userNames.TryGetValue(role.CreatedBy, out var createdByName))
                                {
                                    role.CreatedByName = createdByName;
                                    role.CreatedBy = createdByName; // Keep for backward compatibility
                                }
                            }

                            // Parse and set ModifiedBy ID and Name (UpdatedBy is ModifiedBy)
                            if (!string.IsNullOrEmpty(role.UpdatedBy) && Guid.TryParse(role.UpdatedBy, out var modifiedId))
                            {
                                role.ModifiedById = modifiedId;
                                if (userNames.TryGetValue(role.UpdatedBy, out var modifiedByName))
                                {
                                    role.ModifiedByName = modifiedByName;
                                    role.UpdatedBy = modifiedByName; // Keep for backward compatibility
                                }
                            }
                        }
                    }
                }
            }

                // Populate organization names
                if (roles.Any())
                {
                    var orgIds = roles.Select(r => r.OrganizationId).Distinct().ToList();
                    var orgRepo = _unitOfWork.Repository<Organization>();
                    var organizations = await orgRepo.GetQueryable()
                        .IgnoreQueryFilters()
                        .Where(o => orgIds.Contains(o.Id) && !o.IsDeleted)
                        .Select(o => new { o.Id, o.Name })
                        .ToDictionaryAsync(o => o.Id, o => o.Name);

                    foreach (var role in roles)
                    {
                        if (organizations.TryGetValue(role.OrganizationId, out var orgName))
                        {
                            role.OrganizationName = orgName;
                        }
                    }
                }

                // Load permissions efficiently with single optimized query
            if (roles.Any())
            {
                var roleIds = roles.Select(r => r.Id).ToList();
                var rolePermissions = await _unitOfWork.Repository<RolePermission>().GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(rp => roleIds.Contains(rp.RoleId) && !rp.IsDeleted)
                    .Join(_unitOfWork.Repository<Permission>().GetQueryable().IgnoreQueryFilters().Where(p => !p.IsDeleted),
                        rp => rp.PermissionId,
                        p => p.Id,
                        (rp, p) => new { RoleId = rp.RoleId, PermissionId = p.Id, PermissionName = p.Name })
                    .ToListAsync();

                foreach (var role in roles)
                {
                    var permissions = rolePermissions.Where(rp => rp.RoleId == role.Id).ToList();
                    role.PermissionCount = permissions.Count;
                    role.PermissionNames = permissions.Select(p => p.PermissionName).ToList();
                }

                // Load user counts efficiently (excluding deleted users)
                var userCounts = await _unitOfWork.Repository<UserRole>().GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(ur => roleIds.Contains(ur.RoleId) && !ur.IsDeleted)
                    .Join(_unitOfWork.Repository<User>().GetQueryable().IgnoreQueryFilters().Where(u => !u.IsDeleted),
                        ur => ur.UserId,
                        u => u.Id,
                        (ur, u) => ur)
                    .GroupBy(ur => ur.RoleId)
                    .Select(g => new { RoleId = g.Key, UserCount = g.Count() })
                    .ToListAsync();

                foreach (var role in roles)
                {
                    var userCount = userCounts.FirstOrDefault(uc => uc.RoleId == role.Id);
                    role.UserCount = userCount?.UserCount ?? 0;
                }
            }

            var result = new PagedResultDto<RoleDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = roles
            };

            await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
            return result;
        });
    }

    public async Task<RoleDto?> GetRoleByIdAsync(Guid id)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();

        var cacheKey = $"role:detail:{id}";
        var cachedResult = await _cacheService.GetCachedAsync<RoleDto>(cacheKey);
        if (cachedResult != null) return cachedResult;

        IQueryable<Role> roleQuery;
        if (isSystemAdmin)
        {
            // System Admin can view roles from any organization
            roleQuery = _unitOfWork.Repository<Role>().GetQueryable()
                .IgnoreQueryFilters()
                .Where(r => r.Id == id && !r.IsDeleted);
        }
        else
        {
            // Regular users can only view roles from their organization
            roleQuery = _unitOfWork.Repository<Role>().GetQueryable()
                .Where(r => r.Id == id && r.OrganizationId == organizationId && !r.IsDeleted);
        }

        var role = await roleQuery
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                RoleType = r.RoleType,
                ParentRoleId = r.ParentRoleId,
                Level = r.Level,
                IsSystemRole = r.IsSystemRole,
                IsActive = r.IsActive,
                SortOrder = r.SortOrder,
                Color = r.Color,
                Icon = r.Icon,
                CreatedAtUtc = r.CreatedAtUtc,
                LastModifiedAtUtc = r.LastModifiedAtUtc,
                CreatedBy = r.CreatedBy, // Will resolve to name below
                UpdatedBy = r.ModifiedBy, // Will resolve to name below
                UpdatedAtUtc = r.ModifiedAtUtc,
                OrganizationId = r.OrganizationId,
                OrganizationName = null // Will be populated below
            })
            .FirstOrDefaultAsync();

        if (role != null)
        {
            // Resolve CreatedBy and UpdatedBy user names and IDs
            var userRepo = _unitOfWork.Repository<User>();
            Guid? createdById = null;
            Guid? modifiedById = null;
            string? createdByName = null;
            string? modifiedByName = null;

            if (!string.IsNullOrEmpty(role.CreatedBy) && Guid.TryParse(role.CreatedBy, out var createdId))
            {
                createdById = createdId;
                var createdByUser = await userRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == createdId && !u.IsDeleted);
                if (createdByUser != null)
                {
                    createdByName = createdByUser.FullName;
                    role.CreatedBy = createdByName; // Keep for backward compatibility
                }
            }

            if (!string.IsNullOrEmpty(role.UpdatedBy) && Guid.TryParse(role.UpdatedBy, out var modifiedId))
            {
                modifiedById = modifiedId;
                var modifiedByUser = await userRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == modifiedId && !u.IsDeleted);
                if (modifiedByUser != null)
                {
                    modifiedByName = modifiedByUser.FullName;
                    role.UpdatedBy = modifiedByName; // Keep for backward compatibility
                }
            }
            
            // Populate organization name
            if (role.OrganizationId != Guid.Empty)
            {
                var orgRepo = _unitOfWork.Repository<Organization>();
                var org = await orgRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(o => o.Id == role.OrganizationId && !o.IsDeleted);
                role.OrganizationName = org?.Name;
            }

            role.CreatedById = createdById;
            role.CreatedByName = createdByName;
            role.ModifiedById = modifiedById;
            role.ModifiedByName = modifiedByName;

            // Load permission count
            var permissionCount = await _unitOfWork.Repository<RolePermission>().GetQueryable()
                .IgnoreQueryFilters()
                .Where(rp => rp.RoleId == id && !rp.IsDeleted)
                .Join(_unitOfWork.Repository<Permission>().GetQueryable().IgnoreQueryFilters().Where(p => !p.IsDeleted),
                    rp => rp.PermissionId,
                    p => p.Id,
                    (rp, p) => rp)
                .CountAsync();
            
            role.PermissionCount = permissionCount;

            // Load user count (excluding deleted users)
            var userCount = await _unitOfWork.Repository<UserRole>().GetQueryable()
                .IgnoreQueryFilters()
                .Where(ur => ur.RoleId == id && !ur.IsDeleted)
                .Join(_unitOfWork.Repository<User>().GetQueryable().Where(u => !u.IsDeleted),
                    ur => ur.UserId,
                    u => u.Id,
                    (ur, u) => ur)
                .CountAsync();
            
            role.UserCount = userCount;

            await _cacheService.SetCacheAsync(cacheKey, role, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
        }

        return role;
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleDto createDto)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();

        // Check if role name already exists
        var existingRole = await _unitOfWork.Repository<Role>().GetQueryable()
            .FirstOrDefaultAsync(r => r.Name == createDto.Name && r.OrganizationId == organizationId && !r.IsDeleted);

        if (existingRole != null)
        {
            throw new InvalidOperationException("A role with this name already exists.");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = createDto.Name,
            Description = createDto.Description,
            RoleType = createDto.RoleType,
            ParentRoleId = createDto.ParentRoleId,
            Level = createDto.ParentRoleId.HasValue ? 1 : 0, // Simple level calculation
            IsSystemRole = false,
            IsActive = createDto.IsActive,
            SortOrder = createDto.SortOrder,
            Color = createDto.Color,
            Icon = createDto.Icon,
            OrganizationId = organizationId,
            CreatedBy = userId.ToString(),
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedBy = userId.ToString(),
            ModifiedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Role>().AddAsync(role);
        await _unitOfWork.SaveChangesAsync();

        // Clear ALL role caches
        await InvalidateAllRoleCachesAsync(organizationId);
        // No users have this role yet (new role), so no user permission cache invalidation needed

        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            RoleType = role.RoleType,
            ParentRoleId = role.ParentRoleId,
            Level = role.Level,
            IsSystemRole = role.IsSystemRole,
            IsActive = role.IsActive,
            SortOrder = role.SortOrder,
            Color = role.Color,
            Icon = role.Icon,
            CreatedAtUtc = role.CreatedAtUtc,
            LastModifiedAtUtc = role.LastModifiedAtUtc,
            CreatedBy = role.CreatedBy,
            UpdatedBy = role.ModifiedBy,
            UpdatedAtUtc = role.ModifiedAtUtc
        };
    }

    public async Task<RoleDto> UpdateRoleAsync(Guid id, UpdateRoleDto updateDto)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();

        Role? role;
        if (isSystemAdmin)
        {
            // System Admin can view roles from any organization, but can only edit their own organization's roles
            role = await _unitOfWork.Repository<Role>().GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            
            if (role != null && role.OrganizationId != organizationId)
            {
                // System Admin cannot edit roles from other organizations
                throw new UnauthorizedAccessException("You can only edit roles from your own organization");
            }
        }
        else
        {
            // Regular users can only edit roles from their organization
            role = await _unitOfWork.Repository<Role>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId && !r.IsDeleted);
        }

        if (role == null)
        {
            throw new InvalidOperationException("Role not found.");
        }

        // Check if new name conflicts with existing roles
        if (role.Name != updateDto.Name)
        {
            var existingRole = await _unitOfWork.Repository<Role>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Name == updateDto.Name && r.OrganizationId == organizationId && r.Id != id && !r.IsDeleted);

            if (existingRole != null)
            {
                throw new InvalidOperationException("A role with this name already exists.");
            }
        }

        role.Name = updateDto.Name;
        role.Description = updateDto.Description;
        role.RoleType = updateDto.RoleType;
        role.ParentRoleId = updateDto.ParentRoleId;
        role.IsActive = updateDto.IsActive;
        role.SortOrder = updateDto.SortOrder;
        role.Color = updateDto.Color;
        role.Icon = updateDto.Icon;
        role.ModifiedBy = userId.ToString();
        role.ModifiedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        // Clear ALL role caches
        await InvalidateAllRoleCachesAsync(organizationId);
        await _cacheService.RemoveCacheAsync($"role:detail:{id}");
        // Invalidate role permissions cache
        await _cacheService.RemoveCacheAsync($"role:permissions:{id}:{organizationId}");
        // Invalidate user permission caches for ALL users with this role (role data changed)
        await InvalidateUserPermissionCachesForRoleAsync(id, organizationId);

        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            RoleType = role.RoleType,
            ParentRoleId = role.ParentRoleId,
            Level = role.Level,
            IsSystemRole = role.IsSystemRole,
            IsActive = role.IsActive,
            SortOrder = role.SortOrder,
            Color = role.Color,
            Icon = role.Icon,
            CreatedAtUtc = role.CreatedAtUtc,
            LastModifiedAtUtc = role.LastModifiedAtUtc,
            CreatedBy = role.CreatedBy,
            UpdatedBy = role.ModifiedBy,
            UpdatedAtUtc = role.ModifiedAtUtc
        };
    }

    public async Task<bool> DeleteRoleAsync(Guid id)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();

        Role? role;
        if (isSystemAdmin)
        {
            // System Admin can view roles from any organization, but can only delete their own organization's roles
            role = await _unitOfWork.Repository<Role>().GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            
            if (role != null && role.OrganizationId != organizationId)
            {
                // System Admin cannot delete roles from other organizations
                throw new UnauthorizedAccessException("You can only delete roles from your own organization");
            }
        }
        else
        {
            // Regular users can only delete roles from their organization
            role = await _unitOfWork.Repository<Role>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId && !r.IsDeleted);
        }

        if (role == null)
        {
            return false;
        }

        // Check if role is assigned to any users (excluding deleted users)
        var userCount = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .Where(ur => ur.RoleId == id && ur.OrganizationId == organizationId)
            .Join(_unitOfWork.Repository<User>().GetQueryable().Where(u => !u.IsDeleted),
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => ur)
            .CountAsync();

        if (userCount > 0)
        {
            throw new InvalidOperationException("Cannot delete role that is assigned to users. Please remove all user assignments first.");
        }

        role.IsDeleted = true;
        role.DeletedAtUtc = DateTimeOffset.UtcNow;
        role.DeletedBy = userId.ToString();
        role.ModifiedAtUtc = DateTimeOffset.UtcNow;
        role.ModifiedBy = userId.ToString();

        _unitOfWork.Repository<Role>().Update(role);
        await _unitOfWork.SaveChangesAsync();

        // Clear ALL role caches
        await InvalidateAllRoleCachesAsync(organizationId);
        await _cacheService.RemoveCacheAsync($"role:detail:{id}");
        // Invalidate role permissions cache
        await _cacheService.RemoveCacheAsync($"role:permissions:{id}:{organizationId}");
        // Invalidate user permission caches for ALL users with this role (role deleted affects permissions)
        await InvalidateUserPermissionCachesForRoleAsync(id, organizationId);

        return true;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();

        Role? role;
        if (isSystemAdmin)
        {
            // System Admin can view roles from any organization, but can only modify their own organization's roles
            role = await _unitOfWork.Repository<Role>().GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            
            if (role != null && role.OrganizationId != organizationId)
            {
                // System Admin cannot modify roles from other organizations
                throw new UnauthorizedAccessException("You can only modify roles from your own organization");
            }
        }
        else
        {
            // Regular users can only modify roles from their organization
            role = await _unitOfWork.Repository<Role>().GetQueryable()
                .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == organizationId && !r.IsDeleted);
        }

        if (role == null) return false;

        role.IsActive = isActive;
        role.ModifiedAtUtc = DateTimeOffset.UtcNow;
        role.ModifiedBy = userId.ToString();

        await _unitOfWork.SaveChangesAsync();

        // Clear ALL role caches
        await InvalidateAllRoleCachesAsync(organizationId);
        await _cacheService.RemoveCacheAsync($"role:detail:{id}");
        // Invalidate role permissions cache
        await _cacheService.RemoveCacheAsync($"role:permissions:{id}:{organizationId}");
        // CRITICAL: When role IsActive changes, all users with this role lose/gain permissions
        await InvalidateUserPermissionCachesForRoleAsync(id, organizationId);

        return true;
    }

    public async Task BulkDeleteAsync(List<Guid> ids)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();

        var roles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => ids.Contains(r.Id) && r.OrganizationId == organizationId && !r.IsDeleted)
            .ToListAsync();

        // Check if any roles are assigned to users (excluding deleted users)
        var assignedRoles = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .Where(ur => ids.Contains(ur.RoleId) && ur.OrganizationId == organizationId)
            .Join(_unitOfWork.Repository<User>().GetQueryable().Where(u => !u.IsDeleted),
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => ur)
            .Select(ur => ur.RoleId)
            .Distinct()
            .ToListAsync();

        if (assignedRoles.Any())
        {
            throw new InvalidOperationException($"Cannot delete roles that are assigned to users. Please remove user assignments for roles: {string.Join(", ", assignedRoles)}");
        }

        foreach (var role in roles)
        {
            role.IsDeleted = true;
            role.DeletedAtUtc = DateTimeOffset.UtcNow;
            role.ModifiedAtUtc = DateTimeOffset.UtcNow;
            role.ModifiedBy = userId.ToString();
            _unitOfWork.Repository<Role>().Update(role);
        }

        await _unitOfWork.SaveChangesAsync();

        // Clear ALL role caches
        await InvalidateAllRoleCachesAsync(organizationId);
        foreach (var id in ids)
        {
            await _cacheService.RemoveCacheAsync($"role:detail:{id}");
            // Invalidate role permissions cache
            await _cacheService.RemoveCacheAsync($"role:permissions:{id}:{organizationId}");
            // Invalidate user permission caches for each deleted role
            await InvalidateUserPermissionCachesForRoleAsync(id, organizationId);
        }
    }

    public async Task<List<RoleDto>> BulkCloneAsync(List<Guid> ids)
    {
        if (ids == null || !ids.Any())
            return new List<RoleDto>();

        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var roleRepo = _unitOfWork.Repository<Role>();
        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();

        // Get original roles
        var originalRoles = await roleRepo.GetQueryable()
            .Where(r => ids.Contains(r.Id) && r.OrganizationId == organizationId && !r.IsDeleted)
            .ToListAsync();

        if (!originalRoles.Any())
            return new List<RoleDto>();

        var clonedRoles = new List<RoleDto>();
        var generatedNames = new HashSet<string>(); // Track names in current batch
        var clonedRoleEntities = new List<Role>(); // Store role entities before saving
        var rolePermissionsToAdd = new List<RolePermission>(); // Store role permissions to add

        // First, get all existing role names from database to avoid conflicts
        var existingNames = await roleRepo.GetQueryable()
            .Where(r => r.OrganizationId == organizationId && !r.IsDeleted)
            .Select(r => r.Name)
            .ToListAsync();
        
        generatedNames.UnionWith(existingNames);

        foreach (var originalRole in originalRoles)
        {
            // Generate unique role name with GUID to ensure uniqueness
            var baseName = originalRole.Name;
            var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
            var newName = $"{baseName} (Copy {uniqueSuffix})";
            var counter = 1;
            
            // Check if name already exists in current batch (includes database names)
            while (generatedNames.Contains(newName))
            {
                uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
                newName = $"{baseName} (Copy {uniqueSuffix})";
                counter++;
                if (counter > 100) break; // Safety limit
            }
            
            generatedNames.Add(newName); // Track this name for current batch

            // Create cloned role
            var clonedRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = newName,
                Description = originalRole.Description,
                RoleType = originalRole.RoleType,
                ParentRoleId = originalRole.ParentRoleId, // Keep same parent
                Level = originalRole.Level,
                IsSystemRole = false, // Cloned roles are never system roles
                IsActive = false, // Cloned roles start as inactive
                SortOrder = originalRole.SortOrder,
                Color = originalRole.Color,
                Icon = originalRole.Icon,
                OrganizationId = organizationId,
                CreatedBy = userId.ToString(),
                CreatedAtUtc = DateTime.UtcNow,
                ModifiedBy = userId.ToString(),
                ModifiedAtUtc = DateTime.UtcNow
            };

            clonedRoleEntities.Add(clonedRole);

            // Get original role permissions for later assignment
            var originalPermissions = await rolePermissionRepo.GetQueryable()
                .Where(rp => rp.RoleId == originalRole.Id && rp.OrganizationId == organizationId && !rp.IsDeleted)
                .ToListAsync();

            foreach (var originalPermission in originalPermissions)
            {
                rolePermissionsToAdd.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    RoleId = clonedRole.Id,
                    PermissionId = originalPermission.PermissionId,
                    CreatedBy = userId.ToString(),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // Add all role entities at once
        foreach (var clonedRole in clonedRoleEntities)
        {
            await roleRepo.AddAsync(clonedRole);
        }

        // Save all cloned roles in a single transaction
        await _unitOfWork.SaveChangesAsync();

        // Add all role permissions at once
        foreach (var rolePermission in rolePermissionsToAdd)
        {
            await rolePermissionRepo.AddAsync(rolePermission);
        }

        // Save all role permissions in a single transaction
        await _unitOfWork.SaveChangesAsync();

        // Build DTOs after saving
        foreach (var clonedRole in clonedRoleEntities)
        {
            clonedRoles.Add(new RoleDto
            {
                Id = clonedRole.Id,
                Name = clonedRole.Name,
                Description = clonedRole.Description,
                RoleType = clonedRole.RoleType,
                ParentRoleId = clonedRole.ParentRoleId,
                Level = clonedRole.Level,
                IsSystemRole = clonedRole.IsSystemRole,
                IsActive = clonedRole.IsActive,
                SortOrder = clonedRole.SortOrder,
                Color = clonedRole.Color,
                Icon = clonedRole.Icon,
                CreatedAtUtc = clonedRole.CreatedAtUtc,
                LastModifiedAtUtc = clonedRole.LastModifiedAtUtc,
                CreatedBy = clonedRole.CreatedBy,
                UpdatedBy = clonedRole.ModifiedBy,
                UpdatedAtUtc = clonedRole.ModifiedAtUtc
            });
        }

        // Invalidate caches - comprehensive like create/update (once for all)
        await InvalidateAllRoleCachesAsync(organizationId);
        foreach (var clonedRole in clonedRoleEntities)
        {
            await _cacheService.RemoveCacheAsync($"role:detail:{clonedRole.Id}");
            // Invalidate user permission caches since new role with permissions was created
            await InvalidateUserPermissionCachesForRoleAsync(clonedRole.Id, organizationId);
        }

        return clonedRoles;
    }

    public async Task<List<RoleHierarchyDto>> GetRoleHierarchyAsync()
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        var cacheKey = _cacheService.GenerateDropdownCacheKey("role_hierarchy", organizationId);
        var cachedResult = await _cacheService.GetCachedAsync<List<RoleHierarchyDto>>(cacheKey);
        if (cachedResult != null) return cachedResult;

        var roles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => r.OrganizationId == organizationId && !r.IsDeleted)
            .OrderBy(r => r.Level)
            .ThenBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .ToListAsync();

        // Get user counts efficiently (excluding deleted users)
        var roleIds = roles.Select(r => r.Id).ToList();
        var userCounts = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .Where(ur => roleIds.Contains(ur.RoleId) && ur.OrganizationId == organizationId)
            .Join(_unitOfWork.Repository<User>().GetQueryable().Where(u => !u.IsDeleted),
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => ur)
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, UserCount = g.Count() })
            .ToListAsync();

        // Get permission counts efficiently (excluding deleted permissions)
        var permissionCounts = await _unitOfWork.Repository<RolePermission>().GetQueryable()
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.OrganizationId == organizationId)
            .Join(_unitOfWork.Repository<Permission>().GetQueryable().Where(p => !p.IsDeleted),
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => rp)
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, PermissionCount = g.Count() })
            .ToListAsync();

        var hierarchyList = roles.Select(r => new RoleHierarchyDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            RoleType = r.RoleType,
            ParentRoleId = r.ParentRoleId,
            Level = r.Level,
            IsActive = r.IsActive,
            SortOrder = r.SortOrder,
            Color = r.Color,
            Icon = r.Icon,
            UserCount = userCounts.FirstOrDefault(uc => uc.RoleId == r.Id)?.UserCount ?? 0,
            PermissionCount = permissionCounts.FirstOrDefault(pc => pc.RoleId == r.Id)?.PermissionCount ?? 0,
            Children = new List<RoleHierarchyDto>()
        }).ToList();

        await _cacheService.SetCacheAsync(cacheKey, hierarchyList, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
        return hierarchyList;
    }

    public async Task<List<RoleDto>> GetChildRolesAsync(Guid parentRoleId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        var roles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => r.ParentRoleId == parentRoleId && r.OrganizationId == organizationId && !r.IsDeleted)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                RoleType = r.RoleType,
                ParentRoleId = r.ParentRoleId,
                Level = r.Level,
                IsSystemRole = r.IsSystemRole,
                IsActive = r.IsActive,
                SortOrder = r.SortOrder,
                Color = r.Color,
                Icon = r.Icon,
                CreatedAtUtc = r.CreatedAtUtc,
                LastModifiedAtUtc = r.LastModifiedAtUtc,
                CreatedBy = r.CreatedBy,
                UpdatedBy = r.ModifiedBy,
                UpdatedAtUtc = r.ModifiedAtUtc
            })
            .ToListAsync();

        return roles;
    }

    public async Task<bool> AssignPermissionToRoleAsync(Guid roleId, Guid permissionId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        // Check if role exists and is not deleted
        var role = await _unitOfWork.Repository<Role>().GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId && !r.IsDeleted);

        if (role == null)
        {
            throw new InvalidOperationException("Role not found or has been deleted.");
        }

        // Check if user is System Admin
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();

        // Check if permission exists and is not deleted - Permissions are system-wide
        var permission = await _unitOfWork.Repository<Permission>().GetQueryable()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == permissionId && !p.IsDeleted);

        if (permission == null)
        {
            throw new InvalidOperationException("Permission not found or has been deleted.");
        }

        // Company Admin can only assign permissions that are NOT System Admin only
        if (!isSystemAdmin && permission.IsSystemAdminOnly)
        {
            throw new UnauthorizedAccessException("Company Administrators cannot assign System Admin only permissions.");
        }

        var existingAssignment = await _unitOfWork.Repository<RolePermission>().GetQueryable()
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId && rp.OrganizationId == organizationId);

        if (existingAssignment != null) return true;

        var rolePermission = new RolePermission
        {
            Id = Guid.NewGuid(),
            RoleId = roleId,
            PermissionId = permissionId,
            OrganizationId = organizationId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.Repository<RolePermission>().AddAsync(rolePermission);
        await _unitOfWork.SaveChangesAsync();

        // Audit: permission granted to role
        var actorId = _tenantService.GetCurrentUserId();
        _logger.LogInformation("Permission {PermissionId} assigned to Role {RoleId} by User {ActorId} (Org {OrgId})", permissionId, roleId, actorId, organizationId);

        await _cacheService.RemoveCacheAsync($"role:detail:{roleId}");
        // Invalidate role permissions cache
        await _cacheService.RemoveCacheAsync($"role:permissions:{roleId}:{organizationId}");
        // Invalidate permission list cache (system-wide) since permission assignment changed
        var systemOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await _cacheService.RemoveCacheByPatternAsync($"permissions:list:{systemOrgId}*");
        await InvalidateAllRoleCachesAsync(organizationId);
        // Invalidate all user permission caches since role permissions changed
        await InvalidateAllUserPermissionCachesAsync(organizationId);

        return true;
    }

    public async Task<bool> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        var rolePermission = await _unitOfWork.Repository<RolePermission>().GetQueryable()
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId && rp.OrganizationId == organizationId);

        if (rolePermission == null) return false;

        _unitOfWork.Repository<RolePermission>().Remove(rolePermission);
        await _unitOfWork.SaveChangesAsync();

        // Audit: permission revoked from role
        var actorId = _tenantService.GetCurrentUserId();
        _logger.LogInformation("Permission {PermissionId} removed from Role {RoleId} by User {ActorId} (Org {OrgId})", permissionId, roleId, actorId, organizationId);

        await _cacheService.RemoveCacheAsync($"role:detail:{roleId}");
        // Invalidate role permissions cache
        await _cacheService.RemoveCacheAsync($"role:permissions:{roleId}:{organizationId}");
        // Invalidate permission list cache (system-wide) since permission assignment changed
        var systemOrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        await _cacheService.RemoveCacheByPatternAsync($"permissions:list:{systemOrgId}*");
        await InvalidateAllRoleCachesAsync(organizationId);
        // Invalidate all user permission caches since role permissions changed
        await InvalidateAllUserPermissionCachesAsync(organizationId);

        return true;
    }

    public async Task<List<PermissionDto>> GetRolePermissionsAsync(Guid roleId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        // Check cache first
        var cacheKey = $"role:permissions:{roleId}:{organizationId}";
        var cachedPermissions = await _cacheService.GetCachedAsync<List<PermissionDto>>(cacheKey);
        if (cachedPermissions != null)
        {
            return cachedPermissions;
        }

        // Check if user is System Admin
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();

        // Permissions are system-wide, ignore organization filter
        var permissionQuery = _unitOfWork.Repository<Permission>().GetQueryable()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.IsActive);

        // Filter out System Admin only permissions for Company Admin
        if (!isSystemAdmin)
        {
            permissionQuery = permissionQuery.Where(p => !p.IsSystemAdminOnly);
        }

        var permissions = await _unitOfWork.Repository<RolePermission>().GetQueryable()
            .Where(rp => rp.RoleId == roleId && rp.OrganizationId == organizationId && !rp.IsDeleted)
            .Join(permissionQuery,
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => new PermissionDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    Module = p.Module,
                    Code = p.Code,
                    Action = p.Action,
                    Resource = p.Resource,
                    IsSystemPermission = p.IsSystemPermission,
                    IsSystemAdminOnly = p.IsSystemAdminOnly,
                    IsActive = p.IsActive,
                    SortOrder = p.SortOrder,
                    Category = p.Category
                })
            .ToListAsync();

        // Cache the result
        await _cacheService.SetCacheAsync(cacheKey, permissions, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));

        return permissions;
    }

    public async Task<List<PermissionDto>> GetEffectivePermissionsAsync(Guid roleId)
    {
        // For now, just return direct permissions
        // In a more complex system, this would include inherited permissions
        return await GetRolePermissionsAsync(roleId);
    }

    public async Task<bool> AssignRoleToUserAsync(Guid userId, Guid roleId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        // Check if user exists and is not deleted
        var user = await _unitOfWork.Repository<User>().GetQueryable()
            .FirstOrDefaultAsync(u => u.Id == userId && u.OrganizationId == organizationId && !u.IsDeleted);

        if (user == null)
        {
            throw new InvalidOperationException("User not found or has been deleted.");
        }

        // Check if role exists and is not deleted
        var role = await _unitOfWork.Repository<Role>().GetQueryable()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId && !r.IsDeleted);

        if (role == null)
        {
            throw new InvalidOperationException("Role not found or has been deleted.");
        }

        var existingAssignment = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.OrganizationId == organizationId);

        if (existingAssignment != null) return true;

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            OrganizationId = organizationId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _unitOfWork.Repository<UserRole>().AddAsync(userRole);
        await _unitOfWork.SaveChangesAsync();

        // Audit: role assigned to user
        var actorId = _tenantService.GetCurrentUserId();
        _logger.LogInformation("Role {RoleId} assigned to User {UserId} by User {ActorId} (Org {OrgId})", roleId, userId, actorId, organizationId);

        await _cacheService.RemoveCacheAsync($"user:detail:{userId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:list:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:stats:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:dropdown:{organizationId}");
        await _cacheService.RemoveCacheAsync($"role:detail:{roleId}");
        await InvalidateAllRoleCachesAsync(organizationId);
        // Invalidate user permission caches since user role assignment changed
        await InvalidateUserPermissionCacheAsync(userId, organizationId);

        return true;
    }

    public async Task<bool> RemoveRoleFromUserAsync(Guid userId, Guid roleId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        var userRole = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId && ur.OrganizationId == organizationId);

        if (userRole == null) return false;

        _unitOfWork.Repository<UserRole>().Remove(userRole);
        await _unitOfWork.SaveChangesAsync();

        // Audit: role removed from user
        var actorId = _tenantService.GetCurrentUserId();
        _logger.LogInformation("Role {RoleId} removed from User {UserId} by User {ActorId} (Org {OrgId})", roleId, userId, actorId, organizationId);

        await _cacheService.RemoveCacheAsync($"user:detail:{userId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:list:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:stats:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:dropdown:{organizationId}");
        await _cacheService.RemoveCacheAsync($"role:detail:{roleId}");
        await InvalidateAllRoleCachesAsync(organizationId);
        // Invalidate user permission caches since user role assignment changed
        await InvalidateUserPermissionCacheAsync(userId, organizationId);

        return true;
    }

    public async Task<List<UserDto>> GetRoleUsersAsync(Guid roleId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        var users = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .Where(ur => ur.RoleId == roleId && ur.OrganizationId == organizationId)
            .Join(_unitOfWork.Repository<User>().GetQueryable().Where(u => !u.IsDeleted),
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FullName = u.FullName,
                    IsActive = u.IsActive,
                    IsEmailVerified = u.IsEmailVerified,
                    CreatedAtUtc = u.CreatedAtUtc
                })
            .ToListAsync();

        return users;
    }

    public async Task<List<RoleDto>> GetUserRolesAsync(Guid userId)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        var roles = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .Where(ur => ur.UserId == userId && ur.OrganizationId == organizationId)
            .Join(_unitOfWork.Repository<Role>().GetQueryable().Where(r => !r.IsDeleted),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new RoleDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Description = r.Description,
                    RoleType = r.RoleType,
                    ParentRoleId = r.ParentRoleId,
                    Level = r.Level,
                    IsSystemRole = r.IsSystemRole,
                    IsActive = r.IsActive,
                    SortOrder = r.SortOrder,
                    Color = r.Color,
                    Icon = r.Icon,
                    CreatedAtUtc = r.CreatedAtUtc,
                    LastModifiedAtUtc = r.LastModifiedAtUtc ?? r.CreatedAtUtc,
                    CreatedBy = r.CreatedBy,
                    UpdatedBy = r.ModifiedBy,
                    UpdatedAtUtc = r.ModifiedAtUtc
                })
            .ToListAsync();

        return roles;
    }

    // ========================================
    // Async Export Methods (Non-Blocking)
    // ========================================

    public async Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
    {
        // Capture context before background task
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var userName = _tenantService.GetCurrentUserName();

        return await _importExportService.StartExportJobAsync<Role>(
            entityType: "Role",
            format: format,
            dataFetcher: async (f) =>
            {
                // Create new scope to get fresh DbContext using IServiceScopeFactory
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
                
                // CRITICAL: Set background context to ensure DbContext global query filter works correctly
                var orgId = organizationId; // Use captured value
                scopedTenantService.SetBackgroundContext(orgId, userId, userName);
                
                var repo = scopedUnitOfWork.Repository<Role>();

                // Fetch roles based on filters
                var search = f.GetValueOrDefault("search")?.ToString();
                var isActive = f.GetValueOrDefault("isActive") as bool?;
                var roleType = f.GetValueOrDefault("roleType")?.ToString();
                var parentRoleId = f.GetValueOrDefault("parentRoleId")?.ToString();
                var selectedIds = f.GetValueOrDefault("selectedIds");

                // Parse parentRoleId if provided
                Guid? parentRoleIdGuid = null;
                if (!string.IsNullOrEmpty(parentRoleId) && parentRoleId != "all" && Guid.TryParse(parentRoleId, out var parsedGuid))
                    parentRoleIdGuid = parsedGuid;

                // Parse date filters - can be DateTimeOffset or string
                DateTimeOffset? createdFrom = null;
                var createdFromValue = f.GetValueOrDefault("createdFrom");
                if (createdFromValue != null)
                {
                    if (createdFromValue is DateTimeOffset dtValue)
                        createdFrom = dtValue;
                    else if (createdFromValue is string strValue && DateTimeOffset.TryParse(strValue, out var parsedDate))
                        createdFrom = parsedDate;
                }

                DateTimeOffset? createdTo = null;
                var createdToValue = f.GetValueOrDefault("createdTo");
                if (createdToValue != null)
                {
                    if (createdToValue is DateTimeOffset dtValue)
                        createdTo = dtValue;
                    else if (createdToValue is string strValue && DateTimeOffset.TryParse(strValue, out var parsedDate))
                        createdTo = parsedDate;
                }

                var searchLower = search?.ToLower();
                List<Role> rolesToExport;
                
                // If specific IDs are selected, only fetch those
                if (selectedIds != null)
                {
                    var idsList = selectedIds as List<Guid>;
                    if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
                    {
                        idsList = enumerableIds.ToList();
                    }

                    if (idsList != null && idsList.Any())
                    {
                        var roleIds = idsList; // Make a copy for lambda
                        var selectedRoles = await repo.FindManyAsync(r => r.OrganizationId == orgId && !r.IsDeleted && roleIds.Contains(r.Id));
                        rolesToExport = selectedRoles.ToList();
                    }
                    else
                    {
                        rolesToExport = new List<Role>();
                    }
                }
                else
                {
                    // Build query with all filters
                    var query = repo.GetQueryable()
                        .Where(r => r.OrganizationId == orgId && !r.IsDeleted);

                    // Apply search filter
                    if (!string.IsNullOrEmpty(search))
                    {
                        searchLower = search.ToLower();
                        query = query.Where(r => 
                            r.Name.ToLower().Contains(searchLower) || 
                            (r.Description != null && r.Description.ToLower().Contains(searchLower)));
                    }

                    // Apply other filters
                    if (isActive.HasValue)
                        query = query.Where(r => r.IsActive == isActive.Value);

                    if (!string.IsNullOrEmpty(roleType) && roleType != "all")
                        query = query.Where(r => r.RoleType == roleType);

                    if (parentRoleIdGuid.HasValue)
                        query = query.Where(r => r.ParentRoleId == parentRoleIdGuid.Value);

                    // Apply date range filters
                    if (createdFrom.HasValue)
                        query = query.Where(r => r.CreatedAtUtc >= createdFrom.Value);

                    if (createdTo.HasValue)
                        query = query.Where(r => r.CreatedAtUtc <= createdTo.Value);

                    rolesToExport = await query.ToListAsync();
                }

                return rolesToExport;
            },
            filters: filters,
            columnMapper: MapRoleToExportColumns
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

    /// <summary>
    /// Invalidates ALL role-related caches (list, stats, dropdown, hierarchy) for the organization
    /// </summary>
    private async Task InvalidateAllRoleCachesAsync(Guid organizationId)
    {
        // Clear all role cache types for this organization
        await _cacheService.RemoveCacheByPatternAsync($"roles:list:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"roles:stats:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"roles:dropdown:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"roles:hierarchy:{organizationId}");
    }

    /// <summary>
    /// Invalidates all user permission caches for a specific user
    /// </summary>
    private async Task InvalidateUserPermissionCacheAsync(Guid userId, Guid organizationId)
    {
        await _cacheService.RemoveCacheAsync($"user_permissions_{userId}_{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_{userId}_{organizationId}");
    }

    /// <summary>
    /// Invalidates all user permission caches for all users in the organization
    /// Used when role permissions change
    /// </summary>
    private async Task InvalidateAllUserPermissionCachesAsync(Guid organizationId)
    {
        await _cacheService.RemoveCacheByPatternAsync($"user_permissions_*_{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_*_{organizationId}");
    }

    /// <summary>
    /// Invalidates user permission caches for ALL users who have a specific role
    /// Used when role data changes (Update, Delete, SetActive, etc.)
    /// </summary>
    private async Task InvalidateUserPermissionCachesForRoleAsync(Guid roleId, Guid organizationId)
    {
        // Get all users with this role
        var userIds = await _unitOfWork.Repository<UserRole>().GetQueryable()
            .Where(ur => ur.RoleId == roleId && ur.OrganizationId == organizationId)
            .Join(_unitOfWork.Repository<User>().GetQueryable().Where(u => !u.IsDeleted),
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => u.Id)
            .ToListAsync();

        // Invalidate cache for each user
        foreach (var userId in userIds)
        {
            await _cacheService.RemoveCacheAsync($"user_permissions_{userId}_{organizationId}");
            await _cacheService.RemoveCacheByPatternAsync($"user_menus_{userId}_{organizationId}");
        }

        // Also invalidate user list/dropdown caches since user roles might affect display
        await _cacheService.RemoveCacheByPatternAsync($"users:list:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:dropdown:{organizationId}");
    }

    private Dictionary<string, object> MapRoleToExportColumns(Role role)
    {
        return new Dictionary<string, object>
        {
            ["ID"] = role.Id.ToString(),
            ["Name"] = role.Name,
            ["Description"] = role.Description ?? "",
            ["Role Type"] = role.RoleType,
            ["Parent Role ID"] = role.ParentRoleId?.ToString() ?? "",
            ["Level"] = role.Level,
            ["Is System Role"] = role.IsSystemRole ? "Yes" : "No",
            ["Status"] = role.IsActive ? "Active" : "Inactive",
            ["Sort Order"] = role.SortOrder,
            ["Color"] = role.Color ?? "",
            ["Icon"] = role.Icon ?? "",
            ["Created Date"] = role.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")            
        };
    }

    // ========================================
    // Import Methods
    // ========================================

    public async Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId)
    {
        return await _importExportService.GetImportJobStatusAsync(jobId);
    }

    public async Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var userName = _tenantService.GetCurrentUserName();

        return await _importExportService.StartImportJobAsync<CreateRoleDto>(
            entityType: "Role",
            fileStream: fileStream,
            fileName: fileName,
            rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
            {
                // Validate required fields
                if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
                    return (false, "Name is required", false, false);

                // Use scoped repositories from the passed UnitOfWork
                var roleRepo = scopedUnitOfWork.Repository<Role>();

                // Check if role already exists
                var existingRole = await roleRepo.FindAsync(r =>
                    r.OrganizationId == organizationId && r.Name.ToLower() == name.ToLower() && !r.IsDeleted);

                if (existingRole != null)
                {
                    if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
                        return (true, null, false, true); // Skip

                    if (duplicateStrategy == DuplicateHandlingStrategy.Update)
                    {
                        // Update existing role
                        existingRole.Description = rowData.GetValueOrDefault("Description");
                        existingRole.RoleType = rowData.GetValueOrDefault("Role Type") ?? "CUSTOM";
                        existingRole.Color = rowData.GetValueOrDefault("Color");
                        existingRole.Icon = rowData.GetValueOrDefault("Icon");

                        if (rowData.TryGetValue("Status", out var status))
                            existingRole.IsActive = status?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;

                        if (rowData.TryGetValue("Sort Order", out var sortOrderStr) && int.TryParse(sortOrderStr, out var sortOrder))
                            existingRole.SortOrder = sortOrder;

                        // Handle Parent Role assignment
                        if (rowData.TryGetValue("Parent Role", out var parentRoleName) && !string.IsNullOrWhiteSpace(parentRoleName))
                        {
                            var parentRole = await roleRepo.FindAsync(r =>
                                r.OrganizationId == organizationId && r.Name.ToLower() == parentRoleName.ToLower() && !r.IsDeleted && r.IsActive);

                            if (parentRole != null)
                            {
                                existingRole.ParentRoleId = parentRole.Id;
                                existingRole.Level = parentRole.Level + 1;
                            }
                        }
                        else
                        {
                            existingRole.ParentRoleId = null;
                            existingRole.Level = 0;
                        }

                        existingRole.ModifiedAtUtc = DateTimeOffset.UtcNow;
                        existingRole.ModifiedBy = userId.ToString();

                        roleRepo.Update(existingRole);
                        return (true, null, true, false); // Updated
                    }
                }

                // Handle Parent Role for new role
                Guid? parentRoleId = null;
                int level = 0;

                if (rowData.TryGetValue("Parent Role", out var newParentRoleName) && !string.IsNullOrWhiteSpace(newParentRoleName))
                {
                    var parentRole = await roleRepo.FindAsync(r =>
                        r.OrganizationId == organizationId && r.Name.ToLower() == newParentRoleName.ToLower() && !r.IsDeleted && r.IsActive);

                    if (parentRole != null)
                    {
                        parentRoleId = parentRole.Id;
                        level = parentRole.Level + 1;
                    }
                }

                // Create new role
                var newRole = new Role
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Name = name,
                    Description = rowData.GetValueOrDefault("Description"),
                    RoleType = rowData.GetValueOrDefault("Role Type") ?? "CUSTOM",
                    ParentRoleId = parentRoleId,
                    Level = level,
                    IsSystemRole = false,
                    IsActive = rowData.TryGetValue("Status", out var newStatus) &&
                               newStatus?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true ||
                               newStatus == null,
                    SortOrder = rowData.TryGetValue("Sort Order", out var newSortOrderStr) && int.TryParse(newSortOrderStr, out var newSortOrder) ? newSortOrder : 0,
                    Color = rowData.GetValueOrDefault("Color") ?? "#3B82F6",
                    Icon = rowData.GetValueOrDefault("Icon") ?? "fas fa-user",
                    CreatedBy = userId.ToString(),
                    CreatedAtUtc = DateTime.UtcNow,
                    ModifiedBy = userId.ToString(),
                    ModifiedAtUtc = DateTime.UtcNow
                };

                await roleRepo.AddAsync(newRole);
                await scopedUnitOfWork.SaveChangesAsync();

                return (true, null, false, false); // Success
            },
            duplicateStrategy: duplicateStrategy
        );
    }

    public async Task<byte[]> GetImportTemplateAsync()
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        // Get parent roles for reference
        var roleRepo = _unitOfWork.Repository<Role>();
        var parentRoles = await roleRepo.FindManyAsync(r => r.OrganizationId == organizationId && !r.IsDeleted && r.IsActive);

        // Generate Excel template with dropdown validation
        return GenerateExcelImportTemplate(parentRoles);
    }

    private byte[] GenerateExcelImportTemplate(IEnumerable<Role> parentRoles)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Import Data Template
        var importSheet = workbook.Worksheets.Add("Import Data");

        // Set up headers
        var headers = new[] { "Name", "Description", "Role Type", "Parent Role", "Sort Order", "Color", "Icon", "Status" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = importSheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Add sample data row
        importSheet.Cell(2, 1).Value = "Manager";
        importSheet.Cell(2, 2).Value = "Manager role with full access";
        importSheet.Cell(2, 3).Value = "BUSINESS";
        importSheet.Cell(2, 4).Value = ""; // Parent Role (optional)
        importSheet.Cell(2, 5).Value = 10;
        importSheet.Cell(2, 6).Value = "#3B82F6";
        importSheet.Cell(2, 7).Value = "fas fa-user-tie";
        importSheet.Cell(2, 8).Value = "Active";

        // Sheet 2: Reference Data
        var referenceSheet = workbook.Worksheets.Add("Reference Data");
        referenceSheet.Cell(1, 1).Value = "Role Types";
        referenceSheet.Cell(1, 2).Value = "Status";
        referenceSheet.Cell(1, 3).Value = "Available Parent Roles";

        // Style headers
        for (int i = 1; i <= 3; i++)
        {
            referenceSheet.Cell(1, i).Style.Font.Bold = true;
            referenceSheet.Cell(1, i).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Populate reference data
        int row = 2;
        var roleTypeList = new List<string> { "SYSTEM", "BUSINESS", "CUSTOM" };
        var statusList = new List<string> { "Active", "Inactive" };
        var parentRoleList = parentRoles.OrderBy(r => r.Name).Select(r => r.Name).ToList();

        int maxRows = new[] { roleTypeList.Count, statusList.Count, parentRoleList.Count }.Max();

        for (int i = 0; i < maxRows; i++)
        {
            if (i < roleTypeList.Count) referenceSheet.Cell(row + i, 1).Value = roleTypeList[i];
            if (i < statusList.Count) referenceSheet.Cell(row + i, 2).Value = statusList[i];
            if (i < parentRoleList.Count) referenceSheet.Cell(row + i, 3).Value = parentRoleList[i];
        }

        // Create named ranges for dropdowns
        var roleTypeRange = referenceSheet.Range(2, 1, row + roleTypeList.Count - 1, 1);
        var statusRange = referenceSheet.Range(2, 2, row + statusList.Count - 1, 2);
        var parentRoleRange = referenceSheet.Range(2, 3, row + parentRoleList.Count - 1, 3);

        workbook.NamedRanges.Add("RoleTypes", roleTypeRange);
        workbook.NamedRanges.Add("StatusValues", statusRange);
        if (parentRoleList.Any())
            workbook.NamedRanges.Add("ParentRoles", parentRoleRange);

        // Add data validation (dropdowns) to Import Data sheet (rows 2-1000)
        // Role Type column (C)
        var roleTypeValidation = importSheet.Range("C2:C1000").SetDataValidation();
        roleTypeValidation.List("=RoleTypes", true);
        roleTypeValidation.IgnoreBlanks = true;
        roleTypeValidation.InCellDropdown = true;

        // Parent Role column (D) - optional
        if (parentRoleList.Any())
        {
            var parentRoleValidation = importSheet.Range("D2:D1000").SetDataValidation();
            parentRoleValidation.List("=ParentRoles", true);
            parentRoleValidation.IgnoreBlanks = true;
            parentRoleValidation.InCellDropdown = true;
        }

        // Status column (H - moved from G because we added Parent Role column)
        var statusValidation = importSheet.Range("H2:H1000").SetDataValidation();
        statusValidation.List("=StatusValues", true);
        statusValidation.IgnoreBlanks = true;
        statusValidation.InCellDropdown = true;

        // Auto-fit columns
        importSheet.Columns().AdjustToContents();
        referenceSheet.Columns().AdjustToContents();

        // Freeze header row
        importSheet.SheetView.FreezeRows(1);

        // Save to byte array
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]?> GetImportErrorReportAsync(string errorReportId)
    {
        return await _importExportService.GetImportErrorReportAsync(errorReportId);
    }

    public async Task<RoleStatisticsDto> GetStatisticsAsync()
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var cacheKey = _cacheService.GenerateStatsCacheKey("roles", organizationId);

        var cachedResult = await _cacheService.GetCachedAsync<RoleStatisticsDto>(cacheKey);
        if (cachedResult != null) return cachedResult;

        var totalRoles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => r.OrganizationId == organizationId && !r.IsDeleted)
            .CountAsync();

        var activeRoles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => r.OrganizationId == organizationId && !r.IsDeleted && r.IsActive)
            .CountAsync();

        var systemRoles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => r.OrganizationId == organizationId && !r.IsDeleted && r.RoleType == "SYSTEM")
            .CountAsync();

        var businessRoles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => r.OrganizationId == organizationId && !r.IsDeleted && r.RoleType == "BUSINESS")
            .CountAsync();

        var result = new RoleStatisticsDto
        {
            TotalRoles = totalRoles,
            ActiveRoles = activeRoles,
            InactiveRoles = totalRoles - activeRoles,
            SystemRoles = systemRoles,
            BusinessRoles = businessRoles
        };

        await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
        return result;
    }

    public async Task<RoleDropdownOptionsDto> GetDropdownOptionsAsync()
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var cacheKey = _cacheService.GenerateDropdownCacheKey("roles", organizationId);

        var cachedResult = await _cacheService.GetCachedAsync<RoleDropdownOptionsDto>(cacheKey);
        if (cachedResult != null) return cachedResult;

        var roles = await _unitOfWork.Repository<Role>().GetQueryable()
            .Where(r => r.OrganizationId == organizationId && !r.IsDeleted && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name)
            .Select(r => new RoleDropdownDto
            {
                Id = r.Id,
                Name = r.Name,
                RoleType = r.RoleType
            })
            .ToListAsync();

        var result = new RoleDropdownOptionsDto
        {
            Roles = roles
        };

        await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));
        return result;
    }

    public async Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();

        var query = _unitOfWork.Repository<ImportExportHistory>().GetQueryable()
            .Where(h => h.OrganizationId == organizationId && h.EntityType == "Role");

        if (type.HasValue)
            query = query.Where(h => h.OperationType == (ImportExportOperationType)type.Value);

        var totalCount = await query.CountAsync();

        var history = await query
            .OrderByDescending(h => h.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new ImportExportHistoryDto
            {
                Id = h.Id,
                JobId = h.JobId,
                EntityType = h.EntityType,
                OperationType = h.OperationType.ToString(),
                FileName = h.FileName,
                Format = h.Format,
                FileSizeBytes = h.FileSizeBytes,
                Status = h.Status.ToString(),
                Progress = h.Progress,
                ErrorMessage = h.ErrorMessage,
                TotalRows = h.TotalRows,
                SuccessCount = h.SuccessCount,
                UpdatedCount = h.UpdatedCount,
                SkippedCount = h.SkippedCount,
                ErrorCount = h.ErrorCount,
                DuplicateHandlingStrategy = h.DuplicateHandlingStrategy,
                ErrorReportId = h.ErrorReportId,
                DownloadUrl = h.DownloadUrl,
                AppliedFilters = h.AppliedFilters,
                CreatedAtUtc = h.CreatedAtUtc,
                CompletedAtUtc = h.CompletedAt,
                ImportedBy = h.ImportedBy
            })
            .ToListAsync();

        return new PagedResultDto<ImportExportHistoryDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = history
        };
    }

    // ========================================
    // Helper Methods
    // ========================================

    private IQueryable<Role> ApplySortingAtDatabase(IQueryable<Role> query, string? sortField, string? sortDirection)
    {
        if (string.IsNullOrEmpty(sortField))
        {
            return query.OrderBy(r => r.SortOrder).ThenBy(r => r.Name);
        }

        return sortField.ToLower() switch
        {
            "name" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            "description" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(r => r.Description) : query.OrderBy(r => r.Description),
            "roletype" or "type" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(r => r.RoleType) : query.OrderBy(r => r.RoleType),
            "isactive" or "status" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(r => r.IsActive) : query.OrderBy(r => r.IsActive),
            "createdatutc" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(r => r.CreatedAtUtc) : query.OrderBy(r => r.CreatedAtUtc),
            "updatedatutc" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(r => r.ModifiedAtUtc) : query.OrderBy(r => r.ModifiedAtUtc),
            "sortorder" => sortDirection?.ToLower() == "desc" ? query.OrderByDescending(r => r.SortOrder) : query.OrderBy(r => r.SortOrder),
            _ => query.OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
        };
    }
}
