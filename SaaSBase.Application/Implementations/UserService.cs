using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Domain;
using AutoMapper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SaaSBase.Application.Implementations;

public class UserService : BaseCrudService<User, UserDetailsDto, CreateUserDto, UpdateUserDto, UserStatisticsDto, DropdownOptionsDto>, IUserService
{
    private readonly IEmailService _emailService;
    private readonly IUserContextService _userContextService;
    private readonly IOrganizationService _organizationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IImportExportService _importExportService;
    private readonly IBackgroundOperationService _backgroundOperationService;
    private readonly ICacheService _cacheService;
    private readonly IPerformanceService _performanceService;

    public UserService(
        IUnitOfWork unitOfWork,
        ICurrentTenantService tenantService,
        IMapper mapper,
        IEmailService emailService,
        IUserContextService userContextService,
        IOrganizationService organizationService,
        IServiceScopeFactory serviceScopeFactory,
        IImportExportService importExportService,
        IBackgroundOperationService backgroundOperationService,
        ICacheService cacheService,
        IPerformanceService performanceService)
        : base(unitOfWork, tenantService, mapper)
    {
        _emailService = emailService;
        _userContextService = userContextService;
        _organizationService = organizationService;
        _serviceScopeFactory = serviceScopeFactory;
        _importExportService = importExportService;
        _backgroundOperationService = backgroundOperationService;
        _cacheService = cacheService;
        _performanceService = performanceService;
    }

    public async Task<ImportJobStatusDto?> GetImportJobStatusAsync(string jobId)
    {
        return await _importExportService.GetImportJobStatusAsync(jobId);
    }

    public async Task<string> StartImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var userName = _tenantService.GetCurrentUserName();

