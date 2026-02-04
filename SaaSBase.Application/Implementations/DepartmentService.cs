using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Application;
using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;

namespace SaaSBase.Application.Implementations;

public class DepartmentService : IDepartmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly IUserContextService _userContextService;
    private readonly ICacheService _cacheService;
    private readonly IImportExportService _importExportService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DepartmentService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, IUserContextService userContextService, ICacheService cacheService, IImportExportService importExportService, IServiceScopeFactory serviceScopeFactory)
    {
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _userContextService = userContextService;
        _cacheService = cacheService;
        _importExportService = importExportService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<PagedResultDto<DepartmentDto>> GetDepartmentsAsync(string? search, bool? isActive, Guid? organizationId, int page, int pageSize, string? sortField = null, string? sortDirection = "desc", DateTime? createdFrom = null, DateTime? createdTo = null)
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

        var cacheKey = _cacheService.GenerateListCacheKey("departments", filterOrganizationId, page, pageSize,
            search, sortField, sortDirection, isActive, organizationId, createdFrom, createdTo);

        var cachedResult = await _cacheService.GetCachedAsync<PagedResultDto<DepartmentDto>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // Build query with proper filtering
        IQueryable<Department> query;
        if (isSystemAdmin && !organizationId.HasValue)
        {
            // System Admin without filter - show all organizations
            query = _unitOfWork.Repository<Department>().GetQueryable()
                .IgnoreQueryFilters()
                .Where(d => !d.IsDeleted);
        }
        else
        {
            // Filter by specific organization
            query = _unitOfWork.Repository<Department>().GetQueryable()
                .IgnoreQueryFilters()
                .Where(d => d.OrganizationId == filterOrganizationId && !d.IsDeleted);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(d =>
                d.Name.ToLower().Contains(searchLower) ||
                (d.Description != null && d.Description.ToLower().Contains(searchLower)) ||
                (d.Code != null && d.Code.ToLower().Contains(searchLower)) ||
                (d.ManagerName != null && d.ManagerName.ToLower().Contains(searchLower)));
        }

        if (isActive.HasValue)
            query = query.Where(d => d.IsActive == isActive.Value);

        if (createdFrom.HasValue)
            query = query.Where(d => d.CreatedAtUtc >= createdFrom.Value);

        if (createdTo.HasValue)
            query = query.Where(d => d.CreatedAtUtc <= createdTo.Value);

        var totalCount = await query.CountAsync();

        query = ApplySorting(query, sortField, sortDirection);

        var departments = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DepartmentDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                Code = d.Code,
                ManagerId = d.ManagerId,
                ManagerName = d.ManagerName,
                IsActive = d.IsActive,
                SortOrder = d.SortOrder,
                CreatedAtUtc = d.CreatedAtUtc,
                LastModifiedAtUtc = d.ModifiedAtUtc ?? d.CreatedAtUtc,
                OrganizationId = d.OrganizationId,
                OrganizationName = null // Will be populated below
            })
            .ToListAsync();
        
        // Populate organization names
        if (departments.Any())
        {
            var orgIds = departments.Select(d => d.OrganizationId).Distinct().ToList();
            var orgRepo = _unitOfWork.Repository<Organization>();
            var organizations = await orgRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(o => orgIds.Contains(o.Id) && !o.IsDeleted)
                .Select(o => new { o.Id, o.Name })
                .ToDictionaryAsync(o => o.Id, o => o.Name);

            foreach (var department in departments)
            {
                if (organizations.TryGetValue(department.OrganizationId, out var orgName))
                {
                    department.OrganizationName = orgName;
                }
            }
        }

        var result = new PagedResultDto<DepartmentDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = departments
        };

        await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));

        return result;
    }

    public async Task<List<DepartmentDto>> GetDepartmentsAsync(bool? isActive = null)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var repo = _unitOfWork.Repository<Department>();
        
        var departments = await repo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);
        
        if (isActive.HasValue)
        {
            departments = departments.Where(x => x.IsActive == isActive.Value);
        }

        return departments.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(MapToDto).ToList();
    }

    public async Task<DepartmentDto?> GetDepartmentByIdAsync(Guid id)
    {
        var organizationId = _tenantService.GetOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var repo = _unitOfWork.Repository<Department>();
        
        Department? department;
        if (isSystemAdmin)
        {
            // System Admin can view departments from any organization
            department = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }
        else
        {
            // Regular users can only view departments from their organization
            department = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (department == null) return null;
        
        var dto = MapToDto(department);
        
        // Populate organization name
        if (department.OrganizationId != Guid.Empty)
        {
            var orgRepo = _unitOfWork.Repository<Organization>();
            var org = await orgRepo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == department.OrganizationId && !o.IsDeleted);
            dto.OrganizationName = org?.Name;
        }
        
        return dto;
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentDto dto)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Department>();
        
        var department = new Department
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            Name = dto.Name,
            Description = dto.Description,
            Code = dto.Code,
            ManagerId = dto.ManagerId,
            ManagerName = dto.ManagerName,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = userId.ToString()
        };

        await repo.AddAsync(department);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidateDepartmentCachesAsync(OrganizationId);
        
        return MapToDto(department);
    }

    public async Task<DepartmentDto?> UpdateDepartmentAsync(Guid id, UpdateDepartmentDto dto)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Department>();
        
        Department? department;
        if (isSystemAdmin)
        {
            // System Admin can view departments from any organization, but can only edit their own organization's departments
            department = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (department != null && department.OrganizationId != organizationId)
            {
                // System Admin cannot edit departments from other organizations
                throw new UnauthorizedAccessException("You can only edit departments from your own organization");
            }
        }
        else
        {
            // Regular users can only edit departments from their organization
            department = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (department == null) return null;

        department.Name = dto.Name;
        department.Description = dto.Description;
        department.Code = dto.Code;
        department.ManagerId = dto.ManagerId;
        department.ManagerName = dto.ManagerName;
        department.IsActive = dto.IsActive;
        department.SortOrder = dto.SortOrder;
        department.ModifiedAtUtc = DateTimeOffset.UtcNow;
        department.ModifiedBy = userId.ToString();

        repo.Update(department);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidateDepartmentCachesAsync(organizationId);
        
        return MapToDto(department);
    }

    public async Task<bool> DeleteDepartmentAsync(Guid id)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Department>();
        
        Department? department;
        if (isSystemAdmin)
        {
            // System Admin can view departments from any organization, but can only delete their own organization's departments
            department = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (department != null && department.OrganizationId != organizationId)
            {
                // System Admin cannot delete departments from other organizations
                throw new UnauthorizedAccessException("You can only delete departments from your own organization");
            }
        }
        else
        {
            // Regular users can only delete departments from their organization
            department = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (department == null) return false;

        department.IsDeleted = true;
        department.DeletedAtUtc = DateTimeOffset.UtcNow;
        department.DeletedBy = userId.ToString();
        department.ModifiedAtUtc = DateTimeOffset.UtcNow;
        department.ModifiedBy = userId.ToString();
        
        repo.Update(department);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidateDepartmentCachesAsync(organizationId);
        
        return true;
    }

    public async Task BulkDeleteAsync(List<Guid> ids)
    {
        if (ids == null || !ids.Any())
            return;

        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Department>();

        var departments = await repo.GetQueryable()
            .Where(d => ids.Contains(d.Id) && d.OrganizationId == OrganizationId && !d.IsDeleted)
            .ToListAsync();

        if (!departments.Any())
            return;

        foreach (var department in departments)
        {
            department.IsDeleted = true;
            department.DeletedAtUtc = DateTimeOffset.UtcNow;
            department.ModifiedAtUtc = DateTimeOffset.UtcNow;
            department.ModifiedBy = userId.ToString();
            repo.Update(department);
        }

        await _unitOfWork.SaveChangesAsync();
        await InvalidateDepartmentCachesAsync(OrganizationId);
    }

    public async Task<List<DepartmentDto>> BulkCloneAsync(List<Guid> ids)
    {
        if (ids == null || !ids.Any())
            return new List<DepartmentDto>();

        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Department>();

        // Get original departments
        var originalDepartments = await repo.GetQueryable()
            .Where(d => ids.Contains(d.Id) && d.OrganizationId == organizationId && !d.IsDeleted)
            .ToListAsync();

        if (!originalDepartments.Any())
            return new List<DepartmentDto>();

        var clonedDepartments = new List<DepartmentDto>();
        var generatedNames = new HashSet<string>(); // Track names in current batch
        var clonedDepartmentEntities = new List<Department>(); // Store department entities before saving

        // First, get all existing department names from database to avoid conflicts
        var existingNames = await repo.GetQueryable()
            .Where(d => d.OrganizationId == organizationId && !d.IsDeleted)
            .Select(d => d.Name)
            .ToListAsync();
        
        generatedNames.UnionWith(existingNames);

        foreach (var originalDepartment in originalDepartments)
        {
            // Generate unique department name with GUID to ensure uniqueness
            var baseName = originalDepartment.Name;
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

            // Create cloned department
            var clonedDepartment = new Department
            {
                Id = Guid.NewGuid(),
                Name = newName,
                Description = originalDepartment.Description,
                Code = originalDepartment.Code,
                ManagerId = originalDepartment.ManagerId, // Keep same manager
                ManagerName = originalDepartment.ManagerName,
                IsActive = false, // Cloned departments start as inactive
                SortOrder = originalDepartment.SortOrder,
                OrganizationId = organizationId,
                CreatedBy = userId.ToString(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ModifiedBy = userId.ToString(),
                ModifiedAtUtc = DateTimeOffset.UtcNow
            };

            clonedDepartmentEntities.Add(clonedDepartment);
        }

        // Add all department entities at once
        foreach (var clonedDepartment in clonedDepartmentEntities)
        {
            await repo.AddAsync(clonedDepartment);
        }

        // Save all cloned departments in a single transaction
        await _unitOfWork.SaveChangesAsync();

        // Build DTOs after saving
        foreach (var clonedDepartment in clonedDepartmentEntities)
        {
            clonedDepartments.Add(MapToDto(clonedDepartment));
        }

        // Invalidate caches
        await InvalidateDepartmentCachesAsync(organizationId);
        foreach (var clonedDepartment in clonedDepartmentEntities)
        {
            await _cacheService.RemoveCacheAsync($"department:detail:{clonedDepartment.Id}");
        }

        return clonedDepartments;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Department>();
        
        Department? department;
        if (isSystemAdmin)
        {
            // System Admin can view departments from any organization, but can only modify their own organization's departments
            department = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (department != null && department.OrganizationId != organizationId)
            {
                // System Admin cannot modify departments from other organizations
                throw new UnauthorizedAccessException("You can only modify departments from your own organization");
            }
        }
        else
        {
            // Regular users can only modify departments from their organization
            department = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (department == null) return false;

        department.IsActive = isActive;
        department.ModifiedAtUtc = DateTimeOffset.UtcNow;
        department.ModifiedBy = userId.ToString();
        repo.Update(department);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidateDepartmentCachesAsync(organizationId);
        
        return true;
    }

    public async Task<byte[]> GetImportTemplateAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<Department>();
        
        var departments = await repo.GetQueryable()
            .Where(d => d.OrganizationId == OrganizationId && !d.IsDeleted)
            .Select(d => new { d.Name, d.Code, d.Description })
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var importSheet = workbook.Worksheets.Add("Import Data");
        var referenceSheet = workbook.Worksheets.Add("Reference Data");

        // Headers
        importSheet.Cell(1, 1).Value = "Name";
        importSheet.Cell(1, 2).Value = "Code";
        importSheet.Cell(1, 3).Value = "Description";
        importSheet.Cell(1, 4).Value = "Manager Name";
        importSheet.Cell(1, 5).Value = "Status";
        importSheet.Cell(1, 6).Value = "Sort Order";

        // Style headers
        var headerRange = importSheet.Range(1, 1, 1, 6);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Status values
        referenceSheet.Cell(1, 1).Value = "Status";
        referenceSheet.Cell(1, 1).Style.Font.Bold = true;
        referenceSheet.Cell(2, 1).Value = "Active";
        referenceSheet.Cell(3, 1).Value = "Inactive";

        workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 1, 3, 1));

        // Add data validation
        var statusValidation = importSheet.Range("F2:F1000").SetDataValidation();
        statusValidation.List("=StatusValues", true);
        statusValidation.IgnoreBlanks = true;
        statusValidation.InCellDropdown = true;

        importSheet.Columns().AdjustToContents();
        referenceSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<string> StartExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var userName = _tenantService.GetCurrentUserName();

        return await _importExportService.StartExportJobAsync<DepartmentDto>(
            entityType: "Department",
            format: format,
            dataFetcher: async (f) =>
            {
                // Create new scope to get fresh DbContext using IServiceScopeFactory
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
                
                // CRITICAL: Set background context to ensure DbContext global query filter works correctly
                scopedTenantService.SetBackgroundContext(organizationId, userId, userName);

                var search = f.ContainsKey("search") ? f["search"]?.ToString() : null;
                var isActive = f.ContainsKey("isActive") ? (bool?)f["isActive"] : null;
                var createdFrom = f.ContainsKey("createdFrom") ? (DateTime?)f["createdFrom"] : null;
                var createdTo = f.ContainsKey("createdTo") ? (DateTime?)f["createdTo"] : null;
                var selectedIds = f.ContainsKey("selectedIds") ? (List<Guid>?)f["selectedIds"] : null;

                var query = scopedUnitOfWork.Repository<Department>().GetQueryable()
                    .Where(d => d.OrganizationId == organizationId && !d.IsDeleted);

                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(d =>
                        d.Name.ToLower().Contains(searchLower) ||
                        (d.Description != null && d.Description.ToLower().Contains(searchLower)) ||
                        (d.Code != null && d.Code.ToLower().Contains(searchLower)));
                }

                if (isActive.HasValue)
                    query = query.Where(d => d.IsActive == isActive.Value);

                if (createdFrom.HasValue)
                    query = query.Where(d => d.CreatedAtUtc >= createdFrom.Value);

                if (createdTo.HasValue)
                    query = query.Where(d => d.CreatedAtUtc <= createdTo.Value);

                if (selectedIds != null && selectedIds.Any())
                    query = query.Where(d => selectedIds.Contains(d.Id));

                var departments = await query
                    .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
                    .Select(d => new DepartmentDto
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Description = d.Description,
                        Code = d.Code,
                        ManagerId = d.ManagerId,
                        ManagerName = d.ManagerName,
                        IsActive = d.IsActive,
                        SortOrder = d.SortOrder,
                        CreatedAtUtc = d.CreatedAtUtc,
                        LastModifiedAtUtc = d.ModifiedAtUtc ?? d.CreatedAtUtc
                    })
                    .ToListAsync();

                return departments;
            },
            filters: filters,
            columnMapper: (d) => new Dictionary<string, object>
            {
                ["Name"] = d.Name ?? "",
                ["Code"] = d.Code ?? "",
                ["Description"] = d.Description ?? "",
                ["Manager Name"] = d.ManagerName ?? "",
                ["Status"] = d.IsActive ? "Active" : "Inactive",
                ["Sort Order"] = d.SortOrder.ToString(),
                ["Created At"] = d.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["Last Modified"] = d.LastModifiedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
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
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();

        return await _importExportService.StartImportJobAsync<CreateDepartmentDto>(
            entityType: "Department",
            fileStream: fileStream,
            fileName: fileName,
            rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
            {
                if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
                    return (false, "Name is required", false, false);

                var repo = scopedUnitOfWork.Repository<Department>();

                var code = rowData.GetValueOrDefault("Code");
                var existingDepartment = !string.IsNullOrWhiteSpace(code)
                    ? await repo.FindAsync(d => d.OrganizationId == OrganizationId && d.Code != null && d.Code.ToLower() == code.ToLower() && !d.IsDeleted)
                    : await repo.FindAsync(d => d.OrganizationId == OrganizationId && d.Name.ToLower() == name.ToLower() && !d.IsDeleted);

                if (existingDepartment != null)
                {
                    if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
                        return (true, null, false, true);

                    if (duplicateStrategy == DuplicateHandlingStrategy.Update)
                    {
                        existingDepartment.Name = name;
                        existingDepartment.Description = rowData.GetValueOrDefault("Description");
                        existingDepartment.Code = code;
                        existingDepartment.ManagerName = rowData.GetValueOrDefault("Manager Name");
                        if (rowData.TryGetValue("Status", out var status))
                            existingDepartment.IsActive = status?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;
                        if (int.TryParse(rowData.GetValueOrDefault("Sort Order"), out var sortOrder))
                            existingDepartment.SortOrder = sortOrder;
                        existingDepartment.ModifiedAtUtc = DateTimeOffset.UtcNow;
                        existingDepartment.ModifiedBy = userId.ToString();
                        repo.Update(existingDepartment);
                        return (true, null, true, false);
                    }
                }

                var newDepartment = new Department
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = OrganizationId,
                    Name = name,
                    Description = rowData.GetValueOrDefault("Description"),
                    Code = code,
                    ManagerName = rowData.GetValueOrDefault("Manager Name"),
                    IsActive = true,
                    SortOrder = 0,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = userId.ToString()
                };

                if (rowData.TryGetValue("Status", out var statusValue))
                    newDepartment.IsActive = statusValue?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;
                if (int.TryParse(rowData.GetValueOrDefault("Sort Order"), out var newSortOrder))
                    newDepartment.SortOrder = newSortOrder;

                await repo.AddAsync(newDepartment);
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
        return await _importExportService.GetHistoryAsync("Department", type, page, pageSize);
    }

    public async Task<DepartmentStatisticsDto> GetStatisticsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<Department>();
        
        var allDepartments = await repo.GetQueryable()
            .Where(d => d.OrganizationId == OrganizationId && !d.IsDeleted)
            .ToListAsync();

        return new DepartmentStatisticsDto
        {
            Total = allDepartments.Count,
            Active = allDepartments.Count(d => d.IsActive),
            Inactive = allDepartments.Count(d => !d.IsActive)
        };
    }

    public async Task<DepartmentDropdownOptionsDto> GetDropdownOptionsAsync()
    {
        return new DepartmentDropdownOptionsDto
        {
            Levels = new List<string>() // Add levels if needed
        };
    }

    public async Task<byte[]?> GetImportErrorReportAsync(string errorReportId)
    {
        return await _importExportService.GetImportErrorReportAsync(errorReportId);
    }

    private IQueryable<Department> ApplySorting(IQueryable<Department> query, string? sortField, string? sortDirection)
    {
        sortField = sortField?.ToLower() ?? "createdatutc";
        sortDirection = sortDirection?.ToLower() ?? "desc";

        return sortField switch
        {
            "name" => sortDirection == "asc" ? query.OrderBy(d => d.Name) : query.OrderByDescending(d => d.Name),
            "code" => sortDirection == "asc" ? query.OrderBy(d => d.Code) : query.OrderByDescending(d => d.Code),
            "sortorder" => sortDirection == "asc" ? query.OrderBy(d => d.SortOrder) : query.OrderByDescending(d => d.SortOrder),
            "isactive" => sortDirection == "asc" ? query.OrderBy(d => d.IsActive) : query.OrderByDescending(d => d.IsActive),
            _ => sortDirection == "asc" ? query.OrderBy(d => d.CreatedAtUtc) : query.OrderByDescending(d => d.CreatedAtUtc)
        };
    }

    private async Task InvalidateDepartmentCachesAsync(Guid organizationId)
    {
        // Clear all list cache variations (all pages, filters, sorts)
        // Pattern with wildcard matches: departments:list:{orgId}:1:10:null:null:null:...
        await _cacheService.RemoveCacheByPatternAsync($"departments:list:{organizationId}:*");
        // Pattern without wildcard matches any key starting with this prefix (redundant but safe)
        await _cacheService.RemoveCacheByPatternAsync($"departments:list:{organizationId}");
        // Clear dropdown and stats caches
        await _cacheService.RemoveCacheByPatternAsync($"departments:dropdown:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"departments:stats:{organizationId}");
        // Also clear detail cache if exists
        await _cacheService.RemoveCacheByPatternAsync($"department:detail:*");
    }

    private static DepartmentDto MapToDto(Department department)
    {
        return new DepartmentDto
        {
            Id = department.Id,
            Name = department.Name,
            Description = department.Description,
            Code = department.Code,
            ManagerId = department.ManagerId,
            ManagerName = department.ManagerName,
            IsActive = department.IsActive,
            SortOrder = department.SortOrder,
            CreatedAtUtc = department.CreatedAtUtc,
            LastModifiedAtUtc = department.ModifiedAtUtc ?? department.CreatedAtUtc,
            OrganizationId = department.OrganizationId,
            OrganizationName = null // Will be populated separately if needed
        };
    }
}
