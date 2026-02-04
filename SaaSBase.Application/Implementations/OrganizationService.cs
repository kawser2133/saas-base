using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SaaSBase.Application.DTOs;
using SaaSBase.Application.Services;
using SaaSBase.Application;
using SaaSBase.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;
using System.IO;

namespace SaaSBase.Application.Implementations;

public class OrganizationService : IOrganizationService
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICurrentTenantService _tenantService;
	private readonly IFileService _fileService;
	private readonly IImportExportService _importExportService;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly IUserContextService _userContextService;
	private readonly ICacheService _cacheService;

	public OrganizationService(IUnitOfWork unitOfWork, ICurrentTenantService tenantService, IFileService fileService, IImportExportService importExportService, IServiceScopeFactory serviceScopeFactory, IUserContextService userContextService, ICacheService cacheService)
	{
		_unitOfWork = unitOfWork;
		_tenantService = tenantService;
		_fileService = fileService;
		_importExportService = importExportService;
		_serviceScopeFactory = serviceScopeFactory;
		_userContextService = userContextService;
		_cacheService = cacheService;
	}

	public async Task<List<OrganizationDto>> GetOrganizationsAsync()
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		var organizations = await organizationRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);

		var locationRepo = _unitOfWork.Repository<Location>();
		return organizations.Select(x => new OrganizationDto
		{
			Id = x.Id,
			Name = x.Name,
			Description = x.Description,
			LogoUrl = x.LogoUrl,
			Email = x.Email,
			Phone = x.Phone,
			PrimaryColor = x.PrimaryColor,
			SecondaryColor = x.SecondaryColor,
			IsActive = x.IsActive,
			LocationCount = locationRepo.CountAsync(l => l.OrganizationId == x.Id && l.OrganizationId == OrganizationId && !l.IsDeleted).GetAwaiter().GetResult()
		}).ToList();
	}

	public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid id)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		
		// For organizations, OrganizationId should equal Id (organization is its own tenant)
		// For company admin: always return their own organization (ignore id parameter)
		// For system admin: can access any organization
		Organization? organization;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin)
		{
			// System admin can view any organization - ignore tenant filter
			organization = await organizationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
		}
		else
		{
			// Company admin can only view their own organization
			// Always use currentUserOrgId regardless of id parameter
			organization = await organizationRepo.FindAsync(x => x.Id == currentUserOrgId && !x.IsDeleted);
		}

		if (organization == null) return null;

		var locationRepo = _unitOfWork.Repository<Location>();
		var viewedOrgId = organization.Id;
		return new OrganizationDto
		{
			Id = organization.Id,
			Name = organization.Name,
			Description = organization.Description,
			LogoUrl = organization.LogoUrl,
			Website = organization.Website,
			Email = organization.Email,
			Phone = organization.Phone,
			Address = organization.Address,
			City = organization.City,
			State = organization.State,
			Country = organization.Country,
			PostalCode = organization.PostalCode,
			TaxId = organization.TaxId,
			RegistrationNumber = organization.RegistrationNumber,
			PrimaryColor = organization.PrimaryColor,
			SecondaryColor = organization.SecondaryColor,
			IsActive = organization.IsActive,
			LocationCount = await locationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.CountAsync(l => l.OrganizationId == viewedOrgId && !l.IsDeleted)
		};
	}

	public async Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		
		var organization = new Organization
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			Name = dto.Name,
			Description = dto.Description,
			Email = dto.Email,
			Phone = dto.Phone,
			Address = dto.Address,
			City = dto.City,
			State = dto.State,
			Country = dto.Country,
			PostalCode = dto.PostalCode,
			TaxId = dto.TaxId,
			RegistrationNumber = dto.RegistrationNumber,
			IsActive = true
		};

		await organizationRepo.AddAsync(organization);
		await _unitOfWork.SaveChangesAsync();

		// Invalidate organization caches
		await InvalidateOrganizationCachesAsync(organization.Id);

		return await GetOrganizationByIdAsync(organization.Id) ?? throw new InvalidOperationException("Failed to create organization");
	}

	public async Task<OrganizationDto> UpdateOrganizationAsync(Guid id, UpdateOrganizationDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		var organization = await organizationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (organization == null) throw new ArgumentException("Organization not found");

		organization.Name = dto.Name;
		organization.Description = dto.Description;
		organization.Website = dto.Website;
		organization.Email = dto.Email;
		organization.Phone = dto.Phone;
		organization.Address = dto.Address;
		organization.City = dto.City;
		organization.State = dto.State;
		organization.Country = dto.Country;
		organization.PostalCode = dto.PostalCode;
		organization.TaxId = dto.TaxId;
		organization.RegistrationNumber = dto.RegistrationNumber;
		organization.PrimaryColor = dto.PrimaryColor;
		organization.SecondaryColor = dto.SecondaryColor;
		if (dto.IsActive.HasValue)
		{
			organization.IsActive = dto.IsActive.Value;
		}

		organizationRepo.Update(organization);
		await _unitOfWork.SaveChangesAsync();

		// Invalidate organization caches
		await InvalidateOrganizationCachesAsync(organization.Id);

		return await GetOrganizationByIdAsync(organization.Id) ?? throw new InvalidOperationException("Failed to update organization");
	}

	public async Task<bool> DeleteOrganizationAsync(Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		var organization = await organizationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (organization == null) return false;

		organization.IsDeleted = true;
		organization.DeletedAtUtc = DateTimeOffset.UtcNow;
		organization.DeletedBy = currentUserId.ToString();

		organizationRepo.Update(organization);
		await _unitOfWork.SaveChangesAsync();

		// Invalidate organization caches
		await InvalidateOrganizationCachesAsync(organization.Id);

		return true;
	}

	public async Task<DTOs.OrganizationSummaryDto> GetOrganizationSummaryAsync(Guid organizationId)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		
		// For organizations, OrganizationId should equal Id (organization is its own tenant)
		// For company admin: organizationId should match their tenant
		// For system admin: can access any organization
		Organization? organization;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin)
		{
			// System admin can view any organization - ignore tenant filter
			organization = await organizationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.FirstOrDefaultAsync(x => x.Id == organizationId && !x.IsDeleted);
		}
		else
		{
			// Company admin can only view their own organization
			// For organizations, Id should equal OrganizationId (organization is its own tenant)
			organization = await organizationRepo.FindAsync(x => x.Id == organizationId && x.Id == currentUserOrgId && !x.IsDeleted);
		}

		if (organization == null) throw new ArgumentException("Organization not found");

		// Use the viewed organization's ID for counts (not current user's tenant)
		var viewedOrgId = organization.Id;
		var locationRepo = _unitOfWork.Repository<Location>();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		var taxRepo = _unitOfWork.Repository<TaxRate>();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		
		return new DTOs.OrganizationSummaryDto
		{
			Id = organization.Id,
			Name = organization.Name,
			LogoUrl = organization.LogoUrl,
			LocationCount = await locationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.CountAsync(l => l.OrganizationId == viewedOrgId && !l.IsDeleted),
			ActiveLocationCount = await locationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.CountAsync(l => l.OrganizationId == viewedOrgId && !l.IsDeleted && l.IsActive),
			CurrencyCount = await currencyRepo.GetQueryable()
				.IgnoreQueryFilters()
				.CountAsync(c => c.OrganizationId == viewedOrgId && !c.IsDeleted),
			TaxRateCount = await taxRepo.GetQueryable()
				.IgnoreQueryFilters()
				.CountAsync(t => t.OrganizationId == viewedOrgId && !t.IsDeleted),
			IntegrationCount = await integrationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.CountAsync(i => i.OrganizationId == viewedOrgId && !i.IsDeleted),
			ActiveIntegrationCount = await integrationRepo.GetQueryable()
				.IgnoreQueryFilters()
				.CountAsync(i => i.OrganizationId == viewedOrgId && !i.IsDeleted && i.IsActive)
		};
	}

	public async Task<bool> UploadLogoAsync(Guid organizationId, UploadLogoDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		var organization = await organizationRepo.FindAsync(x => x.Id == organizationId && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (organization == null) return false;

		try
		{
			// Delete old logo if exists
			if (!string.IsNullOrEmpty(organization.LogoUrl))
			{
				await _fileService.DeleteFileAsync(organization.LogoUrl);
			}

			// Save new file
			var folderPath = $"logos/{organizationId}";
			var savedFilePath = await _fileService.SaveFileAsync(dto.FileData, dto.FileName, folderPath);

			// Update organization with new logo path
			organization.LogoUrl = savedFilePath;

			organizationRepo.Update(organization);
			await _unitOfWork.SaveChangesAsync();
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public async Task<bool> RemoveLogoAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var organizationRepo = _unitOfWork.Repository<Organization>();
		var organization = await organizationRepo.FindAsync(x => x.Id == organizationId && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (organization == null) return false;

		try
		{
			// Delete file if exists
			if (!string.IsNullOrEmpty(organization.LogoUrl))
			{
				await _fileService.DeleteFileAsync(organization.LogoUrl);
			}

			// Clear logo URL
			organization.LogoUrl = null;

			organizationRepo.Update(organization);
			await _unitOfWork.SaveChangesAsync();
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	// Location Management
	public async Task<List<LocationDto>> GetLocationsAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		var locations = await locationRepo.FindManyAsync(x => x.OrganizationId == organizationId && x.OrganizationId == OrganizationId && !x.IsDeleted);

		return locations.Select(x => new LocationDto
		{
			Id = x.Id,
			OrganizationId = x.OrganizationId,
			Name = x.Name,
			Description = x.Description,
			Address = x.Address,
			City = x.City,
			State = x.State,
			Country = x.Country,
			PostalCode = x.PostalCode,
			Phone = x.Phone,
			Email = x.Email,
			ManagerName = x.ManagerName,
			IsActive = x.IsActive,
			IsWarehouse = x.IsWarehouse,
			IsRetail = x.IsRetail,
			IsOffice = x.IsOffice,
			Latitude = x.Latitude,
			Longitude = x.Longitude,
			TimeZone = x.TimeZone,
			Currency = x.Currency,
			Language = x.Language,
			LocationCode = x.LocationCode,
			LocationType = x.LocationType,
			ParentLocationId = x.ParentLocationId,
			Level = x.Level,
			SortOrder = x.SortOrder,
			IsDefault = x.IsDefault
		}).ToList();
	}

	public async Task<PagedResultDto<LocationDto>> GetLocationsPagedAsync(string? search, bool? isActive, string? country, string? city, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		
		// Determine which organization to query
		Guid filterOrganizationId;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin viewing another organization
			filterOrganizationId = targetOrganizationId.Value;
		}
		else
		{
			// Use current user's organization
			filterOrganizationId = currentUserOrgId;
		}
		
		// Build query with filters
		var query = locationRepo.GetQueryable();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin can view any organization - ignore tenant filter
			query = query.IgnoreQueryFilters()
				.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}
		else
		{
			// Regular users see only their organization
			query = query.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}

		// Apply search filter
		if (!string.IsNullOrEmpty(search))
		{
			var searchLower = search.ToLower();
			query = query.Where(x =>
				(x.Name != null && x.Name.ToLower().Contains(searchLower)) ||
				(x.Address != null && x.Address.ToLower().Contains(searchLower)) ||
				(x.City != null && x.City.ToLower().Contains(searchLower)) ||
				(x.Country != null && x.Country.ToLower().Contains(searchLower)) ||
				(x.Email != null && x.Email.ToLower().Contains(searchLower)));
		}

		// Apply filters
		if (isActive.HasValue)
			query = query.Where(x => x.IsActive == isActive.Value);

		if (!string.IsNullOrEmpty(country))
			query = query.Where(x => x.Country == country);

		if (!string.IsNullOrEmpty(city))
			query = query.Where(x => x.City == city);

		if (createdFrom.HasValue)
			query = query.Where(x => x.CreatedAtUtc >= createdFrom.Value);

		if (createdTo.HasValue)
			query = query.Where(x => x.CreatedAtUtc <= createdTo.Value);

		// Get total count
		var totalCount = await query.CountAsync();

		// Apply sorting
		query = sortField?.ToLower() switch
		{
			"name" => sortDirection == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
			"address" => sortDirection == "asc" ? query.OrderBy(x => x.Address) : query.OrderByDescending(x => x.Address),
			"city" => sortDirection == "asc" ? query.OrderBy(x => x.City) : query.OrderByDescending(x => x.City),
			"country" => sortDirection == "asc" ? query.OrderBy(x => x.Country) : query.OrderByDescending(x => x.Country),
			"isactive" => sortDirection == "asc" ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
			_ => sortDirection == "asc" ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc)
		};

		// Apply pagination
		var locations = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new LocationDto
			{
				Id = x.Id,
				OrganizationId = x.OrganizationId,
				Name = x.Name,
				Description = x.Description,
				Address = x.Address,
				City = x.City,
				State = x.State,
				Country = x.Country,
				PostalCode = x.PostalCode,
				Phone = x.Phone,
				Email = x.Email,
				ManagerName = x.ManagerName,
				IsActive = x.IsActive,
				IsWarehouse = x.IsWarehouse,
				IsRetail = x.IsRetail,
				IsOffice = x.IsOffice,
				Latitude = x.Latitude,
				Longitude = x.Longitude,
				TimeZone = x.TimeZone,
				Currency = x.Currency,
				Language = x.Language,
				LocationCode = x.LocationCode,
				LocationType = x.LocationType,
				ParentLocationId = x.ParentLocationId,
				Level = x.Level,
				SortOrder = x.SortOrder,
				IsDefault = x.IsDefault
			})
			.ToListAsync();

		return new PagedResultDto<LocationDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = locations
		};
	}

	public async Task<LocationDto?> GetLocationByIdAsync(Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		var location = await locationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (location == null) return null;

		// Parse User IDs
		Guid? createdById = null;
		Guid? modifiedById = null;
		if (!string.IsNullOrEmpty(location.CreatedBy) && Guid.TryParse(location.CreatedBy, out var createdId))
			createdById = createdId;
		if (!string.IsNullOrEmpty(location.ModifiedBy) && Guid.TryParse(location.ModifiedBy, out var modifiedId))
			modifiedById = modifiedId;

		// Resolve user names
		var userIds = new List<Guid>();
		if (createdById.HasValue) userIds.Add(createdById.Value);
		if (modifiedById.HasValue && modifiedById != createdById) userIds.Add(modifiedById.Value);

		var userNames = userIds.Any() ? await ResolveUserNamesAsync(userIds, OrganizationId) : new Dictionary<Guid, string>();

		var createdByName = createdById.HasValue && userNames.TryGetValue(createdById.Value, out var cName) ? cName : null;
		var modifiedByName = modifiedById.HasValue && userNames.TryGetValue(modifiedById.Value, out var mName) ? mName : null;

		return new LocationDto
		{
			Id = location.Id,
			OrganizationId = location.OrganizationId,
			Name = location.Name,
			Description = location.Description,
			Address = location.Address,
			City = location.City,
			State = location.State,
			Country = location.Country,
			PostalCode = location.PostalCode,
			Phone = location.Phone,
			Email = location.Email,
			ManagerName = location.ManagerName,
			IsActive = location.IsActive,
			IsWarehouse = location.IsWarehouse,
			IsRetail = location.IsRetail,
			IsOffice = location.IsOffice,
			Latitude = location.Latitude,
			Longitude = location.Longitude,
			TimeZone = location.TimeZone,
			Currency = location.Currency,
			Language = location.Language,
			LocationCode = location.LocationCode,
			LocationType = location.LocationType,
			ParentLocationId = location.ParentLocationId,
			Level = location.Level,
			SortOrder = location.SortOrder,
			IsDefault = location.IsDefault,
			CreatedAtUtc = location.CreatedAtUtc,
			ModifiedAtUtc = location.ModifiedAtUtc,
			CreatedBy = createdByName ?? location.CreatedBy, // Use resolved name or fallback to ID
			ModifiedBy = modifiedByName ?? location.ModifiedBy, // Use resolved name or fallback to ID
			CreatedById = createdById,
			CreatedByName = createdByName,
			ModifiedById = modifiedById,
			ModifiedByName = modifiedByName
		};
	}

	public async Task<LocationDto> CreateLocationAsync(Guid organizationId, CreateLocationDto dto)
	{
		// Validate required fields
		if (string.IsNullOrWhiteSpace(dto.Phone))
			throw new ArgumentException("Phone is required");
		if (string.IsNullOrWhiteSpace(dto.Email))
			throw new ArgumentException("Email is required");
		if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
			throw new ArgumentException("Email format is invalid");
		if (string.IsNullOrWhiteSpace(dto.LocationType))
			throw new ArgumentException("Location type is required");

		var OrganizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		
		// Calculate level based on parent location
		int level = 0;
		if (dto.ParentLocationId.HasValue)
		{
			var parentLocation = await locationRepo.FindAsync(l => l.Id == dto.ParentLocationId.Value && l.OrganizationId == OrganizationId && !l.IsDeleted);
			if (parentLocation != null)
			{
				level = parentLocation.Level + 1;
			}
		}
		
		// If this location is being set as default, unset all other default locations
		if (dto.IsDefault)
		{
			var existingDefaultLocations = await locationRepo.FindManyAsync(l => l.OrganizationId == OrganizationId && l.IsDefault && !l.IsDeleted);
			foreach (var existingDefault in existingDefaultLocations)
			{
				existingDefault.IsDefault = false;
				locationRepo.Update(existingDefault);
			}
		}

		var currentUserId = _tenantService.GetCurrentUserId();
		var location = new Location
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			Name = dto.Name,
			Description = dto.Description,
			Address = dto.Address,
			City = dto.City,
			State = dto.State,
			Country = dto.Country,
			PostalCode = dto.PostalCode,
			Phone = dto.Phone,
			Email = dto.Email,
			ManagerName = dto.ManagerName,
			IsActive = dto.IsActive,
			IsWarehouse = dto.IsWarehouse,
			IsRetail = dto.IsRetail,
			IsOffice = dto.IsOffice,
			Latitude = dto.Latitude,
			Longitude = dto.Longitude,
			TimeZone = dto.TimeZone,
			Currency = dto.Currency,
			Language = dto.Language,
			LocationCode = dto.LocationCode,
			LocationType = dto.LocationType,
			ParentLocationId = dto.ParentLocationId,
			Level = level,
			SortOrder = 0,
			IsDefault = dto.IsDefault,
			CreatedBy = currentUserId.ToString(),
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		await locationRepo.AddAsync(location);
		await _unitOfWork.SaveChangesAsync();

		// Invalidate location caches
		await InvalidateLocationCachesAsync(OrganizationId);

		return await GetLocationByIdAsync(location.Id) ?? throw new InvalidOperationException("Failed to create location");
	}

	public async Task<LocationDto> UpdateLocationAsync(Guid id, UpdateLocationDto dto)
	{
		// Validate required fields
		if (string.IsNullOrWhiteSpace(dto.Phone))
			throw new ArgumentException("Phone is required");
		if (string.IsNullOrWhiteSpace(dto.Email))
			throw new ArgumentException("Email is required");
		if (!System.Text.RegularExpressions.Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
			throw new ArgumentException("Email format is invalid");
		if (string.IsNullOrWhiteSpace(dto.LocationType))
			throw new ArgumentException("Location type is required");

		var OrganizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		var location = await locationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (location == null) throw new ArgumentException("Location not found");

		// Calculate level based on parent location (if parent changed)
		int level = location.Level;
		if (location.ParentLocationId != dto.ParentLocationId)
		{
			if (dto.ParentLocationId.HasValue)
			{
				var parentLocation = await locationRepo.FindAsync(l => l.Id == dto.ParentLocationId.Value && l.OrganizationId == OrganizationId && !l.IsDeleted);
				if (parentLocation != null)
				{
					level = parentLocation.Level + 1;
				}
			}
			else
			{
				level = 0;
			}
		}

		location.Name = dto.Name;
		location.Description = dto.Description;
		location.Address = dto.Address;
		location.City = dto.City;
		location.State = dto.State;
		location.Country = dto.Country;
		location.PostalCode = dto.PostalCode;
		location.Phone = dto.Phone;
		location.Email = dto.Email;
		location.ManagerName = dto.ManagerName;
		location.IsActive = dto.IsActive;
		location.IsWarehouse = dto.IsWarehouse;
		location.IsRetail = dto.IsRetail;
		location.IsOffice = dto.IsOffice;
		location.Latitude = dto.Latitude;
		location.Longitude = dto.Longitude;
		location.TimeZone = dto.TimeZone;
		location.Currency = dto.Currency;
		location.Language = dto.Language;
		location.LocationCode = dto.LocationCode;
		location.LocationType = dto.LocationType;
		location.ParentLocationId = dto.ParentLocationId;
		location.Level = level;
		
		// If this location is being set as default, unset all other default locations
		if (dto.IsDefault && !location.IsDefault)
		{
			var existingDefaultLocations = await locationRepo.FindManyAsync(l => l.OrganizationId == OrganizationId && l.Id != id && l.IsDefault && !l.IsDeleted);
			foreach (var existingDefault in existingDefaultLocations)
			{
				existingDefault.IsDefault = false;
				locationRepo.Update(existingDefault);
			}
		}
		
		var currentUserId = _tenantService.GetCurrentUserId();
		location.IsDefault = dto.IsDefault;
		location.ModifiedBy = currentUserId.ToString();
		location.ModifiedAtUtc = DateTimeOffset.UtcNow;

		locationRepo.Update(location);
		await _unitOfWork.SaveChangesAsync();

		// Invalidate location caches
		await InvalidateLocationCachesAsync(OrganizationId);

		return await GetLocationByIdAsync(location.Id) ?? throw new InvalidOperationException("Failed to update location");
	}

	public async Task<bool> DeleteLocationAsync(Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var locationRepo = _unitOfWork.Repository<Location>();
		var location = await locationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (location == null) return false;

		location.IsDeleted = true;
		location.DeletedAtUtc = DateTimeOffset.UtcNow;
		location.DeletedBy = currentUserId.ToString();

		locationRepo.Update(location);
		await _unitOfWork.SaveChangesAsync();

		// Invalidate location caches
		await InvalidateLocationCachesAsync(OrganizationId);

		return true;
	}

	public async Task<List<LocationHierarchyDto>> GetLocationHierarchyAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		var locations = await locationRepo.FindManyAsync(x => x.OrganizationId == organizationId && x.OrganizationId == OrganizationId && !x.IsDeleted);

		var rootLocations = locations.Where(l => l.ParentId == null).OrderBy(l => l.SortOrder).ToList();
		var result = new List<LocationHierarchyDto>();

		foreach (var root in rootLocations)
		{
			var locationDto = MapToLocationHierarchyDto(root);
			locationDto.Children = BuildLocationHierarchy(root.Id, locations);
			result.Add(locationDto);
		}

		return result;
	}

	// Business Settings Management
	public async Task<List<BusinessSettingDto>> GetBusinessSettingsAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		var settings = await settingRepo.FindManyAsync(x => x.OrganizationId == organizationId && x.OrganizationId == OrganizationId && !x.IsDeleted);

		return settings.Select(x => new BusinessSettingDto
		{
			Id = x.Id,
			OrganizationId = x.OrganizationId,
			SettingKey = x.SettingKey,
			SettingValue = x.SettingValue,
			SettingType = x.SettingType,
			Description = x.Description,
			IsActive = x.IsActive
		}).ToList();
	}

	public async Task<BusinessSettingDto> CreateBusinessSettingAsync(Guid organizationId, CreateBusinessSettingDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		
		// Check for duplicate setting key (case-insensitive)
		if (!string.IsNullOrWhiteSpace(dto.SettingKey))
		{
			var settingKeyLower = dto.SettingKey.ToLower();
			var existingSetting = await settingRepo.FindAsync(s => 
				s.OrganizationId == OrganizationId && 
				s.SettingKey != null &&
				s.SettingKey.ToLower() == settingKeyLower && 
				!s.IsDeleted);
			
			if (existingSetting != null)
			{
				throw new InvalidOperationException("A business setting with this key already exists");
			}
		}
		
		var currentUserId = _tenantService.GetCurrentUserId();
		var setting = new BusinessSetting
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			SettingKey = dto.SettingKey,
			SettingValue = dto.SettingValue,
			SettingType = dto.SettingType,
			Description = dto.Description,
			IsActive = dto.IsActive,
			CreatedBy = currentUserId.ToString(),
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		await settingRepo.AddAsync(setting);
		await _unitOfWork.SaveChangesAsync();

		return await GetBusinessSettingByIdAsync(organizationId, setting.Id) ?? throw new InvalidOperationException("Failed to create business setting");
	}

	public async Task<BusinessSettingDto?> GetBusinessSettingByIdAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		var setting = await settingRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (setting == null) return null;

		// Parse User IDs
		Guid? createdById = null;
		Guid? modifiedById = null;
		if (!string.IsNullOrEmpty(setting.CreatedBy) && Guid.TryParse(setting.CreatedBy, out var createdId))
			createdById = createdId;
		if (!string.IsNullOrEmpty(setting.ModifiedBy) && Guid.TryParse(setting.ModifiedBy, out var modifiedId))
			modifiedById = modifiedId;

		// Resolve user names
		var userIds = new List<Guid>();
		if (createdById.HasValue) userIds.Add(createdById.Value);
		if (modifiedById.HasValue && modifiedById != createdById) userIds.Add(modifiedById.Value);

		var userNames = userIds.Any() ? await ResolveUserNamesAsync(userIds, OrganizationId) : new Dictionary<Guid, string>();

		var createdByName = createdById.HasValue && userNames.TryGetValue(createdById.Value, out var cName) ? cName : null;
		var modifiedByName = modifiedById.HasValue && userNames.TryGetValue(modifiedById.Value, out var mName) ? mName : null;

		return new BusinessSettingDto
		{
			Id = setting.Id,
			OrganizationId = setting.OrganizationId,
			SettingKey = setting.SettingKey,
			SettingValue = setting.SettingValue,
			SettingType = setting.SettingType,
			Description = setting.Description,
			IsActive = setting.IsActive,
			CreatedAtUtc = setting.CreatedAtUtc,
			ModifiedAtUtc = setting.ModifiedAtUtc,
			CreatedBy = createdByName ?? setting.CreatedBy, // Use resolved name or fallback to ID
			ModifiedBy = modifiedByName ?? setting.ModifiedBy, // Use resolved name or fallback to ID
			CreatedById = createdById,
			CreatedByName = createdByName,
			ModifiedById = modifiedById,
			ModifiedByName = modifiedByName
		};
	}

	public async Task<BusinessSettingDto> UpdateBusinessSettingAsync(Guid organizationId, Guid id, CreateBusinessSettingDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		var setting = await settingRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (setting == null) throw new ArgumentException("Business setting not found");

		// Check for duplicate setting key (case-insensitive) - exclude current setting
		if (!string.IsNullOrWhiteSpace(dto.SettingKey))
		{
			var settingKeyLower = dto.SettingKey.ToLower();
			var existingSetting = await settingRepo.FindAsync(s => 
				s.OrganizationId == OrganizationId && 
				s.Id != id &&
				s.SettingKey != null &&
				s.SettingKey.ToLower() == settingKeyLower && 
				!s.IsDeleted);
			
			if (existingSetting != null)
			{
				throw new InvalidOperationException("A business setting with this key already exists");
			}
		}

		var currentUserId = _tenantService.GetCurrentUserId();
		setting.SettingKey = dto.SettingKey;
		setting.SettingValue = dto.SettingValue;
		setting.SettingType = dto.SettingType;
		setting.Description = dto.Description;
		setting.IsActive = dto.IsActive;
		setting.ModifiedBy = currentUserId.ToString();
		setting.ModifiedAtUtc = DateTimeOffset.UtcNow;

		settingRepo.Update(setting);
		await _unitOfWork.SaveChangesAsync();

		return await GetBusinessSettingByIdAsync(organizationId, setting.Id) ?? throw new InvalidOperationException("Failed to update business setting");
	}

	public async Task<bool> DeleteBusinessSettingAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		var setting = await settingRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (setting == null) return false;

		setting.IsDeleted = true;
		setting.DeletedAtUtc = DateTimeOffset.UtcNow;
		setting.DeletedBy = currentUserId.ToString();

		settingRepo.Update(setting);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<List<BusinessSettingDto>> BulkCloneBusinessSettingsAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return new List<BusinessSettingDto>();

		var organizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		
		// Get original settings
		var originalSettings = await settingRepo.GetQueryable()
			.Where(s => ids.Contains(s.Id) && s.OrganizationId == organizationId && !s.IsDeleted)
			.ToListAsync();

		if (!originalSettings.Any())
			return new List<BusinessSettingDto>();

		var clonedSettings = new List<BusinessSettingDto>();
		var generatedKeys = new HashSet<string>();
		var clonedSettingEntities = new List<BusinessSetting>();

		// Get all existing setting keys from database to avoid conflicts
		var existingKeys = await settingRepo.GetQueryable()
			.Where(s => s.OrganizationId == organizationId && !s.IsDeleted)
			.Select(s => s.Key)
			.ToListAsync();
		
		generatedKeys.UnionWith(existingKeys);

		foreach (var originalSetting in originalSettings)
		{
			// Generate unique key with GUID to ensure uniqueness
			var baseKey = originalSetting.Key;
			var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
			var newKey = $"{baseKey}_Copy_{uniqueSuffix}";
			var counter = 1;
			
			// Check if key already exists in current batch (includes database keys)
			while (generatedKeys.Contains(newKey))
			{
				uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
				newKey = $"{baseKey}_Copy_{uniqueSuffix}";
				counter++;
				if (counter > 100) break; // Safety limit
			}
			
			generatedKeys.Add(newKey);

			// Create cloned setting
			var clonedSetting = new BusinessSetting
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Key = newKey,
				Value = originalSetting.Value,
				Description = originalSetting.Description,
				SettingKey = originalSetting.SettingKey,
				SettingValue = originalSetting.SettingValue,
				SettingType = originalSetting.SettingType,
				DataType = originalSetting.DataType,
				IsActive = originalSetting.IsActive,
				CreatedBy = currentUserId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			clonedSettingEntities.Add(clonedSetting);
		}

		// Add all setting entities at once
		foreach (var clonedSetting in clonedSettingEntities)
		{
			await settingRepo.AddAsync(clonedSetting);
		}

		// Save all cloned settings in a single transaction
		await _unitOfWork.SaveChangesAsync();

		// Build DTOs after saving
		foreach (var clonedSetting in clonedSettingEntities)
		{
			clonedSettings.Add(new BusinessSettingDto
			{
				Id = clonedSetting.Id,
				OrganizationId = clonedSetting.OrganizationId,
				Key = clonedSetting.Key,
				Value = clonedSetting.Value,
				Description = clonedSetting.Description,
				SettingKey = clonedSetting.SettingKey,
				SettingValue = clonedSetting.SettingValue,
				SettingType = clonedSetting.SettingType,
				DataType = clonedSetting.DataType,
				IsActive = clonedSetting.IsActive
			});
		}

		return clonedSettings;
	}

	// Currency Management
	public async Task<List<CurrencyDto>> GetCurrenciesAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		var currencies = await currencyRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);

		return currencies.Select(x => new CurrencyDto
		{
			Id = x.Id,
			OrganizationId = x.OrganizationId,
			Code = x.Code,
			Name = x.Name,
			Symbol = x.Symbol,
			ExchangeRate = x.ExchangeRate,
			DecimalPlaces = x.DecimalPlaces,
			IsActive = x.IsActive,
			IsDefault = x.IsDefault
		}).ToList();
	}

	public async Task<CurrencyDto?> GetCurrencyByIdAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		var currency = await currencyRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (currency == null) return null;

		// Parse User IDs
		Guid? createdById = null;
		Guid? modifiedById = null;
		if (!string.IsNullOrEmpty(currency.CreatedBy) && Guid.TryParse(currency.CreatedBy, out var createdId))
			createdById = createdId;
		if (!string.IsNullOrEmpty(currency.ModifiedBy) && Guid.TryParse(currency.ModifiedBy, out var modifiedId))
			modifiedById = modifiedId;

		// Resolve user names
		var userIds = new List<Guid>();
		if (createdById.HasValue) userIds.Add(createdById.Value);
		if (modifiedById.HasValue && modifiedById != createdById) userIds.Add(modifiedById.Value);

		var userNames = userIds.Any() ? await ResolveUserNamesAsync(userIds, OrganizationId) : new Dictionary<Guid, string>();

		var createdByName = createdById.HasValue && userNames.TryGetValue(createdById.Value, out var cName) ? cName : null;
		var modifiedByName = modifiedById.HasValue && userNames.TryGetValue(modifiedById.Value, out var mName) ? mName : null;

		return new CurrencyDto
		{
			Id = currency.Id,
			OrganizationId = currency.OrganizationId,
			Code = currency.Code,
			Name = currency.Name,
			Symbol = currency.Symbol,
			ExchangeRate = currency.ExchangeRate,
			DecimalPlaces = currency.DecimalPlaces,
			IsActive = currency.IsActive,
			IsDefault = currency.IsDefault,
			CreatedAtUtc = currency.CreatedAtUtc,
			ModifiedAtUtc = currency.ModifiedAtUtc,
			CreatedBy = createdByName ?? currency.CreatedBy, // Use resolved name or fallback to ID
			ModifiedBy = modifiedByName ?? currency.ModifiedBy, // Use resolved name or fallback to ID
			CreatedById = createdById,
			CreatedByName = createdByName,
			ModifiedById = modifiedById,
			ModifiedByName = modifiedByName
		};
	}

	public async Task<CurrencyDto> CreateCurrencyAsync(Guid organizationId, CreateCurrencyDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		
		// If setting this currency as default, unset any existing default currency (including inactive)
		if (dto.IsDefault)
		{
			var existingDefaultCurrencies = await currencyRepo.FindManyAsync(x => 
				x.OrganizationId == OrganizationId && 
				x.IsDefault && 
				!x.IsDeleted);
			
			foreach (var existingDefault in existingDefaultCurrencies)
			{
				existingDefault.IsDefault = false;
				currencyRepo.Update(existingDefault);
			}
		}
		
		var currentUserId = _tenantService.GetCurrentUserId();
		var currency = new Currency
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			Code = dto.Code,
			Name = dto.Name,
			Symbol = dto.Symbol,
			ExchangeRate = dto.ExchangeRate,
			DecimalPlaces = dto.DecimalPlaces,
			IsActive = dto.IsActive,
			IsDefault = dto.IsDefault,
			CreatedBy = currentUserId.ToString(),
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		await currencyRepo.AddAsync(currency);
		await _unitOfWork.SaveChangesAsync();

		return await GetCurrencyByIdAsync(organizationId, currency.Id) ?? throw new InvalidOperationException("Failed to create currency");
	}

	public async Task<CurrencyDto> UpdateCurrencyAsync(Guid organizationId, Guid id, CreateCurrencyDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		var currency = await currencyRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (currency == null) throw new ArgumentException("Currency not found");

		// If setting this currency as default, unset any other existing default currency (including inactive)
		if (dto.IsDefault && !currency.IsDefault)
		{
			var existingDefaultCurrencies = await currencyRepo.FindManyAsync(x => 
				x.OrganizationId == OrganizationId && 
				x.Id != id &&
				x.IsDefault && 
				!x.IsDeleted);
			
			foreach (var existingDefault in existingDefaultCurrencies)
			{
				existingDefault.IsDefault = false;
				currencyRepo.Update(existingDefault);
			}
		}

		var currentUserId = _tenantService.GetCurrentUserId();
		currency.Code = dto.Code;
		currency.Name = dto.Name;
		currency.Symbol = dto.Symbol;
		currency.ExchangeRate = dto.ExchangeRate;
		currency.DecimalPlaces = dto.DecimalPlaces;
		currency.IsActive = dto.IsActive;
		currency.IsDefault = dto.IsDefault;
		currency.ModifiedBy = currentUserId.ToString();
		currency.ModifiedAtUtc = DateTimeOffset.UtcNow;

		currencyRepo.Update(currency);
		await _unitOfWork.SaveChangesAsync();

		return await GetCurrencyByIdAsync(organizationId, currency.Id) ?? throw new InvalidOperationException("Failed to update currency");
	}

	public async Task<bool> DeleteCurrencyAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		var currency = await currencyRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (currency == null) return false;

		currency.IsDeleted = true;
		currency.DeletedAtUtc = DateTimeOffset.UtcNow;
		currency.DeletedBy = currentUserId.ToString();

		currencyRepo.Update(currency);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<List<CurrencyDto>> BulkCloneCurrenciesAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return new List<CurrencyDto>();

		var organizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		
		// Get original currencies
		var originalCurrencies = await currencyRepo.GetQueryable()
			.Where(c => ids.Contains(c.Id) && c.OrganizationId == organizationId && !c.IsDeleted)
			.ToListAsync();

		if (!originalCurrencies.Any())
			return new List<CurrencyDto>();

		var clonedCurrencies = new List<CurrencyDto>();
		var generatedCodes = new HashSet<string>();
		var clonedCurrencyEntities = new List<Currency>();

		// Get all existing currency codes from database to avoid conflicts
		var existingCodes = await currencyRepo.GetQueryable()
			.Where(c => c.OrganizationId == organizationId && !c.IsDeleted)
			.Select(c => c.Code)
			.ToListAsync();
		
		generatedCodes.UnionWith(existingCodes);

		foreach (var originalCurrency in originalCurrencies)
		{
			// Generate unique code with GUID to ensure uniqueness
			var baseCode = originalCurrency.Code;
			var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
			var newCode = $"{baseCode}_COPY_{uniqueSuffix}";
			var counter = 1;
			
			// Check if code already exists in current batch (includes database codes)
			while (generatedCodes.Contains(newCode))
			{
				uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
				newCode = $"{baseCode}_COPY_{uniqueSuffix}";
				counter++;
				if (counter > 100) break; // Safety limit
			}
			
			generatedCodes.Add(newCode);

			// Create cloned currency
			var clonedCurrency = new Currency
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Code = newCode,
				Name = originalCurrency.Name,
				Symbol = originalCurrency.Symbol,
				ExchangeRate = originalCurrency.ExchangeRate,
				DecimalPlaces = originalCurrency.DecimalPlaces,
				IsActive = originalCurrency.IsActive,
				IsDefault = false, // Cloned currencies are never default
				CreatedBy = currentUserId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			clonedCurrencyEntities.Add(clonedCurrency);
		}

		// Add all currency entities at once
		foreach (var clonedCurrency in clonedCurrencyEntities)
		{
			await currencyRepo.AddAsync(clonedCurrency);
		}

		// Save all cloned currencies in a single transaction
		await _unitOfWork.SaveChangesAsync();

		// Build DTOs after saving
		foreach (var clonedCurrency in clonedCurrencyEntities)
		{
			clonedCurrencies.Add(new CurrencyDto
			{
				Id = clonedCurrency.Id,
				OrganizationId = clonedCurrency.OrganizationId,
				Code = clonedCurrency.Code,
				Name = clonedCurrency.Name,
				Symbol = clonedCurrency.Symbol,
				ExchangeRate = clonedCurrency.ExchangeRate,
				DecimalPlaces = clonedCurrency.DecimalPlaces,
				IsActive = clonedCurrency.IsActive,
				IsDefault = clonedCurrency.IsDefault
			});
		}

		return clonedCurrencies;
	}

	// Tax Rate Management
	public async Task<List<TaxRateDto>> GetTaxRatesAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		var taxRates = await taxRateRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);

		return taxRates.Select(x => new TaxRateDto
		{
			Id = x.Id,
			OrganizationId = x.OrganizationId,
			Name = x.Name,
			Description = x.Description,
			Rate = x.Rate,
			TaxType = x.TaxType,
			IsActive = x.IsActive,
			IsDefault = x.IsDefault,
			EffectiveFrom = x.EffectiveFrom,
			EffectiveTo = x.EffectiveTo
		}).ToList();
	}

	public async Task<TaxRateDto?> GetTaxRateByIdAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		var taxRate = await taxRateRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (taxRate == null) return null;

		// Parse User IDs
		Guid? createdById = null;
		Guid? modifiedById = null;
		if (!string.IsNullOrEmpty(taxRate.CreatedBy) && Guid.TryParse(taxRate.CreatedBy, out var createdId))
			createdById = createdId;
		if (!string.IsNullOrEmpty(taxRate.ModifiedBy) && Guid.TryParse(taxRate.ModifiedBy, out var modifiedId))
			modifiedById = modifiedId;

		// Resolve user names
		var userIds = new List<Guid>();
		if (createdById.HasValue) userIds.Add(createdById.Value);
		if (modifiedById.HasValue && modifiedById != createdById) userIds.Add(modifiedById.Value);

		var userNames = userIds.Any() ? await ResolveUserNamesAsync(userIds, OrganizationId) : new Dictionary<Guid, string>();

		var createdByName = createdById.HasValue && userNames.TryGetValue(createdById.Value, out var cName) ? cName : null;
		var modifiedByName = modifiedById.HasValue && userNames.TryGetValue(modifiedById.Value, out var mName) ? mName : null;

		return new TaxRateDto
		{
			Id = taxRate.Id,
			OrganizationId = taxRate.OrganizationId,
			Name = taxRate.Name,
			Description = taxRate.Description,
			Rate = taxRate.Rate,
			TaxType = taxRate.TaxType,
			IsActive = taxRate.IsActive,
			IsDefault = taxRate.IsDefault,
			EffectiveFrom = taxRate.EffectiveFrom,
			EffectiveTo = taxRate.EffectiveTo,
			CreatedAtUtc = taxRate.CreatedAtUtc,
			ModifiedAtUtc = taxRate.ModifiedAtUtc,
			CreatedBy = createdByName ?? taxRate.CreatedBy, // Use resolved name or fallback to ID
			ModifiedBy = modifiedByName ?? taxRate.ModifiedBy, // Use resolved name or fallback to ID
			CreatedById = createdById,
			CreatedByName = createdByName,
			ModifiedById = modifiedById,
			ModifiedByName = modifiedByName
		};
	}

	public async Task<TaxRateDto> CreateTaxRateAsync(Guid organizationId, CreateTaxRateDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		
		// If setting this tax rate as default, unset any existing default tax rate (including inactive)
		if (dto.IsDefault)
		{
			var existingDefaultTaxRates = await taxRateRepo.FindManyAsync(x => 
				x.OrganizationId == OrganizationId && 
				x.IsDefault && 
				!x.IsDeleted);
			
			foreach (var existingDefault in existingDefaultTaxRates)
			{
				existingDefault.IsDefault = false;
				taxRateRepo.Update(existingDefault);
			}
		}
		
		var currentUserId = _tenantService.GetCurrentUserId();
		var taxRate = new TaxRate
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			Name = dto.Name,
			Description = dto.Description,
			Rate = dto.Rate,
			TaxType = dto.TaxType,
			IsActive = dto.IsActive,
			IsDefault = dto.IsDefault,
			EffectiveFrom = dto.EffectiveFrom,
			EffectiveTo = dto.EffectiveTo,
			CreatedBy = currentUserId.ToString(),
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		await taxRateRepo.AddAsync(taxRate);
		await _unitOfWork.SaveChangesAsync();

		return await GetTaxRateByIdAsync(organizationId, taxRate.Id) ?? throw new InvalidOperationException("Failed to create tax rate");
	}

	public async Task<TaxRateDto> UpdateTaxRateAsync(Guid organizationId, Guid id, CreateTaxRateDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		var taxRate = await taxRateRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (taxRate == null) throw new ArgumentException("Tax rate not found");

		// If setting this tax rate as default, unset any other existing default tax rate (including inactive)
		if (dto.IsDefault)
		{
			var existingDefaultTaxRates = await taxRateRepo.FindManyAsync(x => 
				x.OrganizationId == OrganizationId && 
				x.Id != id &&
				x.IsDefault && 
				!x.IsDeleted);
			
			foreach (var existingDefault in existingDefaultTaxRates)
			{
				existingDefault.IsDefault = false;
				taxRateRepo.Update(existingDefault);
			}
		}

		var currentUserId = _tenantService.GetCurrentUserId();
		taxRate.Name = dto.Name;
		taxRate.Description = dto.Description;
		taxRate.Rate = dto.Rate;
		taxRate.TaxType = dto.TaxType;
		taxRate.IsActive = dto.IsActive;
		taxRate.IsDefault = dto.IsDefault;
		taxRate.EffectiveFrom = dto.EffectiveFrom;
		taxRate.EffectiveTo = dto.EffectiveTo;
		taxRate.ModifiedBy = currentUserId.ToString();
		taxRate.ModifiedAtUtc = DateTimeOffset.UtcNow;

		taxRateRepo.Update(taxRate);
		await _unitOfWork.SaveChangesAsync();

		return await GetTaxRateByIdAsync(organizationId, taxRate.Id) ?? throw new InvalidOperationException("Failed to update tax rate");
	}

	public async Task<bool> DeleteTaxRateAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		var taxRate = await taxRateRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (taxRate == null) return false;

		taxRate.IsDeleted = true;
		taxRate.DeletedAtUtc = DateTimeOffset.UtcNow;
		taxRate.DeletedBy = currentUserId.ToString();

		taxRateRepo.Update(taxRate);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<List<TaxRateDto>> BulkCloneTaxRatesAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return new List<TaxRateDto>();

		var organizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		
		// Get original tax rates
		var originalTaxRates = await taxRateRepo.GetQueryable()
			.Where(t => ids.Contains(t.Id) && t.OrganizationId == organizationId && !t.IsDeleted)
			.ToListAsync();

		if (!originalTaxRates.Any())
			return new List<TaxRateDto>();

		var clonedTaxRates = new List<TaxRateDto>();
		var generatedNames = new HashSet<string>();
		var clonedTaxRateEntities = new List<TaxRate>();

		// Get all existing tax rate names from database to avoid conflicts
		var existingNames = await taxRateRepo.GetQueryable()
			.Where(t => t.OrganizationId == organizationId && !t.IsDeleted)
			.Select(t => t.Name)
			.ToListAsync();
		
		generatedNames.UnionWith(existingNames);

		foreach (var originalTaxRate in originalTaxRates)
		{
			// Generate unique name with GUID to ensure uniqueness
			var baseName = originalTaxRate.Name;
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
			
			generatedNames.Add(newName);

			// Create cloned tax rate
			var clonedTaxRate = new TaxRate
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = newName,
				Description = originalTaxRate.Description,
				Rate = originalTaxRate.Rate,
				TaxType = originalTaxRate.TaxType,
				IsActive = originalTaxRate.IsActive,
				IsDefault = false, // Cloned tax rates are never default
				EffectiveFrom = originalTaxRate.EffectiveFrom,
				EffectiveTo = originalTaxRate.EffectiveTo,
				CreatedBy = currentUserId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			clonedTaxRateEntities.Add(clonedTaxRate);
		}

		// Add all tax rate entities at once
		foreach (var clonedTaxRate in clonedTaxRateEntities)
		{
			await taxRateRepo.AddAsync(clonedTaxRate);
		}

		// Save all cloned tax rates in a single transaction
		await _unitOfWork.SaveChangesAsync();

		// Build DTOs after saving
		foreach (var clonedTaxRate in clonedTaxRateEntities)
		{
			clonedTaxRates.Add(new TaxRateDto
			{
				Id = clonedTaxRate.Id,
				OrganizationId = clonedTaxRate.OrganizationId,
				Name = clonedTaxRate.Name,
				Description = clonedTaxRate.Description,
				Rate = clonedTaxRate.Rate,
				TaxType = clonedTaxRate.TaxType,
				IsActive = clonedTaxRate.IsActive,
				IsDefault = clonedTaxRate.IsDefault,
				EffectiveFrom = clonedTaxRate.EffectiveFrom,
				EffectiveTo = clonedTaxRate.EffectiveTo
			});
		}

		return clonedTaxRates;
	}

	// Notification Templates
	public async Task<List<NotificationTemplateDto>> GetNotificationTemplatesAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		var templates = await templateRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);

		return templates.Select(x => new NotificationTemplateDto
		{
			Id = x.Id,
			OrganizationId = x.OrganizationId,
			Name = x.Name,
			Description = x.Description,
			Subject = x.Subject,
			Body = x.Body,
			TemplateType = x.TemplateType,
			Variables = x.Variables,
			IsActive = x.IsActive,
			IsSystemTemplate = x.IsSystemTemplate,
			Category = x.Category
		}).ToList();
	}

	public async Task<NotificationTemplateDto?> GetNotificationTemplateByIdAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		var template = await templateRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (template == null) return null;

		// Parse User IDs
		Guid? createdById = null;
		Guid? modifiedById = null;
		if (!string.IsNullOrEmpty(template.CreatedBy) && Guid.TryParse(template.CreatedBy, out var createdId))
			createdById = createdId;
		if (!string.IsNullOrEmpty(template.ModifiedBy) && Guid.TryParse(template.ModifiedBy, out var modifiedId))
			modifiedById = modifiedId;

		// Resolve user names
		var userIds = new List<Guid>();
		if (createdById.HasValue) userIds.Add(createdById.Value);
		if (modifiedById.HasValue && modifiedById != createdById) userIds.Add(modifiedById.Value);

		var userNames = userIds.Any() ? await ResolveUserNamesAsync(userIds, OrganizationId) : new Dictionary<Guid, string>();

		var createdByName = createdById.HasValue && userNames.TryGetValue(createdById.Value, out var cName) ? cName : null;
		var modifiedByName = modifiedById.HasValue && userNames.TryGetValue(modifiedById.Value, out var mName) ? mName : null;

		return new NotificationTemplateDto
		{
			Id = template.Id,
			OrganizationId = template.OrganizationId,
			Name = template.Name,
			Description = template.Description,
			Subject = template.Subject,
			Body = template.Body,
			TemplateType = template.TemplateType,
			Variables = template.Variables,
			IsActive = template.IsActive,
			IsSystemTemplate = template.IsSystemTemplate,
			Category = template.Category,
			CreatedAtUtc = template.CreatedAtUtc,
			ModifiedAtUtc = template.ModifiedAtUtc,
			CreatedBy = createdByName ?? template.CreatedBy, // Use resolved name or fallback to ID
			ModifiedBy = modifiedByName ?? template.ModifiedBy, // Use resolved name or fallback to ID
			CreatedById = createdById,
			CreatedByName = createdByName,
			ModifiedById = modifiedById,
			ModifiedByName = modifiedByName
		};
	}

	public async Task<NotificationTemplateDto> CreateNotificationTemplateAsync(Guid organizationId, CreateNotificationTemplateDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		
		var currentUserId = _tenantService.GetCurrentUserId();
		var template = new NotificationTemplate
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			Name = dto.Name,
			Description = dto.Description,
			Subject = dto.Subject,
			Body = dto.Body,
			TemplateType = dto.TemplateType,
			Variables = dto.Variables,
			Category = dto.Category,
			IsActive = dto.IsActive,
			CreatedBy = currentUserId.ToString(),
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		await templateRepo.AddAsync(template);
		await _unitOfWork.SaveChangesAsync();

		return await GetNotificationTemplateByIdAsync(organizationId, template.Id) ?? throw new InvalidOperationException("Failed to create notification template");
	}

	public async Task<NotificationTemplateDto> UpdateNotificationTemplateAsync(Guid organizationId, Guid id, CreateNotificationTemplateDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		var template = await templateRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (template == null) throw new ArgumentException("Notification template not found");

		var currentUserId = _tenantService.GetCurrentUserId();
		template.Name = dto.Name;
		template.Description = dto.Description;
		template.Subject = dto.Subject;
		template.Body = dto.Body;
		template.TemplateType = dto.TemplateType;
		template.Variables = dto.Variables;
		template.Category = dto.Category;
		template.IsActive = dto.IsActive;
		template.ModifiedBy = currentUserId.ToString();
		template.ModifiedAtUtc = DateTimeOffset.UtcNow;

		templateRepo.Update(template);
		await _unitOfWork.SaveChangesAsync();

		return await GetNotificationTemplateByIdAsync(organizationId, template.Id) ?? throw new InvalidOperationException("Failed to update notification template");
	}

	public async Task<bool> DeleteNotificationTemplateAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		var template = await templateRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (template == null) return false;

		template.IsDeleted = true;
		template.DeletedAtUtc = DateTimeOffset.UtcNow;
		template.DeletedBy = currentUserId.ToString();

		templateRepo.Update(template);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<List<NotificationTemplateDto>> BulkCloneNotificationTemplatesAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return new List<NotificationTemplateDto>();

		var organizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		
		// Get original templates
		var originalTemplates = await templateRepo.GetQueryable()
			.Where(t => ids.Contains(t.Id) && t.OrganizationId == organizationId && !t.IsDeleted)
			.ToListAsync();

		if (!originalTemplates.Any())
			return new List<NotificationTemplateDto>();

		var clonedTemplates = new List<NotificationTemplateDto>();
		var generatedNames = new HashSet<string>();
		var clonedTemplateEntities = new List<NotificationTemplate>();

		// Get all existing template names from database to avoid conflicts
		var existingNames = await templateRepo.GetQueryable()
			.Where(t => t.OrganizationId == organizationId && !t.IsDeleted)
			.Select(t => t.Name)
			.ToListAsync();
		
		generatedNames.UnionWith(existingNames);

		foreach (var originalTemplate in originalTemplates)
		{
			// Generate unique name with GUID to ensure uniqueness
			var baseName = originalTemplate.Name;
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
			
			generatedNames.Add(newName);

			// Create cloned template
			var clonedTemplate = new NotificationTemplate
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = newName,
				Description = originalTemplate.Description,
				TemplateType = originalTemplate.TemplateType,
				Subject = originalTemplate.Subject,
				Body = originalTemplate.Body,
				Variables = originalTemplate.Variables,
				IsActive = originalTemplate.IsActive,
				IsSystemTemplate = false, // Cloned templates are never system templates
				Category = originalTemplate.Category,
				CreatedBy = currentUserId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			clonedTemplateEntities.Add(clonedTemplate);
		}

		// Add all template entities at once
		foreach (var clonedTemplate in clonedTemplateEntities)
		{
			await templateRepo.AddAsync(clonedTemplate);
		}

		// Save all cloned templates in a single transaction
		await _unitOfWork.SaveChangesAsync();

		// Build DTOs after saving
		foreach (var clonedTemplate in clonedTemplateEntities)
		{
			clonedTemplates.Add(new NotificationTemplateDto
			{
				Id = clonedTemplate.Id,
				OrganizationId = clonedTemplate.OrganizationId,
				Name = clonedTemplate.Name,
				Description = clonedTemplate.Description,
				TemplateType = clonedTemplate.TemplateType,
				Subject = clonedTemplate.Subject,
				Body = clonedTemplate.Body,
				Variables = clonedTemplate.Variables,
				IsActive = clonedTemplate.IsActive,
				IsSystemTemplate = clonedTemplate.IsSystemTemplate,
				Category = clonedTemplate.Category
			});
		}

		return clonedTemplates;
	}

	// Integration Settings
	public async Task<List<IntegrationSettingDto>> GetIntegrationSettingsAsync(Guid organizationId)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		var integrations = await integrationRepo.FindManyAsync(x => x.OrganizationId == OrganizationId && !x.IsDeleted);

		return integrations.Select(x => new IntegrationSettingDto
		{
			Id = x.Id,
			OrganizationId = x.OrganizationId,
			Name = x.Name,
			Description = x.Description,
			IntegrationType = x.IntegrationType,
			Provider = x.Provider,
			Configuration = x.Configuration,
			IsActive = x.IsActive,
			IsEnabled = x.IsEnabled,
			LastSyncAt = x.LastSyncAt,
			LastSyncStatus = x.LastSyncStatus,
			ErrorMessage = x.ErrorMessage
		}).ToList();
	}

	public async Task<IntegrationSettingDto?> GetIntegrationSettingByIdAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		var integration = await integrationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (integration == null) return null;

		// Parse User IDs
		Guid? createdById = null;
		Guid? modifiedById = null;
		if (!string.IsNullOrEmpty(integration.CreatedBy) && Guid.TryParse(integration.CreatedBy, out var createdId))
			createdById = createdId;
		if (!string.IsNullOrEmpty(integration.ModifiedBy) && Guid.TryParse(integration.ModifiedBy, out var modifiedId))
			modifiedById = modifiedId;

		// Resolve user names
		var userIds = new List<Guid>();
		if (createdById.HasValue) userIds.Add(createdById.Value);
		if (modifiedById.HasValue && modifiedById != createdById) userIds.Add(modifiedById.Value);

		var userNames = userIds.Any() ? await ResolveUserNamesAsync(userIds, OrganizationId) : new Dictionary<Guid, string>();

		var createdByName = createdById.HasValue && userNames.TryGetValue(createdById.Value, out var cName) ? cName : null;
		var modifiedByName = modifiedById.HasValue && userNames.TryGetValue(modifiedById.Value, out var mName) ? mName : null;

		return new IntegrationSettingDto
		{
			Id = integration.Id,
			OrganizationId = integration.OrganizationId,
			Name = integration.Name,
			Description = integration.Description,
			IntegrationType = integration.IntegrationType,
			Provider = integration.Provider,
			Configuration = integration.Configuration,
			IsActive = integration.IsActive,
			IsEnabled = integration.IsEnabled,
			LastSyncAt = integration.LastSyncAt,
			LastSyncStatus = integration.LastSyncStatus,
			ErrorMessage = integration.ErrorMessage,
			CreatedAtUtc = integration.CreatedAtUtc,
			ModifiedAtUtc = integration.ModifiedAtUtc,
			CreatedBy = createdByName ?? integration.CreatedBy, // Use resolved name or fallback to ID
			ModifiedBy = modifiedByName ?? integration.ModifiedBy, // Use resolved name or fallback to ID
			CreatedById = createdById,
			CreatedByName = createdByName,
			ModifiedById = modifiedById,
			ModifiedByName = modifiedByName
		};
	}

	public async Task<IntegrationSettingDto> CreateIntegrationSettingAsync(Guid organizationId, CreateIntegrationSettingDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		
		var currentUserId = _tenantService.GetCurrentUserId();
		var integration = new IntegrationSetting
		{
			Id = Guid.NewGuid(),
			OrganizationId = OrganizationId,
			Name = dto.Name,
			Description = dto.Description,
			IntegrationType = dto.IntegrationType,
			Provider = dto.Provider,
			Configuration = dto.Configuration,
			IsActive = dto.IsActive,
			IsEnabled = dto.IsEnabled,
			CreatedBy = currentUserId.ToString(),
			CreatedAtUtc = DateTimeOffset.UtcNow
		};

		await integrationRepo.AddAsync(integration);
		await _unitOfWork.SaveChangesAsync();

		return await GetIntegrationSettingByIdAsync(organizationId, integration.Id) ?? throw new InvalidOperationException("Failed to create integration setting");
	}

	public async Task<IntegrationSettingDto> UpdateIntegrationSettingAsync(Guid organizationId, Guid id, CreateIntegrationSettingDto dto)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		var integration = await integrationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (integration == null) throw new ArgumentException("Integration setting not found");

		var currentUserId = _tenantService.GetCurrentUserId();
		integration.Name = dto.Name;
		integration.Description = dto.Description;
		integration.IntegrationType = dto.IntegrationType;
		integration.Provider = dto.Provider;
		integration.Configuration = dto.Configuration;
		integration.IsActive = dto.IsActive;
		integration.IsEnabled = dto.IsEnabled;
		integration.ModifiedBy = currentUserId.ToString();
		integration.ModifiedAtUtc = DateTimeOffset.UtcNow;

		integrationRepo.Update(integration);
		await _unitOfWork.SaveChangesAsync();

		return await GetIntegrationSettingByIdAsync(organizationId, integration.Id) ?? throw new InvalidOperationException("Failed to update integration setting");
	}

	public async Task<bool> DeleteIntegrationSettingAsync(Guid organizationId, Guid id)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		var integration = await integrationRepo.FindAsync(x => x.Id == id && x.OrganizationId == OrganizationId && !x.IsDeleted);

		if (integration == null) return false;

		integration.IsDeleted = true;
		integration.DeletedAtUtc = DateTimeOffset.UtcNow;
		integration.DeletedBy = currentUserId.ToString();

		integrationRepo.Update(integration);
		await _unitOfWork.SaveChangesAsync();
		return true;
	}

	public async Task<List<IntegrationSettingDto>> BulkCloneIntegrationSettingsAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return new List<IntegrationSettingDto>();

		var organizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		
		// Get original integrations
		var originalIntegrations = await integrationRepo.GetQueryable()
			.Where(i => ids.Contains(i.Id) && i.OrganizationId == organizationId && !i.IsDeleted)
			.ToListAsync();

		if (!originalIntegrations.Any())
			return new List<IntegrationSettingDto>();

		var clonedIntegrations = new List<IntegrationSettingDto>();
		var generatedNames = new HashSet<string>();
		var clonedIntegrationEntities = new List<IntegrationSetting>();

		// Get all existing integration names from database to avoid conflicts
		var existingNames = await integrationRepo.GetQueryable()
			.Where(i => i.OrganizationId == organizationId && !i.IsDeleted)
			.Select(i => i.Name)
			.ToListAsync();
		
		generatedNames.UnionWith(existingNames);

		foreach (var originalIntegration in originalIntegrations)
		{
			// Generate unique name with GUID to ensure uniqueness
			var baseName = originalIntegration.Name ?? originalIntegration.IntegrationType;
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
			
			generatedNames.Add(newName);

			// Create cloned integration
			var clonedIntegration = new IntegrationSetting
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = newName,
				Description = originalIntegration.Description,
				IntegrationType = originalIntegration.IntegrationType,
				Provider = originalIntegration.Provider,
				Configuration = originalIntegration.Configuration,
				Credentials = originalIntegration.Credentials,
				IsActive = originalIntegration.IsActive,
				IsEnabled = false, // Cloned integrations start as disabled
				CreatedBy = currentUserId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			clonedIntegrationEntities.Add(clonedIntegration);
		}

		// Add all integration entities at once
		foreach (var clonedIntegration in clonedIntegrationEntities)
		{
			await integrationRepo.AddAsync(clonedIntegration);
		}

		// Save all cloned integrations in a single transaction
		await _unitOfWork.SaveChangesAsync();

		// Build DTOs after saving
		foreach (var clonedIntegration in clonedIntegrationEntities)
		{
			clonedIntegrations.Add(new IntegrationSettingDto
			{
				Id = clonedIntegration.Id,
				OrganizationId = clonedIntegration.OrganizationId,
				Name = clonedIntegration.Name,
				Description = clonedIntegration.Description,
				IntegrationType = clonedIntegration.IntegrationType,
				Provider = clonedIntegration.Provider,
				Configuration = clonedIntegration.Configuration,
				IsActive = clonedIntegration.IsActive,
				IsEnabled = clonedIntegration.IsEnabled,
				LastSyncAt = clonedIntegration.LastSyncAt,
				LastSyncStatus = clonedIntegration.LastSyncStatus,
				ErrorMessage = clonedIntegration.ErrorMessage
			});
		}

		return clonedIntegrations;
	}

	private List<LocationHierarchyDto> BuildLocationHierarchy(Guid parentId, IEnumerable<Location> allLocations)
	{
		var children = allLocations
			.Where(l => l.ParentId == parentId)
			.OrderBy(l => l.SortOrder)
			.ToList();

		var result = new List<LocationHierarchyDto>();
		foreach (var child in children)
		{
			var locationDto = MapToLocationHierarchyDto(child);
			locationDto.Children = BuildLocationHierarchy(child.Id, allLocations);
			result.Add(locationDto);
		}

		return result;
	}

	private LocationHierarchyDto MapToLocationHierarchyDto(Location location)
	{
		return new LocationHierarchyDto
		{
			Id = location.Id,
			Name = location.Name,
			LocationCode = location.LocationCode,
			LocationType = location.LocationType,
			Level = location.Level,
			SortOrder = location.SortOrder,
			IsActive = location.IsActive,
			Children = new List<LocationHierarchyDto>()
		};
	}

	// ========================================
	// Location Import/Export/History Methods
	// ========================================

	public async Task<byte[]> GetLocationImportTemplateAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		var allLocations = await locationRepo.FindManyAsync(x => x.OrganizationId == organizationId && !x.IsDeleted);

		var countries = allLocations.Where(l => !string.IsNullOrEmpty(l.Country)).Select(l => l.Country).Distinct().OrderBy(c => c).ToList();
		var cities = allLocations.Where(l => !string.IsNullOrEmpty(l.City)).Select(l => l.City).Distinct().OrderBy(c => c).ToList();
		var locationTypes = new[] { "HEADQUARTERS", "BRANCH", "WAREHOUSE", "RETAIL", "OFFICE", "DISTRIBUTION_CENTER" };
		
		// Get parent location names for dropdown
		var parentLocationNames = allLocations.Where(l => !string.IsNullOrEmpty(l.Name)).Select(l => l.Name).Distinct().OrderBy(n => n).ToList();

		// Get active currencies for dropdown
		var currencyRepo = _unitOfWork.Repository<Currency>();
		var activeCurrencies = await currencyRepo.FindManyAsync(x => x.OrganizationId == organizationId && x.IsActive && !x.IsDeleted);
		var currencyCodes = activeCurrencies.Select(c => c.Code).OrderBy(c => c).ToList();

		return GenerateLocationImportTemplate(countries, cities, locationTypes, currencyCodes, parentLocationNames);
	}

	private byte[] GenerateLocationImportTemplate(List<string?> countries, List<string?> cities, string[] locationTypes, List<string> currencyCodes, List<string> parentLocationNames)
	{
		using var workbook = new XLWorkbook();
		var importSheet = workbook.Worksheets.Add("Import Data");

		// Updated headers: removed Is Warehouse, Is Retail, Is Office; added Location Type, Parent Location
		var headers = new[] { "Name", "Address", "City", "State", "Country", "Postal Code", "Phone", "Email", "Manager Name", "Location Code", "Location Type", "Parent Location", "Is Active", "Time Zone", "Currency", "Language", "Latitude", "Longitude" };
		
		for (int i = 0; i < headers.Length; i++)
		{
			var cell = importSheet.Cell(1, i + 1);
			cell.Value = headers[i];
			cell.Style.Font.Bold = true;
			cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
			cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
		}

		// Add sample data row
		importSheet.Cell(2, 1).Value = "Main Office";
		importSheet.Cell(2, 2).Value = "123 Main St";
		importSheet.Cell(2, 3).Value = cities.FirstOrDefault() ?? "City";
		importSheet.Cell(2, 4).Value = "State";
		importSheet.Cell(2, 5).Value = countries.FirstOrDefault() ?? "Country";
		importSheet.Cell(2, 6).Value = "12345";
		importSheet.Cell(2, 7).Value = "+1234567890";
		importSheet.Cell(2, 8).Value = "office@example.com";
		importSheet.Cell(2, 9).Value = "John Doe";
		importSheet.Cell(2, 10).Value = "MAIN-001";
		importSheet.Cell(2, 11).Value = locationTypes.FirstOrDefault() ?? "BRANCH";
		importSheet.Cell(2, 12).Value = ""; // Parent Location (optional)
		importSheet.Cell(2, 13).Value = "Active";
		importSheet.Cell(2, 14).Value = "UTC";
		importSheet.Cell(2, 15).Value = currencyCodes.FirstOrDefault() ?? "USD";
		importSheet.Cell(2, 16).Value = "en";
		importSheet.Cell(2, 17).Value = "40.7128";
		importSheet.Cell(2, 18).Value = "-74.0060";

		// Reference Data sheet
		var referenceSheet = workbook.Worksheets.Add("Reference Data");
		
		// Status values
		referenceSheet.Cell(1, 1).Value = "Status";
		referenceSheet.Cell(1, 1).Style.Font.Bold = true;
		referenceSheet.Cell(2, 1).Value = "Active";
		referenceSheet.Cell(3, 1).Value = "Inactive";

		// Location Types
		referenceSheet.Cell(1, 2).Value = "Location Type";
		referenceSheet.Cell(1, 2).Style.Font.Bold = true;
		for (int i = 0; i < locationTypes.Length; i++)
		{
			referenceSheet.Cell(i + 2, 2).Value = locationTypes[i];
		}

		// Currency codes
		referenceSheet.Cell(1, 3).Value = "Currency";
		referenceSheet.Cell(1, 3).Style.Font.Bold = true;
		for (int i = 0; i < currencyCodes.Count; i++)
		{
			referenceSheet.Cell(i + 2, 3).Value = currencyCodes[i];
		}

		// Parent Location names
		referenceSheet.Cell(1, 4).Value = "Parent Location";
		referenceSheet.Cell(1, 4).Style.Font.Bold = true;
		for (int i = 0; i < parentLocationNames.Count; i++)
		{
			referenceSheet.Cell(i + 2, 4).Value = parentLocationNames[i];
		}

		// Named ranges
		workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 1, 3, 1));
		workbook.NamedRanges.Add("LocationTypeValues", referenceSheet.Range(2, 2, locationTypes.Length + 1, 2));
		if (currencyCodes.Any())
		{
			workbook.NamedRanges.Add("CurrencyValues", referenceSheet.Range(2, 3, currencyCodes.Count + 1, 3));
		}
		if (parentLocationNames.Any())
		{
			workbook.NamedRanges.Add("ParentLocationValues", referenceSheet.Range(2, 4, parentLocationNames.Count + 1, 4));
		}

		// Data validation (dropdowns)
		// Location Type (column K = 11)
		var locationTypeValidation = importSheet.Range("K2:K1000").SetDataValidation();
		locationTypeValidation.List("=LocationTypeValues", true);
		locationTypeValidation.IgnoreBlanks = false; // Required field
		locationTypeValidation.InCellDropdown = true;

		// Parent Location (column L = 12) - optional
		if (parentLocationNames.Any())
		{
			var parentLocationValidation = importSheet.Range("L2:L1000").SetDataValidation();
			parentLocationValidation.List("=ParentLocationValues", true);
			parentLocationValidation.IgnoreBlanks = true;
			parentLocationValidation.InCellDropdown = true;
		}

		// Is Active (column M = 13)
		var statusValidation = importSheet.Range("M2:M1000").SetDataValidation();
		statusValidation.List("=StatusValues", true);
		statusValidation.IgnoreBlanks = true;
		statusValidation.InCellDropdown = true;

		// Currency dropdown (column O = 15)
		if (currencyCodes.Any())
		{
			var currencyValidation = importSheet.Range("O2:O1000").SetDataValidation();
			currencyValidation.List("=CurrencyValues", true);
			currencyValidation.IgnoreBlanks = true;
			currencyValidation.InCellDropdown = true;
		}

		importSheet.Columns().AdjustToContents();
		referenceSheet.Columns().AdjustToContents();

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}

	public async Task<string> StartLocationExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var organizationId = _tenantService.GetOrganizationId();

		return await _importExportService.StartExportJobAsync<Location>(
			entityType: "Location",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				scopedTenantService.SetBackgroundContext(organizationId, null, null);

				var repo = scopedUnitOfWork.Repository<Location>();

				var search = f.GetValueOrDefault("search")?.ToString();
				var isActive = f.GetValueOrDefault("isActive") as bool?;
				var country = f.GetValueOrDefault("country")?.ToString();
				var city = f.GetValueOrDefault("city")?.ToString();
				var createdFrom = f.GetValueOrDefault("createdFrom") as DateTimeOffset?;
				var createdTo = f.GetValueOrDefault("createdTo") as DateTimeOffset?;
				var selectedIds = f.GetValueOrDefault("selectedIds");

				search = string.IsNullOrWhiteSpace(search) ? null : search;
				country = string.IsNullOrWhiteSpace(country) ? null : country;
				city = string.IsNullOrWhiteSpace(city) ? null : city;

				var searchLower = search?.ToLower();
				List<Location> locationsToExport;

				List<Guid>? idsList = null;
				if (selectedIds != null)
				{
					idsList = selectedIds as List<Guid>;
					if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
					{
						idsList = enumerableIds.ToList();
					}
				}

				if (idsList != null && idsList.Any())
				{
					var locationIds = idsList;
					var query = repo.GetQueryable()
						.Include(l => l.ParentLocation)
						.Where(l => l.OrganizationId == organizationId && !l.IsDeleted && locationIds.Contains(l.Id));
					locationsToExport = await query.ToListAsync();
				}
				else
				{
					var query = repo.GetQueryable()
						.Include(l => l.ParentLocation)
						.Where(l => l.OrganizationId == organizationId && !l.IsDeleted &&
							(searchLower == null || (l.Name.ToLower().Contains(searchLower) || (l.Description != null && l.Description.ToLower().Contains(searchLower)) || (l.Address != null && l.Address.ToLower().Contains(searchLower)) || (l.City != null && l.City.ToLower().Contains(searchLower)) || (l.Country != null && l.Country.ToLower().Contains(searchLower)))) &&
							(isActive == null || l.IsActive == isActive) &&
							(country == null || l.Country == country) &&
							(city == null || l.City == city) &&
							(createdFrom == null || l.CreatedAtUtc >= createdFrom.Value) &&
							(createdTo == null || l.CreatedAtUtc <= createdTo.Value));
					locationsToExport = await query.ToListAsync();
				}

				return locationsToExport;
			},
			filters: filters,
			columnMapper: MapLocationToExportColumns
		);
	}

	private Dictionary<string, object> MapLocationToExportColumns(Location location)
	{
		// Get parent location name
		string parentLocationName = "";
		if (location.ParentLocationId.HasValue && location.ParentLocation != null)
		{
			parentLocationName = location.ParentLocation.Name ?? "";
		}

		return new Dictionary<string, object>
		{
			{ "Name", location.Name ?? "" },
			{ "Address", location.Address ?? "" },
			{ "City", location.City ?? "" },
			{ "State", location.State ?? "" },
			{ "Country", location.Country ?? "" },
			{ "Postal Code", location.PostalCode ?? "" },
			{ "Phone", location.Phone ?? "" },
			{ "Email", location.Email ?? "" },
			{ "Manager Name", location.ManagerName ?? "" },
			{ "Location Code", location.LocationCode ?? "" },
			{ "Location Type", location.LocationType ?? "" },
			{ "Parent Location", parentLocationName },
			{ "Is Active", location.IsActive ? "Active" : "Inactive" },
			{ "Time Zone", location.TimeZone ?? "" },
			{ "Currency", location.Currency ?? "" },
			{ "Language", location.Language ?? "" },
			{ "Latitude", location.Latitude?.ToString() ?? "" },
			{ "Longitude", location.Longitude?.ToString() ?? "" }
		};
	}

	public async Task<ExportJobStatusDto?> GetLocationExportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetExportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> DownloadLocationExportFileAsync(string jobId)
	{
		return await _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<string> StartLocationImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		var organizationId = _tenantService.GetOrganizationId();
		var userId = _tenantService.GetCurrentUserId();
		var userName = _tenantService.GetCurrentUserName();

		return await _importExportService.StartImportJobAsync<CreateLocationDto>(
			entityType: "Location",
			fileStream: fileStream,
			fileName: fileName,
			rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
			{
				if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
					return (false, "Name is required", false, false);

				// Validate required fields: Phone and Email
				var phone = rowData.GetValueOrDefault("Phone")?.ToString();
				if (string.IsNullOrWhiteSpace(phone))
					return (false, "Phone is required", false, false);

				var email = rowData.GetValueOrDefault("Email")?.ToString();
				if (string.IsNullOrWhiteSpace(email))
					return (false, "Email is required", false, false);

				// Validate email format
				if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
					return (false, "Email format is invalid", false, false);

				// Validate Location Type (required)
				var locationType = rowData.GetValueOrDefault("Location Type")?.ToString();
				if (string.IsNullOrWhiteSpace(locationType))
					return (false, "Location Type is required", false, false);

				var locationRepo = scopedUnitOfWork.Repository<Location>();
				var existingLocation = await locationRepo.FindAsync(l => l.OrganizationId == organizationId && l.Name == name && !l.IsDeleted);

				// Find parent location by name if provided
				Guid? parentLocationId = null;
				int level = 0;
				var parentLocationName = rowData.GetValueOrDefault("Parent Location")?.ToString();
				if (!string.IsNullOrWhiteSpace(parentLocationName))
				{
					var parentLocation = await locationRepo.FindAsync(l => l.OrganizationId == organizationId && l.Name == parentLocationName && !l.IsDeleted);
					if (parentLocation != null)
					{
						parentLocationId = parentLocation.Id;
						level = parentLocation.Level + 1;
					}
					else
					{
						return (false, $"Parent Location '{parentLocationName}' not found", false, false);
					}
				}

				var newLocation = new Location
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					Name = name,
					Address = rowData.GetValueOrDefault("Address")?.ToString(),
					City = rowData.GetValueOrDefault("City")?.ToString(),
					State = rowData.GetValueOrDefault("State")?.ToString(),
					Country = rowData.GetValueOrDefault("Country")?.ToString(),
					PostalCode = rowData.GetValueOrDefault("Postal Code")?.ToString(),
					Phone = phone,
					Email = email,
					ManagerName = rowData.GetValueOrDefault("Manager Name")?.ToString(),
					LocationCode = rowData.GetValueOrDefault("Location Code")?.ToString(),
					LocationType = locationType,
					ParentLocationId = parentLocationId,
					Level = level,
					IsActive = rowData.GetValueOrDefault("Is Active")?.ToString()?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true,
					TimeZone = rowData.GetValueOrDefault("Time Zone")?.ToString(),
					Currency = rowData.GetValueOrDefault("Currency")?.ToString(),
					Language = rowData.GetValueOrDefault("Language")?.ToString(),
					CreatedBy = userId.ToString(),
					CreatedAtUtc = DateTimeOffset.UtcNow,
					ModifiedBy = userId.ToString(),
					ModifiedAtUtc = DateTimeOffset.UtcNow
				};

				if (rowData.TryGetValue("Latitude", out var latStr) && decimal.TryParse(latStr, out var lat))
					newLocation.Latitude = lat;

				if (rowData.TryGetValue("Longitude", out var lonStr) && decimal.TryParse(lonStr, out var lon))
					newLocation.Longitude = lon;

				if (existingLocation != null)
				{
					if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
						return (false, "Location already exists", false, true);
					
					if (duplicateStrategy == DuplicateHandlingStrategy.Update)
					{
						existingLocation.Address = newLocation.Address;
						existingLocation.City = newLocation.City;
						existingLocation.State = newLocation.State;
						existingLocation.Country = newLocation.Country;
						existingLocation.PostalCode = newLocation.PostalCode;
						existingLocation.Phone = newLocation.Phone;
						existingLocation.Email = newLocation.Email;
						existingLocation.ManagerName = newLocation.ManagerName;
						existingLocation.LocationCode = newLocation.LocationCode;
						existingLocation.LocationType = newLocation.LocationType;
						existingLocation.ParentLocationId = newLocation.ParentLocationId;
						existingLocation.Level = newLocation.Level;
						existingLocation.IsActive = newLocation.IsActive;
						existingLocation.TimeZone = newLocation.TimeZone;
						existingLocation.Currency = newLocation.Currency;
						existingLocation.Language = newLocation.Language;
						existingLocation.Latitude = newLocation.Latitude;
						existingLocation.Longitude = newLocation.Longitude;
						existingLocation.ModifiedBy = userId.ToString();
						existingLocation.ModifiedAtUtc = DateTimeOffset.UtcNow;
						locationRepo.Update(existingLocation);
						return (true, null, true, false);
					}
				}

				await locationRepo.AddAsync(newLocation);
				return (true, null, false, false);
			},
			duplicateStrategy: duplicateStrategy
		);
	}

	public async Task<ImportJobStatusDto?> GetLocationImportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetImportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> GetLocationImportErrorReportAsync(string errorReportId)
	{
		return await _importExportService.GetImportErrorReportAsync(errorReportId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetLocationImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		return await _importExportService.GetHistoryAsync("Location", type, page, pageSize);
	}

	// Location Bulk Clone
	public async Task<List<LocationDto>> BulkCloneLocationsAsync(List<Guid> ids)
	{
		if (ids == null || !ids.Any())
			return new List<LocationDto>();

		var organizationId = _tenantService.GetOrganizationId();
		var currentUserId = _tenantService.GetCurrentUserId();
		var locationRepo = _unitOfWork.Repository<Location>();
		
		// Get original locations
		var originalLocations = await locationRepo.GetQueryable()
			.Where(l => ids.Contains(l.Id) && l.OrganizationId == organizationId && !l.IsDeleted)
			.ToListAsync();

		if (!originalLocations.Any())
			return new List<LocationDto>();

		var clonedLocations = new List<LocationDto>();
		var generatedNames = new HashSet<string>();
		var clonedLocationEntities = new List<Location>();

		// Get all existing location names from database to avoid conflicts
		var existingNames = await locationRepo.GetQueryable()
			.Where(l => l.OrganizationId == organizationId && !l.IsDeleted)
			.Select(l => l.Name)
			.ToListAsync();
		
		generatedNames.UnionWith(existingNames);

		foreach (var originalLocation in originalLocations)
		{
			// Generate unique name with GUID to ensure uniqueness
			var baseName = originalLocation.Name;
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
			
			generatedNames.Add(newName);

			// Create cloned location
			var clonedLocation = new Location
			{
				Id = Guid.NewGuid(),
				OrganizationId = organizationId,
				Name = newName,
				Description = originalLocation.Description,
				Address = originalLocation.Address,
				City = originalLocation.City,
				State = originalLocation.State,
				Country = originalLocation.Country,
				PostalCode = originalLocation.PostalCode,
				Phone = originalLocation.Phone,
				Email = originalLocation.Email,
				ManagerName = originalLocation.ManagerName,
				IsActive = originalLocation.IsActive,
				IsWarehouse = originalLocation.IsWarehouse,
				IsRetail = originalLocation.IsRetail,
				IsOffice = originalLocation.IsOffice,
				Latitude = originalLocation.Latitude,
				Longitude = originalLocation.Longitude,
				TimeZone = originalLocation.TimeZone,
				Currency = originalLocation.Currency,
				Language = originalLocation.Language,
				ParentId = originalLocation.ParentId,
				SortOrder = originalLocation.SortOrder,
				CreatedBy = currentUserId.ToString(),
				CreatedAtUtc = DateTimeOffset.UtcNow
			};

			clonedLocationEntities.Add(clonedLocation);
		}

		// Add all location entities at once
		foreach (var clonedLocation in clonedLocationEntities)
		{
			await locationRepo.AddAsync(clonedLocation);
		}

		// Save all cloned locations in a single transaction
		await _unitOfWork.SaveChangesAsync();

		// Build DTOs after saving
		foreach (var clonedLocation in clonedLocationEntities)
		{
			clonedLocations.Add(new LocationDto
			{
				Id = clonedLocation.Id,
				OrganizationId = clonedLocation.OrganizationId,
				Name = clonedLocation.Name,
				Description = clonedLocation.Description,
				Address = clonedLocation.Address,
				City = clonedLocation.City,
				State = clonedLocation.State,
				Country = clonedLocation.Country,
				PostalCode = clonedLocation.PostalCode,
				Phone = clonedLocation.Phone,
				Email = clonedLocation.Email,
				ManagerName = clonedLocation.ManagerName,
				IsActive = clonedLocation.IsActive,
				IsWarehouse = clonedLocation.IsWarehouse,
				IsRetail = clonedLocation.IsRetail,
				IsOffice = clonedLocation.IsOffice,
				Latitude = clonedLocation.Latitude,
				Longitude = clonedLocation.Longitude
			});
		}

		return clonedLocations;
	}

	// Location Filter Options
	public async Task<List<string>> GetLocationCountriesAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		
		return await locationRepo.GetQueryable()
			.Where(x => x.OrganizationId == organizationId && !x.IsDeleted && !string.IsNullOrEmpty(x.Country))
			.Select(x => x.Country!)
			.Distinct()
			.OrderBy(x => x)
			.ToListAsync();
	}

	public async Task<List<LocationDropdownDto>> GetLocationDropdownOptionsAsync(bool? isActive = null)
	{
		var OrganizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		
		var query = locationRepo.GetQueryable()
			.Where(x => x.OrganizationId == OrganizationId && !x.IsDeleted);
		
		if (isActive.HasValue)
		{
			query = query.Where(x => x.IsActive == isActive.Value);
		}
		
		var locations = await query
			.OrderBy(x => x.SortOrder)
			.ThenBy(x => x.Name)
			.Select(x => new LocationDropdownDto
			{
				Id = x.Id,
				Name = x.Name,
				IsActive = x.IsActive
			})
			.ToListAsync();
		
		return locations;
	}

	public async Task<List<string>> GetLocationCitiesAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var locationRepo = _unitOfWork.Repository<Location>();
		
		return await locationRepo.GetQueryable()
			.Where(x => x.OrganizationId == organizationId && !x.IsDeleted && !string.IsNullOrEmpty(x.City))
			.Select(x => x.City!)
			.Distinct()
			.OrderBy(x => x)
			.ToListAsync();
	}

	// Business Settings Pagination
	public async Task<PagedResultDto<BusinessSettingDto>> GetBusinessSettingsPagedAsync(string? search, bool? isActive, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		
		// Determine which organization to query
		Guid filterOrganizationId;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin viewing another organization
			filterOrganizationId = targetOrganizationId.Value;
		}
		else
		{
			// Use current user's organization
			filterOrganizationId = currentUserOrgId;
		}
		
		var query = settingRepo.GetQueryable();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin can view any organization - ignore tenant filter
			query = query.IgnoreQueryFilters()
				.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}
		else
		{
			// Regular users see only their organization
			query = query.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}

		if (!string.IsNullOrEmpty(search))
		{
			var searchLower = search.ToLower();
			query = query.Where(x =>
				(x.SettingKey != null && x.SettingKey.ToLower().Contains(searchLower)) ||
				(x.SettingValue != null && x.SettingValue.ToLower().Contains(searchLower)) ||
				(x.Description != null && x.Description.ToLower().Contains(searchLower)));
		}

		if (isActive.HasValue)
			query = query.Where(x => x.IsActive == isActive.Value);

		if (createdFrom.HasValue)
			query = query.Where(x => x.CreatedAtUtc >= createdFrom.Value);

		if (createdTo.HasValue)
			query = query.Where(x => x.CreatedAtUtc <= createdTo.Value);

		var totalCount = await query.CountAsync();

		query = sortField?.ToLower() switch
		{
			"settingkey" => sortDirection == "asc" ? query.OrderBy(x => x.SettingKey) : query.OrderByDescending(x => x.SettingKey),
			"settingvalue" => sortDirection == "asc" ? query.OrderBy(x => x.SettingValue) : query.OrderByDescending(x => x.SettingValue),
			"description" => sortDirection == "asc" ? query.OrderBy(x => x.Description) : query.OrderByDescending(x => x.Description),
			"isactive" => sortDirection == "asc" ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
			_ => sortDirection == "asc" ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc)
		};

		var settings = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new BusinessSettingDto
			{
				Id = x.Id,
				OrganizationId = x.OrganizationId,
				SettingKey = x.SettingKey,
				SettingValue = x.SettingValue,
				SettingType = x.SettingType,
				Description = x.Description,
				IsActive = x.IsActive
			})
			.ToListAsync();

		return new PagedResultDto<BusinessSettingDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = settings
		};
	}

	// Business Settings Import/Export/History
	public async Task<byte[]> GetBusinessSettingImportTemplateAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var settingRepo = _unitOfWork.Repository<BusinessSetting>();
		var existingSettings = await settingRepo.FindManyAsync(x => x.OrganizationId == organizationId && !x.IsDeleted);
		var settingKeys = existingSettings.Select(x => x.SettingKey).Distinct().ToList();
		var settingTypes = existingSettings.Select(x => x.SettingType).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();

		return GenerateBusinessSettingImportTemplate(settingKeys, settingTypes);
	}

	private byte[] GenerateBusinessSettingImportTemplate(List<string> settingKeys, List<string> settingTypes)
	{
		using var workbook = new XLWorkbook();
		var importSheet = workbook.Worksheets.Add("Import Data");

		// Headers
		importSheet.Cell(1, 1).Value = "Setting Key";
		importSheet.Cell(1, 2).Value = "Setting Value";
		importSheet.Cell(1, 3).Value = "Setting Type";
		importSheet.Cell(1, 4).Value = "Description";
		importSheet.Cell(1, 5).Value = "Is Active";

		var headerRange = importSheet.Range(1, 1, 1, 5);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

		// Example row
		importSheet.Cell(2, 1).Value = settingKeys.FirstOrDefault() ?? "SettingKey";
		importSheet.Cell(2, 2).Value = "SettingValue";
		importSheet.Cell(2, 3).Value = settingTypes.FirstOrDefault() ?? "String";
		importSheet.Cell(2, 4).Value = "Description";
		importSheet.Cell(2, 5).Value = "Active";

		// Reference Data sheet
		// Note: Only include fields that are dropdown/checkbox/toggle in the modal
		// Setting Key is a text input in modal, so not included
		var referenceSheet = workbook.Worksheets.Add("Reference Data");
		
		if (settingTypes.Any())
		{
			referenceSheet.Cell(1, 1).Value = "Setting Types";
			referenceSheet.Cell(1, 1).Style.Font.Bold = true;
			for (int i = 0; i < settingTypes.Count; i++)
			{
				referenceSheet.Cell(i + 2, 1).Value = settingTypes[i];
			}
		}

		referenceSheet.Cell(1, 2).Value = "Status";
		referenceSheet.Cell(1, 2).Style.Font.Bold = true;
		referenceSheet.Cell(2, 2).Value = "Active";
		referenceSheet.Cell(3, 2).Value = "Inactive";

		// Named ranges
		// Note: SettingKeys removed from dropdowns as Setting Key is a text input in the modal
		if (settingTypes.Any())
			workbook.NamedRanges.Add("SettingTypes", referenceSheet.Range(2, 1, settingTypes.Count + 1, 1));
		workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 2, 3, 2));

		// Data validation (dropdowns) - only for dropdown/checkbox/toggle fields from modal
		// Setting Type (dropdown in modal)
		if (settingTypes.Any())
		{
			var settingTypeValidation = importSheet.Range("C2:C1000").SetDataValidation();
			settingTypeValidation.List("=SettingTypes", true);
			settingTypeValidation.IgnoreBlanks = true;
			settingTypeValidation.InCellDropdown = true;
		}

		// Is Active (checkbox/toggle in modal)
		var statusValidation = importSheet.Range("E2:E1000").SetDataValidation();
		statusValidation.List("=StatusValues", true);
		statusValidation.IgnoreBlanks = true;
		statusValidation.InCellDropdown = true;

		importSheet.Columns().AdjustToContents();
		referenceSheet.Columns().AdjustToContents();

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}

	public async Task<string> StartBusinessSettingExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var organizationId = _tenantService.GetOrganizationId();

		return await _importExportService.StartExportJobAsync<BusinessSetting>(
			entityType: "BusinessSetting",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				scopedTenantService.SetBackgroundContext(organizationId, null, null);

				var repo = scopedUnitOfWork.Repository<BusinessSetting>();

				var search = f.GetValueOrDefault("search")?.ToString();
				var isActive = f.GetValueOrDefault("isActive") as bool?;
				var createdFrom = f.GetValueOrDefault("createdFrom") as DateTimeOffset?;
				var createdTo = f.GetValueOrDefault("createdTo") as DateTimeOffset?;
				var selectedIds = f.GetValueOrDefault("selectedIds");

				var searchLower = search?.ToLower();
				List<BusinessSetting> settingsToExport;

				List<Guid>? idsList = null;
				if (selectedIds != null)
				{
					idsList = selectedIds as List<Guid>;
					if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
					{
						idsList = enumerableIds.ToList();
					}
				}

				if (idsList != null && idsList.Any())
				{
					var query = repo.GetQueryable()
						.Where(s => s.OrganizationId == organizationId && !s.IsDeleted && idsList.Contains(s.Id));
					settingsToExport = await query.ToListAsync();
				}
				else
				{
					var query = repo.GetQueryable()
						.Where(s => s.OrganizationId == organizationId && !s.IsDeleted &&
							(searchLower == null || (s.SettingKey.ToLower().Contains(searchLower) || (s.SettingValue != null && s.SettingValue.ToLower().Contains(searchLower)) || (s.Description != null && s.Description.ToLower().Contains(searchLower)))) &&
							(isActive == null || s.IsActive == isActive) &&
							(createdFrom == null || s.CreatedAtUtc >= createdFrom.Value) &&
							(createdTo == null || s.CreatedAtUtc <= createdTo.Value));
					settingsToExport = await query.ToListAsync();
				}

				return settingsToExport;
			},
			filters: filters,
			columnMapper: MapBusinessSettingToExportColumns
		);
	}

	private Dictionary<string, object> MapBusinessSettingToExportColumns(BusinessSetting setting)
	{
		return new Dictionary<string, object>
		{
			{ "Setting Key", setting.SettingKey ?? "" },
			{ "Setting Value", setting.SettingValue ?? "" },
			{ "Setting Type", setting.SettingType ?? "" },
			{ "Description", setting.Description ?? "" },
			{ "Is Active", setting.IsActive ? "Active" : "Inactive" }
		};
	}

	public async Task<ExportJobStatusDto?> GetBusinessSettingExportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetExportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> DownloadBusinessSettingExportFileAsync(string jobId)
	{
		return await _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<string> StartBusinessSettingImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		var organizationId = _tenantService.GetOrganizationId();
		var userId = _tenantService.GetCurrentUserId();

		return await _importExportService.StartImportJobAsync<CreateBusinessSettingDto>(
			entityType: "BusinessSetting",
			fileStream: fileStream,
			fileName: fileName,
			rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
			{
				if (!rowData.TryGetValue("Setting Key", out var settingKey) || string.IsNullOrWhiteSpace(settingKey))
					return (false, "Setting Key is required", false, false);

				var settingRepo = scopedUnitOfWork.Repository<BusinessSetting>();
				var existingSetting = await settingRepo.FindAsync(s => s.OrganizationId == organizationId && s.SettingKey == settingKey && !s.IsDeleted);

				var newSetting = new BusinessSetting
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					SettingKey = settingKey,
					SettingValue = rowData.GetValueOrDefault("Setting Value")?.ToString() ?? "",
					SettingType = rowData.GetValueOrDefault("Setting Type")?.ToString() ?? "String",
					Description = rowData.GetValueOrDefault("Description")?.ToString(),
					IsActive = rowData.GetValueOrDefault("Is Active")?.ToString()?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true,
					CreatedBy = userId.ToString(),
					CreatedAtUtc = DateTimeOffset.UtcNow,
					ModifiedBy = userId.ToString(),
					ModifiedAtUtc = DateTimeOffset.UtcNow
				};

				if (existingSetting != null)
				{
					if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
						return (false, "Business Setting already exists", false, true);
					
					if (duplicateStrategy == DuplicateHandlingStrategy.Update)
					{
						existingSetting.SettingValue = newSetting.SettingValue;
						existingSetting.SettingType = newSetting.SettingType;
						existingSetting.Description = newSetting.Description;
						existingSetting.IsActive = newSetting.IsActive;
						existingSetting.ModifiedBy = userId.ToString();
						existingSetting.ModifiedAtUtc = DateTimeOffset.UtcNow;
						settingRepo.Update(existingSetting);
						return (true, null, true, false);
					}
				}

				await settingRepo.AddAsync(newSetting);
				return (true, null, false, false);
			},
			duplicateStrategy: duplicateStrategy
		);
	}

	public async Task<ImportJobStatusDto?> GetBusinessSettingImportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetImportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> GetBusinessSettingImportErrorReportAsync(string errorReportId)
	{
		return await _importExportService.GetImportErrorReportAsync(errorReportId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetBusinessSettingImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		return await _importExportService.GetHistoryAsync("BusinessSetting", type, page, pageSize);
	}

	// Currency Pagination
	public async Task<PagedResultDto<CurrencyDto>> GetCurrenciesPagedAsync(string? search, bool? isActive, string? code, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		
		// Determine which organization to query
		Guid filterOrganizationId;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin viewing another organization
			filterOrganizationId = targetOrganizationId.Value;
		}
		else
		{
			// Use current user's organization
			filterOrganizationId = currentUserOrgId;
		}
		
		var query = currencyRepo.GetQueryable();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin can view any organization - ignore tenant filter
			query = query.IgnoreQueryFilters()
				.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}
		else
		{
			// Regular users see only their organization
			query = query.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}

		if (!string.IsNullOrEmpty(search))
		{
			var searchLower = search.ToLower();
			query = query.Where(x =>
				(x.Code != null && x.Code.ToLower().Contains(searchLower)) ||
				(x.Name != null && x.Name.ToLower().Contains(searchLower)) ||
				(x.Symbol != null && x.Symbol.ToLower().Contains(searchLower)));
		}

		if (isActive.HasValue)
			query = query.Where(x => x.IsActive == isActive.Value);

		if (!string.IsNullOrEmpty(code))
			query = query.Where(x => x.Code == code);

		if (createdFrom.HasValue)
			query = query.Where(x => x.CreatedAtUtc >= createdFrom.Value);

		if (createdTo.HasValue)
			query = query.Where(x => x.CreatedAtUtc <= createdTo.Value);

		var totalCount = await query.CountAsync();

		query = sortField?.ToLower() switch
		{
			"code" => sortDirection == "asc" ? query.OrderBy(x => x.Code) : query.OrderByDescending(x => x.Code),
			"name" => sortDirection == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
			"exchangerate" => sortDirection == "asc" ? query.OrderBy(x => x.ExchangeRate) : query.OrderByDescending(x => x.ExchangeRate),
			"isdefault" or "basecurrency" => sortDirection == "asc" ? query.OrderBy(x => x.IsDefault) : query.OrderByDescending(x => x.IsDefault),
			"isactive" => sortDirection == "asc" ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
			_ => sortDirection == "asc" ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc)
		};

		var currencies = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new CurrencyDto
			{
				Id = x.Id,
				Code = x.Code,
				Name = x.Name,
				Symbol = x.Symbol,
				ExchangeRate = x.ExchangeRate,
				DecimalPlaces = x.DecimalPlaces,
				IsActive = x.IsActive,
				IsDefault = x.IsDefault
			})
			.ToListAsync();

		return new PagedResultDto<CurrencyDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = currencies
		};
	}

	// Currency Import/Export/History
	public async Task<byte[]> GetCurrencyImportTemplateAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		var existingCurrencies = await currencyRepo.FindManyAsync(x => x.OrganizationId == organizationId && !x.IsDeleted);
		var currencyCodes = existingCurrencies.Select(x => x.Code).Distinct().ToList();

		return GenerateCurrencyImportTemplate(currencyCodes);
	}

	private byte[] GenerateCurrencyImportTemplate(List<string> currencyCodes)
	{
		using var workbook = new XLWorkbook();
		var importSheet = workbook.Worksheets.Add("Import Data");

		importSheet.Cell(1, 1).Value = "Code";
		importSheet.Cell(1, 2).Value = "Name";
		importSheet.Cell(1, 3).Value = "Symbol";
		importSheet.Cell(1, 4).Value = "Exchange Rate";
		importSheet.Cell(1, 5).Value = "Decimal Places";
		importSheet.Cell(1, 6).Value = "Is Base Currency";
		importSheet.Cell(1, 7).Value = "Is Active";

		var headerRange = importSheet.Range(1, 1, 1, 7);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

		importSheet.Cell(2, 1).Value = currencyCodes.FirstOrDefault() ?? "USD";
		importSheet.Cell(2, 2).Value = "US Dollar";
		importSheet.Cell(2, 3).Value = "$";
		importSheet.Cell(2, 4).Value = 1.0;
		importSheet.Cell(2, 5).Value = 2;
		importSheet.Cell(2, 6).Value = "Yes";
		importSheet.Cell(2, 7).Value = "Active";

		// Reference Data sheet
		// Note: Only include fields that are dropdown/checkbox/toggle in the modal
		// Currency Code is a text input in modal, so not included
		var referenceSheet = workbook.Worksheets.Add("Reference Data");
		
		referenceSheet.Cell(1, 1).Value = "Status";
		referenceSheet.Cell(1, 1).Style.Font.Bold = true;
		referenceSheet.Cell(2, 1).Value = "Active";
		referenceSheet.Cell(3, 1).Value = "Inactive";

		referenceSheet.Cell(1, 2).Value = "Yes/No";
		referenceSheet.Cell(1, 2).Style.Font.Bold = true;
		referenceSheet.Cell(2, 2).Value = "Yes";
		referenceSheet.Cell(3, 2).Value = "No";

		// Named ranges
		// Note: Currency Codes removed from dropdowns as Code is a text input in the modal
		workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 1, 3, 1));
		workbook.NamedRanges.Add("YesNoValues", referenceSheet.Range(2, 2, 3, 2));

		// Data validation (dropdowns) - only for checkbox/toggle fields from modal
		// Is Base Currency (checkbox/toggle in modal)
		var yesNoValidation = importSheet.Range("F2:F1000").SetDataValidation();
		yesNoValidation.List("=YesNoValues", true);
		yesNoValidation.IgnoreBlanks = true;
		yesNoValidation.InCellDropdown = true;

		// Is Active (checkbox/toggle in modal)
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

	public async Task<string> StartCurrencyExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var organizationId = _tenantService.GetOrganizationId();

		return await _importExportService.StartExportJobAsync<Currency>(
			entityType: "Currency",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				scopedTenantService.SetBackgroundContext(organizationId, null, null);

				var repo = scopedUnitOfWork.Repository<Currency>();

				var search = f.GetValueOrDefault("search")?.ToString();
				var isActive = f.GetValueOrDefault("isActive") as bool?;
				var code = f.GetValueOrDefault("code")?.ToString();
				var createdFrom = f.GetValueOrDefault("createdFrom") as DateTimeOffset?;
				var createdTo = f.GetValueOrDefault("createdTo") as DateTimeOffset?;
				var selectedIds = f.GetValueOrDefault("selectedIds");

				var searchLower = search?.ToLower();
				List<Currency> currenciesToExport;

				List<Guid>? idsList = null;
				if (selectedIds != null)
				{
					idsList = selectedIds as List<Guid>;
					if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
					{
						idsList = enumerableIds.ToList();
					}
				}

				if (idsList != null && idsList.Any())
				{
					var query = repo.GetQueryable()
						.Where(c => c.OrganizationId == organizationId && !c.IsDeleted && idsList.Contains(c.Id));
					currenciesToExport = await query.ToListAsync();
				}
				else
				{
					var query = repo.GetQueryable()
						.Where(c => c.OrganizationId == organizationId && !c.IsDeleted &&
							(searchLower == null || (c.Code.ToLower().Contains(searchLower) || c.Name.ToLower().Contains(searchLower) || (c.Symbol != null && c.Symbol.ToLower().Contains(searchLower)))) &&
							(isActive == null || c.IsActive == isActive) &&
							(code == null || c.Code == code) &&
							(createdFrom == null || c.CreatedAtUtc >= createdFrom.Value) &&
							(createdTo == null || c.CreatedAtUtc <= createdTo.Value));
					currenciesToExport = await query.ToListAsync();
				}

				return currenciesToExport;
			},
			filters: filters,
			columnMapper: MapCurrencyToExportColumns
		);
	}

	private Dictionary<string, object> MapCurrencyToExportColumns(Currency currency)
	{
		return new Dictionary<string, object>
		{
			{ "Code", currency.Code ?? "" },
			{ "Name", currency.Name ?? "" },
			{ "Symbol", currency.Symbol ?? "" },
			{ "Exchange Rate", currency.ExchangeRate },
			{ "Decimal Places", currency.DecimalPlaces },
			{ "Is Base Currency", currency.IsDefault ? "Yes" : "No" },
			{ "Is Active", currency.IsActive ? "Active" : "Inactive" }
		};
	}

	public async Task<ExportJobStatusDto?> GetCurrencyExportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetExportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> DownloadCurrencyExportFileAsync(string jobId)
	{
		return await _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<string> StartCurrencyImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		var organizationId = _tenantService.GetOrganizationId();
		var userId = _tenantService.GetCurrentUserId();

		return await _importExportService.StartImportJobAsync<CreateCurrencyDto>(
			entityType: "Currency",
			fileStream: fileStream,
			fileName: fileName,
			rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
			{
				if (!rowData.TryGetValue("Code", out var code) || string.IsNullOrWhiteSpace(code))
					return (false, "Code is required", false, false);

				var currencyRepo = scopedUnitOfWork.Repository<Currency>();
				var existingCurrency = await currencyRepo.FindAsync(c => c.OrganizationId == organizationId && c.Code == code && !c.IsDeleted);

				var newCurrency = new Currency
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					Code = code,
					Name = rowData.GetValueOrDefault("Name")?.ToString() ?? code,
					Symbol = rowData.GetValueOrDefault("Symbol")?.ToString() ?? "",
					ExchangeRate = decimal.TryParse(rowData.GetValueOrDefault("Exchange Rate")?.ToString(), out var rate) ? rate : 1.0m,
					DecimalPlaces = int.TryParse(rowData.GetValueOrDefault("Decimal Places")?.ToString(), out var places) ? places : 2,
					IsDefault = rowData.GetValueOrDefault("Is Base Currency")?.ToString()?.Equals("Yes", StringComparison.OrdinalIgnoreCase) ?? false,
					IsActive = rowData.GetValueOrDefault("Is Active")?.ToString()?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true,
					CreatedBy = userId.ToString(),
					CreatedAtUtc = DateTimeOffset.UtcNow,
					ModifiedBy = userId.ToString(),
					ModifiedAtUtc = DateTimeOffset.UtcNow
				};

				if (existingCurrency != null)
				{
					if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
						return (false, "Currency already exists", false, true);
					
					if (duplicateStrategy == DuplicateHandlingStrategy.Update)
					{
						existingCurrency.Name = newCurrency.Name;
						existingCurrency.Symbol = newCurrency.Symbol;
						existingCurrency.ExchangeRate = newCurrency.ExchangeRate;
						existingCurrency.DecimalPlaces = newCurrency.DecimalPlaces;
						existingCurrency.IsDefault = newCurrency.IsDefault;
						existingCurrency.IsActive = newCurrency.IsActive;
						existingCurrency.ModifiedBy = userId.ToString();
						existingCurrency.ModifiedAtUtc = DateTimeOffset.UtcNow;
						currencyRepo.Update(existingCurrency);
						return (true, null, true, false);
					}
				}

				await currencyRepo.AddAsync(newCurrency);
				return (true, null, false, false);
			},
			duplicateStrategy: duplicateStrategy
		);
	}

	public async Task<ImportJobStatusDto?> GetCurrencyImportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetImportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> GetCurrencyImportErrorReportAsync(string errorReportId)
	{
		return await _importExportService.GetImportErrorReportAsync(errorReportId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetCurrencyImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		return await _importExportService.GetHistoryAsync("Currency", type, page, pageSize);
	}

	// Currency Filter Options
	public async Task<List<string>> GetCurrencyCodesAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var currencyRepo = _unitOfWork.Repository<Currency>();
		
		return await currencyRepo.GetQueryable()
			.Where(x => x.OrganizationId == organizationId && !x.IsDeleted && !string.IsNullOrEmpty(x.Code))
			.Select(x => x.Code)
			.Distinct()
			.OrderBy(x => x)
			.ToListAsync();
	}

	// Tax Rate Pagination
	public async Task<PagedResultDto<TaxRateDto>> GetTaxRatesPagedAsync(string? search, bool? isActive, string? taxType, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		
		// Determine which organization to query
		Guid filterOrganizationId;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin viewing another organization
			filterOrganizationId = targetOrganizationId.Value;
		}
		else
		{
			// Use current user's organization
			filterOrganizationId = currentUserOrgId;
		}
		
		var query = taxRateRepo.GetQueryable();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin can view any organization - ignore tenant filter
			query = query.IgnoreQueryFilters()
				.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}
		else
		{
			// Regular users see only their organization
			query = query.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}

		if (!string.IsNullOrEmpty(search))
		{
			var searchLower = search.ToLower();
			query = query.Where(x =>
				(x.Name != null && x.Name.ToLower().Contains(searchLower)) ||
				(x.Description != null && x.Description.ToLower().Contains(searchLower)) ||
				(x.TaxType != null && x.TaxType.ToLower().Contains(searchLower)));
		}

		if (isActive.HasValue)
			query = query.Where(x => x.IsActive == isActive.Value);

		if (!string.IsNullOrEmpty(taxType))
			query = query.Where(x => x.TaxType == taxType);

		if (createdFrom.HasValue)
			query = query.Where(x => x.CreatedAtUtc >= createdFrom.Value);

		if (createdTo.HasValue)
			query = query.Where(x => x.CreatedAtUtc <= createdTo.Value);

		var totalCount = await query.CountAsync();

		query = sortField?.ToLower() switch
		{
			"name" => sortDirection == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
			"rate" => sortDirection == "asc" ? query.OrderBy(x => x.Rate) : query.OrderByDescending(x => x.Rate),
			"taxtype" => sortDirection == "asc" ? query.OrderBy(x => x.TaxType) : query.OrderByDescending(x => x.TaxType),
			"isdefault" => sortDirection == "asc" ? query.OrderBy(x => x.IsDefault) : query.OrderByDescending(x => x.IsDefault),
			"isactive" => sortDirection == "asc" ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
			_ => sortDirection == "asc" ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc)
		};

		var taxRates = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new TaxRateDto
			{
				Id = x.Id,
				OrganizationId = x.OrganizationId,
				Name = x.Name,
				Description = x.Description,
				Rate = x.Rate,
				TaxType = x.TaxType,
				IsActive = x.IsActive,
				IsDefault = x.IsDefault,
				EffectiveFrom = x.EffectiveFrom,
				EffectiveTo = x.EffectiveTo
			})
			.ToListAsync();

		return new PagedResultDto<TaxRateDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = taxRates
		};
	}

	// Tax Rate Import/Export/History
	public async Task<byte[]> GetTaxRateImportTemplateAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		var existingTaxRates = await taxRateRepo.FindManyAsync(x => x.OrganizationId == organizationId && !x.IsDeleted);
		var taxTypes = existingTaxRates.Select(x => x.TaxType).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();

		return GenerateTaxRateImportTemplate(taxTypes);
	}

	private byte[] GenerateTaxRateImportTemplate(List<string> taxTypes)
	{
		using var workbook = new XLWorkbook();
		var importSheet = workbook.Worksheets.Add("Import Data");

		importSheet.Cell(1, 1).Value = "Name";
		importSheet.Cell(1, 2).Value = "Description";
		importSheet.Cell(1, 3).Value = "Rate";
		importSheet.Cell(1, 4).Value = "Tax Type";
		importSheet.Cell(1, 5).Value = "Is Default";
		importSheet.Cell(1, 6).Value = "Is Active";
		importSheet.Cell(1, 7).Value = "Effective From";
		importSheet.Cell(1, 8).Value = "Effective To";

		var headerRange = importSheet.Range(1, 1, 1, 8);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

		importSheet.Cell(2, 1).Value = "Standard Tax";
		importSheet.Cell(2, 2).Value = "Standard tax rate";
		importSheet.Cell(2, 3).Value = 8.5;
		importSheet.Cell(2, 4).Value = taxTypes.FirstOrDefault() ?? "Sales";
		importSheet.Cell(2, 5).Value = "No";
		importSheet.Cell(2, 6).Value = "Active";
		importSheet.Cell(2, 7).Value = DateTime.Now.ToString("yyyy-MM-dd");
		importSheet.Cell(2, 8).Value = "";

		var referenceSheet = workbook.Worksheets.Add("Reference Data");
		if (taxTypes.Any())
		{
			referenceSheet.Cell(1, 1).Value = "Tax Types";
			referenceSheet.Cell(1, 1).Style.Font.Bold = true;
			for (int i = 0; i < taxTypes.Count; i++)
			{
				referenceSheet.Cell(i + 2, 1).Value = taxTypes[i];
			}
		}

		referenceSheet.Cell(1, 2).Value = "Status";
		referenceSheet.Cell(1, 2).Style.Font.Bold = true;
		referenceSheet.Cell(2, 2).Value = "Active";
		referenceSheet.Cell(3, 2).Value = "Inactive";

		referenceSheet.Cell(1, 3).Value = "Yes/No";
		referenceSheet.Cell(1, 3).Style.Font.Bold = true;
		referenceSheet.Cell(2, 3).Value = "Yes";
		referenceSheet.Cell(3, 3).Value = "No";

		// Named ranges
		if (taxTypes.Any())
			workbook.NamedRanges.Add("TaxTypes", referenceSheet.Range(2, 1, taxTypes.Count + 1, 1));
		workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 2, 3, 2));
		workbook.NamedRanges.Add("YesNoValues", referenceSheet.Range(2, 3, 3, 3));

		// Data validation (dropdowns)
		if (taxTypes.Any())
		{
			var taxTypeValidation = importSheet.Range("D2:D1000").SetDataValidation();
			taxTypeValidation.List("=TaxTypes", true);
			taxTypeValidation.IgnoreBlanks = true;
			taxTypeValidation.InCellDropdown = true;
		}

		var yesNoValidation = importSheet.Range("E2:E1000").SetDataValidation();
		yesNoValidation.List("=YesNoValues", true);
		yesNoValidation.IgnoreBlanks = true;
		yesNoValidation.InCellDropdown = true;

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

	public async Task<string> StartTaxRateExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var organizationId = _tenantService.GetOrganizationId();

		return await _importExportService.StartExportJobAsync<TaxRate>(
			entityType: "TaxRate",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				scopedTenantService.SetBackgroundContext(organizationId, null, null);

				var repo = scopedUnitOfWork.Repository<TaxRate>();

				var search = f.GetValueOrDefault("search")?.ToString();
				var isActive = f.GetValueOrDefault("isActive") as bool?;
				var taxType = f.GetValueOrDefault("taxType")?.ToString();
				var createdFrom = f.GetValueOrDefault("createdFrom") as DateTimeOffset?;
				var createdTo = f.GetValueOrDefault("createdTo") as DateTimeOffset?;
				var selectedIds = f.GetValueOrDefault("selectedIds");

				var searchLower = search?.ToLower();
				List<TaxRate> taxRatesToExport;

				List<Guid>? idsList = null;
				if (selectedIds != null)
				{
					idsList = selectedIds as List<Guid>;
					if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
					{
						idsList = enumerableIds.ToList();
					}
				}

				if (idsList != null && idsList.Any())
				{
					var query = repo.GetQueryable()
						.Where(t => t.OrganizationId == organizationId && !t.IsDeleted && idsList.Contains(t.Id));
					taxRatesToExport = await query.ToListAsync();
				}
				else
				{
					var query = repo.GetQueryable()
						.Where(t => t.OrganizationId == organizationId && !t.IsDeleted &&
							(searchLower == null || (t.Name.ToLower().Contains(searchLower) || (t.Description != null && t.Description.ToLower().Contains(searchLower)) || (t.TaxType != null && t.TaxType.ToLower().Contains(searchLower)))) &&
							(isActive == null || t.IsActive == isActive) &&
							(taxType == null || t.TaxType == taxType) &&
							(createdFrom == null || t.CreatedAtUtc >= createdFrom.Value) &&
							(createdTo == null || t.CreatedAtUtc <= createdTo.Value));
					taxRatesToExport = await query.ToListAsync();
				}

				return taxRatesToExport;
			},
			filters: filters,
			columnMapper: MapTaxRateToExportColumns
		);
	}

	private Dictionary<string, object> MapTaxRateToExportColumns(TaxRate taxRate)
	{
		return new Dictionary<string, object>
		{
			{ "Name", taxRate.Name ?? "" },
			{ "Description", taxRate.Description ?? "" },
			{ "Rate", taxRate.Rate },
			{ "Tax Type", taxRate.TaxType ?? "" },
			{ "Is Default", taxRate.IsDefault ? "Yes" : "No" },
			{ "Is Active", taxRate.IsActive ? "Active" : "Inactive" },
			{ "Effective From", taxRate.EffectiveFrom?.ToString("yyyy-MM-dd") ?? "" },
			{ "Effective To", taxRate.EffectiveTo?.ToString("yyyy-MM-dd") ?? "" }
		};
	}

	public async Task<ExportJobStatusDto?> GetTaxRateExportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetExportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> DownloadTaxRateExportFileAsync(string jobId)
	{
		return await _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<string> StartTaxRateImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		var organizationId = _tenantService.GetOrganizationId();
		var userId = _tenantService.GetCurrentUserId();

		return await _importExportService.StartImportJobAsync<CreateTaxRateDto>(
			entityType: "TaxRate",
			fileStream: fileStream,
			fileName: fileName,
			rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
			{
				if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
					return (false, "Name is required", false, false);

				var taxRateRepo = scopedUnitOfWork.Repository<TaxRate>();
				var existingTaxRate = await taxRateRepo.FindAsync(t => t.OrganizationId == organizationId && t.Name == name && !t.IsDeleted);

				var newTaxRate = new TaxRate
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					Name = name,
					Description = rowData.GetValueOrDefault("Description")?.ToString(),
					Rate = decimal.TryParse(rowData.GetValueOrDefault("Rate")?.ToString(), out var rate) ? rate : 0m,
					TaxType = rowData.GetValueOrDefault("Tax Type")?.ToString() ?? "Sales",
					IsDefault = rowData.GetValueOrDefault("Is Default")?.ToString()?.Equals("Yes", StringComparison.OrdinalIgnoreCase) ?? false,
					IsActive = rowData.GetValueOrDefault("Is Active")?.ToString()?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true,
					EffectiveFrom = DateTimeOffset.TryParse(rowData.GetValueOrDefault("Effective From")?.ToString(), out var from) ? from : null,
					EffectiveTo = DateTimeOffset.TryParse(rowData.GetValueOrDefault("Effective To")?.ToString(), out var to) ? to : null,
					CreatedBy = userId.ToString(),
					CreatedAtUtc = DateTimeOffset.UtcNow,
					ModifiedBy = userId.ToString(),
					ModifiedAtUtc = DateTimeOffset.UtcNow
				};

				if (existingTaxRate != null)
				{
					if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
						return (false, "Tax Rate already exists", false, true);
					
					if (duplicateStrategy == DuplicateHandlingStrategy.Update)
					{
						existingTaxRate.Description = newTaxRate.Description;
						existingTaxRate.Rate = newTaxRate.Rate;
						existingTaxRate.TaxType = newTaxRate.TaxType;
						existingTaxRate.IsDefault = newTaxRate.IsDefault;
						existingTaxRate.IsActive = newTaxRate.IsActive;
						existingTaxRate.EffectiveFrom = newTaxRate.EffectiveFrom;
						existingTaxRate.EffectiveTo = newTaxRate.EffectiveTo;
						existingTaxRate.ModifiedBy = userId.ToString();
						existingTaxRate.ModifiedAtUtc = DateTimeOffset.UtcNow;
						taxRateRepo.Update(existingTaxRate);
						return (true, null, true, false);
					}
				}

				await taxRateRepo.AddAsync(newTaxRate);
				return (true, null, false, false);
			},
			duplicateStrategy: duplicateStrategy
		);
	}

	public async Task<ImportJobStatusDto?> GetTaxRateImportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetImportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> GetTaxRateImportErrorReportAsync(string errorReportId)
	{
		return await _importExportService.GetImportErrorReportAsync(errorReportId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetTaxRateImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		return await _importExportService.GetHistoryAsync("TaxRate", type, page, pageSize);
	}

	// Tax Rate Filter Options
	public async Task<List<string>> GetTaxTypesAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var taxRateRepo = _unitOfWork.Repository<TaxRate>();
		
		var taxTypes = await taxRateRepo.GetQueryable()
			.Where(x => x.OrganizationId == organizationId && !x.IsDeleted && !string.IsNullOrEmpty(x.TaxType))
			.Select(x => x.TaxType!)
			.Distinct()
			.OrderBy(x => x)
			.ToListAsync();
		
		// If no tax types found, return default list
		if (taxTypes.Count == 0)
		{
			return new List<string> { "Sales Tax", "VAT", "GST", "Service Tax", "Other" };
		}
		
		return taxTypes;
	}

	// Notification Template Pagination
	public async Task<PagedResultDto<NotificationTemplateDto>> GetNotificationTemplatesPagedAsync(string? search, bool? isActive, string? category, string? templateType, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		
		// Determine which organization to query
		Guid filterOrganizationId;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin viewing another organization
			filterOrganizationId = targetOrganizationId.Value;
		}
		else
		{
			// Use current user's organization
			filterOrganizationId = currentUserOrgId;
		}
		
		var query = templateRepo.GetQueryable();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin can view any organization - ignore tenant filter
			query = query.IgnoreQueryFilters()
				.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}
		else
		{
			// Regular users see only their organization
			query = query.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}

		if (!string.IsNullOrEmpty(search))
		{
			var searchLower = search.ToLower();
			query = query.Where(x =>
				(x.Name != null && x.Name.ToLower().Contains(searchLower)) ||
				(x.Subject != null && x.Subject.ToLower().Contains(searchLower)) ||
				(x.Description != null && x.Description.ToLower().Contains(searchLower)));
		}

		if (isActive.HasValue)
			query = query.Where(x => x.IsActive == isActive.Value);

		if (!string.IsNullOrEmpty(category))
			query = query.Where(x => x.Category == category);

		if (!string.IsNullOrEmpty(templateType))
			query = query.Where(x => x.TemplateType == templateType);

		if (createdFrom.HasValue)
			query = query.Where(x => x.CreatedAtUtc >= createdFrom.Value);

		if (createdTo.HasValue)
			query = query.Where(x => x.CreatedAtUtc <= createdTo.Value);

		var totalCount = await query.CountAsync();

		query = sortField?.ToLower() switch
		{
			"name" => sortDirection == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
			"subject" => sortDirection == "asc" ? query.OrderBy(x => x.Subject) : query.OrderByDescending(x => x.Subject),
			"category" => sortDirection == "asc" ? query.OrderBy(x => x.Category) : query.OrderByDescending(x => x.Category),
			"isactive" => sortDirection == "asc" ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
			_ => sortDirection == "asc" ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc)
		};

		var templates = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new NotificationTemplateDto
			{
				Id = x.Id,
				OrganizationId = x.OrganizationId,
				Name = x.Name,
				Description = x.Description,
				Subject = x.Subject,
				Body = x.Body,
				TemplateType = x.TemplateType,
				Variables = x.Variables,
				IsActive = x.IsActive,
				IsSystemTemplate = x.IsSystemTemplate,
				Category = x.Category
			})
			.ToListAsync();

		return new PagedResultDto<NotificationTemplateDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = templates
		};
	}

	// Notification Template Import/Export/History
	public async Task<byte[]> GetNotificationTemplateImportTemplateAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		var existingTemplates = await templateRepo.FindManyAsync(x => x.OrganizationId == organizationId && !x.IsDeleted);
		var categories = existingTemplates.Select(x => x.Category).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();
		var templateTypes = existingTemplates.Select(x => x.TemplateType).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();

		return GenerateNotificationTemplateImportTemplate(categories, templateTypes);
	}

	private byte[] GenerateNotificationTemplateImportTemplate(List<string> categories, List<string> templateTypes)
	{
		using var workbook = new XLWorkbook();
		var importSheet = workbook.Worksheets.Add("Import Data");

		importSheet.Cell(1, 1).Value = "Name";
		importSheet.Cell(1, 2).Value = "Subject";
		importSheet.Cell(1, 3).Value = "Body";
		importSheet.Cell(1, 4).Value = "Template Type";
		importSheet.Cell(1, 5).Value = "Category";
		importSheet.Cell(1, 6).Value = "Description";
		importSheet.Cell(1, 7).Value = "Variables";
		importSheet.Cell(1, 8).Value = "Is Active";

		var headerRange = importSheet.Range(1, 1, 1, 8);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

		importSheet.Cell(2, 1).Value = "Order Confirmation";
		importSheet.Cell(2, 2).Value = "Your order has been confirmed";
		importSheet.Cell(2, 3).Value = "Dear {{name}}, your order {{orderId}} has been confirmed.";
		importSheet.Cell(2, 4).Value = templateTypes.FirstOrDefault() ?? "EMAIL";
		importSheet.Cell(2, 5).Value = categories.FirstOrDefault() ?? "ORDER";
		importSheet.Cell(2, 6).Value = "Order confirmation template";
		importSheet.Cell(2, 7).Value = "[\"name\", \"orderId\"]";
		importSheet.Cell(2, 8).Value = "Active";

		var referenceSheet = workbook.Worksheets.Add("Reference Data");
		if (categories.Any())
		{
			referenceSheet.Cell(1, 1).Value = "Categories";
			referenceSheet.Cell(1, 1).Style.Font.Bold = true;
			for (int i = 0; i < categories.Count; i++)
			{
				referenceSheet.Cell(i + 2, 1).Value = categories[i];
			}
		}

		if (templateTypes.Any())
		{
			referenceSheet.Cell(1, 2).Value = "Template Types";
			referenceSheet.Cell(1, 2).Style.Font.Bold = true;
			for (int i = 0; i < templateTypes.Count; i++)
			{
				referenceSheet.Cell(i + 2, 2).Value = templateTypes[i];
			}
		}

		referenceSheet.Cell(1, 3).Value = "Status";
		referenceSheet.Cell(1, 3).Style.Font.Bold = true;
		referenceSheet.Cell(2, 3).Value = "Active";
		referenceSheet.Cell(3, 3).Value = "Inactive";

		// Named ranges
		if (categories.Any())
			workbook.NamedRanges.Add("Categories", referenceSheet.Range(2, 1, categories.Count + 1, 1));
		if (templateTypes.Any())
			workbook.NamedRanges.Add("TemplateTypes", referenceSheet.Range(2, 2, templateTypes.Count + 1, 2));
		workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 3, 3, 3));

		// Data validation (dropdowns)
		if (templateTypes.Any())
		{
			var templateTypeValidation = importSheet.Range("D2:D1000").SetDataValidation();
			templateTypeValidation.List("=TemplateTypes", true);
			templateTypeValidation.IgnoreBlanks = true;
			templateTypeValidation.InCellDropdown = true;
		}

		if (categories.Any())
		{
			var categoryValidation = importSheet.Range("E2:E1000").SetDataValidation();
			categoryValidation.List("=Categories", true);
			categoryValidation.IgnoreBlanks = true;
			categoryValidation.InCellDropdown = true;
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

	public async Task<string> StartNotificationTemplateExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var organizationId = _tenantService.GetOrganizationId();

		return await _importExportService.StartExportJobAsync<NotificationTemplate>(
			entityType: "NotificationTemplate",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				scopedTenantService.SetBackgroundContext(organizationId, null, null);

				var repo = scopedUnitOfWork.Repository<NotificationTemplate>();

				var search = f.GetValueOrDefault("search")?.ToString();
				var isActive = f.GetValueOrDefault("isActive") as bool?;
				var category = f.GetValueOrDefault("category")?.ToString();
				var templateType = f.GetValueOrDefault("templateType")?.ToString();
				var createdFrom = f.GetValueOrDefault("createdFrom") as DateTimeOffset?;
				var createdTo = f.GetValueOrDefault("createdTo") as DateTimeOffset?;
				var selectedIds = f.GetValueOrDefault("selectedIds");

				var searchLower = search?.ToLower();
				List<NotificationTemplate> templatesToExport;

				List<Guid>? idsList = null;
				if (selectedIds != null)
				{
					idsList = selectedIds as List<Guid>;
					if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
					{
						idsList = enumerableIds.ToList();
					}
				}

				if (idsList != null && idsList.Any())
				{
					var query = repo.GetQueryable()
						.Where(t => t.OrganizationId == organizationId && !t.IsDeleted && idsList.Contains(t.Id));
					templatesToExport = await query.ToListAsync();
				}
				else
				{
					var query = repo.GetQueryable()
						.Where(t => t.OrganizationId == organizationId && !t.IsDeleted &&
							(searchLower == null || (t.Name.ToLower().Contains(searchLower) || (t.Subject != null && t.Subject.ToLower().Contains(searchLower)) || (t.Description != null && t.Description.ToLower().Contains(searchLower)))) &&
							(isActive == null || t.IsActive == isActive) &&
							(category == null || t.Category == category) &&
							(templateType == null || t.TemplateType == templateType) &&
							(createdFrom == null || t.CreatedAtUtc >= createdFrom.Value) &&
							(createdTo == null || t.CreatedAtUtc <= createdTo.Value));
					templatesToExport = await query.ToListAsync();
				}

				return templatesToExport;
			},
			filters: filters,
			columnMapper: MapNotificationTemplateToExportColumns
		);
	}

	private Dictionary<string, object> MapNotificationTemplateToExportColumns(NotificationTemplate template)
	{
		return new Dictionary<string, object>
		{
			{ "Name", template.Name ?? "" },
			{ "Subject", template.Subject ?? "" },
			{ "Body", template.Body ?? "" },
			{ "Template Type", template.TemplateType ?? "" },
			{ "Category", template.Category ?? "" },
			{ "Description", template.Description ?? "" },
			{ "Variables", template.Variables ?? "" },
			{ "Is Active", template.IsActive ? "Active" : "Inactive" }
		};
	}

	public async Task<ExportJobStatusDto?> GetNotificationTemplateExportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetExportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> DownloadNotificationTemplateExportFileAsync(string jobId)
	{
		return await _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<string> StartNotificationTemplateImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		var organizationId = _tenantService.GetOrganizationId();
		var userId = _tenantService.GetCurrentUserId();

		return await _importExportService.StartImportJobAsync<CreateNotificationTemplateDto>(
			entityType: "NotificationTemplate",
			fileStream: fileStream,
			fileName: fileName,
			rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
			{
				if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
					return (false, "Name is required", false, false);

				var templateRepo = scopedUnitOfWork.Repository<NotificationTemplate>();
				var existingTemplate = await templateRepo.FindAsync(t => t.OrganizationId == organizationId && t.Name == name && !t.IsDeleted);

				var newTemplate = new NotificationTemplate
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					Name = name,
					Subject = rowData.GetValueOrDefault("Subject")?.ToString() ?? "",
					Body = rowData.GetValueOrDefault("Body")?.ToString() ?? "",
					TemplateType = rowData.GetValueOrDefault("Template Type")?.ToString() ?? "EMAIL",
					Category = rowData.GetValueOrDefault("Category")?.ToString(),
					Description = rowData.GetValueOrDefault("Description")?.ToString(),
					Variables = rowData.GetValueOrDefault("Variables")?.ToString(),
					IsActive = rowData.GetValueOrDefault("Is Active")?.ToString()?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true,
					IsSystemTemplate = false,
					CreatedBy = userId.ToString(),
					CreatedAtUtc = DateTimeOffset.UtcNow,
					ModifiedBy = userId.ToString(),
					ModifiedAtUtc = DateTimeOffset.UtcNow
				};

				if (existingTemplate != null)
				{
					if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
						return (false, "Notification Template already exists", false, true);
					
					if (duplicateStrategy == DuplicateHandlingStrategy.Update)
					{
						existingTemplate.Subject = newTemplate.Subject;
						existingTemplate.Body = newTemplate.Body;
						existingTemplate.TemplateType = newTemplate.TemplateType;
						existingTemplate.Category = newTemplate.Category;
						existingTemplate.Description = newTemplate.Description;
						existingTemplate.Variables = newTemplate.Variables;
						existingTemplate.IsActive = newTemplate.IsActive;
						existingTemplate.ModifiedBy = userId.ToString();
						existingTemplate.ModifiedAtUtc = DateTimeOffset.UtcNow;
						templateRepo.Update(existingTemplate);
						return (true, null, true, false);
					}
				}

				await templateRepo.AddAsync(newTemplate);
				return (true, null, false, false);
			},
			duplicateStrategy: duplicateStrategy
		);
	}

	public async Task<ImportJobStatusDto?> GetNotificationTemplateImportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetImportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> GetNotificationTemplateImportErrorReportAsync(string errorReportId)
	{
		return await _importExportService.GetImportErrorReportAsync(errorReportId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetNotificationTemplateImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		return await _importExportService.GetHistoryAsync("NotificationTemplate", type, page, pageSize);
	}

	// Notification Template Filter Options
	public async Task<List<string>> GetNotificationTemplateCategoriesAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var templateRepo = _unitOfWork.Repository<NotificationTemplate>();
		
		var categories = await templateRepo.GetQueryable()
			.Where(x => x.OrganizationId == organizationId && !x.IsDeleted && !string.IsNullOrEmpty(x.Category))
			.Select(x => x.Category!)
			.Distinct()
			.OrderBy(x => x)
			.ToListAsync();
		
		// If no categories found, return default list
		if (categories.Count == 0)
		{
			return new List<string> { "ORDER", "INVENTORY", "USER", "SYSTEM", "PAYMENT", "SHIPPING" };
		}
		
		return categories;
	}

	// Integration Setting Pagination
	public async Task<PagedResultDto<IntegrationSettingDto>> GetIntegrationSettingsPagedAsync(string? search, bool? isActive, string? provider, string? integrationType, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, int page, int pageSize, string? sortField = "createdAtUtc", string? sortDirection = "desc", Guid? targetOrganizationId = null)
	{
		var currentUserOrgId = _tenantService.GetOrganizationId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		
		// Determine which organization to query
		Guid filterOrganizationId;
		var isSystemAdmin = await _userContextService.IsSystemAdministratorAsync();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin viewing another organization
			filterOrganizationId = targetOrganizationId.Value;
		}
		else
		{
			// Use current user's organization
			filterOrganizationId = currentUserOrgId;
		}
		
		var query = integrationRepo.GetQueryable();
		
		if (isSystemAdmin && targetOrganizationId.HasValue)
		{
			// System admin can view any organization - ignore tenant filter
			query = query.IgnoreQueryFilters()
				.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}
		else
		{
			// Regular users see only their organization
			query = query.Where(x => x.OrganizationId == filterOrganizationId && !x.IsDeleted);
		}

		if (!string.IsNullOrEmpty(search))
		{
			var searchLower = search.ToLower();
			query = query.Where(x =>
				(x.Name != null && x.Name.ToLower().Contains(searchLower)) ||
				(x.Description != null && x.Description.ToLower().Contains(searchLower)) ||
				(x.IntegrationType != null && x.IntegrationType.ToLower().Contains(searchLower)) ||
				(x.Provider != null && x.Provider.ToLower().Contains(searchLower)));
		}

		if (isActive.HasValue)
			query = query.Where(x => x.IsActive == isActive.Value);

		if (!string.IsNullOrEmpty(provider))
			query = query.Where(x => x.Provider == provider);

		if (!string.IsNullOrEmpty(integrationType))
			query = query.Where(x => x.IntegrationType == integrationType);

		if (createdFrom.HasValue)
			query = query.Where(x => x.CreatedAtUtc >= createdFrom.Value);

		if (createdTo.HasValue)
			query = query.Where(x => x.CreatedAtUtc <= createdTo.Value);

		var totalCount = await query.CountAsync();

		query = sortField?.ToLower() switch
		{
			"name" => sortDirection == "asc" ? query.OrderBy(x => x.Name) : query.OrderByDescending(x => x.Name),
			"provider" => sortDirection == "asc" ? query.OrderBy(x => x.Provider) : query.OrderByDescending(x => x.Provider),
			"isenabled" => sortDirection == "asc" ? query.OrderBy(x => x.IsEnabled) : query.OrderByDescending(x => x.IsEnabled),
			"isactive" => sortDirection == "asc" ? query.OrderBy(x => x.IsActive) : query.OrderByDescending(x => x.IsActive),
			_ => sortDirection == "asc" ? query.OrderBy(x => x.CreatedAtUtc) : query.OrderByDescending(x => x.CreatedAtUtc)
		};

		var integrations = await query
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new IntegrationSettingDto
			{
				Id = x.Id,
				OrganizationId = x.OrganizationId,
				Name = x.Name,
				Description = x.Description,
				IntegrationType = x.IntegrationType,
				Provider = x.Provider,
				Configuration = x.Configuration,
				IsActive = x.IsActive,
				IsEnabled = x.IsEnabled,
				LastSyncAt = x.LastSyncAt,
				LastSyncStatus = x.LastSyncStatus,
				ErrorMessage = x.ErrorMessage
			})
			.ToListAsync();

		return new PagedResultDto<IntegrationSettingDto>
		{
			Page = page,
			PageSize = pageSize,
			TotalCount = totalCount,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items = integrations
		};
	}

	// Integration Setting Import/Export/History
	public async Task<byte[]> GetIntegrationSettingImportTemplateAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		var existingIntegrations = await integrationRepo.FindManyAsync(x => x.OrganizationId == organizationId && !x.IsDeleted);
		var providers = existingIntegrations.Select(x => x.Provider).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();
		var integrationTypes = existingIntegrations.Select(x => x.IntegrationType).Distinct().Where(x => !string.IsNullOrEmpty(x)).ToList();

		return GenerateIntegrationSettingImportTemplate(providers, integrationTypes);
	}

	private byte[] GenerateIntegrationSettingImportTemplate(List<string> providers, List<string> integrationTypes)
	{
		using var workbook = new XLWorkbook();
		var importSheet = workbook.Worksheets.Add("Import Data");

		importSheet.Cell(1, 1).Value = "Name";
		importSheet.Cell(1, 2).Value = "Description";
		importSheet.Cell(1, 3).Value = "Integration Type";
		importSheet.Cell(1, 4).Value = "Provider";
		importSheet.Cell(1, 5).Value = "Configuration";
		importSheet.Cell(1, 6).Value = "Is Enabled";
		importSheet.Cell(1, 7).Value = "Is Active";

		var headerRange = importSheet.Range(1, 1, 1, 7);
		headerRange.Style.Font.Bold = true;
		headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

		importSheet.Cell(2, 1).Value = "ERP Integration";
		importSheet.Cell(2, 2).Value = "ERP system integration";
		importSheet.Cell(2, 3).Value = integrationTypes.FirstOrDefault() ?? "ERP";
		importSheet.Cell(2, 4).Value = providers.FirstOrDefault() ?? "SAP";
		importSheet.Cell(2, 5).Value = "{}";
		importSheet.Cell(2, 6).Value = "Yes";
		importSheet.Cell(2, 7).Value = "Active";

		// Reference Data sheet
		// Note: Only include fields that are dropdown/checkbox/toggle in the modal
		// Provider is a text input in modal, so not included
		var referenceSheet = workbook.Worksheets.Add("Reference Data");
		
		if (integrationTypes.Any())
		{
			referenceSheet.Cell(1, 1).Value = "Integration Types";
			referenceSheet.Cell(1, 1).Style.Font.Bold = true;
			for (int i = 0; i < integrationTypes.Count; i++)
			{
				referenceSheet.Cell(i + 2, 1).Value = integrationTypes[i];
			}
		}

		referenceSheet.Cell(1, 2).Value = "Status";
		referenceSheet.Cell(1, 2).Style.Font.Bold = true;
		referenceSheet.Cell(2, 2).Value = "Active";
		referenceSheet.Cell(3, 2).Value = "Inactive";

		referenceSheet.Cell(1, 3).Value = "Yes/No";
		referenceSheet.Cell(1, 3).Style.Font.Bold = true;
		referenceSheet.Cell(2, 3).Value = "Yes";
		referenceSheet.Cell(3, 3).Value = "No";

		// Named ranges
		// Note: Providers removed from dropdowns as Provider is a text input in the modal
		if (integrationTypes.Any())
			workbook.NamedRanges.Add("IntegrationTypes", referenceSheet.Range(2, 1, integrationTypes.Count + 1, 1));
		workbook.NamedRanges.Add("StatusValues", referenceSheet.Range(2, 2, 3, 2));
		workbook.NamedRanges.Add("YesNoValues", referenceSheet.Range(2, 3, 3, 3));

		// Data validation (dropdowns) - only for dropdown/checkbox/toggle fields from modal
		// Integration Type (dropdown in modal)
		if (integrationTypes.Any())
		{
			var integrationTypeValidation = importSheet.Range("C2:C1000").SetDataValidation();
			integrationTypeValidation.List("=IntegrationTypes", true);
			integrationTypeValidation.IgnoreBlanks = true;
			integrationTypeValidation.InCellDropdown = true;
		}

		// Is Enabled (checkbox/toggle in modal)
		var yesNoValidation = importSheet.Range("F2:F1000").SetDataValidation();
		yesNoValidation.List("=YesNoValues", true);
		yesNoValidation.IgnoreBlanks = true;
		yesNoValidation.InCellDropdown = true;

		// Is Active (checkbox/toggle in modal)
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

	public async Task<string> StartIntegrationSettingExportJobAsync(ExportFormat format, Dictionary<string, object?> filters)
	{
		var organizationId = _tenantService.GetOrganizationId();

		return await _importExportService.StartExportJobAsync<IntegrationSetting>(
			entityType: "IntegrationSetting",
			format: format,
			dataFetcher: async (f) =>
			{
				using var scope = _serviceScopeFactory.CreateScope();
				var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
				var scopedTenantService = scope.ServiceProvider.GetRequiredService<ICurrentTenantService>();
				scopedTenantService.SetBackgroundContext(organizationId, null, null);

				var repo = scopedUnitOfWork.Repository<IntegrationSetting>();

				var search = f.GetValueOrDefault("search")?.ToString();
				var isActive = f.GetValueOrDefault("isActive") as bool?;
				var provider = f.GetValueOrDefault("provider")?.ToString();
				var createdFrom = f.GetValueOrDefault("createdFrom") as DateTimeOffset?;
				var createdTo = f.GetValueOrDefault("createdTo") as DateTimeOffset?;
				var selectedIds = f.GetValueOrDefault("selectedIds");

				var searchLower = search?.ToLower();
				List<IntegrationSetting> integrationsToExport;

				List<Guid>? idsList = null;
				if (selectedIds != null)
				{
					idsList = selectedIds as List<Guid>;
					if (idsList == null && selectedIds is IEnumerable<Guid> enumerableIds)
					{
						idsList = enumerableIds.ToList();
					}
				}

				if (idsList != null && idsList.Any())
				{
					var query = repo.GetQueryable()
						.Where(i => i.OrganizationId == organizationId && !i.IsDeleted && idsList.Contains(i.Id));
					integrationsToExport = await query.ToListAsync();
				}
				else
				{
					var query = repo.GetQueryable()
						.Where(i => i.OrganizationId == organizationId && !i.IsDeleted &&
							(searchLower == null || (i.Name.ToLower().Contains(searchLower) || (i.Description != null && i.Description.ToLower().Contains(searchLower)) || (i.IntegrationType != null && i.IntegrationType.ToLower().Contains(searchLower)) || (i.Provider != null && i.Provider.ToLower().Contains(searchLower)))) &&
							(isActive == null || i.IsActive == isActive) &&
							(provider == null || i.Provider == provider) &&
							(createdFrom == null || i.CreatedAtUtc >= createdFrom.Value) &&
							(createdTo == null || i.CreatedAtUtc <= createdTo.Value));
					integrationsToExport = await query.ToListAsync();
				}

				return integrationsToExport;
			},
			filters: filters,
			columnMapper: MapIntegrationSettingToExportColumns
		);
	}

	private Dictionary<string, object> MapIntegrationSettingToExportColumns(IntegrationSetting integration)
	{
		return new Dictionary<string, object>
		{
			{ "Name", integration.Name ?? "" },
			{ "Description", integration.Description ?? "" },
			{ "Integration Type", integration.IntegrationType ?? "" },
			{ "Provider", integration.Provider ?? "" },
			{ "Configuration", integration.Configuration ?? "" },
			{ "Is Enabled", integration.IsEnabled ? "Yes" : "No" },
			{ "Is Active", integration.IsActive ? "Active" : "Inactive" }
		};
	}

	public async Task<ExportJobStatusDto?> GetIntegrationSettingExportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetExportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> DownloadIntegrationSettingExportFileAsync(string jobId)
	{
		return await _importExportService.DownloadExportFileAsync(jobId);
	}

	public async Task<string> StartIntegrationSettingImportJobAsync(Stream fileStream, string fileName, DuplicateHandlingStrategy duplicateStrategy = DuplicateHandlingStrategy.Skip)
	{
		var organizationId = _tenantService.GetOrganizationId();
		var userId = _tenantService.GetCurrentUserId();

		return await _importExportService.StartImportJobAsync<CreateIntegrationSettingDto>(
			entityType: "IntegrationSetting",
			fileStream: fileStream,
			fileName: fileName,
			rowProcessor: async (scopedUnitOfWork, rowData, dto) =>
			{
				if (!rowData.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
					return (false, "Name is required", false, false);

				var integrationRepo = scopedUnitOfWork.Repository<IntegrationSetting>();
				var existingIntegration = await integrationRepo.FindAsync(i => i.OrganizationId == organizationId && i.Name == name && !i.IsDeleted);

				var newIntegration = new IntegrationSetting
				{
					Id = Guid.NewGuid(),
					OrganizationId = organizationId,
					Name = name,
					Description = rowData.GetValueOrDefault("Description")?.ToString(),
					IntegrationType = rowData.GetValueOrDefault("Integration Type")?.ToString() ?? "API",
					Provider = rowData.GetValueOrDefault("Provider")?.ToString() ?? "",
					Configuration = rowData.GetValueOrDefault("Configuration")?.ToString(),
					IsEnabled = rowData.GetValueOrDefault("Is Enabled")?.ToString()?.Equals("Yes", StringComparison.OrdinalIgnoreCase) ?? false,
					IsActive = rowData.GetValueOrDefault("Is Active")?.ToString()?.Equals("Active", StringComparison.OrdinalIgnoreCase) ?? true,
					CreatedBy = userId.ToString(),
					CreatedAtUtc = DateTimeOffset.UtcNow,
					ModifiedBy = userId.ToString(),
					ModifiedAtUtc = DateTimeOffset.UtcNow
				};

				if (existingIntegration != null)
				{
					if (duplicateStrategy == DuplicateHandlingStrategy.Skip)
						return (false, "Integration Setting already exists", false, true);
					
					if (duplicateStrategy == DuplicateHandlingStrategy.Update)
					{
						existingIntegration.Description = newIntegration.Description;
						existingIntegration.IntegrationType = newIntegration.IntegrationType;
						existingIntegration.Provider = newIntegration.Provider;
						existingIntegration.Configuration = newIntegration.Configuration;
						existingIntegration.IsEnabled = newIntegration.IsEnabled;
						existingIntegration.IsActive = newIntegration.IsActive;
						existingIntegration.ModifiedBy = userId.ToString();
						existingIntegration.ModifiedAtUtc = DateTimeOffset.UtcNow;
						integrationRepo.Update(existingIntegration);
						return (true, null, true, false);
					}
				}

				await integrationRepo.AddAsync(newIntegration);
				return (true, null, false, false);
			},
			duplicateStrategy: duplicateStrategy
		);
	}

	public async Task<ImportJobStatusDto?> GetIntegrationSettingImportJobStatusAsync(string jobId)
	{
		return await _importExportService.GetImportJobStatusAsync(jobId);
	}

	public async Task<byte[]?> GetIntegrationSettingImportErrorReportAsync(string errorReportId)
	{
		return await _importExportService.GetImportErrorReportAsync(errorReportId);
	}

	public async Task<PagedResultDto<ImportExportHistoryDto>> GetIntegrationSettingImportExportHistoryAsync(ImportExportType? type, int page, int pageSize)
	{
		return await _importExportService.GetHistoryAsync("IntegrationSetting", type, page, pageSize);
	}

	// Integration Setting Filter Options
	public async Task<List<string>> GetIntegrationProvidersAsync()
	{
		var organizationId = _tenantService.GetOrganizationId();
		var integrationRepo = _unitOfWork.Repository<IntegrationSetting>();
		
		return await integrationRepo.GetQueryable()
			.Where(x => x.OrganizationId == organizationId && !x.IsDeleted && !string.IsNullOrEmpty(x.Provider))
			.Select(x => x.Provider!)
			.Distinct()
			.OrderBy(x => x)
			.ToListAsync();
	}

	// Helper method to resolve user names from IDs
	private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(List<Guid> userIds, Guid organizationId)
	{
		if (!userIds.Any()) return new Dictionary<Guid, string>();

		var userRepo = _unitOfWork.Repository<User>();
		var users = await userRepo.GetQueryable()
			.Where(u => userIds.Contains(u.Id) && u.OrganizationId == organizationId)
			.Select(u => new { u.Id, u.FullName })
			.ToListAsync();

		return users.ToDictionary(u => u.Id, u => u.FullName);
	}

	/// <summary>
	/// Invalidates all organization-related caches
	/// </summary>
	private async Task InvalidateOrganizationCachesAsync(Guid organizationId)
	{
		await _cacheService.RemoveCacheAsync($"organization:detail:{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"organizations:list:*");
		await _cacheService.RemoveCacheByPatternAsync($"organizations:dropdown:*");
		await _cacheService.RemoveCacheByPatternAsync($"organizations:stats:*");
	}

	/// <summary>
	/// Invalidates all location-related caches for an organization
	/// </summary>
	private async Task InvalidateLocationCachesAsync(Guid organizationId)
	{
		await _cacheService.RemoveCacheByPatternAsync($"locations:list:{organizationId}:*");
		await _cacheService.RemoveCacheByPatternAsync($"locations:list:{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"locations:dropdown:{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"locations:hierarchy:{organizationId}");
		await _cacheService.RemoveCacheByPatternAsync($"location:detail:*");
	}
}