        return await _importExportService.StartImportJobAsync<CreateUserDto>(
            entityType: "User",
            fileStream: fileStream,
            fileName: fileName,
            rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
            {
                // Validate required fields
                if (!rowData.TryGetValue("Email", out var email) || string.IsNullOrWhiteSpace(email))
                    return (false, "Email is required", false, false);

                if (!rowData.TryGetValue("First Name", out var firstName) || string.IsNullOrWhiteSpace(firstName))
                    return (false, "First Name is required", false, false);

                // Validate email format
                if (!IsValidEmail(email))
                    return (false, $"Invalid email format: {email}", false, false);

                // Use scoped repositories from the passed UnitOfWork
                var userRepo = scopedUnitOfWork.Repository<User>();
                var roleRepo = scopedUnitOfWork.Repository<Role>();
                var userRoleRepo = scopedUnitOfWork.Repository<UserRole>();

                // Check if user already exists
                var existingUser = await userRepo.FindAsync(u =>
                    u.OrganizationId == organizationId && u.Email.ToLower() == email.ToLower() && !u.IsDeleted);

                if (existingUser != null)
                {
                    if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
                        return (true, null, false, true); // Skip

                    if (duplicateStrategy == DuplicateHandlingStrategy.Update)
                    {
                        // Update existing user
                        existingUser.FirstName = firstName;
                        existingUser.LastName = rowData.GetValueOrDefault("Last Name");
                        existingUser.FullName = $"{firstName} {rowData.GetValueOrDefault("Last Name")}".Trim();
                        existingUser.Department = rowData.GetValueOrDefault("Department");
                        existingUser.JobTitle = rowData.GetValueOrDefault("Job Title");
                        existingUser.Location = rowData.GetValueOrDefault("Location");
                        existingUser.PhoneNumber = rowData.GetValueOrDefault("Phone Number");
                        existingUser.EmployeeId = rowData.GetValueOrDefault("Employee ID");

                        if (rowData.TryGetValue("Status", out var status))
                            existingUser.IsActive = status?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;

                        existingUser.ModifiedAtUtc = DateTimeOffset.UtcNow;
                        existingUser.ModifiedBy = userId.ToString();

                        userRepo.Update(existingUser);

                        // Update role if provided
                        if (rowData.TryGetValue("Role", out var roleName) && !string.IsNullOrWhiteSpace(roleName))
                        {
                            var role = await roleRepo.FindAsync(r =>
                                r.OrganizationId == organizationId && r.Name.ToLower() == roleName.ToLower() && r.IsActive);

                            if (role != null)
                            {
                                // Remove existing roles
                                var existingRoles = await userRoleRepo.FindManyAsync(ur =>
                                    ur.UserId == existingUser.Id && ur.OrganizationId == organizationId);
                                foreach (var existingRole in existingRoles)
                                    userRoleRepo.Remove(existingRole);

                                // Add new role
                                await userRoleRepo.AddAsync(new UserRole
                                {
                                    Id = Guid.NewGuid(),
                                    OrganizationId = organizationId,
                                    UserId = existingUser.Id,
                                    RoleId = role.Id
                                });
                            }
                        }

                        await scopedUnitOfWork.SaveChangesAsync();
                        return (true, null, true, false); // Updated
                    }
                }

                // Create new user
                var fullName = $"{firstName} {rowData.GetValueOrDefault("Last Name")}".Trim();
                var newUser = new User
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(GenerateRandomPassword(8)),
                    FullName = fullName,
                    FirstName = firstName,
                    LastName = rowData.GetValueOrDefault("Last Name") ?? string.Empty,
                    Department = rowData.GetValueOrDefault("Department"),
                    JobTitle = rowData.GetValueOrDefault("Job Title"),
                    Location = rowData.GetValueOrDefault("Location"),
                    PhoneNumber = rowData.GetValueOrDefault("Phone Number"),
                    EmployeeId = rowData.GetValueOrDefault("Employee ID"),
                    IsActive = rowData.TryGetValue("Status", out var newStatus) &&
                               newStatus?.Equals("Active", StringComparison.OrdinalIgnoreCase) == true ||
                               newStatus == null,
                    IsEmailVerified = false,
                    IsPhoneVerified = false,
                    TimeZone = "UTC",
                    Language = "en",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = userId.ToString(),
                    ModifiedAtUtc = DateTimeOffset.UtcNow,
                    ModifiedBy = userId.ToString()
                };

                await userRepo.AddAsync(newUser);
                await scopedUnitOfWork.SaveChangesAsync();

                // Assign role if provided
                if (rowData.TryGetValue("Role", out var newRoleName) && !string.IsNullOrWhiteSpace(newRoleName))
                {
                    var role = await roleRepo.FindAsync(r =>
                        r.OrganizationId == organizationId && r.Name.ToLower() == newRoleName.ToLower() && r.IsActive);

                    if (role != null)
                    {
                        await userRoleRepo.AddAsync(new UserRole
                        {
                            Id = Guid.NewGuid(),
                            OrganizationId = organizationId,
                            UserId = newUser.Id,
                            RoleId = role.Id
                        });
                        await scopedUnitOfWork.SaveChangesAsync();
                    }
                }

                // Send welcome email (fire and forget)
                // Capture necessary data before the scope ends
                var userIdForEmail = newUser.Id;
                var userEmail = newUser.Email;
                var userFullName = newUser.FullName;

                // Use background operation service to maintain context
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _backgroundOperationService.ExecuteWithContextAsync<IEmailService>(
                            organizationId, userId, userName,
                            async (emailService) =>
                            {
                                await emailService.SendWelcomeEmailAsync(userEmail, userFullName);
                            });

                        await _backgroundOperationService.ExecuteWithContextAsync<IUserService>(
                            organizationId, userId, userName,
                            async (userService) =>
                            {
                                await userService.SendEmailVerificationAsync(userIdForEmail);
                            });
                    }
                    catch { /* Ignore email errors */ }
                });

                return (true, null, false, false); // Success
            },
            duplicateStrategy: duplicateStrategy
        );
    }

    public async Task<PagedResultDto<UserListItemDto>> GetUsersAsync(string? search, string? department, string? jobTitle, string? location, bool? isActive, bool? isEmailVerified, Guid? roleId, Guid? organizationId, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc")
    {
        return await _performanceService.MonitorAsync("GetUsersAsync", async () =>
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
            
            // Generate cache key using generic method
            var cacheKey = _cacheService.GenerateListCacheKey("users", filterOrganizationId, page, pageSize, 
                search, sortField, sortDirection, 
                department, jobTitle, location, isActive, isEmailVerified, roleId, organizationId, createdFrom, createdTo);

            // Build optimized query with proper database-level filtering
            IQueryable<User> query;
            if (isSystemAdmin && !organizationId.HasValue)
            {
                // System Admin without filter - show all organizations
                query = _unitOfWork.Repository<User>().GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(u => !u.IsDeleted);
            }
            else
            {
                // Filter by specific organization
                query = _unitOfWork.Repository<User>().GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(u => u.OrganizationId == filterOrganizationId && !u.IsDeleted);
            }

            // Apply search filter with case-insensitive search
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(u => 
                    u.Email.ToLower().Contains(searchLower) ||
                    u.FullName.ToLower().Contains(searchLower) ||
                    (u.EmployeeId != null && u.EmployeeId.ToLower().Contains(searchLower)));
            }

            // Apply filters at database level for better performance
            if (!string.IsNullOrEmpty(department) && department != "all")
                query = query.Where(u => u.Department == department);

            if (!string.IsNullOrEmpty(jobTitle) && jobTitle != "all")
                query = query.Where(u => u.JobTitle == jobTitle);

            if (!string.IsNullOrEmpty(location) && location != "all")
                query = query.Where(u => u.Location == location);

            if (isActive.HasValue)
                query = query.Where(u => u.IsActive == isActive.Value);

            if (isEmailVerified.HasValue)
                query = query.Where(u => u.IsEmailVerified == isEmailVerified.Value);

            if (createdFrom.HasValue)
                query = query.Where(u => u.CreatedAtUtc >= createdFrom.Value);

            if (createdTo.HasValue)
                query = query.Where(u => u.CreatedAtUtc <= createdTo.Value);

            // Handle role filtering with efficient JOIN
            if (roleId.HasValue)
            {
                query = query.Join(_unitOfWork.Repository<UserRole>().GetQueryable(),
                    u => u.Id,
                    ur => ur.UserId,
                    (u, ur) => new { User = u, UserRole = ur })
                    .Where(x => x.UserRole.RoleId == roleId.Value && x.UserRole.OrganizationId == filterOrganizationId)
                    .Select(x => x.User);
            }

            // Get total count efficiently at database level
            var totalCount = await query.CountAsync();

            // Apply sorting at database level for better performance
            query = ApplySortingAtDatabase(query, sortField, sortDirection);

            // Apply pagination
            var users = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
            .Select(u => new UserListItemDto
            {
                Id = u.Id,
                Email = u.Email,
                FullName = u.FullName,
                FirstName = u.FirstName,
                LastName = u.LastName,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                IsEmailVerified = u.IsEmailVerified,
                AvatarUrl = u.AvatarUrl,
                Department = u.Department,
                JobTitle = u.JobTitle,
                Location = u.Location,
                EmployeeId = u.EmployeeId,
                LastLoginAt = u.LastLoginAt,
                CreatedAtUtc = u.CreatedAtUtc,
                CreatedBy = u.CreatedBy, // Will resolve to name below
                ModifiedAtUtc = u.ModifiedAtUtc,
                ModifiedBy = u.ModifiedBy, // Will resolve to name below
                LockedUntil = u.LockedUntil,
                OrganizationId = u.OrganizationId,
                OrganizationName = null // Will be populated below if needed
                })
                .ToListAsync();

            // Resolve CreatedBy and ModifiedBy user names and IDs
            if (users.Any())
            {
                var userIdStrings = users
                    .SelectMany(u => new[] { u.CreatedBy, u.ModifiedBy })
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
                            .Select(u => new { Id = u.Id, IdString = u.Id.ToString(), FullName = u.FullName })
                            .ToDictionaryAsync(u => u.IdString, u => new { u.Id, u.FullName });

                        foreach (var user in users)
                        {
                            // Parse and set CreatedBy ID and Name
                            if (!string.IsNullOrEmpty(user.CreatedBy) && Guid.TryParse(user.CreatedBy, out var createdId))
                            {
                                user.CreatedById = createdId;
                                if (userNames.TryGetValue(user.CreatedBy, out var createdByInfo))
                                {
                                    user.CreatedByName = createdByInfo.FullName;
                                    user.CreatedBy = createdByInfo.FullName; // Keep for backward compatibility
                                }
                            }

                            // Parse and set ModifiedBy ID and Name
                            if (!string.IsNullOrEmpty(user.ModifiedBy) && Guid.TryParse(user.ModifiedBy, out var modifiedId))
                            {
                                user.ModifiedById = modifiedId;
                                if (userNames.TryGetValue(user.ModifiedBy, out var modifiedByInfo))
                                {
                                    user.ModifiedByName = modifiedByInfo.FullName;
                                    user.ModifiedBy = modifiedByInfo.FullName; // Keep for backward compatibility
                                }
                            }
                        }
                    }
                }
            }

            // Load roles efficiently with single optimized query - ALL roles for each user
            if (users.Any())
            {
                var userIds = users.Select(u => u.Id).ToList();
                
                // Query UserRole table with proper filters
                var userRolesQuery = _unitOfWork.Repository<UserRole>().GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(ur => userIds.Contains(ur.UserId) && !ur.IsDeleted);
                
                // Join with Role table and filter active, non-deleted roles
                var rolesQuery = _unitOfWork.Repository<Role>().GetQueryable()
                    .Where(r => !r.IsDeleted && r.IsActive);
                
                var userRolesWithRoles = await userRolesQuery
                    .Join(rolesQuery,
                        ur => ur.RoleId,
                        r => r.Id,
                        (ur, r) => new { UserId = ur.UserId, RoleId = r.Id, RoleName = r.Name })
                    .ToListAsync();

                // Group roles by UserId
                var rolesByUserId = userRolesWithRoles
                    .GroupBy(ur => ur.UserId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var user in users)
                {
                    if (rolesByUserId.TryGetValue(user.Id, out var userRoles))
                    {
                        // Set all roles
                        user.RoleIds = userRoles.Select(ur => ur.RoleId).ToList();
                        user.RoleNames = userRoles.Select(ur => ur.RoleName).ToList();
                        
                        // Keep backward compatibility - set first role
                        if (userRoles.Any())
                        {
                            user.RoleId = userRoles.First().RoleId;
                            user.RoleName = userRoles.First().RoleName;
                        }
                    }
                    else
                    {
                        // Initialize empty lists if no roles
                        user.RoleIds = new List<Guid>();
                        user.RoleNames = new List<string>();
                    }
                }
            }

            // Populate organization names for all users
            if (users.Any())
            {
                var orgIds = users.Select(u => u.OrganizationId).Distinct().ToList();
                var orgRepo = _unitOfWork.Repository<Organization>();
                var organizations = await orgRepo.GetQueryable()
                    .IgnoreQueryFilters()
                    .Where(o => orgIds.Contains(o.Id) && !o.IsDeleted)
                    .Select(o => new { o.Id, o.Name })
                    .ToDictionaryAsync(o => o.Id, o => o.Name);

                foreach (var user in users)
                {
                    if (organizations.TryGetValue(user.OrganizationId, out var orgName))
                    {
                        user.OrganizationName = orgName;
                    }
                }
            }

            var result = new PagedResultDto<UserListItemDto>
        {
            Page = page,
            PageSize = pageSize,
                TotalCount = totalCount,
                Items = users
            };

            // Cache the result
            await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));

            return result;
        });
    }

    /// <summary>
    /// Apply sorting at database level for optimal performance
    /// </summary>
    private IQueryable<User> ApplySortingAtDatabase(IQueryable<User> query, string? sortField, string? sortDirection)
    {
        return sortField?.ToLower() switch
        {
            "email" => sortDirection == "asc" ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
            "fullname" => sortDirection == "asc" ? query.OrderBy(u => u.FullName) : query.OrderByDescending(u => u.FullName),
            "department" => sortDirection == "asc" ? query.OrderBy(u => u.Department) : query.OrderByDescending(u => u.Department),
            "jobtitle" => sortDirection == "asc" ? query.OrderBy(u => u.JobTitle) : query.OrderByDescending(u => u.JobTitle),
            "location" => sortDirection == "asc" ? query.OrderBy(u => u.Location) : query.OrderByDescending(u => u.Location),
            "isactive" or "status" => sortDirection == "asc" ? query.OrderBy(u => u.IsActive) : query.OrderByDescending(u => u.IsActive),
            "isemailverified" => sortDirection == "asc" ? query.OrderBy(u => u.IsEmailVerified) : query.OrderByDescending(u => u.IsEmailVerified),
            "lastloginat" => sortDirection == "asc" ? query.OrderBy(u => u.LastLoginAt) : query.OrderByDescending(u => u.LastLoginAt),
            "createdatutc" => sortDirection == "asc" ? query.OrderBy(u => u.CreatedAtUtc) : query.OrderByDescending(u => u.CreatedAtUtc),
            _ => sortDirection == "asc" ? query.OrderBy(u => u.CreatedAtUtc) : query.OrderByDescending(u => u.CreatedAtUtc)
        };
    }

    public async Task<UserDetailsDto?> GetUserByIdAsync(Guid id)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var repo = _unitOfWork.Repository<User>();

        // Get user with metadata fields
        User? user;
        if (isSystemAdmin)
        {
            // System Admin can view users from any organization
            user = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }
        else
        {
            // Regular users can only view users from their organization
            user = await repo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId);
        }
        
        if (user == null) return null;

        // Resolve CreatedBy and ModifiedBy user names
        Guid? createdById = null;
        Guid? modifiedById = null;
        string? createdByName = null;
        string? modifiedByName = null;

        if (!string.IsNullOrEmpty(user.CreatedBy) && Guid.TryParse(user.CreatedBy, out var createdId))
        {
            createdById = createdId;
            var createdByUser = await repo.FindAsync(u => u.Id == createdId && u.OrganizationId == OrganizationId);
            createdByName = createdByUser?.FullName;
        }

        if (!string.IsNullOrEmpty(user.ModifiedBy) && Guid.TryParse(user.ModifiedBy, out var modifiedId))
        {
            modifiedById = modifiedId;
            var modifiedByUser = await repo.FindAsync(u => u.Id == modifiedId && u.OrganizationId == OrganizationId);
            modifiedByName = modifiedByUser?.FullName;
        }

        // Map to DTO using MapToDetails helper
        var dto = MapToDetails(user);
        dto.CreatedBy = createdByName ?? user.CreatedBy; // Keep for backward compatibility
        dto.ModifiedBy = modifiedByName ?? user.ModifiedBy; // Keep for backward compatibility
        dto.CreatedById = createdById;
        dto.CreatedByName = createdByName;
        dto.ModifiedById = modifiedById;
        dto.ModifiedByName = modifiedByName;
        
        // Populate organization name
        if (user.OrganizationId != Guid.Empty)
        {
            var orgRepo = _unitOfWork.Repository<Organization>();
            var org = await orgRepo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == user.OrganizationId && !o.IsDeleted);
            dto.OrganizationName = org?.Name;
        }

        // Load user roles
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var roleRepo = _unitOfWork.Repository<Role>();
        
        IQueryable<UserRole> userRoleQuery;
        if (isSystemAdmin)
        {
            // System Admin can see roles from any organization
            userRoleQuery = userRoleRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(ur => ur.UserId == id && !ur.IsDeleted);
        }
        else
        {
            // Regular users see roles from their organization only
            userRoleQuery = userRoleRepo.GetQueryable()
                .Where(ur => ur.UserId == id && ur.OrganizationId == OrganizationId && !ur.IsDeleted);
        }
        
        var userRoles = await userRoleQuery
            .Join(roleRepo.GetQueryable().IgnoreQueryFilters().Where(r => !r.IsDeleted && r.IsActive),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { RoleId = r.Id, RoleName = r.Name })
            .ToListAsync();

        // Populate RoleIds and RoleNames
        dto.RoleIds = userRoles.Select(ur => ur.RoleId).ToList();
        dto.RoleNames = userRoles.Select(ur => ur.RoleName).ToList();
        
        // Set RoleId and RoleName for backward compatibility (use first role if any)
        if (userRoles.Any())
        {
            dto.RoleId = userRoles.First().RoleId;
            dto.RoleName = userRoles.First().RoleName;
        }

        return dto;
    }

    public async Task<UserDetailsDto> CreateUserAsync(CreateUserDto dto)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var currentUserId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<User>();
        // Optimized: Check if user exists using existence check
        var userExists = await repo.ExistsAsync(x => x.OrganizationId == OrganizationId && x.Email == dto.Email);
        if (userExists)
        {
            throw new ArgumentException("User with this email already exists");
        }

        var fullName = !string.IsNullOrWhiteSpace(dto.FullName)
            ? dto.FullName.Trim()
            : ($"{dto.FirstName ?? string.Empty} {dto.LastName ?? string.Empty}".Trim());

        // Generate secure random 8-character password (letters+digits)
        var plainPassword = GenerateRandomPassword(8);

        var user = new User
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword),
            FullName = fullName,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber,
            Department = dto.Department,
            JobTitle = dto.JobTitle,
            Location = dto.Location,
            EmployeeId = dto.EmployeeId,
            IsActive = dto.IsActive,
            IsEmailVerified = false,
            IsPhoneVerified = false,
            TimeZone = "UTC",
            Language = "en",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = currentUserId.ToString(),
            ModifiedAtUtc = DateTimeOffset.UtcNow,
            ModifiedBy = currentUserId.ToString()
        };

        await repo.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // Assign role (now required)
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            UserId = user.Id,
            RoleId = dto.RoleId
        };
        await userRoleRepo.AddAsync(userRole);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate user permission caches since new user with role was created
        await _cacheService.RemoveCacheAsync($"user_permissions_{user.Id}_{OrganizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_{user.Id}_{OrganizationId}");

        // Log user creation
        await LogUserActivityAsync(user.Id, "USER_CREATED", "User account created", "User", user.Id);

        // Invalidate ALL user caches for this organization
        var orgId = _tenantService.GetCurrentOrganizationId();
        await InvalidateAllUserCachesAsync(orgId);

        // Send welcome + verification email (password sent after verification)
        await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);
        await SendEmailVerificationAsync(user.Id);
        // Note: After verification, send password email

        return MapToDetails(user);
    }

    private static string GenerateRandomPassword(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789"; // exclude similar chars
        var rng = new Random();
        return new string(Enumerable.Range(0, Math.Max(8, length)).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }

    public async Task<UserDetailsDto?> UpdateUserAsync(Guid id, UpdateUserDto dto)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var currentUserId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<User>();
        
        User? u;
        if (isSystemAdmin)
        {
            // System Admin can view users from any organization, but can only edit their own organization's users
            u = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (u != null && u.OrganizationId != OrganizationId)
            {
                // System Admin cannot edit users from other organizations
                throw new UnauthorizedAccessException("You can only edit users from your own organization");
            }
        }
        else
        {
            // Regular users can only edit users from their organization
            u = await repo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId);
        }
        
        if (u == null) return null;

        var oldActive = u.IsActive;
        // Update names and compute FullName if needed
        u.FirstName = dto.FirstName;
        u.LastName = dto.LastName;
        if (!string.IsNullOrWhiteSpace(dto.FullName))
        {
            u.FullName = dto.FullName!.Trim();
        }
        else if (dto.FirstName != null || dto.LastName != null)
        {
            var first = dto.FirstName ?? u.FirstName ?? string.Empty;
            var last = dto.LastName ?? u.LastName ?? string.Empty;
            u.FullName = ($"{first} {last}").Trim();
        }
        u.PhoneNumber = dto.PhoneNumber;
        u.Department = dto.Department;
        u.JobTitle = dto.JobTitle;
        u.Location = dto.Location;
        u.EmployeeId = dto.EmployeeId;
        if (dto.IsActive.HasValue) u.IsActive = dto.IsActive.Value;
        u.ModifiedAtUtc = DateTimeOffset.UtcNow;
        u.ModifiedBy = currentUserId.ToString();

        repo.Update(u);
        await _unitOfWork.SaveChangesAsync();

        // Update roles - support multi-role
        var userRoleRepo = _unitOfWork.Repository<UserRole>();

        // Remove existing roles
        var existingRoles = await userRoleRepo.FindManyAsync(x => x.UserId == id && x.OrganizationId == OrganizationId && !x.IsDeleted);
        foreach (var existingRole in existingRoles)
        {
            userRoleRepo.Remove(existingRole);
        }

        // Determine which roles to assign - prefer RoleIds over RoleId
        var rolesToAssign = dto.RoleIds != null && dto.RoleIds.Any() 
            ? dto.RoleIds 
            : (dto.RoleId != Guid.Empty ? new List<Guid> { dto.RoleId } : new List<Guid>());

        // Add new roles
        foreach (var roleId in rolesToAssign)
        {
            if (roleId == Guid.Empty) continue; // Skip empty GUIDs
            
            var userRole = new UserRole
            {
                Id = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                UserId = id,
                RoleId = roleId
            };
            await userRoleRepo.AddAsync(userRole);
        }
        await _unitOfWork.SaveChangesAsync();

        // Invalidate user permission caches since role assignment changed
        await _cacheService.RemoveCacheAsync($"user_permissions_{id}_{OrganizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_{id}_{OrganizationId}");

        // Log user update
        await LogUserActivityAsync(u.Id, "USER_UPDATED", "User profile updated", "User", u.Id);

        // Log activation/deactivation
        if (dto.IsActive.HasValue && oldActive != dto.IsActive.Value)
        {
            await LogUserActivityAsync(u.Id, dto.IsActive.Value ? "USER_ACTIVATED" : "USER_DEACTIVATED",
                $"User account {(dto.IsActive.Value ? "activated" : "deactivated")}", "User", u.Id);
        }

        // Invalidate ALL user caches for this organization
        var orgId = _tenantService.GetCurrentOrganizationId();
        await InvalidateAllUserCachesAsync(orgId);

        return MapToDetails(u);
    }

    public override async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        
        User? user;
        if (isSystemAdmin)
        {
            // System Admin can view users from any organization, but can only modify their own organization's users
            user = await Repository.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (user != null && user.OrganizationId != organizationId)
            {
                // System Admin cannot modify users from other organizations
                throw new UnauthorizedAccessException("You can only modify users from your own organization");
            }
        }
        else
        {
            // Regular users can only modify users from their organization
            user = await Repository.FindAsync(x => x.Id == id && x.OrganizationId == organizationId);
        }
        
        if (user == null) return false;

        var oldActive = user.IsActive;
        if (oldActive == isActive) return true; // No change needed

        // Validate status change
        await ValidateStatusChangeAsync(user, isActive);

        user.IsActive = isActive;
        user.ModifiedAtUtc = DateTimeOffset.UtcNow;
        Repository.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Perform post-status-change actions
        await OnAfterStatusChangeAsync(user, isActive);

        // Invalidate ALL user caches for this organization
        await InvalidateAllUserCachesAsync(organizationId);
        // Invalidate user permission caches since active status affects permission access
        await _cacheService.RemoveCacheAsync($"user_permissions_{id}_{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"user_menus_{id}_{organizationId}");

        return true;
    }

    protected override async Task OnAfterStatusChangeAsync(User user, bool newStatus)
    {
        // Log activation/deactivation
        await LogUserActivityAsync(user.Id, newStatus ? "USER_ACTIVATED" : "USER_DEACTIVATED",
            $"User account {(newStatus ? "activated" : "deactivated")}", "User", user.Id);

        // Send notification email using templates
        if (newStatus)
        {
            await _emailService.SendAccountActivationEmailAsync(user.Email, user.FullName);
        }
        else
        {
            await _emailService.SendAccountDeactivationEmailAsync(user.Email, user.FullName);
        }
    }

    /// <summary>
    /// Invalidates ALL user-related caches (list, stats, dropdown) for the organization
    /// </summary>
    private async Task InvalidateAllUserCachesAsync(Guid organizationId)
    {
        // Clear all user cache types for this organization
        await _cacheService.RemoveCacheByPatternAsync($"users:list:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:stats:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"users:dropdown:{organizationId}");
    }

    private async Task LogUserActivityAsync(Guid userId, string action, string description, string? resourceType = null, Guid? resourceId = null)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var logRepo = _unitOfWork.Repository<UserActivityLog>();
        var ipAddress = _userContextService.GetCurrentIpAddress();
        var userAgent = _userContextService.GetCurrentUserAgent();

        await logRepo.AddAsync(new UserActivityLog
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            UserId = userId,
            Action = action,
            Description = description,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Timestamp = DateTimeOffset.UtcNow,
            Severity = "INFO",
            IpAddress = ipAddress ?? "0.0.0.0",
            UserAgent = userAgent ?? "Unknown"
        });
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var repo = _unitOfWork.Repository<User>();
        
        User? user;
        if (isSystemAdmin)
        {
            // System Admin can view users from any organization, but can only delete their own organization's users
            user = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (user != null && user.OrganizationId != organizationId)
            {
                // System Admin cannot delete users from other organizations
                throw new UnauthorizedAccessException("You can only delete users from your own organization");
            }
        }
        else
        {
            // Regular users can only delete users from their organization
            user = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId);
        }
        
        if (user == null) return false;
        
        // Use base class DeleteAsync which handles soft delete
        return await DeleteAsync(id);
    }

    protected override async Task OnAfterDeleteAsync(User user)
    {
        // Deactivate user on deletion
        user.IsActive = false;

        // Log deletion
        await LogUserActivityAsync(user.Id, "USER_DELETED", "User account deleted", "User", user.Id);

        // Invalidate ALL user caches for this organization
        await InvalidateAllUserCachesAsync(user.OrganizationId);
    }

    public new async Task BulkDeleteAsync(List<Guid> ids)
    {
        // Use base class BulkDeleteAsync which handles soft delete
        await base.BulkDeleteAsync(ids);
    }

    public async Task<List<UserDetailsDto>> BulkCloneAsync(List<Guid> ids)
    {
        if (ids == null || !ids.Any())
            return new List<UserDetailsDto>();

		var OrganizationId = _tenantService.GetCurrentOrganizationId();
		var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<User>();
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        
        // Get original users
        var originalUsers = await repo.GetQueryable()
            .Where(u => ids.Contains(u.Id) && u.OrganizationId == OrganizationId && !u.IsDeleted)
            .ToListAsync();

        if (!originalUsers.Any())
            return new List<UserDetailsDto>();

        var clonedUsers = new List<UserDetailsDto>();
        var generatedEmails = new HashSet<string>(); // Track emails in current batch
        var clonedUserEntities = new List<User>(); // Store user entities before saving
        var userRolesToAdd = new List<UserRole>(); // Store user roles to add

        // First, get all existing emails from database to avoid conflicts
        var existingEmails = await repo.GetQueryable()
            .Where(u => u.OrganizationId == OrganizationId && !u.IsDeleted)
            .Select(u => u.Email)
            .ToListAsync();
        
        generatedEmails.UnionWith(existingEmails);

        foreach (var originalUser in originalUsers)
        {
            // Generate unique email
            var baseEmail = originalUser.Email.Split('@')[0];
            var domain = originalUser.Email.Contains('@') ? originalUser.Email.Split('@')[1] : "example.com";
            var newEmail = $"{baseEmail}.clone.{Guid.NewGuid().ToString("N")[..8]}@{domain}";
            
            // Check if email already exists in current batch (includes database emails)
            while (generatedEmails.Contains(newEmail))
            {
                newEmail = $"{baseEmail}.clone.{Guid.NewGuid().ToString("N")[..8]}@{domain}";
            }
            
            generatedEmails.Add(newEmail); // Track this email for current batch

            // Generate new password
            var plainPassword = GenerateRandomPassword(8);

            // Create cloned user
            var clonedUser = new User
            {
                Id = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                Email = newEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword),
                FullName = $"{originalUser.FullName} (Copy)",
                FirstName = originalUser.FirstName,
                LastName = originalUser.LastName,
                PhoneNumber = originalUser.PhoneNumber,
                Department = originalUser.Department,
                JobTitle = originalUser.JobTitle,
                Location = originalUser.Location,
                EmployeeId = originalUser.EmployeeId,
                IsActive = false, // Cloned users start as inactive
                IsEmailVerified = false,
                IsPhoneVerified = false,
                TimeZone = originalUser.TimeZone ?? "UTC",
				Language = originalUser.Language ?? "en",
				CreatedBy = userId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow
            };

            clonedUserEntities.Add(clonedUser);

            // Get original user roles for later assignment
            var originalRoles = await userRoleRepo.GetQueryable()
                .Where(ur => ur.UserId == originalUser.Id && ur.OrganizationId == OrganizationId && !ur.IsDeleted)
                .ToListAsync();

            foreach (var originalRole in originalRoles)
            {
                userRolesToAdd.Add(new UserRole
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = OrganizationId,
                    UserId = clonedUser.Id,
					RoleId = originalRole.RoleId,
					CreatedBy = userId.ToString(),
					CreatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        // Add all user entities at once
        foreach (var clonedUser in clonedUserEntities)
        {
            await repo.AddAsync(clonedUser);
        }

        // Save all cloned users in a single transaction
        await _unitOfWork.SaveChangesAsync();

        // Add all user roles at once
        foreach (var userRole in userRolesToAdd)
        {
            await userRoleRepo.AddAsync(userRole);
        }

        // Save all user roles in a single transaction
        await _unitOfWork.SaveChangesAsync();

        // Build DTOs and log activities after saving
        for (int i = 0; i < clonedUserEntities.Count; i++)
        {
            var clonedUser = clonedUserEntities[i];
            var originalUser = originalUsers[i];
            
            // Log cloning
            await LogUserActivityAsync(clonedUser.Id, "USER_CLONED", $"User cloned from {originalUser.Email}", "User", clonedUser.Id);
            
            clonedUsers.Add(MapToDetails(clonedUser));
        }

        // Invalidate caches - comprehensive like create/update (once for all)
        foreach (var clonedUser in clonedUserEntities)
        {
            await _cacheService.RemoveCacheAsync($"user_permissions_{clonedUser.Id}_{OrganizationId}");
            await _cacheService.RemoveCacheByPatternAsync($"user_menus_{clonedUser.Id}_{OrganizationId}");
        }
        await InvalidateAllUserCachesAsync(OrganizationId);
        // Invalidate statistics cache since new users were created
        await _cacheService.RemoveCacheAsync($"user:statistics:{OrganizationId}");

        return clonedUsers;
    }

    protected override async Task OnAfterBulkDeleteAsync(List<User> users)
    {
        // Deactivate all users and log each deletion
        foreach (var user in users)
        {
            user.IsActive = false;
            await LogUserActivityAsync(user.Id, "USER_DELETED", "User account deleted (bulk)", "User", user.Id);
        }

        // Invalidate ALL user caches for this organization (use first user's org ID)
        if (users.Any())
        {
            await InvalidateAllUserCachesAsync(users.First().OrganizationId);
        }
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

        // Thread-safe dictionary to store user roles (shared between dataFetcher and columnMapper)
        var userRolesMap = new System.Collections.Concurrent.ConcurrentDictionary<Guid, string>();

        return await _importExportService.StartExportJobAsync<User>(
            entityType: "User",
            format: format,
            dataFetcher: async (f) =>
            {
                // Create new scope to get fresh DbContext using IServiceScopeFactory
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
                
                // CRITICAL: Set background context to ensure DbContext global query filter works correctly
                // The organizationId is captured from outer scope and passed via filters
                var orgId = organizationId; // Use captured value
                scopedTenantService.SetBackgroundContext(orgId, userId, userName);
                
                var repo = scopedUnitOfWork.Repository<User>();

                // Fetch users based on filters
                var search = f.GetValueOrDefault("search")?.ToString();
                var department = f.GetValueOrDefault("department")?.ToString();
                var jobTitle = f.GetValueOrDefault("jobTitle")?.ToString();
                var location = f.GetValueOrDefault("location")?.ToString();
                var isActive = f.GetValueOrDefault("isActive") as bool?;
                var isEmailVerified = f.GetValueOrDefault("isEmailVerified") as bool?;
                
                // Parse roleId - can be Guid or string
                Guid? roleId = null;
                var roleIdValue = f.GetValueOrDefault("roleId");
                if (roleIdValue != null)
                {
                    if (roleIdValue is Guid guidValue)
                        roleId = guidValue;
                    else if (roleIdValue is string stringValue && Guid.TryParse(stringValue, out var parsedGuid))
                        roleId = parsedGuid;
                }
                
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
                
                var selectedIds = f.GetValueOrDefault("selectedIds");

                var searchLower = search?.ToLower();
                List<User> usersToExport;

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
                        var userIds = idsList; // Make a copy for lambda
                        var selectedUsers = await repo.FindManyAsync(u => u.OrganizationId == orgId && !u.IsDeleted && userIds.Contains(u.Id));
                        usersToExport = selectedUsers.ToList();
                    }
                    else
                    {
                        usersToExport = new List<User>();
                    }
                }
                else
                {
                    // Build query with all filters
                    var query = repo.GetQueryable()
                        .Where(u => u.OrganizationId == orgId && !u.IsDeleted);

                    // Apply search filter
                    if (!string.IsNullOrEmpty(search))
                    {
                        searchLower = search.ToLower();
                        query = query.Where(u => 
                            u.Email.ToLower().Contains(searchLower) || 
                            u.FullName.ToLower().Contains(searchLower) || 
                            (u.EmployeeId != null && u.EmployeeId.ToLower().Contains(searchLower)));
                    }

                    // Apply other filters
                    if (!string.IsNullOrEmpty(department) && department != "all")
                        query = query.Where(u => u.Department == department);

                    if (!string.IsNullOrEmpty(jobTitle) && jobTitle != "all")
                        query = query.Where(u => u.JobTitle == jobTitle);

                    if (!string.IsNullOrEmpty(location) && location != "all")
                        query = query.Where(u => u.Location == location);

                    if (isActive.HasValue)
                        query = query.Where(u => u.IsActive == isActive.Value);

                    if (isEmailVerified.HasValue)
                        query = query.Where(u => u.IsEmailVerified == isEmailVerified.Value);

                    // Apply date range filters
                    if (createdFrom.HasValue)
                        query = query.Where(u => u.CreatedAtUtc >= createdFrom.Value);

                    if (createdTo.HasValue)
                        query = query.Where(u => u.CreatedAtUtc <= createdTo.Value);

                    // Apply role filter with JOIN
                    if (roleId.HasValue)
                    {
                        var userRoleRepo = scopedUnitOfWork.Repository<UserRole>();
                        query = query.Join(userRoleRepo.GetQueryable(),
                            u => u.Id,
                            ur => ur.UserId,
                            (u, ur) => new { User = u, UserRole = ur })
                            .Where(x => x.UserRole.RoleId == roleId.Value && x.UserRole.OrganizationId == orgId && !x.UserRole.IsDeleted)
                            .Select(x => x.User);
                    }

                    usersToExport = await query.ToListAsync();
                }

                // Load roles for all users in batch
                if (usersToExport.Any())
                {
                    var userIds = usersToExport.Select(u => u.Id).ToList();
                    var userRoleRepo = scopedUnitOfWork.Repository<UserRole>();
                    var roleRepo = scopedUnitOfWork.Repository<Role>();
                    
                    var userRolesWithRoles = await userRoleRepo.GetQueryable()
                        .Where(ur => userIds.Contains(ur.UserId) && ur.OrganizationId == orgId && !ur.IsDeleted)
                        .Join(roleRepo.GetQueryable().Where(r => !r.IsDeleted && r.IsActive),
                            ur => ur.RoleId,
                            r => r.Id,
                            (ur, r) => new { UserId = ur.UserId, RoleName = r.Name })
                        .ToListAsync();

                    // Group roles by UserId and join with comma
                    var rolesByUserId = userRolesWithRoles
                        .GroupBy(ur => ur.UserId)
                        .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(r => r.RoleName)));

                    // Populate thread-safe dictionary
                    foreach (var user in usersToExport)
                    {
                        if (rolesByUserId.TryGetValue(user.Id, out var roleNames))
                        {
                            userRolesMap[user.Id] = roleNames;
                        }
                        else
                        {
                            userRolesMap[user.Id] = "";
                        }
                    }
                }

                return usersToExport;
            },
            filters: filters,
            columnMapper: (user) => MapUserToExportColumns(user, userRolesMap)
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

    private Dictionary<string, object> MapUserToExportColumns(User user, System.Collections.Concurrent.ConcurrentDictionary<Guid, string> userRolesMap)
    {
        // Get roles from pre-loaded dictionary
        var roles = userRolesMap.TryGetValue(user.Id, out var roleNames) ? roleNames : "";
        
        return new Dictionary<string, object>
        {
            ["ID"] = user.Id.ToString(),
            ["Full Name"] = user.FullName,
            ["Email"] = user.Email,
            ["Role"] = roles,
            ["Department"] = user.Department ?? "",
            ["Job Title"] = user.JobTitle ?? "",
            ["Location"] = user.Location ?? "",
            ["Phone Number"] = user.PhoneNumber ?? "",
            ["Employee ID"] = user.EmployeeId ?? "",
            ["Status"] = user.IsActive ? "Active" : "Inactive",
            ["Email Verified"] = user.IsEmailVerified ? "Yes" : "No",
            ["Phone Verified"] = user.IsPhoneVerified ? "Yes" : "No",
            ["Last Login"] = user.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never",
            ["Created Date"] = user.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm")
        };
    }

    public async Task<byte[]> GetImportTemplateAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();

        // Get master data for reference
        var departmentRepo = _unitOfWork.Repository<Department>();
        var departments = await departmentRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted && x.IsActive);

        var positionRepo = _unitOfWork.Repository<Position>();
        var positions = await positionRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted && x.IsActive);

        var locations = await _organizationService.GetLocationsAsync(OrganizationId);

        var roleRepo = _unitOfWork.Repository<Role>();
        var roles = await roleRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && x.IsActive);

        // Generate Excel template with dropdown validation
        return GenerateExcelImportTemplate(departments, positions, locations, roles);
    }

    private byte[] GenerateExcelImportTemplate(
        IEnumerable<Department> departments,
        IEnumerable<Position> positions,
        IEnumerable<LocationDto> locations,
        IEnumerable<Role> roles)
    {
        using var workbook = new XLWorkbook();

        // Sheet 1: Import Data Template
        var importSheet = workbook.Worksheets.Add("Import Data");

        // Set up headers
        var headers = new[] { "First Name", "Last Name", "Email", "Department", "Job Title", "Location", "Role", "Phone Number", "Status", "Employee ID" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = importSheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Add sample data row
        importSheet.Cell(2, 1).Value = "John";
        importSheet.Cell(2, 2).Value = "Doe";
        importSheet.Cell(2, 3).Value = "john.doe1@company.com";
        importSheet.Cell(2, 4).Value = departments.FirstOrDefault()?.Name ?? "IT";
        importSheet.Cell(2, 5).Value = positions.FirstOrDefault()?.Name ?? "Software Developer";
        importSheet.Cell(2, 6).Value = locations.FirstOrDefault()?.Name ?? "Main Office";
        importSheet.Cell(2, 7).Value = roles.FirstOrDefault()?.Name ?? "Admin";
        importSheet.Cell(2, 8).Value = "+1234567890";
        importSheet.Cell(2, 9).Value = "Active";
        importSheet.Cell(2, 10).Value = "EMP001";

        // Sheet 2: Reference Data
        var referenceSheet = workbook.Worksheets.Add("Reference Data");
        referenceSheet.Cell(1, 1).Value = "Departments";
        referenceSheet.Cell(1, 2).Value = "Job Titles";
        referenceSheet.Cell(1, 3).Value = "Locations";
        referenceSheet.Cell(1, 4).Value = "Roles";
        referenceSheet.Cell(1, 5).Value = "Status";

        // Style headers
        for (int i = 1; i <= 5; i++)
        {
            referenceSheet.Cell(1, i).Style.Font.Bold = true;
            referenceSheet.Cell(1, i).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Populate reference data
        int row = 2;
        var deptList = departments.OrderBy(d => d.Name).Select(d => d.Name).ToList();
        var positionList = positions.OrderBy(p => p.Name).Select(p => p.Name).ToList();
        var locationList = locations.OrderBy(l => l.Name).Select(l => l.Name).ToList();
        var roleList = roles.OrderBy(r => r.Name).Select(r => r.Name).ToList();
        var statusList = new List<string> { "Active", "Inactive" };

        int maxRows = new[] { deptList.Count, positionList.Count, locationList.Count, roleList.Count, statusList.Count }.Max();

        for (int i = 0; i < maxRows; i++)
        {
            if (i < deptList.Count) referenceSheet.Cell(row + i, 1).Value = deptList[i];
            if (i < positionList.Count) referenceSheet.Cell(row + i, 2).Value = positionList[i];
            if (i < locationList.Count) referenceSheet.Cell(row + i, 3).Value = locationList[i];
            if (i < roleList.Count) referenceSheet.Cell(row + i, 4).Value = roleList[i];
            if (i < statusList.Count) referenceSheet.Cell(row + i, 5).Value = statusList[i];
        }

        // Create named ranges for dropdowns
        var deptRange = referenceSheet.Range(2, 1, row + deptList.Count - 1, 1);
        var positionRange = referenceSheet.Range(2, 2, row + positionList.Count - 1, 2);
        var locationRange = referenceSheet.Range(2, 3, row + locationList.Count - 1, 3);
        var roleRange = referenceSheet.Range(2, 4, row + roleList.Count - 1, 4);
        var statusRange = referenceSheet.Range(2, 5, row + statusList.Count - 1, 5);

        workbook.NamedRanges.Add("Departments", deptRange);
        workbook.NamedRanges.Add("JobTitles", positionRange);
        workbook.NamedRanges.Add("Locations", locationRange);
        workbook.NamedRanges.Add("Roles", roleRange);
        workbook.NamedRanges.Add("StatusValues", statusRange);

        // Add data validation (dropdowns) to Import Data sheet (rows 2-1000)
        // Department column (D)
        var deptValidation = importSheet.Range("D2:D1000").SetDataValidation();
        deptValidation.List("=Departments", true);
        deptValidation.IgnoreBlanks = true;
        deptValidation.InCellDropdown = true;

        // Job Title column (E)
        var positionValidation = importSheet.Range("E2:E1000").SetDataValidation();
        positionValidation.List("=JobTitles", true);
        positionValidation.IgnoreBlanks = true;
        positionValidation.InCellDropdown = true;

        // Location column (F)
        var locationValidation = importSheet.Range("F2:F1000").SetDataValidation();
        locationValidation.List("=Locations", true);
        locationValidation.IgnoreBlanks = true;
        locationValidation.InCellDropdown = true;

        // Role column (G)
        var roleValidation = importSheet.Range("G2:G1000").SetDataValidation();
        roleValidation.List("=Roles", true);
        roleValidation.IgnoreBlanks = true;
        roleValidation.InCellDropdown = true;

        // Status column (I)
        var statusValidation = importSheet.Range("I2:I1000").SetDataValidation();
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

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    public async Task GeneratePasswordResetLinkAsync(Guid id)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<User>();
        var u = await repo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId);

        if (u == null) throw new ArgumentException("User not found");

        // Invalidate any previous active password reset tokens for this user
        var passwordResetRepo = _unitOfWork.Repository<PasswordResetToken>();
        var existingTokens = await passwordResetRepo.FindManyAsync(t => t.UserId == u.Id && t.OrganizationId == OrganizationId && !t.IsUsed && t.ExpiresAt > DateTimeOffset.UtcNow);
        foreach (var token in existingTokens)
        {
            token.IsUsed = true;
            token.UsedAt = DateTimeOffset.UtcNow;
            passwordResetRepo.Update(token);
        }

        // Create and persist a new password reset token
        var tokenValue = Guid.NewGuid().ToString("N");
        var passwordResetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            UserId = u.Id,
            Token = tokenValue,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
            IsUsed = false
        };

        await passwordResetRepo.AddAsync(passwordResetToken);
        await _unitOfWork.SaveChangesAsync();


        // Send the password reset email using the unified template
        await _emailService.SendPasswordResetEmailAsync(u.Email, tokenValue);

        await LogUserActivityAsync(u.Id, "PASSWORD_RESET_REQUESTED", "Password reset link sent", "User", u.Id);
    }

    public async Task SendEmailVerificationAsync(Guid id)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<User>();
        var u = await repo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId);

        if (u == null) throw new ArgumentException("User not found");

        // Invalidate any previous active verification tokens for this user
        var emailTokenRepo = _unitOfWork.Repository<EmailVerificationToken>();
        var existingTokens = await emailTokenRepo.FindManyAsync(t => t.UserId == u.Id && t.OrganizationId == OrganizationId && !t.IsUsed && t.ExpiresAt > DateTimeOffset.UtcNow);
        foreach (var token in existingTokens)
        {
            token.IsUsed = true;
            token.UsedAt = DateTimeOffset.UtcNow;
            emailTokenRepo.Update(token);
        }

        // Create and persist a new verification token
        var tokenValue = Guid.NewGuid().ToString("N");
        var emailToken = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            UserId = u.Id,
            Token = tokenValue,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            IsUsed = false
        };

        await emailTokenRepo.AddAsync(emailToken);
        await _unitOfWork.SaveChangesAsync();


        // Send the verification email using centralized template
        await _emailService.SendEmailVerificationAsync(u.Email, tokenValue);

        await LogUserActivityAsync(u.Id, "EMAIL_VERIFICATION_SENT", "Email verification sent", "User", u.Id);
    }

    public async Task ResendInvitationAsync(Guid id)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<User>();
        var u = await repo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId);

        if (u == null) throw new ArgumentException("User not found");

        await _emailService.SendInvitationEmailAsync(u.Email, u.FullName);

        await LogUserActivityAsync(u.Id, "INVITATION_RESENT", "Invitation resent", "User", u.Id);
    }

    public override async Task<UserStatisticsDto> GetStatisticsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<User>();
        var allUsers = await repo.FindManyAsync(u => u.OrganizationId == OrganizationId && !u.IsDeleted);

        return new UserStatisticsDto
        {
            Total = allUsers.Count(),
            Active = allUsers.Count(u => u.IsActive),
            Inactive = allUsers.Count(u => !u.IsActive),
            EmailVerifiedUsers = allUsers.Count(u => u.IsEmailVerified),
            EmailUnverifiedUsers = allUsers.Count(u => !u.IsEmailVerified),
            RecentlyCreatedUsers = allUsers.Count(u => u.CreatedAtUtc >= DateTimeOffset.UtcNow.AddDays(-30))
        };
    }

    // Keep backward compatibility
    public async Task<UserStatisticsDto> GetUserStatisticsAsync()
    {
        return await GetStatisticsAsync();
    }


    private static UserDetailsDto MapToDetails(User u)
    {
        return new UserDetailsDto
        {
            Id = u.Id,
            Email = u.Email,
            FullName = u.FullName,
            FirstName = u.FirstName,
            LastName = u.LastName,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
            IsEmailVerified = u.IsEmailVerified,
            IsPhoneVerified = u.IsPhoneVerified,
            LastLoginAt = u.LastLoginAt,
            AvatarUrl = u.AvatarUrl,
            TimeZone = u.TimeZone,
            Language = u.Language,
            IsMfaEnabled = u.IsMfaEnabled,
            JobTitle = u.JobTitle,
            Department = u.Department,
            Location = u.Location,
            EmployeeId = u.EmployeeId,
            DateOfBirth = u.DateOfBirth?.DateTime,
            Address = u.Address,
            City = u.City,
            State = u.State,
            Country = u.Country,
            PostalCode = u.PostalCode,
            EmergencyContactName = u.EmergencyContactName,
            EmergencyContactPhone = u.EmergencyContactPhone,
            EmergencyContactRelation = u.EmergencyContactRelation,
            CreatedAtUtc = u.CreatedAtUtc,
            LockedUntil = u.LockedUntil,
            CreatedBy = u.CreatedBy,
            ModifiedAtUtc = u.ModifiedAtUtc,
            ModifiedBy = u.ModifiedBy,
            OrganizationId = u.OrganizationId,
            OrganizationName = null // Will be populated separately if needed
        };
    }

    public override async Task<DropdownOptionsDto> GetDropdownOptionsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();

        // Get locations from existing OrganizationService
        var locations = await _organizationService.GetLocationsAsync(OrganizationId);

        // Get departments from master data
        var departmentRepo = _unitOfWork.Repository<Department>();
        var departments = await departmentRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted && x.IsActive);

        // Get positions from master data
        var positionRepo = _unitOfWork.Repository<Position>();
        var positions = await positionRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted && x.IsActive);

        // Get roles
        var roleRepo = _unitOfWork.Repository<Role>();
        var roles = await roleRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted && x.IsActive);

        return new DropdownOptionsDto
        {
            Locations = locations.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Name).ToList(),
            Departments = departments.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Name).ToList(),
            Positions = positions.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Name).ToList(),
            Roles = roles.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new RoleDropdownDto
            {
                Id = x.Id,
                Name = x.Name,
                RoleType = x.RoleType
            }).ToList()
        };
    }

    public async Task<List<string>> GetLocationOptionsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var locations = await _organizationService.GetLocationsAsync(OrganizationId);

        return locations.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Name).ToList();
    }

    public async Task<List<string>> GetDepartmentOptionsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<Department>();
        var departments = await repo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted && x.IsActive);

        return departments.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Name).ToList();
    }

    public async Task<List<string>> GetPositionOptionsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<Position>();
        var positions = await repo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted && x.IsActive);

        return positions.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => x.Name).ToList();
    }

    // ========================================
    // Unified Import/Export History (Using ImportExportHistory table)
    // ========================================

    public async Task<PagedResultDto<ImportExportHistoryDto>> GetImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
    {
        return await _importExportService.GetHistoryAsync("User", type, page, pageSize);
    }
}


