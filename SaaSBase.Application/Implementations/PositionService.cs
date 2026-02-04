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

public class PositionService : IPositionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentTenantService _tenantService;
    private readonly IUserContextService _userContextService;
    private readonly ICacheService _cacheService;
    private readonly IImportExportService _importExportService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PositionService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, IUserContextService userContextService, ICacheService cacheService, IImportExportService importExportService, IServiceScopeFactory serviceScopeFactory)
    {
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _userContextService = userContextService;
        _cacheService = cacheService;
        _importExportService = importExportService;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<PagedResultDto<PositionDto>> GetPositionsAsync(string? search, bool? isActive, Guid? departmentId, Guid? organizationId, int page, int pageSize, string? sortField = null, string? sortDirection = "desc", DateTime? createdFrom = null, DateTime? createdTo = null)
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

        var cacheKey = _cacheService.GenerateListCacheKey("positions", filterOrganizationId, page, pageSize,
            search, sortField, sortDirection, isActive, departmentId?.ToString(), organizationId, createdFrom, createdTo);

        var cachedResult = await _cacheService.GetCachedAsync<PagedResultDto<PositionDto>>(cacheKey);
        if (cachedResult != null)
        {
            return cachedResult;
        }

        // Build query with proper filtering
        IQueryable<Position> query;
        if (isSystemAdmin && !organizationId.HasValue)
        {
            // System Admin without filter - show all organizations
            query = _unitOfWork.Repository<Position>().GetQueryable()
                .IgnoreQueryFilters()
                .Where(p => !p.IsDeleted);
        }
        else
        {
            // Filter by specific organization
            query = _unitOfWork.Repository<Position>().GetQueryable()
                .IgnoreQueryFilters()
                .Where(p => p.OrganizationId == filterOrganizationId && !p.IsDeleted);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                (p.Code != null && p.Code.ToLower().Contains(searchLower)) ||
                (p.Level != null && p.Level.ToLower().Contains(searchLower)) ||
                (p.DepartmentName != null && p.DepartmentName.ToLower().Contains(searchLower)));
        }

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (departmentId.HasValue)
            query = query.Where(p => p.DepartmentId == departmentId.Value);

        if (createdFrom.HasValue)
            query = query.Where(p => p.CreatedAtUtc >= createdFrom.Value);

        if (createdTo.HasValue)
            query = query.Where(p => p.CreatedAtUtc <= createdTo.Value);

        var totalCount = await query.CountAsync();

        query = ApplySorting(query, sortField, sortDirection);

        var positions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PositionDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Code = p.Code,
                Level = p.Level,
                DepartmentId = p.DepartmentId,
                DepartmentName = p.DepartmentName,
                IsActive = p.IsActive,
                SortOrder = p.SortOrder,
                CreatedAtUtc = p.CreatedAtUtc,
                LastModifiedAtUtc = p.ModifiedAtUtc ?? p.CreatedAtUtc,
                OrganizationId = p.OrganizationId,
                OrganizationName = null // Will be populated below
            })
            .ToListAsync();
        
        // Populate organization names
        if (positions.Any())
        {
            var orgIds = positions.Select(p => p.OrganizationId).Distinct().ToList();
            var orgRepo = _unitOfWork.Repository<Organization>();
            var organizations = await orgRepo.GetQueryable()
                .IgnoreQueryFilters()
                .Where(o => orgIds.Contains(o.Id) && !o.IsDeleted)
                .Select(o => new { o.Id, o.Name })
                .ToDictionaryAsync(o => o.Id, o => o.Name);

            foreach (var position in positions)
            {
                if (organizations.TryGetValue(position.OrganizationId, out var orgName))
                {
                    position.OrganizationName = orgName;
                }
            }
        }

        var result = new PagedResultDto<PositionDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = positions
        };

        await _cacheService.SetCacheAsync(cacheKey, result, TimeSpan.FromMinutes(_cacheService.GetCacheExpirationMinutes()));

        return result;
    }

    public async Task<List<PositionDto>> GetPositionsAsync(bool? isActive = null, Guid? departmentId = null)
    {
        var OrganizationId = _tenantService.GetOrganizationId();
        var repo = _unitOfWork.Repository<Position>();
        
        var positions = await repo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);
        
        if (isActive.HasValue)
        {
            positions = positions.Where(x => x.IsActive == isActive.Value);
        }

        if (departmentId.HasValue)
        {
            positions = positions.Where(x => x.DepartmentId == departmentId.Value);
        }

        return positions.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(MapToDto).ToList();
    }

    public async Task<PositionDto?> GetPositionByIdAsync(Guid id)
    {
        var organizationId = _tenantService.GetOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var repo = _unitOfWork.Repository<Position>();
        
        Position? position;
        if (isSystemAdmin)
        {
            // System Admin can view positions from any organization
            position = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }
        else
        {
            // Regular users can only view positions from their organization
            position = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (position == null) return null;
        
        var dto = MapToDto(position);
        
        // Populate organization name
        if (position.OrganizationId != Guid.Empty)
        {
            var orgRepo = _unitOfWork.Repository<Organization>();
            var org = await orgRepo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == position.OrganizationId && !o.IsDeleted);
            dto.OrganizationName = org?.Name;
        }
        
        return dto;
    }

    public async Task<PositionDto> CreatePositionAsync(CreatePositionDto dto)
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Position>();
        
        var position = new Position
        {
            Id = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            Name = dto.Name,
            Description = dto.Description,
            Code = dto.Code,
            Level = dto.Level,
            DepartmentId = dto.DepartmentId,
            DepartmentName = dto.DepartmentName,
            IsActive = dto.IsActive,
            SortOrder = dto.SortOrder,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = userId.ToString()
        };

        await repo.AddAsync(position);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidatePositionCachesAsync(OrganizationId);
        
        return MapToDto(position);
    }

    public async Task<PositionDto?> UpdatePositionAsync(Guid id, UpdatePositionDto dto)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Position>();
        
        Position? position;
        if (isSystemAdmin)
        {
            // System Admin can view positions from any organization, but can only edit their own organization's positions
            position = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (position != null && position.OrganizationId != organizationId)
            {
                // System Admin cannot edit positions from other organizations
                throw new UnauthorizedAccessException("You can only edit positions from your own organization");
            }
        }
        else
        {
            // Regular users can only edit positions from their organization
            position = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (position == null) return null;

        position.Name = dto.Name;
        position.Description = dto.Description;
        position.Code = dto.Code;
        position.Level = dto.Level;
        position.DepartmentId = dto.DepartmentId;
        position.DepartmentName = dto.DepartmentName;
        position.IsActive = dto.IsActive;
        position.SortOrder = dto.SortOrder;
        position.ModifiedAtUtc = DateTimeOffset.UtcNow;
        position.ModifiedBy = userId.ToString();

        repo.Update(position);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidatePositionCachesAsync(organizationId);
        
        return MapToDto(position);
    }

    public async Task<bool> DeletePositionAsync(Guid id)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Position>();
        
        Position? position;
        if (isSystemAdmin)
        {
            // System Admin can view positions from any organization, but can only delete their own organization's positions
            position = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (position != null && position.OrganizationId != organizationId)
            {
                // System Admin cannot delete positions from other organizations
                throw new UnauthorizedAccessException("You can only delete positions from your own organization");
            }
        }
        else
        {
            // Regular users can only delete positions from their organization
            position = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (position == null) return false;

        position.IsDeleted = true;
        position.DeletedAtUtc = DateTimeOffset.UtcNow;
        position.DeletedBy = userId.ToString();
        position.ModifiedAtUtc = DateTimeOffset.UtcNow;
        position.ModifiedBy = userId.ToString();
        
        repo.Update(position);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidatePositionCachesAsync(organizationId);
        
        return true;
    }

    public async Task BulkDeleteAsync(List<Guid> ids)
    {
        if (ids == null || !ids.Any())
            return;

        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Position>();

        var positions = await repo.GetQueryable()
            .Where(p => ids.Contains(p.Id) && p.OrganizationId == OrganizationId && !p.IsDeleted)
            .ToListAsync();

        if (!positions.Any())
            return;

        foreach (var position in positions)
        {
            position.IsDeleted = true;
            position.DeletedAtUtc = DateTimeOffset.UtcNow;
            position.ModifiedAtUtc = DateTimeOffset.UtcNow;
            position.ModifiedBy = userId.ToString();
            repo.Update(position);
        }

        await _unitOfWork.SaveChangesAsync();
        await InvalidatePositionCachesAsync(OrganizationId);
    }

    public async Task<List<PositionDto>> BulkCloneAsync(List<Guid> ids)
    {
        if (ids == null || !ids.Any())
            return new List<PositionDto>();

        var organizationId = _tenantService.GetCurrentOrganizationId();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Position>();

        // Get original positions
        var originalPositions = await repo.GetQueryable()
            .Where(p => ids.Contains(p.Id) && p.OrganizationId == organizationId && !p.IsDeleted)
            .ToListAsync();

        if (!originalPositions.Any())
            return new List<PositionDto>();

        var clonedPositions = new List<PositionDto>();
        var generatedNames = new HashSet<string>(); // Track names in current batch
        var clonedPositionEntities = new List<Position>(); // Store position entities before saving

        // First, get all existing position names from database to avoid conflicts
        var existingNames = await repo.GetQueryable()
            .Where(p => p.OrganizationId == organizationId && !p.IsDeleted)
            .Select(p => p.Name)
            .ToListAsync();
        
        generatedNames.UnionWith(existingNames);

        foreach (var originalPosition in originalPositions)
        {
            // Generate unique position name with GUID to ensure uniqueness
            var baseName = originalPosition.Name;
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

            // Create cloned position
            var clonedPosition = new Position
            {
                Id = Guid.NewGuid(),
                Name = newName,
                Description = originalPosition.Description,
                Code = originalPosition.Code,
                Level = originalPosition.Level,
                DepartmentId = originalPosition.DepartmentId, // Keep same department
                DepartmentName = originalPosition.DepartmentName,
                IsActive = false, // Cloned positions start as inactive
                SortOrder = originalPosition.SortOrder,
                OrganizationId = organizationId,
                CreatedBy = userId.ToString(),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ModifiedBy = userId.ToString(),
                ModifiedAtUtc = DateTimeOffset.UtcNow
            };

            clonedPositionEntities.Add(clonedPosition);
        }

        // Add all position entities at once
        foreach (var clonedPosition in clonedPositionEntities)
        {
            await repo.AddAsync(clonedPosition);
        }

        // Save all cloned positions in a single transaction
        await _unitOfWork.SaveChangesAsync();

        // Build DTOs after saving
        foreach (var clonedPosition in clonedPositionEntities)
        {
            clonedPositions.Add(MapToDto(clonedPosition));
        }

        // Invalidate caches
        await InvalidatePositionCachesAsync(organizationId);
        foreach (var clonedPosition in clonedPositionEntities)
        {
            await _cacheService.RemoveCacheAsync($"position:detail:{clonedPosition.Id}");
        }

        return clonedPositions;
    }

    public async Task<bool> SetActiveAsync(Guid id, bool isActive)
    {
        var organizationId = _tenantService.GetCurrentOrganizationId();
        var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
        var userId = _tenantService.GetCurrentUserId();
        var repo = _unitOfWork.Repository<Position>();
        
        Position? position;
        if (isSystemAdmin)
        {
            // System Admin can view positions from any organization, but can only modify their own organization's positions
            position = await repo.GetQueryable()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            
            if (position != null && position.OrganizationId != organizationId)
            {
                // System Admin cannot modify positions from other organizations
                throw new UnauthorizedAccessException("You can only modify positions from your own organization");
            }
        }
        else
        {
            // Regular users can only modify positions from their organization
            position = await repo.FindAsync(x => x.Id == id && x.OrganizationId == organizationId && !x.IsDeleted);
        }
        
        if (position == null) return false;

        position.IsActive = isActive;
        position.ModifiedAtUtc = DateTimeOffset.UtcNow;
        position.ModifiedBy = userId.ToString();
        repo.Update(position);
        await _unitOfWork.SaveChangesAsync();
        
        await InvalidatePositionCachesAsync(organizationId);
        
        return true;
    }

    public async Task<byte[]> GetImportTemplateAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var departmentRepo = _unitOfWork.Repository<Department>();
        
        var departments = await departmentRepo.GetQueryable()
            .Where(d => d.OrganizationId == OrganizationId && !d.IsDeleted && d.IsActive)
            .Select(d => d.Name)
            .Distinct()
            .ToListAsync();

        var levels = new[] { "Junior", "Mid", "Senior", "Lead", "Manager", "Director", "Executive" };

        using var workbook = new XLWorkbook();
        var importSheet = workbook.Worksheets.Add("Import Data");
        var referenceSheet = workbook.Worksheets.Add("Reference Data");

        // Headers
        importSheet.Cell(1, 1).Value = "Name";
        importSheet.Cell(1, 2).Value = "Code";
        importSheet.Cell(1, 3).Value = "Description";
        importSheet.Cell(1, 4).Value = "Level";
        importSheet.Cell(1, 5).Value = "Department Name";
        importSheet.Cell(1, 6).Value = "Status";
        importSheet.Cell(1, 7).Value = "Sort Order";

        // Style headers
        var headerRange = importSheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Reference data
        referenceSheet.Cell(1, 1).Value = "Level";
        referenceSheet.Cell(1, 1).Style.Font.Bold = true;
        for (int i = 0; i < levels.Length; i++)
        {
            referenceSheet.Cell(i + 2, 1).Value = levels[i];
        }

        referenceSheet.Cell(1, 2).Value = "Department";
        referenceSheet.Cell(1, 2).Style.Font.Bold = true;
        for (int i = 0; i < departments.Count; i++)
        {
            referenceSheet.Cell(i + 2, 2).Value = departments[i];
        }

        referenceSheet.Cell(1, 3).Value = "Status";
        referenceSheet.Cell(1, 3).Style.Font.Bold = true;
        referenceSheet.Cell(2, 3).Value = "Active";
        referenceSheet.Cell(3, 3).Value = "Inactive";

        workbook.NamedRanges.Add("Levels", referenceSheet.Range(2, 1, levels.Length + 1, 1));
        if (departments.Any())
            workbook.NamedRanges.Add("Departments", referenceSheet.Range(2, 2, departments.Count + 1, 2));
        workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 3, 3, 3));

        // Add data validation
        var levelValidation = importSheet.Range("D2:D1000").SetDataValidation();
        levelValidation.List("=Levels", true);
        levelValidation.IgnoreBlanks = true;
        levelValidation.InCellDropdown = true;

        if (departments.Any())
        {
            var deptValidation = importSheet.Range("E2:E1000").SetDataValidation();
            deptValidation.List("=Departments", true);
            deptValidation.IgnoreBlanks = true;
            deptValidation.InCellDropdown = true;
        }

        var statusValidation = importSheet.Range("H2:H1000").SetDataValidation();
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

        return await _importExportService.StartExportJobAsync<PositionDto>(
            entityType: "Position",
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
                var departmentId = f.ContainsKey("departmentId") ? (Guid?)f["departmentId"] : null;
                var createdFrom = f.ContainsKey("createdFrom") ? (DateTime?)f["createdFrom"] : null;
                var createdTo = f.ContainsKey("createdTo") ? (DateTime?)f["createdTo"] : null;
                var selectedIds = f.ContainsKey("selectedIds") ? (List<Guid>?)f["selectedIds"] : null;

                var query = scopedUnitOfWork.Repository<Position>().GetQueryable()
                    .Where(p => p.OrganizationId == organizationId && !p.IsDeleted);

                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(p =>
                        p.Name.ToLower().Contains(searchLower) ||
                        (p.Description != null && p.Description.ToLower().Contains(searchLower)) ||
                        (p.Code != null && p.Code.ToLower().Contains(searchLower)) ||
                        (p.Level != null && p.Level.ToLower().Contains(searchLower)));
                }

                if (isActive.HasValue)
                    query = query.Where(p => p.IsActive == isActive.Value);

                if (departmentId.HasValue)
                    query = query.Where(p => p.DepartmentId == departmentId.Value);

                if (createdFrom.HasValue)
                    query = query.Where(p => p.CreatedAtUtc >= createdFrom.Value);

                if (createdTo.HasValue)
                    query = query.Where(p => p.CreatedAtUtc <= createdTo.Value);

                if (selectedIds != null && selectedIds.Any())
                    query = query.Where(p => selectedIds.Contains(p.Id));

                var positions = await query
                    .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
                    .Select(p => new PositionDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Code = p.Code,
                        Level = p.Level,
                        DepartmentId = p.DepartmentId,
                        DepartmentName = p.DepartmentName,
                        IsActive = p.IsActive,
                        SortOrder = p.SortOrder,
                        CreatedAtUtc = p.CreatedAtUtc,
                        LastModifiedAtUtc = p.ModifiedAtUtc ?? p.CreatedAtUtc
                    })
                    .ToListAsync();

                return positions;
            },
            filters: filters,
            columnMapper: (p) => new Dictionary<string, object>
            {
                ["Name"] = p.Name ?? "",
                ["Code"] = p.Code ?? "",
                ["Description"] = p.Description ?? "",
                ["Level"] = p.Level ?? "",
                ["Department Name"] = p.DepartmentName ?? "",
                ["Status"] = p.IsActive ? "Active" : "Inactive",
                ["Sort Order"] = p.SortOrder.ToString(),
                ["Created At"] = p.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                ["Last Modified"] = p.LastModifiedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
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
        var departmentRepo = _unitOfWork.Repository<Department>();

        return await _importExportService.StartImportJobAsync<CreatePositionDto>(
            entityType: "Position",
            fileStream: fileStream,
            fileName: fileName,
            rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
            {
                if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
                    return (false, "Name is required", false, false);

                var repo = scopedUnitOfWork.Repository<Position>();
                var scopedDeptRepo = scopedUnitOfWork.Repository<Department>();

                var code = rowData.GetValueOrDefault("Code");
                var existingPosition = !string.IsNullOrWhiteSpace(code)
                    ? await repo.FindAsync(p => p.OrganizationId == OrganizationId && p.Code != null && p.Code.ToLower() == code.ToLower() && !p.IsDeleted)
                    : await repo.FindAsync(p => p.OrganizationId == OrganizationId && p.Name.ToLower() == name.ToLower() && !p.IsDeleted);

                Guid? departmentId = null;
                var departmentName = rowData.GetValueOrDefault("Department Name");
                if (!string.IsNullOrWhiteSpace(departmentName))
                {
                    var department = await scopedDeptRepo.FindAsync(d => 
                        d.OrganizationId == OrganizationId && 
                        d.Name == departmentName && 
                        !d.IsDeleted);
                    if (department != null)
                    {
                        departmentId = department.Id;
                    }
                }

                if (existingPosition != null)
                {
                    if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
                        return (true, null, false, true);

                    if (duplicateStrategy == DuplicateHandlingStrategy.Update)
                    {
                        existingPosition.Name = name;
                        existingPosition.Description = rowData.GetValueOrDefault("Description");
                        existingPosition.Code = code;
                        existingPosition.Level = rowData.GetValueOrDefault("Level");
                        existingPosition.DepartmentId = departmentId;
                        existingPosition.DepartmentName = departmentName;
                        if (rowData.TryGetValue("Status", out var status))
                            existingPosition.IsActive = status?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;
                        if (int.TryParse(rowData.GetValueOrDefault("Sort Order"), out var sortOrder))
                            existingPosition.SortOrder = sortOrder;
                        existingPosition.ModifiedAtUtc = DateTimeOffset.UtcNow;
                        existingPosition.ModifiedBy = userId.ToString();
                        repo.Update(existingPosition);
                        return (true, null, true, false);
                    }
                }

                var newPosition = new Position
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = OrganizationId,
                    Name = name,
                    Description = rowData.GetValueOrDefault("Description"),
                    Code = code,
                    Level = rowData.GetValueOrDefault("Level"),
                    DepartmentId = departmentId,
                    DepartmentName = departmentName,
                    IsActive = true,
                    SortOrder = 0,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = userId.ToString()
                };

                if (rowData.TryGetValue("Status", out var statusValue))
                    newPosition.IsActive = statusValue?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true;
                if (int.TryParse(rowData.GetValueOrDefault("Sort Order"), out var newSortOrder))
                    newPosition.SortOrder = newSortOrder;

                await repo.AddAsync(newPosition);
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
        return await _importExportService.GetHistoryAsync("Position", type, page, pageSize);
    }

    public async Task<PositionStatisticsDto> GetStatisticsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var repo = _unitOfWork.Repository<Position>();
        
        var allPositions = await repo.GetQueryable()
            .Where(p => p.OrganizationId == OrganizationId && !p.IsDeleted)
            .ToListAsync();

        return new PositionStatisticsDto
        {
            Total = allPositions.Count,
            Active = allPositions.Count(p => p.IsActive),
            Inactive = allPositions.Count(p => !p.IsActive)
        };
    }

    public async Task<PositionDropdownOptionsDto> GetDropdownOptionsAsync()
    {
        var OrganizationId = _tenantService.GetCurrentOrganizationId();
        var departmentRepo = _unitOfWork.Repository<Department>();
        
        var departments = await departmentRepo.GetQueryable()
            .Where(d => d.OrganizationId == OrganizationId && !d.IsDeleted && d.IsActive)
            .Select(d => new DepartmentDropdownDto
            {
                Id = d.Id,
                Name = d.Name,
                IsActive = d.IsActive
            })
            .ToListAsync();

        var levels = new[] { "Junior", "Mid", "Senior", "Lead", "Manager", "Director", "Executive" };

        return new PositionDropdownOptionsDto
        {
            Levels = levels.ToList(),
            Departments = departments
        };
    }

    public async Task<byte[]?> GetImportErrorReportAsync(string errorReportId)
    {
        return await _importExportService.GetImportErrorReportAsync(errorReportId);
    }

    private IQueryable<Position> ApplySorting(IQueryable<Position> query, string? sortField, string? sortDirection)
    {
        sortField = sortField?.ToLower() ?? "createdatutc";
        sortDirection = sortDirection?.ToLower() ?? "desc";

        return sortField switch
        {
            "name" => sortDirection == "asc" ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
            "code" => sortDirection == "asc" ? query.OrderBy(p => p.Code) : query.OrderByDescending(p => p.Code),
            "level" => sortDirection == "asc" ? query.OrderBy(p => p.Level) : query.OrderByDescending(p => p.Level),
            "departmentname" => sortDirection == "asc" ? query.OrderBy(p => p.DepartmentName) : query.OrderByDescending(p => p.DepartmentName),
            "sortorder" => sortDirection == "asc" ? query.OrderBy(p => p.SortOrder) : query.OrderByDescending(p => p.SortOrder),
            "isactive" => sortDirection == "asc" ? query.OrderBy(p => p.IsActive) : query.OrderByDescending(p => p.IsActive),
            _ => sortDirection == "asc" ? query.OrderBy(p => p.CreatedAtUtc) : query.OrderByDescending(p => p.CreatedAtUtc)
        };
    }

    private async Task InvalidatePositionCachesAsync(Guid organizationId)
    {
        // Clear all list cache variations (all pages, filters, sorts)
        // Pattern with wildcard matches: positions:list:{orgId}:1:10:null:null:null:...
        await _cacheService.RemoveCacheByPatternAsync($"positions:list:{organizationId}:*");
        // Pattern without wildcard matches any key starting with this prefix (redundant but safe)
        await _cacheService.RemoveCacheByPatternAsync($"positions:list:{organizationId}");
        // Clear dropdown and stats caches
        await _cacheService.RemoveCacheByPatternAsync($"positions:dropdown:{organizationId}");
        await _cacheService.RemoveCacheByPatternAsync($"positions:stats:{organizationId}");
        // Also clear detail cache if exists
        await _cacheService.RemoveCacheByPatternAsync($"position:detail:*");
    }

    private static PositionDto MapToDto(Position position)
    {
        return new PositionDto
        {
            Id = position.Id,
            Name = position.Name,
            Description = position.Description,
            Code = position.Code,
            Level = position.Level,
            DepartmentId = position.DepartmentId,
            DepartmentName = position.DepartmentName,
            IsActive = position.IsActive,
            SortOrder = position.SortOrder,
            CreatedAtUtc = position.CreatedAtUtc,
            LastModifiedAtUtc = position.ModifiedAtUtc ?? position.CreatedAtUtc,
            OrganizationId = position.OrganizationId,
            OrganizationName = null // Will be populated separately if needed
        };
    }
}
